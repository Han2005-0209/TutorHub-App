using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Services;

/// <summary>
/// Tác vụ nền định kỳ thực thi:
/// 1) Tự động giải ngân đơn `DA_XAC_NHAN` quá `BookingPolicy:AutoDisbursementHours` (mặc định 24h) sau `KetThucLuc` (bỏ qua `TRANH_CHAP`).
/// 2) Chuyển `HET_HAN` cho đơn `CHO_XAC_NHAN` quá `min(HanPhanHoi, BatDauLuc)` và đơn `DA_XAC_NHAN` có phí chưa thanh toán quá `BatDauLuc` (hoàn 100% về ví học viên nếu đã `DANG_GIU`).
/// </summary>
public sealed class TuDongXuLyDonVaGiaiNganBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<TuDongXuLyDonVaGiaiNganBackgroundService> _logger;

    public TuDongXuLyDonVaGiaiNganBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<TuDongXuLyDonVaGiaiNganBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = !string.Equals(_config["Jobs:Enabled"], "false", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            _logger.LogInformation("Jobs:Enabled = false => bỏ qua TuDongXuLyDonVaGiaiNganBackgroundService.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var taiChinh = scope.ServiceProvider.GetRequiredService<IViVaTaiChinhService>();
                var kq = await taiChinh.ChayChuKyTuDongXuLyDonVaGiaiNganAsync(null, stoppingToken);

                if (kq.SoDonDaGiaiNgan > 0 || kq.SoDonChuyenHetHan > 0)
                {
                    _logger.LogInformation(
                        "Chu kỳ tự động hoàn tất: giải ngân {GiaiNgan} đơn, hết hạn {HetHan} đơn (hoàn tiền {HoanTien} đơn).",
                        kq.SoDonDaGiaiNgan,
                        kq.SoDonChuyenHetHan,
                        kq.SoDonDaHoanTienKhiHetHan);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi trong chu kỳ TuDongXuLyDonVaGiaiNganBackgroundService.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
