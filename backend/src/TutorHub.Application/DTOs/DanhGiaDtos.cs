namespace TutorHub.Application.DTOs;

public sealed record TaoDanhGiaYeuCauDto(
    Guid DonDatLichId,
    int Diem,
    string BinhLuan);

public sealed record SuaDanhGiaYeuCauDto(
    int Diem,
    string BinhLuan);

public sealed record PhanHoiDanhGiaYeuCauDto(
    string PhanHoi);

public sealed record KiemDuyetDanhGiaYeuCauDto(
    bool AnDanhGia,
    string? LyDoAn = null);

public sealed record DanhGiaDonTomTatDto(
    Guid Id,
    int Diem,
    string BinhLuan,
    string? PhanHoiGiaSu,
    bool BiAn,
    string? LyDoAn,
    DateTime TaoLuc,
    bool CoTheSua);

public sealed record DanhGiaChiTietDto(
    Guid Id,
    Guid DonDatLichId,
    string MaDon,
    Guid HocSinhId,
    string TenHocSinh,
    string? AnhDaiDienHocSinhUrl,
    Guid GiaSuId,
    string TenGiaSu,
    string TenMonHoc,
    DateTime BuoiHocBatDauLuc,
    int Diem,
    string BinhLuan,
    string? PhanHoiGiaSu,
    bool BiAn,
    string? LyDoAn,
    string? TenNguoiKiemDuyet,
    string? EmailNguoiKiemDuyet,
    DateTime TaoLuc,
    bool CoTheSua,
    bool CoThePhanHoi);
