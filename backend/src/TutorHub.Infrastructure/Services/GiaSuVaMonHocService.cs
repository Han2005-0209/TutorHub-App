using Microsoft.EntityFrameworkCore;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Infrastructure.Persistence;

namespace TutorHub.Infrastructure.Services;

public sealed class GiaSuVaMonHocService : IGiaSuVaMonHocService
{
    private readonly AppDbContext _db;
    private readonly ILuuTruTepService _luuTruTep;
    private readonly IKiemDuyetNoiDungService _kiemDuyet;

    public GiaSuVaMonHocService(
        AppDbContext db,
        ILuuTruTepService luuTruTep,
        IKiemDuyetNoiDungService kiemDuyet)
    {
        _db = db;
        _luuTruTep = luuTruTep;
        _kiemDuyet = kiemDuyet;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 1. DANH MỤC MÔN HỌC & ĐẶC TẢ RÕ RÀNG 2 HÌNH THỨC HỌC (TRỰC TUYẾN VS. TRỰC TIẾP)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<MonHocDto>> LayDsMonHocAsync(CancellationToken cancellationToken = default)
    {
        var ds = await _db.MonHocs
            .AsNoTracking()
            .Where(m => m.DangHoatDong)
            .OrderBy(m => m.Ten)
            .ToListAsync(cancellationToken);

        return ds.Select(m => new MonHocDto(m.Id, m.Ten, m.Slug, m.DangHoatDong)).ToList();
    }

    public IReadOnlyList<ThongTinHinhThucHocDto> LayThongTinHinhThucHoc() =>
    [
        new ThongTinHinhThucHocDto(
            Ma: HinhThucBuoiHoc.TRUC_TUYEN,
            TenHienThi: "Học Trực tuyến (Online 1-1)",
            KhauHieu: "Học mọi lúc, mọi nơi — Không giới hạn khoảng cách địa lý",
            MoTaNgan: "Kết nối trực tiếp 1-1 với gia sư trên toàn quốc qua phòng học video tích hợp sẵn trên trình duyệt, chia sẻ màn hình và bảng trắng trực quan.",
            PhamViDiaLy: "Toàn quốc — Không giới hạn tỉnh thành hay bán kính di chuyển",
            KhongGianHoc: "Phòng học video trực tuyến tích hợp, tự động tạo link khi gia sư xác nhận lịch và mở trước giờ học 15 phút",
            ChinhSachLienHe: "Bảo mật tuyệt đối số điện thoại cá nhân; trao đổi bài học qua khung nhắn tin an toàn trong hệ thống",
            ApDungBanKinhDiChuyen: false,
            CoPhongHocVideoTichHop: true,
            UuDiemNoiBat:
            [
                "Tiếp cận gia sư giỏi và chuyên gia luyện thi trên toàn quốc mà không cần ở cùng khu vực",
                "Tiết kiệm 100% thời gian di chuyển, dễ dàng sắp xếp lịch học linh hoạt buổi tối hoặc cuối tuần",
                "Phòng học video tự mở đúng giờ, không cần cài đặt phần mềm phức tạp",
                "Học phí được bảo vệ tạm giữ an toàn và chỉ giải ngân sau khi hoàn thành buổi học"
            ],
            YeuCauChuanBi:
            [
                "Máy tính, máy tính bảng hoặc điện thoại có camera, micro và đường truyền Internet ổn định",
                "Không gian yên tĩnh, tai nghe để trao đổi rõ ràng với gia sư"
            ]),
        new ThongTinHinhThucHocDto(
            Ma: HinhThucBuoiHoc.TRUC_TIEP,
            TenHienThi: "Học Trực tiếp tại nhà (Offline 1-1)",
            KhauHieu: "Gia sư đến tận nhà kèm sát — Theo dõi trực tiếp từng bài tập",
            MoTaNgan: "Gia sư di chuyển tới nhà học sinh hoặc địa điểm học đã thống nhất trong phạm vi khu vực và bán kính phục vụ đã đăng ký.",
            PhamViDiaLy: "Giới hạn trong khu vực hoạt động và bán kính phục vụ (km) của từng gia sư",
            KhongGianHoc: "Tại nhà học sinh hoặc địa điểm yên tĩnh đã thống nhất trong khu vực phục vụ",
            ChinhSachLienHe: "Số điện thoại được bảo mật ở trạng thái chờ và chỉ mở hiển thị sau khi đơn đặt lịch đã được xác nhận để tiện liên hệ dẫn đường",
            ApDungBanKinhDiChuyen: true,
            CoPhongHocVideoTichHop: false,
            UuDiemNoiBat:
            [
                "Tương tác trực diện giúp gia sư kèm sát vở viết, chỉnh sửa từng bước làm bài trên giấy",
                "Phù hợp đặc biệt với học sinh tiểu học, THCS hoặc học sinh cần sự giám sát trực tiếp từ gia sư & phụ huynh",
                "Hiển thị trực quan khu vực hoạt động và vòng tròn bán kính phục vụ trên bản đồ",
                "Hồ sơ và bằng cấp gia sư đã qua kiểm duyệt chặt chẽ trước khi nhận lớp"
            ],
            YeuCauChuanBi:
            [
                "Chọn gia sư có khu vực và bán kính phục vụ bao phủ địa chỉ học của bạn",
                "Chuẩn bị bàn học yên tĩnh, sách giáo khoa và tài liệu bài tập tại nhà"
            ])
    ];

    public static string TaoMoTaHinhThucDay(GiaSu giaSu)
    {
        if (giaSu.DayTrucTuyen && giaSu.DayTrucTiep)
        {
            var khuVuc = string.IsNullOrWhiteSpace(giaSu.KhuVuc) ? "khu vực hoạt động" : giaSu.KhuVuc;
            return giaSu.BanKinhPhucVuKm > 0
                ? $"Dạy trực tuyến toàn quốc & trực tiếp tại {khuVuc} (bán kính {giaSu.BanKinhPhucVuKm} km)"
                : $"Dạy trực tuyến toàn quốc & trực tiếp tại {khuVuc}";
        }

        if (giaSu.DayTrucTiep)
        {
            var khuVuc = string.IsNullOrWhiteSpace(giaSu.KhuVuc) ? "khu vực hoạt động" : giaSu.KhuVuc;
            return giaSu.BanKinhPhucVuKm > 0
                ? $"Chỉ dạy trực tiếp tại {khuVuc} (bán kính {giaSu.BanKinhPhucVuKm} km)"
                : $"Chỉ dạy trực tiếp tại {khuVuc}";
        }

        return "Chỉ dạy trực tuyến toàn quốc qua phòng học video tích hợp";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. MODULE 2 — HỒ SƠ GIA SƯ & GIẤY TỜ CỦA TÔI (GIA_SU)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<HoSoGiaSuToiDto> LayHoSoGiaSuToiAsync(Guid giaSuId, CancellationToken cancellationToken = default)
    {
        var giaSu = await NapGiaSuDayDuAsync(giaSuId, asNoTracking: true, cancellationToken);
        return AnhXaHoSoGiaSuToiDto(giaSu);
    }

    public async Task<HoSoGiaSuToiDto> CapNhatHoSoGiaSuToiAsync(
        Guid giaSuId,
        CapNhatHoSoGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        if (yeuCau.SoNamKinhNghiem < 0 || yeuCau.SoNamKinhNghiem > 60)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Số năm kinh nghiệm phải từ 0 đến 60.", 400);
        }

        if (yeuCau.HinhThucDay is null || yeuCau.HinhThucDay.Count == 0)
        {
            throw new LoiUngDungException(
                "DU_LIEU_KHONG_HOP_LE",
                "Phải chọn ít nhất 1 hình thức dạy (Trực tuyến hoặc Trực tiếp tại nhà).",
                400);
        }

        var coDayTrucTuyen = yeuCau.HinhThucDay.Contains(HinhThucBuoiHoc.TRUC_TUYEN);
        if (yeuCau.CoHocThu && !coDayTrucTuyen)
        {
            throw new LoiUngDungException(
                "HOC_THU_CHI_ONLINE",
                "Ưu đãi buổi học thử miễn phí chỉ áp dụng khi bạn bật hình thức dạy trực tuyến (Online).",
                422);
        }

        var coDayTrucTiep = yeuCau.HinhThucDay.Contains(HinhThucBuoiHoc.TRUC_TIEP);
        if (coDayTrucTiep)
        {
            if (string.IsNullOrWhiteSpace(yeuCau.KhuVuc))
            {
                throw new LoiUngDungException(
                    "DU_LIEU_KHONG_HOP_LE",
                    "Khi nhận dạy trực tiếp tại nhà, bạn bắt buộc phải nhập khu vực hoạt động (Quận/Huyện, Tỉnh/Thành).",
                    400,
                    [new ChiTietLoiTruongDto("khuVuc", "Vui lòng nhập khu vực hoạt động cho hình thức dạy trực tiếp.")]);
            }

            if (yeuCau.BanKinhPhucVuKm < 1 || yeuCau.BanKinhPhucVuKm > 100)
            {
                throw new LoiUngDungException(
                    "DU_LIEU_KHONG_HOP_LE",
                    "Khi nhận dạy trực tiếp tại nhà, bán kính phục vụ phải từ 1 đến 100 km.",
                    400,
                    [new ChiTietLoiTruongDto("banKinhPhucVuKm", "Bán kính di chuyển dạy trực tiếp phải từ 1 đến 100 km.")]);
            }
        }

        // Kiểm duyệt nội dung đầu vào: chặn ngôn từ không phù hợp và thông tin liên hệ ngoài luồng
        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe(
        [
            ("hoTen", yeuCau.HoTen, true),
            ("tieuDe", yeuCau.TieuDe, true),
            ("gioiThieu", yeuCau.GioiThieu, true),
            ("hocVan", yeuCau.HocVan, true)
        ]);

        var giaSu = await NapGiaSuDayDuAsync(giaSuId, asNoTracking: false, cancellationToken);

        if (!string.IsNullOrWhiteSpace(yeuCau.HoTen))
        {
            giaSu.HoTen = yeuCau.HoTen.Trim();
        }

        if (yeuCau.SoDienThoai is not null)
        {
            giaSu.SoDienThoai = string.IsNullOrWhiteSpace(yeuCau.SoDienThoai) ? null : yeuCau.SoDienThoai.Trim();
        }

        if (yeuCau.KhuVuc is not null)
        {
            giaSu.KhuVuc = string.IsNullOrWhiteSpace(yeuCau.KhuVuc) ? null : yeuCau.KhuVuc.Trim();
        }

        giaSu.TieuDe = (yeuCau.TieuDe ?? string.Empty).Trim();
        giaSu.GioiThieu = (yeuCau.GioiThieu ?? string.Empty).Trim();
        giaSu.HocVan = (yeuCau.HocVan ?? string.Empty).Trim();
        giaSu.SoNamKinhNghiem = yeuCau.SoNamKinhNghiem;
        giaSu.DatHinhThucDay(yeuCau.HinhThucDay);
        // Chính sách học thử: chỉ áp dụng Online miễn phí (0 ₫, 60p)
        giaSu.CoHocThu = giaSu.DayTrucTuyen && yeuCau.CoHocThu;

        // Nếu chỉ dạy trực tuyến thì không áp dụng bán kính di chuyển tại nhà (đặt 0)
        giaSu.BanKinhPhucVuKm = giaSu.DayTrucTiep
            ? Math.Clamp(yeuCau.BanKinhPhucVuKm, 1, 100)
            : 0;

        // Cập nhật tài khoản ngân hàng đã tách riêng sang PUT /api/gia-su/toi/ngan-hang (yêu cầu mật khẩu + AuditLog)
        await _db.SaveChangesAsync(cancellationToken);
        return await LayHoSoGiaSuToiAsync(giaSuId, cancellationToken);
    }

    public async Task<HoSoGiaSuToiDto> GuiDuyetHoSoAsync(Guid giaSuId, CancellationToken cancellationToken = default)
    {
        var giaSu = await NapGiaSuDayDuAsync(giaSuId, asNoTracking: false, cancellationToken);

        if (giaSu.TrangThaiHoSo is not (TrangThaiHoSoGiaSu.NHAP or TrangThaiHoSoGiaSu.TU_CHOI))
        {
            throw new LoiUngDungException(
                "CHUYEN_TRANG_THAI_SAI",
                "Hồ sơ hiện đang ở trạng thái chờ duyệt hoặc đã được duyệt, không thể gửi duyệt lại.",
                409);
        }

        // Kiểm duyệt lại toàn bộ nội dung hồ sơ trước khi gửi duyệt
        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe(
        [
            ("hoTen", giaSu.HoTen, true),
            ("tieuDe", giaSu.TieuDe, true),
            ("gioiThieu", giaSu.GioiThieu, true),
            ("hocVan", giaSu.HocVan, true)
        ]);

        // Kiểm tra 7 điều kiện bắt buộc để Gia sư gửi duyệt hồ sơ
        var chiTietThieu = new List<ChiTietLoiTruongDto>();

        if (giaSu.TieuDe.Trim().Length < 10)
        {
            chiTietThieu.Add(new ChiTietLoiTruongDto("tieuDe", "Tiêu đề hồ sơ phải có ít nhất 10 ký tự."));
        }

        if (giaSu.GioiThieu.Trim().Length < 50)
        {
            chiTietThieu.Add(new ChiTietLoiTruongDto("gioiThieu", "Phần giới thiệu bản thân phải có ít nhất 50 ký tự."));
        }

        if (giaSu.HocVan.Trim().Length < 2)
        {
            chiTietThieu.Add(new ChiTietLoiTruongDto("hocVan", "Trình độ học vấn phải có ít nhất 2 ký tự."));
        }

        if (string.IsNullOrWhiteSpace(giaSu.KhuVuc))
        {
            chiTietThieu.Add(new ChiTietLoiTruongDto("khuVuc", "Khu vực cư trú/giảng dạy (Quận/Huyện, Tỉnh/Thành) không được để trống."));
        }

        if (giaSu.DayTrucTiep && giaSu.BanKinhPhucVuKm < 1)
        {
            chiTietThieu.Add(new ChiTietLoiTruongDto("banKinhPhucVuKm", "Khi nhận dạy trực tiếp tại nhà, bán kính phục vụ phải từ 1 km trở lên."));
        }

        if (giaSu.DsMonDay.Count < 1)
        {
            chiTietThieu.Add(new ChiTietLoiTruongDto("dsMonDay", "Phải khai báo ít nhất 1 môn dạy kèm học phí."));
        }

        if (giaSu.DsGiayTo.Count < 1)
        {
            chiTietThieu.Add(new ChiTietLoiTruongDto("dsGiayTo", "Phải tải lên ít nhất 1 giấy tờ xác minh (bằng cấp, chứng chỉ hoặc căn cước)."));
        }

        if (chiTietThieu.Count > 0)
        {
            throw new LoiUngDungException(
                "HO_SO_CHUA_DAY_DU",
                "Hồ sơ gia sư chưa đáp ứng đầy đủ các điều kiện bắt buộc để gửi duyệt.",
                422,
                chiTietThieu);
        }

        giaSu.TrangThaiHoSo = TrangThaiHoSoGiaSu.CHO_DUYET;
        giaSu.LyDoTuChoi = null;

        await _db.SaveChangesAsync(cancellationToken);
        return AnhXaHoSoGiaSuToiDto(giaSu);
    }

    public async Task<IReadOnlyList<GiayToGiaSuDto>> LayDsGiayToToiAsync(Guid giaSuId, CancellationToken cancellationToken = default)
    {
        var ds = await _db.GiayToGiaSus
            .AsNoTracking()
            .Include(g => g.NguoiDuyet)
            .Where(g => g.GiaSuId == giaSuId)
            .OrderByDescending(g => g.DuyetLuc)
            .ToListAsync(cancellationToken);

        return ds.Select(AnhXaGiayToDto).ToList();
    }

    public async Task<GiayToGiaSuDto> TaiLenGiayToAsync(
        Guid giaSuId,
        LoaiGiayTo loai,
        string tenTepGoc,
        Stream luongTep,
        long kichThuocByte,
        CancellationToken cancellationToken = default)
    {
        if (kichThuocByte <= 0)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Tệp tải lên rỗng.", 400);
        }

        if (kichThuocByte > 5 * 1024 * 1024)
        {
            throw new LoiUngDungException("TEP_QUA_LON", "Dung lượng tệp giấy tờ không được vượt quá 5MB.", 413);
        }

        var ext = Path.GetExtension(tenTepGoc).ToLowerInvariant();
        var allowedExts = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            throw new LoiUngDungException(
                "LOAI_TEP_KHONG_HOP_LE",
                "Chỉ chấp nhận tệp định dạng PDF, JPG, PNG hoặc WebP.",
                415);
        }

        var coGiaSu = await _db.GiaSus.AnyAsync(g => g.Id == giaSuId, cancellationToken);
        if (!coGiaSu)
        {
            throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);
        }

        var duongDanTep = await _luuTruTep.LuuTepAsync(
            luongTep,
            tenTepGoc,
            $"giay-to-gia-su/{giaSuId:N}",
            laKhoPrivate: true,
            cancellationToken);

        var giayTo = new GiayToGiaSu
        {
            Id = Guid.NewGuid(),
            GiaSuId = giaSuId,
            Loai = loai,
            DuongDanTep = duongDanTep,
            TenTepGoc = Path.GetFileName(tenTepGoc),
            TrangThai = TrangThaiGiayTo.CHO_DUYET
        };

        _db.GiayToGiaSus.Add(giayTo);
        await _db.SaveChangesAsync(cancellationToken);

        return AnhXaGiayToDto(giayTo);
    }

    public async Task XoaGiayToAsync(Guid giaSuId, Guid giayToId, CancellationToken cancellationToken = default)
    {
        var giayTo = await _db.GiayToGiaSus
            .FirstOrDefaultAsync(g => g.Id == giayToId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy giấy tờ.", 404);

        if (giayTo.GiaSuId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền xóa giấy tờ của gia sư khác.", 403);
        }

        await _luuTruTep.XoaTepAsync(giayTo.DuongDanTep, laKhoPrivate: true, cancellationToken);
        _db.GiayToGiaSus.Remove(giayTo);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UrlXemGiayToDto> TaoUrlXemGiayToAsync(
        Guid nguoiYeuCauId,
        VaiTro vaiTroNguoiYeuCau,
        Guid giayToId,
        CancellationToken cancellationToken = default)
    {
        var giayTo = await _db.GiayToGiaSus
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == giayToId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy giấy tờ.", 404);

        if (vaiTroNguoiYeuCau != VaiTro.QUAN_TRI && giayTo.GiaSuId != nguoiYeuCauId)
        {
            throw new LoiUngDungException(
                "KHONG_CO_QUYEN",
                "Chỉ gia sư chủ sở hữu hoặc quản trị viên mới được phép xem tệp giấy tờ xác minh.",
                403);
        }

        var thoiHan = TimeSpan.FromMinutes(10);
        var urlXem = _luuTruTep.TaoSignedUrlXemTep(giayTo.DuongDanTep, thoiHan);

        return new UrlXemGiayToDto(
            GiayToId: giayTo.Id,
            TenTepGoc: giayTo.TenTepGoc,
            Loai: giayTo.Loai,
            UrlXem: urlXem,
            HetHanLuc: DateTime.UtcNow.Add(thoiHan));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. MODULE 3 — CẤU HÌNH MÔN DẠY & LỊCH RẢNH CỦA TÔI (GIA_SU)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<GiaSuMonHocDto>> CapNhatDsMonDayAsync(
        Guid giaSuId,
        CapNhatDsMonDayYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var dsYeuCau = yeuCau.DsMonDay ?? [];
        if (dsYeuCau.Count == 0)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Danh sách môn dạy không được để trống.", 400);
        }

        var trungLap = dsYeuCau
            .GroupBy(x => x.MonHocId)
            .Any(g => g.Count() > 1);
        if (trungLap)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Không được khai báo trùng Môn học.", 400);
        }

        foreach (var item in dsYeuCau)
        {
            if (item.GiaMoiGio < 20_000 || item.GiaMoiGio > 5_000_000)
            {
                throw new LoiUngDungException(
                    "DU_LIEU_KHONG_HOP_LE",
                    "Học phí mỗi giờ phải nằm trong khoảng từ 20.000đ đến 5.000.000đ.",
                    400);
            }
        }

        var dsMonHocIds = dsYeuCau.Select(x => x.MonHocId).Distinct().ToList();
        var soMonTonTai = await _db.MonHocs.CountAsync(m => dsMonHocIds.Contains(m.Id) && m.DangHoatDong, cancellationToken);
        if (soMonTonTai != dsMonHocIds.Count)
        {
            throw new LoiUngDungException("KHONG_TIM_THAY", "Một hoặc nhiều môn học không tồn tại hoặc đã ngừng hoạt động.", 404);
        }

        var dsHienTai = await _db.GiaSuMonHocs
            .Include(m => m.DsDonDatLich)
            .Where(m => m.GiaSuId == giaSuId)
            .ToListAsync(cancellationToken);

        foreach (var cu in dsHienTai)
        {
            var khopMoi = dsYeuCau.FirstOrDefault(x => x.MonHocId == cu.MonHocId);
            if (khopMoi is not null)
            {
                cu.GiaMoiGio = khopMoi.GiaMoiGio;
            }
            else if (cu.DsDonDatLich.Count == 0)
            {
                _db.GiaSuMonHocs.Remove(cu);
            }
        }

        foreach (var moi in dsYeuCau)
        {
            var daCo = dsHienTai.Any(x => x.MonHocId == moi.MonHocId);
            if (!daCo)
            {
                _db.GiaSuMonHocs.Add(new GiaSuMonHoc
                {
                    Id = Guid.NewGuid(),
                    GiaSuId = giaSuId,
                    MonHocId = moi.MonHocId,
                    GiaMoiGio = moi.GiaMoiGio
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var dsCapNhat = await _db.GiaSuMonHocs
            .AsNoTracking()
            .Include(m => m.MonHoc)
            .Where(m => m.GiaSuId == giaSuId)
            .OrderBy(m => m.MonHoc!.Ten)
            .ToListAsync(cancellationToken);

        return dsCapNhat.Select(AnhXaGiaSuMonHocDto).ToList();
    }

    public async Task<LichRanhGiaSuDto> LayLichRanhToiAsync(Guid giaSuId, CancellationToken cancellationToken = default)
    {
        var giaSu = await _db.GiaSus
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);

        var dsLichLap = await _db.LichLapHangTuans
            .AsNoTracking()
            .Where(l => l.GiaSuId == giaSuId)
            .OrderBy(l => l.ThuTrongTuan)
            .ThenBy(l => l.GioBatDau)
            .ToListAsync(cancellationToken);

        var dsNgayCuThe = await _db.LichNgayCuThes
            .AsNoTracking()
            .Where(l => l.GiaSuId == giaSuId)
            .OrderBy(l => l.NgayCuThe)
            .ThenBy(l => l.GioBatDau)
            .ToListAsync(cancellationToken);

        return new LichRanhGiaSuDto(
            DsLichLapHangTuan: dsLichLap.Select(AnhXaLichLapDto).ToList(),
            DsLichNgayCuThe: dsNgayCuThe.Select(AnhXaLichNgayCuTheDto).ToList(),
            ThoiGianDemPhut: giaSu.ThoiGianDemPhut,
            ThoiLuongToiThieuTaiNhaPhut: giaSu.ThoiLuongToiThieuTaiNhaPhut);
    }

    public async Task<IReadOnlyList<LichLapHangTuanDto>> CapNhatLichLapHangTuanAsync(
        Guid giaSuId,
        CapNhatLichLapHangTuanYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var giaSu = await _db.GiaSus
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);

        if (yeuCau.ThoiGianDemPhut.HasValue)
        {
            giaSu.ThoiGianDemPhut = Math.Clamp(yeuCau.ThoiGianDemPhut.Value, 15, 60);
        }
        if (yeuCau.ThoiLuongToiThieuTaiNhaPhut.HasValue)
        {
            giaSu.ThoiLuongToiThieuTaiNhaPhut = Math.Clamp(yeuCau.ThoiLuongToiThieuTaiNhaPhut.Value, 90, 180);
        }

        var dsYeuCau = yeuCau.DsLichLapHangTuan ?? [];
        var dsMoi = new List<LichLapHangTuan>();

        foreach (var item in dsYeuCau)
        {
            if (item.ThuTrongTuan is < 1 or > 7)
            {
                throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Ngày trong tuần không hợp lệ (chọn từ Chủ nhật đến Thứ 7).", 400);
            }

            if (!TimeOnly.TryParse(item.GioBatDau, out var batDau) ||
                !TimeOnly.TryParse(item.GioKetThuc, out var ketThuc) ||
                ketThuc <= batDau)
            {
                throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Giờ kết thúc phải lớn hơn giờ bắt đầu.", 400);
            }

            var thoiLuongPhut = (ketThuc.Hour * 60 + ketThuc.Minute) - (batDau.Hour * 60 + batDau.Minute);
            if (item.HinhThucLich == HinhThucLichTuan.TRUC_TIEP && thoiLuongPhut < giaSu.ThoiLuongToiThieuTaiNhaPhut)
            {
                throw new LoiUngDungException(
                    "THOI_LUONG_KHONG_DU",
                    $"Khung giờ dạy tại nhà phải có thời lượng tối thiểu từ {giaSu.ThoiLuongToiThieuTaiNhaPhut} phút trở lên.",
                    400);
            }

            dsMoi.Add(new LichLapHangTuan
            {
                Id = Guid.NewGuid(),
                GiaSuId = giaSuId,
                ThuTrongTuan = item.ThuTrongTuan,
                GioBatDau = batDau,
                GioKetThuc = ketThuc,
                HinhThucLich = item.HinhThucLich
            });
        }

        foreach (var nhom in dsMoi.GroupBy(x => x.ThuTrongTuan))
        {
            var sapXep = nhom.OrderBy(x => x.GioBatDau).ToList();
            for (var i = 1; i < sapXep.Count; i++)
            {
                if (sapXep[i].GioBatDau < sapXep[i - 1].GioKetThuc)
                {
                    throw new LoiUngDungException(
                        "LICH_RANH_CHONG_NHAU",
                        "Các khung giờ rảnh trong cùng một ngày không được chồng lấp lên nhau.",
                        422);
                }
            }
        }

        var dsCu = await _db.LichLapHangTuans
            .Where(l => l.GiaSuId == giaSuId)
            .ToListAsync(cancellationToken);

        _db.LichLapHangTuans.RemoveRange(dsCu);
        _db.LichLapHangTuans.AddRange(dsMoi);
        await _db.SaveChangesAsync(cancellationToken);

        return dsMoi
            .OrderBy(l => l.ThuTrongTuan)
            .ThenBy(l => l.GioBatDau)
            .Select(AnhXaLichLapDto)
            .ToList();
    }

    public async Task<LichNgayCuTheDto> ThemLichNgayCuTheAsync(
        Guid giaSuId,
        TaoLichNgayCuTheYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(yeuCau.NgayCuThe, out var ngay))
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Ngày chọn không hợp lệ.", 400);
        }

        DateOnly? denNgay = null;
        if (!string.IsNullOrWhiteSpace(yeuCau.NgayKetThuc))
        {
            if (!DateOnly.TryParse(yeuCau.NgayKetThuc, out var dn) || dn < ngay)
            {
                throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.", 400);
            }
            denNgay = dn;
        }

        TimeOnly? batDau = null;
        TimeOnly? ketThuc = null;
        var coBatDau = !string.IsNullOrWhiteSpace(yeuCau.GioBatDau);
        var coKetThuc = !string.IsNullOrWhiteSpace(yeuCau.GioKetThuc);

        if (coBatDau != coKetThuc)
        {
            throw new LoiUngDungException(
                "DU_LIEU_KHONG_HOP_LE",
                "Vui lòng để trống cả hai mốc giờ (nếu nghỉ cả ngày) hoặc nhập đầy đủ giờ bắt đầu và giờ kết thúc.",
                400);
        }

        if (coBatDau && coKetThuc)
        {
            if (!TimeOnly.TryParse(yeuCau.GioBatDau, out var bd) ||
                !TimeOnly.TryParse(yeuCau.GioKetThuc, out var kt) ||
                kt <= bd)
            {
                throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Giờ kết thúc phải lớn hơn giờ bắt đầu.", 400);
            }

            batDau = bd;
            ketThuc = kt;
        }
        else if (yeuCau.Loai == LoaiLichNgayCuThe.BO_SUNG)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Lịch bổ sung giờ dạy bắt buộc phải có giờ bắt đầu và giờ kết thúc.", 400);
        }

        var lich = new LichNgayCuThe
        {
            Id = Guid.NewGuid(),
            GiaSuId = giaSuId,
            NgayCuThe = ngay,
            NgayKetThuc = denNgay,
            Loai = yeuCau.Loai,
            GioBatDau = batDau,
            GioKetThuc = ketThuc,
            LyDo = string.IsNullOrWhiteSpace(yeuCau.LyDo) ? null : yeuCau.LyDo.Trim()
        };

        _db.LichNgayCuThes.Add(lich);
        await _db.SaveChangesAsync(cancellationToken);

        return AnhXaLichNgayCuTheDto(lich);
    }

    public async Task<KiemTraXungDotNgayNghiKetQuaDto> KiemTraXungDotNgayNghiAsync(
        Guid giaSuId,
        DateOnly tuNgay,
        DateOnly denNgay,
        TimeOnly? gioBatDau = null,
        TimeOnly? gioKetThuc = null,
        CancellationToken cancellationToken = default)
    {
        if (denNgay < tuNgay)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.", 400);
        }

        var mocTuUtc = tuNgay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        var mocDenUtc = denNgay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);

        var dsDon = await _db.DonDatLichs
            .AsNoTracking()
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)
            .Where(d => d.GiaSuMonHoc != null &&
                        d.GiaSuMonHoc.GiaSuId == giaSuId &&
                        d.BatDauLuc < mocDenUtc &&
                        d.KetThucLuc > mocTuUtc &&
                        (d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN ||
                         d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN ||
                         d.TrangThai == TrangThaiDonDatLich.TRANH_CHAP))
            .OrderBy(d => d.BatDauLuc)
            .ToListAsync(cancellationToken);

        var dsXungDot = new List<XungDotBuoiHocDto>();

        foreach (var don in dsDon)
        {
            var bdVn = don.BatDauLuc.AddHours(7);
            var ktVn = don.KetThucLuc.AddHours(7);
            var ngayDon = DateOnly.FromDateTime(bdVn);

            if (ngayDon < tuNgay || ngayDon > denNgay)
            {
                continue;
            }

            // Nếu nghỉ cả ngày -> xung đột 100%
            if (!gioBatDau.HasValue || !gioKetThuc.HasValue)
            {
                dsXungDot.Add(new XungDotBuoiHocDto(
                    DonDatLichId: don.Id,
                    MaDon: don.MaDon,
                    TenHocSinh: don.HocSinh?.HoTen ?? "Học sinh",
                    Ngay: ngayDon.ToString("yyyy-MM-dd"),
                    GioBatDau: bdVn.ToString("HH:mm"),
                    GioKetThuc: ktVn.ToString("HH:mm"),
                    HinhThuc: don.HinhThuc,
                    TrangThai: don.TrangThai));
            }
            else
            {
                // Có mốc giờ cụ thể -> kiểm tra overlap
                var donBd = TimeOnly.FromDateTime(bdVn);
                var donKt = TimeOnly.FromDateTime(ktVn);
                if (gioBatDau.Value < donKt && gioKetThuc.Value > donBd)
                {
                    dsXungDot.Add(new XungDotBuoiHocDto(
                        DonDatLichId: don.Id,
                        MaDon: don.MaDon,
                        TenHocSinh: don.HocSinh?.HoTen ?? "Học sinh",
                        Ngay: ngayDon.ToString("yyyy-MM-dd"),
                        GioBatDau: bdVn.ToString("HH:mm"),
                        GioKetThuc: ktVn.ToString("HH:mm"),
                        HinhThuc: don.HinhThuc,
                        TrangThai: don.TrangThai));
                }
            }
        }

        return new KiemTraXungDotNgayNghiKetQuaDto(
            CoXungDot: dsXungDot.Count > 0,
            SoLuongXungDot: dsXungDot.Count,
            DsXungDot: dsXungDot);
    }

    public async Task XoaLichNgayCuTheAsync(Guid giaSuId, Guid lichNgayCuTheId, CancellationToken cancellationToken = default)
    {
        var lich = await _db.LichNgayCuThes
            .FirstOrDefaultAsync(l => l.Id == lichNgayCuTheId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy lịch ngày cụ thể.", 404);

        if (lich.GiaSuId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền xóa lịch của gia sư khác.", 403);
        }

        _db.LichNgayCuThes.Remove(lich);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. MODULE 3 — TÌM KIẾM GIA SƯ, KHUNG GIỜ TRỐNG, ĐÁNH GIÁ & MỤC YÊU THÍCH
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<KetQuaPhanTrangDto<ThongTinGiaSuTomTatDto>> TimKiemGiaSuAsync(
        BoLocTimKiemGiaSuDto boLoc,
        Guid? hocSinhHienTaiId = null,
        CancellationToken cancellationToken = default)
    {
        var trang = Math.Max(1, boLoc.Trang);
        var kichThuocTrang = Math.Clamp(boLoc.KichThuocTrang, 1, 200);

        // 1. Đẩy toàn bộ bộ lọc có cấu trúc trực tiếp xuống SQL Server (phòng thủ nghẽn khi dữ liệu lớn)
        var truyVan = _db.GiaSus
            .AsNoTracking()
            .Where(g => g.TrangThai == TrangThaiNguoiDung.HOAT_DONG &&
                        g.TrangThaiHoSo == TrangThaiHoSoGiaSu.DA_DUYET);

        if (boLoc.MonHocId.HasValue)
        {
            var monHocId = boLoc.MonHocId.Value;
            truyVan = truyVan.Where(g => g.DsMonDay.Any(m => m.MonHocId == monHocId));
        }

        if (boLoc.HinhThuc == HinhThucBuoiHoc.TRUC_TUYEN)
        {
            truyVan = truyVan.Where(g => g.DayTrucTuyen);
        }
        else if (boLoc.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP)
        {
            truyVan = truyVan.Where(g => g.DayTrucTiep);
        }

        if (boLoc.GiaToiThieu.HasValue)
        {
            var giaMin = boLoc.GiaToiThieu.Value;
            truyVan = truyVan.Where(g => g.DsMonDay.Any(m => m.GiaMoiGio >= giaMin));
        }

        if (boLoc.GiaToiDa.HasValue)
        {
            var giaMax = boLoc.GiaToiDa.Value;
            truyVan = truyVan.Where(g => g.DsMonDay.Any(m => m.GiaMoiGio <= giaMax));
        }

        if (boLoc.CoHocThu == true)
        {
            truyVan = truyVan.Where(g => g.CoHocThu && g.DayTrucTuyen);
        }

        if (boLoc.ChiDaXacMinh == true)
        {
            truyVan = truyVan.Where(g => g.DsGiayTo.Any(gt =>
                (gt.Loai == LoaiGiayTo.BANG_CAP || gt.Loai == LoaiGiayTo.CHUNG_CHI) &&
                gt.TrangThai == TrangThaiGiayTo.DA_DUYET));
        }

        // 2. Chiếu tổng hợp siêu nhẹ tại SQL Server (KHÔNG Include bảng DonDatLich/DanhGia/GiayTo tránh Cartesian explosion)
        var truyVanChiSo = truyVan.Select(g => new ChiSoGiaSuSql
        {
            Id = g.Id,
            HoTen = g.HoTen,
            TieuDe = g.TieuDe,
            KhuVuc = g.KhuVuc,
            SoNamKinhNghiem = g.SoNamKinhNghiem,
            DayTrucTuyen = g.DayTrucTuyen,
            DayTrucTiep = g.DayTrucTiep,
            CoHocThu = g.CoHocThu,
            BanKinhPhucVuKm = g.BanKinhPhucVuKm,
            GiaThapNhat = g.DsMonDay.Select(m => (int?)m.GiaMoiGio).Min() ?? 0,
            GiaCaoNhat = g.DsMonDay.Select(m => (int?)m.GiaMoiGio).Max() ?? 0,
            DaXacMinh = g.DsGiayTo.Any(gt =>
                (gt.Loai == LoaiGiayTo.BANG_CAP || gt.Loai == LoaiGiayTo.CHUNG_CHI) &&
                gt.TrangThai == TrangThaiGiayTo.DA_DUYET),
            SoBuoiHoanThanh = g.DsMonDay
                .SelectMany(m => m.DsDonDatLich)
                .Count(d => d.TrangThai == TrangThaiDonDatLich.HOAN_THANH),
            SoLuotDanhGia = g.DsMonDay
                .SelectMany(m => m.DsDonDatLich)
                .Count(d => d.DanhGia != null && d.DanhGia.LyDoAn == null),
            TongDiemDanhGia = g.DsMonDay
                .SelectMany(m => m.DsDonDatLich)
                .Where(d => d.DanhGia != null && d.DanhGia.LyDoAn == null)
                .Sum(d => (int?)d.DanhGia!.Diem) ?? 0
        });

        var coLocTuKhoa = !string.IsNullOrWhiteSpace(boLoc.TuKhoa);
        var coLocKhuVuc = !string.IsNullOrWhiteSpace(boLoc.KhuVuc);
        var coLocToaDo = boLoc.ViDo.HasValue && boLoc.KinhDo.HasValue;
        var sapXep = boLoc.SapXep?.ToLowerInvariant() ?? "phu_hop";

        int tong;
        List<ChiSoGiaSuSql> dsTrangChiSo;

        if (!coLocTuKhoa && !coLocKhuVuc && !coLocToaDo)
        {
            // Nhánh phân trang thuần SQL Server: chỉ truyền đúng kichThuocTrang (12) dòng qua mạng!
            tong = await truyVan.CountAsync(cancellationToken);
            if (tong == 0)
            {
                return new KetQuaPhanTrangDto<ThongTinGiaSuTomTatDto>([], trang, kichThuocTrang, 0, 0);
            }

            IQueryable<ChiSoGiaSuSql> truyVanSapXep = sapXep switch
            {
                "gia_tang" => truyVanChiSo
                    .OrderBy(x => x.GiaThapNhat)
                    .ThenByDescending(x => x.SoLuotDanhGia > 0 ? (double)x.TongDiemDanhGia / x.SoLuotDanhGia : 0.0)
                    .ThenByDescending(x => x.SoBuoiHoanThanh),
                "gia_giam" => truyVanChiSo
                    .OrderByDescending(x => x.GiaThapNhat)
                    .ThenByDescending(x => x.SoLuotDanhGia > 0 ? (double)x.TongDiemDanhGia / x.SoLuotDanhGia : 0.0),
                "danh_gia" => truyVanChiSo
                    .OrderByDescending(x => x.SoLuotDanhGia > 0 ? (double)x.TongDiemDanhGia / x.SoLuotDanhGia : 0.0)
                    .ThenByDescending(x => x.SoLuotDanhGia)
                    .ThenByDescending(x => x.SoBuoiHoanThanh),
                "kinh_nghiem" => truyVanChiSo
                    .OrderByDescending(x => x.SoNamKinhNghiem)
                    .ThenByDescending(x => x.SoBuoiHoanThanh),
                _ => truyVanChiSo
                    .OrderByDescending(x =>
                        0.35 * ((x.SoLuotDanhGia > 0 ? (double)x.TongDiemDanhGia / x.SoLuotDanhGia : 0.0) / 5.0) +
                        0.25 * ((x.SoBuoiHoanThanh > 100 ? 100 : x.SoBuoiHoanThanh) / 100.0) +
                        (x.DaXacMinh ? 0.20 : 0.0))
                    .ThenByDescending(x => x.SoBuoiHoanThanh)
                    .ThenBy(x => x.Id)
            };

            dsTrangChiSo = await truyVanSapXep
                .Skip((trang - 1) * kichThuocTrang)
                .Take(kichThuocTrang)
                .ToListAsync(cancellationToken);
        }
        else
        {
            // Nhánh tìm kiếm tiếng Việt không dấu / khu vực / quét tọa độ quanh học sinh trên tập chỉ số siêu nhẹ
            var dsNhe = await truyVanChiSo.ToListAsync(cancellationToken);

            Dictionary<Guid, List<string>>? mapTenMon = null;
            if (coLocTuKhoa && dsNhe.Count > 0)
            {
                var ids = dsNhe.Select(x => x.Id).ToList();
                var pairs = await _db.GiaSuMonHocs
                    .AsNoTracking()
                    .Where(m => ids.Contains(m.GiaSuId) && m.MonHoc != null)
                    .Select(m => new { m.GiaSuId, TenMon = m.MonHoc!.Ten })
                    .ToListAsync(cancellationToken);

                mapTenMon = pairs
                    .GroupBy(p => p.GiaSuId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.TenMon).ToList());
            }

            IEnumerable<ChiSoGiaSuSql> loc = dsNhe;

            if (coLocTuKhoa)
            {
                var tk = boLoc.TuKhoa!.Trim();
                var tkSlug = MonHoc.TaoSlug(tk);
                var cacTuKhoa = tk.Split(new[] { ' ', ',', '-', ';', '.', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(MonHoc.TaoSlug)
                    .Where(s => !string.IsNullOrWhiteSpace(s) && s.Length >= 2 && !new[] { "tai", "cho", "can", "tim", "gia", "su", "day", "hoc", "o" }.Contains(s))
                    .ToList();

                loc = loc.Where(g =>
                {
                    var dsMon = mapTenMon != null && mapTenMon.TryGetValue(g.Id, out var ms) ? ms : null;
                    if (ChuaKhongDau(g.HoTen, tkSlug) ||
                        ChuaKhongDau(g.TieuDe, tkSlug) ||
                        ChuaKhongDau(g.KhuVuc, tkSlug) ||
                        (dsMon != null && dsMon.Any(ten => ChuaKhongDau(ten, tkSlug))))
                    {
                        return true;
                    }

                    if (cacTuKhoa.Count > 0)
                    {
                        return cacTuKhoa.All(tu =>
                            ChuaKhongDau(g.HoTen, tu) ||
                            ChuaKhongDau(g.TieuDe, tu) ||
                            ChuaKhongDau(g.KhuVuc, tu) ||
                            (dsMon != null && dsMon.Any(ten => ChuaKhongDau(ten, tu))));
                    }

                    return false;
                });
            }

            if (coLocKhuVuc)
            {
                var kvSlug = MonHoc.TaoSlug(boLoc.KhuVuc);
                loc = loc.Where(g => ChuaKhongDau(g.KhuVuc, kvSlug));
            }

            var danhSachLoc = loc.ToList();

            if (coLocToaDo)
            {
                var lat = boLoc.ViDo!.Value;
                var lng = boLoc.KinhDo!.Value;
                var banKinh = boLoc.BanKinhQuetKm ?? 0;

                foreach (var item in danhSachLoc)
                {
                    if (item.DayTrucTiep)
                    {
                        var (vLat, vLng) = TinhToaDoMo(item.Id, item.KhuVuc);
                        if (vLat.HasValue && vLng.HasValue)
                        {
                            item.KhoangCachKm = TinhKhoangCachHaversineKm(lat, lng, vLat.Value, vLng.Value);
                        }
                    }
                }

                if (banKinh > 0 && boLoc.HinhThuc != HinhThucBuoiHoc.TRUC_TUYEN)
                {
                    danhSachLoc = danhSachLoc
                        .Where(x => x.DayTrucTiep
                            ? (x.KhoangCachKm.HasValue && x.KhoangCachKm.Value <= banKinh)
                            : boLoc.HinhThuc != HinhThucBuoiHoc.TRUC_TIEP)
                        .ToList();
                }

                danhSachLoc = danhSachLoc
                    .OrderBy(x => x.KhoangCachKm ?? 9999.0)
                    .ThenByDescending(x => TinhDiemXepHang(x, boLoc.GiaToiDa))
                    .ToList();
            }
            else
            {
                danhSachLoc = sapXep switch
                {
                    "gia_tang" => danhSachLoc.OrderBy(x => x.GiaThapNhat).ThenByDescending(x => TinhDiemXepHang(x, boLoc.GiaToiDa)).ToList(),
                    "gia_giam" => danhSachLoc.OrderByDescending(x => x.GiaThapNhat).ThenByDescending(x => TinhDiemXepHang(x, boLoc.GiaToiDa)).ToList(),
                    "danh_gia" => danhSachLoc.OrderByDescending(x => x.DiemTb).ThenByDescending(x => x.SoLuotDanhGia).ToList(),
                    "kinh_nghiem" => danhSachLoc.OrderByDescending(x => x.SoNamKinhNghiem).ThenByDescending(x => TinhDiemXepHang(x, boLoc.GiaToiDa)).ToList(),
                    _ => danhSachLoc.OrderByDescending(x => TinhDiemXepHang(x, boLoc.GiaToiDa)).ThenByDescending(x => x.DiemTb).ToList()
                };
            }

            tong = danhSachLoc.Count;
            dsTrangChiSo = danhSachLoc
                .Skip((trang - 1) * kichThuocTrang)
                .Take(kichThuocTrang)
                .ToList();
        }

        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocTrang);
        if (dsTrangChiSo.Count == 0)
        {
            return new KetQuaPhanTrangDto<ThongTinGiaSuTomTatDto>([], trang, kichThuocTrang, tong, tongTrang);
        }

        // 3. Chỉ tải hồ sơ chi tiết + danh sách môn dạy cho đúng <= 12 gia sư thuộc trang hiện tại!
        var pageIds = dsTrangChiSo.Select(x => x.Id).ToList();
        var mapChiSo = dsTrangChiSo.ToDictionary(x => x.Id);

        var dsGiaSuTrang = await _db.GiaSus
            .AsNoTracking()
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.MonHoc)
            .AsSplitQuery()
            .Where(g => pageIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        var tapYeuThich = hocSinhHienTaiId.HasValue
            ? (await _db.MucYeuThichs
                .AsNoTracking()
                .Where(y => y.HocSinhId == hocSinhHienTaiId.Value && pageIds.Contains(y.GiaSuId))
                .Select(y => y.GiaSuId)
                .ToListAsync(cancellationToken)).ToHashSet()
            : new HashSet<Guid>();

        var duLieuTrang = new List<ThongTinGiaSuTomTatDto>(pageIds.Count);
        foreach (var id in pageIds)
        {
            if (!dsGiaSuTrang.TryGetValue(id, out var gs))
            {
                continue;
            }

            var cs = mapChiSo[id];
            var dsMonDayDto = gs.DsMonDay
                .OrderBy(m => m.MonHoc?.Ten)
                .Select(AnhXaGiaSuMonHocDto)
                .ToList();

            var (viDoMo, kinhDoMo) = gs.DayTrucTiep ? TinhToaDoMo(gs.Id, gs.KhuVuc) : ((double?)null, (double?)null);

            duLieuTrang.Add(new ThongTinGiaSuTomTatDto(
                Id: gs.Id,
                HoTen: gs.HoTen,
                AnhDaiDienUrl: gs.AnhDaiDienUrl,
                KhuVuc: gs.KhuVuc,
                TieuDe: gs.TieuDe,
                GioiThieu: gs.GioiThieu,
                HocVan: gs.HocVan,
                SoNamKinhNghiem: gs.SoNamKinhNghiem,
                HinhThucDay: gs.HinhThucDay,
                CoDayTrucTuyen: gs.DayTrucTuyen,
                CoDayTrucTiep: gs.DayTrucTiep,
                MoTaHinhThucDay: TaoMoTaHinhThucDay(gs),
                DaXacMinh: cs.DaXacMinh,
                CoHocThu: gs.CoHocThu,
                BanKinhPhucVuKm: gs.DayTrucTiep ? gs.BanKinhPhucVuKm : 0,
                GiaThapNhat: cs.GiaThapNhat,
                GiaCaoNhat: cs.GiaCaoNhat,
                DiemDanhGiaTrungBinh: cs.DiemTb,
                SoLuotDanhGia: cs.SoLuotDanhGia,
                SoBuoiHoanThanh: cs.SoBuoiHoanThanh,
                DiemXepHang: TinhDiemXepHang(cs, boLoc.GiaToiDa),
                ViDoMo: viDoMo,
                KinhDoMo: kinhDoMo,
                DaYeuThich: tapYeuThich.Contains(gs.Id),
                DsMonDay: dsMonDayDto));
        }

        return new KetQuaPhanTrangDto<ThongTinGiaSuTomTatDto>(
            DuLieu: duLieuTrang,
            Trang: trang,
            KichThuocTrang: kichThuocTrang,
            Tong: tong,
            TongTrang: tongTrang);
    }

    private sealed class ChiSoGiaSuSql
    {
        public Guid Id { get; set; }
        public string HoTen { get; set; } = string.Empty;
        public string TieuDe { get; set; } = string.Empty;
        public string? KhuVuc { get; set; }
        public int SoNamKinhNghiem { get; set; }
        public bool DayTrucTuyen { get; set; }
        public bool DayTrucTiep { get; set; }
        public bool CoHocThu { get; set; }
        public int BanKinhPhucVuKm { get; set; }
        public int GiaThapNhat { get; set; }
        public int GiaCaoNhat { get; set; }
        public bool DaXacMinh { get; set; }
        public int SoBuoiHoanThanh { get; set; }
        public int SoLuotDanhGia { get; set; }
        public int TongDiemDanhGia { get; set; }
        public double? KhoangCachKm { get; set; }

        public double DiemTb => SoLuotDanhGia == 0
            ? 0.0
            : Math.Round((double)TongDiemDanhGia / SoLuotDanhGia, 1, MidpointRounding.AwayFromZero);
    }

    private static double TinhDiemXepHang(ChiSoGiaSuSql cs, int? giaMucTieu)
    {
        var diemPhuHopGia = giaMucTieu.HasValue && giaMucTieu.Value > 0 && cs.GiaThapNhat > 0
            ? Math.Clamp(1.0 - Math.Max(0, cs.GiaThapNhat - giaMucTieu.Value) / (double)giaMucTieu.Value, 0.0, 1.0)
            : 1.0;

        return Math.Round(
            0.35 * (cs.DiemTb / 5.0) +
            0.25 * (Math.Min(cs.SoBuoiHoanThanh, 100) / 100.0) +
            0.20 * (cs.DaXacMinh ? 1.0 : 0.0) +
            0.10 * 1.0 +
            0.10 * diemPhuHopGia,
            4,
            MidpointRounding.AwayFromZero);
    }

    private static double TinhKhoangCachHaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLng = (lng2 - lng1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public async Task<IReadOnlyList<ThongTinGiaSuTomTatDto>> LayDsGiaSuGoiYAsync(
        Guid? hocSinhHienTaiId = null,
        int soLuong = 6,
        CancellationToken cancellationToken = default)
    {
        var kq = await TimKiemGiaSuAsync(
            new BoLocTimKiemGiaSuDto(SapXep: "phu_hop", Trang: 1, KichThuocTrang: Math.Clamp(soLuong, 1, 20)),
            hocSinhHienTaiId,
            cancellationToken);

        return kq.DuLieu;
    }

    public async Task<ChiTietGiaSuCongKhaiDto> LayChiTietGiaSuCongKhaiAsync(
        Guid giaSuId,
        Guid? hocSinhHienTaiId = null,
        CancellationToken cancellationToken = default)
    {
        var giaSu = await _db.GiaSus
            .AsNoTracking()
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.MonHoc)
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.DsDonDatLich)
                    .ThenInclude(d => d.DanhGia)
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.DsDonDatLich)
                    .ThenInclude(d => d.HocSinh)
            .Include(g => g.DsGiayTo)
            .Include(g => g.DsLichRanh)
            .FirstOrDefaultAsync(
                g => g.Id == giaSuId &&
                     g.TrangThai == TrangThaiNguoiDung.HOAT_DONG &&
                     g.TrangThaiHoSo == TrangThaiHoSoGiaSu.DA_DUYET,
                cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư hoặc hồ sơ chưa được duyệt.", 404);

        var daYeuThich = hocSinhHienTaiId.HasValue &&
                         await _db.MucYeuThichs.AnyAsync(
                             y => y.HocSinhId == hocSinhHienTaiId.Value && y.GiaSuId == giaSuId,
                             cancellationToken);

        var duDieuKienHocThuLanDau = false;
        if (giaSu.CoHocThu && giaSu.DayTrucTuyen)
        {
            if (!hocSinhHienTaiId.HasValue)
            {
                duDieuKienHocThuLanDau = true;
            }
            else
            {
                var daTungDatVoiGiaSu = await _db.DonDatLichs
                    .AsNoTracking()
                    .AnyAsync(d =>
                        d.HocSinhId == hocSinhHienTaiId.Value &&
                        d.GiaSuMonHoc != null &&
                        d.GiaSuMonHoc.GiaSuId == giaSuId &&
                        d.TrangThai != TrangThaiDonDatLich.DA_HUY &&
                        d.TrangThai != TrangThaiDonDatLich.BI_TU_CHOI &&
                        d.TrangThai != TrangThaiDonDatLich.HET_HAN,
                        cancellationToken);
                duDieuKienHocThuLanDau = !daTungDatVoiGiaSu;
            }
        }

        var dsMonDayDto = giaSu.DsMonDay
            .OrderBy(m => m.MonHoc?.Ten)
            .Select(AnhXaGiaSuMonHocDto)
            .ToList();

        var giaThapNhat = dsMonDayDto.Count == 0 ? 0 : dsMonDayDto.Min(m => m.GiaMoiGio);
        var giaCaoNhat = dsMonDayDto.Count == 0 ? 0 : dsMonDayDto.Max(m => m.GiaMoiGio);
        var (viDoMo, kinhDoMo) = giaSu.DayTrucTiep ? TinhToaDoMo(giaSu.Id, giaSu.KhuVuc) : ((double?)null, (double?)null);

        var dsLichLap = giaSu.DsLichRanh
            .OfType<LichLapHangTuan>()
            .OrderBy(l => l.ThuTrongTuan)
            .ThenBy(l => l.GioBatDau)
            .Select(AnhXaLichLapDto)
            .ToList();

        var dsDanhGia = giaSu.DsMonDay
            .SelectMany(m => m.DsDonDatLich.Select(d => (Mon: m, Don: d)))
            .Where(x => x.Don.DanhGia is { NguoiKiemDuyetId: null })
            .OrderByDescending(x => x.Don.DanhGia!.TaoLuc)
            .Take(10)
            .Select(x => new DanhGiaCongKhaiDto(
                Id: x.Don.DanhGia!.Id,
                DonDatLichId: x.Don.Id,
                TenHocSinh: x.Don.HocSinh?.HoTen ?? "Học sinh TutorHub",
                AnhDaiDienHocSinhUrl: x.Don.HocSinh?.AnhDaiDienUrl,
                TenMonHoc: x.Mon.MonHoc?.Ten ?? "Môn học",
                Diem: x.Don.DanhGia.Diem,
                BinhLuan: x.Don.DanhGia.BinhLuan,
                PhanHoiGiaSu: x.Don.DanhGia.PhanHoiGiaSu,
                TaoLuc: x.Don.DanhGia.TaoLuc))
            .ToList();

        var dsLoaiGiayToDaDuyet = giaSu.DsGiayTo
            .Where(g => g.TrangThai == TrangThaiGiayTo.DA_DUYET &&
                        (g.Loai == LoaiGiayTo.BANG_CAP || g.Loai == LoaiGiayTo.CHUNG_CHI))
            .Select(g => g.Loai)
            .Distinct()
            .ToList();

        return new ChiTietGiaSuCongKhaiDto(
            Id: giaSu.Id,
            HoTen: giaSu.HoTen,
            AnhDaiDienUrl: giaSu.AnhDaiDienUrl,
            KhuVuc: giaSu.KhuVuc,
            TieuDe: giaSu.TieuDe,
            GioiThieu: giaSu.GioiThieu,
            HocVan: giaSu.HocVan,
            SoNamKinhNghiem: giaSu.SoNamKinhNghiem,
            HinhThucDay: giaSu.HinhThucDay,
            CoDayTrucTuyen: giaSu.DayTrucTuyen,
            CoDayTrucTiep: giaSu.DayTrucTiep,
            MoTaHinhThucDay: TaoMoTaHinhThucDay(giaSu),
            DaXacMinh: giaSu.DaXacMinh,
            CoHocThu: giaSu.CoHocThu,
            BanKinhPhucVuKm: giaSu.DayTrucTiep ? giaSu.BanKinhPhucVuKm : 0,
            GiaThapNhat: giaThapNhat,
            GiaCaoNhat: giaCaoNhat,
            DiemDanhGiaTrungBinh: giaSu.DiemDanhGiaTrungBinh,
            SoLuotDanhGia: giaSu.SoLuotDanhGia,
            SoBuoiHoanThanh: giaSu.SoBuoiHoanThanh,
            ViDoMo: viDoMo,
            KinhDoMo: kinhDoMo,
            DaYeuThich: daYeuThich,
            DsMonDay: dsMonDayDto,
            DsLichLapHangTuan: dsLichLap,
            DsDanhGiaGanDay: dsDanhGia,
            DsLoaiGiayToDaDuyet: dsLoaiGiayToDaDuyet,
            DuDieuKienHocThuLanDau: duDieuKienHocThuLanDau);
    }

    public async Task<IReadOnlyList<KhungGioTrongDto>> LayKhungGioTrongAsync(
        Guid giaSuId,
        DateOnly tuNgay,
        DateOnly denNgay,
        CancellationToken cancellationToken = default)
    {
        if (denNgay < tuNgay || denNgay.DayNumber - tuNgay.DayNumber > 31)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Khoảng ngày tra cứu khung giờ trống phải từ 1 đến 31 ngày.", 400);
        }

        var giaSu = await _db.GiaSus
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken);
        if (giaSu is null)
        {
            throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy gia sư.", 404);
        }

        var thoiGianDem = Math.Clamp(giaSu.ThoiGianDemPhut, 0, 60);

        var dsLichLap = await _db.LichLapHangTuans
            .AsNoTracking()
            .Where(l => l.GiaSuId == giaSuId)
            .ToListAsync(cancellationToken);

        var dsNgayCuThe = await _db.LichNgayCuThes
            .AsNoTracking()
            .Where(l => l.GiaSuId == giaSuId &&
                        l.NgayCuThe <= denNgay &&
                        (l.NgayKetThuc == null || l.NgayKetThuc >= tuNgay))
            .ToListAsync(cancellationToken);

        var mocTuUtc = tuNgay.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        var mocDenUtc = denNgay.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);

        var dsDonBan = await _db.DonDatLichs
            .AsNoTracking()
            .Include(d => d.GiaSuMonHoc)
            .Where(d => d.GiaSuMonHoc != null &&
                        d.GiaSuMonHoc.GiaSuId == giaSuId &&
                        d.BatDauLuc < mocDenUtc &&
                        d.KetThucLuc > mocTuUtc &&
                        (d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN ||
                         d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN ||
                         d.TrangThai == TrangThaiDonDatLich.TRANH_CHAP))
            .ToListAsync(cancellationToken);

        var ketQua = new List<KhungGioTrongDto>();

        for (var ngay = tuNgay; ngay <= denNgay; ngay = ngay.AddDays(1))
        {
            var thu = ChuyenSangThuTrongTuanV4(ngay.DayOfWeek);
            var ngoaiLeTrongNgay = dsNgayCuThe.Where(x =>
                ngay >= x.NgayCuThe &&
                ngay <= (x.NgayKetThuc ?? x.NgayCuThe)).ToList();

            if (ngoaiLeTrongNgay.Any(x => x.Loai == LoaiLichNgayCuThe.NGHI_PHEP && !x.GioBatDau.HasValue && !x.GioKetThuc.HasValue))
            {
                continue;
            }

            var dsKhoangRanh = new List<(int BatDauPhut, int KetThucPhut)>();
            foreach (var lap in dsLichLap.Where(x => x.ThuTrongTuan == thu))
            {
                dsKhoangRanh.Add((ToPhut(lap.GioBatDau), ToPhut(lap.GioKetThuc)));
            }

            foreach (var boSung in ngoaiLeTrongNgay.Where(x => x.Loai == LoaiLichNgayCuThe.BO_SUNG && x.GioBatDau.HasValue && x.GioKetThuc.HasValue))
            {
                dsKhoangRanh.Add((ToPhut(boSung.GioBatDau!.Value), ToPhut(boSung.GioKetThuc!.Value)));
            }

            dsKhoangRanh = GopKhoangThoiGian(dsKhoangRanh);
            if (dsKhoangRanh.Count == 0)
            {
                continue;
            }

            var dsKhoangBan = new List<(int BatDauPhut, int KetThucPhut)>();
            foreach (var nghi in ngoaiLeTrongNgay.Where(x => x.Loai == LoaiLichNgayCuThe.NGHI_PHEP && x.GioBatDau.HasValue && x.GioKetThuc.HasValue))
            {
                dsKhoangBan.Add((ToPhut(nghi.GioBatDau!.Value), ToPhut(nghi.GioKetThuc!.Value)));
            }

            foreach (var don in dsDonBan)
            {
                var batDauVn = don.BatDauLuc.AddHours(7);
                var ketThucVn = don.KetThucLuc.AddHours(7);
                if (DateOnly.FromDateTime(batDauVn) == ngay)
                {
                    var bdPhut = batDauVn.Hour * 60 + batDauVn.Minute;
                    var ktPhut = ketThucVn.Hour * 60 + ketThucVn.Minute;
                    if (ktPhut > bdPhut)
                    {
                        // Quy tắc chặn lịch cốt lõi (4.3) + Đệm thích ứng ngầm:
                        // Đệm di chuyển CHỈ áp dụng khi ca liền kề là Tại nhà (TRUC_TIEP).
                        // Online nối Online thì đệm = 0.
                        // Công thức chuẩn xác: Đệm chừa ra = Thời gian di chuyển ước tính (~15p thường / ~25p cao điểm) + 15 phút dự phòng chuẩn!
                        var laGioCaoDiem = DichVuUocTinhDiChuyen.KiemTraGioCaoDiem(don.BatDauLuc) || DichVuUocTinhDiChuyen.KiemTraGioCaoDiem(don.KetThucLuc);
                        var thoiGianDiChuyenUocTinh = laGioCaoDiem ? 25 : 15;
                        var demTongCong = thoiGianDiChuyenUocTinh + 15;

                        var bdPhutBan = don.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP
                            ? Math.Max(0, bdPhut - demTongCong)
                            : bdPhut;
                        var ktPhutBan = don.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP
                            ? Math.Min(1440, ktPhut + demTongCong)
                            : ktPhut;

                        dsKhoangBan.Add((bdPhutBan, ktPhutBan));
                    }
                }
            }

            var conLai = TruKhoangThoiGian(dsKhoangRanh, dsKhoangBan);
            foreach (var (bd, kt) in conLai.Where(k => k.KetThucPhut - k.BatDauPhut >= 30))
            {
                ketQua.Add(new KhungGioTrongDto(
                    Ngay: ngay.ToString("yyyy-MM-dd"),
                    ThuTrongTuan: thu,
                    GioBatDau: TuPhut(bd).ToString("HH:mm"),
                    GioKetThuc: TuPhut(kt).ToString("HH:mm")));
            }
        }

        return ketQua;
    }

    public async Task<KetQuaPhanTrangDto<DanhGiaCongKhaiDto>> LayDsDanhGiaGiaSuAsync(
        Guid giaSuId,
        int trang = 1,
        int kichThuocTrang = 10,
        CancellationToken cancellationToken = default)
    {
        trang = Math.Max(1, trang);
        kichThuocTrang = Math.Clamp(kichThuocTrang, 1, 50);

        var query = _db.DanhGias
            .AsNoTracking()
            .Include(dg => dg.DonDatLich)
                .ThenInclude(d => d!.HocSinh)
            .Include(dg => dg.DonDatLich)
                .ThenInclude(d => d!.GiaSuMonHoc)
                    .ThenInclude(m => m!.MonHoc)
            .Where(dg => dg.NguoiKiemDuyetId == null &&
                         dg.DonDatLich != null &&
                         dg.DonDatLich.GiaSuMonHoc != null &&
                         dg.DonDatLich.GiaSuMonHoc.GiaSuId == giaSuId);

        var tong = await query.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocTrang);

        var ds = await query
            .OrderByDescending(dg => dg.TaoLuc)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(cancellationToken);

        var duLieu = ds.Select(dg => new DanhGiaCongKhaiDto(
            Id: dg.Id,
            DonDatLichId: dg.DonDatLichId,
            TenHocSinh: dg.DonDatLich?.HocSinh?.HoTen ?? "Học sinh TutorHub",
            AnhDaiDienHocSinhUrl: dg.DonDatLich?.HocSinh?.AnhDaiDienUrl,
            TenMonHoc: dg.DonDatLich?.GiaSuMonHoc?.MonHoc?.Ten ?? "Môn học",
            Diem: dg.Diem,
            BinhLuan: dg.BinhLuan,
            PhanHoiGiaSu: dg.PhanHoiGiaSu,
            TaoLuc: dg.TaoLuc)).ToList();

        return new KetQuaPhanTrangDto<DanhGiaCongKhaiDto>(duLieu, trang, kichThuocTrang, tong, tongTrang);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5. MỤC YÊU THÍCH (HOC_SINH)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<MucYeuThichDto>> LayDsMucYeuThichAsync(Guid hocSinhId, CancellationToken cancellationToken = default)
    {
        var dsYeuThich = await _db.MucYeuThichs
            .AsNoTracking()
            .Include(y => y.GiaSu)
                .ThenInclude(g => g!.DsMonDay)
                    .ThenInclude(m => m.MonHoc)
            .Include(y => y.GiaSu)
                .ThenInclude(g => g!.DsMonDay)
                    .ThenInclude(m => m.DsDonDatLich)
                        .ThenInclude(d => d.DanhGia)
            .Include(y => y.GiaSu)
                .ThenInclude(g => g!.DsGiayTo)
            .Where(y => y.HocSinhId == hocSinhId && y.GiaSu != null)
            .OrderByDescending(y => y.TaoLuc)
            .ToListAsync(cancellationToken);

        return dsYeuThich
            .Select(y => new MucYeuThichDto(
                GiaSuId: y.GiaSuId,
                TaoLuc: y.TaoLuc,
                GiaSu: AnhXaGiaSuTomTatDto(y.GiaSu!, daYeuThich: true)))
            .ToList();
    }

    public async Task<MucYeuThichDto> ThemMucYeuThichAsync(Guid hocSinhId, Guid giaSuId, CancellationToken cancellationToken = default)
    {
        if (hocSinhId == giaSuId)
        {
            throw new LoiUngDungException("KHONG_DUOC_TU_THAO_TAC", "Không thể tự thêm chính mình vào danh sách yêu thích.", 422);
        }

        var giaSu = await _db.GiaSus
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.MonHoc)
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.DsDonDatLich)
                    .ThenInclude(d => d.DanhGia)
            .Include(g => g.DsGiayTo)
            .FirstOrDefaultAsync(g => g.Id == giaSuId && g.TrangThai == TrangThaiNguoiDung.HOAT_DONG, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy gia sư.", 404);

        var hienCo = await _db.MucYeuThichs
            .FirstOrDefaultAsync(y => y.HocSinhId == hocSinhId && y.GiaSuId == giaSuId, cancellationToken);

        if (hienCo is null)
        {
            hienCo = new MucYeuThich
            {
                HocSinhId = hocSinhId,
                GiaSuId = giaSuId,
                TaoLuc = DateTime.UtcNow
            };
            _db.MucYeuThichs.Add(hienCo);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new MucYeuThichDto(
            GiaSuId: giaSuId,
            TaoLuc: hienCo.TaoLuc,
            GiaSu: AnhXaGiaSuTomTatDto(giaSu, daYeuThich: true));
    }

    public async Task XoaMucYeuThichAsync(Guid hocSinhId, Guid giaSuId, CancellationToken cancellationToken = default)
    {
        var hienCo = await _db.MucYeuThichs
            .FirstOrDefaultAsync(y => y.HocSinhId == hocSinhId && y.GiaSuId == giaSuId, cancellationToken);

        if (hienCo is not null)
        {
            _db.MucYeuThichs.Remove(hienCo);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // CÁC HÀM NỘI BỘ & THUẬT TOÁN HỖ TRỢ
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<GiaSu> NapGiaSuDayDuAsync(Guid giaSuId, bool asNoTracking, CancellationToken cancellationToken)
    {
        IQueryable<GiaSu> query = _db.GiaSus
            .Include(g => g.TaiKhoanVi)
            .Include(g => g.TaiKhoanNganHang)
            .Include(g => g.DsGiayTo)
                .ThenInclude(gt => gt.NguoiDuyet)
            .Include(g => g.DsLichRanh)
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.MonHoc)
            .Include(g => g.DsMonDay)
                .ThenInclude(m => m.DsDonDatLich)
                    .ThenInclude(d => d.DanhGia);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
               ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);
    }

    private static HoSoGiaSuToiDto AnhXaHoSoGiaSuToiDto(GiaSu giaSu)
    {
        var dsLichLap = giaSu.DsLichRanh
            .OfType<LichLapHangTuan>()
            .OrderBy(l => l.ThuTrongTuan)
            .ThenBy(l => l.GioBatDau)
            .Select(AnhXaLichLapDto)
            .ToList();

        var dsNgayCuThe = giaSu.DsLichRanh
            .OfType<LichNgayCuThe>()
            .OrderBy(l => l.NgayCuThe)
            .ThenBy(l => l.GioBatDau)
            .Select(AnhXaLichNgayCuTheDto)
            .ToList();

        return new HoSoGiaSuToiDto(
            Id: giaSu.Id,
            Email: giaSu.Email,
            HoTen: giaSu.HoTen,
            SoDienThoai: giaSu.SoDienThoai,
            AnhDaiDienUrl: giaSu.AnhDaiDienUrl,
            KhuVuc: giaSu.KhuVuc,
            TieuDe: giaSu.TieuDe,
            GioiThieu: giaSu.GioiThieu,
            HocVan: giaSu.HocVan,
            SoNamKinhNghiem: giaSu.SoNamKinhNghiem,
            HinhThucDay: giaSu.HinhThucDay,
            CoDayTrucTuyen: giaSu.DayTrucTuyen,
            CoDayTrucTiep: giaSu.DayTrucTiep,
            MoTaHinhThucDay: TaoMoTaHinhThucDay(giaSu),
            TrangThaiHoSo: giaSu.TrangThaiHoSo,
            LyDoTuChoi: giaSu.LyDoTuChoi,
            DaXacMinh: giaSu.DaXacMinh,
            CoHocThu: giaSu.CoHocThu,
            BanKinhPhucVuKm: giaSu.DayTrucTiep ? giaSu.BanKinhPhucVuKm : 0,
            ThoiGianDemPhut: giaSu.ThoiGianDemPhut,
            ThoiLuongToiThieuTaiNhaPhut: giaSu.ThoiLuongToiThieuTaiNhaPhut,
            DiemDanhGiaTrungBinh: giaSu.DiemDanhGiaTrungBinh,
            SoLuotDanhGia: giaSu.SoLuotDanhGia,
            SoBuoiHoanThanh: giaSu.SoBuoiHoanThanh,
            SoDuVi: giaSu.TaiKhoanVi?.SoDu ?? 0,
            TaiKhoanNganHang: giaSu.TaiKhoanNganHang is null
                ? null
                : new TaiKhoanNganHangDto(
                    giaSu.TaiKhoanNganHang.TenNganHang,
                    ViVaTaiChinhService.CheSoTaiKhoan(giaSu.TaiKhoanNganHang.SoTaiKhoan),
                    giaSu.TaiKhoanNganHang.ChuTaiKhoan),
            DsMonDay: giaSu.DsMonDay
                .OrderBy(m => m.MonHoc?.Ten)
                .Select(AnhXaGiaSuMonHocDto)
                .ToList(),
            DsGiayTo: giaSu.DsGiayTo
                .OrderByDescending(g => g.DuyetLuc)
                .Select(AnhXaGiayToDto)
                .ToList(),
            LichRanh: new LichRanhGiaSuDto(
                DsLichLapHangTuan: dsLichLap,
                DsLichNgayCuThe: dsNgayCuThe,
                ThoiGianDemPhut: giaSu.ThoiGianDemPhut,
                ThoiLuongToiThieuTaiNhaPhut: giaSu.ThoiLuongToiThieuTaiNhaPhut));
    }

    private static ThongTinGiaSuTomTatDto AnhXaGiaSuTomTatDto(GiaSu giaSu, bool daYeuThich, int? giaMucTieu = null)
    {
        var dsMonDayDto = giaSu.DsMonDay
            .OrderBy(m => m.MonHoc?.Ten)
            .Select(AnhXaGiaSuMonHocDto)
            .ToList();

        var giaThapNhat = dsMonDayDto.Count == 0 ? 0 : dsMonDayDto.Min(m => m.GiaMoiGio);
        var giaCaoNhat = dsMonDayDto.Count == 0 ? 0 : dsMonDayDto.Max(m => m.GiaMoiGio);

        var diemTb = giaSu.DiemDanhGiaTrungBinh;
        var soBuoi = giaSu.SoBuoiHoanThanh;
        var daXacMinh = giaSu.DaXacMinh;

        var diemPhuHopGia = giaMucTieu.HasValue && giaMucTieu.Value > 0 && giaThapNhat > 0
            ? Math.Clamp(1.0 - Math.Max(0, giaThapNhat - giaMucTieu.Value) / (double)giaMucTieu.Value, 0.0, 1.0)
            : 1.0;

        var diemXepHang = Math.Round(
            0.35 * (diemTb / 5.0) +
            0.25 * (Math.Min(soBuoi, 100) / 100.0) +
            0.20 * (daXacMinh ? 1.0 : 0.0) +
            0.10 * 1.0 +
            0.10 * diemPhuHopGia,
            4,
            MidpointRounding.AwayFromZero);

        // Chỉ gắn tọa độ bản đồ khu vực đối với gia sư có nhận dạy trực tiếp tại nhà
        var (viDoMo, kinhDoMo) = giaSu.DayTrucTiep ? TinhToaDoMo(giaSu.Id, giaSu.KhuVuc) : ((double?)null, (double?)null);

        return new ThongTinGiaSuTomTatDto(
            Id: giaSu.Id,
            HoTen: giaSu.HoTen,
            AnhDaiDienUrl: giaSu.AnhDaiDienUrl,
            KhuVuc: giaSu.KhuVuc,
            TieuDe: giaSu.TieuDe,
            GioiThieu: giaSu.GioiThieu,
            HocVan: giaSu.HocVan,
            SoNamKinhNghiem: giaSu.SoNamKinhNghiem,
            HinhThucDay: giaSu.HinhThucDay,
            CoDayTrucTuyen: giaSu.DayTrucTuyen,
            CoDayTrucTiep: giaSu.DayTrucTiep,
            MoTaHinhThucDay: TaoMoTaHinhThucDay(giaSu),
            DaXacMinh: daXacMinh,
            CoHocThu: giaSu.CoHocThu,
            BanKinhPhucVuKm: giaSu.DayTrucTiep ? giaSu.BanKinhPhucVuKm : 0,
            GiaThapNhat: giaThapNhat,
            GiaCaoNhat: giaCaoNhat,
            DiemDanhGiaTrungBinh: diemTb,
            SoLuotDanhGia: giaSu.SoLuotDanhGia,
            SoBuoiHoanThanh: soBuoi,
            DiemXepHang: diemXepHang,
            ViDoMo: viDoMo,
            KinhDoMo: kinhDoMo,
            DaYeuThich: daYeuThich,
            DsMonDay: dsMonDayDto);
    }

    private static GiaSuMonHocDto AnhXaGiaSuMonHocDto(GiaSuMonHoc m) =>
        new(m.Id, m.MonHocId, m.MonHoc?.Ten ?? "Môn học", m.GiaMoiGio);

    private static GiayToGiaSuDto AnhXaGiayToDto(GiayToGiaSu g) =>
        new(g.Id, g.Loai, g.TenTepGoc, g.DuongDanTep, g.TrangThai, g.DuyetLuc, g.NguoiDuyet?.HoTen, g.NguoiDuyet?.Email);

    private static LichLapHangTuanDto AnhXaLichLapDto(LichLapHangTuan l) =>
        new(l.Id, l.ThuTrongTuan, l.GioBatDau.ToString("HH:mm"), l.GioKetThuc.ToString("HH:mm"), l.HinhThucLich);

    private static LichNgayCuTheDto AnhXaLichNgayCuTheDto(LichNgayCuThe l) =>
        new(
            l.Id,
            l.NgayCuThe.ToString("yyyy-MM-dd"),
            l.Loai,
            l.GioBatDau?.ToString("HH:mm"),
            l.GioKetThuc?.ToString("HH:mm"),
            l.NgayKetThuc?.ToString("yyyy-MM-dd"),
            l.LyDo);

    private static bool ChuaKhongDau(string? nguon, string tuKhoaSlug)
    {
        if (string.IsNullOrWhiteSpace(nguon) || string.IsNullOrWhiteSpace(tuKhoaSlug))
        {
            return false;
        }

        return MonHoc.TaoSlug(nguon).Contains(tuKhoaSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static (double? ViDoMo, double? KinhDoMo) TinhToaDoMo(Guid id, string? khuVuc)
    {
        var slug = MonHoc.TaoSlug(khuVuc);
        var (baseLat, baseLng) = slug switch
        {
            // Cần Thơ chi tiết theo từng khu dân cư / tuyến đường / phường / quận
            var s when s.Contains("hong-phat") => (10.0135, 105.7335),
            var s when s.Contains("nam-can-tho") || s.Contains("nguyen-van-cu") => (10.0078, 105.7228),
            var s when s.Contains("an-binh") => (10.0112, 105.7285),
            var s when s.Contains("an-khanh") => (10.0355, 105.7535),
            var s when s.Contains("xuan-khanh") || s.Contains("3-2") || s.Contains("30-4") => (10.0282, 105.7715),
            var s when s.Contains("hung-loi") => (10.0175, 105.7665),
            var s when s.Contains("mau-than") || s.Contains("an-phu") => (10.0348, 105.7768),
            var s when s.Contains("tan-an") || s.Contains("hai-ba-trung") => (10.0332, 105.7862),
            var s when s.Contains("an-hoa") || s.Contains("an-nghiep") => (10.0425, 105.7735),
            var s when s.Contains("cai-khe") => (10.0485, 105.7845),
            var s when s.Contains("hung-phu") || s.Contains("nam-long") => (10.0125, 105.7825),
            var s when s.Contains("phu-thu") || s.Contains("thuong-thanh") => (9.9965, 105.7715),
            var s when s.Contains("cai-rang") || s.Contains("le-binh") => (10.0025, 105.7545),
            var s when s.Contains("long-tuyen") || s.Contains("long-hoa") => (10.0385, 105.7215),
            var s when s.Contains("tra-noc") || s.Contains("an-thoi") => (10.0755, 105.7365),
            var s when s.Contains("binh-thuy") => (10.0625, 105.7485),
            var s when s.Contains("phong-dien") => (9.9985, 105.6825),
            var s when s.Contains("o-mon") => (10.1140, 105.6230),
            var s when s.Contains("thot-not") => (10.2730, 105.5340),
            var s when s.Contains("ninh-kieu") || s.Contains("can-tho") => (10.0310, 105.7720),

            // TP. Hồ Chí Minh
            var s when s.Contains("thu-duc") => (10.849, 106.771),
            var s when s.Contains("binh-thanh") => (10.803, 106.710),
            var s when s.Contains("quan-7") => (10.734, 106.721),
            var s when s.Contains("tan-binh") || s.Contains("phu-nhuan") => (10.799, 106.654),
            var s when s.Contains("go-vap") => (10.838, 106.665),
            var s when s.Contains("ho-chi-minh") || s.Contains("quan-1") || s.Contains("quan-3") || s.Contains("quan-5") || s.Contains("quan-10") => (10.775, 106.695),

            // Hà Nội
            var s when s.Contains("cau-giay") => (21.036, 105.790),
            var s when s.Contains("dong-da") => (21.018, 105.829),
            var s when s.Contains("thanh-xuan") => (20.994, 105.805),
            var s when s.Contains("ha-dong") => (20.971, 105.778),
            var s when s.Contains("long-bien") => (21.036, 105.893),
            var s when s.Contains("ha-noi") || s.Contains("ba-dinh") || s.Contains("hoan-kiem") => (21.028, 105.845),

            // Đà Nẵng & Miền Trung
            var s when s.Contains("son-tra") || s.Contains("ngu-hanh-son") => (16.060, 108.240),
            var s when s.Contains("da-nang") || s.Contains("hai-chau") || s.Contains("thanh-khe") || s.Contains("lien-chieu") => (16.054, 108.202),
            var s when s.Contains("hue") => (16.463, 107.590),
            var s when s.Contains("nha-trang") => (12.238, 109.196),
            var s when s.Contains("da-lat") => (11.940, 108.458),

            // Miền Tây & Các tỉnh thành khác
            var s when s.Contains("vinh-long") => (10.253, 105.972),
            var s when s.Contains("hau-giang") || s.Contains("vi-thanh") => (9.784, 105.470),
            var s when s.Contains("an-giang") || s.Contains("long-xuyen") => (10.386, 105.435),
            var s when s.Contains("kien-giang") || s.Contains("rach-gia") => (10.012, 105.080),
            var s when s.Contains("soc-trang") => (9.603, 105.974),
            var s when s.Contains("dong-thap") || s.Contains("cao-lanh") => (10.459, 105.632),
            var s when s.Contains("tien-giang") || s.Contains("my-tho") => (10.360, 106.360),
            var s when s.Contains("ben-tre") => (10.243, 106.375),
            var s when s.Contains("hai-phong") => (20.844, 106.688),
            var s when s.Contains("bien-hoa") || s.Contains("dong-nai") => (10.957, 106.842),
            var s when s.Contains("binh-duong") || s.Contains("thu-dau-mot") => (10.980, 106.651),
            var s when s.Contains("vung-tau") => (10.346, 107.084),
            _ => (10.031, 105.772)
        };

        // Băm toàn bộ 16 byte của Guid (bao gồm cả 2 byte cuối của các GUID tuần tự) để mỗi gia sư có tọa độ riêng biệt
        var bytes = id.ToByteArray();
        uint h1 = 2166136261u;
        uint h2 = 2166136261u ^ 0x9E3779B9u;
        for (var i = 0; i < bytes.Length; i++)
        {
            h1 = (h1 ^ bytes[i]) * 16777619u;
            h2 = (h2 ^ bytes[bytes.Length - 1 - i]) * 16777619u;
        }

        var normLat = ((int)(h1 % 41) - 20) / 20.0; // [-1.0 .. +1.0]
        var normLng = ((int)(h2 % 41) - 20) / 20.0; // [-1.0 .. +1.0]
        var offsetLat = normLat * 0.0095;
        var offsetLng = normLng * 0.0095;

        return (
            Math.Round(baseLat + offsetLat, 4, MidpointRounding.AwayFromZero),
            Math.Round(baseLng + offsetLng, 4, MidpointRounding.AwayFromZero));
    }

    public static int ChuyenSangThuTrongTuanV4(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
        {
            DayOfWeek.Sunday => 1,
            DayOfWeek.Monday => 2,
            DayOfWeek.Tuesday => 3,
            DayOfWeek.Wednesday => 4,
            DayOfWeek.Thursday => 5,
            DayOfWeek.Friday => 6,
            DayOfWeek.Saturday => 7,
            _ => 2
        };

    private static int ToPhut(TimeOnly time) => time.Hour * 60 + time.Minute;

    private static TimeOnly TuPhut(int phut) => new(Math.Clamp(phut / 60, 0, 23), Math.Clamp(phut % 60, 0, 59));

    public static List<(int BatDauPhut, int KetThucPhut)> GopKhoangThoiGian(List<(int BatDauPhut, int KetThucPhut)> dsKhoang)
    {
        var hopLe = dsKhoang
            .Where(x => x.KetThucPhut > x.BatDauPhut)
            .OrderBy(x => x.BatDauPhut)
            .ThenBy(x => x.KetThucPhut)
            .ToList();

        if (hopLe.Count <= 1)
        {
            return hopLe;
        }

        var kq = new List<(int BatDauPhut, int KetThucPhut)> { hopLe[0] };
        for (var i = 1; i < hopLe.Count; i++)
        {
            var hienTai = hopLe[i];
            var cuoi = kq[^1];
            if (hienTai.BatDauPhut <= cuoi.KetThucPhut)
            {
                kq[^1] = (cuoi.BatDauPhut, Math.Max(cuoi.KetThucPhut, hienTai.KetThucPhut));
            }
            else
            {
                kq.Add(hienTai);
            }
        }

        return kq;
    }

    public static List<(int BatDauPhut, int KetThucPhut)> TruKhoangThoiGian(
        List<(int BatDauPhut, int KetThucPhut)> dsKhoangRanh,
        List<(int BatDauPhut, int KetThucPhut)> dsKhoangBan)
    {
        var ketQua = GopKhoangThoiGian(dsKhoangRanh);
        var banDaGop = GopKhoangThoiGian(dsKhoangBan);

        foreach (var ban in banDaGop)
        {
            var tiepTheo = new List<(int BatDauPhut, int KetThucPhut)>();
            foreach (var ranh in ketQua)
            {
                if (ban.KetThucPhut <= ranh.BatDauPhut || ban.BatDauPhut >= ranh.KetThucPhut)
                {
                    tiepTheo.Add(ranh);
                    continue;
                }

                if (ban.BatDauPhut > ranh.BatDauPhut)
                {
                    tiepTheo.Add((ranh.BatDauPhut, ban.BatDauPhut));
                }

                if (ban.KetThucPhut < ranh.KetThucPhut)
                {
                    tiepTheo.Add((ban.KetThucPhut, ranh.KetThucPhut));
                }
            }

            ketQua = tiepTheo;
        }

        return ketQua;
    }
}
