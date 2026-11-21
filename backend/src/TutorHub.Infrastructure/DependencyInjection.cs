using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorHub.Application.Common;
using TutorHub.Application.Services;
using TutorHub.Infrastructure.Payments;
using TutorHub.Infrastructure.Persistence;
using TutorHub.Infrastructure.Services;
using TutorHub.Infrastructure.Storage;

namespace TutorHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var tyLePhi = decimal.TryParse(
            configuration["BookingPolicy:DefaultPlatformFeePercent"],
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var parsedFee)
            ? parsedFee
            : 12m;

        var gioTuGiaiNgan = int.TryParse(
            configuration["BookingPolicy:AutoDisbursementHours"],
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedHours) && parsedHours > 0
            ? parsedHours
            : 24;

        services.AddSingleton(new BookingPolicyOptions
        {
            DefaultPlatformFeePercent = tyLePhi,
            AutoDisbursementHours = gioTuGiaiNgan
        });

        services.AddDbContext<AppDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                });
            }
        });

        services.AddSingleton<IKiemDuyetNoiDungService, KiemDuyetNoiDungService>();
        services.AddScoped<IKiemTraSucKhoeService, KiemTraSucKhoeService>();
        services.AddScoped<IXacThucService, XacThucService>();
        services.AddScoped<IGiaSuVaMonHocService, GiaSuVaMonHocService>();
        services.AddScoped<IBangDieuKhienService, BangDieuKhienService>();
        services.AddScoped<IDonDatLichService, DonDatLichService>();
        services.AddScoped<IViVaTaiChinhService, ViVaTaiChinhService>();
        services.AddScoped<IHoiThoaiService, HoiThoaiService>();
        services.AddScoped<ILuuTruTepService, LuuTruTepService>();
        services.AddScoped<IVnPayClient, VnPayClient>();
        services.AddScoped<IPayOsClient, PayOsClient>();
        services.AddHostedService<TuDongXuLyDonVaGiaiNganBackgroundService>();

        return services;
    }
}
