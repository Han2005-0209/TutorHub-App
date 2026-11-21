using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Enums;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Ví & Sổ cái Gia sư (`/api/vi/toi*`).
/// Chỉ phục vụ vai trò `GIA_SU` (API 1..5). Học sinh xem số dư ví qua `/api/hoc-sinh/toi/tong-quan`.
/// </summary>
[ApiController]
[Route("api/vi/toi")]
[Authorize(Roles = "GIA_SU")]
public sealed class ViController : ControllerBase
{
    private readonly IViVaTaiChinhService _service;

    public ViController(IViVaTaiChinhService service)
    {
        _service = service;
    }

    // API 1: Tổng quan 4 số dư ví gia sư
    [HttpGet]
    [ProducesResponseType(typeof(PhanHoiApi<TongQuanViGiaSuDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TongQuanViGiaSuDto>>> LayTongQuanVi(
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.LayTongQuanViGiaSuAsync(giaSuId, cancellationToken);
        return Ok(new PhanHoiApi<TongQuanViGiaSuDto>(dto));
    }

    // API 2: Danh sách sổ cái giao dịch ví gia sư (loai chỉ nhận THU_NHAP | RUT_TIEN)
    [HttpGet("giao-dich")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<GiaoDichViGiaSuDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<GiaoDichViGiaSuDto>>>> LayDsGiaoDich(
        [FromQuery] string? loai,
        [FromQuery] DateOnly? tuNgay,
        [FromQuery] DateOnly? denNgay,
        [FromQuery] string? tuKhoa,
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        var giaSuId = User.LayNguoiDungId();
        var kq = await _service.LayDsGiaoDichViGiaSuAsync(
            giaSuId, loai, tuNgay, denNgay, tuKhoa, trang, kichThuocTrang, cancellationToken);

        return Ok(new PhanHoiApi<IReadOnlyList<GiaoDichViGiaSuDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kq.KichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    // API 3: Chi tiết tra soát 1 bút toán sổ cái (chống IDOR)
    [HttpGet("giao-dich/{id:guid}")]
    [ProducesResponseType(typeof(PhanHoiApi<GiaoDichViGiaSuDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<GiaoDichViGiaSuDto>>> LayChiTietGiaoDich(
        Guid id,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.LayChiTietGiaoDichViGiaSuAsync(giaSuId, id, cancellationToken);
        return Ok(new PhanHoiApi<GiaoDichViGiaSuDto>(dto));
    }

    // API 4: Danh sách yêu cầu rút tiền của gia sư
    [HttpGet("rut-tien")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<YeuCauRutTienDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<YeuCauRutTienDto>>>> LayDsYeuCauRutTien(
        [FromQuery] TrangThaiRutTien? trangThai,
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        var giaSuId = User.LayNguoiDungId();
        var kq = await _service.LayDsYeuCauRutTienGiaSuAsync(giaSuId, trangThai, trang, kichThuocTrang, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<YeuCauRutTienDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kq.KichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    // API 5: Tạo yêu cầu rút tiền (xác nhận mật khẩu + IdempotencyKey + khóa nguyên tử)
    [HttpPost("rut-tien")]
    [ProducesResponseType(typeof(PhanHoiApi<KetQuaTaoYeuCauRutTienDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<KetQuaTaoYeuCauRutTienDto>>> TaoYeuCauRutTien(
        [FromBody] TaoYeuCauRutTienDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var dto = await _service.TaoYeuCauRutTienAsync(giaSuId, yeuCau, ip, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<KetQuaTaoYeuCauRutTienDto>(dto));
    }

    // API 5b: Gia sư chủ động hủy yêu cầu rút tiền đang chờ xử lý (mở khóa số dư khả dụng nguyên tử)
    [HttpPost("rut-tien/{id:guid}/huy")]
    [ProducesResponseType(typeof(PhanHoiApi<KetQuaTaoYeuCauRutTienDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<KetQuaTaoYeuCauRutTienDto>>> HuyYeuCauRutTien(
        Guid id,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.HuyYeuCauRutTienGiaSuAsync(giaSuId, id, cancellationToken);
        return Ok(new PhanHoiApi<KetQuaTaoYeuCauRutTienDto>(dto));
    }
}
