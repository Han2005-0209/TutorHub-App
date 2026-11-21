using TutorHub.Application.DTOs;
using TutorHub.Domain.Enums;

namespace TutorHub.Application.Services;

/// <summary>
/// Dịch vụ Bảng điều khiển riêng theo từng vai trò (Học sinh, Gia sư, Quản trị viên)
/// và các thao tác vận hành cốt lõi của Quản trị viên.
/// </summary>
public interface IBangDieuKhienService
{
    Task<TongQuanHocSinhDto> LayTongQuanHocSinhAsync(Guid hocSinhId, CancellationToken cancellationToken = default);
    Task<TongQuanGiaSuDto> LayTongQuanGiaSuAsync(Guid giaSuId, CancellationToken cancellationToken = default);

    Task<TongQuanQuanTriDto> LayTongQuanQuanTriAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GiaSuQuanTriDto>> LayDsGiaSuQuanTriAsync(
        TrangThaiHoSoGiaSu? trangThai = null,
        CancellationToken cancellationToken = default);
    Task<GiaSuQuanTriDto> DuyetHoSoGiaSuAsync(
        Guid quanTriId,
        Guid giaSuId,
        DuyetHoSoGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);
    Task<GiayToQuanTriDto> DuyetGiayToGiaSuAsync(
        Guid quanTriId,
        Guid giayToId,
        DuyetGiayToYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NguoiDungQuanTriDto>> LayDsNguoiDungQuanTriAsync(
        VaiTro? vaiTro = null,
        string? tuKhoa = null,
        CancellationToken cancellationToken = default);
    Task<NguoiDungQuanTriDto> KhoaTaiKhoanAsync(
        Guid quanTriId,
        Guid nguoiDungId,
        CancellationToken cancellationToken = default);
    Task<NguoiDungQuanTriDto> MoKhoaTaiKhoanAsync(
        Guid nguoiDungId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonHocQuanTriDto>> LayDsMonHocQuanTriAsync(CancellationToken cancellationToken = default);
    Task<MonHocQuanTriDto> TaoMonHocAsync(TaoMonHocYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task<MonHocQuanTriDto> CapNhatMonHocAsync(
        Guid monHocId,
        CapNhatMonHocYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);
}
