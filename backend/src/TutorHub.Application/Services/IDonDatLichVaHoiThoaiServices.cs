using TutorHub.Application.DTOs;
using TutorHub.Domain.Enums;

namespace TutorHub.Application.Services;

public interface IDonDatLichService
{
    Task<DonDatLichChiTietDto> TaoDonDatLichAsync(
        Guid hocSinhId,
        TaoDonDatLichYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DonDatLichChiTietDto>> LayDsDonCuaToiAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        CancellationToken cancellationToken = default);

    Task<DonDatLichChiTietDto> XacNhanDonAsync(
        Guid giaSuId,
        Guid donId,
        CancellationToken cancellationToken = default);

    Task<DonDatLichChiTietDto> TuChoiDonAsync(
        Guid giaSuId,
        Guid donId,
        string lyDo,
        CancellationToken cancellationToken = default);

    Task<DonDatLichChiTietDto> HuyDonAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        Guid donId,
        string lyDo,
        CancellationToken cancellationToken = default);
}

public interface IHoiThoaiService
{
    Task<HoiThoaiChiTietDto> MoHoacTaoHoiThoaiAsync(
        Guid hocSinhId,
        Guid giaSuId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HoiThoaiTomTatDto>> LayDsHoiThoaiCuaToiAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        CancellationToken cancellationToken = default);

    Task<HoiThoaiChiTietDto> LayChiTietHoiThoaiAsync(
        Guid nguoiDungId,
        Guid hoiThoaiId,
        CancellationToken cancellationToken = default);

    Task<TinNhanDto> GuiTinNhanAsync(
        Guid nguoiGuiId,
        Guid hoiThoaiId,
        string noiDung,
        CancellationToken cancellationToken = default);
}
