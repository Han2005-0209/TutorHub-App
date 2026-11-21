using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TutorHub.Domain.Enums;

namespace TutorHub.Api.Realtime;

/// <summary>
/// Quản lý trạng thái phòng học trực tuyến nội bộ theo thời gian thực (WebSocket Signaling cho WebRTC,
/// đồng bộ Bảng trắng 2 chiều và Trò chuyện trong buổi học). Không phụ thuộc dịch vụ bên thứ ba.
/// </summary>
public sealed class PhongHocRealtimeManager
{
    public const int GioiHanNetVe = 600;
    public const int GioiHanTinNhanLop = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ConcurrentDictionary<Guid, PhongHocTrangThai> _phongs = new();

    public PhongHocTrangThai LayHoacTaoPhong(Guid donDatLichId)
    {
        DonDepPhongNhanRoi();
        return _phongs.GetOrAdd(donDatLichId, id => new PhongHocTrangThai(id));
    }

    public PhongHocTrangThai? TimPhong(Guid donDatLichId)
        => _phongs.TryGetValue(donDatLichId, out var phong) ? phong : null;

    /// <summary>
    /// Thêm thành viên vào phòng; nếu người dùng đã có socket cũ (reload tab) thì trả về socket cũ để đóng gọn.
    /// </summary>
    public ThanhVienKetNoi? ThamGiaPhong(PhongHocTrangThai phong, ThanhVienKetNoi thanhVienMoi)
    {
        phong.CapNhatCuoiUtc = DateTime.UtcNow;
        ThanhVienKetNoi? cu = null;
        phong.ThanhViens.AddOrUpdate(
            thanhVienMoi.NguoiDungId,
            thanhVienMoi,
            (_, hienCo) =>
            {
                cu = hienCo;
                return thanhVienMoi;
            });
        return cu;
    }

    public bool RoiPhong(PhongHocTrangThai phong, Guid nguoiDungId, Guid phienId)
    {
        phong.CapNhatCuoiUtc = DateTime.UtcNow;
        if (phong.ThanhViens.TryGetValue(nguoiDungId, out var hienCo) && hienCo.PhienId == phienId)
        {
            return phong.ThanhViens.TryRemove(nguoiDungId, out _);
        }
        return false;
    }

    public bool ThemNetVe(PhongHocTrangThai phong, VaiTro vaiTro, JsonElement netVe)
    {
        lock (phong.LockTrangThai)
        {
            if (vaiTro != VaiTro.GIA_SU && !phong.ChoPhepHocVienVe)
            {
                return false;
            }

            if (phong.NetVe.Count >= GioiHanNetVe)
            {
                phong.NetVe.RemoveAt(0);
            }
            phong.NetVe.Add(netVe.Clone());
            phong.CapNhatCuoiUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool HoanTacNetVe(PhongHocTrangThai phong, VaiTro vaiTro)
    {
        lock (phong.LockTrangThai)
        {
            if (vaiTro != VaiTro.GIA_SU && !phong.ChoPhepHocVienVe)
            {
                return false;
            }

            if (phong.NetVe.Count == 0)
            {
                return false;
            }
            phong.NetVe.RemoveAt(phong.NetVe.Count - 1);
            phong.CapNhatCuoiUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool XoaBangTrang(PhongHocTrangThai phong, VaiTro vaiTro)
    {
        lock (phong.LockTrangThai)
        {
            if (vaiTro != VaiTro.GIA_SU && !phong.ChoPhepHocVienVe)
            {
                return false;
            }

            phong.NetVe.Clear();
            phong.CapNhatCuoiUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool DoiNenGiay(PhongHocTrangThai phong, VaiTro vaiTro, string nenGiay)
    {
        if (nenGiay is not ("grid" or "lines" or "plain"))
        {
            return false;
        }

        lock (phong.LockTrangThai)
        {
            if (vaiTro != VaiTro.GIA_SU && !phong.ChoPhepHocVienVe)
            {
                return false;
            }

            phong.NenGiay = nenGiay;
            phong.CapNhatCuoiUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool CapNhatQuyenVeHocVien(PhongHocTrangThai phong, VaiTro vaiTro, bool choPhep)
    {
        if (vaiTro != VaiTro.GIA_SU)
        {
            return false;
        }

        lock (phong.LockTrangThai)
        {
            phong.ChoPhepHocVienVe = choPhep;
            phong.CapNhatCuoiUtc = DateTime.UtcNow;
            return true;
        }
    }

    public bool CapNhatTrangThaiMoBangTrang(PhongHocTrangThai phong, VaiTro vaiTro, bool dangMo)
    {
        if (vaiTro != VaiTro.GIA_SU)
        {
            return false;
        }

        lock (phong.LockTrangThai)
        {
            phong.BangTrangDangMo = dangMo;
            phong.CapNhatCuoiUtc = DateTime.UtcNow;
            return true;
        }
    }

    public TinNhanTrongPhongDto ThemTinNhanLop(PhongHocTrangThai phong, Guid nguoiGuiId, string hoTen, VaiTro vaiTro, string noiDung)
    {
        var noiDungSach = noiDung.Trim();
        if (noiDungSach.Length > 1000)
        {
            noiDungSach = noiDungSach[..1000];
        }

        var msg = new TinNhanTrongPhongDto(
            Id: Guid.NewGuid(),
            FromUserId: nguoiGuiId,
            HoTen: hoTen,
            VaiTro: vaiTro,
            NoiDung: noiDungSach,
            GuiLucUtc: DateTime.UtcNow);

        lock (phong.LockTrangThai)
        {
            if (phong.LichSuChat.Count >= GioiHanTinNhanLop)
            {
                phong.LichSuChat.RemoveAt(0);
            }
            phong.LichSuChat.Add(msg);
            phong.CapNhatCuoiUtc = DateTime.UtcNow;
        }

        return msg;
    }

    public object TaoAnhChupBangTrang(PhongHocTrangThai phong)
    {
        lock (phong.LockTrangThai)
        {
            return new
            {
                dangMo = phong.BangTrangDangMo,
                nenGiay = phong.NenGiay,
                choPhepHocVienVe = phong.ChoPhepHocVienVe,
                strokes = phong.NetVe.ToList()
            };
        }
    }

    public IReadOnlyList<TinNhanTrongPhongDto> LayLichSuChat(PhongHocTrangThai phong)
    {
        lock (phong.LockTrangThai)
        {
            return phong.LichSuChat.ToList();
        }
    }

    public static async Task GuiJsonAsync(ThanhVienKetNoi tv, object duLieu, CancellationToken ct)
    {
        if (tv.Socket is null || tv.Socket.State != WebSocketState.Open)
        {
            return;
        }

        var json = JsonSerializer.Serialize(duLieu, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await tv.GuiLock.WaitAsync(ct);
        try
        {
            if (tv.Socket.State == WebSocketState.Open)
            {
                await tv.Socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: ct);
            }
        }
        catch (WebSocketException)
        {
            // Bỏ qua nếu kết nối phía client đã đóng đột ngột
        }
        catch (OperationCanceledException)
        {
            // Bỏ qua khi hủy kết nối
        }
        finally
        {
            tv.GuiLock.Release();
        }
    }

    public static async Task PhatToiPhongAsync(
        PhongHocTrangThai phong,
        object duLieu,
        Guid? boQuaNguoiDungId = null,
        CancellationToken ct = default)
    {
        var ds = phong.ThanhViens.Values
            .Where(t => !boQuaNguoiDungId.HasValue || t.NguoiDungId != boQuaNguoiDungId.Value)
            .ToList();

        foreach (var tv in ds)
        {
            await GuiJsonAsync(tv, duLieu, ct);
        }
    }

    private void DonDepPhongNhanRoi()
    {
        var nguong = DateTime.UtcNow.AddMinutes(-60);
        foreach (var kvp in _phongs)
        {
            if (kvp.Value.ThanhViens.IsEmpty && kvp.Value.CapNhatCuoiUtc < nguong)
            {
                _phongs.TryRemove(kvp.Key, out _);
            }
        }
    }
}

public sealed class PhongHocTrangThai(Guid donDatLichId)
{
    public Guid DonDatLichId { get; } = donDatLichId;
    public ConcurrentDictionary<Guid, ThanhVienKetNoi> ThanhViens { get; } = new();
    public object LockTrangThai { get; } = new();
    public List<JsonElement> NetVe { get; } = [];
    public bool BangTrangDangMo { get; set; }
    public string NenGiay { get; set; } = "grid";
    public bool ChoPhepHocVienVe { get; set; }
    public List<TinNhanTrongPhongDto> LichSuChat { get; } = [];
    public DateTime CapNhatCuoiUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ThanhVienKetNoi
{
    public required Guid NguoiDungId { get; init; }
    public required string HoTen { get; init; }
    public required VaiTro VaiTro { get; init; }
    public required Guid PhienId { get; init; }
    public WebSocket? Socket { get; init; }
    public SemaphoreSlim GuiLock { get; } = new(1, 1);
    public bool CameraOn { get; set; }
    public bool MicOn { get; set; }
    public bool ScreenOn { get; set; }

    public object ToPeerDto() => new
    {
        nguoiDungId = NguoiDungId,
        hoTen = HoTen,
        vaiTro = VaiTro,
        cameraOn = CameraOn,
        micOn = MicOn,
        screenOn = ScreenOn
    };
}

public sealed record TinNhanTrongPhongDto(
    Guid Id,
    Guid FromUserId,
    string HoTen,
    VaiTro VaiTro,
    string NoiDung,
    DateTime GuiLucUtc);
