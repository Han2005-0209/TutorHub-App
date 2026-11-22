using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TutorHub.Application.Common;
using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Services;

/// <summary>
/// Dịch vụ gửi Email giao dịch thật (OTP xác thực email & đặt lại mật khẩu).
/// Ưu tiên Resend HTTPS API (chạy mượt qua port 443 trên Render), hỗ trợ dự phòng SMTP STARTTLS (Gmail App Password / Brevo / Outlook).
/// </summary>
public sealed class GuiEmailService : IGuiEmailService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly IConfiguration _config;
    private readonly ILogger<GuiEmailService> _logger;

    public GuiEmailService(IConfiguration config, ILogger<GuiEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string? ResendApiKey =>
        LayCauHinh("Email:ResendApiKey", "RESEND_API_KEY");

    private string? SmtpHost =>
        LayCauHinh("Email:SmtpHost", "SMTP_HOST");

    private int SmtpPort =>
        int.TryParse(LayCauHinh("Email:SmtpPort", "SMTP_PORT"), out var p) && p > 0 ? p : 587;

    private string? SmtpUser =>
        LayCauHinh("Email:SmtpUser", "SMTP_USER");

    private string? SmtpPassword =>
        LayCauHinh("Email:SmtpPassword", "SMTP_PASSWORD");

    private string FromEmail =>
        LayCauHinh("Email:FromEmail", "EMAIL_FROM")
        ?? (!string.IsNullOrWhiteSpace(SmtpUser) ? SmtpUser : "onboarding@resend.dev");

    private string FromName =>
        LayCauHinh("Email:FromName", "EMAIL_FROM_NAME") ?? "TutorHub";

    public bool DaCauHinhGuiEmailThat =>
        !string.IsNullOrWhiteSpace(ResendApiKey)
        || (!string.IsNullOrWhiteSpace(SmtpHost)
            && !string.IsNullOrWhiteSpace(SmtpUser)
            && !string.IsNullOrWhiteSpace(SmtpPassword));

    public string TenNhaCungCap
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ResendApiKey)) return "RESEND";
            if (!string.IsNullOrWhiteSpace(SmtpHost)
                && !string.IsNullOrWhiteSpace(SmtpUser)
                && !string.IsNullOrWhiteSpace(SmtpPassword)) return "SMTP";
            return "CHUA_CAU_HINH";
        }
    }

    public Task GuiEmailXacThucAsync(
        string denEmail,
        string hoTen,
        string maOtp,
        int phutHetHan,
        CancellationToken cancellationToken = default)
    {
        var tieuDe = $"[TutorHub] Mã xác thực địa chỉ email của bạn: {maOtp}";
        var noiDungHtml = TaoMauHtmlEmail(
            tieuDeNgan: "Xác thực địa chỉ Email",
            loiChao: $"Xin chào <strong>{WebUtility.HtmlEncode(hoTen)}</strong>,",
            moTa: "Cảm ơn bạn đã sử dụng nền tảng gia sư <strong>TutorHub</strong>. Vui lòng nhập mã xác nhận 6 chữ số bên dưới để hoàn tất xác thực địa chỉ email tài khoản của bạn:",
            maOtp: maOtp,
            ghiChuHetHan: $"Mã xác nhận có hiệu lực trong <strong>{phutHetHan} phút</strong> và chỉ dùng được một lần.",
            canhBao: "Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email này.");

        return GuiEmailThucTeAsync(denEmail, tieuDe, noiDungHtml, cancellationToken);
    }

    public Task GuiEmailDatLaiMatKhauAsync(
        string denEmail,
        string hoTen,
        string maOtp,
        int phutHetHan,
        CancellationToken cancellationToken = default)
    {
        var tieuDe = $"[TutorHub] Mã khôi phục mật khẩu của bạn: {maOtp}";
        var noiDungHtml = TaoMauHtmlEmail(
            tieuDeNgan: "Khôi phục mật khẩu tài khoản",
            loiChao: $"Xin chào <strong>{WebUtility.HtmlEncode(hoTen)}</strong>,",
            moTa: "Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản <strong>TutorHub</strong> của bạn. Vui lòng nhập mã OTP 6 chữ số bên dưới vào màn hình khôi phục mật khẩu:",
            maOtp: maOtp,
            ghiChuHetHan: $"Mã khôi phục có hiệu lực trong <strong>{phutHetHan} phút</strong> (tối đa 5 lần nhập sai).",
            canhBao: "Tuyệt đối không chia sẻ mã OTP này cho bất kỳ ai, kể cả nhân viên hỗ trợ. Nếu không phải bạn yêu cầu, hãy bỏ qua email này.");

        return GuiEmailThucTeAsync(denEmail, tieuDe, noiDungHtml, cancellationToken);
    }

    private async Task GuiEmailThucTeAsync(
        string denEmail,
        string tieuDe,
        string noiDungHtml,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ResendApiKey))
        {
            await GuiQuaResendAsync(denEmail, tieuDe, noiDungHtml, cancellationToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(SmtpHost)
            && !string.IsNullOrWhiteSpace(SmtpUser)
            && !string.IsNullOrWhiteSpace(SmtpPassword))
        {
            await GuiQuaSmtpAsync(denEmail, tieuDe, noiDungHtml, cancellationToken);
            return;
        }

        throw new LoiUngDungException(
            "CHUA_CAU_HINH_EMAIL",
            "Hệ thống chưa cấu hình máy chủ gửi email thật (RESEND_API_KEY hoặc SMTP_HOST/SMTP_USER/SMTP_PASSWORD). Vui lòng liên hệ quản trị viên.",
            503);
    }

    private async Task GuiQuaResendAsync(
        string denEmail,
        string tieuDe,
        string noiDungHtml,
        CancellationToken cancellationToken)
    {
        var fromHeader = FromEmail.Contains('<') ? FromEmail : $"{FromName} <{FromEmail}>";
        var payload = new
        {
            from = fromHeader,
            to = new[] { denEmail },
            subject = tieuDe,
            html = noiDungHtml
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ResendApiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var resp = await Http.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gửi email qua Resend thất bại ({StatusCode}): {Body}", (int)resp.StatusCode, errBody);
            throw new LoiUngDungException(
                "LOI_GUI_EMAIL",
                "Không thể gửi email xác nhận lúc này. Vui lòng kiểm tra cấu hình dịch vụ email hoặc thử lại sau ít phút.",
                502);
        }

        _logger.LogInformation("Đã gửi email qua Resend thành công tới {Email}.", denEmail);
    }

    private async Task GuiQuaSmtpAsync(
        string denEmail,
        string tieuDe,
        string noiDungHtml,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient(SmtpHost!, SmtpPort)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(SmtpUser, SmtpPassword),
                Timeout = 20_000
            };

            using var message = new MailMessage
            {
                From = new MailAddress(FromEmail, FromName, Encoding.UTF8),
                Subject = tieuDe,
                SubjectEncoding = Encoding.UTF8,
                Body = noiDungHtml,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(denEmail));

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Đã gửi email qua SMTP ({Host}:{Port}) thành công tới {Email}.", SmtpHost, SmtpPort, denEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gửi email qua SMTP ({Host}:{Port}) tới {Email} thất bại.", SmtpHost, SmtpPort, denEmail);
            throw new LoiUngDungException(
                "LOI_GUI_EMAIL",
                "Không thể gửi email qua máy chủ SMTP lúc này. Vui lòng thử lại sau ít phút.",
                502);
        }
    }

    private string? LayCauHinh(string keyJson, string keyEnv)
    {
        var v = _config[keyJson];
        if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        var env = _config[keyEnv] ?? Environment.GetEnvironmentVariable(keyEnv);
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    private static string TaoMauHtmlEmail(
        string tieuDeNgan,
        string loiChao,
        string moTa,
        string maOtp,
        string ghiChuHetHan,
        string canhBao) =>
        $"""
        <!doctype html>
        <html lang="vi">
          <body style="margin:0;padding:24px;background:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;color:#0f172a">
            <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e2e8f0;box-shadow:0 8px 24px rgba(15,23,42,0.06)">
              <div style="background:linear-gradient(135deg,#312e81 0%,#4f46e5 100%);padding:22px 26px;color:#ffffff">
                <div style="font-size:12px;letter-spacing:0.08em;text-transform:uppercase;opacity:0.85;font-weight:700">TutorHub · Nền tảng kết nối Gia sư 1-1</div>
                <h1 style="margin:6px 0 0;font-size:20px;font-weight:800">{WebUtility.HtmlEncode(tieuDeNgan)}</h1>
              </div>
              <div style="padding:26px">
                <p style="margin:0 0 12px;font-size:15px;line-height:1.55">{loiChao}</p>
                <p style="margin:0 0 20px;font-size:14px;line-height:1.6;color:#334155">{moTa}</p>
                <div style="background:#eef2ff;border:1px dashed #6366f1;border-radius:12px;padding:18px;text-align:center;margin:0 0 20px">
                  <div style="font-size:11px;text-transform:uppercase;letter-spacing:0.1em;color:#4338ca;font-weight:700;margin-bottom:6px">Mã xác nhận OTP (6 chữ số)</div>
                  <div style="font-size:32px;font-weight:900;letter-spacing:0.28em;color:#1e1b4b;font-family:Consolas,monospace">{WebUtility.HtmlEncode(maOtp)}</div>
                </div>
                <p style="margin:0 0 10px;font-size:13px;color:#475569;line-height:1.55">{ghiChuHetHan}</p>
                <p style="margin:0;font-size:12px;color:#64748b;line-height:1.55;border-top:1px solid #e2e8f0;padding-top:14px">{canhBao}</p>
              </div>
            </div>
          </body>
        </html>
        """;
}
