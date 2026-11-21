using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Payments;

/// <summary>
/// Tích hợp trực tiếp cổng VietQR tự động PayOS API v2 (https://api-merchant.payos.vn).
/// Sử dụng chuẩn ký và xác thực chữ ký HMAC-SHA256 của PayOS, không chứa khóa bí mật cứng trong mã nguồn.
/// </summary>
public sealed class PayOsClient : IPayOsClient
{
    private const string PayOsApiBaseUrl = "https://api-merchant.payos.vn";

    private static readonly IReadOnlyDictionary<string, string> BangNganHangBin =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["970422"] = "MB Bank",
            ["970436"] = "Vietcombank",
            ["970407"] = "Techcombank",
            ["970416"] = "ACB",
            ["970418"] = "BIDV",
            ["970415"] = "VietinBank",
            ["970423"] = "TPBank",
            ["970432"] = "VPBank",
            ["970405"] = "Agribank",
            ["970403"] = "Sacombank",
            ["970441"] = "VIB",
            ["970437"] = "HDBank",
            ["970448"] = "OCB",
            ["970426"] = "MSB",
            ["970443"] = "SHB",
            ["970431"] = "Eximbank",
            ["970449"] = "LPBank",
            ["970454"] = "BVBank",
            ["970406"] = "DongA Bank",
            ["970414"] = "OCEANBANK",
            ["970428"] = "Nam A Bank",
            ["970429"] = "SCB",
            ["970433"] = "VietBank",
            ["970438"] = "BaoViet Bank",
            ["970440"] = "SeABank",
            ["970452"] = "KienLongBank",
            ["970458"] = "UOB",
            ["546034"] = "CAKE by VPBank",
            ["546035"] = "Ubank by VPBank",
            ["963388"] = "Timo by BVBank",
            ["971005"] = "Viettel Money",
            ["971011"] = "VNPT Money",
        };

    private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        BaseAddress = new Uri(PayOsApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(20)
    };

    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _apiKey;
    private readonly string _checksumKey;

    public PayOsClient(IConfiguration configuration, HttpClient? httpClient = null)
    {
        _http = httpClient ?? SharedHttpClient;
        if (_http.BaseAddress is null)
        {
            _http.BaseAddress = new Uri(PayOsApiBaseUrl);
        }

        _clientId = LayGiaTriCauHinh(configuration, "PayOs:ClientId", "PAYOS_CLIENT_ID");
        _apiKey = LayGiaTriCauHinh(configuration, "PayOs:ApiKey", "PAYOS_API_KEY");
        _checksumKey = LayGiaTriCauHinh(configuration, "PayOs:ChecksumKey", "PAYOS_CHECKSUM_KEY");
    }

    public bool DaCauHinh =>
        !string.IsNullOrWhiteSpace(_clientId) &&
        !string.IsNullOrWhiteSpace(_apiKey) &&
        !string.IsNullOrWhiteSpace(_checksumKey);

    public async Task<PayOsTaoLinkKetQuaDto> TaoYeuCauThanhToanAsync(
        long orderCode,
        int amount,
        string description,
        string returnUrl,
        string cancelUrl,
        long expiredAtUnix,
        CancellationToken cancellationToken = default)
    {
        if (!DaCauHinh)
        {
            throw new LoiUngDungException(
                "CHUA_CAU_HINH_PAYOS",
                "Hệ thống chưa được cấu hình khóa cổng PayOS VietQR (PayOs__ClientId, PayOs__ApiKey, PayOs__ChecksumKey).",
                503);
        }

        var moTaChuan = description.Length > 25 ? description[..25] : description;
        var signature = TinhChuKyTaoYeuCau(_checksumKey, orderCode, amount, moTaChuan, returnUrl, cancelUrl);

        var body = new
        {
            orderCode,
            amount,
            description = moTaChuan,
            returnUrl,
            cancelUrl,
            expiredAt = expiredAtUnix,
            signature
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v2/payment-requests")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("x-client-id", _clientId);
        req.Headers.Add("x-api-key", _apiKey);

        using var res = await _http.SendAsync(req, cancellationToken);
        var raw = await res.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
        var desc = root.TryGetProperty("desc", out var descEl) ? descEl.GetString() : null;

        if (!res.IsSuccessStatusCode || !string.Equals(code, "00", StringComparison.Ordinal) ||
            !root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
        {
            throw new LoiUngDungException(
                "LOI_CONG_PAYOS",
                $"Không thể khởi tạo mã VietQR PayOS: {desc ?? res.ReasonPhrase ?? "Lỗi không xác định"} (mã: {code ?? ((int)res.StatusCode).ToString(CultureInfo.InvariantCulture)}).",
                502);
        }

        return new PayOsTaoLinkKetQuaDto(
            OrderCode: dataEl.TryGetProperty("orderCode", out var ocEl) ? ocEl.GetInt64() : orderCode,
            PaymentLinkId: DocChuoi(dataEl, "paymentLinkId"),
            CheckoutUrl: DocChuoi(dataEl, "checkoutUrl"),
            QrCode: DocChuoi(dataEl, "qrCode"),
            Bin: DocChuoi(dataEl, "bin"),
            AccountNumber: DocChuoi(dataEl, "accountNumber"),
            AccountName: DocChuoi(dataEl, "accountName"),
            Description: DocChuoi(dataEl, "description", moTaChuan),
            Status: DocChuoi(dataEl, "status", "PENDING"));
    }

    public async Task<PayOsTrangThaiDonDto?> LayThongTinDonAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        if (!DaCauHinh)
        {
            return null;
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/v2/payment-requests/{orderCode}");
        req.Headers.Add("x-client-id", _clientId);
        req.Headers.Add("x-api-key", _apiKey);

        using var res = await _http.SendAsync(req, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            return null;
        }

        var raw = await res.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
        if (!string.Equals(code, "00", StringComparison.Ordinal) ||
            !root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? reference = null;
        if (dataEl.TryGetProperty("transactions", out var txArr) &&
            txArr.ValueKind == JsonValueKind.Array &&
            txArr.GetArrayLength() > 0)
        {
            var firstTx = txArr[0];
            if (firstTx.ValueKind == JsonValueKind.Object &&
                firstTx.TryGetProperty("reference", out var refEl))
            {
                reference = refEl.GetString();
            }
        }

        var amount = dataEl.TryGetProperty("amount", out var aEl) && aEl.TryGetInt32(out var aVal) ? aVal : 0;
        var amountPaid = dataEl.TryGetProperty("amountPaid", out var apEl) && apEl.TryGetInt32(out var apVal) ? apVal : 0;
        var status = DocChuoi(dataEl, "status", "PENDING");

        return new PayOsTrangThaiDonDto(
            OrderCode: orderCode,
            Amount: amount,
            AmountPaid: amountPaid,
            Status: status,
            Reference: reference);
    }

    public async Task HuyDonAsync(
        long orderCode,
        string lyDo,
        CancellationToken cancellationToken = default)
    {
        if (!DaCauHinh)
        {
            return;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/v2/payment-requests/{orderCode}/cancel")
            {
                Content = JsonContent.Create(new { cancellationReason = lyDo })
            };
            req.Headers.Add("x-client-id", _clientId);
            req.Headers.Add("x-api-key", _apiKey);
            using var _ = await _http.SendAsync(req, cancellationToken);
        }
        catch
        {
            // Bỏ qua lỗi mạng khi hủy link thanh toán phụ trên PayOS
        }
    }

    public bool XacThucChuKyWebhook(JsonElement dataElement, string signature)
    {
        if (!DaCauHinh || string.IsNullOrWhiteSpace(signature) || dataElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var expectedHex = TinhChuKyWebhookData(_checksumKey, dataElement);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHex.ToLowerInvariant());
        var actualBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public string LayTenNganHangTheoBin(string? bin)
    {
        if (string.IsNullOrWhiteSpace(bin))
        {
            return "Ngân hàng VietQR";
        }

        return BangNganHangBin.TryGetValue(bin.Trim(), out var ten) ? ten : $"Ngân hàng ({bin.Trim()})";
    }

    /// <summary>
    /// Chuẩn ký tạo Payment Request của PayOS API v2:
    /// amount=$amount&amp;cancelUrl=$cancelUrl&amp;description=$description&amp;orderCode=$orderCode&amp;returnUrl=$returnUrl
    /// </summary>
    public static string TinhChuKyTaoYeuCau(
        string checksumKey,
        long orderCode,
        int amount,
        string description,
        string returnUrl,
        string cancelUrl)
    {
        var dataStr = string.Create(
            CultureInfo.InvariantCulture,
            $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}");
        return TinhHmacSha256Hex(checksumKey, dataStr);
    }

    /// <summary>
    /// Chuẩn ký đối tượng `data` trong Webhook PayOS API v2 (sắp xếp khóa theo thứ tự ASCII Ordinal).
    /// </summary>
    public static string TinhChuKyWebhookData(string checksumKey, JsonElement dataElement)
    {
        var props = dataElement.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={ChuanHoaGiaTriWebhook(p.Value)}");

        var dataStr = string.Join("&", props);
        return TinhHmacSha256Hex(checksumKey, dataStr);
    }

    private static string ChuanHoaGiaTriWebhook(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => ChuanHoaChuoiWebhook(value.GetString()),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Array => JsonSerializer.Serialize(
                value.EnumerateArray().Select(ChuanHoaNodeDeQuy).ToArray()),
            JsonValueKind.Object => JsonSerializer.Serialize(ChuanHoaNodeDeQuy(value)),
            _ => value.GetRawText()
        };
    }

    private static string ChuanHoaChuoiWebhook(string? s)
    {
        if (string.IsNullOrEmpty(s) ||
            string.Equals(s, "null", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s, "undefined", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return s;
    }

    private static object? ChuanHoaNodeDeQuy(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject()
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToDictionary(p => p.Name, p => ChuanHoaNodeDeQuy(p.Value)),
            JsonValueKind.Array => el.EnumerateArray().Select(ChuanHoaNodeDeQuy).ToArray(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string TinhHmacSha256Hex(string secretKey, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string LayGiaTriCauHinh(IConfiguration configuration, string keyChinh, string keyEnvPhu)
    {
        return (configuration[keyChinh] ?? configuration[keyEnvPhu] ?? Environment.GetEnvironmentVariable(keyEnvPhu) ?? string.Empty).Trim();
    }

    private static string DocChuoi(JsonElement obj, string propName, string macDinh = "")
    {
        return obj.TryGetProperty(propName, out var el) && el.ValueKind == JsonValueKind.String
            ? (el.GetString() ?? macDinh)
            : macDinh;
    }
}
