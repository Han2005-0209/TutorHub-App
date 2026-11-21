using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Ví Học sinh & Nạp tiền VietQR tự động (`/api/vi/hoc-sinh*`).
/// Chỉ phục vụ vai trò `HOC_SINH`.
/// </summary>
[ApiController]
[Route("api/vi/hoc-sinh")]
[Authorize(Roles = "HOC_SINH")]
public sealed class ViHocSinhController : ControllerBase
{
    private readonly IViVaTaiChinhService _service;

    public ViHocSinhController(IViVaTaiChinhService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PhanHoiApi<TongQuanViHocSinhDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TongQuanViHocSinhDto>>> LayTongQuanVi(
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var dto = await _service.LayTongQuanViHocSinhAsync(hocSinhId, cancellationToken);
        return Ok(new PhanHoiApi<TongQuanViHocSinhDto>(dto));
    }

    [HttpGet("giao-dich")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<GiaoDichViHocSinhDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<GiaoDichViHocSinhDto>>>> LayDsGiaoDich(
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        var hocSinhId = User.LayNguoiDungId();
        var kq = await _service.LayDsGiaoDichViHocSinhAsync(hocSinhId, trang, kichThuocTrang, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<GiaoDichViHocSinhDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kq.KichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    [HttpPost("nap-tien")]
    [ProducesResponseType(typeof(PhanHoiApi<YeuCauNapTienDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<YeuCauNapTienDto>>> TaoYeuCauNapTien(
        [FromBody] TaoYeuCauNapTienDto yeuCau,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var origin = Request.Headers.Origin.ToString();
        var dto = await _service.TaoYeuCauNapTienVietQrAsync(hocSinhId, yeuCau, origin, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<YeuCauNapTienDto>(dto));
    }

    [HttpGet("nap-tien/{id:guid}")]
    [ProducesResponseType(typeof(PhanHoiApi<YeuCauNapTienDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<YeuCauNapTienDto>>> KiemTraTrangThaiNapTien(
        Guid id,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var dto = await _service.KiemTraVaDongBoNapTienAsync(hocSinhId, id, cancellationToken);
        return Ok(new PhanHoiApi<YeuCauNapTienDto>(dto));
    }

    [HttpPost("nap-tien/{id:guid}/huy")]
    [ProducesResponseType(typeof(PhanHoiApi<YeuCauNapTienDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<YeuCauNapTienDto>>> HuyYeuCauNapTien(
        Guid id,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var dto = await _service.HuyYeuCauNapTienAsync(hocSinhId, id, cancellationToken);
        return Ok(new PhanHoiApi<YeuCauNapTienDto>(dto));
    }
}

/// <summary>
/// Webhook nhận thông báo chuyển khoản VietQR thời gian thực từ PayOS (`POST /api/thanh-toan/payos/webhook`).
/// Xác thực chữ ký HMAC-SHA256 bằng ChecksumKey trước khi cộng tiền vào ví.
/// </summary>
[ApiController]
[Route("api/thanh-toan/payos")]
public sealed class PayOsWebhookController : ControllerBase
{
    private readonly IViVaTaiChinhService _service;

    public PayOsWebhookController(IViVaTaiChinhService service)
    {
        _service = service;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> NhanWebhookPayOs(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var ok = await _service.XuLyWebhookPayOsAsync(rawBody, cancellationToken);
        if (!ok)
        {
            return BadRequest(new { success = false, code = "01", desc = "Invalid payload" });
        }

        return Ok(new { success = true, code = "00", desc = "Ok" });
    }
}
