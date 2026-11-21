namespace TutorHub.Application.DTOs;

/// <summary>
/// DTO trả về cho endpoint kiểm tra sức khỏe hệ thống GET /api/health.
/// </summary>
public sealed record TrangThaiSucKhoeDto(
    string TrangThai,
    string DichVu,
    string Csdl,
    string PhienBanRuntime,
    DateTime ThoiGianUtc);
