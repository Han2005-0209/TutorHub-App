namespace TutorHub.Application.DTOs;

public sealed record ViPhamNoiDungDto(
    string NhomQuyTac,
    string MaQuyTac,
    string Truong,
    string DoanViPham,
    string LyDo);

public sealed record KetQuaKiemDuyetNoiDungDto(
    bool HopLe,
    IReadOnlyList<ViPhamNoiDungDto> DsViPham);

public sealed record KiemTraVanBanYeuCauDto(
    string NoiDung,
    string? TenTruong = null,
    bool KiemTraLienHeNgoaiLuong = true);
