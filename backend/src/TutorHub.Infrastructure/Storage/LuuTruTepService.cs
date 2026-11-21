using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Storage;

/// <summary>
/// Dịch vụ lưu trữ tệp hỗ trợ Cloudinary (Public CDN URL cho ảnh đại diện, Private + Signed URL có thời hạn cho giấy tờ gia sư & minh chứng khiếu nại)
/// và tự động dự phòng lưu đĩa cục bộ khi chạy kiểm thử hoặc môi trường ngoại tuyến chưa cấu hình Cloudinary.
/// </summary>
public sealed class LuuTruTepService : ILuuTruTepService
{
    private const string TienToCloudinaryPrivate = "cld:private:";
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    private readonly IConfiguration _config;

    public LuuTruTepService(IConfiguration config)
    {
        _config = config;
    }

    private (string CloudName, string ApiKey, string ApiSecret)? LayCauHinhCloudinary()
    {
        var cloudName = _config["Cloudinary:CloudName"]?.Trim();
        var apiKey = _config["Cloudinary:ApiKey"]?.Trim();
        var apiSecret = _config["Cloudinary:ApiSecret"]?.Trim();
        if (string.IsNullOrWhiteSpace(cloudName)
            || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(apiSecret))
        {
            return null;
        }

        return (cloudName, apiKey, apiSecret);
    }

    public async Task<string> LuuTepAsync(
        Stream luongTep,
        string tenTepGoc,
        string thuMucCon,
        bool laKhoPrivate,
        CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(tenTepGoc).ToLowerInvariant();
        var thuMucChuan = thuMucCon.Trim('/').Replace('\\', '/');

        var cld = LayCauHinhCloudinary();
        if (cld is not null)
        {
            return await TaiLenCloudinaryAsync(cld.Value, luongTep, tenTepGoc, ext, thuMucChuan, laKhoPrivate, cancellationToken);
        }

        var tenMoi = $"{Guid.NewGuid():N}{ext}";
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

    public async Task XoaTepAsync(
        string duongDanTep,
        bool laKhoPrivate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(duongDanTep))
        {
            return;
        }

        var cld = LayCauHinhCloudinary();
        if (cld is not null && duongDanTep.StartsWith(TienToCloudinaryPrivate, StringComparison.Ordinal))
        {
            var parsed = PhanTichKhoaCloudinaryPrivate(duongDanTep);
            if (parsed is not null)
            {
                await XoaTepCloudinaryAsync(cld.Value, parsed.Value.ResourceType, parsed.Value.PublicId, "private", cancellationToken);
            }
            return;
        }

        if (cld is not null
            && duongDanTep.StartsWith("https://res.cloudinary.com/", StringComparison.OrdinalIgnoreCase))
        {
            var publicId = TrichXuatPublicIdTuUrlCongKhai(duongDanTep, cld.Value.CloudName);
            if (!string.IsNullOrWhiteSpace(publicId))
            {
                await XoaTepCloudinaryAsync(cld.Value, "image", publicId, "upload", cancellationToken);
            }
            return;
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
    }

    public string TaoSignedUrlXemTep(string duongDanTep, TimeSpan thoiHan)
    {
        if (string.IsNullOrWhiteSpace(duongDanTep))
        {
            return string.Empty;
        }

        if (duongDanTep.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || duongDanTep.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return duongDanTep;
        }

        var configuredUrl = _config["App:PublicApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            configuredUrl = _config["RENDER_EXTERNAL_URL"];
        }
        var baseUrl = (string.IsNullOrWhiteSpace(configuredUrl) ? "http://localhost:5000" : configuredUrl).TrimEnd('/');
        var expUnix = DateTimeOffset.UtcNow.Add(thoiHan).ToUnixTimeSeconds();
        var secret = LayKhoaKyUrl();
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
        if (!KiemTraChuKyUrl(duongDanTep, expUnix, sig))
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
        return (vatLy, LayContentTypeTheoDuoi(ext));
    }

    public async Task<(byte[] NoiDung, string ContentType)?> XacThucVaTaiTepRiengTuAsync(
        string duongDanTep,
        long expUnix,
        string sig,
        CancellationToken cancellationToken = default)
    {
        if (!KiemTraChuKyUrl(duongDanTep, expUnix, sig))
        {
            return null;
        }

        if (duongDanTep.StartsWith(TienToCloudinaryPrivate, StringComparison.Ordinal))
        {
            var cld = LayCauHinhCloudinary();
            var parsed = PhanTichKhoaCloudinaryPrivate(duongDanTep);
            if (cld is null || parsed is null)
            {
                return null;
            }

            var downloadUrl = TaoUrlTaiRiengTuCloudinary(
                cld.Value,
                parsed.Value.ResourceType,
                parsed.Value.Format,
                parsed.Value.PublicId,
                TimeSpan.FromMinutes(2));

            using var resp = await Http.GetAsync(downloadUrl, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
            var ct = resp.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(ct) || ct == "application/octet-stream")
            {
                var extFromFormat = string.IsNullOrWhiteSpace(parsed.Value.Format)
                    ? Path.GetExtension(parsed.Value.PublicId)
                    : "." + parsed.Value.Format.TrimStart('.');
                ct = LayContentTypeTheoDuoi(extFromFormat.ToLowerInvariant());
            }

            return (bytes, ct);
        }

        var local = XacThucVaLayTepRiengTu(duongDanTep, expUnix, sig);
        if (local is null)
        {
            return null;
        }

        var localBytes = await File.ReadAllBytesAsync(local.Value.DuongDanVatLy, cancellationToken);
        return (localBytes, local.Value.ContentType);
    }

    private async Task<string> TaiLenCloudinaryAsync(
        (string CloudName, string ApiKey, string ApiSecret) cld,
        Stream luongTep,
        string tenTepGoc,
        string ext,
        string thuMucChuan,
        bool laKhoPrivate,
        CancellationToken cancellationToken)
    {
        var laAnh = ext is ".jpg" or ".jpeg" or ".png" or ".webp";
        var resourceType = laAnh ? "image" : "raw";
        var nhom = laKhoPrivate ? "private" : "public";
        var maNgauNhien = Guid.NewGuid().ToString("N");
        var publicId = laAnh
            ? $"tutorhub/{nhom}/{thuMucChuan}/{maNgauNhien}"
            : $"tutorhub/{nhom}/{thuMucChuan}/{maNgauNhien}{ext}";

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["overwrite"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = ts
        };
        if (laKhoPrivate)
        {
            signParams["type"] = "private";
        }

        var signature = KySha1Cloudinary(signParams, cld.ApiSecret);

        using var ms = new MemoryStream();
        await luongTep.CopyToAsync(ms, cancellationToken);
        var fileBytes = ms.ToArray();

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(LayContentTypeTheoDuoi(ext));
        form.Add(fileContent, "file", string.IsNullOrWhiteSpace(tenTepGoc) ? $"file{ext}" : tenTepGoc);
        form.Add(new StringContent(cld.ApiKey), "api_key");
        form.Add(new StringContent(ts), "timestamp");
        form.Add(new StringContent(publicId), "public_id");
        form.Add(new StringContent("true"), "overwrite");
        if (laKhoPrivate)
        {
            form.Add(new StringContent("private"), "type");
        }
        form.Add(new StringContent(signature), "signature");

        var endpoint = $"https://api.cloudinary.com/v1_1/{cld.CloudName}/{resourceType}/upload";
        using var resp = await Http.PostAsync(endpoint, form, cancellationToken);
        var jsonText = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Không thể tải tệp lên Cloudinary ({(int)resp.StatusCode}): {jsonText}");
        }

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;
        if (!laKhoPrivate)
        {
            var secureUrl = root.GetProperty("secure_url").GetString();
            if (string.IsNullOrWhiteSpace(secureUrl))
            {
                throw new InvalidOperationException("Cloudinary không trả về secure_url cho tệp công khai.");
            }
            return secureUrl;
        }

        var returnedPublicId = root.GetProperty("public_id").GetString() ?? publicId;
        var returnedFormat = root.TryGetProperty("format", out var fmtProp) ? (fmtProp.GetString() ?? string.Empty) : string.Empty;
        if (string.IsNullOrWhiteSpace(returnedFormat) && !string.IsNullOrWhiteSpace(ext))
        {
            returnedFormat = ext.TrimStart('.');
        }

        return $"{TienToCloudinaryPrivate}{resourceType}:{returnedFormat}:{returnedPublicId}";
    }

    private static async Task XoaTepCloudinaryAsync(
        (string CloudName, string ApiKey, string ApiSecret) cld,
        string resourceType,
        string publicId,
        string deliveryType,
        CancellationToken cancellationToken)
    {
        try
        {
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["public_id"] = publicId,
                ["timestamp"] = ts,
                ["type"] = deliveryType
            };
            var sig = KySha1Cloudinary(signParams, cld.ApiSecret);

            using var form = new MultipartFormDataContent
            {
                { new StringContent(cld.ApiKey), "api_key" },
                { new StringContent(publicId), "public_id" },
                { new StringContent(deliveryType), "type" },
                { new StringContent(ts), "timestamp" },
                { new StringContent(sig), "signature" }
            };

            var endpoint = $"https://api.cloudinary.com/v1_1/{cld.CloudName}/{resourceType}/destroy";
            using var _ = await Http.PostAsync(endpoint, form, cancellationToken);
        }
        catch
        {
            // Bỏ qua lỗi mạng khi dọn tệp cũ
        }
    }

    private static string TaoUrlTaiRiengTuCloudinary(
        (string CloudName, string ApiKey, string ApiSecret) cld,
        string resourceType,
        string format,
        string publicId,
        TimeSpan thoiHan)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = now + Math.Max(30, (long)thoiHan.TotalSeconds);

        var signParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["expires_at"] = expiresAt.ToString(),
            ["public_id"] = publicId,
            ["timestamp"] = now.ToString(),
            ["type"] = "private"
        };

        if (resourceType == "image" && !string.IsNullOrWhiteSpace(format))
        {
            signParams["format"] = format;
        }

        var sig = KySha1Cloudinary(signParams, cld.ApiSecret);

        var qs = new StringBuilder();
        qs.Append("api_key=").Append(Uri.EscapeDataString(cld.ApiKey));
        qs.Append("&expires_at=").Append(expiresAt);
        if (resourceType == "image" && !string.IsNullOrWhiteSpace(format))
        {
            qs.Append("&format=").Append(Uri.EscapeDataString(format));
        }
        qs.Append("&public_id=").Append(Uri.EscapeDataString(publicId));
        qs.Append("&signature=").Append(sig);
        qs.Append("&timestamp=").Append(now);
        qs.Append("&type=private");

        return $"https://api.cloudinary.com/v1_1/{cld.CloudName}/{resourceType}/download?{qs}";
    }

    private static (string ResourceType, string Format, string PublicId)? PhanTichKhoaCloudinaryPrivate(string key)
    {
        if (!key.StartsWith(TienToCloudinaryPrivate, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = key[TienToCloudinaryPrivate.Length..];
        var parts = rest.Split(':', 3);
        if (parts.Length < 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return null;
        }

        return (parts[0], parts[1], parts[2]);
    }

    private static string? TrichXuatPublicIdTuUrlCongKhai(string url, string cloudName)
    {
        var marker = $"/{cloudName}/image/upload/";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return null;
        }

        var tail = url[(idx + marker.Length)..];
        var qIdx = tail.IndexOf('?');
        if (qIdx >= 0)
        {
            tail = tail[..qIdx];
        }

        // Bỏ tiền tố phiên bản v123456789/
        if (tail.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            var slash = tail.IndexOf('/');
            if (slash > 1 && tail[1..slash].All(char.IsDigit))
            {
                tail = tail[(slash + 1)..];
            }
        }

        var dot = tail.LastIndexOf('.');
        return dot > 0 ? tail[..dot] : tail;
    }

    private static string KySha1Cloudinary(SortedDictionary<string, string> thamSo, string apiSecret)
    {
        var chuoi = string.Join("&", thamSo.Select(kv => $"{kv.Key}={kv.Value}")) + apiSecret;
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(chuoi));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private bool KiemTraChuKyUrl(string duongDanTep, long expUnix, string sig)
    {
        if (string.IsNullOrWhiteSpace(duongDanTep) || string.IsNullOrWhiteSpace(sig))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expUnix)
        {
            return false;
        }

        var secret = LayKhoaKyUrl();
        var payload = $"{duongDanTep}|{expUnix}";
        var expectedBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(sig.Trim());
        }
        catch
        {
            return false;
        }

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private string LayKhoaKyUrl()
    {
        var key = _config["Storage:UrlSigningKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            key = _config["Jwt:SecretKey"];
        }
        return string.IsNullOrWhiteSpace(key) ? "TutorHub-Local-Dev-Signing-Key" : key;
    }

    private static string LayContentTypeTheoDuoi(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };

    private string LayThuMucGoc(bool laKhoPrivate)
    {
        var basePath = _config["Storage:BasePath"] ?? "storage";
        var subFolder = laKhoPrivate ? "private" : "public";
        return Path.Combine(AppContext.BaseDirectory, basePath, subFolder);
    }
}
