using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Module 1 — Xác thực (`/api/xac-thuc/*`).
/// Không chứa logic nghiệp vụ, chỉ uỷ quyền sang `IXacThucService`.
/// </summary>
[ApiController]
[Route("api/xac-thuc")]
public sealed class XacThucController : ControllerBase
{
    private readonly IXacThucService _xacThucService;

    public XacThucController(IXacThucService xacThucService)
    {
        _xacThucService = xacThucService;
    }

    [HttpPost("dang-ky")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<KetQuaDangNhapDto>), StatusCodes.Status201Created)]
    public async Task<ActionResult<PhanHoiApi<KetQuaDangNhapDto>>> DangKy(
        [FromBody] DangKyYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var ketQua = await _xacThucService.DangKyAsync(yeuCau, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<KetQuaDangNhapDto>(ketQua));
    }

    [HttpPost("dang-nhap")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<KetQuaDangNhapDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<KetQuaDangNhapDto>>> DangNhap(
        [FromBody] DangNhapYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var ketQua = await _xacThucService.DangNhapAsync(yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<KetQuaDangNhapDto>(ketQua));
    }

    [HttpPost("lam-moi")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<KetQuaDangNhapDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<KetQuaDangNhapDto>>> LamMoi(
        [FromBody] LamMoiTokenYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var ketQua = await _xacThucService.LamMoiTokenAsync(yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<KetQuaDangNhapDto>(ketQua));
    }

    [HttpPost("dang-xuat")]
    [Authorize]
    public async Task<ActionResult<PhanHoiApi<object>>> DangXuat(CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        await _xacThucService.DangXuatAsync(userId, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daDangXuat = true }));
    }

    [HttpPost("xac-thuc-email")]
    [Authorize]
    public async Task<ActionResult<PhanHoiApi<NguoiDungHienTaiDto>>> XacThucEmail(
        [FromBody] XacThucEmailYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var dto = await _xacThucService.XacThucEmailAsync(userId, yeuCau.Token, cancellationToken);
        return Ok(new PhanHoiApi<NguoiDungHienTaiDto>(dto));
    }

    [HttpPost("gui-lai-xac-thuc")]
    [Authorize]
    public async Task<ActionResult<PhanHoiApi<KetQuaGuiEmailOtpDto>>> GuiLaiXacThuc(CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var kq = await _xacThucService.GuiLaiXacThucEmailAsync(userId, cancellationToken);
        return Ok(new PhanHoiApi<KetQuaGuiEmailOtpDto>(kq));
    }

    [HttpPost("quen-mat-khau")]
    [AllowAnonymous]
    public async Task<ActionResult<PhanHoiApi<KetQuaGuiEmailOtpDto>>> QuenMatKhau(
        [FromBody] QuenMatKhauYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var kq = await _xacThucService.QuenMatKhauAsync(yeuCau.Email, cancellationToken);
        return Ok(new PhanHoiApi<KetQuaGuiEmailOtpDto>(kq));
    }

    [HttpPost("dat-lai-mat-khau")]
    [AllowAnonymous]
    public async Task<ActionResult<PhanHoiApi<object>>> DatLaiMatKhau(
        [FromBody] DatLaiMatKhauYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        await _xacThucService.DatLaiMatKhauAsync(yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daDatLai = true }));
    }
}

/// <summary>
/// Controller Module 1 — Hồ sơ cá nhân người dùng đang đăng nhập (`/api/nguoi-dung/toi`).
/// </summary>
[ApiController]
[Route("api/nguoi-dung/toi")]
[Authorize]
public sealed class NguoiDungController : ControllerBase
{
    private readonly IXacThucService _xacThucService;
    private readonly ILuuTruTepService _luuTruTepService;

    public NguoiDungController(IXacThucService xacThucService, ILuuTruTepService luuTruTepService)
    {
        _xacThucService = xacThucService;
        _luuTruTepService = luuTruTepService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PhanHoiApi<NguoiDungHienTaiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<NguoiDungHienTaiDto>>> LayThongTinToi(CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var dto = await _xacThucService.LayThongTinToiAsync(userId, cancellationToken);
        return Ok(new PhanHoiApi<NguoiDungHienTaiDto>(dto));
    }

    [HttpPatch]
    [ProducesResponseType(typeof(PhanHoiApi<NguoiDungHienTaiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<NguoiDungHienTaiDto>>> CapNhatHoSoToi(
        [FromBody] CapNhatHoSoNguoiDungDto yeuCau,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        var dto = await _xacThucService.CapNhatHoSoToiAsync(userId, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<NguoiDungHienTaiDto>(dto));
    }

    [HttpPost("anh-dai-dien")]
    [ProducesResponseType(typeof(PhanHoiApi<NguoiDungHienTaiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<NguoiDungHienTaiDto>>> TaiLenAnhDaiDien(
        IFormFile? tep,
        CancellationToken cancellationToken)
    {
        if (tep is null || tep.Length == 0)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Vui lòng chọn tệp ảnh đại diện.", 400);
        }

        if (tep.Length > 2 * 1024 * 1024)
        {
            throw new LoiUngDungException("TEP_QUA_LON", "Ảnh đại diện không được vượt quá 2MB.", 413);
        }

        var ext = Path.GetExtension(tep.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            throw new LoiUngDungException("LOAI_TEP_KHONG_HOP_LE", "Chỉ chấp nhận tệp ảnh JPG, PNG hoặc WebP.", 415);
        }

        var userId = User.LayNguoiDungId();
        await using var stream = tep.OpenReadStream();
        var duongDan = await _luuTruTepService.LuuTepAsync(
            stream,
            tep.FileName,
            $"anh-dai-dien/{userId:N}",
            laKhoPrivate: false,
            cancellationToken);

        var dto = await _xacThucService.CapNhatAnhDaiDienAsync(userId, duongDan, cancellationToken);
        return Ok(new PhanHoiApi<NguoiDungHienTaiDto>(dto));
    }

    [HttpPost("doi-mat-khau")]
    public async Task<ActionResult<PhanHoiApi<object>>> DoiMatKhau(
        [FromBody] DoiMatKhauYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        await _xacThucService.DoiMatKhauAsync(userId, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daDoiMatKhau = true }));
    }

    [HttpDelete]
    public async Task<ActionResult<PhanHoiApi<object>>> VoHieuHoaTaiKhoan(CancellationToken cancellationToken)
    {
        var userId = User.LayNguoiDungId();
        await _xacThucService.VoHieuHoaTaiKhoanAsync(userId, cancellationToken);
        return Ok(new PhanHoiApi<object>(new { daVoHieuHoa = true }));
    }
}
