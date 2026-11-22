namespace TutorHub.Application.Services;

/// <summary>
/// Dịch vụ gửi Email giao dịch thật (hỗ trợ Resend HTTPS API hoặc SMTP STARTTLS như Gmail App Password / Brevo).
/// </summary>
public interface IGuiEmailService
{
    /// <summary>
    /// Cho biết máy chủ gửi email thật (Resend hoặc SMTP) đã được cấu hình hay chưa.
    /// </summary>
    bool DaCauHinhGuiEmailThat { get; }

    /// <summary>
    /// Tên nhà cung cấp đang hoạt động ("RESEND" | "SMTP" | "CHUA_CAU_HINH").
    /// </summary>
    string TenNhaCungCap { get; }

    Task GuiEmailXacThucAsync(
        string denEmail,
        string hoTen,
        string maOtp,
        int phutHetHan,
        CancellationToken cancellationToken = default);

    Task GuiEmailDatLaiMatKhauAsync(
        string denEmail,
        string hoTen,
        string maOtp,
        int phutHetHan,
        CancellationToken cancellationToken = default);
}
