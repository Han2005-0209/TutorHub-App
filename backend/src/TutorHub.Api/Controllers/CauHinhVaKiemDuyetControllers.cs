using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller cấu hình công khai về hình thức học (Học Trực tuyến vs. Học Trực tiếp tại nhà).
/// </summary>
[ApiController]
[Route("api/cau-hinh")]
public sealed class CauHinhController : ControllerBase
{
    private readonly IGiaSuVaMonHocService _service;

    public CauHinhController(IGiaSuVaMonHocService service)
    {
        _service = service;
    }

    [HttpGet("hinh-thuc-hoc")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<ThongTinHinhThucHocDto>>), StatusCodes.Status200OK)]
    public ActionResult<PhanHoiApi<IReadOnlyList<ThongTinHinhThucHocDto>>> LayThongTinHinhThucHoc()
    {
        var ds = _service.LayThongTinHinhThucHoc();
        return Ok(new PhanHoiApi<IReadOnlyList<ThongTinHinhThucHocDto>>(ds));
    }
}

/// <summary>
/// Controller kiểm duyệt nội dung hiển thị web & nội dung người dùng nhập.
/// </summary>
[ApiController]
[Route("api/kiem-duyet")]
public sealed class KiemDuyetNoiDungController : ControllerBase
{
    private readonly IKiemDuyetNoiDungService _kiemDuyet;

    public KiemDuyetNoiDungController(IKiemDuyetNoiDungService kiemDuyet)
    {
        _kiemDuyet = kiemDuyet;
    }

    [HttpPost("kiem-tra-noi-dung")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<KetQuaKiemDuyetNoiDungDto>), StatusCodes.Status200OK)]
    public ActionResult<PhanHoiApi<KetQuaKiemDuyetNoiDungDto>> KiemTraNoiDungNguoiDung(
        [FromBody] KiemTraVanBanYeuCauDto yeuCau)
    {
        var ketQua = _kiemDuyet.KiemTraNoiDungNguoiDung(
            yeuCau.TenTruong ?? "noiDung",
            yeuCau.NoiDung,
            yeuCau.KiemTraLienHeNgoaiLuong);

        return Ok(new PhanHoiApi<KetQuaKiemDuyetNoiDungDto>(ketQua));
    }

    [HttpPost("kiem-tra-hien-thi-web")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PhanHoiApi<KetQuaKiemDuyetNoiDungDto>), StatusCodes.Status200OK)]
    public ActionResult<PhanHoiApi<KetQuaKiemDuyetNoiDungDto>> KiemTraVanBanHienThiWeb(
        [FromBody] KiemTraVanBanYeuCauDto yeuCau)
    {
        var ketQua = _kiemDuyet.KiemTraVanBanHienThiWeb(yeuCau.NoiDung, yeuCau.TenTruong ?? "web");
        return Ok(new PhanHoiApi<KetQuaKiemDuyetNoiDungDto>(ketQua));
    }
}
