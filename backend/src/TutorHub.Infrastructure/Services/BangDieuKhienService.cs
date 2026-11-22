using Microsoft.EntityFrameworkCore;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Domain.Services;
using TutorHub.Infrastructure.Persistence;

namespace TutorHub.Infrastructure.Services;

public sealed class BangDieuKhienService : IBangDieuKhienService
{
    private readonly AppDbContext _db;
    private readonly IKiemDuyetNoiDungService _kiemDuyet;
    private readonly BookingPolicyOptions _policy;

    public BangDieuKhienService(
        AppDbContext db,
        IKiemDuyetNoiDungService kiemDuyet,
        BookingPolicyOptions policy)
    {
        _db = db;
        _kiemDuyet = kiemDuyet;
        _policy = policy;
    }

    public async Task<TongQuanHocSinhDto> LayTongQuanHocSinhAsync(
        Guid hocSinhId,
        CancellationToken cancellationToken = default)
    {
        var hocSinh = await _db.HocSinhs
            .AsNoTracking()
            .Include(h => h.TaiKhoanVi)
            .Include(h => h.DsMucYeuThich)
            .FirstOrDefaultAsync(h => h.Id == hocSinhId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ học sinh.", 404);

        var dsDon = await _db.DonDatLichs
            .AsNoTracking()
            .Where(d => d.HocSinhId == hocSinhId)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DsThanhToan)
            .OrderByDescending(d => d.BatDauLuc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var dsDonDto = dsDon.Select(d => AnhXaDonDatLichTomTat(d, choGiaSu: false, thuNhapTheoThanhToan: null)).ToList();

        var sapToi = dsDonDto
            .Where(d => d.TrangThai is TrangThaiDonDatLich.DA_XAC_NHAN or TrangThaiDonDatLich.CHO_XAC_NHAN)
            .OrderBy(d => d.BatDauLuc)
            .ToList();

        var soHoanThanh = await _db.DonDatLichs
            .AsNoTracking()
            .CountAsync(d => d.HocSinhId == hocSinhId && d.TrangThai == TrangThaiDonDatLich.HOAN_THANH, cancellationToken);

        return new TongQuanHocSinhDto(
            HocSinhId: hocSinh.Id,
            HoTen: hocSinh.HoTen,
            Email: hocSinh.Email,
            KhuVuc: hocSinh.KhuVuc,
            SoDuVi: hocSinh.TaiKhoanVi?.SoDu ?? 0,
            SoBuoiSapToi: sapToi.Count,
            SoBuoiHoanThanh: soHoanThanh,
            SoGiaSuYeuThich: hocSinh.DsMucYeuThich.Count,
            BuoiHocSapToiGanNhat: sapToi.FirstOrDefault(),
            DsDonDatLichGanDay: dsDonDto.Take(8).ToList());
    }

    public async Task<TongQuanGiaSuDto> LayTongQuanGiaSuAsync(
        Guid giaSuId,
        CancellationToken cancellationToken = default)
    {
        var giaSu = await _db.GiaSus
            .AsNoTracking()
            .Include(g => g.TaiKhoanVi)
            .Include(g => g.DsGiayTo)
            .Include(g => g.DsLichRanh)
            .Include(g => g.DsMonDay).ThenInclude(m => m.MonHoc)
            .Include(g => g.DsMonDay).ThenInclude(m => m.DsDonDatLich).ThenInclude(d => d.HocSinh)
            .Include(g => g.DsMonDay).ThenInclude(m => m.DsDonDatLich).ThenInclude(d => d.DanhGia)
            .Include(g => g.DsMonDay).ThenInclude(m => m.DsDonDatLich).ThenInclude(d => d.DsThanhToan)
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư.", 404);

        var tatCaDon = giaSu.DsDonDatLich
            .OrderByDescending(d => d.BatDauLuc)
            .ToList();

        // Ghép cặp ca liền kề trước đó trong cùng ngày để phân tích lộ trình di chuyển thích ứng
        var donTheoThoiGian = tatCaDon.OrderBy(d => d.BatDauLuc).ToList();
        var mapDonLienKe = new Dictionary<Guid, DonDatLich>();
        for (int i = 0; i < donTheoThoiGian.Count; i++)
        {
            var cur = donTheoThoiGian[i];
            if (i > 0)
            {
                var prev = donTheoThoiGian[i - 1];
                if (DateOnly.FromDateTime(prev.BatDauLuc.AddHours(7)) == DateOnly.FromDateTime(cur.BatDauLuc.AddHours(7)))
                {
                    mapDonLienKe[cur.Id] = prev;
                }
            }
        }

        var dsThanhToanIdDaGiaiNgan = tatCaDon
            .SelectMany(d => d.DsThanhToan)
            .Where(t => t.TrangThai == TrangThaiThanhToan.DA_GIAI_NGAN)
            .Select(t => t.Id)
            .Distinct()
            .ToList();

        var thuNhapTheoThanhToan = dsThanhToanIdDaGiaiNgan.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.GiaoDichVis
                .AsNoTracking()
                .Where(g => g.Loai == LoaiGiaoDichVi.THU_NHAP &&
                            g.ThanhToanId.HasValue &&
                            dsThanhToanIdDaGiaiNgan.Contains(g.ThanhToanId.Value))
                .ToDictionaryAsync(g => g.ThanhToanId!.Value, g => g.SoTien, cancellationToken);

        var dsDonDto = tatCaDon.Select(d =>
        {
            mapDonLienKe.TryGetValue(d.Id, out var donTruoc);
            return AnhXaDonDatLichTomTat(d, choGiaSu: true, thuNhapTheoThanhToan, donTruoc, giaSu);
        }).ToList();
        var soSapToi = tatCaDon.Count(d => d.TrangThai is TrangThaiDonDatLich.DA_XAC_NHAN or TrangThaiDonDatLich.CHO_XAC_NHAN);
        var tongThuNhap = dsDonDto
            .Where(d => d.TrangThai == TrangThaiDonDatLich.HOAN_THANH)
            .Sum(d => d.ThuNhapGiaSu);
        var soLichHangTuan = giaSu.DsLichRanh.OfType<LichLapHangTuan>().Count();

        return new TongQuanGiaSuDto(
            GiaSuId: giaSu.Id,
            HoTen: giaSu.HoTen,
            Email: giaSu.Email,
            KhuVuc: giaSu.KhuVuc,
            TrangThaiHoSo: giaSu.TrangThaiHoSo,
            DaXacMinh: giaSu.DaXacMinh,
            DayTrucTuyen: giaSu.DayTrucTuyen,
            DayTrucTiep: giaSu.DayTrucTiep,
            BanKinhPhucVuKm: giaSu.DayTrucTiep ? giaSu.BanKinhPhucVuKm : 0,
            SoDuVi: giaSu.TaiKhoanVi?.SoDu ?? 0,
            TongThuNhapDaHoanThanh: tongThuNhap,
            SoBuoiSapToi: soSapToi,
            SoBuoiHoanThanh: giaSu.SoBuoiHoanThanh,
            DiemDanhGiaTrungBinh: giaSu.DiemDanhGiaTrungBinh,
            SoLuotDanhGia: giaSu.SoLuotDanhGia,
            SoMonDay: giaSu.DsMonDay.Count,
            SoKhungGioHangTuan: soLichHangTuan,
            SoGiayTo: giaSu.DsGiayTo.Count,
            DsLichDayGanDay: dsDonDto.Take(8).ToList());
    }

    public async Task<TongQuanQuanTriDto> LayTongQuanQuanTriAsync(CancellationToken cancellationToken = default)
    {
        var tongNguoiDung = await _db.NguoiDungs.AsNoTracking().CountAsync(cancellationToken);
        var tongHocSinh = await _db.HocSinhs.AsNoTracking().CountAsync(cancellationToken);
        var tongGiaSu = await _db.GiaSus.AsNoTracking().CountAsync(cancellationToken);
        var giaSuChoDuyet = await _db.GiaSus.AsNoTracking().CountAsync(g => g.TrangThaiHoSo == TrangThaiHoSoGiaSu.CHO_DUYET, cancellationToken);
        var giaSuDaDuyet = await _db.GiaSus.AsNoTracking().CountAsync(g => g.TrangThaiHoSo == TrangThaiHoSoGiaSu.DA_DUYET, cancellationToken);
        var giayToChoDuyet = await _db.GiayToGiaSus.AsNoTracking().CountAsync(g => g.TrangThai == TrangThaiGiayTo.CHO_DUYET, cancellationToken);
        var tongMonHoc = await _db.MonHocs.AsNoTracking().CountAsync(cancellationToken);
        var tongDonDatLich = await _db.DonDatLichs.AsNoTracking().CountAsync(cancellationToken);
        var donHoanThanh = await _db.DonDatLichs.AsNoTracking().CountAsync(d => d.TrangThai == TrangThaiDonDatLich.HOAN_THANH, cancellationToken);
        var khieuNaiDangMo = await _db.KhieuNais.AsNoTracking().CountAsync(
            k => k.TrangThai == TrangThaiKhieuNai.MO,
            cancellationToken);
        var yeuCauRutTienChoXuLy = await _db.YeuCauRutTiens.AsNoTracking().CountAsync(
            r => r.TrangThai == TrangThaiRutTien.CHO_XU_LY,
            cancellationToken);
        var tongGiaTriGiaoDich = await _db.ThanhToans.AsNoTracking()
            .Where(t => t.TrangThai == TrangThaiThanhToan.DANG_GIU || t.TrangThai == TrangThaiThanhToan.DA_GIAI_NGAN)
            .SumAsync(t => (int?)t.SoTien, cancellationToken) ?? 0;

        return new TongQuanQuanTriDto(
            TongNguoiDung: tongNguoiDung,
            TongHocSinh: tongHocSinh,
            TongGiaSu: tongGiaSu,
            GiaSuChoDuyet: giaSuChoDuyet,
            GiaSuDaDuyet: giaSuDaDuyet,
            GiayToChoDuyet: giayToChoDuyet,
            TongMonHoc: tongMonHoc,
            TongDonDatLich: tongDonDatLich,
            DonHoanThanh: donHoanThanh,
            KhieuNaiDangMo: khieuNaiDangMo,
            YeuCauRutTienChoXuLy: yeuCauRutTienChoXuLy,
            TongGiaTriGiaoDich: tongGiaTriGiaoDich);
    }

    public async Task<ThongKeChiTietQuanTriDto> LayThongKeChiTietQuanTriAsync(
        string? phamVi = "30_ngay",
        CancellationToken cancellationToken = default)
    {
        var phamViChuan = (phamVi ?? "30_ngay").Trim().ToLowerInvariant();
        if (phamViChuan is not ("7_ngay" or "30_ngay" or "6_thang" or "12_thang"))
        {
            phamViChuan = "30_ngay";
        }

        // Múi giờ Việt Nam (UTC+7) để gom nhóm ngày/tháng khớp thực tế kinh doanh
        var nowVn = DateTime.UtcNow.AddHours(7);
        var todayVn = nowVn.Date;
        var theoThang = phamViChuan is "6_thang" or "12_thang";
        var donViThoiGian = theoThang ? "THANG" : "NGAY";
        var nhanPhamVi = phamViChuan switch
        {
            "7_ngay" => "7 ngày qua",
            "30_ngay" => "30 ngày qua",
            "6_thang" => "6 tháng qua",
            "12_thang" => "12 tháng qua",
            _ => "30 ngày qua"
        };

        DateTime startVn;
        DateTime endExclusiveVn;
        DateTime prevStartVn;
        var danhSachKhoa = new List<(string Khoa, string Nhan)>();

        if (!theoThang)
        {
            var soNgay = phamViChuan == "7_ngay" ? 7 : 30;
            startVn = todayVn.AddDays(-(soNgay - 1));
            endExclusiveVn = todayVn.AddDays(1);
            prevStartVn = startVn.AddDays(-soNgay);

            for (int i = 0; i < soNgay; i++)
            {
                var d = startVn.AddDays(i);
                danhSachKhoa.Add((d.ToString("yyyy-MM-dd"), d.ToString("dd/MM")));
            }
        }
        else
        {
            var soThang = phamViChuan == "6_thang" ? 6 : 12;
            var thangHienTai = new DateTime(todayVn.Year, todayVn.Month, 1);
            startVn = thangHienTai.AddMonths(-(soThang - 1));
            endExclusiveVn = thangHienTai.AddMonths(1);
            prevStartVn = startVn.AddMonths(-soThang);

            for (int i = 0; i < soThang; i++)
            {
                var m = startVn.AddMonths(i);
                danhSachKhoa.Add((m.ToString("yyyy-MM"), $"T{m.Month:00}/{m.Year}"));
            }
        }

        var startUtc = DateTime.SpecifyKind(startVn.AddHours(-7), DateTimeKind.Utc);
        var endExclusiveUtc = DateTime.SpecifyKind(endExclusiveVn.AddHours(-7), DateTimeKind.Utc);
        var prevStartUtc = DateTime.SpecifyKind(prevStartVn.AddHours(-7), DateTimeKind.Utc);

        string LayKhoaThoiGian(DateTime utc)
        {
            var vn = utc.AddHours(7);
            return theoThang ? vn.ToString("yyyy-MM") : vn.ToString("yyyy-MM-dd");
        }

        var buckets = danhSachKhoa.ToDictionary(
            x => x.Khoa,
            x => new BoDemThoiGianNoiBo(x.Nhan, x.Khoa));

        // 1. Truy vấn các thanh toán trong 2 kỳ liên tiếp (kỳ hiện tại + kỳ liền trước để so sánh tăng trưởng)
        var dsThanhToan = await _db.ThanhToans
            .AsNoTracking()
            .Where(t =>
                t.TrangThai != TrangThaiThanhToan.THAT_BAI &&
                (t.ThanhToanLuc ?? t.TaoLuc) >= prevStartUtc &&
                (t.ThanhToanLuc ?? t.TaoLuc) < endExclusiveUtc)
            .Select(t => new
            {
                t.Id,
                t.DonDatLichId,
                t.SoTien,
                t.TienDaHoan,
                t.TrangThai,
                ThoiDiem = t.ThanhToanLuc ?? t.TaoLuc
            })
            .ToListAsync(cancellationToken);

        var dsThanhToanIdDaGiaiNgan = dsThanhToan
            .Where(t => t.TrangThai == TrangThaiThanhToan.DA_GIAI_NGAN)
            .Select(t => t.Id)
            .Distinct()
            .ToList();

        var thuNhapTheoThanhToan = dsThanhToanIdDaGiaiNgan.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.GiaoDichVis
                .AsNoTracking()
                .Where(g => g.Loai == LoaiGiaoDichVi.THU_NHAP &&
                            g.ThanhToanId.HasValue &&
                            dsThanhToanIdDaGiaiNgan.Contains(g.ThanhToanId.Value))
                .ToDictionaryAsync(g => g.ThanhToanId!.Value, g => g.SoTien, cancellationToken);

        (int Net, int Phi, int ThuNhap) TinhTaiChinhThanhToan(Guid id, int soTien, int tienDaHoan, TrangThaiThanhToan trangThai)
        {
            if (trangThai == TrangThaiThanhToan.CHO_THANH_TOAN)
            {
                return (0, 0, 0);
            }

            var net = Math.Max(0, soTien - tienDaHoan);
            if (net == 0)
            {
                return (0, 0, 0);
            }

            if (trangThai == TrangThaiThanhToan.DA_GIAI_NGAN &&
                thuNhapTheoThanhToan.TryGetValue(id, out var thucNhanLichSu))
            {
                var thuNhap = Math.Clamp(thucNhanLichSu, 0, net);
                return (net, Math.Max(0, net - thuNhap), thuNhap);
            }

            var (_, phi, thuNhapTinh) = BoTinhTaiChinhDon.TinhTaiChinhDon(net, _policy.DefaultPlatformFeePercent);
            return (net, phi, thuNhapTinh);
        }

        int tongDoanhThuKyNay = 0;
        int tongPhiKyNay = 0;
        int tongThuNhapKyNay = 0;
        int tongDoanhThuKyTruoc = 0;
        int tongPhiKyTruoc = 0;
        int soThanhToanCoGiaTriKyNay = 0;

        var doanhThuTheoDonIdKyNay = new Dictionary<Guid, (int DoanhThu, int ThuNhap)>();
        var trangThaiTtMap = new Dictionary<string, (string Nhan, int SoLuong, int GiaTri)>();

        void CongTrangThaiTt(string ma, string nhan, int giaTri)
        {
            if (giaTri <= 0) return;
            if (trangThaiTtMap.TryGetValue(ma, out var cur))
            {
                trangThaiTtMap[ma] = (nhan, cur.SoLuong + 1, cur.GiaTri + giaTri);
            }
            else
            {
                trangThaiTtMap[ma] = (nhan, 1, giaTri);
            }
        }

        foreach (var tt in dsThanhToan)
        {
            var (net, phi, thuNhap) = TinhTaiChinhThanhToan(tt.Id, tt.SoTien, tt.TienDaHoan, tt.TrangThai);
            if (tt.ThoiDiem >= startUtc)
            {
                // Phân bổ tình trạng xử lý học phí (giải ngân, tạm giữ, hoàn tiền, chờ thanh toán)
                if (tt.TrangThai == TrangThaiThanhToan.CHO_THANH_TOAN)
                {
                    CongTrangThaiTt("CHO_THANH_TOAN", "Chờ thanh toán", tt.SoTien);
                }
                else
                {
                    if (tt.TrangThai == TrangThaiThanhToan.DA_GIAI_NGAN && net > 0)
                    {
                        CongTrangThaiTt("DA_GIAI_NGAN", "Đã giải ngân cho gia sư", net);
                    }
                    else if (tt.TrangThai == TrangThaiThanhToan.DANG_GIU && net > 0)
                    {
                        CongTrangThaiTt("DANG_GIU", "Đang tạm giữ chờ buổi học", net);
                    }

                    if (tt.TienDaHoan > 0)
                    {
                        CongTrangThaiTt("DA_HOAN", "Đã hoàn về ví học viên", tt.TienDaHoan);
                    }
                }

                if (net > 0)
                {
                    tongDoanhThuKyNay += net;
                    tongPhiKyNay += phi;
                    tongThuNhapKyNay += thuNhap;
                    soThanhToanCoGiaTriKyNay++;

                    if (doanhThuTheoDonIdKyNay.TryGetValue(tt.DonDatLichId, out var curDon))
                    {
                        doanhThuTheoDonIdKyNay[tt.DonDatLichId] = (curDon.DoanhThu + net, curDon.ThuNhap + thuNhap);
                    }
                    else
                    {
                        doanhThuTheoDonIdKyNay[tt.DonDatLichId] = (net, thuNhap);
                    }

                    var khoa = LayKhoaThoiGian(tt.ThoiDiem);
                    if (buckets.TryGetValue(khoa, out var b))
                    {
                        b.TongDoanhThu += net;
                        b.PhiNenTang += phi;
                        b.ThuNhapGiaSu += thuNhap;
                    }
                }
            }
            else if (net > 0)
            {
                tongDoanhThuKyTruoc += net;
                tongPhiKyTruoc += phi;
            }
        }

        // 2. Dòng tiền ví (Nạp ví, Chi trả rút tiền, Hoàn tiền) trong kỳ
        var dsGiaoDichVi = await _db.GiaoDichVis
            .AsNoTracking()
            .Where(g =>
                g.TaoLuc >= startUtc &&
                g.TaoLuc < endExclusiveUtc &&
                (g.Loai == LoaiGiaoDichVi.NAP_TIEN ||
                 g.Loai == LoaiGiaoDichVi.RUT_TIEN ||
                 g.Loai == LoaiGiaoDichVi.HOAN_TIEN))
            .Select(g => new { g.Loai, g.SoTien, g.TaoLuc })
            .ToListAsync(cancellationToken);

        int tongNapViKyNay = 0;
        int tongRutTienKyNay = 0;
        int tongHoanTienKyNay = 0;

        foreach (var gd in dsGiaoDichVi)
        {
            var giaTri = Math.Abs(gd.SoTien);
            var khoa = LayKhoaThoiGian(gd.TaoLuc);
            buckets.TryGetValue(khoa, out var b);

            switch (gd.Loai)
            {
                case LoaiGiaoDichVi.NAP_TIEN:
                    tongNapViKyNay += giaTri;
                    if (b is not null) b.TongNapVi += giaTri;
                    break;
                case LoaiGiaoDichVi.RUT_TIEN:
                    tongRutTienKyNay += giaTri;
                    if (b is not null) b.TongRutTien += giaTri;
                    break;
                case LoaiGiaoDichVi.HOAN_TIEN:
                    tongHoanTienKyNay += giaTri;
                    if (b is not null) b.TongHoanTien += giaTri;
                    break;
            }
        }

        // 3. Đơn đặt lịch (biến động theo ngày/tháng, phân bổ trạng thái, môn học, hình thức, top gia sư)
        var dsDon = await _db.DonDatLichs
            .AsNoTracking()
            .Where(d => d.TaoLuc >= prevStartUtc && d.TaoLuc < endExclusiveUtc)
            .Select(d => new
            {
                d.Id,
                d.TaoLuc,
                d.BatDauLuc,
                d.KetThucLuc,
                d.GiaMoiGio,
                d.TienGiam,
                d.LaBuoiHocThu,
                d.TrangThai,
                d.HinhThuc,
                MonHocId = d.GiaSuMonHoc != null ? d.GiaSuMonHoc.MonHocId : Guid.Empty,
                TenMonHoc = d.GiaSuMonHoc != null && d.GiaSuMonHoc.MonHoc != null ? d.GiaSuMonHoc.MonHoc.Ten : "Môn học khác",
                GiaSuId = d.GiaSuMonHoc != null ? d.GiaSuMonHoc.GiaSuId : Guid.Empty,
                TenGiaSu = d.GiaSuMonHoc != null && d.GiaSuMonHoc.GiaSu != null ? d.GiaSuMonHoc.GiaSu.HoTen : "Gia sư",
                KhuVucGiaSu = d.GiaSuMonHoc != null && d.GiaSuMonHoc.GiaSu != null ? d.GiaSuMonHoc.GiaSu.KhuVuc : null
            })
            .ToListAsync(cancellationToken);

        var donKyNay = dsDon.Where(d => d.TaoLuc >= startUtc).ToList();
        var soDonKyNay = donKyNay.Count;
        var soDonKyTruoc = dsDon.Count - soDonKyNay;
        var soDonHoanThanhKyNay = donKyNay.Count(d => d.TrangThai == TrangThaiDonDatLich.HOAN_THANH);
        var tyLeHoanThanh = soDonKyNay == 0
            ? 0.0
            : Math.Round(soDonHoanThanhKyNay * 100.0 / soDonKyNay, 1);

        static int TinhTongTienDon(bool laBuoiHocThu, int giaMoiGio, int tienGiam, DateTime batDau, DateTime ketThuc)
        {
            var tienGoc = laBuoiHocThu
                ? Math.Max(0, giaMoiGio)
                : Math.Max(0, (int)Math.Round(giaMoiGio * Math.Max(0.0, (ketThuc - batDau).TotalMinutes) / 60.0, MidpointRounding.AwayFromZero));
            return Math.Max(0, tienGoc - Math.Max(0, tienGiam));
        }

        foreach (var d in donKyNay)
        {
            var khoaTao = LayKhoaThoiGian(d.TaoLuc);
            if (buckets.TryGetValue(khoaTao, out var bTao))
            {
                bTao.SoDonDatMoi++;
            }

            if (d.TrangThai == TrangThaiDonDatLich.HOAN_THANH)
            {
                var khoaHoanThanh = LayKhoaThoiGian(d.KetThucLuc <= endExclusiveUtc && d.KetThucLuc >= startUtc ? d.KetThucLuc : d.TaoLuc);
                if (buckets.TryGetValue(khoaHoanThanh, out var bHt))
                {
                    bHt.SoBuoiHoanThanh++;
                }
            }
        }

        // Phân bổ trạng thái đơn
        var thuTuTrangThai = new (TrangThaiDonDatLich Ma, string Nhan)[]
        {
            (TrangThaiDonDatLich.HOAN_THANH, "Hoàn thành"),
            (TrangThaiDonDatLich.DA_XAC_NHAN, "Đã xác nhận"),
            (TrangThaiDonDatLich.CHO_XAC_NHAN, "Chờ xác nhận"),
            (TrangThaiDonDatLich.DA_HUY, "Đã hủy"),
            (TrangThaiDonDatLich.TRANH_CHAP, "Tranh chấp"),
            (TrangThaiDonDatLich.BI_TU_CHOI, "Từ chối"),
            (TrangThaiDonDatLich.HET_HAN, "Hết hạn")
        };

        var phanBoTrangThaiDon = thuTuTrangThai
            .Select(tt =>
            {
                var nhom = donKyNay.Where(d => d.TrangThai == tt.Ma).ToList();
                var giaTri = nhom.Sum(d =>
                    doanhThuTheoDonIdKyNay.TryGetValue(d.Id, out var rev)
                        ? rev.DoanhThu
                        : TinhTongTienDon(d.LaBuoiHocThu, d.GiaMoiGio, d.TienGiam, d.BatDauLuc, d.KetThucLuc));
                var tyLe = soDonKyNay == 0 ? 0.0 : Math.Round(nhom.Count * 100.0 / soDonKyNay, 1);
                return new ThongKePhanBoMucDto(tt.Ma.ToString(), tt.Nhan, nhom.Count, giaTri, tyLe);
            })
            .Where(x => x.SoLuong > 0)
            .ToList();

        // Top 8 môn học theo doanh thu & số buổi
        var tongDoanhThuMon = 0;
        var monHocRaw = donKyNay
            .GroupBy(d => new { d.MonHocId, d.TenMonHoc })
            .Select(g =>
            {
                var doanhThu = g.Sum(d =>
                    doanhThuTheoDonIdKyNay.TryGetValue(d.Id, out var rev)
                        ? rev.DoanhThu
                        : (d.TrangThai is TrangThaiDonDatLich.HOAN_THANH or TrangThaiDonDatLich.DA_XAC_NHAN
                            ? TinhTongTienDon(d.LaBuoiHocThu, d.GiaMoiGio, d.TienGiam, d.BatDauLuc, d.KetThucLuc)
                            : 0));
                return new
                {
                    Ma = g.Key.MonHocId.ToString(),
                    Nhan = g.Key.TenMonHoc,
                    SoLuong = g.Count(),
                    GiaTri = doanhThu
                };
            })
            .OrderByDescending(x => x.GiaTri)
            .ThenByDescending(x => x.SoLuong)
            .Take(8)
            .ToList();

        tongDoanhThuMon = monHocRaw.Sum(x => x.GiaTri);
        var topMonHoc = monHocRaw
            .Select(x => new ThongKePhanBoMucDto(
                x.Ma,
                x.Nhan,
                x.SoLuong,
                x.GiaTri,
                tongDoanhThuMon == 0 ? 0.0 : Math.Round(x.GiaTri * 100.0 / tongDoanhThuMon, 1)))
            .ToList();

        // Phân bổ hình thức học (Online vs Tại nhà)
        var phanBoHinhThuc = new[]
        {
            (HinhThucBuoiHoc.TRUC_TUYEN, "Học trực tuyến (Online 1-1)"),
            (HinhThucBuoiHoc.TRUC_TIEP, "Học trực tiếp (Tại nhà)")
        }
        .Select(ht =>
        {
            var nhom = donKyNay.Where(d => d.HinhThuc == ht.Item1).ToList();
            var giaTri = nhom.Sum(d =>
                doanhThuTheoDonIdKyNay.TryGetValue(d.Id, out var rev)
                    ? rev.DoanhThu
                    : (d.TrangThai is TrangThaiDonDatLich.HOAN_THANH or TrangThaiDonDatLich.DA_XAC_NHAN
                        ? TinhTongTienDon(d.LaBuoiHocThu, d.GiaMoiGio, d.TienGiam, d.BatDauLuc, d.KetThucLuc)
                        : 0));
            var tyLe = soDonKyNay == 0 ? 0.0 : Math.Round(nhom.Count * 100.0 / soDonKyNay, 1);
            return new ThongKePhanBoMucDto(ht.Item1.ToString(), ht.Item2, nhom.Count, giaTri, tyLe);
        })
        .ToList();

        // Phân bổ tình trạng xử lý học phí (Đã giải ngân, Đang tạm giữ, Đã hoàn tiền, Chờ thanh toán)
        var thuTuTrangThaiTt = new[] { "DA_GIAI_NGAN", "DANG_GIU", "DA_HOAN", "CHO_THANH_TOAN" };
        var tongGiaTriTt = trangThaiTtMap.Values.Sum(v => v.GiaTri);
        var phanBoTrangThaiTt = thuTuTrangThaiTt
            .Where(ma => trangThaiTtMap.ContainsKey(ma))
            .Select(ma =>
            {
                var v = trangThaiTtMap[ma];
                return new ThongKePhanBoMucDto(
                    ma,
                    v.Nhan,
                    v.SoLuong,
                    v.GiaTri,
                    tongGiaTriTt == 0 ? 0.0 : Math.Round(v.GiaTri * 100.0 / tongGiaTriTt, 1));
            })
            .ToList();

        // 4. Tăng trưởng người dùng mới (Học sinh & Gia sư)
        var dsHocSinhMoi = await _db.HocSinhs
            .AsNoTracking()
            .Where(h => h.TaoLuc >= prevStartUtc && h.TaoLuc < endExclusiveUtc)
            .Select(h => h.TaoLuc)
            .ToListAsync(cancellationToken);

        var dsGiaSuMoi = await _db.GiaSus
            .AsNoTracking()
            .Where(g => g.TaoLuc >= prevStartUtc && g.TaoLuc < endExclusiveUtc)
            .Select(g => g.TaoLuc)
            .ToListAsync(cancellationToken);

        int nguoiDungMoiKyNay = 0;
        int nguoiDungMoiKyTruoc = 0;

        foreach (var t in dsHocSinhMoi)
        {
            if (t >= startUtc)
            {
                nguoiDungMoiKyNay++;
                var khoa = LayKhoaThoiGian(t);
                if (buckets.TryGetValue(khoa, out var b)) b.HocSinhMoi++;
            }
            else
            {
                nguoiDungMoiKyTruoc++;
            }
        }

        foreach (var t in dsGiaSuMoi)
        {
            if (t >= startUtc)
            {
                nguoiDungMoiKyNay++;
                var khoa = LayKhoaThoiGian(t);
                if (buckets.TryGetValue(khoa, out var b)) b.GiaSuMoi++;
            }
            else
            {
                nguoiDungMoiKyTruoc++;
            }
        }

        // 5. Top 5 gia sư có doanh thu cao nhất trong kỳ
        var topGiaSuNhom = donKyNay
            .Where(d => d.GiaSuId != Guid.Empty)
            .GroupBy(d => new { d.GiaSuId, d.TenGiaSu, d.KhuVucGiaSu })
            .Select(g =>
            {
                int doanhThu = 0;
                int thuNhap = 0;
                foreach (var d in g)
                {
                    if (doanhThuTheoDonIdKyNay.TryGetValue(d.Id, out var rev))
                    {
                        doanhThu += rev.DoanhThu;
                        thuNhap += rev.ThuNhap;
                    }
                    else if (d.TrangThai is TrangThaiDonDatLich.HOAN_THANH or TrangThaiDonDatLich.DA_XAC_NHAN)
                    {
                        var tong = TinhTongTienDon(d.LaBuoiHocThu, d.GiaMoiGio, d.TienGiam, d.BatDauLuc, d.KetThucLuc);
                        var (_, _, thucNhan) = BoTinhTaiChinhDon.TinhTaiChinhDon(tong, _policy.DefaultPlatformFeePercent);
                        doanhThu += tong;
                        thuNhap += thucNhan;
                    }
                }

                return new
                {
                    g.Key.GiaSuId,
                    g.Key.TenGiaSu,
                    g.Key.KhuVucGiaSu,
                    SoBuoiHoanThanh = g.Count(x => x.TrangThai == TrangThaiDonDatLich.HOAN_THANH),
                    TongDoanhThu = doanhThu,
                    ThuNhapGiaSu = thuNhap
                };
            })
            .Where(x => x.TongDoanhThu > 0 || x.SoBuoiHoanThanh > 0)
            .OrderByDescending(x => x.TongDoanhThu)
            .ThenByDescending(x => x.SoBuoiHoanThanh)
            .Take(5)
            .ToList();

        var topGiaSuIds = topGiaSuNhom.Select(x => x.GiaSuId).ToList();
        var tapGiaSuDaXacMinh = topGiaSuIds.Count == 0
            ? new HashSet<Guid>()
            : (await _db.GiayToGiaSus
                .AsNoTracking()
                .Where(gt =>
                    topGiaSuIds.Contains(gt.GiaSuId) &&
                    gt.TrangThai == TrangThaiGiayTo.DA_DUYET &&
                    (gt.Loai == LoaiGiayTo.BANG_CAP || gt.Loai == LoaiGiayTo.CHUNG_CHI))
                .Select(gt => gt.GiaSuId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var danhGiaTheoGiaSu = topGiaSuIds.Count == 0
            ? new Dictionary<Guid, (double DiemTb, int SoLuot)>()
            : (await _db.DanhGias
                .AsNoTracking()
                .Where(dg =>
                    dg.NguoiKiemDuyetId == null &&
                    dg.DonDatLich != null &&
                    dg.DonDatLich.GiaSuMonHoc != null &&
                    topGiaSuIds.Contains(dg.DonDatLich.GiaSuMonHoc.GiaSuId))
                .GroupBy(dg => dg.DonDatLich!.GiaSuMonHoc!.GiaSuId)
                .Select(g => new
                {
                    GiaSuId = g.Key,
                    DiemTb = g.Average(x => (double)x.Diem),
                    SoLuot = g.Count()
                })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.GiaSuId, x => (DiemTb: Math.Round(x.DiemTb, 1), x.SoLuot));

        var topGiaSu = topGiaSuNhom
            .Select(x =>
            {
                danhGiaTheoGiaSu.TryGetValue(x.GiaSuId, out var dg);
                return new TopGiaSuThongKeDto(
                    GiaSuId: x.GiaSuId,
                    HoTen: x.TenGiaSu,
                    KhuVuc: x.KhuVucGiaSu,
                    DaXacMinh: tapGiaSuDaXacMinh.Contains(x.GiaSuId),
                    SoBuoiHoanThanh: x.SoBuoiHoanThanh,
                    TongDoanhThu: x.TongDoanhThu,
                    ThuNhapGiaSu: x.ThuNhapGiaSu,
                    DiemDanhGiaTrungBinh: dg.DiemTb,
                    SoLuotDanhGia: dg.SoLuot);
            })
            .ToList();

        // 6. Số dư tổng toàn hệ thống & học phí đang tạm giữ theo thời gian thực
        var tongSoDuViHeThong = await _db.TaiKhoanVis
            .AsNoTracking()
            .SumAsync(v => (int?)v.SoDu, cancellationToken) ?? 0;

        var tongTienDangTamGiu = await _db.ThanhToans
            .AsNoTracking()
            .Where(t => t.TrangThai == TrangThaiThanhToan.DANG_GIU)
            .SumAsync(t => (int?)(t.SoTien - t.TienDaHoan), cancellationToken) ?? 0;

        static double? TinhTangTruong(int kyNay, int kyTruoc)
        {
            if (kyTruoc == 0)
            {
                return kyNay == 0 ? 0.0 : null;
            }

            return Math.Round((kyNay - kyTruoc) * 100.0 / kyTruoc, 1);
        }

        var giaTriDonTrungBinh = soThanhToanCoGiaTriKyNay == 0
            ? 0
            : (int)Math.Round(tongDoanhThuKyNay / (double)soThanhToanCoGiaTriKyNay, MidpointRounding.AwayFromZero);

        var bieuDoTheoThoiGian = danhSachKhoa
            .Select(k =>
            {
                var b = buckets[k.Khoa];
                return new DiemBieuDoThoiGianDto(
                    Nhan: b.Nhan,
                    KhoaThoiGian: b.KhoaThoiGian,
                    TongDoanhThu: b.TongDoanhThu,
                    PhiNenTang: b.PhiNenTang,
                    ThuNhapGiaSu: b.ThuNhapGiaSu,
                    TongNapVi: b.TongNapVi,
                    TongRutTien: b.TongRutTien,
                    TongHoanTien: b.TongHoanTien,
                    SoDonDatMoi: b.SoDonDatMoi,
                    SoBuoiHoanThanh: b.SoBuoiHoanThanh,
                    HocSinhMoi: b.HocSinhMoi,
                    GiaSuMoi: b.GiaSuMoi);
            })
            .ToList();

        return new ThongKeChiTietQuanTriDto(
            PhamVi: phamViChuan,
            NhanPhamVi: nhanPhamVi,
            DonViThoiGian: donViThoiGian,
            TuNgay: startUtc,
            DenNgay: endExclusiveUtc.AddSeconds(-1),
            TongDoanhThuTrongKy: tongDoanhThuKyNay,
            TangTruongDoanhThuPhanTram: TinhTangTruong(tongDoanhThuKyNay, tongDoanhThuKyTruoc),
            TongPhiNenTangTrongKy: tongPhiKyNay,
            TangTruongPhiPhanTram: TinhTangTruong(tongPhiKyNay, tongPhiKyTruoc),
            TongThuNhapGiaSuTrongKy: tongThuNhapKyNay,
            TongNapViTrongKy: tongNapViKyNay,
            TongRutTienDaChiTraTrongKy: tongRutTienKyNay,
            TongHoanTienTrongKy: tongHoanTienKyNay,
            SoDonDatTrongKy: soDonKyNay,
            TangTruongDonPhanTram: TinhTangTruong(soDonKyNay, soDonKyTruoc),
            SoDonHoanThanhTrongKy: soDonHoanThanhKyNay,
            TyLeHoanThanhPhanTram: tyLeHoanThanh,
            GiaTriDonTrungBinh: giaTriDonTrungBinh,
            NguoiDungMoiTrongKy: nguoiDungMoiKyNay,
            TangTruongNguoiDungPhanTram: TinhTangTruong(nguoiDungMoiKyNay, nguoiDungMoiKyTruoc),
            TongSoDuViHeThong: Math.Max(0, tongSoDuViHeThong),
            TongTienDangTamGiu: Math.Max(0, tongTienDangTamGiu),
            BieuDoTheoThoiGian: bieuDoTheoThoiGian,
            PhanBoTrangThaiDon: phanBoTrangThaiDon,
            TopMonHocTheoDoanhThu: topMonHoc,
            PhanBoHinhThucHoc: phanBoHinhThuc,
            PhanBoTrangThaiThanhToan: phanBoTrangThaiTt,
            TopGiaSu: topGiaSu);
    }

    private sealed class BoDemThoiGianNoiBo(string nhan, string khoaThoiGian)
    {
        public string Nhan { get; } = nhan;
        public string KhoaThoiGian { get; } = khoaThoiGian;
        public int TongDoanhThu { get; set; }
        public int PhiNenTang { get; set; }
        public int ThuNhapGiaSu { get; set; }
        public int TongNapVi { get; set; }
        public int TongRutTien { get; set; }
        public int TongHoanTien { get; set; }
        public int SoDonDatMoi { get; set; }
        public int SoBuoiHoanThanh { get; set; }
        public int HocSinhMoi { get; set; }
        public int GiaSuMoi { get; set; }
    }

    public async Task<KetQuaPhanTrangDto<GiaSuQuanTriDto>> LayDsGiaSuQuanTriAsync(
        TrangThaiHoSoGiaSu? trangThai = null,
        string? tuKhoa = null,
        int trang = 1,
        int kichThuocTrang = 12,
        CancellationToken cancellationToken = default)
    {
        var trangChuan = Math.Max(1, trang);
        var kichThuocChuan = Math.Clamp(kichThuocTrang, 1, 50);

        var truyVan = _db.GiaSus.AsNoTracking().AsQueryable();

        if (trangThai.HasValue)
        {
            truyVan = truyVan.Where(g => g.TrangThaiHoSo == trangThai.Value);
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var tk = tuKhoa.Trim();
            truyVan = truyVan.Where(g =>
                g.HoTen.Contains(tk) ||
                g.Email.Contains(tk) ||
                g.TieuDe.Contains(tk) ||
                (g.KhuVuc != null && g.KhuVuc.Contains(tk)));
        }

        var tong = await truyVan.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocChuan);

        var ds = await truyVan
            .Include(g => g.NguoiDuyet)
            .Include(g => g.DsGiayTo).ThenInclude(gt => gt.NguoiDuyet)
            .Include(g => g.DsMonDay)
            .AsSplitQuery()
            .OrderBy(g => g.TrangThaiHoSo == TrangThaiHoSoGiaSu.CHO_DUYET ? 0 : 1)
            .ThenByDescending(g => g.TaoLuc)
            .Skip((trangChuan - 1) * kichThuocChuan)
            .Take(kichThuocChuan)
            .ToListAsync(cancellationToken);

        return new KetQuaPhanTrangDto<GiaSuQuanTriDto>(
            DuLieu: ds.Select(AnhXaGiaSuQuanTri).ToList(),
            Trang: trangChuan,
            KichThuocTrang: kichThuocChuan,
            Tong: tong,
            TongTrang: tongTrang);
    }

    public async Task<GiaSuQuanTriDto> DuyetHoSoGiaSuAsync(
        Guid quanTriId,
        Guid giaSuId,
        DuyetHoSoGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var giaSu = await _db.GiaSus
            .Include(g => g.DsGiayTo).ThenInclude(gt => gt.NguoiDuyet)
            .Include(g => g.DsMonDay)
            .FirstOrDefaultAsync(g => g.Id == giaSuId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hồ sơ gia sư cần xét duyệt.", 404);

        if (!yeuCau.PheDuyet)
        {
            var lyDo = yeuCau.LyDoTuChoi?.Trim();
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng nhập lý do khi từ chối hồ sơ gia sư.", 400);
            }

            _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("lyDoTuChoi", lyDo, false)]);
            giaSu.TrangThaiHoSo = TrangThaiHoSoGiaSu.TU_CHOI;
            giaSu.LyDoTuChoi = lyDo;
        }
        else
        {
            giaSu.TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET;
            giaSu.LyDoTuChoi = null;
        }

        giaSu.NguoiDuyetId = quanTriId;
        await _db.SaveChangesAsync(cancellationToken);

        giaSu.NguoiDuyet = await _db.QuanTriViens.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quanTriId, cancellationToken);
        return AnhXaGiaSuQuanTri(giaSu);
    }

    public async Task<GiayToQuanTriDto> DuyetGiayToGiaSuAsync(
        Guid quanTriId,
        Guid giayToId,
        DuyetGiayToYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var giayTo = await _db.GiayToGiaSus
            .FirstOrDefaultAsync(g => g.Id == giayToId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy giấy tờ cần duyệt.", 404);

        giayTo.TrangThai = yeuCau.PheDuyet ? TrangThaiGiayTo.DA_DUYET : TrangThaiGiayTo.TU_CHOI;
        giayTo.NguoiDuyetId = quanTriId;
        giayTo.DuyetLuc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var quanTri = await _db.QuanTriViens.AsNoTracking().FirstOrDefaultAsync(q => q.Id == quanTriId, cancellationToken);
        return new GiayToQuanTriDto(
            giayTo.Id,
            giayTo.Loai,
            giayTo.TenTepGoc,
            giayTo.TrangThai,
            giayTo.DuyetLuc,
            quanTri?.HoTen,
            quanTri?.Email);
    }

    public async Task<KetQuaPhanTrangDto<NguoiDungQuanTriDto>> LayDsNguoiDungQuanTriAsync(
        VaiTro? vaiTro = null,
        string? tuKhoa = null,
        int trang = 1,
        int kichThuocTrang = 15,
        CancellationToken cancellationToken = default)
    {
        var trangChuan = Math.Max(1, trang);
        var kichThuocChuan = Math.Clamp(kichThuocTrang, 1, 50);

        IQueryable<NguoiDung> truyVan = vaiTro switch
        {
            VaiTro.HOC_SINH => _db.HocSinhs.AsNoTracking(),
            VaiTro.GIA_SU => _db.GiaSus.AsNoTracking(),
            VaiTro.QUAN_TRI => _db.QuanTriViens.AsNoTracking(),
            _ => _db.NguoiDungs.AsNoTracking()
        };

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var tk = tuKhoa.Trim();
            truyVan = truyVan.Where(u =>
                u.HoTen.Contains(tk) ||
                u.Email.Contains(tk) ||
                (u.KhuVuc != null && u.KhuVuc.Contains(tk)));
        }

        var tong = await truyVan.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocChuan);

        var ds = await truyVan
            .OrderByDescending(u => u.TaoLuc)
            .Skip((trangChuan - 1) * kichThuocChuan)
            .Take(kichThuocChuan)
            .ToListAsync(cancellationToken);

        var dsIds = ds.Select(u => u.Id).ToList();
        var dsVi = dsIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.TaiKhoanVis
                .AsNoTracking()
                .Where(v => dsIds.Contains(v.NguoiDungThamGiaId))
                .ToDictionaryAsync(v => v.NguoiDungThamGiaId, v => v.SoDu, cancellationToken);

        var duLieu = ds.Select(u => new NguoiDungQuanTriDto(
            Id: u.Id,
            HoTen: u.HoTen,
            Email: u.Email,
            SoDienThoai: u.SoDienThoai,
            KhuVuc: u.KhuVuc,
            VaiTro: u.VaiTro,
            TrangThai: u.TrangThai,
            SoDuVi: dsVi.TryGetValue(u.Id, out var soDu) ? soDu : null,
            TaoLuc: u.TaoLuc)).ToList();

        return new KetQuaPhanTrangDto<NguoiDungQuanTriDto>(
            DuLieu: duLieu,
            Trang: trangChuan,
            KichThuocTrang: kichThuocChuan,
            Tong: tong,
            TongTrang: tongTrang);
    }

    public async Task<NguoiDungQuanTriDto> KhoaTaiKhoanAsync(
        Guid quanTriId,
        Guid nguoiDungId,
        CancellationToken cancellationToken = default)
    {
        if (quanTriId == nguoiDungId)
        {
            throw new LoiUngDungException(
                "KHONG_THE_TU_KHOA",
                "Quản trị viên không thể tự khóa tài khoản đang đăng nhập.",
                400);
        }

        var user = await _db.NguoiDungs.FirstOrDefaultAsync(u => u.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        if (user.VaiTro == VaiTro.QUAN_TRI)
        {
            throw new LoiUngDungException("KHONG_DUOC_PHEP", "Không thể khóa tài khoản quản trị viên khác.", 403);
        }

        user.TrangThai = TrangThaiNguoiDung.BI_KHOA;
        await _db.SaveChangesAsync(cancellationToken);

        var soDu = await _db.TaiKhoanVis.AsNoTracking()
            .Where(v => v.NguoiDungThamGiaId == user.Id)
            .Select(v => (int?)v.SoDu)
            .FirstOrDefaultAsync(cancellationToken);

        return new NguoiDungQuanTriDto(
            user.Id, user.HoTen, user.Email, user.SoDienThoai, user.KhuVuc,
            user.VaiTro, user.TrangThai, soDu, user.TaoLuc);
    }

    public async Task<NguoiDungQuanTriDto> MoKhoaTaiKhoanAsync(
        Guid nguoiDungId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.NguoiDungs.FirstOrDefaultAsync(u => u.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        user.TrangThai = TrangThaiNguoiDung.HOAT_DONG;
        await _db.SaveChangesAsync(cancellationToken);

        var soDu = await _db.TaiKhoanVis.AsNoTracking()
            .Where(v => v.NguoiDungThamGiaId == user.Id)
            .Select(v => (int?)v.SoDu)
            .FirstOrDefaultAsync(cancellationToken);

        return new NguoiDungQuanTriDto(
            user.Id, user.HoTen, user.Email, user.SoDienThoai, user.KhuVuc,
            user.VaiTro, user.TrangThai, soDu, user.TaoLuc);
    }

    public async Task<IReadOnlyList<MonHocQuanTriDto>> LayDsMonHocQuanTriAsync(CancellationToken cancellationToken = default)
    {
        var ds = await _db.MonHocs
            .AsNoTracking()
            .Include(m => m.DsGiaSuMonHoc)
            .OrderBy(m => m.Ten)
            .ToListAsync(cancellationToken);

        return ds.Select(m => new MonHocQuanTriDto(
            Id: m.Id,
            Ten: m.Ten,
            Slug: m.Slug,
            DangHoatDong: m.DangHoatDong,
            SoGiaSuDangDay: m.DsGiaSuMonHoc.Select(x => x.GiaSuId).Distinct().Count())).ToList();
    }

    public async Task<MonHocQuanTriDto> TaoMonHocAsync(
        TaoMonHocYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var ten = yeuCau.Ten?.Trim() ?? string.Empty;
        if (ten.Length < 2 || ten.Length > 100)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Tên môn học cần từ 2 đến 100 ký tự.", 400);
        }

        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("ten", ten, false)]);

        var daTonTai = await _db.MonHocs.AnyAsync(m => m.Ten.ToLower() == ten.ToLower(), cancellationToken);
        if (daTonTai)
        {
            throw new LoiUngDungException("MON_HOC_DA_TON_TAI", "Môn học này đã có trong danh mục.", 409);
        }

        var mon = new MonHoc { Ten = ten, DangHoatDong = true };
        _db.MonHocs.Add(mon);
        await _db.SaveChangesAsync(cancellationToken);

        return new MonHocQuanTriDto(mon.Id, mon.Ten, mon.Slug, mon.DangHoatDong, 0);
    }

    public async Task<MonHocQuanTriDto> CapNhatMonHocAsync(
        Guid monHocId,
        CapNhatMonHocYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var mon = await _db.MonHocs
            .Include(m => m.DsGiaSuMonHoc)
            .FirstOrDefaultAsync(m => m.Id == monHocId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy môn học.", 404);

        var ten = yeuCau.Ten?.Trim() ?? string.Empty;
        if (ten.Length < 2 || ten.Length > 100)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Tên môn học cần từ 2 đến 100 ký tự.", 400);
        }

        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("ten", ten, false)]);

        var trungTen = await _db.MonHocs.AnyAsync(
            m => m.Id != monHocId && m.Ten.ToLower() == ten.ToLower(),
            cancellationToken);
        if (trungTen)
        {
            throw new LoiUngDungException("MON_HOC_DA_TON_TAI", "Tên môn học đã trùng với môn khác.", 409);
        }

        mon.Ten = ten;
        mon.DangHoatDong = yeuCau.DangHoatDong;
        await _db.SaveChangesAsync(cancellationToken);

        return new MonHocQuanTriDto(
            mon.Id,
            mon.Ten,
            mon.Slug,
            mon.DangHoatDong,
            mon.DsGiaSuMonHoc.Select(x => x.GiaSuId).Distinct().Count());
    }

    private DonDatLichTomTatDto AnhXaDonDatLichTomTat(
        DonDatLich don,
        bool choGiaSu,
        IReadOnlyDictionary<Guid, int>? thuNhapTheoThanhToan,
        DonDatLich? donLienKeTruoc = null,
        GiaSu? giaSu = null)
    {
        var doiTacId = choGiaSu ? don.HocSinhId : (don.GiaSuMonHoc?.GiaSuId ?? Guid.Empty);
        var tenDoiTac = choGiaSu
            ? (don.HocSinh?.HoTen ?? "Học sinh")
            : (don.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư");

        var thanhToanMoiNhat = don.DsThanhToan
            .OrderByDescending(t => t.TaoLuc)
            .FirstOrDefault();

        int phiNenTang;
        int thuNhapGiaSu;
        if (thanhToanMoiNhat is { TrangThai: TrangThaiThanhToan.DA_GIAI_NGAN } ttGiaiNgan &&
            thuNhapTheoThanhToan is not null &&
            thuNhapTheoThanhToan.TryGetValue(ttGiaiNgan.Id, out var thucNhanLichSu))
        {
            var soHocVienTra = Math.Max(0, ttGiaiNgan.SoTien - ttGiaiNgan.TienDaHoan);
            thuNhapGiaSu = thucNhanLichSu;
            phiNenTang = Math.Max(0, soHocVienTra - thucNhanLichSu);
        }
        else
        {
            var soHocVienTra = thanhToanMoiNhat is not null
                ? Math.Max(0, thanhToanMoiNhat.SoTien - thanhToanMoiNhat.TienDaHoan)
                : don.TongTien;
            (_, phiNenTang, thuNhapGiaSu) = BoTinhTaiChinhDon.TinhTaiChinhDon(
                soHocVienTra,
                _policy.DefaultPlatformFeePercent);
        }

        UocTinhDiChuyenDto? uocTinh = null;
        if (choGiaSu && don.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP)
        {
            uocTinh = DichVuUocTinhDiChuyen.PhanTichLoTrinhDiChuyen(don, donLienKeTruoc, giaSu);
        }

        return new DonDatLichTomTatDto(
            Id: don.Id,
            MaDon: don.MaDon,
            DoiTacId: doiTacId,
            TenDoiTac: tenDoiTac,
            TenMonHoc: don.GiaSuMonHoc?.MonHoc?.Ten ?? "Môn học",
            HinhThuc: don.HinhThuc,
            BatDauLuc: don.BatDauLuc,
            KetThucLuc: don.KetThucLuc,
            TrangThai: don.TrangThai,
            LaBuoiHocThu: don.LaBuoiHocThu,
            TongTien: don.TongTien,
            ThuNhapGiaSu: thuNhapGiaSu,
            PhongHocUrl: don.PhongHocUrl,
            TrangThaiThanhToan: thanhToanMoiNhat?.TrangThai,
            DiaChiHocSinh: don.DiaChiHocSinh,
            UocTinhDiChuyen: uocTinh,
            PhiNenTang: phiNenTang);
    }

    private static GiaSuQuanTriDto AnhXaGiaSuQuanTri(GiaSu g) =>
        new(
            Id: g.Id,
            HoTen: g.HoTen,
            Email: g.Email,
            KhuVuc: g.KhuVuc,
            TieuDe: g.TieuDe,
            HocVan: g.HocVan,
            SoNamKinhNghiem: g.SoNamKinhNghiem,
            DayTrucTuyen: g.DayTrucTuyen,
            DayTrucTiep: g.DayTrucTiep,
            BanKinhPhucVuKm: g.DayTrucTiep ? g.BanKinhPhucVuKm : 0,
            TrangThaiHoSo: g.TrangThaiHoSo,
            LyDoTuChoi: g.LyDoTuChoi,
            DaXacMinh: g.DaXacMinh,
            TrangThaiTaiKhoan: g.TrangThai,
            SoMonDay: g.DsMonDay.Count,
            TaoLuc: g.TaoLuc,
            DsGiayTo: g.DsGiayTo
                .OrderByDescending(x => x.DuyetLuc)
                .Select(x => new GiayToQuanTriDto(
                    x.Id,
                    x.Loai,
                    x.TenTepGoc,
                    x.TrangThai,
                    x.DuyetLuc,
                    x.NguoiDuyet?.HoTen,
                    x.NguoiDuyet?.Email))
                .ToList(),
            TenNguoiDuyet: g.NguoiDuyet?.HoTen,
            EmailNguoiDuyet: g.NguoiDuyet?.Email);
}
