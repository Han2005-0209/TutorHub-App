namespace TutorHub.Application.Common;

/// <summary>
/// Cấu hình chính sách đặt lịch & giải ngân đọc từ section `BookingPolicy` trong appsettings.json.
/// Dùng 1 nguồn duy nhất cho tỷ lệ phí nền tảng và cửa sổ 24h (khiếu nại / tự giải ngân).
/// </summary>
public sealed class BookingPolicyOptions
{
    public decimal DefaultPlatformFeePercent { get; init; } = 12m;
    public int AutoDisbursementHours { get; init; } = 24;
}
