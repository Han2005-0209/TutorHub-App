using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Infrastructure.Persistence;

namespace TutorHub.Infrastructure.Services;

public sealed class HoiThoaiService : IHoiThoaiService
{
    private const string ChuoiCheThongTin = "[Thông tin liên hệ bị ẩn để bảo vệ giao dịch]";

    // BR-MSG-01: Bộ lọc chống lộ SĐT, Email, Zalo, Facebook, URL ngoài nền tảng
    private static readonly Regex[] BoLocLienHe =
    [
        new(@"(?<!\d)(?:\+?84|0)(?:[\s.\-]*\d){8,9}(?!\d)", RegexOptions.Compiled),
        new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?:zalo|zl\s*[:.]|fb\.com|fb\.me|facebook\.com|telegram|t\.me|whatsapp|viber)\b[^\s]*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"\b(?:https?://|www\.)[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    private readonly AppDbContext _db;

    public HoiThoaiService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HoiThoaiChiTietDto> MoHoacTaoHoiThoaiAsync(
        Guid hocSinhId,
        Guid giaSuId,
        CancellationToken cancellationToken = default)
    {
        if (hocSinhId == giaSuId)
        {
            throw new LoiUngDungException("KHONG_DUOC_TU_THAO_TAC", "Bạn không thể tự nhắn tin với chính mình.", 422);
        }

        var giaSu = await _db.GiaSus
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == giaSuId && g.TrangThai == TrangThaiNguoiDung.HOAT_DONG, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy gia sư.", 404);

        var hoiThoai = await _db.HoiThoais
            .Include(h => h.HocSinh)
            .Include(h => h.GiaSu)
            .Include(h => h.DsTinNhan).ThenInclude(t => t.NguoiGui)
            .FirstOrDefaultAsync(h => h.HocSinhId == hocSinhId && h.GiaSuId == giaSuId, cancellationToken);

        if (hoiThoai is null)
        {
            hoiThoai = new HoiThoai
            {
                HocSinhId = hocSinhId,
                GiaSuId = giaSuId,
                TaoLuc = DateTime.UtcNow
            };
            _db.HoiThoais.Add(hoiThoai);
            await _db.SaveChangesAsync(cancellationToken);

            hoiThoai = await _db.HoiThoais
                .Include(h => h.HocSinh)
                .Include(h => h.GiaSu)
                .Include(h => h.DsTinNhan).ThenInclude(t => t.NguoiGui)
                .FirstAsync(h => h.Id == hoiThoai.Id, cancellationToken);
        }

        await DanhDauDaDocAsync(hoiThoai, hocSinhId, cancellationToken);
        return AnhXaChiTiet(hoiThoai, hocSinhId, giaSu);
    }

    public async Task<IReadOnlyList<HoiThoaiTomTatDto>> LayDsHoiThoaiCuaToiAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        CancellationToken cancellationToken = default)
    {
        var truyVan = _db.HoiThoais
            .AsNoTracking()
            .Include(h => h.HocSinh)
            .Include(h => h.GiaSu)
            .Include(h => h.DsTinNhan)
            .AsQueryable();

        truyVan = vaiTro == VaiTro.GIA_SU
            ? truyVan.Where(h => h.GiaSuId == nguoiDungId)
            : truyVan.Where(h => h.HocSinhId == nguoiDungId);

        var ds = await truyVan.ToListAsync(cancellationToken);

        return ds
            .Select(h =>
            {
                var laHocSinh = h.HocSinhId == nguoiDungId;
                var tinCuoi = h.DsTinNhan.OrderByDescending(t => t.TaoLuc).FirstOrDefault();
                var soChuaDoc = h.DsTinNhan.Count(t => t.NguoiGuiId != nguoiDungId && t.DocLuc == null);
                return new HoiThoaiTomTatDto(
                    Id: h.Id,
                    DoiTacId: laHocSinh ? h.GiaSuId : h.HocSinhId,
                    TenDoiTac: laHocSinh ? (h.GiaSu?.HoTen ?? "Gia sư") : (h.HocSinh?.HoTen ?? "Học sinh"),
                    AnhDoiTac: laHocSinh ? h.GiaSu?.AnhDaiDienUrl : h.HocSinh?.AnhDaiDienUrl,
                    VaiTroDoiTac: laHocSinh ? VaiTro.GIA_SU : VaiTro.HOC_SINH,
                    TinNhanCuoi: tinCuoi?.NoiDung,
                    TinNhanCuoiLuc: tinCuoi?.TaoLuc ?? h.TaoLuc,
                    SoChuaDoc: soChuaDoc);
            })
            .OrderByDescending(x => x.TinNhanCuoiLuc)
            .ToList();
    }

    public async Task<HoiThoaiChiTietDto> LayChiTietHoiThoaiAsync(
        Guid nguoiDungId,
        Guid hoiThoaiId,
        CancellationToken cancellationToken = default)
    {
        var hoiThoai = await _db.HoiThoais
            .Include(h => h.HocSinh)
            .Include(h => h.GiaSu)
            .Include(h => h.DsTinNhan).ThenInclude(t => t.NguoiGui)
            .FirstOrDefaultAsync(h => h.Id == hoiThoaiId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hội thoại.", 404);

        if (hoiThoai.HocSinhId != nguoiDungId && hoiThoai.GiaSuId != nguoiDungId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền truy cập hội thoại này.", 403);
        }

        await DanhDauDaDocAsync(hoiThoai, nguoiDungId, cancellationToken);
        return AnhXaChiTiet(hoiThoai, nguoiDungId);
    }

    public async Task<TinNhanDto> GuiTinNhanAsync(
        Guid nguoiGuiId,
        Guid hoiThoaiId,
        string noiDung,
        CancellationToken cancellationToken = default)
    {
        var goc = noiDung?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(goc) || goc.Length > 2000)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Nội dung tin nhắn phải từ 1 đến 2000 ký tự.", 400);
        }

        var hoiThoai = await _db.HoiThoais
            .Include(h => h.HocSinh)
            .Include(h => h.GiaSu)
            .FirstOrDefaultAsync(h => h.Id == hoiThoaiId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy hội thoại.", 404);

        if (hoiThoai.HocSinhId != nguoiGuiId && hoiThoai.GiaSuId != nguoiGuiId)
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Bạn không có quyền gửi tin nhắn trong hội thoại này.", 403);
        }

        var (noiDungDaLoc, biGanCo) = LocThongTinLienHeNgoaiLuong(goc);
        var nowUtc = DateTime.UtcNow;

        var tin = new TinNhan
        {
            HoiThoaiId = hoiThoaiId,
            NguoiGuiId = nguoiGuiId,
            NoiDung = noiDungDaLoc,
            BiGanCo = biGanCo,
            TaoLuc = nowUtc
        };

        _db.TinNhans.Add(tin);

        var nguoiNhanId = hoiThoai.HocSinhId == nguoiGuiId ? hoiThoai.GiaSuId : hoiThoai.HocSinhId;
        var tenNguoiGui = hoiThoai.HocSinhId == nguoiGuiId
            ? (hoiThoai.HocSinh?.HoTen ?? "Học sinh")
            : (hoiThoai.GiaSu?.HoTen ?? "Gia sư");

        _db.ThongBaos.Add(new ThongBao
        {
            NguoiNhanId = nguoiNhanId,
            Loai = LoaiThongBao.HE_THONG,
            TieuDe = $"Tin nhắn mới từ {tenNguoiGui}",
            NoiDung = noiDungDaLoc.Length > 120 ? noiDungDaLoc[..120] + "..." : noiDungDaLoc,
            TaoLuc = nowUtc
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new TinNhanDto(
            Id: tin.Id,
            HoiThoaiId: tin.HoiThoaiId,
            NguoiGuiId: tin.NguoiGuiId,
            TenNguoiGui: tenNguoiGui,
            LaCuaToi: true,
            NoiDung: tin.NoiDung,
            BiGanCo: tin.BiGanCo,
            DocLuc: tin.DocLuc,
            TaoLuc: tin.TaoLuc);
    }

    public static (string NoiDungDaLoc, bool BiGanCo) LocThongTinLienHeNgoaiLuong(string goc)
    {
        var ketQua = goc;
        var biGanCo = false;
        foreach (var regex in BoLocLienHe)
        {
            if (regex.IsMatch(ketQua))
            {
                biGanCo = true;
                ketQua = regex.Replace(ketQua, ChuoiCheThongTin);
            }
        }
        return (ketQua, biGanCo);
    }

    private async Task DanhDauDaDocAsync(HoiThoai hoiThoai, Guid nguoiDocId, CancellationToken cancellationToken)
    {
        var chuaDoc = hoiThoai.DsTinNhan.Where(t => t.NguoiGuiId != nguoiDocId && t.DocLuc == null).ToList();
        if (chuaDoc.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var t in chuaDoc)
        {
            t.DocLuc = now;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static HoiThoaiChiTietDto AnhXaChiTiet(HoiThoai h, Guid nguoiXemId, GiaSu? giaSuDuPhong = null)
    {
        var laHocSinh = h.HocSinhId == nguoiXemId;
        var doiTacGs = h.GiaSu ?? giaSuDuPhong;
        var dsTin = h.DsTinNhan
            .OrderBy(t => t.TaoLuc)
            .Select(t => new TinNhanDto(
                Id: t.Id,
                HoiThoaiId: t.HoiThoaiId,
                NguoiGuiId: t.NguoiGuiId,
                TenNguoiGui: t.NguoiGui?.HoTen ?? (t.NguoiGuiId == h.HocSinhId ? (h.HocSinh?.HoTen ?? "Học sinh") : (doiTacGs?.HoTen ?? "Gia sư")),
                LaCuaToi: t.NguoiGuiId == nguoiXemId,
                NoiDung: t.NoiDung,
                BiGanCo: t.BiGanCo,
                DocLuc: t.DocLuc,
                TaoLuc: t.TaoLuc))
            .ToList();

        return new HoiThoaiChiTietDto(
            Id: h.Id,
            DoiTacId: laHocSinh ? h.GiaSuId : h.HocSinhId,
            TenDoiTac: laHocSinh ? (doiTacGs?.HoTen ?? "Gia sư") : (h.HocSinh?.HoTen ?? "Học sinh"),
            AnhDoiTac: laHocSinh ? doiTacGs?.AnhDaiDienUrl : h.HocSinh?.AnhDaiDienUrl,
            VaiTroDoiTac: laHocSinh ? VaiTro.GIA_SU : VaiTro.HOC_SINH,
            DsTinNhan: dsTin);
    }
}
