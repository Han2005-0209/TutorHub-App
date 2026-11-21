using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Enums;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Module 3 — Danh mục Môn học (`/api/mon-hoc`).
/// </summary>
[ApiController]
[Route("api/mon-hoc")]
public sealed class MonHocController : ControllerBase
{
    private readonly IGiaSuVaMonHocService _service;

    public MonHocController(IGiaSuVaMonHocService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<MonHocDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<MonHocDto>>>> LayDsMonHoc(CancellationToken cancellationToken)
    {
        var ds = await _service.LayDsMonHocAsync(cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<MonHocDto>>(ds));
    }
}

/// <summary>
/// Controller Module 2 & 3 — Tìm kiếm Gia sư công khai, Khung giờ trống, Đánh giá & Quản lý Hồ sơ Gia sư của tôi (`/api/gia-su/*`).
/// </summary>
[ApiController]
[Route("api/gia-su")]
public sealed class GiaSuController : ControllerBase
{
    private readonly IGiaSuVaMonHocService _service;
    private readonly IViVaTaiChinhService _viVaTaiChinh;

    public GiaSuController(
        IGiaSuVaMonHocService service,
        IViVaTaiChinhService viVaTaiChinh)
    {
        _service = service;
        _viVaTaiChinh = viVaTaiChinh;
    }

    // ── Công khai: Tìm kiếm, Gợi ý, Chi tiết hồ sơ, Khung giờ trống, Đánh giá ──

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<ThongTinGiaSuTomTatDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<ThongTinGiaSuTomTatDto>>>> TimKiemGiaSu(
        [FromQuery] string? tuKhoa,
        [FromQuery] Guid? monHocId,
        [FromQuery] HinhThucBuoiHoc? hinhThuc,
        [FromQuery] string? khuVuc,
        [FromQuery] int? giaToiThieu,
        [FromQuery] int? giaToiDa,
        [FromQuery] bool? chiDaXacMinh,
        [FromQuery] bool? coHocThu,
        [FromQuery] string? sapXep = "phu_hop",
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 12,
        [FromQuery] double? viDo = null,
        [FromQuery] double? kinhDo = null,
        [FromQuery] double? banKinhQuetKm = null,
        CancellationToken cancellationToken = default)
    {
        var hocSinhId = User.LayNguoiDungIdTuyChon();
        var boLoc = new BoLocTimKiemGiaSuDto(
            TuKhoa: tuKhoa,
            MonHocId: monHocId,
            HinhThuc: hinhThuc,
            KhuVuc: khuVuc,
            GiaToiThieu: giaToiThieu,
            GiaToiDa: giaToiDa,
            ChiDaXacMinh: chiDaXacMinh,
            CoHocThu: coHocThu,
            SapXep: sapXep,
            Trang: trang,
            KichThuocTrang: kichThuocTrang,
            ViDo: viDo,
            KinhDo: kinhDo,
            BanKinhQuetKm: banKinhQuetKm);

        var kq = await _service.TimKiemGiaSuAsync(boLoc, hocSinhId, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<ThongTinGiaSuTomTatDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kq.KichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    [HttpGet("goi-y")]
    [AllowAnonymous]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<ThongTinGiaSuTomTatDto>>>> LayDsGiaSuGoiY(
        [FromQuery] int soLuong = 6,
        CancellationToken cancellationToken = default)
    {
        var hocSinhId = User.LayNguoiDungIdTuyChon();
        var ds = await _service.LayDsGiaSuGoiYAsync(hocSinhId, soLuong, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<ThongTinGiaSuTomTatDto>>(ds));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PhanHoiApi<ChiTietGiaSuCongKhaiDto>>> LayChiTietGiaSu(
        Guid id,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungIdTuyChon();
        var dto = await _service.LayChiTietGiaSuCongKhaiAsync(id, hocSinhId, cancellationToken);
        return Ok(new PhanHoiApi<ChiTietGiaSuCongKhaiDto>(dto));
    }

    [HttpGet("{id:guid}/khung-gio-trong")]
    [AllowAnonymous]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<KhungGioTrongDto>>>> LayKhungGioTrong(
        Guid id,
        [FromQuery] string? tuNgay,
        [FromQuery] string? denNgay,
        CancellationToken cancellationToken)
    {
        var homNayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var mocTu = DateOnly.TryParse(tuNgay, out var tn) ? tn : homNayVn;
        var mocDen = DateOnly.TryParse(denNgay, out var dn) ? dn : mocTu.AddDays(6);

        var ds = await _service.LayKhungGioTrongAsync(id, mocTu, mocDen, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<KhungGioTrongDto>>(ds));
    }

    [HttpGet("{id:guid}/danh-gia")]
    [AllowAnonymous]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<DanhGiaCongKhaiDto>>>> LayDsDanhGiaGiaSu(
        Guid id,
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 10,
        CancellationToken cancellationToken = default)
    {
        var kq = await _service.LayDsDanhGiaGiaSuAsync(id, trang, kichThuocTrang, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<DanhGiaCongKhaiDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kq.KichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    // ── Module 2 & 3: Quản lý hồ sơ, môn học, lịch rảnh & giấy tờ của Gia sư đang đăng nhập ──

    [HttpGet("toi")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<HoSoGiaSuToiDto>>> LayHoSoGiaSuToi(CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.LayHoSoGiaSuToiAsync(giaSuId, cancellationToken);
        return Ok(new PhanHoiApi<HoSoGiaSuToiDto>(dto));
    }

    [HttpPut("toi")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<HoSoGiaSuToiDto>>> CapNhatHoSoGiaSuToi(
        [FromBody] CapNhatHoSoGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.CapNhatHoSoGiaSuToiAsync(giaSuId, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<HoSoGiaSuToiDto>(dto));
    }

    // API 6: Đổi tài khoản ngân hàng nhận tiền (yêu cầu xác nhận mật khẩu + AuditLog)
    [HttpPut("toi/ngan-hang")]
    [Authorize(Roles = "GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<TaiKhoanNganHangCheDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TaiKhoanNganHangCheDto>>> CapNhatNganHangGiaSu(
        [FromBody] CapNhatNganHangYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var dto = await _viVaTaiChinh.CapNhatNganHangGiaSuAsync(giaSuId, yeuCau, ip, cancellationToken);
        return Ok(new PhanHoiApi<TaiKhoanNganHangCheDto>(dto));
    }

    [HttpPost("toi/gui-duyet")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<HoSoGiaSuToiDto>>> GuiDuyetHoSo(CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.GuiDuyetHoSoAsync(giaSuId, cancellationToken);
        return Ok(new PhanHoiApi<HoSoGiaSuToiDto>(dto));
    }

    [HttpPut("toi/mon-hoc")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<GiaSuMonHocDto>>>> CapNhatDsMonDay(
        [FromBody] CapNhatDsMonDayYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var ds = await _service.CapNhatDsMonDayAsync(giaSuId, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<GiaSuMonHocDto>>(ds));
    }

    [HttpGet("toi/lich-ranh")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<LichRanhGiaSuDto>>> LayLichRanhToi(CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.LayLichRanhToiAsync(giaSuId, cancellationToken);
        return Ok(new PhanHoiApi<LichRanhGiaSuDto>(dto));
    }

    [HttpPut("toi/lich-lap-hang-tuan")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<LichLapHangTuanDto>>>> CapNhatLichLapHangTuan(
        [FromBody] CapNhatLichLapHangTuanYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var ds = await _service.CapNhatLichLapHangTuanAsync(giaSuId, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<LichLapHangTuanDto>>(ds));
    }

    [HttpPost("toi/lich-ngay-cu-the")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<LichNgayCuTheDto>>> ThemLichNgayCuThe(
        [FromBody] TaoLichNgayCuTheYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.ThemLichNgayCuTheAsync(giaSuId, yeuCau, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<LichNgayCuTheDto>(dto));
    }

    [HttpDelete("toi/lich-ngay-cu-the/{id:guid}")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<object>>> XoaLichNgayCuThe(
        Guid id,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        await _service.XoaLichNgayCuTheAsync(giaSuId, id, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daXoa = true }));
    }

    [HttpGet("toi/xung-dot-ngay-nghi")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<KiemTraXungDotNgayNghiKetQuaDto>>> KiemTraXungDotNgayNghi(
        [FromQuery] string tuNgay,
        [FromQuery] string denNgay,
        [FromQuery] string? gioBatDau,
        [FromQuery] string? gioKetThuc,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        if (!DateOnly.TryParse(tuNgay, out var tn))
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Ngày bắt đầu không hợp lệ.", 400);
        }
        if (!DateOnly.TryParse(denNgay, out var dn))
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Ngày kết thúc không hợp lệ.", 400);
        }

        TimeOnly? bd = null;
        if (!string.IsNullOrWhiteSpace(gioBatDau) && TimeOnly.TryParse(gioBatDau, out var bdVal))
        {
            bd = bdVal;
        }

        TimeOnly? kt = null;
        if (!string.IsNullOrWhiteSpace(gioKetThuc) && TimeOnly.TryParse(gioKetThuc, out var ktVal))
        {
            kt = ktVal;
        }

        var kq = await _service.KiemTraXungDotNgayNghiAsync(giaSuId, tn, dn, bd, kt, cancellationToken);
        return Ok(new PhanHoiApi<KiemTraXungDotNgayNghiKetQuaDto>(kq));
    }

    [HttpGet("toi/giay-to")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<GiayToGiaSuDto>>>> LayDsGiayToToi(CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var ds = await _service.LayDsGiayToToiAsync(giaSuId, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<GiayToGiaSuDto>>(ds));
    }

    [HttpPost("toi/giay-to")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<GiayToGiaSuDto>>> TaiLenGiayTo(
        [FromForm] LoaiGiayTo loai,
        IFormFile? tep,
        CancellationToken cancellationToken)
    {
        if (tep is null || tep.Length == 0)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng chọn tệp giấy tờ để tải lên.", 400);
        }

        var giaSuId = User.LayNguoiDungId();
        await using var stream = tep.OpenReadStream();
        var dto = await _service.TaiLenGiayToAsync(giaSuId, loai, tep.FileName, stream, tep.Length, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<GiayToGiaSuDto>(dto));
    }

    [HttpDelete("toi/giay-to/{id:guid}")]
    [Authorize(Roles = "GIA_SU")]
    public async Task<ActionResult<PhanHoiApi<object>>> XoaGiayTo(
        Guid id,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        await _service.XoaGiayToAsync(giaSuId, id, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daXoa = true }));
    }
}

/// <summary>
/// Controller Module 2 — Xem tệp giấy tờ gia sư trong kho Private qua Signed URL có thời hạn (`/api/giay-to/{id}/url-xem`).
/// </summary>
[ApiController]
[Route("api/giay-to")]
[Authorize(Roles = "GIA_SU,QUAN_TRI")]
public sealed class GiayToController : ControllerBase
{
    private readonly IGiaSuVaMonHocService _service;
    private readonly ILuuTruTepService _luuTruTep;

    public GiayToController(
        IGiaSuVaMonHocService service,
        ILuuTruTepService luuTruTep)
    {
        _service = service;
        _luuTruTep = luuTruTep;
    }

    [HttpGet("{id:guid}/url-xem")]
    public async Task<ActionResult<PhanHoiApi<UrlXemGiayToDto>>> LayUrlXemGiayTo(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var vaiTro = User.LayVaiTro();
        var dto = await _service.TaoUrlXemGiayToAsync(userId, vaiTro, id, cancellationToken);
        return Ok(new PhanHoiApi<UrlXemGiayToDto>(dto));
    }

    [HttpGet("xem-tep")]
    [AllowAnonymous]
    public async Task<IActionResult> XemTepDaKy(
        [FromQuery] string tep,
        [FromQuery] long exp,
        [FromQuery] string sig,
        CancellationToken cancellationToken)
    {
        var ketQua = await _luuTruTep.XacThucVaTaiTepRiengTuAsync(tep, exp, sig, cancellationToken);
        if (ketQua is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                loi = new
                {
                    ma = "SIGNED_URL_KHONG_HOP_LE",
                    thongBao = "Liên kết xem tệp đã hết hạn hoặc chữ ký không hợp lệ."
                }
            });
        }

        return File(ketQua.Value.NoiDung, ketQua.Value.ContentType);
    }
}

/// <summary>
/// Controller Module 3 — Danh sách Gia sư Yêu thích của Học sinh (`/api/muc-yeu-thich`).
/// </summary>
[ApiController]
[Route("api/muc-yeu-thich")]
[Authorize(Roles = "HOC_SINH")]
public sealed class MucYeuThichController : ControllerBase
{
    private readonly IGiaSuVaMonHocService _service;

    public MucYeuThichController(IGiaSuVaMonHocService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<MucYeuThichDto>>>> LayDsMucYeuThich(CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var ds = await _service.LayDsMucYeuThichAsync(hocSinhId, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<MucYeuThichDto>>(ds));
    }

    [HttpPost("{giaSuId:guid}")]
    public async Task<ActionResult<PhanHoiApi<MucYeuThichDto>>> ThemMucYeuThich(
        Guid giaSuId,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var dto = await _service.ThemMucYeuThichAsync(hocSinhId, giaSuId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<MucYeuThichDto>(dto));
    }

    [HttpDelete("{giaSuId:guid}")]
    public async Task<ActionResult<PhanHoiApi<object>>> XoaMucYeuThich(
        Guid giaSuId,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        await _service.XoaMucYeuThichAsync(hocSinhId, giaSuId, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daXoa = true }));
    }
}
