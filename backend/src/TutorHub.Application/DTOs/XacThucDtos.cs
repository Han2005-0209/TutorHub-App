using TutorHub.Domain.Enums;

namespace TutorHub.Application.DTOs;

public sealed record DangKyYeuCauDto(
    string Email,
    string MatKhau,
    string HoTen,
    VaiTro VaiTro = VaiTro.HOC_SINH,
    string? SoDienThoai = null,
    string? KhuVuc = null);

public sealed record DangNhapYeuCauDto(
    string Email,
    string MatKhau);

public sealed record LamMoiTokenYeuCauDto(
    string RefreshToken);

public sealed record XacThucEmailYeuCauDto(
    string Token);

public sealed record QuenMatKhauYeuCauDto(
    string Email);

public sealed record DatLaiMatKhauYeuCauDto(
    string Token,
    string MatKhauMoi);

public sealed record CapNhatHoSoNguoiDungDto(
    string HoTen,
    string? SoDienThoai,
    string? KhuVuc);

public sealed record DoiMatKhauYeuCauDto(
    string MatKhauCu,
    string MatKhauMoi);

public sealed record NguoiDungHienTaiDto(
    Guid Id,
    string Email,
    string HoTen,
    VaiTro VaiTro,
    string? SoDienThoai,
    string? AnhDaiDienUrl,
    string? KhuVuc,
    TrangThaiNguoiDung TrangThai,
    bool DaXacThucEmail,
    DateTime? XacThucEmailLuc,
    int? SoDuVi,
    DateTime TaoLuc);

public sealed record KetQuaDangNhapDto(
    string AccessToken,
    string RefreshToken,
    DateTime HetHanLuc,
    NguoiDungHienTaiDto NguoiDung,
    string? TokenXacThucEmailDemo = null);
