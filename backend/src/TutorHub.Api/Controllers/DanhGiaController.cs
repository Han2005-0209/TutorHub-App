using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Đánh giá buổi học (Module 6 — `/api/danh-gia`).
/// </summary>
[ApiController]
[Route("api/danh-gia")]
[Authorize]
public sealed class DanhGiaController : ControllerBase
{
    private readonly IDanhGiaService _service;

    public DanhGiaController(IDanhGiaService service)
    {
        _service = service;
    }

    /// <summary>
    /// Học sinh gửi đánh giá 1–5 sao cho buổi học đã hoàn thành (`HOAN_THANH`).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "HOC_SINH")]
    [ProducesResponseType(typeof(PhanHoiApi<DanhGiaChiTietDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<DanhGiaChiTietDto>>> TaoDanhGia(
        [FromBody] TaoDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var kq = await _service.TaoDanhGiaAsync(hocSinhId, yeuCau, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<DanhGiaChiTietDto>(kq));
    }

    /// <summary>
    /// Học sinh chỉnh sửa đánh giá đã gửi trong vòng 48 giờ.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "HOC_SINH")]
    [ProducesResponseType(typeof(PhanHoiApi<DanhGiaChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<DanhGiaChiTietDto>>> SuaDanhGia(
        Guid id,
        [FromBody] SuaDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var kq = await _service.SuaDanhGiaAsync(hocSinhId, id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<DanhGiaChiTietDto>(kq));
    }

    /// <summary>
    /// Gia sư xem danh sách toàn bộ đánh giá học viên đã gửi cho mình.
    /// </summary>
    [HttpGet("gia-su/toi")]
    [Authorize(Roles = "GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<DanhGiaChiTietDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<DanhGiaChiTietDto>>>> LayDsDanhGiaCuaToi(
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 15,
        CancellationToken cancellationToken = default)
    {
        var giaSuId = User.LayNguoiDungId();
        var kq = await _service.LayDsDanhGiaCuaGiaSuAsync(giaSuId, trang, kichThuocTrang, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<DanhGiaChiTietDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    /// <summary>
    /// Gia sư phản hồi một đánh giá từ học viên (tối đa 1 lần).
    /// </summary>
    [HttpPost("{id:guid}/phan-hoi")]
    [Authorize(Roles = "GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<DanhGiaChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<DanhGiaChiTietDto>>> PhanHoiDanhGia(
        Guid id,
        [FromBody] PhanHoiDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var kq = await _service.PhanHoiDanhGiaAsync(giaSuId, id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<DanhGiaChiTietDto>(kq));
    }
}
