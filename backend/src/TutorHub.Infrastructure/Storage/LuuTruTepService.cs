using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Storage;

/// <summary>
/// Dịch vụ lưu trữ tệp (Kho Public cho ảnh đại diện, Kho Private + Signed URL HMAC-SHA256 có thời hạn hết hạn cho giấy tờ gia sư).
/// CSDL chỉ lưu đường dẫn tương đối (`duongDanTep`).
/// </summary>
public sealed class LuuTruTepService : ILuuTruTepService
{
    private readonly IConfiguration _config;

    public LuuTruTepService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<string> LuuTepAsync(
        Stream luongTep,
        string tenTepGoc,
        string thuMucCon,
        bool laKhoPrivate,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(tenTepGoc).ToLowerInvariant();
        var tenMoi = $"{Guid.NewGuid():N}{ext}";
        var thuMucChuan = thuMucCon.Trim('/').Replace('\\', '/');
        var duongDanTuongDoi = $"{thuMucChuan}/{tenMoi}";

        var thuMucGoc = LayThuMucGoc(laKhoPrivate);
        var duongDanVatLy = Path.Combine(thuMucGoc, duongDanTuongDoi.Replace('/', Path.DirectorySeparatorChar));
        var thuMucCha = Path.GetDirectoryName(duongDanVatLy);
        if (!string.IsNullOrEmpty(thuMucCha))
        {
            Directory.CreateDirectory(thuMucCha);
        }

        await using var fileStream = new FileStream(duongDanVatLy, FileMode.Create, FileAccess.Write, FileShare.None);
        await luongTep.CopyToAsync(fileStream, cancellationToken);

        return duongDanTuongDoi;
    }

    public Task XoaTepAsync(
        string duongDanTep,
        bool laKhoPrivate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(duongDanTep))
        {
            return Task.CompletedTask;
        }

        var thuMucGoc = LayThuMucGoc(laKhoPrivate);
        var duongDanVatLy = Path.Combine(thuMucGoc, duongDanTep.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(duongDanVatLy))
        {
            try
            {
                File.Delete(duongDanVatLy);
            }
            catch
            {
                // Bỏ qua nếu tệp đang bận
            }
        }

        return Task.CompletedTask;
    }

    public string TaoSignedUrlXemTep(string duongDanTep, TimeSpan thoiHan)
    {
        var configuredUrl = _config["App:PublicApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = _config["RENDER_EXTERNAL_URL"];
        }
        var baseUrl = (string.IsNullOrWhiteSpace(configuredUrl) ? "http://localhost:5000" : configuredUrl).TrimEnd('/');
        var expUnix = DateTimeOffset.UtcNow.Add(thoiHan).ToUnixTimeSeconds();
        var configuredSecret = _config["Jwt:SecretKey"];
        var secret = string.IsNullOrWhiteSpace(configuredSecret) ? "TutorHub-Local-Dev-Signing-Key" : configuredSecret;
        var payload = $"{duongDanTep}|{expUnix}";
        var sigBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        var sig = Convert.ToHexString(sigBytes).ToLowerInvariant();

        return $"{baseUrl}/api/giay-to/xem-tep?tep={Uri.EscapeDataString(duongDanTep)}&exp={expUnix}&sig={sig}";
    }

    public Task<string> TaoUrlXemTepRiengTuCoThoiHanAsync(
        string duongDanTep,
        TimeSpan thoiHan,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(TaoSignedUrlXemTep(duongDanTep, thoiHan));
    }

    public (string DuongDanVatLy, string ContentType)? XacThucVaLayTepRiengTu(
        string duongDanTep,
        long expUnix,
        string sig)
    {
        if (string.IsNullOrWhiteSpace(duongDanTep) || string.IsNullOrWhiteSpace(sig))
        {
            return null;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expUnix)
        {
            return null;
        }

        var configuredSecret = _config["Jwt:SecretKey"];
        var secret = string.IsNullOrWhiteSpace(configuredSecret) ? "TutorHub-Local-Dev-Signing-Key" : configuredSecret;
        var payload = $"{duongDanTep}|{expUnix}";
        var expectedBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(sig.Trim());
        }
        catch
        {
            return null;
        }

        if (providedBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
        {
            return null;
        }

        var goc = Path.GetFullPath(LayThuMucGoc(laKhoPrivate: true));
        var duongDanSach = duongDanTep.Replace('\\', '/').TrimStart('/');
        if (duongDanSach.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var vatLy = Path.GetFullPath(Path.Combine(goc, duongDanSach.Replace('/', Path.DirectorySeparatorChar)));
        if (!vatLy.StartsWith(goc, StringComparison.OrdinalIgnoreCase) || !File.Exists(vatLy))
        {
            return null;
        }

        var ext = Path.GetExtension(vatLy).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };

        return (vatLy, contentType);
    }

    private string LayThuMucGoc(bool laKhoPrivate)
    {
        var basePath = _config["Storage:BasePath"] ?? "storage";
        var subFolder = laKhoPrivate ? "private" : "public";
        return Path.Combine(AppContext.BaseDirectory, basePath, subFolder);
    }
}
