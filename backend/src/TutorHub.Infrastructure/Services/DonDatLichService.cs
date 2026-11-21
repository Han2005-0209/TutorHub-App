using Microsoft.EntityFrameworkCore;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Domain.Services;
using TutorHub.Infrastructure.Persistence;

namespace TutorHub.Infrastructure.Services;

public sealed class DonDatLichService : IDonDatLichService
{
    public const int ThoiLuongBuoiDauPhut = 60;
    private static readonly int[] ThoiLuongThuongHopLe = [60, 90, 120, 180];

    public static (int GiaSnapshot, int TienGiam) TinhGiaBuoiHocDauTien(HinhThucBuoiHoc hinhThuc)
    {
        if (hinhThuc != HinhThucBuoiHoc.TRUC_TUYEN)
        {
            throw new LoiUngDungException(
                "HOC_THU_CHI_ONLINE",
                "Buổi học thử miễn phí chỉ áp dụng cho hình thức học trực tuyến (Online).",
                422);
        }

        // Buổi học thử Online luôn miễn phí 100% (GiaMoiGio = 0, TienGiam = 0, cố định 60 phút)
        return (0, 0);
    }

    private readonly AppDbContext _db;
    private readonly IGiaSuVaMonHocService _giaSuService;
    private readonly IKiemDuyetNoiDungService _kiemDuyet;
    private readonly BookingPolicyOptions _policy;
    private readonly IViVaTaiChinhService _viVaTaiChinh;

    public DonDatLichService(
        AppDbContext db,
        IGiaSuVaMonHocService giaSuService,
        IKiemDuyetNoiDungService kiemDuyet,
        BookingPolicyOptions policy,
        IViVaTaiChinhService viVaTaiChinh)
    {
        _db = db;
        _giaSuService = giaSuService;
        _kiemDuyet = kiemDuyet;
        _policy = policy;
        _viVaTaiChinh = viVaTaiChinh;
    }

    public async Task<DonDatLichChiTietDto> TaoDonDatLichAsync(
        Guid hocSinhId,
        TaoDonDatLichYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var hocSinh = await _db.HocSinhs
            .FirstOrDefaultAsync(h => h.Id == hocSinhId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy tài khoản học sinh.", 404);

        if (hocSinh.TrangThai != TrangThaiNguoiDung.HOAT_DONG)
        {
            throw new LoiUngDungException("TAI_KHOAN_BI_KHOA", "Tài khoản của bạn hiện không thể đặt lịch.", 403);
        }

        if (hocSinh.XacThucEmailLuc is null)
        {
            throw new LoiUngDungException("EMAIL_CHUA_XAC_THUC", "Vui lòng xác thực email trước khi đặt lịch học.", 403);
        }

        var giaSuMonHoc = await _db.GiaSuMonHocs
            .Include(m => m.GiaSu)
            .Include(m => m.MonHoc)
            .FirstOrDefaultAsync(m => m.Id == yeuCau.GiaSuMonHocId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy môn dạy của gia sư.", 404);

        var giaSu = giaSuMonHoc.GiaSu
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);

        if (giaSu.Id == hocSinhId)
        {
            throw new LoiUngDungException("KHONG_DUOC_TU_THAO_TAC", "Bạn không thể tự đặt lịch học với chính mình.", 422);
        }

        if (giaSu.TrangThaiHoSo != TrangThaiHoSoGiaSu.DA_DUYET || giaSu.TrangThai != TrangThaiNguoiDung.HOAT_DONG)
        {
            throw new LoiUngDungException("GIA_SU_KHONG_KHA_DUNG", "Gia sư này hiện chưa sẵn sàng nhận lớp công khai.", 422);
        }

        // Kiểm tra hình thức học gia sư có nhận hay không
        if (yeuCau.HinhThuc == HinhThucBuoiHoc.TRUC_TUYEN && !giaSu.DayTrucTuyen)
        {
            throw new LoiUngDungException("HINH_THUC_KHONG_HO_TRO", "Gia sư hiện không mở hình thức học trực tuyến.", 422);
        }

        if (yeuCau.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP && !giaSu.DayTrucTiep)
        {
            throw new LoiUngDungException("HINH_THUC_KHONG_HO_TRO", "Gia sư hiện không nhận dạy trực tiếp tại nhà.", 422);
        }

        string? diaChiSach = null;
        if (yeuCau.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP)
        {
            diaChiSach = yeuCau.DiaChiHocSinh?.Trim();
            if (string.IsNullOrWhiteSpace(diaChiSach) || diaChiSach.Length < 5)
            {
                throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng nhập địa chỉ học trực tiếp (tối thiểu 5 ký tự).", 400);
            }
            _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("diaChiHocSinh", diaChiSach, false)]);
        }

        // BR-BKG-03: Kiểm tra lead time (>= 2h), horizon (<= 60 ngày) và nằm gọn trong 1 ngày GMT+7
        var nowUtc = DateTime.UtcNow;
        var batDauUtc = yeuCau.BatDauLuc.Kind == DateTimeKind.Utc
            ? yeuCau.BatDauLuc
            : yeuCau.BatDauLuc.ToUniversalTime();
        var ketThucUtc = batDauUtc.AddMinutes(yeuCau.ThoiLuongPhut);

        if (batDauUtc < nowUtc.AddHours(2))
        {
            throw new LoiUngDungException("DAT_LICH_QUA_SAT", "Vui lòng đặt lịch trước giờ học ít nhất 2 giờ.", 422);
        }

        if (batDauUtc > nowUtc.AddDays(60))
        {
            throw new LoiUngDungException("DAT_LICH_QUA_XA", "Chỉ hỗ trợ đặt lịch trước tối đa 60 ngày.", 422);
        }

        var batDauVn = batDauUtc.AddHours(7);
        var ketThucVn = ketThucUtc.AddHours(7);
        var ngayVn = DateOnly.FromDateTime(batDauVn);
        if (DateOnly.FromDateTime(ketThucVn) != ngayVn && !(ketThucVn.Hour == 0 && ketThucVn.Minute == 0 && DateOnly.FromDateTime(ketThucVn) == ngayVn.AddDays(1)))
        {
            throw new LoiUngDungException("KHUNG_GIO_KHONG_RANH", "Buổi học phải nằm trọn trong cùng một ngày.", 422);
        }

        // Cách 1 (Concurrency Locking): Khóa nguyên tử đồng thời theo gia sư và học sinh (thứ tự khóa tăng dần chống deadlock)
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(
            _db,
            [$"gia-su:{giaSu.Id:N}", $"hoc-sinh:{hocSinhId:N}"],
            cancellationToken);

        // BR-BKG-05: Ưu đãi buổi học thử chỉ áp dụng cho hình thức Online (miễn phí 0 ₫, 60 phút, lần đầu học với gia sư này)
        var daTungDatVoiGiaSu = await _db.DonDatLichs
            .AsNoTracking()
            .AnyAsync(d =>
                d.HocSinhId == hocSinhId &&
                d.GiaSuMonHoc != null &&
                d.GiaSuMonHoc.GiaSuId == giaSu.Id &&
                d.TrangThai != TrangThaiDonDatLich.DA_HUY &&
                d.TrangThai != TrangThaiDonDatLich.BI_TU_CHOI &&
                d.TrangThai != TrangThaiDonDatLich.HET_HAN,
                cancellationToken);

        if (yeuCau.LaBuoiHocThu)
        {
            if (yeuCau.HinhThuc != HinhThucBuoiHoc.TRUC_TUYEN)
            {
                throw new LoiUngDungException(
                    "HOC_THU_CHI_ONLINE",
                    "Buổi học thử miễn phí chỉ áp dụng cho hình thức học trực tuyến (Online).",
                    422);
            }

            if (!giaSu.CoHocThu)
            {
                throw new LoiUngDungException("KHONG_CO_HOC_THU", "Gia sư này hiện không mở ưu đãi buổi học thử.", 422);
            }

            if (daTungDatVoiGiaSu)
            {
                throw new LoiUngDungException("DA_DUNG_HOC_THU", "Ưu đãi buổi học thử chỉ áp dụng cho lần đầu tiên đặt lịch với gia sư này.", 409);
            }
        }

        var laBuoiHocThu = yeuCau.HinhThuc == HinhThucBuoiHoc.TRUC_TUYEN && giaSu.CoHocThu && !daTungDatVoiGiaSu;

        // BR-BKG-02: Kiểm tra thời lượng hợp lệ (Buổi đầu tiên cố định 60 phút; buổi thường 60/90/120/180 phút)
        if (laBuoiHocThu)
        {
            if (yeuCau.ThoiLuongPhut != ThoiLuongBuoiDauPhut)
            {
                throw new LoiUngDungException(
                    "THOI_LUONG_KHONG_HOP_LE",
                    $"Buổi học đầu tiên (học thử) có thời lượng cố định {ThoiLuongBuoiDauPhut} phút.",
                    422);
            }
        }
        else if (!ThoiLuongThuongHopLe.Contains(yeuCau.ThoiLuongPhut))
        {
            throw new LoiUngDungException("THOI_LUONG_KHONG_HOP_LE", "Thời lượng buổi học phải là 60, 90, 120 hoặc 180 phút.", 422);
        }

        // BR-BKG-06: Khớp khung giờ rảnh khả dụng của gia sư
        var dsTrong = await _giaSuService.LayKhungGioTrongAsync(giaSu.Id, ngayVn, ngayVn, cancellationToken);
        var bdPhut = batDauVn.Hour * 60 + batDauVn.Minute;
        var ktPhut = DateOnly.FromDateTime(ketThucVn) > ngayVn ? 1440 : ketThucVn.Hour * 60 + ketThucVn.Minute;

        var khopKhungRanh = dsTrong.Any(slot =>
        {
            if (!TimeOnly.TryParse(slot.GioBatDau, out var sBd) || !TimeOnly.TryParse(slot.GioKetThuc, out var sKt))
                return false;
            var sBdPhut = sBd.Hour * 60 + sBd.Minute;
            var sKtPhut = sKt.Hour * 60 + sKt.Minute;
            return bdPhut >= sBdPhut && ktPhut <= sKtPhut;
        });

        if (!khopKhungRanh)
        {
            // Phân biệt trường hợp khung giờ của gia sư vừa được học viên KHÁC đặt trước (409)
            // với trường hợp nằm ngoài lịch rảnh hoặc chính học viên tự đặt trùng ca gia sư này (422)
            var biHocVienKhacDatTruoc = await _db.DonDatLichs
                .AsNoTracking()
                .AnyAsync(d =>
                    d.HocSinhId != hocSinhId &&
                    d.GiaSuMonHoc != null &&
                    d.GiaSuMonHoc.GiaSuId == giaSu.Id &&
                    (d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN ||
                     d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN ||
                     d.TrangThai == TrangThaiDonDatLich.TRANH_CHAP) &&
                    d.BatDauLuc < ketThucUtc &&
                    d.KetThucLuc > batDauUtc,
                    cancellationToken);

            if (biHocVienKhacDatTruoc)
            {
                throw new LoiUngDungException(
                    "KHUNG_GIO_VUA_CO_NGUOI_DAT",
                    "Khung giờ này của gia sư vừa được học viên khác đặt. Vui lòng chọn khung giờ trống khác.",
                    409);
            }

            throw new LoiUngDungException("KHUNG_GIO_KHONG_RANH", "Khung giờ bạn chọn nằm ngoài lịch trống khả dụng của gia sư.", 422);
        }

        // BR-BKG-07 (tách rõ lỗi): 1) Chống 1 học sinh đặt trùng giờ với 2 gia sư khác nhau
        var donTrungHocSinh = await _db.DonDatLichs
            .AsNoTracking()
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .FirstOrDefaultAsync(d =>
                d.HocSinhId == hocSinhId &&
                (d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN ||
                 d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN ||
                 d.TrangThai == TrangThaiDonDatLich.TRANH_CHAP) &&
                d.BatDauLuc < ketThucUtc &&
                d.KetThucLuc > batDauUtc,
                cancellationToken);

        if (donTrungHocSinh is not null)
        {
            var bdTrungVn = donTrungHocSinh.BatDauLuc.AddHours(7);
            var ktTrungVn = donTrungHocSinh.KetThucLuc.AddHours(7);
            var tenGsTrung = donTrungHocSinh.GiaSuMonHoc?.GiaSu?.HoTen ?? "gia sư khác";
            throw new LoiUngDungException(
                "HOC_SINH_TRUNG_LICH",
                $"Bạn đã có lịch học {donTrungHocSinh.MaDon} với {tenGsTrung} từ {bdTrungVn:HH:mm}–{ktTrungVn:HH:mm} ngày {bdTrungVn:dd/MM/yyyy}. Vui lòng chọn khung giờ khác.",
                409);
        }

        // 2) Kiểm tra phòng thủ gia sư trùng lịch dưới khóa giao dịch
        var trungLichGiaSu = await _db.DonDatLichs
            .AsNoTracking()
            .AnyAsync(d =>
                d.GiaSuMonHoc != null &&
                d.GiaSuMonHoc.GiaSuId == giaSu.Id &&
                (d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN ||
                 d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN ||
                 d.TrangThai == TrangThaiDonDatLich.TRANH_CHAP) &&
                d.BatDauLuc < ketThucUtc &&
                d.KetThucLuc > batDauUtc,
                cancellationToken);

        if (trungLichGiaSu)
        {
            throw new LoiUngDungException(
                "KHUNG_GIO_VUA_CO_NGUOI_DAT",
                "Khung giờ này của gia sư vừa được học viên khác đặt. Vui lòng chọn khung giờ trống khác.",
                409);
        }

        // Tính snapshot giá & ưu đãi buổi đầu / mã giảm giá (BR-FIN-03, BR-FIN-04)
        int giaSnapshot;
        int tienGiam;
        Guid? maGiamGiaId = null;

        if (laBuoiHocThu)
        {
            (giaSnapshot, tienGiam) = TinhGiaBuoiHocDauTien(yeuCau.HinhThuc);
        }
        else
        {
            giaSnapshot = giaSuMonHoc.GiaMoiGio;
            tienGiam = 0;
            var tienGoc = Math.Max(0, (int)Math.Round(giaSnapshot * (yeuCau.ThoiLuongPhut / 60.0), MidpointRounding.AwayFromZero));

            var maNhap = yeuCau.MaGiamGia?.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(maNhap) && tienGoc > 0)
            {
                var mgg = await _db.MaGiamGias
                    .Include(x => x.DsDonDatLich)
                    .FirstOrDefaultAsync(x => x.Ma == maNhap, cancellationToken);

                if (mgg is null ||
                    !mgg.DangHoatDong ||
                    nowUtc < mgg.BatDauLuc ||
                    nowUtc > mgg.HetHanLuc ||
                    mgg.SoLanSuDung >= mgg.SoLuotToiDa ||
                    tienGoc < mgg.GiaTriDonToiThieu)
                {
                    throw new LoiUngDungException("MA_GIAM_GIA_SAI", "Mã giảm giá không hợp lệ, đã hết hạn hoặc không đủ điều kiện áp dụng.", 422);
                }

                tienGiam = mgg.Loai == LoaiMaGiamGia.PHAN_TRAM
                    ? Math.Min(tienGoc, (int)Math.Round(tienGoc * (mgg.GiaTri / 100.0), MidpointRounding.AwayFromZero))
                    : Math.Min(tienGoc, Math.Max(0, mgg.GiaTri));
                maGiamGiaId = mgg.Id;
            }
        }

        // BR-BKG-04: hanPhanHoi = min(now + 24h, batDauLuc)
        var han24h = nowUtc.AddHours(24);
        var hanPhanHoi = han24h < batDauUtc ? han24h : batDauUtc;

        var maDon = $"BK-{batDauVn:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        while (await _db.DonDatLichs.AnyAsync(d => d.MaDon == maDon, cancellationToken))
        {
            maDon = $"BK-{batDauVn:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        }

        var don = new DonDatLich
        {
            MaDon = maDon,
            HocSinhId = hocSinhId,
            GiaSuMonHocId = giaSuMonHoc.Id,
            MaGiamGiaId = maGiamGiaId,
            HinhThuc = yeuCau.HinhThuc,
            DiaChiHocSinh = diaChiSach,
            BatDauLuc = batDauUtc,
            KetThucLuc = ketThucUtc,
            GiaMoiGio = giaSnapshot,
            TienGiam = tienGiam,
            TrangThai = TrangThaiDonDatLich.CHO_XAC_NHAN,
            LaBuoiHocThu = laBuoiHocThu,
            HanPhanHoi = hanPhanHoi,
            TaoLuc = nowUtc
        };

        var moTaBuoi = laBuoiHocThu
            ? "buổi học thử Online miễn phí (60 phút)"
            : "buổi học";

        _db.DonDatLichs.Add(don);
        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = giaSu.Id,
            Loai = LoaiThongBao.DAT_LICH_MOI,
            TieuDe = $"Yêu cầu đặt lịch mới ({maDon})",
            NoiDung = $"{hocSinh.HoTen} vừa đặt {moTaBuoi} môn {giaSuMonHoc.MonHoc?.Ten ?? "học"} lúc {batDauVn:HH:mm dd/MM/yyyy}.",
            TaoLuc = nowUtc
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        don.HocSinh = hocSinh;
        don.GiaSuMonHoc = giaSuMonHoc;
        return AnhXaChiTiet(don);
    }

    public async Task<IReadOnlyList<DonDatLichChiTietDto>> LayDsDonCuaToiAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        CancellationToken cancellationToken = default)
    {
        var truyVan = _db.DonDatLichs
            .AsNoTracking()
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsThanhToan)
            .AsQueryable();

        truyVan = vaiTro == VaiTro.GIA_SU
            ? truyVan.Where(d => d.GiaSuMonHoc != null && d.GiaSuMonHoc.GiaSuId == nguoiDungId)
            : truyVan.Where(d => d.HocSinhId == nguoiDungId);

        var ds = await truyVan
            .OrderByDescending(d => d.TaoLuc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return ds.Select(AnhXaChiTiet).ToList();
    }

    public async Task<DonDatLichChiTietDto> XacNhanDonAsync(
        Guid giaSuId,
        Guid donId,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(
            _db,
            [$"don:{donId:N}", $"gia-su:{giaSuId:N}"],
            cancellationToken);

        var don = await _db.DonDatLichs
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsThanhToan)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        if (don.GiaSuMonHoc?.GiaSuId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền xác nhận đơn đặt lịch này.", 403);
        }

        if (don.TrangThai != TrangThaiDonDatLich.CHO_XAC_NHAN)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Chỉ có thể xác nhận đơn đang chờ phản hồi.", 409);
        }

        // Quy tắc Prepaid Escrow: đơn có phí bắt buộc đã thanh toán tạm giữ (DANG_GIU) trước khi gia sư xác nhận
        if (don.TongTien > 0)
        {
            var daTamGiu = don.DsThanhToan.Any(t => t.TrangThai == TrangThaiThanhToan.DANG_GIU);
            if (!daTamGiu)
            {
                throw new LoiUngDungException(
                    "DON_CHUA_THANH_TOAN",
                    "Đơn đặt lịch có phí chỉ có thể xác nhận sau khi học viên hoàn tất thanh toán tạm giữ (Escrow).",
                    409);
            }
        }

        // Phòng thủ 1: Chặn nếu gia sư đã có đơn DA_XAC_NHAN hoặc TRANH_CHAP trùng khung giờ
        var daCoDonXacNhanTrung = await _db.DonDatLichs
            .AsNoTracking()
            .AnyAsync(d =>
                d.Id != don.Id &&
                d.GiaSuMonHoc != null &&
                d.GiaSuMonHoc.GiaSuId == giaSuId &&
                (d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN ||
                 d.TrangThai == TrangThaiDonDatLich.TRANH_CHAP) &&
                d.BatDauLuc < don.KetThucLuc &&
                d.KetThucLuc > don.BatDauLuc,
                cancellationToken);

        if (daCoDonXacNhanTrung)
        {
            throw new LoiUngDungException(
                "GIA_SU_TRUNG_LICH",
                "Bạn đã có buổi học khác đã xác nhận hoặc đang tranh chấp trong khung giờ này.",
                409);
        }

        // Phòng thủ 2: Nếu dữ liệu cũ/bất thường có các đơn CHO_XAC_NHAN khác trùng giờ => tự động từ chối + hoàn 100% về ví học viên nếu đã DANG_GIU
        var dsDonChoTrung = await _db.DonDatLichs
            .Include(d => d.DsThanhToan)
            .Where(d =>
                d.Id != don.Id &&
                d.GiaSuMonHoc != null &&
                d.GiaSuMonHoc.GiaSuId == giaSuId &&
                d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN &&
                d.BatDauLuc < don.KetThucLuc &&
                d.KetThucLuc > don.BatDauLuc)
            .OrderBy(d => d.HocSinhId)
            .ToListAsync(cancellationToken);

        foreach (var donTrung in dsDonChoTrung)
        {
            donTrung.TrangThai = TrangThaiDonDatLich.BI_TU_CHOI;
            donTrung.LyDoHuy = "Khung giờ này đã được gia sư xác nhận cho một buổi học khác.";

            var ttTrung = donTrung.DsThanhToan.FirstOrDefault();
            if (ttTrung is { TrangThai: TrangThaiThanhToan.DANG_GIU })
            {
                await _viVaTaiChinh.HoanToanBoVeViHocSinhTrongGiaoDichAsync(
                    ttTrung,
                    donTrung.HocSinhId,
                    $"Hoàn tiền 100% do đơn {donTrung.MaDon} tự động từ chối vì gia sư đã xác nhận ca trùng giờ",
                    cancellationToken: cancellationToken);
            }
            else if (ttTrung is { TrangThai: TrangThaiThanhToan.CHO_THANH_TOAN })
            {
                ttTrung.TrangThai = TrangThaiThanhToan.THAT_BAI;
            }
        }

        don.TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN;
        if (don.HinhThuc == HinhThucBuoiHoc.TRUC_TUYEN && string.IsNullOrWhiteSpace(don.PhongHocUrl))
        {
            don.PhongHocUrl = $"/pages/phong-hoc.html?donId={don.Id}";
        }

        var batDauVn = don.BatDauLuc.AddHours(7);
        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = don.HocSinhId,
            Loai = LoaiThongBao.XAC_NHAN_LICH,
            TieuDe = $"Gia sư đã xác nhận lịch học ({don.MaDon})",
            NoiDung = $"{don.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư"} đã xác nhận buổi học lúc {batDauVn:HH:mm dd/MM/yyyy}.",
            TaoLuc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return AnhXaChiTiet(don);
    }

    public async Task<DonDatLichChiTietDto> TuChoiDonAsync(
        Guid giaSuId,
        Guid donId,
        string lyDo,
        CancellationToken cancellationToken = default)
    {
        var lyDoSach = lyDo?.Trim() ?? string.Empty;
        if (lyDoSach.Length < 3)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng nhập lý do từ chối (tối thiểu 3 ký tự).", 400);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"don:{donId:N}"], cancellationToken);

        var don = await _db.DonDatLichs
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsThanhToan)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        if (don.GiaSuMonHoc?.GiaSuId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền từ chối đơn đặt lịch này.", 403);
        }

        if (don.TrangThai != TrangThaiDonDatLich.CHO_XAC_NHAN)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Chỉ có thể từ chối đơn đang chờ xác nhận.", 409);
        }

        don.TrangThai = TrangThaiDonDatLich.BI_TU_CHOI;
        don.LyDoHuy = lyDoSach;

        var tt = don.DsThanhToan.FirstOrDefault();
        if (tt is { TrangThai: TrangThaiThanhToan.DANG_GIU })
        {
            await _viVaTaiChinh.HoanToanBoVeViHocSinhTrongGiaoDichAsync(
                tt,
                don.HocSinhId,
                $"Hoàn tiền 100% do gia sư từ chối đơn {don.MaDon}",
                cancellationToken: cancellationToken);
        }
        else if (tt is { TrangThai: TrangThaiThanhToan.CHO_THANH_TOAN })
        {
            tt.TrangThai = TrangThaiThanhToan.THAT_BAI;
        }

        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = don.HocSinhId,
            Loai = LoaiThongBao.TU_CHOI_LICH,
            TieuDe = $"Đơn đặt lịch {don.MaDon} chưa được nhận",
            NoiDung = $"Gia sư chưa thể nhận buổi học này. Lý do: {lyDoSach}",
            TaoLuc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return AnhXaChiTiet(don);
    }

    public async Task<DonDatLichChiTietDto> HuyDonAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        Guid donId,
        string lyDo,
        CancellationToken cancellationToken = default)
    {
        var lyDoSach = lyDo?.Trim() ?? string.Empty;
        if (lyDoSach.Length < 3)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng nhập lý do hủy lịch (tối thiểu 3 ký tự).", 400);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await KhoaUngDungSqlHelper.KhoaTaiNguyenTrongGiaoDichAsync(_db, [$"don:{donId:N}"], cancellationToken);

        var don = await _db.DonDatLichs
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsThanhToan)
            .FirstOrDefaultAsync(d => d.Id == donId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch.", 404);

        var laChuDon = (vaiTro == VaiTro.HOC_SINH && don.HocSinhId == nguoiDungId) ||
                       (vaiTro == VaiTro.GIA_SU && don.GiaSuMonHoc?.GiaSuId == nguoiDungId);
        if (!laChuDon)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền hủy đơn đặt lịch này.", 403);
        }

        if (don.TrangThai is not (TrangThaiDonDatLich.CHO_XAC_NHAN or TrangThaiDonDatLich.DA_XAC_NHAN) ||
            DateTime.UtcNow >= don.BatDauLuc)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Đơn này không thể hủy ở trạng thái hoặc thời điểm hiện tại.", 409);
        }

        don.TrangThai = TrangThaiDonDatLich.DA_HUY;
        don.LyDoHuy = lyDoSach;

        // Hoàn 100% về Ví Học viên nếu đơn đã thanh toán tạm giữ (Escrow)
        var tt = don.DsThanhToan.FirstOrDefault();
        if (tt is { TrangThai: TrangThaiThanhToan.DANG_GIU })
        {
            await _viVaTaiChinh.HoanToanBoVeViHocSinhTrongGiaoDichAsync(
                tt,
                don.HocSinhId,
                $"Hoàn tiền 100% do hủy đơn {don.MaDon} trước giờ học",
                cancellationToken: cancellationToken);
        }
        else if (tt is { TrangThai: TrangThaiThanhToan.CHO_THANH_TOAN })
        {
            tt.TrangThai = TrangThaiThanhToan.THAT_BAI;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return AnhXaChiTiet(don);
    }

    private DonDatLichChiTietDto AnhXaChiTiet(DonDatLich d)
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
