using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Persistence;

public sealed class KiemTraSucKhoeService : IKiemTraSucKhoeService
{
    private readonly AppDbContext _dbContext;

    public KiemTraSucKhoeService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TrangThaiSucKhoeDto> KiemTraAsync(CancellationToken cancellationToken = default)
    {
        var csdlUp = false;
        try
        {
            csdlUp = await _dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            csdlUp = false;
        }

        return new TrangThaiSucKhoeDto(
            TrangThai: "ok",
            DichVu: "TutorHub.Api",
            Csdl: csdlUp ? "up" : "down",
            PhienBanRuntime: Environment.Version.ToString(),
            ThoiGianUtc: DateTime.UtcNow);
    }
}
