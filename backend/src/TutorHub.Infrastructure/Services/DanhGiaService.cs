using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Infrastructure.Persistence;

namespace TutorHub.Infrastructure.Services;

/// <summary>
/// Cài đặt đầy đủ nghiệp vụ Đánh giá buổi học (Module 6 — `DanhGia`).
/// </summary>
public sealed class DanhGiaService : IDanhGiaService
{
    private const int GioChoPhepSuaDanhGia = 48;
    private readonly AppDbContext _db;
    private readonly IKiemDuyetNoiDungService _kiemDuyet;

    public DanhGiaService(AppDbContext db, IKiemDuyetNoiDungService kiemDuyet)
    {
        _db = db;
        _kiemDuyet = kiemDuyet;
    }

    public async Task<DanhGiaChiTietDto> TaoDanhGiaAsync(
        Guid hocSinhId,
        TaoDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        KiemTraDiemVaBinhLuan(yeuCau.Diem, yeuCau.BinhLuan);
        var binhLuan = yeuCau.BinhLuan.Trim();
        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("binhLuan", binhLuan, true)]);

        var don = await _db.DonDatLichs
            .Include(d => d.HocSinh)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(d => d.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(d => d.DanhGia)
            .FirstOrDefaultAsync(d => d.Id == yeuCau.DonDatLichId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đơn đặt lịch cần đánh giá.", 404);

        if (don.HocSinhId != hocSinhId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Chỉ học viên của buổi học này mới có thể gửi đánh giá.", 403);
        }

        if (don.TrangThai != TrangThaiDonDatLich.HOAN_THANH)
        {
            throw new LoiUngDungException(
                "CHUYEN_TRANG_THAI_SAI",
                "Chỉ có thể đánh giá sau khi buổi học đã ở trạng thái Hoàn thành.",
                409);
        }

        if (don.DanhGia is not null)
        {
            throw new LoiUngDungException(
                "DA_DANH_GIA",
                "Buổi học này đã được đánh giá. Bạn có thể chỉnh sửa trong vòng 48 giờ kể từ lúc gửi.",
                409);
        }

        var now = DateTime.UtcNow;
        var danhGia = new DanhGia
        {
            Id = Guid.NewGuid(),
            DonDatLichId = don.Id,
            Diem = yeuCau.Diem,
            BinhLuan = binhLuan,
            TaoLuc = now
        };

        _db.DanhGias.Add(danhGia);

        if (don.GiaSuMonHoc is not null)
        {
            _db.ThongBaos.Add(new ThongBao
            {
                Id = Guid.NewGuid(),
                NguoiNhanId = don.GiaSuMonHoc.GiaSuId,
                Loai = LoaiThongBao.DANH_GIA,
                TieuDe = $"Nhận đánh giá {yeuCau.Diem}★ cho đơn {don.MaDon}",
                NoiDung = $"Học viên {don.HocSinh?.HoTen ?? "TutorHub"} vừa đánh giá {yeuCau.Diem}/5 sao môn {don.GiaSuMonHoc.MonHoc?.Ten ?? "học"}: \"{RutGon(binhLuan, 120)}\"",
                TaoLuc = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        danhGia.DonDatLich = don;
        return AnhXaChiTiet(danhGia);
    }

    public async Task<DanhGiaChiTietDto> SuaDanhGiaAsync(
        Guid hocSinhId,
        Guid danhGiaId,
        SuaDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        KiemTraDiemVaBinhLuan(yeuCau.Diem, yeuCau.BinhLuan);
        var binhLuan = yeuCau.BinhLuan.Trim();
        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("binhLuan", binhLuan, true)]);

        var dg = await _db.DanhGias
            .Include(x => x.NguoiKiemDuyet)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .FirstOrDefaultAsync(x => x.Id == danhGiaId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đánh giá cần chỉnh sửa.", 404);

        if (dg.DonDatLich?.HocSinhId != hocSinhId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền chỉnh sửa đánh giá này.", 403);
        }

        if (dg.BiAn)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Đánh giá này đã bị quản trị viên ẩn nên không thể chỉnh sửa.", 409);
        }

        if (DateTime.UtcNow > dg.TaoLuc.AddHours(GioChoPhepSuaDanhGia))
        {
            throw new LoiUngDungException(
                "QUA_HAN_CHINH_SUA",
                $"Đã quá thời hạn {GioChoPhepSuaDanhGia} giờ cho phép chỉnh sửa đánh giá.",
                409);
        }

        dg.Diem = yeuCau.Diem;
        dg.BinhLuan = binhLuan;
        await _db.SaveChangesAsync(cancellationToken);

        return AnhXaChiTiet(dg);
    }

    public async Task<KetQuaPhanTrangDto<DanhGiaChiTietDto>> LayDsDanhGiaCuaGiaSuAsync(
        Guid giaSuId,
        int trang = 1,
        int kichThuocTrang = 15,
        CancellationToken cancellationToken = default)
    {
        var trangChuan = Math.Max(1, trang);
        var kichThuocChuan = Math.Clamp(kichThuocTrang, 1, 50);

        var truyVan = _db.DanhGias
            .AsNoTracking()
            .Include(x => x.NguoiKiemDuyet)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Where(x => x.DonDatLich != null &&
                        x.DonDatLich.GiaSuMonHoc != null &&
                        x.DonDatLich.GiaSuMonHoc.GiaSuId == giaSuId);

        var tong = await truyVan.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocChuan);

        var ds = await truyVan
            .OrderByDescending(x => x.TaoLuc)
            .Skip((trangChuan - 1) * kichThuocChuan)
            .Take(kichThuocChuan)
            .ToListAsync(cancellationToken);

        return new KetQuaPhanTrangDto<DanhGiaChiTietDto>(
            DuLieu: ds.Select(AnhXaChiTiet).ToList(),
            Trang: trangChuan,
            KichThuocTrang: kichThuocChuan,
            Tong: tong,
            TongTrang: tongTrang);
    }

    public async Task<DanhGiaChiTietDto> PhanHoiDanhGiaAsync(
        Guid giaSuId,
        Guid danhGiaId,
        PhanHoiDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var phanHoi = yeuCau.PhanHoi?.Trim() ?? string.Empty;
        if (phanHoi.Length < 3 || phanHoi.Length > 1000)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Nội dung phản hồi cần từ 3 đến 1000 ký tự.", 400);
        }

        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("phanHoi", phanHoi, true)]);

        var dg = await _db.DanhGias
            .Include(x => x.NguoiKiemDuyet)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .FirstOrDefaultAsync(x => x.Id == danhGiaId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đánh giá cần phản hồi.", 404);

        if (dg.DonDatLich?.GiaSuMonHoc?.GiaSuId != giaSuId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Chỉ gia sư phụ trách buổi học mới có thể phản hồi đánh giá này.", 403);
        }

        if (dg.BiAn)
        {
            throw new LoiUngDungException("CHUYEN_TRANG_THAI_SAI", "Đánh giá đang bị ẩn bởi quản trị viên, không thể phản hồi.", 409);
        }

        if (!string.IsNullOrWhiteSpace(dg.PhanHoiGiaSu))
        {
            throw new LoiUngDungException("DA_PHAN_HOI", "Bạn đã phản hồi đánh giá này trước đó (mỗi đánh giá chỉ phản hồi 1 lần).", 409);
        }

        dg.PhanHoiGiaSu = phanHoi;

        _db.ThongBaos.Add(new ThongBao
        {
            Id = Guid.NewGuid(),
            NguoiNhanId = dg.DonDatLich.HocSinhId,
            Loai = LoaiThongBao.DANH_GIA,
            TieuDe = $"Gia sư đã phản hồi đánh giá đơn {dg.DonDatLich.MaDon}",
            NoiDung = $"{dg.DonDatLich.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư"} phản hồi: \"{RutGon(phanHoi, 120)}\"",
            TaoLuc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return AnhXaChiTiet(dg);
    }

    public async Task<KetQuaPhanTrangDto<DanhGiaChiTietDto>> LayDsDanhGiaQuanTriAsync(
        bool? biAn = null,
        string? tuKhoa = null,
        int trang = 1,
        int kichThuocTrang = 15,
        CancellationToken cancellationToken = default)
    {
        var trangChuan = Math.Max(1, trang);
        var kichThuocChuan = Math.Clamp(kichThuocTrang, 1, 50);

        var truyVan = _db.DanhGias
            .AsNoTracking()
            .Include(x => x.NguoiKiemDuyet)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .AsQueryable();

        if (biAn.HasValue)
        {
            truyVan = biAn.Value
                ? truyVan.Where(x => x.NguoiKiemDuyetId != null)
                : truyVan.Where(x => x.NguoiKiemDuyetId == null);
        }

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var tk = tuKhoa.Trim();
            truyVan = truyVan.Where(x =>
                x.BinhLuan.Contains(tk) ||
                (x.PhanHoiGiaSu != null && x.PhanHoiGiaSu.Contains(tk)) ||
                (x.DonDatLich != null && x.DonDatLich.MaDon.Contains(tk)) ||
                (x.DonDatLich != null && x.DonDatLich.HocSinh != null && x.DonDatLich.HocSinh.HoTen.Contains(tk)) ||
                (x.DonDatLich != null && x.DonDatLich.GiaSuMonHoc != null && x.DonDatLich.GiaSuMonHoc.GiaSu != null && x.DonDatLich.GiaSuMonHoc.GiaSu.HoTen.Contains(tk)));
        }

        var tong = await truyVan.CountAsync(cancellationToken);
        var tongTrang = tong == 0 ? 0 : (int)Math.Ceiling(tong / (double)kichThuocChuan);

        var ds = await truyVan
            .OrderByDescending(x => x.TaoLuc)
            .Skip((trangChuan - 1) * kichThuocChuan)
            .Take(kichThuocChuan)
            .ToListAsync(cancellationToken);

        return new KetQuaPhanTrangDto<DanhGiaChiTietDto>(
            DuLieu: ds.Select(AnhXaChiTiet).ToList(),
            Trang: trangChuan,
            KichThuocTrang: kichThuocChuan,
            Tong: tong,
            TongTrang: tongTrang);
    }

    public async Task<DanhGiaChiTietDto> KiemDuyetDanhGiaAsync(
        Guid quanTriId,
        Guid danhGiaId,
        KiemDuyetDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        var dg = await _db.DanhGias
            .Include(x => x.NguoiKiemDuyet)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.GiaSu)
            .Include(x => x.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .FirstOrDefaultAsync(x => x.Id == danhGiaId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy đánh giá cần kiểm duyệt.", 404);

        var quanTri = await _db.QuanTriViens
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quanTriId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_CO_QUYEN", "Tài khoản quản trị viên không hợp lệ.", 403);

        var trangThaiCu = new { dg.BiAn, dg.LyDoAn, dg.NguoiKiemDuyetId };

        if (yeuCau.AnDanhGia)
        {
            var lyDo = yeuCau.LyDoAn?.Trim() ?? string.Empty;
            if (lyDo.Length < 5 || lyDo.Length > 500)
            {
                throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng nhập lý do ẩn đánh giá (từ 5 đến 500 ký tự).", 400);
            }

            _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("lyDoAn", lyDo, false)]);
            dg.NguoiKiemDuyetId = quanTriId;
            dg.LyDoAn = lyDo;

            if (dg.DonDatLich is not null)
            {
                _db.ThongBaos.Add(new ThongBao
                {
                    Id = Guid.NewGuid(),
                    NguoiNhanId = dg.DonDatLich.HocSinhId,
                    Loai = LoaiThongBao.HE_THONG,
                    TieuDe = $"Đánh giá đơn {dg.DonDatLich.MaDon} đã được tạm ẩn",
                    NoiDung = $"Quản trị viên {quanTri.HoTen} đã ẩn đánh giá của bạn. Lý do: {lyDo}",
                    TaoLuc = DateTime.UtcNow
                });
            }
        }
        else
        {
            dg.NguoiKiemDuyetId = null;
            dg.LyDoAn = null;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            NguoiDungId = quanTriId,
            HanhDong = yeuCau.AnDanhGia ? "AN_DANH_GIA" : "KHOI_PHUC_DANH_GIA",
            DoiTuong = $"DanhGia:{dg.Id}",
            GiaTriCuJson = JsonSerializer.Serialize(trangThaiCu),
            GiaTriMoiJson = JsonSerializer.Serialize(new { BiAn = yeuCau.AnDanhGia, dg.LyDoAn, dg.NguoiKiemDuyetId }),
            TaoLuc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return AnhXaChiTiet(dg, yeuCau.AnDanhGia ? quanTri : null);
    }

    private static void KiemTraDiemVaBinhLuan(int diem, string? binhLuan)
    {
        if (diem < 1 || diem > 5)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Điểm đánh giá phải từ 1 đến 5 sao.", 400);
        }

        var nd = binhLuan?.Trim() ?? string.Empty;
        if (nd.Length < 5 || nd.Length > 1000)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Nội dung nhận xét cần từ 5 đến 1000 ký tự.", 400);
        }
    }

    private static string RutGon(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";

    private static DanhGiaChiTietDto AnhXaChiTiet(DanhGia dg) =>
        AnhXaChiTiet(dg, dg.NguoiKiemDuyet);

    private static DanhGiaChiTietDto AnhXaChiTiet(DanhGia dg, QuanTriVien? nguoiKiemDuyetOverride)
    {
        var coTheSua = !dg.BiAn && DateTime.UtcNow <= dg.TaoLuc.AddHours(GioChoPhepSuaDanhGia);
        var coThePhanHoi = !dg.BiAn && string.IsNullOrWhiteSpace(dg.PhanHoiGiaSu);
        var nguoiKiemDuyet = dg.BiAn ? (nguoiKiemDuyetOverride ?? dg.NguoiKiemDuyet) : null;

        return new DanhGiaChiTietDto(
            Id: dg.Id,
            DonDatLichId: dg.DonDatLichId,
            MaDon: dg.DonDatLich?.MaDon ?? string.Empty,
            HocSinhId: dg.DonDatLich?.HocSinhId ?? Guid.Empty,
            TenHocSinh: dg.DonDatLich?.HocSinh?.HoTen ?? "Học viên TutorHub",
            AnhDaiDienHocSinhUrl: dg.DonDatLich?.HocSinh?.AnhDaiDienUrl,
            GiaSuId: dg.DonDatLich?.GiaSuMonHoc?.GiaSuId ?? Guid.Empty,
            TenGiaSu: dg.DonDatLich?.GiaSuMonHoc?.GiaSu?.HoTen ?? "Gia sư",
            TenMonHoc: dg.DonDatLich?.GiaSuMonHoc?.MonHoc?.Ten ?? "Môn học",
            BuoiHocBatDauLuc: dg.DonDatLich?.BatDauLuc ?? dg.TaoLuc,
            Diem: dg.Diem,
            BinhLuan: dg.BinhLuan,
            PhanHoiGiaSu: dg.PhanHoiGiaSu,
            BiAn: dg.BiAn,
            LyDoAn: dg.LyDoAn,
            TenNguoiKiemDuyet: nguoiKiemDuyet?.HoTen,
            EmailNguoiKiemDuyet: nguoiKiemDuyet?.Email,
            TaoLuc: dg.TaoLuc,
            CoTheSua: coTheSua,
            CoThePhanHoi: coThePhanHoi);
    }
}
