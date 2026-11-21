using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Khiếu nại 2 chiều cho Học sinh & Gia sư (`/api/khieu-nai`).
/// </summary>
[ApiController]
[Route("api/khieu-nai")]
[Authorize(Roles = "HOC_SINH,GIA_SU")]
public sealed class KhieuNaiController : ControllerBase
{
    private readonly IViVaTaiChinhService _service;

    public KhieuNaiController(IViVaTaiChinhService service)
    {
        _service = service;
    }

    [HttpGet("toi")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<KhieuNaiChiTietDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<KhieuNaiChiTietDto>>>> LayDsKhieuNaiCuaToi(
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var vaiTro = User.LayVaiTro();
        var ds = await _service.LayDsKhieuNaiCuaToiAsync(userId, vaiTro, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<KhieuNaiChiTietDto>>(ds));
    }

    [HttpPost("tep-bang-chung")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(PhanHoiApi<TepBangChungKetQuaDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<TepBangChungKetQuaDto>>> TaiLenBangChung(
        IFormFile tep,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        if (tep is null || tep.Length == 0)
        {
            return BadRequest(new
            {
                loi = new
                {
                    ma = "TEP_KHONG_HOP_LE",
                    thongBao = "Vui lòng chọn tệp ảnh hoặc PDF bằng chứng (tối đa 5 MB)."
                }
            });
        }

        await using var stream = tep.OpenReadStream();
        var kq = await _service.TaiLenBangChungKhieuNaiAsync(
            userId,
            stream,
            tep.FileName,
            tep.Length,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<TepBangChungKetQuaDto>(kq));
    }

    [HttpPost("{id:guid}/phan-hoi-gia-su")]
    [Authorize(Roles = "GIA_SU")]
    [ProducesResponseType(typeof(PhanHoiApi<KhieuNaiChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<KhieuNaiChiTietDto>>> PhanHoiKhieuNaiGiaSu(
        Guid id,
        [FromBody] PhanHoiKhieuNaiGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var kq = await _service.PhanHoiKhieuNaiGiaSuAsync(giaSuId, id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<KhieuNaiChiTietDto>(kq));
    }
}

/// <summary>
/// Controller Thông báo cá nhân (`/api/thong-bao`).
/// </summary>
[ApiController]
[Route("api/thong-bao")]
[Authorize]
public sealed class ThongBaoController : ControllerBase
{
    private readonly IViVaTaiChinhService _service;

    public ThongBaoController(IViVaTaiChinhService service)
    {
        _service = service;
    }

    [HttpGet("toi")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<ThongBaoDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<ThongBaoDto>>>> LayDsThongBaoCuaToi(
        [FromQuery] int gioiHan = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = User.LayNguoiDungId();
        var ds = await _service.LayDsThongBaoCuaToiAsync(userId, gioiHan, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<ThongBaoDto>>(ds));
    }

    [HttpPost("doc-tat-ca")]
    [ProducesResponseType(typeof(PhanHoiApi<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<object>>> DocTatCa(CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        await _service.DanhDauDaDocThongBaoAsync(userId, null, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daDoc = true }));
    }

    [HttpPost("{id:guid}/da-doc")]
    [ProducesResponseType(typeof(PhanHoiApi<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<object>>> DocMotThongBao(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        await _service.DanhDauDaDocThongBaoAsync(userId, id, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daDoc = true }));
    }
}
