using TutorHub.Application.DTOs;

namespace TutorHub.Application.Services;

/// <summary>
/// Dịch vụ kiểm tra trạng thái hoạt động của API và kết nối SQL Server.
/// </summary>
public interface IKiemTraSucKhoeService
{
    Task<TrangThaiSucKhoeDto> KiemTraAsync(CancellationToken cancellationToken = default);
}
