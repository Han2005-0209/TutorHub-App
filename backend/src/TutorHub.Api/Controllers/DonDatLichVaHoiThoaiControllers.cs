using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Đặt lịch học (`/api/don-dat-lich`).
/// </summary>
[ApiController]
[Route("api/don-dat-lich")]
[Authorize]
public sealed class DonDatLichController : ControllerBase
{
    private readonly IDonDatLichService _service;
    private readonly IViVaTaiChinhService _viVaTaiChinh;

    public DonDatLichController(
        IDonDatLichService service,
        IViVaTaiChinhService viVaTaiChinh)
    {
        _service = service;
        _viVaTaiChinh = viVaTaiChinh;
    }

    [HttpPost]
    [Authorize(Roles = "HOC_SINH")]
    [ProducesResponseType(typeof(PhanHoiApi<DonDatLichChiTietDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<DonDatLichChiTietDto>>> TaoDonDatLich(
        [FromBody] TaoDonDatLichYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var kq = await _service.TaoDonDatLichAsync(hocSinhId, yeuCau, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<DonDatLichChiTietDto>(kq));
    }

    [HttpGet("toi")]
    [Authorize(Roles = "HOC_SINH,GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<DonDatLichChiTietDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<DonDatLichChiTietDto>>>> LayDsDonCuaToi(
        CancellationToken cancellationToken)
    {
        var nguoiDungId = User.LayNguoiDungId();
        var vaiTro = User.LayVaiTro();
        var ds = await _service.LayDsDonCuaToiAsync(nguoiDungId, vaiTro, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<DonDatLichChiTietDto>>(ds));
    }

    [HttpPost("{id:guid}/xac-nhan")]
    [Authorize(Roles = "GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<DonDatLichChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<DonDatLichChiTietDto>>> XacNhanDon(
        Guid id,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var kq = await _service.XacNhanDonAsync(giaSuId, id, cancellationToken);
        return Ok(new PhanHoiApi<DonDatLichChiTietDto>(kq));
    }

    [HttpPost("{id:guid}/tu-choi")]
    [Authorize(Roles = "GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<DonDatLichChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<DonDatLichChiTietDto>>> TuChoiDon(
        Guid id,
        [FromBody] LyDoDonDatLichYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var kq = await _service.TuChoiDonAsync(giaSuId, id, yeuCau.LyDo, cancellationToken);
        return Ok(new PhanHoiApi<DonDatLichChiTietDto>(kq));
    }

    [HttpPost("{id:guid}/huy")]
    [Authorize(Roles = "HOC_SINH,GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<DonDatLichChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<DonDatLichChiTietDto>>> HuyDon(
        Guid id,
        [FromBody] LyDoDonDatLichYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var nguoiDungId = User.LayNguoiDungId();
        var vaiTro = User.LayVaiTro();
        var kq = await _service.HuyDonAsync(nguoiDungId, vaiTro, id, yeuCau.LyDo, cancellationToken);
        return Ok(new PhanHoiApi<DonDatLichChiTietDto>(kq));
    }

    // API 7: Thanh toán đơn đặt lịch vào Escrow (chỉ HOC_SINH chủ đơn)
    [HttpPost("{id:guid}/thanh-toan")]
    [Authorize(Roles = "HOC_SINH")]
    [ProducesResponseType(typeof(PhanHoiApi<ThanhToanKetQuaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<ThanhToanKetQuaDto>>> ThanhToanDon(
        Guid id,
        [FromBody] ThanhToanDonYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var kq = await _viVaTaiChinh.ThanhToanDonAsync(hocSinhId, id, yeuCau, ip, cancellationToken);
        return Ok(new PhanHoiApi<ThanhToanKetQuaDto>(kq));
    }

    // API 8: Học sinh xác nhận hoàn thành buổi học sớm -> giải ngân ngay cho gia sư
    [HttpPost("{id:guid}/hoan-thanh")]
    [Authorize(Roles = "HOC_SINH")]
    [ProducesResponseType(typeof(PhanHoiApi<DonDatLichChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<DonDatLichChiTietDto>>> XacNhanHoanThanhSom(
        Guid id,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var kq = await _viVaTaiChinh.XacNhanHoanThanhSomAsync(hocSinhId, id, cancellationToken);
        return Ok(new PhanHoiApi<DonDatLichChiTietDto>(kq));
    }

    // API 9: Học sinh mở khiếu nại tài chính (chỉ HOC_SINH chủ đơn, DA_XAC_NHAN có DANG_GIU trong cửa sổ 24h)
    [HttpPost("{id:guid}/khieu-nai")]
    [Authorize(Roles = "HOC_SINH")]
    [ProducesResponseType(typeof(PhanHoiApi<KhieuNaiChiTietDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<KhieuNaiChiTietDto>>> MoKhieuNai(
        Guid id,
        [FromBody] MoKhieuNaiYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var kq = await _viVaTaiChinh.MoKhieuNaiAsync(hocSinhId, id, yeuCau, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<KhieuNaiChiTietDto>(kq));
    }

    // Phòng học trực tuyến Mức 1: Thông tin phòng + điểm danh giờ máy chủ + heartbeat
    [HttpGet("{id:guid}/phong-hoc")]
    [Authorize(Roles = "HOC_SINH,GIA_SU,QUAN_TRI")]
    [ProducesResponseType(typeof(PhanHoiApi<ThongTinPhongHocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<ThongTinPhongHocDto>>> LayThongTinPhongHoc(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var vaiTro = User.LayVaiTro();
        var kq = await _viVaTaiChinh.LayThongTinPhongHocAsync(userId, vaiTro, id, null, cancellationToken);
        return Ok(new PhanHoiApi<ThongTinPhongHocDto>(kq));
    }

    [HttpPost("{id:guid}/phong-hoc/vao")]
    [Authorize(Roles = "HOC_SINH,GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<PhienPhongHocKetQuaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<PhienPhongHocKetQuaDto>>> VaoPhongHoc(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var vaiTro = User.LayVaiTro();
        var kq = await _viVaTaiChinh.VaoPhongHocAsync(userId, vaiTro, id, null, cancellationToken);
        return Ok(new PhanHoiApi<PhienPhongHocKetQuaDto>(kq));
    }

    [HttpPost("{id:guid}/phong-hoc/nhip")]
    [Authorize(Roles = "HOC_SINH,GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<TomTatNhatKyPhongHocDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TomTatNhatKyPhongHocDto>>> CapNhatNhipPhongHoc(
        Guid id,
        [FromBody] NhipPhongHocYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var kq = await _viVaTaiChinh.CapNhatNhipPhongHocAsync(userId, id, yeuCau, null, cancellationToken);
        return Ok(new PhanHoiApi<TomTatNhatKyPhongHocDto>(kq));
    }
}

/// <summary>
/// Controller Hội thoại & Nhắn tin giữa Học sinh và Gia sư (`/api/hoi-thoai`).
/// </summary>
[ApiController]
[Route("api/hoi-thoai")]
[Authorize(Roles = "HOC_SINH,GIA_SU")]
public sealed class HoiThoaiController : ControllerBase
{
    private readonly IHoiThoaiService _service;

    public HoiThoaiController(IHoiThoaiService service)
    {
        _service = service;
    }

    [HttpPost]
    [Authorize(Roles = "HOC_SINH")]
    [ProducesResponseType(typeof(PhanHoiApi<HoiThoaiChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<HoiThoaiChiTietDto>>> MoHoacTaoHoiThoai(
        [FromBody] MoHoiThoaiYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var kq = await _service.MoHoacTaoHoiThoaiAsync(hocSinhId, yeuCau.GiaSuId, cancellationToken);
        return Ok(new PhanHoiApi<HoiThoaiChiTietDto>(kq));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<HoiThoaiTomTatDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<HoiThoaiTomTatDto>>>> LayDsHoiThoai(
        CancellationToken cancellationToken)
    {
        var nguoiDungId = User.LayNguoiDungId();
        var vaiTro = User.LayVaiTro();
        var ds = await _service.LayDsHoiThoaiCuaToiAsync(nguoiDungId, vaiTro, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<HoiThoaiTomTatDto>>(ds));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PhanHoiApi<HoiThoaiChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<HoiThoaiChiTietDto>>> LayChiTietHoiThoai(
        Guid id,
        CancellationToken cancellationToken)
    {
        var nguoiDungId = User.LayNguoiDungId();
        var kq = await _service.LayChiTietHoiThoaiAsync(nguoiDungId, id, cancellationToken);
        return Ok(new PhanHoiApi<HoiThoaiChiTietDto>(kq));
    }

    [HttpPost("{id:guid}/tin-nhan")]
    [ProducesResponseType(typeof(PhanHoiApi<TinNhanDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<TinNhanDto>>> GuiTinNhan(
        Guid id,
        [FromBody] GuiTinNhanYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var nguoiGuiId = User.LayNguoiDungId();
        var kq = await _service.GuiTinNhanAsync(nguoiGuiId, id, yeuCau.NoiDung, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<TinNhanDto>(kq));
    }
}
