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
    public const int SoLanSaiMatKhauToiDa = 5;
    public const int CuaSoKhoaMatKhauPhut = 15;

    private static readonly string[] DinhDangBangChungHopLe = [".jpg", ".jpeg", ".png", ".webp", ".pdf"];
    private const long KichThuocBangChungToiDa = 5 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly BookingPolicyOptions _policy;
    private readonly IKiemDuyetNoiDungService _kiemDuyet;
    private readonly ILuuTruTepService _luuTruTep;

    public ViVaTaiChinhService(
        AppDbContext db,
        BookingPolicyOptions policy,
        IKiemDuyetNoiDungService kiemDuyet,
        ILuuTruTepService luuTruTep)
    {
        _db = db;
        _policy = policy;
        _kiemDuyet = kiemDuyet;
        _luuTruTep = luuTruTep;
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
            .Include(g => g.YeuCauRutTien)
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
            .Include(g => g.YeuCauRutTien)
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
            ds.Select(AnhXaYeuCauRutTien).ToList(),
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
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new KetQuaTaoYeuCauRutTienDto(
            YeuCau: AnhXaYeuCauRutTien(lenhRut),
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
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsThanhToan)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsNhatKyPhongHoc)
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
            ds.Select(AnhXaYeuCauRutTien).ToList(),
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
            _db.GiaoDichVis.Add(new GiaoDichVi
            {
                TaiKhoanViId = vi.Id,
                SoThuTu = soThuTu,
                Loai = LoaiGiaoDichVi.RUT_TIEN,
                SoTien = -lenh.SoTien,
                SoDuSau = vi.SoDu + vi.SoDuDangXuLyRut,
                YeuCauRutTienId = lenh.Id,
                GhiChu = $"Chi trả rút tiền về {lenh.TenNganHang} {CheSoTaiKhoan(lenh.SoTaiKhoan)}",
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
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return AnhXaYeuCauRutTien(lenh);
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
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsThanhToan)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsNhatKyPhongHoc)
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
            NoiDung = $"{ketQuaMoTa} Kết luận: {ghiChuSach}",
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
                NoiDung = $"{ketQuaMoTa} Kết luận: {ghiChuSach}",
                TaoLuc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

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
            TaoLuc: gd.TaoLuc);
    }

    private static YeuCauRutTienDto AnhXaYeuCauRutTien(YeuCauRutTien r) => new(
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
        XuLyLuc: r.XuLyLuc);

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
                : null);
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
}
