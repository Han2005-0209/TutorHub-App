using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Domain.Services;
using TutorHub.Infrastructure.Persistence;

namespace TutorHub.Infrastructure.Services;

public sealed class ViVaTaiChinhService : IViVaTaiChinhService
{
    public const int SoTienRutToiThieu = 100_000;
    public const int SoTienNapToiThieu = 2_000;
    public const int SoTienNapToiDa = 50_000_000;
    public const int SoLanSaiMatKhauToiDa = 5;
    public const int CuaSoKhoaMatKhauPhut = 15;

    private static readonly string[] DinhDangBangChungHopLe = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
    private const long KichThuocBangChungToiDa = 5 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly BookingPolicyOptions _policy;
    private readonly IKiemDuyetNoiDungService _kiemDuyet;
    private readonly ILuuTruTepService _luuTruTep;
    private readonly IPayOsClient? _payOsClient;

    public ViVaTaiChinhService(
        AppDbContext db,
        BookingPolicyOptions policy,
        IKiemDuyetNoiDungService kiemDuyet,
        ILuuTruTepService luuTruTep,
        IPayOsClient? payOsClient = null)
    {
        _db = db;
        _policy = policy;
        _kiemDuyet = kiemDuyet;
        _luuTruTep = luuTruTep;
        _payOsClient = payOsClient;
    }

    public static string CheSoTaiKhoan(string? soTaiKhoan)
    {
        var sach = (soTaiKhoan ?? string.Empty).Trim();
        if (sach.Length <= 4)
        {
            return string.IsNullOrEmpty(sach) ? string.Empty : $"•••• {sach}";
        }

        return $"•••• {sach[^4..]}";
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API 1..5 — CỔNG VÍ GIA SƯ (CHỈ VAI TRÒ GIA_SU)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<TongQuanViGiaSuDto> LayTongQuanViGiaSuAsync(
        Guid giaSuId,
        CancellationToken cancellationToken = default)
    {
        var giaSu = await _db.GiaSus
            .AsNoTracking()
            .Include(g => g.TaiKhoanVi)
            .Include(g => g.TaiKhoanNganHang)
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);

        var vi = giaSu.TaiKhoanVi
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy tài khoản ví của gia sư.", 404);

        // "Sắp nhận" = tổng thực nhận dự kiến (theo tỷ lệ phí hiện hành) từ các đơn DANG_GIU đang chờ/đã xác nhận (không gồm TRANH_CHAP)
        var dsThanhToanDangGiu = await _db.ThanhToans
            .AsNoTracking()
            .Where(t => t.TrangThai == TrangThaiThanhToan.DANG_GIU &&
                        t.DonDatLich != null &&
                        (t.DonDatLich.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN ||
                         t.DonDatLich.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN) &&
                        t.DonDatLich.GiaSuMonHoc != null &&
                        t.DonDatLich.GiaSuMonHoc.GiaSuId == giaSuId)
            .Select(t => new { t.SoTien, t.TienDaHoan })
            .ToListAsync(cancellationToken);

        var soDuSapNhan = dsThanhToanDangGiu.Sum(t =>
            BoTinhTaiChinhDon.TinhTaiChinhDon(
                Math.Max(0, t.SoTien - t.TienDaHoan),
                _policy.DefaultPlatformFeePercent).ThucNhan);

        var tongDaRut = await _db.YeuCauRutTiens
            .AsNoTracking()
            .Where(r => r.GiaSuId == giaSuId && r.TrangThai == TrangThaiRutTien.DA_CHI_TRA)
            .SumAsync(r => (int?)r.SoTien, cancellationToken) ?? 0;

        TaiKhoanNganHangCheDto? nganHang = giaSu.TaiKhoanNganHang is null
            ? null
            : new TaiKhoanNganHangCheDto(
                giaSu.TaiKhoanNganHang.TenNganHang,
                CheSoTaiKhoan(giaSu.TaiKhoanNganHang.SoTaiKhoan),
                giaSu.TaiKhoanNganHang.ChuTaiKhoan);

        return new TongQuanViGiaSuDto(
            SoDuKhaDung: vi.SoDu,
            SoDuSapNhan: soDuSapNhan,
            SoDuDangXuLyRut: vi.SoDuDangXuLyRut,
            TongDaRut: tongDaRut,
            TyLePhiHienHanh: _policy.DefaultPlatformFeePercent,
            NganHang: nganHang);
    }

    public async Task<KetQuaPhanTrangDto<GiaoDichViGiaSuDto>> LayDsGiaoDichViGiaSuAsync(
        Guid giaSuId,
        string? loai = null,
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        string? tuKhoa = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        trang = Math.Max(1, trang);
        kichThuocTrang = Math.Clamp(kichThuocTrang, 1, 100);

        LoaiGiaoDichVi? loaiLoc = null;
        if (!string.IsNullOrWhiteSpace(loai))
        {
            if (Enum.TryParse<LoaiGiaoDichVi>(loai.Trim(), ignoreCase: true, out var parsed) &&
                parsed is LoaiGiaoDichVi.THU_NHAP or LoaiGiaoDichVi.RUT_TIEN)
            {
                loaiLoc = parsed;
            }
            else
            {
                throw new LoiUngDungException(
                    "LOAI_GIAO_DICH_KHONG_HOP_LE",
                    "Bộ lọc loại giao dịch sổ cái ví gia sư chỉ hỗ trợ THU_NHAP hoặc RUT_TIEN.",
                    400);
            }
        }

        var viId = await _db.TaiKhoanVis
            .AsNoTracking()
            .Where(v => v.NguoiDungThamGiaId == giaSuId)
            .Select(v => (Guid?)v.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy tài khoản ví của gia sư.", 404);

        var query = _db.GiaoDichVis
            .AsNoTracking()
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.DsKhieuNai).ThenInclude(k => k.NguoiGiaiQuyet)
            .Include(g => g.YeuCauRutTien)!.ThenInclude(r => r!.NguoiXuLy)
            .AsSplitQuery()
            .Where(g => g.TaiKhoanViId == viId);

        if (loaiLoc.HasValue)
        {
            query = query.Where(g => g.Loai == loaiLoc.Value);
        }

        if (tuNgay.HasValue)
        {
            var mocTuUtc = tuNgay.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
            query = query.Where(g => g.TaoLuc >= mocTuUtc);
        }

        if (denNgay.HasValue)
        {
            var mocDenUtc = denNgay.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
            query = query.Where(g => g.TaoLuc < mocDenUtc);
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var tk = tuKhoa.Trim();
            query = query.Where(g =>
                (g.ThanhToan != null && g.ThanhToan.DonDatLich != null &&
                 (g.ThanhToan.DonDatLich.MaDon.Contains(tk) ||
                  (g.ThanhToan.DonDatLich.HocSinh != null && g.ThanhToan.DonDatLich.HocSinh.HoTen.Contains(tk)) ||
                  (g.ThanhToan.DonDatLich.GiaSuMonHoc != null && g.ThanhToan.DonDatLich.GiaSuMonHoc.MonHoc != null &&
                   g.ThanhToan.DonDatLich.GiaSuMonHoc.MonHoc.Ten.Contains(tk)))) ||
                (g.YeuCauRutTien != null && g.YeuCauRutTien.TenNganHang.Contains(tk)));
        }

        var tong = await query.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocTrang);

        var ds = await query
            .OrderByDescending(g => g.SoThuTu)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(cancellationToken);

        return new KetQuaPhanTrangDto<GiaoDichViGiaSuDto>(
            DuLieu: ds.Select(AnhXaGiaoDichViGiaSu).ToList(),
            Trang: trang,
            KichThuocTrang: kichThuocTrang,
            Tong: tong,
            TongTrang: tongTrang);
    }

    public async Task<GiaoDichViGiaSuDto> LayChiTietGiaoDichViGiaSuAsync(
        Guid giaSuId,
        Guid giaoDichId,
        CancellationToken cancellationToken = default)
    {
        var gd = await _db.GiaoDichVis
            .AsNoTracking()
            .Include(g => g.TaiKhoanVi)
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.DsKhieuNai).ThenInclude(k => k.NguoiGiaiQuyet)
            .Include(g => g.YeuCauRutTien)!.ThenInclude(r => r!.NguoiXuLy)
            .AsSplitQuery()
            .FirstOrDefaultAsync(g => g.Id == giaoDichId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy bút toán sổ cái.", 404);

        if (gd.TaiKhoanVi?.NguoiDungThamGiaId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền xem giao dịch ví của người khác.", 403);
        }

        return AnhXaGiaoDichViGiaSu(gd);
    }

    public async Task<KetQuaPhanTrangDto<YeuCauRutTienDto>> LayDsYeuCauRutTienGiaSuAsync(
        Guid giaSuId,
        TrangThaiRutTien? trangThai = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        trang = Math.Max(1, trang);
        kichThuocTrang = Math.Clamp(kichThuocTrang, 1, 100);

        var query = _db.YeuCauRutTiens
            .AsNoTracking()
            .Include(r => r.GiaSu)
            .Include(r => r.NguoiXuLy)
            .Where(r => r.GiaSuId == giaSuId);

        if (trangThai.HasValue)
        {
            query = query.Where(r => r.TrangThai == trangThai.Value);
        }

        var tong = await query.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocTrang);

        var ds = await query
            .OrderByDescending(r => r.TaoLuc)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(cancellationToken);

        return new KetQuaPhanTrangDto<YeuCauRutTienDto>(
            ds.Select(r => AnhXaYeuCauRutTien(r, choQuanTri: false)).ToList(),
            trang,
            kichThuocTrang,
            tong,
            tongTrang);
    }

    public async Task<KetQuaTaoYeuCauRutTienDto> TaoYeuCauRutTienAsync(
        Guid giaSuId,
        TaoYeuCauRutTienDto yeuCau,
        string? diaChiIp = null,
        CancellationToken cancellationToken = default)
    {
        if (yeuCau.SoTien < SoTienRutToiThieu)
        {
            throw new LoiUngDungException(
                "SO_TIEN_TOI_THIEU",
                $"Số tiền rút tối thiểu là {SoTienRutToiThieu:N0} ₫.",
                400);
        }

        var giaSu = await _db.GiaSus
            .Include(g => g.TaiKhoanVi)
            .Include(g => g.TaiKhoanNganHang)
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);

        // Xác thực mật khẩu + rate-limit 5 lần/15p + ghi AuditLog nếu sai
        await XacNhanMatKhauNhayCamAsync(
            giaSu,
            yeuCau.MatKhauXacNhan,
            "YeuCauRutTien",
            diaChiIp,
            cancellationToken);

        var nganHang = giaSu.TaiKhoanNganHang
            ?? throw new LoiUngDungException(
                "CHUA_CO_NGAN_HANG",
                "Vui lòng thiết lập tài khoản ngân hàng nhận tiền trong Cài đặt tài khoản trước khi rút tiền.",
                422);

        var vi = giaSu.TaiKhoanVi
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy tài khoản ví.", 404);

        var idempotencyKey = string.IsNullOrWhiteSpace(yeuCau.IdempotencyKey)
            ? null
            : yeuCau.IdempotencyKey.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"vi:{vi.Id:N}"], cancellationToken);

        if (idempotencyKey is not null)
        {
            var daTonTai = await _db.YeuCauRutTiens
                .AsNoTracking()
                .AnyAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);
            if (daTonTai)
            {
                throw new LoiUngDungException(
                    "IDEMPOTENCY_TRUNG",
                    "Yêu cầu rút tiền với khóa định danh này đã được gửi trước đó.",
                    409);
            }
        }

        // Nạp lại số dư mới nhất sau khi đã giữ khóa Exclusive trên ví
        await _db.Entry(vi).ReloadAsync(cancellationToken);

        if (vi.SoDu < yeuCau.SoTien)
        {
            throw new LoiUngDungException(
                "SO_DU_KHONG_DU",
                $"Số dư khả dụng hiện tại ({vi.SoDu:N0} ₫) không đủ để rút {yeuCau.SoTien:N0} ₫.",
                422);
        }

        // Khóa tiền sang SoDuDangXuLyRut: tổng số dư sổ sách (SoDu + SoDuDangXuLyRut) giữ nguyên => chưa ghi sổ cái
        vi.SoDu -= yeuCau.SoTien;
        vi.SoDuDangXuLyRut += yeuCau.SoTien;

        var lenhRut = new YeuCauRutTien
        {
            GiaSuId = giaSuId,
            GiaSu = giaSu,
            SoTien = yeuCau.SoTien,
            TrangThai = TrangThaiRutTien.CHO_XU_LY,
            TenNganHang = nganHang.TenNganHang,
            SoTaiKhoan = nganHang.SoTaiKhoan,
            ChuTaiKhoan = nganHang.ChuTaiKhoan,
            IdempotencyKey = idempotencyKey,
            TaoLuc = DateTime.UtcNow
        };

        _db.YeuCauRutTiens.Add(lenhRut);
        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = giaSuId,
            Loai = LoaiThongBao.THANH_TOAN,
            TieuDe = "Đã tiếp nhận yêu cầu rút tiền",
            NoiDung = $"Yêu cầu rút {yeuCau.SoTien:N0} ₫ về {nganHang.TenNganHang} ({CheSoTaiKhoan(nganHang.SoTaiKhoan)}) đã được tạm khóa và chuyển cho quản trị viên đối soát chi trả.",
            TaoLuc = lenhRut.TaoLuc
        });
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new KetQuaTaoYeuCauRutTienDto(
            YeuCau: AnhXaYeuCauRutTien(lenhRut, choQuanTri: false),
            SoDuKhaDungMoi: vi.SoDu,
            SoDuDangXuLyRutMoi: vi.SoDuDangXuLyRut);
    }

    public async Task<KetQuaTaoYeuCauRutTienDto> HuyYeuCauRutTienGiaSuAsync(
        Guid giaSuId,
        Guid yeuCauId,
        CancellationToken cancellationToken = default)
    {
        var lenh = await _db.YeuCauRutTiens
            .Include(r => r.GiaSu)!.ThenInclude(g => g!.TaiKhoanVi)
            .FirstOrDefaultAsync(r => r.Id == yeuCauId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy yêu cầu rút tiền.", 404);

        if (lenh.GiaSuId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền hủy yêu cầu rút tiền này.", 403);
        }

        var vi = lenh.GiaSu?.TaiKhoanVi
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy tài khoản ví của gia sư.", 404);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"vi:{vi.Id:N}"], cancellationToken);

        await _db.Entry(lenh).ReloadAsync(cancellationToken);
        await _db.Entry(vi).ReloadAsync(cancellationToken);

        if (lenh.TrangThai != TrangThaiRutTien.CHO_XU_LY)
        {
            throw new LoiUngDungException(
                "DA_XU_LY",
                "Yêu cầu rút tiền này đã được xử lý hoặc hủy trước đó.",
                409);
        }

        var now = DateTime.UtcNow;
        var maLenh = TaoMaLenhRut(lenh);
        var dtoTruocHuy = AnhXaYeuCauRutTien(lenh, choQuanTri: false);
        var giaTriCuJson = JsonSerializer.Serialize(new
        {
            maLenhRut = maLenh,
            soTien = lenh.SoTien,
            tenNganHang = lenh.TenNganHang,
            soTaiKhoanChe = CheSoTaiKhoan(lenh.SoTaiKhoan),
            chuTaiKhoan = lenh.ChuTaiKhoan,
            taoLuc = lenh.TaoLuc
        });

        vi.SoDu += lenh.SoTien;
        vi.SoDuDangXuLyRut = Math.Max(0, vi.SoDuDangXuLyRut - lenh.SoTien);

        // Xóa bản ghi CHO_XU_LY chưa qua Admin duyệt để bảo toàn ràng buộc OCL
        // (DA_CHI_TRA / TU_CHOI bắt buộc có NguoiXuLyId = QuanTriVien) và bất biến sum(CHO_XU_LY) == SoDuDangXuLyRut,
        // đồng thời lưu vết kiểm toán bất biến trong AuditLog + ThongBao.
        _db.YeuCauRutTiens.Remove(lenh);

        _db.AuditLogs.Add(new AuditLog
        {
            NguoiDungId = giaSuId,
            HanhDong = "HUY_YEU_CAU_RUT_TIEN",
            DoiTuong = $"YeuCauRutTien:{lenh.Id}",
            GiaTriCuJson = giaTriCuJson,
            GiaTriMoiJson = JsonSerializer.Serialize(new { trangThai = "DA_HUY_BOI_GIA_SU", soDuKhaDungMoi = vi.SoDu }),
            TaoLuc = now
        });

        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = giaSuId,
            Loai = LoaiThongBao.THANH_TOAN,
            TieuDe = $"Đã hủy yêu cầu rút tiền {maLenh}",
            NoiDung = $"Bạn đã hủy yêu cầu rút {lenh.SoTien:N0} ₫ ({maLenh}). Số tiền đã được mở khóa về Số dư khả dụng ({vi.SoDu:N0} ₫).",
            TaoLuc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new KetQuaTaoYeuCauRutTienDto(
            YeuCau: dtoTruocHuy with
            {
                GhiChu = "Gia sư chủ động hủy yêu cầu rút tiền trước khi quản trị xử lý",
                XuLyLuc = now
            },
            SoDuKhaDungMoi: vi.SoDu,
            SoDuDangXuLyRutMoi: vi.SoDuDangXuLyRut);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API 6 — ĐỔI TÀI KHOẢN NGÂN HÀNG CÓ XÁC NHẬN MẬT KHẨU + AUDIT LOG
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<TaiKhoanNganHangCheDto> CapNhatNganHangGiaSuAsync(
        Guid giaSuId,
        CapNhatNganHangYeuCauDto yeuCau,
        string? diaChiIp = null,
        CancellationToken cancellationToken = default)
    {
        var tenNH = (yeuCau.TenNganHang ?? string.Empty).Trim();
        var soTK = (yeuCau.SoTaiKhoan ?? string.Empty).Trim();
        var chuTK = (yeuCau.ChuTaiKhoan ?? string.Empty).Trim().ToUpperInvariant();

        if (tenNH.Length < 2 || tenNH.Length > 120)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Tên ngân hàng phải từ 2 đến 120 ký tự.", 400);
        }

        if (!Regex.IsMatch(soTK, "^[A-Za-z0-9]{6,30}$"))
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Số tài khoản ngân hàng phải gồm 6 đến 30 ký tự chữ/số liền nhau.", 400);
        }

        if (chuTK.Length < 2 || chuTK.Length > 150)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Tên chủ tài khoản không hợp lệ.", 400);
        }

        var giaSu = await _db.GiaSus
            .Include(g => g.TaiKhoanNganHang)
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);

        await XacNhanMatKhauNhayCamAsync(
            giaSu,
            yeuCau.MatKhauXacNhan,
            "TaiKhoanNganHang",
            diaChiIp,
            cancellationToken);

        string? giaTriCuJson = giaSu.TaiKhoanNganHang is null
            ? null
            : JsonSerializer.Serialize(new
            {
                tenNganHang = giaSu.TaiKhoanNganHang.TenNganHang,
                soTaiKhoanChe = CheSoTaiKhoan(giaSu.TaiKhoanNganHang.SoTaiKhoan),
                chuTaiKhoan = giaSu.TaiKhoanNganHang.ChuTaiKhoan
            });

        if (giaSu.TaiKhoanNganHang is not null)
        {
            giaSu.TaiKhoanNganHang.TenNganHang = tenNH;
            giaSu.TaiKhoanNganHang.SoTaiKhoan = soTK;
            giaSu.TaiKhoanNganHang.ChuTaiKhoan = chuTK;
        }
        else
        {
            giaSu.TaiKhoanNganHang = new TaiKhoanNganHang
            {
                GiaSuId = giaSuId,
                TenNganHang = tenNH,
                SoTaiKhoan = soTK,
                ChuTaiKhoan = chuTK
            };
        }

        var giaTriMoiJson = JsonSerializer.Serialize(new
        {
            tenNganHang = tenNH,
            soTaiKhoanChe = CheSoTaiKhoan(soTK),
            chuTaiKhoan = chuTK
        });

        _db.AuditLogs.Add(new AuditLog
        {
            NguoiDungId = giaSuId,
            HanhDong = "CAP_NHAT_NGAN_HANG",
            DoiTuong = $"GiaSu:{giaSuId}",
            GiaTriCuJson = giaTriCuJson,
            GiaTriMoiJson = giaTriMoiJson,
            DiaChiIp = diaChiIp,
            TaoLuc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new TaiKhoanNganHangCheDto(tenNH, CheSoTaiKhoan(soTK), chuTK);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API 7..9 — THANH TOÁN PREPAID ESCROW, XÁC NHẬN HOÀN THÀNH SỚM & KHIẾU NẠI (HOC_SINH)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ThanhToanKetQuaDto> ThanhToanDonAsync(
        Guid hocSinhId,
        Guid donId,
        ThanhToanDonYeuCauDto yeuCau,
        string? diaChiIp = null,
        CancellationToken cancellationToken = default)
    {
        var don = await _db.DonDatLichs
            .Include(d => d.DsThanhToan)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        if (don.HocSinhId != hocSinhId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền thanh toán đơn đặt lịch này.", 403);
        }

        // Chặn tuyệt đối mọi trạng thái không thể thanh toán
        if (don.TrangThai is not (TrangThaiDonDatLich.CHO_XAC_NHAN or TrangThaiDonDatLich.DA_XAC_NHAN))
        {
            throw new LoiUngDungException(
                "DON_KHONG_THE_THANH_TOAN",
                "Đơn đặt lịch ở trạng thái hiện tại không thể thanh toán.",
                409);
        }

        if (don.TongTien <= 0)
        {
            throw new LoiUngDungException(
                "DON_MIEN_PHI_KHONG_CAN_THANH_TOAN",
                "Buổi học thử miễn phí (0 ₫) không cần thanh toán.",
                422);
        }

        var now = DateTime.UtcNow;
        var hanThanhToan = don.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN
            ? (don.HanPhanHoi < don.BatDauLuc ? don.HanPhanHoi : don.BatDauLuc)
            : don.BatDauLuc;

        if (now >= hanThanhToan)
        {
            throw new LoiUngDungException(
                "DON_QUA_HAN_THANH_TOAN",
                "Đơn đặt lịch đã quá hạn thanh toán.",
                409);
        }

        var viHs = yeuCau.PhuongThuc == PhuongThucThanhToan.VI_NOI_BO
            ? await _db.TaiKhoanVis.FirstOrDefaultAsync(v => v.NguoiDungThamGiaId == hocSinhId, cancellationToken)
              ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví học sinh.", 404)
            : null;

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var locks = new List<string> { $"don:{don.Id:N}" };
        if (viHs is not null)
        {
            locks.Add($"vi:{viHs.Id:N}");
        }

        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, locks, cancellationToken);

        await _db.Entry(don).ReloadAsync(cancellationToken);
        if (don.TrangThai is not (TrangThaiDonDatLich.CHO_XAC_NHAN or TrangThaiDonDatLich.DA_XAC_NHAN))
        {
            throw new LoiUngDungException("DON_KHONG_THE_THANH_TOAN", "Đơn đặt lịch không còn ở trạng thái cho phép thanh toán.", 409);
        }

        var tt = await _db.ThanhToans
            .FirstOrDefaultAsync(t => t.DonDatLichId == don.Id, cancellationToken);

        if (tt is not null)
        {
            if (tt.TrangThai is TrangThaiThanhToan.DANG_GIU or TrangThaiThanhToan.DA_GIAI_NGAN or TrangThaiThanhToan.DA_HOAN)
            {
                throw new LoiUngDungException("DON_DA_THANH_TOAN", "Đơn đặt lịch này đã được thanh toán hoặc xử lý tiền.", 409);
            }

            // Thử lại sau THAT_BAI / CHO_THANH_TOAN: cập nhật chính dòng hiện có (1 dòng/đơn)
            tt.SoTien = don.TongTien;
            tt.PhuongThuc = yeuCau.PhuongThuc;
            tt.TienDaHoan = 0;
        }
        else
        {
            tt = new ThanhToan
            {
                DonDatLichId = don.Id,
                SoTien = don.TongTien,
                PhuongThuc = yeuCau.PhuongThuc,
                TrangThai = TrangThaiThanhToan.CHO_THANH_TOAN,
                MaGiaoDichCong = null,
                TienDaHoan = 0,
                TaoLuc = now
            };
            _db.ThanhToans.Add(tt);
        }

        string? urlChuyenHuong = null;

        if (yeuCau.PhuongThuc == PhuongThucThanhToan.GIA_LAP)
        {
            tt.TrangThai = TrangThaiThanhToan.DANG_GIU;
            tt.MaGiaoDichCong = null;
            tt.ThanhToanLuc = now;
        }
        else if (yeuCau.PhuongThuc == PhuongThucThanhToan.VI_NOI_BO)
        {
            await _db.Entry(viHs!).ReloadAsync(cancellationToken);
            if (viHs!.SoDu < don.TongTien)
            {
                throw new LoiUngDungException(
                    "SO_DU_KHONG_DU",
                    $"Số dư Ví học tập ({viHs.SoDu:N0} ₫) không đủ để thanh toán {don.TongTien:N0} ₫.",
                    422);
            }

            tt.TrangThai = TrangThaiThanhToan.DANG_GIU;
            tt.MaGiaoDichCong = null;
            tt.ThanhToanLuc = now;

            // Lưu ThanhToan trước để đảm bảo FK cho GiaoDichVi
            await _db.SaveChangesAsync(cancellationToken);

            viHs.SoDu -= don.TongTien;
            var soThuTu = await LaySoThuTuTiepTheoAsync(viHs.Id, cancellationToken);
            _db.GiaoDichVis.Add(new GiaoDichVi
            {
                TaiKhoanViId = viHs.Id,
                SoThuTu = soThuTu,
                Loai = LoaiGiaoDichVi.THANH_TOAN,
                SoTien = -don.TongTien,
                SoDuSau = viHs.SoDu + viHs.SoDuDangXuLyRut,
                GhiChu = $"Thanh toán đơn {don.MaDon} bằng Ví học tập",
                ThanhToanId = tt.Id,
                TaoLuc = now
            });
        }
        else
        {
            // VNPAY: giữ CHO_THANH_TOAN và MaGiaoDichCong = NULL cho tới khi nhận IPN/Return
            tt.TrangThai = TrangThaiThanhToan.CHO_THANH_TOAN;
            tt.MaGiaoDichCong = null;
            urlChuyenHuong = $"/pages/ket-qua-thanh-toan.html?thanhToanId={tt.Id}&phuongThuc=VNPAY";
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new ThanhToanKetQuaDto(
            ThanhToanId: tt.Id,
            DonDatLichId: don.Id,
            SoTien: tt.SoTien,
            PhuongThuc: tt.PhuongThuc,
            TrangThai: tt.TrangThai,
            MaGiaoDichCong: tt.MaGiaoDichCong,
            ThanhToanLuc: tt.ThanhToanLuc,
            TienDaHoan: tt.TienDaHoan,
            UrlChuyenHuong: urlChuyenHuong);
    }

    public async Task<DonDatLichChiTietDto> XacNhanHoanThanhSomAsync(
        Guid hocSinhId,
        Guid donId,
        CancellationToken cancellationToken = default)
    {
        var don = await _db.DonDatLichs
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)!.ThenInclude(g => g!.TaiKhoanVi)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsThanhToan)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        if (don.HocSinhId != hocSinhId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền xác nhận hoàn thành đơn đặt lịch này.", 403);
        }

        if (don.TrangThai != TrangThaiDonDatLich.DA_XAC_NHAN)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Chỉ có thể xác nhận hoàn thành đơn đã được gia sư xác nhận.", 409);
        }

        var now = DateTime.UtcNow;
        if (now < don.BatDauLuc)
        {
            throw new LoiUngDungException("BUOI_HOC_CHUA_BAT_DAU", "Chỉ có thể xác nhận hoàn thành sau khi buổi học đã bắt đầu.", 409);
        }

        var viGs = don.GiaSuMonHoc?.GiaSu?.TaiKhoanVi
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy tài khoản ví của gia sư.", 404);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(
            _db,
            [$"don:{don.Id:N}", $"vi:{viGs.Id:N}"],
            cancellationToken);

        await GiaiNganDonTrongGiaoDichAsync(don, viGs, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return AnhXaDonDatLichChiTiet(don);
    }

    public async Task<KhieuNaiChiTietDto> MoKhieuNaiAsync(
        Guid hocSinhId,
        Guid donId,
        MoKhieuNaiYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var moTaSach = (yeuCau.MoTa ?? string.Empty).Trim();
        if (moTaSach.Length < 10 || moTaSach.Length > 2000)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Nội dung khiếu nại phải từ 10 đến 2000 ký tự.", 400);
        }

        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("moTa", moTaSach, false)]);

        var don = await _db.DonDatLichs
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsThanhToan)
            .Include(d => d.DsKhieuNai)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        if (don.HocSinhId != hocSinhId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Chỉ học sinh chủ đơn mới được mở khiếu nại.", 403);
        }

        var tt = don.DsThanhToan.FirstOrDefault();
        if (don.TongTien <= 0 || tt is null)
        {
            throw new LoiUngDungException(
                "DON_MIEN_PHI_KHONG_THE_KHIEU_NAI",
                "Khiếu nại tài chính chỉ áp dụng cho buổi học có phí đang tạm giữ tiền.",
                422);
        }

        if (don.TrangThai == TrangThaiDonDatLich.HOAN_THANH || tt.TrangThai == TrangThaiThanhToan.DA_GIAI_NGAN)
        {
            throw new LoiUngDungException(
                "DON_DA_GIAI_NGAN",
                "Buổi học đã hoàn thành và giải ngân cho gia sư, không thể mở khiếu nại.",
                409);
        }

        if (don.TrangThai != TrangThaiDonDatLich.DA_XAC_NHAN || tt.TrangThai != TrangThaiThanhToan.DANG_GIU)
        {
            throw new LoiUngDungException(
                "CHUYEN_TRANG_THAI_SAI",
                "Chỉ có thể mở khiếu nại cho buổi học đã xác nhận và đang tạm giữ học phí.",
                409);
        }

        var now = DateTime.UtcNow;
        if (now < don.BatDauLuc)
        {
            throw new LoiUngDungException("BUOI_HOC_CHUA_BAT_DAU", "Chưa đến thời gian buổi học, bạn có thể hủy lịch để nhận hoàn tiền 100%.", 409);
        }

        var hanKhieuNai = don.KetThucLuc.AddHours(_policy.AutoDisbursementHours);
        if (now >= hanKhieuNai)
        {
            throw new LoiUngDungException(
                "QUA_HAN_KHIEU_NAI",
                $"Đã quá thời hạn mở khiếu nại ({_policy.AutoDisbursementHours} giờ sau khi buổi học kết thúc).",
                409);
        }

        if (don.DsKhieuNai.Any(k => k.TrangThai == TrangThaiKhieuNai.MO))
        {
            throw new LoiUngDungException("KHIEU_NAI_DA_MO", "Đơn này đang có một khiếu nại chờ xử lý.", 409);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"don:{don.Id:N}"], cancellationToken);

        don.TrangThai = TrangThaiDonDatLich.TRANH_CHAP;
        var khieuNai = new KhieuNai
        {
            DonDatLichId = don.Id,
            DonDatLich = don,
            DanhMuc = yeuCau.DanhMuc,
            MoTa = moTaSach,
            BangChungUrl = string.IsNullOrWhiteSpace(yeuCau.BangChungUrl) ? null : yeuCau.BangChungUrl.Trim(),
            TrangThai = TrangThaiKhieuNai.MO,
            TaoLuc = now
        };

        _db.KhieuNais.Add(khieuNai);

        // Thông báo ngay cho Gia sư để gia sư biết nhận được khiếu nại và gửi giải trình/bằng chứng phản biện
        var giaSuId = don.GiaSuMonHoc?.GiaSuId ?? Guid.Empty;
        if (giaSuId != Guid.Empty)
        {
            _db.ThongBaos.Add(new ThongBao
            {
                NguoiNhanId = giaSuId,
                Loai = LoaiThongBao.KHIEU_NAI,
                TieuDe = $"Học viên mở khiếu nại đơn {don.MaDon}",
                NoiDung = $"{don.HocSinh?.HoTen ?? "Học viên"} vừa gửi khiếu nại cho buổi học {don.GiaSuMonHoc?.MonHoc?.Ten ?? "môn học"} (học phí đã thanh toán: {tt.SoTien:N0} ₫). Vui lòng xem chi tiết và gửi giải trình/bằng chứng phản biện trước khi quản trị viên phân xử.",
                TaoLuc = now
            });
        }

        // Thông báo cho các Quản trị viên đang hoạt động
        var dsAdminIds = await _db.QuanTriViens
            .AsNoTracking()
            .Where(q => q.TrangThai == TrangThaiNguoiDung.HOAT_DONG)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        foreach (var adminId in dsAdminIds)
        {
            _db.ThongBaos.Add(new ThongBao
            {
                NguoiNhanId = adminId,
                Loai = LoaiThongBao.KHIEU_NAI,
                TieuDe = $"Khiếu nại mới · {don.MaDon}",
                NoiDung = $"{don.HocSinh?.HoTen ?? "Học viên"} mở khiếu nại đơn {don.MaDon} ({tt.SoTien:N0} ₫).",
                TaoLuc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return AnhXaKhieuNaiChiTiet(khieuNai);
    }

    public async Task<IReadOnlyList<KhieuNaiChiTietDto>> LayDsKhieuNaiCuaToiAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        CancellationToken cancellationToken = default)
    {
        var query = _db.KhieuNais
            .AsNoTracking()
            .Include(k => k.NguoiGiaiQuyet)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsThanhToan)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsNhatKyPhongHoc)
            .AsSplitQuery()
            .AsQueryable();

        query = vaiTro == VaiTro.GIA_SU
            ? query.Where(k => k.DonDatLich != null && k.DonDatLich.GiaSuMonHoc != null && k.DonDatLich.GiaSuMonHoc.GiaSuId == nguoiDungId)
            : query.Where(k => k.DonDatLich != null && k.DonDatLich.HocSinhId == nguoiDungId);

        var ds = await query
            .OrderByDescending(k => k.TrangThai == TrangThaiKhieuNai.MO)
            .ThenByDescending(k => k.TaoLuc)
            .ToListAsync(cancellationToken);

        return ds.Select(AnhXaKhieuNaiChiTiet).ToList();
    }

    public async Task<KhieuNaiChiTietDto> PhanHoiKhieuNaiGiaSuAsync(
        Guid giaSuId,
        Guid khieuNaiId,
        PhanHoiKhieuNaiGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var phanHoiSach = (yeuCau.PhanHoi ?? string.Empty).Trim();
        if (phanHoiSach.Length < 10 || phanHoiSach.Length > 2000)
        {
            throw new LoiUngDungException(
                "DU_LIEU_KHONG_HOP_LE",
                "Nội dung giải trình / bác bỏ của gia sư phải từ 10 đến 2000 ký tự.",
                400);
        }

        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("phanHoi", phanHoiSach, false)]);

        var kn = await _db.KhieuNais
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsThanhToan)
            .FirstOrDefaultAsync(k => k.Id == khieuNaiId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy khiếu nại.", 404);

        var don = kn.DonDatLich
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch liên kết.", 404);

        if (don.GiaSuMonHoc?.GiaSuId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Chỉ gia sư phụ trách buổi học này mới được gửi phản biện khiếu nại.", 403);
        }

        if (kn.TrangThai != TrangThaiKhieuNai.MO)
        {
            throw new LoiUngDungException("DA_XU_LY", "Khiếu nại này đã được quản trị viên phân xử, không thể cập nhật giải trình.", 409);
        }

        var now = DateTime.UtcNow;
        kn.PhanHoiGiaSu = phanHoiSach;
        kn.PhanHoiGiaSuLuc = now;
        if (!string.IsNullOrWhiteSpace(yeuCau.BangChungUrl))
        {
            kn.BangChungGiaSuUrl = yeuCau.BangChungUrl.Trim();
        }

        // Thông báo cho Học viên biết gia sư đã gửi giải trình phản biện
        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = don.HocSinhId,
            Loai = LoaiThongBao.KHIEU_NAI,
            TieuDe = $"Gia sư đã phản hồi khiếu nại đơn {don.MaDon}",
            NoiDung = $"{don.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư"} đã gửi giải trình và bằng chứng cho khiếu nại đơn {don.MaDon}. Quản trị viên sẽ đối chiếu 2 chiều để phân xử.",
            TaoLuc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        return AnhXaKhieuNaiChiTiet(kn);
    }

    public async Task<TepBangChungKetQuaDto> TaiLenBangChungKhieuNaiAsync(
        Guid nguoiDungId,
        Stream luongTep,
        string tenTepGoc,
        long kichThuoc,
        CancellationToken cancellationToken = default)
    {
        if (kichThuoc <= 0 || kichThuoc > KichThuocBangChungToiDa)
        {
            throw new LoiUngDungException("TEP_KHONG_HOP_LE", "Tệp bằng chứng phải có dung lượng từ 1 byte đến 5 MB.", 400);
        }

        var ext = Path.GetExtension(tenTepGoc ?? string.Empty).ToLowerInvariant();
        if (!DinhDangBangChungHopLe.Contains(ext))
        {
            throw new LoiUngDungException("TEP_KHONG_HOP_LE", "Chỉ chấp nhận ảnh hoặc PDF (.jpg, .jpeg, .png, .webp, .pdf).", 400);
        }

        var duongDan = await _luuTruTep.LuuTepAsync(
            luongTep,
            tenTepGoc ?? $"bang-chung{ext}",
            $"khieu-nai/{nguoiDungId:N}",
            laKhoPrivate: true,
            cancellationToken);

        var urlXem = _luuTruTep.TaoSignedUrlXemTep(duongDan, TimeSpan.FromMinutes(15));
        return new TepBangChungKetQuaDto(duongDan, urlXem);
    }

    public async Task<IReadOnlyList<ThongBaoDto>> LayDsThongBaoCuaToiAsync(
        Guid nguoiDungId,
        int gioiHan = 25,
        CancellationToken cancellationToken = default)
    {
        gioiHan = Math.Clamp(gioiHan, 1, 100);
        return await _db.ThongBaos
            .AsNoTracking()
            .Where(t => t.NguoiNhanId == nguoiDungId)
            .OrderByDescending(t => t.TaoLuc)
            .Take(gioiHan)
            .Select(t => new ThongBaoDto(t.Id, t.Loai, t.TieuDe, t.NoiDung, t.DocLuc, t.TaoLuc))
            .ToListAsync(cancellationToken);
    }

    public async Task DanhDauDaDocThongBaoAsync(
        Guid nguoiDungId,
        Guid? thongBaoId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ThongBaos.Where(t => t.NguoiNhanId == nguoiDungId && t.DocLuc == null);
        if (thongBaoId.HasValue)
        {
            query = query.Where(t => t.Id == thongBaoId.Value);
        }

        var ds = await query.ToListAsync(cancellationToken);
        if (ds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var tb in ds)
        {
            tb.DocLuc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PHÒNG HỌC TRỰC TUYẾN MỨC 1 — ĐIỂM DANH GIỜ MÁY CHỦ & HEARTBEAT
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<ThongTinPhongHocDto> LayThongTinPhongHocAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        Guid donId,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default)
    {
        var don = await _db.DonDatLichs
            .AsNoTracking()
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsNhatKyPhongHoc)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        var giaSuId = don.GiaSuMonHoc?.GiaSuId ?? Guid.Empty;
        if (vaiTro != VaiTro.QUAN_TRI && don.HocSinhId != nguoiDungId && giaSuId != nguoiDungId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền truy cập phòng học của đơn này.", 403);
        }

        if (don.HinhThuc != HinhThucBuoiHoc.TRUC_TUYEN)
        {
            throw new LoiUngDungException("KHONG_PHAI_PHONG_ONLINE", "Đơn này là buổi học trực tiếp tại nhà, không có phòng học trực tuyến.", 422);
        }

        var now = mocThoiGianUtc ?? DateTime.UtcNow;
        var phongUrl = string.IsNullOrWhiteSpace(don.PhongHocUrl) || don.PhongHocUrl.StartsWith("https://meet.jit.si", StringComparison.OrdinalIgnoreCase)
            ? $"/pages/phong-hoc.html?donId={don.Id}"
            : don.PhongHocUrl;
        var tenPhong = $"TutorHub-{don.MaDon}";
        var (choVao, lyDo) = KiemTraCuaSoMoPhong(don, now);
        var thoiLuong = Math.Max(1, (int)Math.Round((don.KetThucLuc - don.BatDauLuc).TotalMinutes));

        return new ThongTinPhongHocDto(
            DonDatLichId: don.Id,
            MaDon: don.MaDon,
            TenMonHoc: don.GiaSuMonHoc?.MonHoc?.Ten ?? "Môn học",
            GiaSuId: giaSuId,
            TenGiaSu: don.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư",
            HocSinhId: don.HocSinhId,
            TenHocSinh: don.HocSinh?.HoTen ?? "Học sinh",
            TrangThaiDon: don.TrangThai,
            BatDauLuc: DateTime.SpecifyKind(don.BatDauLuc, DateTimeKind.Utc),
            KetThucLuc: DateTime.SpecifyKind(don.KetThucLuc, DateTimeKind.Utc),
            ThoiLuongPhut: thoiLuong,
            PhongHocUrl: phongUrl,
            TenPhongJitsi: tenPhong,
            ChoPhepVaoPhong: choVao,
            LyDoChuaVaoDuoc: lyDo,
            GioMayChuUtc: now,
            NhatKy: TinhTomTatNhatKyPhongHoc(don, don.DsNhatKyPhongHoc.ToList(), now));
    }

    public async Task<PhienPhongHocKetQuaDto> VaoPhongHocAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        Guid donId,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default)
    {
        var don = await _db.DonDatLichs
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsNhatKyPhongHoc)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        var giaSuId = don.GiaSuMonHoc?.GiaSuId ?? Guid.Empty;
        var laGiaSu = giaSuId == nguoiDungId && vaiTro == VaiTro.GIA_SU;
        var laHocSinh = don.HocSinhId == nguoiDungId && vaiTro == VaiTro.HOC_SINH;
        if (!laGiaSu && !laHocSinh)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Chỉ gia sư hoặc học sinh của buổi học này mới được vào phòng.", 403);
        }

        if (don.HinhThuc != HinhThucBuoiHoc.TRUC_TUYEN)
        {
            throw new LoiUngDungException("KHONG_PHAI_PHONG_ONLINE", "Đơn này không phải buổi học trực tuyến.", 422);
        }

        var now = mocThoiGianUtc ?? DateTime.UtcNow;
        var (choVao, lyDo) = KiemTraCuaSoMoPhong(don, now);
        if (!choVao)
        {
            throw new LoiUngDungException("CHUA_DEN_GIO_MO_PHONG", lyDo ?? "Chưa thể vào phòng học lúc này.", 409);
        }

        // Đóng các phiên trước đó chưa rời phòng của cùng người dùng (trường hợp reload tab)
        foreach (var cu in don.DsNhatKyPhongHoc.Where(x => x.NguoiDungId == nguoiDungId && !x.DaRoiPhong))
        {
            cu.DaRoiPhong = true;
            if (cu.CapNhatCuoiLuc < now)
            {
                cu.CapNhatCuoiLuc = now;
            }
        }

        var lanDauGiaSuMoLop = laGiaSu && !don.DsNhatKyPhongHoc.Any(x => x.VaiTroLucVao == VaiTro.GIA_SU);

        var phien = new NhatKyPhongHoc
        {
            DonDatLichId = don.Id,
            NguoiDungId = nguoiDungId,
            VaiTroLucVao = laGiaSu ? VaiTro.GIA_SU : VaiTro.HOC_SINH,
            VaoLuc = now,
            CapNhatCuoiLuc = now,
            DaRoiPhong = false
        };

        _db.NhatKyPhongHocs.Add(phien);
        don.DsNhatKyPhongHoc.Add(phien);

        if (lanDauGiaSuMoLop)
        {
            _db.ThongBaos.Add(new ThongBao
            {
                NguoiNhanId = don.HocSinhId,
                Loai = LoaiThongBao.HE_THONG,
                TieuDe = $"Gia sư đã mở phòng học đơn {don.MaDon}",
                NoiDung = $"{don.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư"} vừa mở phòng học trực tuyến môn {don.GiaSuMonHoc?.MonHoc?.Ten ?? "môn học"}. Mời bạn vào lớp học ngay!",
                TaoLuc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new PhienPhongHocKetQuaDto(
            PhienId: phien.Id,
            VaoLuc: now,
            GioMayChuUtc: now,
            NhatKy: TinhTomTatNhatKyPhongHoc(don, don.DsNhatKyPhongHoc.ToList(), now));
    }

    public async Task<TomTatNhatKyPhongHocDto> CapNhatNhipPhongHocAsync(
        Guid nguoiDungId,
        Guid donId,
        NhipPhongHocYeuCauDto yeuCau,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default)
    {
        var don = await _db.DonDatLichs
            .Include(d => d.DsNhatKyPhongHoc)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        var phien = don.DsNhatKyPhongHoc.FirstOrDefault(p => p.Id == yeuCau.PhienId && p.NguoiDungId == nguoiDungId)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy phiên điểm danh phòng học hợp lệ.", 404);

        var now = mocThoiGianUtc ?? DateTime.UtcNow;
        if (now > phien.CapNhatCuoiLuc)
        {
            phien.CapNhatCuoiLuc = now;
        }

        if (yeuCau.RoiPhong)
        {
            phien.DaRoiPhong = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return TinhTomTatNhatKyPhongHoc(don, don.DsNhatKyPhongHoc.ToList(), now);
    }

    private static (bool ChoPhep, string? LyDo) KiemTraCuaSoMoPhong(DonDatLich don, DateTime nowUtc)
    {
        if (don.TrangThai != TrangThaiDonDatLich.DA_XAC_NHAN)
        {
            return (false, "Phòng học chỉ mở cho đơn ở trạng thái Đã xác nhận.");
        }

        var batDauUtc = DateTime.SpecifyKind(don.BatDauLuc, DateTimeKind.Utc);
        var ketThucUtc = DateTime.SpecifyKind(don.KetThucLuc, DateTimeKind.Utc);
        var gioMoSom = batDauUtc.AddMinutes(-15);
        var gioDongMuon = ketThucUtc.AddMinutes(30);

        if (nowUtc < gioMoSom)
        {
            var conPhut = Math.Max(1, (int)Math.Ceiling((gioMoSom - nowUtc).TotalMinutes));
            return (false, $"Phòng học sẽ mở trước giờ học 15 phút (còn khoảng {conPhut} phút nữa).");
        }

        if (nowUtc > gioDongMuon)
        {
            return (false, "Phiên phòng học trực tuyến của buổi này đã kết thúc quá 30 phút.");
        }

        return (true, null);
    }

    public static TomTatNhatKyPhongHocDto TinhTomTatNhatKyPhongHoc(
        DonDatLich don,
        IReadOnlyList<NhatKyPhongHoc> dsPhien,
        DateTime nowUtc)
    {
        var batDauUtc = DateTime.SpecifyKind(don.BatDauLuc, DateTimeKind.Utc);
        var ketThucUtc = DateTime.SpecifyKind(don.KetThucLuc, DateTimeKind.Utc);
        var thoiLuongDangKy = Math.Max(1, (int)Math.Round((ketThucUtc - batDauUtc).TotalMinutes));

        var phienGs = dsPhien
            .Where(p => p.VaiTroLucVao == VaiTro.GIA_SU)
            .Select(p => (
                Start: DateTime.SpecifyKind(p.VaoLuc, DateTimeKind.Utc),
                End: DateTime.SpecifyKind(p.CapNhatCuoiLuc < p.VaoLuc ? p.VaoLuc : p.CapNhatCuoiLuc, DateTimeKind.Utc)))
            .ToList();

        var phienHs = dsPhien
            .Where(p => p.VaiTroLucVao == VaiTro.HOC_SINH)
            .Select(p => (
                Start: DateTime.SpecifyKind(p.VaoLuc, DateTimeKind.Utc),
                End: DateTime.SpecifyKind(p.CapNhatCuoiLuc < p.VaoLuc ? p.VaoLuc : p.CapNhatCuoiLuc, DateTimeKind.Utc)))
            .ToList();

        var gsHop = GopKhoangThoiGian(phienGs);
        var hsHop = GopKhoangThoiGian(phienHs);

        DateTime? gsVao = gsHop.Count > 0 ? gsHop[0].Start : null;
        DateTime? gsRoi = gsHop.Count > 0 ? gsHop[^1].End : null;
        DateTime? hsVao = hsHop.Count > 0 ? hsHop[0].Start : null;
        DateTime? hsRoi = hsHop.Count > 0 ? hsHop[^1].End : null;

        var tongPhutGs = TinhTongPhut(gsHop);
        var tongPhutHs = TinhTongPhut(hsHop);
        var tongPhutCungNhau = TinhTongPhutGiaoNhau(gsHop, hsHop);

        int? trePhut = gsVao.HasValue
            ? Math.Max(0, (int)Math.Floor((gsVao.Value - batDauUtc).TotalMinutes))
            : null;

        var qua15PhutChuaVao = !gsVao.HasValue && nowUtc >= batDauUtc.AddMinutes(15);

        string trangThaiMoLop;
        string moTa;
        if (gsVao.HasValue)
        {
            if (trePhut!.Value <= 5)
            {
                trangThaiMoLop = "DUNG_GIO";
                moTa = trePhut.Value == 0
                    ? "Gia sư mở lớp đúng giờ"
                    : $"Gia sư mở lớp đúng giờ (trễ {trePhut.Value} phút trong mức cho phép)";
            }
            else
            {
                trangThaiMoLop = "VAO_TRE";
                moTa = $"Gia sư vào lớp trễ {trePhut.Value} phút";
            }
        }
        else if (nowUtc < batDauUtc)
        {
            trangThaiMoLop = "CHUA_DEN_GIO";
            moTa = "Chưa đến giờ bắt đầu buổi học";
        }
        else
        {
            trangThaiMoLop = "GIA_SU_CHUA_VAO";
            moTa = qua15PhutChuaVao
                ? "Quá 15 phút gia sư vẫn chưa vào phòng học"
                : "Đã đến giờ học — gia sư chưa điểm danh vào phòng";
        }

        return new TomTatNhatKyPhongHocDto(
            TrangThaiMoLop: trangThaiMoLop,
            MoTaTrangThaiMoLop: moTa,
            GiaSuVaoDauTienLuc: gsVao,
            GiaSuRoiCuoiCungLuc: gsRoi,
            GiaSuTrePhut: trePhut,
            TongPhutGiaSuCoMat: tongPhutGs,
            HocSinhVaoDauTienLuc: hsVao,
            HocSinhRoiCuoiCungLuc: hsRoi,
            TongPhutHocSinhCoMat: tongPhutHs,
            TongPhutHocCungNhau: tongPhutCungNhau,
            ThoiLuongDangKyPhut: thoiLuongDangKy,
            Qua15PhutGiaSuChuaVao: qua15PhutChuaVao);
    }

    private static List<(DateTime Start, DateTime End)> GopKhoangThoiGian(List<(DateTime Start, DateTime End)> ds)
    {
        if (ds.Count == 0)
        {
            return [];
        }

        var ordered = ds.OrderBy(x => x.Start).ThenBy(x => x.End).ToList();
        var merged = new List<(DateTime Start, DateTime End)> { ordered[0] };
        for (var i = 1; i < ordered.Count; i++)
        {
            var cur = ordered[i];
            var last = merged[^1];
            if (cur.Start <= last.End)
            {
                merged[^1] = (last.Start, cur.End > last.End ? cur.End : last.End);
            }
            else
            {
                merged.Add(cur);
            }
        }

        return merged;
    }

    private static int TinhTongPhut(List<(DateTime Start, DateTime End)> ds)
    {
        var tong = ds.Sum(x => Math.Max(0.0, (x.End - x.Start).TotalMinutes));
        return (int)Math.Round(tong, MidpointRounding.AwayFromZero);
    }

    private static int TinhTongPhutGiaoNhau(
        List<(DateTime Start, DateTime End)> a,
        List<(DateTime Start, DateTime End)> b)
    {
        double tong = 0;
        var i = 0;
        var j = 0;
        while (i < a.Count && j < b.Count)
        {
            var start = a[i].Start > b[j].Start ? a[i].Start : b[j].Start;
            var end = a[i].End < b[j].End ? a[i].End : b[j].End;
            if (end > start)
            {
                tong += (end - start).TotalMinutes;
            }

            if (a[i].End < b[j].End)
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return (int)Math.Round(tong, MidpointRounding.AwayFromZero);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API 10..13 — QUẢN TRỊ RÚT TIỀN & KHIẾU NẠI (CHỈ QUAN_TRI)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<KetQuaPhanTrangDto<YeuCauRutTienDto>> LayDsYeuCauRutTienQuanTriAsync(
        TrangThaiRutTien? trangThai = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        trang = Math.Max(1, trang);
        kichThuocTrang = Math.Clamp(kichThuocTrang, 1, 100);

        var query = _db.YeuCauRutTiens
            .AsNoTracking()
            .Include(r => r.GiaSu)
            .Include(r => r.NguoiXuLy)
            .AsQueryable();

        if (trangThai.HasValue)
        {
            query = query.Where(r => r.TrangThai == trangThai.Value);
        }

        var tong = await query.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocTrang);

        var ds = await query
            .OrderByDescending(r => r.TrangThai == TrangThaiRutTien.CHO_XU_LY)
            .ThenByDescending(r => r.TaoLuc)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(cancellationToken);

        return new KetQuaPhanTrangDto<YeuCauRutTienDto>(
            ds.Select(r => AnhXaYeuCauRutTien(r, choQuanTri: true)).ToList(),
            trang,
            kichThuocTrang,
            tong,
            tongTrang);
    }

    public async Task<YeuCauRutTienDto> XuLyYeuCauRutTienAsync(
        Guid quanTriId,
        Guid yeuCauId,
        XuLyYeuCauRutTienDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var ghiChuSach = string.IsNullOrWhiteSpace(yeuCau.GhiChu) ? null : yeuCau.GhiChu.Trim();
        if (!yeuCau.Duyet && string.IsNullOrWhiteSpace(ghiChuSach))
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng nhập lý do từ chối yêu cầu rút tiền.", 400);
        }

        var lenh = await _db.YeuCauRutTiens
            .Include(r => r.GiaSu)!.ThenInclude(g => g!.TaiKhoanVi)
            .FirstOrDefaultAsync(r => r.Id == yeuCauId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy yêu cầu rút tiền.", 404);

        var vi = lenh.GiaSu?.TaiKhoanVi
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy tài khoản ví của gia sư.", 404);

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"vi:{vi.Id:N}"], cancellationToken);

        await _db.Entry(lenh).ReloadAsync(cancellationToken);
        await _db.Entry(vi).ReloadAsync(cancellationToken);

        if (lenh.TrangThai != TrangThaiRutTien.CHO_XU_LY)
        {
            throw new LoiUngDungException("DA_XU_LY", "Yêu cầu rút tiền này đã được xử lý trước đó.", 409);
        }

        var now = DateTime.UtcNow;
        lenh.XuLyLuc = now;
        lenh.NguoiXuLyId = quanTriId;
        var quanTri = await _db.QuanTriViens
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quanTriId, cancellationToken);
        var nhanAdmin = quanTri is not null
            ? $"Quản trị viên {quanTri.HoTen}"
            : "Quản trị viên";

        if (yeuCau.Duyet)
        {
            if (vi.SoDuDangXuLyRut < lenh.SoTien)
            {
                throw new LoiUngDungException(
                    "LECH_SO_DU_DANG_RUT",
                    "Số dư đang xử lý rút của ví không khớp với lệnh rút.",
                    409);
            }

            vi.SoDuDangXuLyRut -= lenh.SoTien;
            lenh.TrangThai = TrangThaiRutTien.DA_CHI_TRA;
            lenh.GhiChu = ghiChuSach ?? "Đã chi trả chuyển khoản thành công";

            // 1 bút toán RUT_TIEN duy nhất khi quản trị xác nhận chi trả
            var soThuTu = await LaySoThuTuTiepTheoAsync(vi.Id, cancellationToken);
            var maLenh = TaoMaLenhRut(lenh);
            var moTaChiTra = string.IsNullOrWhiteSpace(ghiChuSach)
                ? $"Chi trả rút tiền {maLenh} về {lenh.TenNganHang} {CheSoTaiKhoan(lenh.SoTaiKhoan)}"
                : $"Chi trả rút tiền {maLenh} về {lenh.TenNganHang} {CheSoTaiKhoan(lenh.SoTaiKhoan)} · {ghiChuSach}";
            _db.GiaoDichVis.Add(new GiaoDichVi
            {
                TaiKhoanViId = vi.Id,
                SoThuTu = soThuTu,
                Loai = LoaiGiaoDichVi.RUT_TIEN,
                SoTien = -lenh.SoTien,
                SoDuSau = vi.SoDu + vi.SoDuDangXuLyRut,
                YeuCauRutTienId = lenh.Id,
                GhiChu = moTaChiTra.Length > 500 ? moTaChiTra[..500] : moTaChiTra,
                TaoLuc = now
            });

            _db.ThongBaos.Add(new ThongBao
            {
                NguoiNhanId = lenh.GiaSuId,
                Loai = LoaiThongBao.THANH_TOAN,
                TieuDe = $"Đã chi trả yêu cầu rút tiền {maLenh}",
                NoiDung = $"{nhanAdmin} đã xác nhận chuyển khoản {lenh.SoTien:N0} ₫ về {lenh.TenNganHang} ({CheSoTaiKhoan(lenh.SoTaiKhoan)}). Ghi chú: {lenh.GhiChu}.",
                TaoLuc = now
            });
        }
        else
        {
            // Từ chối: hoàn lại khả dụng, giảm đang xử lý rút => số dư sổ sách không đổi => 0 dòng sổ cái
            vi.SoDu += lenh.SoTien;
            vi.SoDuDangXuLyRut = Math.Max(0, vi.SoDuDangXuLyRut - lenh.SoTien);
            lenh.TrangThai = TrangThaiRutTien.TU_CHOI;
            lenh.GhiChu = ghiChuSach;

            var maLenh = TaoMaLenhRut(lenh);
            _db.ThongBaos.Add(new ThongBao
            {
                NguoiNhanId = lenh.GiaSuId,
                Loai = LoaiThongBao.THANH_TOAN,
                TieuDe = $"Yêu cầu rút tiền {maLenh} bị từ chối",
                NoiDung = $"{nhanAdmin} đã từ chối yêu cầu rút {lenh.SoTien:N0} ₫ và hoàn lại vào Số dư khả dụng ({vi.SoDu:N0} ₫). Lý do: {ghiChuSach}.",
                TaoLuc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        lenh.NguoiXuLy = quanTri;
        return AnhXaYeuCauRutTien(lenh, choQuanTri: true);
    }

    public async Task<KetQuaPhanTrangDto<KhieuNaiChiTietDto>> LayDsKhieuNaiQuanTriAsync(
        TrangThaiKhieuNai? trangThai = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        trang = Math.Max(1, trang);
        kichThuocTrang = Math.Clamp(kichThuocTrang, 1, 100);

        var query = _db.KhieuNais
            .AsNoTracking()
            .Include(k => k.NguoiGiaiQuyet)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsThanhToan)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsNhatKyPhongHoc)
            .AsSplitQuery()
            .AsQueryable();

        if (trangThai.HasValue)
        {
            query = query.Where(k => k.TrangThai == trangThai.Value);
        }

        var tong = await query.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocTrang);

        var ds = await query
            .OrderByDescending(k => k.TrangThai == TrangThaiKhieuNai.MO)
            .ThenByDescending(k => k.TaoLuc)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(cancellationToken);

        return new KetQuaPhanTrangDto<KhieuNaiChiTietDto>(
            ds.Select(AnhXaKhieuNaiChiTiet).ToList(),
            trang,
            kichThuocTrang,
            tong,
            tongTrang);
    }

    public async Task<KhieuNaiChiTietDto> GiaiQuyetKhieuNaiAsync(
        Guid quanTriId,
        Guid khieuNaiId,
        GiaiQuyetKhieuNaiYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var ghiChuSach = (yeuCau.GhiChu ?? string.Empty).Trim();
        if (ghiChuSach.Length < 3 || ghiChuSach.Length > 1000)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng nhập ghi chú kết luận giải quyết (từ 3 đến 1000 ký tự).", 400);
        }

        var kn = await _db.KhieuNais
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.HocSinh)!.ThenInclude(h => h!.TaiKhoanVi)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)!.ThenInclude(g => g!.TaiKhoanVi)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsThanhToan)
            .FirstOrDefaultAsync(k => k.Id == khieuNaiId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy khiếu nại.", 404);

        var don = kn.DonDatLich
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch liên kết.", 404);
        var tt = don.DsThanhToan.FirstOrDefault()
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Đơn khiếu nại không có giao dịch thanh toán.", 404);
        var viHs = don.HocSinh?.TaiKhoanVi
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví học sinh.", 404);
        var viGs = don.GiaSuMonHoc?.GiaSu?.TaiKhoanVi
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví gia sư.", 404);

        if (yeuCau.SoTienHoan < 0 || yeuCau.SoTienHoan > tt.SoTien)
        {
            throw new LoiUngDungException(
                "SO_TIEN_HOAN_KHONG_HOP_LE",
                $"Số tiền hoàn phải nằm trong khoảng từ 0 đến {tt.SoTien:N0} ₫.",
                400);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(
            _db,
            [$"don:{don.Id:N}", $"vi:{viHs.Id:N}", $"vi:{viGs.Id:N}"],
            cancellationToken);

        await _db.Entry(kn).ReloadAsync(cancellationToken);
        await _db.Entry(tt).ReloadAsync(cancellationToken);
        await _db.Entry(viHs).ReloadAsync(cancellationToken);
        await _db.Entry(viGs).ReloadAsync(cancellationToken);

        if (kn.TrangThai != TrangThaiKhieuNai.MO)
        {
            throw new LoiUngDungException("DA_XU_LY", "Khiếu nại này đã được giải quyết trước đó.", 409);
        }

        if (tt.TrangThai != TrangThaiThanhToan.DANG_GIU)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Giao dịch thanh toán không còn ở trạng thái đang tạm giữ.", 409);
        }

        var now = DateTime.UtcNow;
        kn.GiaiQuyetLuc = now;
        kn.NguoiGiaiQuyetId = quanTriId;
        kn.GhiChuGiaiQuyet = ghiChuSach;

        if (yeuCau.SoTienHoan == tt.SoTien)
        {
            // Hoàn 100% về Ví Học viên; ví gia sư 0 dòng
            kn.TrangThai = TrangThaiKhieuNai.DA_GIAI_QUYET;
            don.TrangThai = TrangThaiDonDatLich.DA_HUY;
            don.LyDoHuy = $"Hoàn tiền 100% sau khiếu nại: {ghiChuSach}";

            await HoanTienVeViHocSinhNoiBoAsync(
                tt,
                viHs,
                tt.SoTien,
                datDaHoan: true,
                $"Hoàn tiền 100% khiếu nại đơn {don.MaDon}",
                now,
                cancellationToken);
        }
        else if (yeuCau.SoTienHoan > 0)
        {
            // Hoàn một phần về Ví Học viên, phần còn lại giải ngân sau phí cho Gia sư
            kn.TrangThai = TrangThaiKhieuNai.DA_GIAI_QUYET;
            don.TrangThai = TrangThaiDonDatLich.HOAN_THANH;

            await HoanTienVeViHocSinhNoiBoAsync(
                tt,
                viHs,
                yeuCau.SoTienHoan,
                datDaHoan: false,
                $"Hoàn tiền một phần khiếu nại đơn {don.MaDon}",
                now,
                cancellationToken);

            var phanConLai = tt.SoTien - yeuCau.SoTienHoan;
            var (_, _, thucNhan) = BoTinhTaiChinhDon.TinhTaiChinhDon(phanConLai, _policy.DefaultPlatformFeePercent);
            tt.TrangThai = TrangThaiThanhToan.DA_GIAI_NGAN;
            tt.GiaiNganLuc = now;

            if (thucNhan > 0)
            {
                viGs.SoDu += thucNhan;
                var sttGs = await LaySoThuTuTiepTheoAsync(viGs.Id, cancellationToken);
                _db.GiaoDichVis.Add(new GiaoDichVi
                {
                    TaiKhoanViId = viGs.Id,
                    SoThuTu = sttGs,
                    Loai = LoaiGiaoDichVi.THU_NHAP,
                    SoTien = thucNhan,
                    SoDuSau = viGs.SoDu + viGs.SoDuDangXuLyRut,
                    ThanhToanId = tt.Id,
                    GhiChu = $"Giải ngân phần còn lại đơn {don.MaDon} sau xử lý khiếu nại",
                    TaoLuc = now
                });
            }
        }
        else
        {
            // Bác khiếu nại (soTienHoan = 0) => TU_CHOI, giải ngân 100% (sau phí) cho Gia sư
            kn.TrangThai = TrangThaiKhieuNai.TU_CHOI;
            don.TrangThai = TrangThaiDonDatLich.HOAN_THANH;
            tt.TienDaHoan = 0;
            tt.TrangThai = TrangThaiThanhToan.DA_GIAI_NGAN;
            tt.GiaiNganLuc = now;

            var (_, _, thucNhan) = BoTinhTaiChinhDon.TinhTaiChinhDon(tt.SoTien, _policy.DefaultPlatformFeePercent);
            if (thucNhan > 0)
            {
                viGs.SoDu += thucNhan;
                var sttGs = await LaySoThuTuTiepTheoAsync(viGs.Id, cancellationToken);
                _db.GiaoDichVis.Add(new GiaoDichVi
                {
                    TaiKhoanViId = viGs.Id,
                    SoThuTu = sttGs,
                    Loai = LoaiGiaoDichVi.THU_NHAP,
                    SoTien = thucNhan,
                    SoDuSau = viGs.SoDu + viGs.SoDuDangXuLyRut,
                    ThanhToanId = tt.Id,
                    GhiChu = $"Giải ngân đơn {don.MaDon} sau khi bác khiếu nại",
                    TaoLuc = now
                });
            }
        }

        var quanTriGiaiQuyet = await _db.QuanTriViens
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quanTriId, cancellationToken);
        var nhanAdminKn = quanTriGiaiQuyet is not null
            ? $"Quản trị viên {quanTriGiaiQuyet.HoTen}"
            : "Quản trị viên";

        // Gửi thông báo kết quả phân xử khiếu nại cho cả Học viên và Gia sư (dựa trên số tiền lịch sử thực tế)
        var ketQuaMoTa = yeuCau.SoTienHoan == tt.SoTien
            ? $"Hoàn 100% ({tt.SoTien:N0} ₫) về ví học viên."
            : yeuCau.SoTienHoan > 0
                ? $"Hoàn {yeuCau.SoTienHoan:N0} ₫ về ví học viên; phần còn lại ({tt.SoTien - yeuCau.SoTienHoan:N0} ₫ trước phí) đã giải ngân cho gia sư."
                : $"Bác khiếu nại (hoàn 0 ₫); giải ngân toàn bộ học phí lịch sử ({tt.SoTien:N0} ₫ trước phí) cho gia sư.";

        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = don.HocSinhId,
            Loai = LoaiThongBao.KHIEU_NAI,
            TieuDe = $"Kết quả xử lý khiếu nại đơn {don.MaDon}",
            NoiDung = $"{nhanAdminKn} đã xử lý: {ketQuaMoTa} Kết luận: {ghiChuSach}",
            TaoLuc = now
        });

        var giaSuNhanTbId = don.GiaSuMonHoc?.GiaSuId ?? Guid.Empty;
        if (giaSuNhanTbId != Guid.Empty)
        {
            _db.ThongBaos.Add(new ThongBao
            {
                NguoiNhanId = giaSuNhanTbId,
                Loai = LoaiThongBao.KHIEU_NAI,
                TieuDe = $"Kết quả phân xử khiếu nại đơn {don.MaDon}",
                NoiDung = $"{nhanAdminKn} đã phân xử: {ketQuaMoTa} Kết luận: {ghiChuSach}",
                TaoLuc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        kn.NguoiGiaiQuyet = quanTriGiaiQuyet;
        return AnhXaKhieuNaiChiTiet(kn);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // NỘI BỘ DÙNG CHUNG & BACKGROUND JOB ĐỊNH KỲ
    // ══════════════════════════════════════════════════════════════════════════

    public async Task HoanToanBoVeViHocSinhTrongGiaoDichAsync(
        ThanhToan thanhToan,
        Guid hocSinhId,
        string ghiChu,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (thanhToan.TrangThai != TrangThaiThanhToan.DANG_GIU)
        {
            return;
        }

        var viHs = await _db.TaiKhoanVis
            .FirstOrDefaultAsync(v => v.NguoiDungThamGiaId == hocSinhId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví học sinh để hoàn tiền.", 404);

        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"vi:{viHs.Id:N}"], cancellationToken);
        await _db.Entry(viHs).ReloadAsync(cancellationToken);

        await HoanTienVeViHocSinhNoiBoAsync(
            thanhToan,
            viHs,
            thanhToan.SoTien,
            datDaHoan: true,
            ghiChu,
            mocThoiGianUtc ?? DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<KetQuaChuKyTuDongDto> ChayChuKyTuDongXuLyDonVaGiaiNganAsync(
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default)
    {
        var now = mocThoiGianUtc ?? DateTime.UtcNow;
        var mocGiaiNgan = now.AddHours(-_policy.AutoDisbursementHours);

        var soGiaiNgan = 0;
        var soHetHan = 0;
        var soHoanTien = 0;

        // 1) Tự động giải ngân đơn DA_XAC_NHAN đã kết thúc >= AutoDisbursementHours (bỏ qua TRANH_CHAP)
        var dsCanGiaiNganIds = await _db.DonDatLichs
            .AsNoTracking()
            .Where(d => d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN &&
                        d.KetThucLuc <= mocGiaiNgan &&
                        !d.DsKhieuNai.Any(k => k.TrangThai == TrangThaiKhieuNai.MO))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        foreach (var donId in dsCanGiaiNganIds)
        {
            var don = await _db.DonDatLichs
                .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)!.ThenInclude(g => g!.TaiKhoanVi)
                .Include(d => d.DsThanhToan)
                .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken);

            if (don is null || don.TrangThai != TrangThaiDonDatLich.DA_XAC_NHAN)
            {
                continue;
            }

            var tt = don.DsThanhToan.FirstOrDefault();
            // Nếu đơn có phí nhưng chưa từng thanh toán DANG_GIU => xử lý ở nhánh hết hạn phòng thủ bên dưới
            if (don.TongTien > 0 && (tt is null || tt.TrangThai != TrangThaiThanhToan.DANG_GIU))
            {
                continue;
            }

            var viGs = don.GiaSuMonHoc?.GiaSu?.TaiKhoanVi;
            if (viGs is null) continue;

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(
                _db,
                [$"don:{don.Id:N}", $"vi:{viGs.Id:N}"],
                cancellationToken);

            await GiaiNganDonTrongGiaoDichAsync(don, viGs, now, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            soGiaiNgan++;
        }

        // 2) Hết hạn đơn CHO_XAC_NHAN quá min(HanPhanHoi, BatDauLuc)
        var dsChoHetHanIds = await _db.DonDatLichs
            .AsNoTracking()
            .Where(d => d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN &&
                        (d.HanPhanHoi <= now || d.BatDauLuc <= now))
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        foreach (var donId in dsChoHetHanIds)
        {
            var don = await _db.DonDatLichs
                .Include(d => d.DsThanhToan)
                .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken);

            if (don is null || don.TrangThai != TrangThaiDonDatLich.CHO_XAC_NHAN)
            {
                continue;
            }

            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"don:{don.Id:N}"], cancellationToken);
            await _db.Entry(don).ReloadAsync(cancellationToken);

            if (don.TrangThai != TrangThaiDonDatLich.CHO_XAC_NHAN)
            {
                await tx.RollbackAsync(cancellationToken);
                continue;
            }

            don.TrangThai = TrangThaiDonDatLich.HET_HAN;
            var tt = don.DsThanhToan.FirstOrDefault();
            if (tt is { TrangThai: TrangThaiThanhToan.DANG_GIU })
            {
                await HoanToanBoVeViHocSinhTrongGiaoDichAsync(
                    tt,
                    don.HocSinhId,
                    $"Hoàn tiền 100% do đơn {don.MaDon} hết hạn phản hồi",
                    now,
                    cancellationToken);
                soHoanTien++;
            }
            else if (tt is { TrangThai: TrangThaiThanhToan.CHO_THANH_TOAN })
            {
                tt.TrangThai = TrangThaiThanhToan.THAT_BAI;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            soHetHan++;
        }

        // 3) Phòng thủ: Đơn DA_XAC_NHAN có phí nhưng chưa có ThanhToan DANG_GIU đến BatDauLuc => chuyển HET_HAN
        var dsXacNhanChuaTra = await _db.DonDatLichs
            .Include(d => d.DsThanhToan)
            .Where(d => d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN &&
                        d.BatDauLuc <= now &&
                        !d.DsThanhToan.Any(t => t.TrangThai == TrangThaiThanhToan.DANG_GIU ||
                                                t.TrangThai == TrangThaiThanhToan.DA_GIAI_NGAN))
            .ToListAsync(cancellationToken);

        foreach (var don in dsXacNhanChuaTra.Where(d => d.TongTien > 0))
        {
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
            await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"don:{don.Id:N}"], cancellationToken);
            await _db.Entry(don).ReloadAsync(cancellationToken);

            if (don.TrangThai != TrangThaiDonDatLich.DA_XAC_NHAN)
            {
                await tx.RollbackAsync(cancellationToken);
                continue;
            }

            don.TrangThai = TrangThaiDonDatLich.HET_HAN;
            var tt = don.DsThanhToan.FirstOrDefault();
            if (tt is { TrangThai: TrangThaiThanhToan.CHO_THANH_TOAN })
            {
                tt.TrangThai = TrangThaiThanhToan.THAT_BAI;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            soHetHan++;
        }

        return new KetQuaChuKyTuDongDto(soGiaiNgan, soHetHan, soHoanTien);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // HÀM NỘI BỘ HỖ TRỢ
    // ══════════════════════════════════════════════════════════════════════════

    private async Task XacNhanMatKhauNhayCamAsync(
        NguoiDung nguoiDung,
        string? matKhauXacNhan,
        string doiTuong,
        string? diaChiIp,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var mocCuaSo = now.AddMinutes(-CuaSoKhoaMatKhauPhut);

        var soLanSaiGanDay = await _db.AuditLogs
            .AsNoTracking()
            .CountAsync(a => a.NguoiDungId == nguoiDung.Id &&
                             a.HanhDong == "XAC_NHAN_MAT_KHAU_SAI" &&
                             a.TaoLuc >= mocCuaSo,
                        cancellationToken);

        if (soLanSaiGanDay >= SoLanSaiMatKhauToiDa)
        {
            throw new LoiUngDungException(
                "QUA_NHIEU_LAN_SAI",
                $"Bạn đã nhập sai mật khẩu quá {SoLanSaiMatKhauToiDa} lần trong {CuaSoKhoaMatKhauPhut} phút. Vui lòng thử lại sau.",
                429);
        }

        if (string.IsNullOrWhiteSpace(matKhauXacNhan) ||
            !XacThucService.XacMinhMatKhau(matKhauXacNhan, nguoiDung.MatKhauHash))
        {
            _db.AuditLogs.Add(new AuditLog
            {
                NguoiDungId = nguoiDung.Id,
                HanhDong = "XAC_NHAN_MAT_KHAU_SAI",
                DoiTuong = doiTuong,
                DiaChiIp = diaChiIp,
                TaoLuc = now
            });
            await _db.SaveChangesAsync(cancellationToken);

            throw new LoiUngDungException(
                "MAT_KHAU_SAI",
                "Mật khẩu xác nhận không chính xác.",
                401);
        }
    }

    private async Task GiaiNganDonTrongGiaoDichAsync(
        DonDatLich don,
        TaiKhoanVi viGs,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await _db.Entry(don).ReloadAsync(cancellationToken);
        await _db.Entry(viGs).ReloadAsync(cancellationToken);

        if (don.TrangThai != TrangThaiDonDatLich.DA_XAC_NHAN)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Đơn đặt lịch không còn ở trạng thái đã xác nhận.", 409);
        }

        if (don.TongTien <= 0)
        {
            don.TrangThai = TrangThaiDonDatLich.HOAN_THANH;
            return;
        }

        var tt = await _db.ThanhToans
            .FirstOrDefaultAsync(t => t.DonDatLichId == don.Id, cancellationToken)
            ?? throw new LoiUngDungException("CHUA_THANH_TOAN", "Đơn có phí chưa có giao dịch thanh toán để giải ngân.", 409);

        if (tt.TrangThai != TrangThaiThanhToan.DANG_GIU)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Giao dịch thanh toán không ở trạng thái đang tạm giữ.", 409);
        }

        var soHocVienTra = Math.Max(0, tt.SoTien - tt.TienDaHoan);
        var (_, _, thucNhan) = BoTinhTaiChinhDon.TinhTaiChinhDon(soHocVienTra, _policy.DefaultPlatformFeePercent);

        don.TrangThai = TrangThaiDonDatLich.HOAN_THANH;
        tt.TrangThai = TrangThaiThanhToan.DA_GIAI_NGAN;
        tt.GiaiNganLuc = now;

        if (thucNhan > 0)
        {
            viGs.SoDu += thucNhan;
            var soThuTu = await LaySoThuTuTiepTheoAsync(viGs.Id, cancellationToken);
            _db.GiaoDichVis.Add(new GiaoDichVi
            {
                TaiKhoanViId = viGs.Id,
                SoThuTu = soThuTu,
                Loai = LoaiGiaoDichVi.THU_NHAP,
                SoTien = thucNhan,
                SoDuSau = viGs.SoDu + viGs.SoDuDangXuLyRut,
                ThanhToanId = tt.Id,
                GhiChu = $"Giải ngân buổi dạy đơn {don.MaDon}",
                TaoLuc = now
            });
        }
    }

    private async Task HoanTienVeViHocSinhNoiBoAsync(
        ThanhToan tt,
        TaiKhoanVi viHs,
        int soTienHoan,
        bool datDaHoan,
        string ghiChu,
        DateTime now,
        CancellationToken cancellationToken)
    {
        tt.TienDaHoan = soTienHoan;
        if (datDaHoan)
        {
            tt.TrangThai = TrangThaiThanhToan.DA_HOAN;
        }

        if (soTienHoan > 0)
        {
            viHs.SoDu += soTienHoan;
            var soThuTu = await LaySoThuTuTiepTheoAsync(viHs.Id, cancellationToken);
            _db.GiaoDichVis.Add(new GiaoDichVi
            {
                TaiKhoanViId = viHs.Id,
                SoThuTu = soThuTu,
                Loai = LoaiGiaoDichVi.HOAN_TIEN,
                SoTien = soTienHoan,
                SoDuSau = viHs.SoDu + viHs.SoDuDangXuLyRut,
                ThanhToanId = tt.Id,
                GhiChu = ghiChu,
                TaoLuc = now
            });
        }
    }

    private async Task<int> LaySoThuTuTiepTheoAsync(Guid taiKhoanViId, CancellationToken cancellationToken)
    {
        var maxStt = await _db.GiaoDichVis
            .Where(g => g.TaiKhoanViId == taiKhoanViId)
            .MaxAsync(g => (int?)g.SoThuTu, cancellationToken) ?? 0;

        return maxStt + 1;
    }

    private static GiaoDichViGiaSuDto AnhXaGiaoDichViGiaSu(GiaoDichVi gd)
    {
        var soDuTruoc = gd.SoDuSau - gd.SoTien;

        int? soHocVienTra = null;
        int? phiNenTang = null;
        decimal? tyLePhi = null;
        Guid? donId = null;
        string? maDon = null;
        string? tenHocVien = null;
        string? tenMonHoc = null;

        if (gd.Loai == LoaiGiaoDichVi.THU_NHAP && gd.ThanhToan is not null)
        {
            var hvTra = Math.Max(0, gd.ThanhToan.SoTien - gd.ThanhToan.TienDaHoan);
            var phi = Math.Max(0, hvTra - gd.SoTien);
            soHocVienTra = hvTra;
            phiNenTang = phi;
            tyLePhi = hvTra > 0
                ? Math.Round(phi * 100m / hvTra, 2, MidpointRounding.AwayFromZero)
                : 0m;

            donId = gd.ThanhToan.DonDatLichId;
            maDon = gd.ThanhToan.DonDatLich?.MaDon;
            tenHocVien = gd.ThanhToan.DonDatLich?.HocSinh?.HoTen;
            tenMonHoc = gd.ThanhToan.DonDatLich?.GiaSuMonHoc?.MonHoc?.Ten;
        }

        string? tenNganHangRut = gd.YeuCauRutTien?.TenNganHang;
        string? soTaiKhoanRutChe = gd.YeuCauRutTien is null ? null : CheSoTaiKhoan(gd.YeuCauRutTien.SoTaiKhoan);

        string? nguoiThucHien = null;
        string? emailNguoiThucHien = null;
        if (gd.Loai == LoaiGiaoDichVi.RUT_TIEN)
        {
            if (gd.YeuCauRutTien?.NguoiXuLy is not null)
            {
                nguoiThucHien = $"Quản trị viên {gd.YeuCauRutTien.NguoiXuLy.HoTen}";
                emailNguoiThucHien = gd.YeuCauRutTien.NguoiXuLy.Email;
            }
            else
            {
                nguoiThucHien = "Quản trị viên duyệt chi trả";
            }
        }
        else if (gd.Loai == LoaiGiaoDichVi.THU_NHAP)
        {
            var knDaGiaiQuyet = gd.ThanhToan?.DonDatLich?.DsKhieuNai
                .OrderByDescending(k => k.GiaiQuyetLuc ?? k.TaoLuc)
                .FirstOrDefault(k => k.NguoiGiaiQuyet is not null);
            var ghiChuLower = (gd.GhiChu ?? string.Empty).ToLowerInvariant();
            if (knDaGiaiQuyet?.NguoiGiaiQuyet is not null &&
                (ghiChuLower.Contains("khiếu nại") || ghiChuLower.Contains("khieu nai")))
            {
                nguoiThucHien = $"Quản trị viên {knDaGiaiQuyet.NguoiGiaiQuyet.HoTen} (Phân xử khiếu nại)";
                emailNguoiThucHien = knDaGiaiQuyet.NguoiGiaiQuyet.Email;
            }
            else if (ghiChuLower.Contains("xác nhận hoàn thành") || ghiChuLower.Contains("hoàn thành sớm"))
            {
                nguoiThucHien = !string.IsNullOrWhiteSpace(tenHocVien)
                    ? $"Học viên {tenHocVien} (Xác nhận hoàn thành)"
                    : "Học viên (Xác nhận hoàn thành)";
                emailNguoiThucHien = gd.ThanhToan?.DonDatLich?.HocSinh?.Email;
            }
            else
            {
                nguoiThucHien = "Hệ thống tự động (Giải ngân sau buổi học)";
            }
        }

        return new GiaoDichViGiaSuDto(
            Id: gd.Id,
            SoThuTu: gd.SoThuTu,
            Loai: gd.Loai,
            SoTien: gd.SoTien,
            SoDuTruoc: soDuTruoc,
            SoDuSau: gd.SoDuSau,
            SoHocVienTra: soHocVienTra,
            PhiNenTang: phiNenTang,
            TyLePhi: tyLePhi,
            DonDatLichId: donId,
            MaDon: maDon,
            TenHocVien: tenHocVien,
            TenMonHoc: tenMonHoc,
            YeuCauRutTienId: gd.YeuCauRutTienId,
            TenNganHangRut: tenNganHangRut,
            SoTaiKhoanRutChe: soTaiKhoanRutChe,
            GhiChu: gd.GhiChu,
            TaoLuc: gd.TaoLuc,
            NguoiThucHien: nguoiThucHien,
            EmailNguoiThucHien: emailNguoiThucHien);
    }

    private static string TaoMaLenhRut(YeuCauRutTien r)
    {
        var ngay = r.TaoLuc == default ? DateTime.UtcNow : r.TaoLuc;
        var hauTo = r.Id.ToString("N")[..6].ToUpperInvariant();
        return $"RUT-{ngay:yyMMdd}-{hauTo}";
    }

    private static string? TraCuuMaBinTheoTenNganHang(string? tenNganHang)
    {
        if (string.IsNullOrWhiteSpace(tenNganHang))
        {
            return null;
        }

        var decomposed = tenNganHang.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark &&
                char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        var norm = sb.ToString();
        if (norm.Length == 0)
        {
            return null;
        }

        return norm switch
        {
            _ when norm.Contains("vietcombank") || norm == "vcb" => "970436",
            _ when norm.Contains("mbbank") || norm == "mb" || norm.Contains("quandoi") => "970422",
            _ when norm.Contains("techcombank") || norm == "tcb" => "970407",
            _ when norm.Contains("bidv") => "970418",
            _ when norm.Contains("vietinbank") || norm == "ctg" || norm == "icb" => "970415",
            _ when norm.Contains("acb") || norm.Contains("achau") => "970416",
            _ when norm.Contains("cake") => "546034",
            _ when norm.Contains("ubank") => "546035",
            _ when norm.Contains("vpbank") || norm == "vpb" => "970432",
            _ when norm.Contains("tpbank") || norm == "tpb" || norm.Contains("tienphong") => "970423",
            _ when norm.Contains("agribank") || norm == "vba" => "970405",
            _ when norm.Contains("sacombank") || norm == "stb" => "970403",
            _ when norm.Contains("hdbank") || norm == "hdb" => "970437",
            _ when norm.Contains("vib") => "970441",
            _ when norm.Contains("shb") => "970443",
            _ when norm.Contains("msb") || norm.Contains("maritime") => "970426",
            _ when norm.Contains("ocb") || norm.Contains("phuongdong") => "970448",
            _ when norm.Contains("eximbank") || norm == "eib" => "970431",
            _ when norm.Contains("seabank") || norm == "seab" => "970440",
            _ when norm.Contains("lpbank") || norm.Contains("lienviet") => "970449",
            _ when norm.Contains("namabank") || norm == "nab" => "970428",
            _ when norm.Contains("abbank") || norm == "abb" => "970425",
            _ when norm.Contains("bacabank") || norm == "bab" => "970409",
            _ when norm.Contains("bvbank") || norm.Contains("banviet") || norm.Contains("vietcapital") => "970454",
            _ when norm.Contains("timo") => "963388",
            _ when norm.Contains("pvcombank") => "970412",
            _ when norm.Contains("ncb") => "970419",
            _ when norm.Contains("vietbank") => "970433",
            _ when norm.Contains("kienlong") => "970452",
            _ when norm.Contains("saigonbank") => "970400",
            _ when norm.Contains("pgbank") => "970430",
            _ when norm.Contains("vietabank") => "970427",
            _ when norm.Contains("baoviet") => "970438",
            _ when norm.Contains("shinhan") => "970424",
            _ when norm.Contains("woori") => "970457",
            _ when norm.Contains("hsbc") => "458761",
            _ when norm.Contains("cimb") => "422589",
            _ => null
        };
    }

    private static YeuCauRutTienDto AnhXaYeuCauRutTien(YeuCauRutTien r, bool choQuanTri = false)
    {
        var maLenh = TaoMaLenhRut(r);
        var hauTo = r.Id.ToString("N")[..6].ToUpperInvariant();
        var noiDungCk = $"TH RUT {hauTo}";
        var maBin = TraCuuMaBinTheoTenNganHang(r.TenNganHang);
        string? vietQrUrl = null;
        if (choQuanTri && !string.IsNullOrWhiteSpace(maBin) && !string.IsNullOrWhiteSpace(r.SoTaiKhoan))
        {
            vietQrUrl =
                $"https://img.vietqr.io/image/{Uri.EscapeDataString(maBin)}-{Uri.EscapeDataString(r.SoTaiKhoan.Trim())}-compact2.png" +
                $"?amount={r.SoTien}&addInfo={Uri.EscapeDataString(noiDungCk)}&accountName={Uri.EscapeDataString(r.ChuTaiKhoan.Trim())}";
        }

        return new YeuCauRutTienDto(
            Id: r.Id,
            GiaSuId: r.GiaSuId,
            TenGiaSu: r.GiaSu?.HoTen ?? "Gia sư",
            EmailGiaSu: r.GiaSu?.Email ?? string.Empty,
            SoTien: r.SoTien,
            TrangThai: r.TrangThai,
            TenNganHang: r.TenNganHang,
            SoTaiKhoanChe: CheSoTaiKhoan(r.SoTaiKhoan),
            ChuTaiKhoan: r.ChuTaiKhoan,
            GhiChu: r.GhiChu,
            TaoLuc: r.TaoLuc,
            XuLyLuc: r.XuLyLuc,
            MaLenhRut: maLenh,
            NoiDungChuyenKhoan: noiDungCk,
            MaBinNganHang: maBin,
            SoTaiKhoanDayDu: choQuanTri ? r.SoTaiKhoan : null,
            VietQrUrl: vietQrUrl,
            NguoiXuLyId: r.NguoiXuLyId,
            TenNguoiXuLy: r.NguoiXuLy?.HoTen,
            EmailNguoiXuLy: r.NguoiXuLy?.Email);
    }

    private KhieuNaiChiTietDto AnhXaKhieuNaiChiTiet(KhieuNai k)
    {
        var don = k.DonDatLich;
        var tt = don?.DsThanhToan.FirstOrDefault();
        var thoiLuongPhut = don is not null
            ? Math.Max(1, (int)Math.Round((don.KetThucLuc - don.BatDauLuc).TotalMinutes))
            : 60;
        var urlHs = !string.IsNullOrWhiteSpace(k.BangChungUrl)
            ? (k.BangChungUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? k.BangChungUrl
                : _luuTruTep.TaoSignedUrlXemTep(k.BangChungUrl, TimeSpan.FromMinutes(15)))
            : null;
        var urlGs = !string.IsNullOrWhiteSpace(k.BangChungGiaSuUrl)
            ? (k.BangChungGiaSuUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? k.BangChungGiaSuUrl
                : _luuTruTep.TaoSignedUrlXemTep(k.BangChungGiaSuUrl, TimeSpan.FromMinutes(15)))
            : null;

        return new KhieuNaiChiTietDto(
            Id: k.Id,
            DonDatLichId: k.DonDatLichId,
            MaDon: don?.MaDon ?? string.Empty,
            HocSinhId: don?.HocSinhId ?? Guid.Empty,
            TenHocSinh: don?.HocSinh?.HoTen ?? "Học sinh",
            GiaSuId: don?.GiaSuMonHoc?.GiaSuId ?? Guid.Empty,
            TenGiaSu: don?.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư",
            TenMonHoc: don?.GiaSuMonHoc?.MonHoc?.Ten ?? "Môn học",
            DanhMuc: k.DanhMuc,
            MoTa: k.MoTa,
            BangChungUrl: k.BangChungUrl,
            TrangThai: k.TrangThai,
            SoTienThanhToan: tt?.SoTien ?? 0,
            TienDaHoan: tt?.TienDaHoan ?? 0,
            GhiChuGiaiQuyet: k.GhiChuGiaiQuyet,
            TaoLuc: k.TaoLuc,
            GiaiQuyetLuc: k.GiaiQuyetLuc,
            HinhThuc: don?.HinhThuc ?? HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc: don?.BatDauLuc,
            KetThucLuc: don?.KetThucLuc,
            ThoiLuongPhut: thoiLuongPhut,
            GiaMoiGioSnapshot: don?.GiaMoiGio ?? 0,
            TienGiamSnapshot: don?.TienGiam ?? 0,
            PhanHoiGiaSu: k.PhanHoiGiaSu,
            BangChungGiaSuUrl: k.BangChungGiaSuUrl,
            PhanHoiGiaSuLuc: k.PhanHoiGiaSuLuc,
            UrlXemBangChungHocSinh: urlHs,
            UrlXemBangChungGiaSu: urlGs,
            TyLePhiHienHanh: _policy.DefaultPlatformFeePercent,
            NhatKyPhongHoc: don is not null && don.HinhThuc == HinhThucBuoiHoc.TRUC_TUYEN
                ? TinhTomTatNhatKyPhongHoc(don, don.DsNhatKyPhongHoc.ToList(), DateTime.UtcNow)
                : null,
            NguoiGiaiQuyetId: k.NguoiGiaiQuyetId,
            TenNguoiGiaiQuyet: k.NguoiGiaiQuyet?.HoTen,
            EmailNguoiGiaiQuyet: k.NguoiGiaiQuyet?.Email);
    }

    private DonDatLichChiTietDto AnhXaDonDatLichChiTiet(DonDatLich d)
    {
        var tt = d.DsThanhToan.FirstOrDefault();
        var soHocVienTra = tt is not null ? Math.Max(0, tt.SoTien - tt.TienDaHoan) : d.TongTien;
        var (_, phiNenTang, thuNhapGiaSu) = BoTinhTaiChinhDon.TinhTaiChinhDon(
            soHocVienTra,
            _policy.DefaultPlatformFeePercent);

        return new DonDatLichChiTietDto(
            Id: d.Id,
            MaDon: d.MaDon,
            TrangThai: d.TrangThai,
            HinhThuc: d.HinhThuc,
            DiaChiHocSinh: d.DiaChiHocSinh,
            BatDauLuc: d.BatDauLuc,
            KetThucLuc: d.KetThucLuc,
            GiaMoiGio: d.GiaMoiGio,
            TienGoc: d.TienGoc,
            TienGiam: d.TienGiam,
            TongTien: d.TongTien,
            PhiNenTang: phiNenTang,
            ThuNhapGiaSu: thuNhapGiaSu,
            LaBuoiHocThu: d.LaBuoiHocThu,
            PhongHocUrl: d.PhongHocUrl,
            LyDoHuy: d.LyDoHuy,
            HanPhanHoi: d.HanPhanHoi,
            TaoLuc: d.TaoLuc,
            GiaSuMonHocId: d.GiaSuMonHocId,
            MonHocId: d.GiaSuMonHoc?.MonHocId ?? Guid.Empty,
            TenMonHoc: d.GiaSuMonHoc?.MonHoc?.Ten ?? "Môn học",
            GiaSuId: d.GiaSuMonHoc?.GiaSuId ?? Guid.Empty,
            TenGiaSu: d.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư",
            HocSinhId: d.HocSinhId,
            TenHocSinh: d.HocSinh?.HoTen ?? "Học sinh",
            TrangThaiThanhToan: tt?.TrangThai);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VÍ HỌC SINH & NẠP TIỀN VIETQR TỰ ĐỘNG (PAYOS API V2)
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<TongQuanViHocSinhDto> LayTongQuanViHocSinhAsync(
        Guid hocSinhId,
        CancellationToken cancellationToken = default)
    {
        var vi = await _db.TaiKhoanVis
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.NguoiDungThamGiaId == hocSinhId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví học tập của bạn.", 404);

        var dsGd = await _db.GiaoDichVis
            .AsNoTracking()
            .Where(g => g.TaiKhoanViId == vi.Id)
            .Select(g => new { g.Loai, g.SoTien })
            .ToListAsync(cancellationToken);

        var tongDaNap = dsGd.Where(g => g.Loai == LoaiGiaoDichVi.NAP_TIEN).Sum(g => g.SoTien);
        var tongDaChi = dsGd.Where(g => g.Loai == LoaiGiaoDichVi.THANH_TOAN).Sum(g => Math.Abs(g.SoTien));
        var tongDaHoan = dsGd.Where(g => g.Loai == LoaiGiaoDichVi.HOAN_TIEN).Sum(g => g.SoTien);

        var dsNap = await _db.YeuCauNapTiens
            .AsNoTracking()
            .Where(n => n.NguoiDungThamGiaId == hocSinhId)
            .OrderByDescending(n => n.TaoLuc)
            .Take(8)
            .ToListAsync(cancellationToken);

        return new TongQuanViHocSinhDto(
            SoDu: vi.SoDu,
            TongDaNap: tongDaNap,
            TongDaChi: tongDaChi,
            TongDaHoan: tongDaHoan,
            PayOsDaCauHinh: _payOsClient?.DaCauHinh == true,
            DsNapGanDay: dsNap.Select(n => AnhXaYeuCauNapTien(n)).ToList());
    }

    public async Task<KetQuaPhanTrangDto<GiaoDichViHocSinhDto>> LayDsGiaoDichViHocSinhAsync(
        Guid hocSinhId,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        trang = Math.Max(1, trang);
        kichThuocTrang = Math.Clamp(kichThuocTrang, 1, 50);

        var vi = await _db.TaiKhoanVis
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.NguoiDungThamGiaId == hocSinhId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví học tập của bạn.", 404);

        var query = _db.GiaoDichVis
            .AsNoTracking()
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(g => g.ThanhToan)!.ThenInclude(t => t!.DonDatLich)!.ThenInclude(d => d!.DsKhieuNai).ThenInclude(k => k.NguoiGiaiQuyet)
            .AsSplitQuery()
            .Where(g => g.TaiKhoanViId == vi.Id);

        var tong = await query.CountAsync(cancellationToken);
        var ds = await query
            .OrderByDescending(g => g.SoThuTu)
            .Skip((trang - 1) * kichThuocTrang)
            .Take(kichThuocTrang)
            .ToListAsync(cancellationToken);

        var ketQua = ds.Select(g =>
        {
            var (nguoiThucHien, emailNguoiThucHien, phuongThuc) = XacDinhNguoiThucHienGdHocSinh(g);
            return new GiaoDichViHocSinhDto(
                Id: g.Id,
                SoThuTu: g.SoThuTu,
                Loai: g.Loai,
                SoTien: g.SoTien,
                SoDuTruoc: g.SoDuSau - g.SoTien,
                SoDuSau: g.SoDuSau,
                GhiChu: g.GhiChu,
                ThanhToanId: g.ThanhToanId,
                TaoLuc: g.TaoLuc,
                NguoiThucHien: nguoiThucHien,
                EmailNguoiThucHien: emailNguoiThucHien,
                PhuongThuc: phuongThuc);
        }).ToList();

        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocTrang);
        return new KetQuaPhanTrangDto<GiaoDichViHocSinhDto>(ketQua, trang, kichThuocTrang, tong, tongTrang);
    }

    private static (string NguoiThucHien, string? EmailNguoiThucHien, string? PhuongThuc) XacDinhNguoiThucHienGdHocSinh(GiaoDichVi g)
    {
        var ghiChuLower = (g.GhiChu ?? string.Empty).ToLowerInvariant();
        if (g.Loai == LoaiGiaoDichVi.NAP_TIEN)
        {
            return ghiChuLower.Contains("payos") || ghiChuLower.Contains("vietqr")
                ? ("Học viên (Tự nạp qua PayOS VietQR)", null, "PAYOS_VIETQR")
                : ("Học viên (Nạp vào Ví học tập)", null, "VI_NOI_BO");
        }

        if (g.Loai == LoaiGiaoDichVi.THANH_TOAN)
        {
            var hs = g.ThanhToan?.DonDatLich?.HocSinh;
            var pt = g.ThanhToan?.PhuongThuc.ToString() ?? "VI_NOI_BO";
            var nhanPt = pt switch
            {
                "VI_NOI_BO" => "Ví nội bộ",
                "VNPAY" => "VNPay",
                _ => "Thanh toán trực tuyến"
            };
            var ten = hs is not null ? $"Học viên {hs.HoTen} ({nhanPt})" : $"Học viên ({nhanPt})";
            return (ten, hs?.Email, pt);
        }

        if (g.Loai == LoaiGiaoDichVi.HOAN_TIEN)
        {
            var knDaGiaiQuyet = g.ThanhToan?.DonDatLich?.DsKhieuNai
                .OrderByDescending(k => k.GiaiQuyetLuc ?? k.TaoLuc)
                .FirstOrDefault(k => k.NguoiGiaiQuyet is not null);
            if (knDaGiaiQuyet?.NguoiGiaiQuyet is not null &&
                (ghiChuLower.Contains("khiếu nại") || ghiChuLower.Contains("khieu nai")))
            {
                return (
                    $"Quản trị viên {knDaGiaiQuyet.NguoiGiaiQuyet.HoTen} (Phân xử khiếu nại)",
                    knDaGiaiQuyet.NguoiGiaiQuyet.Email,
                    "VI_NOI_BO");
            }

            if (ghiChuLower.Contains("từ chối") || ghiChuLower.Contains("tu choi"))
            {
                var gs = g.ThanhToan?.DonDatLich?.GiaSuMonHoc?.GiaSu;
                return (
                    gs is not null ? $"Gia sư {gs.HoTen} từ chối lịch (Hoàn tự động)" : "Gia sư từ chối lịch (Hoàn tự động)",
                    gs?.Email,
                    "VI_NOI_BO");
            }

            if (ghiChuLower.Contains("hết hạn") || ghiChuLower.Contains("het han"))
            {
                return ("Hệ thống tự động (Đơn hết hạn phản hồi)", null, "VI_NOI_BO");
            }

            return ("Hệ thống hoàn tiền về Ví", null, "VI_NOI_BO");
        }

        return ("Hệ thống TutorHub", null, null);
    }

    public async Task<YeuCauNapTienDto> TaoYeuCauNapTienVietQrAsync(
        Guid hocSinhId,
        TaoYeuCauNapTienDto yeuCau,
        string? originUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (yeuCau.SoTien < SoTienNapToiThieu || yeuCau.SoTien > SoTienNapToiDa)
        {
            throw new LoiUngDungException(
                "SO_TIEN_NAP_KHONG_HOP_LE",
                $"Số tiền nạp phải nằm trong khoảng từ {SoTienNapToiThieu:N0} ₫ đến {SoTienNapToiDa:N0} ₫.",
                422);
        }

        if (_payOsClient is null || !_payOsClient.DaCauHinh)
        {
            throw new LoiUngDungException(
                "CHUA_CAU_HINH_PAYOS",
                "Cổng nạp VietQR PayOS chưa được gắn khóa API (PayOs__ClientId, PayOs__ApiKey, PayOs__ChecksumKey). Hãy thiết lập 3 biến môi trường này trên máy chủ để kích hoạt nạp tiền thật.",
                503);
        }

        var viExists = await _db.TaiKhoanVis
            .AsNoTracking()
            .AnyAsync(v => v.NguoiDungThamGiaId == hocSinhId, cancellationToken);
        if (!viExists)
        {
            throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví học tập của bạn.", 404);
        }

        var now = DateTime.UtcNow;
        var hetHanLuc = now.AddMinutes(15);

        // Sinh orderCode nguyên dương duy nhất cho PayOS (<= 15 chữ số)
        var baseCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds() * 1000L + Random.Shared.Next(100, 999);
        var maDonPayOs = baseCode;
        var maNgan = (maDonPayOs % 1_000_000).ToString("D6");
        var noiDungChuyen = $"THNAP {maNgan}";

        var baseFrontend = !string.IsNullOrWhiteSpace(originUrl)
            ? originUrl.TrimEnd('/')
            : "https://tutorhub-web-nqhn.onrender.com";
        var returnUrl = $"{baseFrontend}/pages/hoc-sinh-tong-quan.html?tab=tab-tai-khoan&napKetQua=thanh-cong&orderCode={maDonPayOs}";
        var cancelUrl = $"{baseFrontend}/pages/hoc-sinh-tong-quan.html?tab=tab-tai-khoan&napKetQua=huy&orderCode={maDonPayOs}";
        var expiredAtUnix = new DateTimeOffset(hetHanLuc).ToUnixTimeSeconds();

        var payOsRes = await _payOsClient.TaoYeuCauThanhToanAsync(
            maDonPayOs,
            yeuCau.SoTien,
            noiDungChuyen,
            returnUrl,
            cancelUrl,
            expiredAtUnix,
            cancellationToken);

        var entity = new YeuCauNapTien
        {
            NguoiDungThamGiaId = hocSinhId,
            MaDonPayOs = payOsRes.OrderCode,
            SoTien = yeuCau.SoTien,
            CongThanhToan = "PAYOS_VIETQR",
            TrangThai = TrangThaiNapTien.CHO_THANH_TOAN,
            PaymentLinkId = payOsRes.PaymentLinkId,
            CheckoutUrl = payOsRes.CheckoutUrl,
            QrCode = payOsRes.QrCode,
            MaBinNganHang = payOsRes.Bin,
            SoTaiKhoan = payOsRes.AccountNumber,
            ChuTaiKhoan = payOsRes.AccountName,
            NoiDungChuyenKhoan = string.IsNullOrWhiteSpace(payOsRes.Description) ? noiDungChuyen : payOsRes.Description,
            TaoLuc = now,
            HetHanLuc = hetHanLuc
        };

        _db.YeuCauNapTiens.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return AnhXaYeuCauNapTien(entity);
    }

    public async Task<YeuCauNapTienDto> KiemTraVaDongBoNapTienAsync(
        Guid hocSinhId,
        Guid yeuCauNapTienId,
        CancellationToken cancellationToken = default)
    {
        var yeuCau = await _db.YeuCauNapTiens
            .FirstOrDefaultAsync(n => n.Id == yeuCauNapTienId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy yêu cầu nạp tiền.", 404);

        if (yeuCau.NguoiDungThamGiaId != hocSinhId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền truy cập yêu cầu nạp tiền này.", 403);
        }

        if (yeuCau.TrangThai == TrangThaiNapTien.CHO_THANH_TOAN && _payOsClient is { DaCauHinh: true })
        {
            var trangThaiCong = await _payOsClient.LayThongTinDonAsync(yeuCau.MaDonPayOs, cancellationToken);
            if (trangThaiCong is not null)
            {
                if (string.Equals(trangThaiCong.Status, "PAID", StringComparison.OrdinalIgnoreCase) &&
                    trangThaiCong.AmountPaid >= yeuCau.SoTien)
                {
                    return await HoanTatNapTienTrongGiaoDichAsync(
                        yeuCau.Id,
                        trangThaiCong.Reference,
                        cancellationToken);
                }

                if (string.Equals(trangThaiCong.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    yeuCau.TrangThai = TrangThaiNapTien.DA_HUY;
                    await _db.SaveChangesAsync(cancellationToken);
                }
                else if (string.Equals(trangThaiCong.Status, "EXPIRED", StringComparison.OrdinalIgnoreCase) ||
                         DateTime.UtcNow >= yeuCau.HetHanLuc)
                {
                    yeuCau.TrangThai = TrangThaiNapTien.HET_HAN;
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }
            else if (DateTime.UtcNow >= yeuCau.HetHanLuc)
            {
                yeuCau.TrangThai = TrangThaiNapTien.HET_HAN;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var soDuHienTai = await _db.TaiKhoanVis
            .AsNoTracking()
            .Where(v => v.NguoiDungThamGiaId == hocSinhId)
            .Select(v => (int?)v.SoDu)
            .FirstOrDefaultAsync(cancellationToken);

        return AnhXaYeuCauNapTien(yeuCau, soDuHienTai);
    }

    public async Task<YeuCauNapTienDto> HuyYeuCauNapTienAsync(
        Guid hocSinhId,
        Guid yeuCauNapTienId,
        CancellationToken cancellationToken = default)
    {
        var yeuCau = await _db.YeuCauNapTiens
            .FirstOrDefaultAsync(n => n.Id == yeuCauNapTienId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy yêu cầu nạp tiền.", 404);

        if (yeuCau.NguoiDungThamGiaId != hocSinhId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền hủy yêu cầu nạp tiền này.", 403);
        }

        if (yeuCau.TrangThai == TrangThaiNapTien.THANH_CONG)
        {
            throw new LoiUngDungException("NAP_TIEN_DA_HOAN_TAT", "Giao dịch nạp tiền đã hoàn tất, không thể hủy.", 409);
        }

        if (yeuCau.TrangThai == TrangThaiNapTien.CHO_THANH_TOAN)
        {
            if (_payOsClient is { DaCauHinh: true })
            {
                await _payOsClient.HuyDonAsync(yeuCau.MaDonPayOs, "Người dùng hủy nạp tiền", cancellationToken);
            }

            yeuCau.TrangThai = TrangThaiNapTien.DA_HUY;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return AnhXaYeuCauNapTien(yeuCau);
    }

    public async Task<bool> XuLyWebhookPayOsAsync(
        string rawJsonBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawJsonBody))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(rawJsonBody);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var orderCode = dataEl.TryGetProperty("orderCode", out var ocEl) && ocEl.TryGetInt64(out var ocVal)
            ? ocVal
            : 0L;

        // PayOS gửi gói test xác nhận Webhook URL khi lưu cấu hình (orderCode = 123)
        if (orderCode == 123L)
        {
            return true;
        }

        if (_payOsClient is null || !_payOsClient.DaCauHinh)
        {
            throw new LoiUngDungException("CHUA_CAU_HINH_PAYOS", "Chưa cấu hình khóa PayOS.", 503);
        }

        var signature = root.TryGetProperty("signature", out var sigEl) ? (sigEl.GetString() ?? string.Empty) : string.Empty;
        if (!_payOsClient.XacThucChuKyWebhook(dataEl, signature))
        {
            throw new LoiUngDungException("CHU_KY_WEBHOOK_KHONG_HOP_LE", "Chữ ký HMAC-SHA256 từ PayOS không hợp lệ.", 400);
        }

        var topCode = root.TryGetProperty("code", out var tcEl) ? tcEl.GetString() : null;
        var dataCode = dataEl.TryGetProperty("code", out var dcEl) ? dcEl.GetString() : null;
        var thanhCong = string.Equals(topCode, "00", StringComparison.Ordinal) &&
                        (string.IsNullOrEmpty(dataCode) || string.Equals(dataCode, "00", StringComparison.Ordinal));
        if (!thanhCong || orderCode <= 0)
        {
            return true;
        }

        var yeuCau = await _db.YeuCauNapTiens
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.MaDonPayOs == orderCode, cancellationToken);
        if (yeuCau is null)
        {
            return true;
        }

        var amount = dataEl.TryGetProperty("amount", out var amEl) && amEl.TryGetInt32(out var amVal)
            ? amVal
            : 0;
        if (amount < yeuCau.SoTien)
        {
            return true;
        }

        var reference = dataEl.TryGetProperty("reference", out var refEl) ? refEl.GetString() : null;
        await HoanTatNapTienTrongGiaoDichAsync(yeuCau.Id, reference, cancellationToken);
        return true;
    }

    public async Task<YeuCauNapTienDto> HoanTatNapTienTrongGiaoDichAsync(
        Guid yeuCauNapTienId,
        string? maThamChieuNganHang,
        CancellationToken cancellationToken = default)
    {
        var thongTin = await _db.YeuCauNapTiens
            .AsNoTracking()
            .Where(n => n.Id == yeuCauNapTienId)
            .Select(n => new { n.Id, n.NguoiDungThamGiaId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy yêu cầu nạp tiền.", 404);

        var viId = await _db.TaiKhoanVis
            .AsNoTracking()
            .Where(v => v.NguoiDungThamGiaId == thongTin.NguoiDungThamGiaId)
            .Select(v => v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (viId == Guid.Empty)
        {
            throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy ví học tập.", 404);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(
            _db,
            [$"nap:{thongTin.Id:N}", $"vi:{viId:N}"],
            cancellationToken);

        var yeuCau = await _db.YeuCauNapTiens.FirstAsync(n => n.Id == thongTin.Id, cancellationToken);
        var vi = await _db.TaiKhoanVis.FirstAsync(v => v.Id == viId, cancellationToken);

        if (yeuCau.TrangThai == TrangThaiNapTien.THANH_CONG)
        {
            await tx.CommitAsync(cancellationToken);
            return AnhXaYeuCauNapTien(yeuCau, vi.SoDu);
        }

        var now = DateTime.UtcNow;
        vi.SoDu += yeuCau.SoTien;

        var soThuTu = await LaySoThuTuTiepTheoAsync(vi.Id, cancellationToken);
        var refText = !string.IsNullOrWhiteSpace(maThamChieuNganHang)
            ? $" · Mã NH: {maThamChieuNganHang.Trim()}"
            : string.Empty;

        var gd = new GiaoDichVi
        {
            TaiKhoanViId = vi.Id,
            SoThuTu = soThuTu,
            Loai = LoaiGiaoDichVi.NAP_TIEN,
            SoTien = yeuCau.SoTien,
            SoDuSau = vi.SoDu + vi.SoDuDangXuLyRut,
            GhiChu = $"Nạp tiền VietQR PayOS ({yeuCau.NoiDungChuyenKhoan}){refText}",
            ThanhToanId = null,
            YeuCauRutTienId = null,
            TaoLuc = now
        };
        _db.GiaoDichVis.Add(gd);
        await _db.SaveChangesAsync(cancellationToken);

        yeuCau.TrangThai = TrangThaiNapTien.THANH_CONG;
        yeuCau.MaThamChieuNganHang = string.IsNullOrWhiteSpace(maThamChieuNganHang) ? null : maThamChieuNganHang.Trim();
        yeuCau.GiaoDichViId = gd.Id;
        yeuCau.HoanThanhLuc = now;

        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = thongTin.NguoiDungThamGiaId,
            Loai = LoaiThongBao.THANH_TOAN,
            TieuDe = "Nạp tiền vào Ví học tập thành công",
            NoiDung = $"Bạn vừa nạp thành công {yeuCau.SoTien:N0} ₫ qua VietQR PayOS. Số dư Ví hiện tại: {vi.SoDu:N0} ₫.",
            TaoLuc = now
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return AnhXaYeuCauNapTien(yeuCau, vi.SoDu);
    }

    private YeuCauNapTienDto AnhXaYeuCauNapTien(YeuCauNapTien n, int? soDuMoi = null)
    {
        return new YeuCauNapTienDto(
            Id: n.Id,
            MaDonPayOs: n.MaDonPayOs,
            SoTien: n.SoTien,
            CongThanhToan: n.CongThanhToan,
            TrangThai: n.TrangThai,
            CheckoutUrl: n.CheckoutUrl,
            QrCode: n.QrCode,
            MaBinNganHang: n.MaBinNganHang,
            TenNganHang: _payOsClient?.LayTenNganHangTheoBin(n.MaBinNganHang) ?? "Ngân hàng VietQR",
            SoTaiKhoan: n.SoTaiKhoan,
            ChuTaiKhoan: n.ChuTaiKhoan,
            NoiDungChuyenKhoan: n.NoiDungChuyenKhoan,
            MaThamChieuNganHang: n.MaThamChieuNganHang,
            TaoLuc: n.TaoLuc,
            HetHanLuc: n.HetHanLuc,
            HoanThanhLuc: n.HoanThanhLuc,
            SoDuMoi: soDuMoi);
    }
}
