using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IKiemTraSucKhoeService _kiemTraSucKhoeService;

    public HealthController(IKiemTraSucKhoeService kiemTraSucKhoeService)
    {
        _kiemTraSucKhoeService = kiemTraSucKhoeService;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<TrangThaiSucKhoeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TrangThaiSucKhoeDto>>> GetHealth(CancellationToken cancellationToken)
    {
        var dto = await _kiemTraSucKhoeService.KiemTraAsync(cancellationToken);
        return Ok(new PhanHoiApi<TrangThaiSucKhoeDto>(dto));
    }
}
