using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TutorHub.Api.Common;
using TutorHub.Api.Contracts;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Enums;

namespace TutorHub.Api.Controllers;

/// <summary>
/// Controller Cổng Học sinh — Bảng điều khiển tổng quan học tập (`/api/hoc-sinh/toi/tong-quan`).
/// </summary>
[ApiController]
[Route("api/hoc-sinh")]
[Authorize(Roles = "HOC_SINH")]
public sealed class HocSinhPortalController : ControllerBase
{
    private readonly IBangDieuKhienService _service;

    public HocSinhPortalController(IBangDieuKhienService service)
    {
        _service = service;
    }

    [HttpGet("toi/tong-quan")]
    [ProducesResponseType(typeof(PhanHoiApi<TongQuanHocSinhDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TongQuanHocSinhDto>>> LayTongQuanHocSinh(CancellationToken cancellationToken)
    {
        var hocSinhId = User.LayNguoiDungId();
        var dto = await _service.LayTongQuanHocSinhAsync(hocSinhId, cancellationToken);
        return Ok(new PhanHoiApi<TongQuanHocSinhDto>(dto));
    }
}

/// <summary>
/// Controller Cổng Gia sư — Tổng quan dạy học & thu nhập (`/api/gia-su/toi/tong-quan`).
/// </summary>
[ApiController]
[Route("api/gia-su/toi/tong-quan")]
[Authorize(Roles = "GIA_SU")]
public sealed class GiaSuPortalController : ControllerBase
{
    private readonly IBangDieuKhienService _service;

    public GiaSuPortalController(IBangDieuKhienService service)
    {
        _service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PhanHoiApi<TongQuanGiaSuDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TongQuanGiaSuDto>>> LayTongQuanGiaSu(CancellationToken cancellationToken)
    {
        var giaSuId = User.LayNguoiDungId();
        var dto = await _service.LayTongQuanGiaSuAsync(giaSuId, cancellationToken);
        return Ok(new PhanHoiApi<TongQuanGiaSuDto>(dto));
    }
}

/// <summary>
/// Controller Cổng Quản trị viên — Tổng quan hệ thống, duyệt hồ sơ/giấy tờ gia sư, quản lý người dùng & môn học (`/api/quan-tri/*`).
/// </summary>
[ApiController]
[Route("api/quan-tri")]
[Authorize(Roles = "QUAN_TRI")]
public sealed class QuanTriController : ControllerBase
{
    private readonly IBangDieuKhienService _service;
    private readonly IViVaTaiChinhService _viVaTaiChinh;

    public QuanTriController(
        IBangDieuKhienService service,
        IViVaTaiChinhService viVaTaiChinh)
    {
        _service = service;
        _viVaTaiChinh = viVaTaiChinh;
    }

    [HttpGet("tong-quan")]
    [ProducesResponseType(typeof(PhanHoiApi<TongQuanQuanTriDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<TongQuanQuanTriDto>>> LayTongQuan(CancellationToken cancellationToken)
    {
        var dto = await _service.LayTongQuanQuanTriAsync(cancellationToken);
        return Ok(new PhanHoiApi<TongQuanQuanTriDto>(dto));
    }

    [HttpGet("gia-su")]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<GiaSuQuanTriDto>>>> LayDsGiaSu(
        [FromQuery] TrangThaiHoSoGiaSu? trangThai,
        CancellationToken cancellationToken)
    {
        var ds = await _service.LayDsGiaSuQuanTriAsync(trangThai, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<GiaSuQuanTriDto>>(ds));
    }

    [HttpPost("gia-su/{id:guid}/duyet")]
    public async Task<ActionResult<PhanHoiApi<GiaSuQuanTriDto>>> DuyetHoSoGiaSu(
        Guid id,
        [FromBody] DuyetHoSoGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var quanTriId = User.LayNguoiDungId();
        var dto = await _service.DuyetHoSoGiaSuAsync(quanTriId, id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<GiaSuQuanTriDto>(dto));
    }

    [HttpPost("giay-to/{id:guid}/duyet")]
    public async Task<ActionResult<PhanHoiApi<GiayToQuanTriDto>>> DuyetGiayToGiaSu(
        Guid id,
        [FromBody] DuyetGiayToYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var quanTriId = User.LayNguoiDungId();
        var dto = await _service.DuyetGiayToGiaSuAsync(quanTriId, id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<GiayToQuanTriDto>(dto));
    }

    [HttpGet("nguoi-dung")]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<NguoiDungQuanTriDto>>>> LayDsNguoiDung(
        [FromQuery] VaiTro? vaiTro,
        [FromQuery] string? tuKhoa,
        CancellationToken cancellationToken)
    {
        var ds = await _service.LayDsNguoiDungQuanTriAsync(vaiTro, tuKhoa, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<NguoiDungQuanTriDto>>(ds));
    }

    [HttpPost("nguoi-dung/{id:guid}/khoa")]
    public async Task<ActionResult<PhanHoiApi<NguoiDungQuanTriDto>>> KhoaTaiKhoan(
        Guid id,
        CancellationToken cancellationToken)
    {
        var quanTriId = User.LayNguoiDungId();
        var dto = await _service.KhoaTaiKhoanAsync(quanTriId, id, cancellationToken);
        return Ok(new PhanHoiApi<NguoiDungQuanTriDto>(dto));
    }

    [HttpPost("nguoi-dung/{id:guid}/mo-khoa")]
    public async Task<ActionResult<PhanHoiApi<NguoiDungQuanTriDto>>> MoKhoaTaiKhoan(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dto = await _service.MoKhoaTaiKhoanAsync(id, cancellationToken);
        return Ok(new PhanHoiApi<NguoiDungQuanTriDto>(dto));
    }

    [HttpGet("mon-hoc")]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<MonHocQuanTriDto>>>> LayDsMonHoc(CancellationToken cancellationToken)
    {
        var ds = await _service.LayDsMonHocQuanTriAsync(cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<MonHocQuanTriDto>>(ds));
    }

    [HttpPost("mon-hoc")]
    public async Task<ActionResult<PhanHoiApi<MonHocQuanTriDto>>> TaoMonHoc(
        [FromBody] TaoMonHocYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var dto = await _service.TaoMonHocAsync(yeuCau, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new PhanHoiApi<MonHocQuanTriDto>(dto));
    }

    [HttpPatch("mon-hoc/{id:guid}")]
    public async Task<ActionResult<PhanHoiApi<MonHocQuanTriDto>>> CapNhatMonHoc(
        Guid id,
        [FromBody] CapNhatMonHocYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var dto = await _service.CapNhatMonHocAsync(id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<MonHocQuanTriDto>(dto));
    }

    // API 10: Danh sách yêu cầu rút tiền gia sư
    [HttpGet("rut-tien")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<YeuCauRutTienDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<YeuCauRutTienDto>>>> LayDsYeuCauRutTien(
        [FromQuery] TrangThaiRutTien? trangThai,
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        var kq = await _viVaTaiChinh.LayDsYeuCauRutTienQuanTriAsync(trangThai, trang, kichThuocTrang, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<YeuCauRutTienDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kq.KichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    // API 11: Duyệt chi trả hoặc từ chối yêu cầu rút tiền
    [HttpPost("rut-tien/{id:guid}/xu-ly")]
    [ProducesResponseType(typeof(PhanHoiApi<YeuCauRutTienDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<YeuCauRutTienDto>>> XuLyYeuCauRutTien(
        Guid id,
        [FromBody] XuLyYeuCauRutTienDto yeuCau,
        CancellationToken cancellationToken)
    {
        var quanTriId = User.LayNguoiDungId();
        var dto = await _viVaTaiChinh.XuLyYeuCauRutTienAsync(quanTriId, id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<YeuCauRutTienDto>(dto));
    }

    // API 12: Danh sách khiếu nại
    [HttpGet("khieu-nai")]
    [ProducesResponseType(typeof(PhanHoiApi<IReadOnlyList<KhieuNaiChiTietDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<IReadOnlyList<KhieuNaiChiTietDto>>>> LayDsKhieuNai(
        [FromQuery] TrangThaiKhieuNai? trangThai,
        [FromQuery] int trang = 1,
        [FromQuery] int kichThuocTrang = 20,
        CancellationToken cancellationToken = default)
    {
        var kq = await _viVaTaiChinh.LayDsKhieuNaiQuanTriAsync(trangThai, trang, kichThuocTrang, cancellationToken);
        return Ok(new PhanHoiApi<IReadOnlyList<KhieuNaiChiTietDto>>(
            kq.DuLieu,
            new MetaPhanTrangDto(kq.Trang, kq.KichThuocTrang, kq.Tong, kq.TongTrang)));
    }

    // API 13: Giải quyết khiếu nại (soTienHoan = 0 -> TU_CHOI; > 0 -> DA_GIAI_QUYET)
    [HttpPost("khieu-nai/{id:guid}/giai-quyet")]
    [ProducesResponseType(typeof(PhanHoiApi<KhieuNaiChiTietDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PhanHoiApi<KhieuNaiChiTietDto>>> GiaiQuyetKhieuNai(
        Guid id,
        [FromBody] GiaiQuyetKhieuNaiYeuCauDto yeuCau,
        CancellationToken cancellationToken)
    {
        var quanTriId = User.LayNguoiDungId();
        var dto = await _viVaTaiChinh.GiaiQuyetKhieuNaiAsync(quanTriId, id, yeuCau, cancellationToken);
        return Ok(new PhanHoiApi<KhieuNaiChiTietDto>(dto));
    }
}
