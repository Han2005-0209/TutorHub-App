using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Enums;

namespace TutorHub.Api.Realtime;

/// <summary>
/// Endpoint WebSocket nội bộ (`/ws/phong-hoc/{donId}`) cho Phòng học trực tuyến TutorHub:
/// - Xác thực ngay bằng JWT TutorHub hiện tại (không cần đăng nhập dịch vụ ngoài).
/// - Ghi nhận giờ vào/nhịp duy trì/rời phòng chính xác theo giờ máy chủ vào `NhatKyPhongHoc`.
/// - Chuyển tiếp tín hiệu WebRTC (offer/answer/ICE) cho Video/Audio/Chia sẻ màn hình P2P.
/// - Đồng bộ Bảng trắng / Giấy nháp 2 chiều và Trò chuyện trong buổi học.
/// </summary>
public static class PhongHocWebSocketEndpoint
{
    private const int KichThuocTinNhanToiDaBytes = 256 * 1024;

    public static async Task HandleAsync(HttpContext context, Guid donId)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new PhanHoiLoiApi(new ThongTinLoiDto(
                Ma: "YEU_CAU_WEBSOCKET",
                ThongBao: "Đường dẫn này chỉ chấp nhận kết nối WebSocket.",
                ChiTiet: null,
                MaYeuCau: context.TraceIdentifier)));
            return;
        }

        var userId = context.User.LayNguoiDungId();
        var vaiTro = context.User.LayVaiTro();
        if (vaiTro != VaiTro.GIA_SU && vaiTro != VaiTro.HOC_SINH)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var ct = context.RequestAborted;
        var viService = context.RequestServices.GetRequiredService<IViVaTaiChinhService>();
        var roomManager = context.RequestServices.GetRequiredService<PhongHocRealtimeManager>();

        ThongTinPhongHocDto thongTin;
        PhienPhongHocKetQuaDto phienKq;
        try
        {
            thongTin = await viService.LayThongTinPhongHocAsync(userId, vaiTro, donId, null, ct);
            phienKq = await viService.VaoPhongHocAsync(userId, vaiTro, donId, null, ct);
        }
        catch (LoiUngDungException ex)
        {
            context.Response.StatusCode = ex.MaHttp;
            await context.Response.WriteAsJsonAsync(new PhanHoiLoiApi(new ThongTinLoiDto(
                Ma: ex.MaLoi,
                ThongBao: ex.Message,
                ChiTiet: null,
                MaYeuCau: context.TraceIdentifier)));
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var hoTen = vaiTro == VaiTro.GIA_SU ? thongTin.TenGiaSu : thongTin.TenHocSinh;
        var phong = roomManager.LayHoacTaoPhong(donId);

        var thanhVien = new ThanhVienKetNoi
        {
            NguoiDungId = userId,
            HoTen = hoTen,
            VaiTro = vaiTro,
            PhienId = phienKq.PhienId,
            Socket = webSocket
        };

        var phienCu = roomManager.ThamGiaPhong(phong, thanhVien);
        if (phienCu?.Socket is { State: WebSocketState.Open })
        {
            try
            {
                await phienCu.Socket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "Bạn đã mở phòng học ở một tab khác.",
                    CancellationToken.None);
            }
            catch
            {
                // Bỏ qua nếu socket cũ đã ngắt
            }
        }

        try
        {
            // 1. Gửi trạng thái khởi tạo phòng cho thành viên vừa vào (peers hiện có, bảng trắng, lịch sử chat, điểm danh)
            var peersHienCo = phong.ThanhViens.Values
                .Where(t => t.NguoiDungId != userId)
                .Select(t => t.ToPeerDto())
                .ToList();

            await PhongHocRealtimeManager.GuiJsonAsync(thanhVien, new
            {
                type = "room_ready",
                phienId = phienKq.PhienId,
                me = thanhVien.ToPeerDto(),
                peers = peersHienCo,
                nhatKy = phienKq.NhatKy,
                whiteboard = roomManager.TaoAnhChupBangTrang(phong),
                chatHistory = roomManager.LayLichSuChat(phong)
            }, ct);

            // 2. Báo cho đối tác trong phòng biết thành viên vừa vào + cập nhật điểm danh mới nhất
            await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
            {
                type = "peer_joined",
                peer = thanhVien.ToPeerDto(),
                nhatKy = phienKq.NhatKy
            }, boQuaNguoiDungId: userId, ct);

            // 3. Vòng lặp nhận thông điệp WebSocket
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var (msgType, payloadText) = await NhanTinNhanDayDuAsync(webSocket, ct);
                if (msgType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (msgType != WebSocketMessageType.Text || string.IsNullOrWhiteSpace(payloadText))
                {
                    continue;
                }

                await XuLyThongDiepAsync(
                    context.RequestServices,
                    roomManager,
                    phong,
                    thanhVien,
                    payloadText,
                    ct);
            }
        }
        catch (WebSocketException)
        {
            // Kết nối client ngắt đột ngột — dọn dẹp tại khối finally
        }
        catch (OperationCanceledException)
        {
            // Request bị hủy — dọn dẹp tại khối finally
        }
        finally
        {
            var daGo = roomManager.RoiPhong(phong, userId, phienKq.PhienId);
            TomTatNhatKyPhongHocDto? nhatKySauRoi = null;
            try
            {
                using var scope = context.RequestServices.CreateScope();
                var scopedService = scope.ServiceProvider.GetRequiredService<IViVaTaiChinhService>();
                nhatKySauRoi = await scopedService.CapNhatNhipPhongHocAsync(
                    userId,
                    donId,
                    new NhipPhongHocYeuCauDto(phienKq.PhienId, RoiPhong: true),
                    null,
                    CancellationToken.None);
            }
            catch
            {
                // Không làm sập tiến trình dọn dẹp nếu DB tạm thời bận
            }

            if (daGo)
            {
                await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                {
                    type = "peer_left",
                    nguoiDungId = userId,
                    vaiTro,
                    hoTen,
                    nhatKy = nhatKySauRoi
                }, boQuaNguoiDungId: userId, CancellationToken.None);
            }

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Đã rời phòng học.",
                        CancellationToken.None);
                }
                catch
                {
                    // Bỏ qua lỗi đóng socket
                }
            }
        }
    }

    private static async Task XuLyThongDiepAsync(
        IServiceProvider serviceProvider,
        PhongHocRealtimeManager roomManager,
        PhongHocTrangThai phong,
        ThanhVienKetNoi gui,
        string payloadText,
        CancellationToken ct)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(payloadText);
        }
        catch (JsonException)
        {
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
            {
                return;
            }

            var type = typeProp.GetString();
            switch (type)
            {
                case "webrtc_offer":
                case "webrtc_answer":
                case "webrtc_ice":
                {
                    if (!root.TryGetProperty("payload", out var sigPayload))
                    {
                        return;
                    }
                    await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                    {
                        type,
                        fromUserId = gui.NguoiDungId,
                        payload = sigPayload.Clone()
                    }, boQuaNguoiDungId: gui.NguoiDungId, ct);
                    break;
                }

                case "media_state":
                {
                    if (root.TryGetProperty("cameraOn", out var cam) && cam.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        gui.CameraOn = cam.GetBoolean();
                    if (root.TryGetProperty("micOn", out var mic) && mic.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        gui.MicOn = mic.GetBoolean();
                    if (root.TryGetProperty("screenOn", out var scr) && scr.ValueKind is JsonValueKind.True or JsonValueKind.False)
                        gui.ScreenOn = scr.GetBoolean();

                    await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                    {
                        type = "media_state",
                        fromUserId = gui.NguoiDungId,
                        cameraOn = gui.CameraOn,
                        micOn = gui.MicOn,
                        screenOn = gui.ScreenOn
                    }, boQuaNguoiDungId: gui.NguoiDungId, ct);
                    break;
                }

                case "wb_add":
                {
                    if (!root.TryGetProperty("stroke", out var strokeEl))
                    {
                        return;
                    }
                    if (!roomManager.ThemNetVe(phong, gui.VaiTro, strokeEl))
                    {
                        await PhongHocRealtimeManager.GuiJsonAsync(gui, new
                        {
                            type = "error",
                            ma = "BANG_TRANG_BI_KHOA",
                            thongBao = "Gia sư đang khóa quyền vẽ trên bảng trắng."
                        }, ct);
                        return;
                    }
                    await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                    {
                        type = "wb_add",
                        fromUserId = gui.NguoiDungId,
                        stroke = strokeEl.Clone()
                    }, boQuaNguoiDungId: gui.NguoiDungId, ct);
                    break;
                }

                case "wb_undo":
                {
                    if (roomManager.HoanTacNetVe(phong, gui.VaiTro))
                    {
                        await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                        {
                            type = "wb_undo",
                            fromUserId = gui.NguoiDungId
                        }, boQuaNguoiDungId: gui.NguoiDungId, ct);
                    }
                    break;
                }

                case "wb_clear":
                {
                    if (roomManager.XoaBangTrang(phong, gui.VaiTro))
                    {
                        await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                        {
                            type = "wb_clear",
                            fromUserId = gui.NguoiDungId
                        }, boQuaNguoiDungId: gui.NguoiDungId, ct);
                    }
                    break;
                }

                case "wb_bg":
                {
                    var bgMode = root.TryGetProperty("bgMode", out var bgEl) ? bgEl.GetString() ?? "" : "";
                    if (roomManager.DoiNenGiay(phong, gui.VaiTro, bgMode))
                    {
                        await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                        {
                            type = "wb_bg",
                            fromUserId = gui.NguoiDungId,
                            bgMode
                        }, boQuaNguoiDungId: gui.NguoiDungId, ct);
                    }
                    break;
                }

                case "wb_open":
                {
                    if (root.TryGetProperty("dangMo", out var openEl)
                        && openEl.ValueKind is JsonValueKind.True or JsonValueKind.False
                        && roomManager.CapNhatTrangThaiMoBangTrang(phong, gui.VaiTro, openEl.GetBoolean()))
                    {
                        await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                        {
                            type = "wb_open",
                            dangMo = openEl.GetBoolean()
                        }, boQuaNguoiDungId: null, ct);
                    }
                    break;
                }

                case "wb_perm":
                {
                    if (root.TryGetProperty("choPhepHocVienVe", out var pEl)
                        && pEl.ValueKind is JsonValueKind.True or JsonValueKind.False
                        && roomManager.CapNhatQuyenVeHocVien(phong, gui.VaiTro, pEl.GetBoolean()))
                    {
                        await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                        {
                            type = "wb_perm",
                            choPhepHocVienVe = pEl.GetBoolean()
                        }, boQuaNguoiDungId: null, ct);
                    }
                    break;
                }

                case "chat_msg":
                {
                    var noiDung = root.TryGetProperty("noiDung", out var ndEl) ? ndEl.GetString() ?? "" : "";
                    if (string.IsNullOrWhiteSpace(noiDung))
                    {
                        return;
                    }
                    var msg = roomManager.ThemTinNhanLop(phong, gui.NguoiDungId, gui.HoTen, gui.VaiTro, noiDung);
                    await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                    {
                        type = "chat_msg",
                        message = msg
                    }, boQuaNguoiDungId: null, ct);
                    break;
                }

                case "heartbeat":
                {
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var scopedService = scope.ServiceProvider.GetRequiredService<IViVaTaiChinhService>();
                        var nhatKy = await scopedService.CapNhatNhipPhongHocAsync(
                            gui.NguoiDungId,
                            phong.DonDatLichId,
                            new NhipPhongHocYeuCauDto(gui.PhienId, RoiPhong: false),
                            null,
                            ct);
                        await PhongHocRealtimeManager.PhatToiPhongAsync(phong, new
                        {
                            type = "attendance_update",
                            nhatKy
                        }, boQuaNguoiDungId: null, ct);
                    }
                    catch
                    {
                        // Bỏ qua lỗi nhịp tạm thời
                    }
                    break;
                }
            }
        }
    }

    private static async Task<(WebSocketMessageType MessageType, string? Text)> NhanTinNhanDayDuAsync(
        WebSocket webSocket,
        CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return (WebSocketMessageType.Close, null);
            }

            if (ms.Length + result.Count > KichThuocTinNhanToiDaBytes)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    "Gói tin vượt quá giới hạn cho phép.",
                    CancellationToken.None);
                return (WebSocketMessageType.Close, null);
            }

            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return (result.MessageType, Encoding.UTF8.GetString(ms.ToArray()));
    }
}
