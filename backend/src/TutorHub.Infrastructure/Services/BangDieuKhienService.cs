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
            .Include(g => g.DsGiayTo)
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
            .Include(g => g.DsGiayTo)
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

        return new GiayToQuanTriDto(
            giayTo.Id,
            giayTo.Loai,
            giayTo.TenTepGoc,
            giayTo.TrangThai,
            giayTo.DuyetLuc);
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
                .Select(x => new GiayToQuanTriDto(x.Id, x.Loai, x.TenTepGoc, x.TrangThai, x.DuyetLuc))
                .ToList());
}
