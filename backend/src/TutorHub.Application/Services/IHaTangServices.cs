using System.Text.Json;
using TutorHub.Application.DTOs;

namespace TutorHub.Application.Services;

/// <summary>
/// Giao diện dịch vụ lưu trữ tệp (Kho Public cho ảnh đại diện, Kho Private + Signed URL cho giấy tờ gia sư).
/// Cài đặt cụ thể đặt tại TutorHub.Infrastructure.Storage.
/// </summary>
public interface ILuuTruTepService
{
    Task<string> LuuTepAsync(
        Stream luongTep,
        string tenTepGoc,
        string thuMucCon,
        bool laKhoPrivate,
        CancellationToken cancellationToken = default);

    Task XoaTepAsync(
        string duongDanTep,
        bool laKhoPrivate,
        CancellationToken cancellationToken = default);

    string TaoSignedUrlXemTep(string duongDanTep, TimeSpan thoiHan);

    Task<string> TaoUrlXemTepRiengTuCoThoiHanAsync(
        string duongDanTep,
        TimeSpan thoiHan,
        CancellationToken cancellationToken = default);

    (string DuongDanVatLy, string ContentType)? XacThucVaLayTepRiengTu(
        string duongDanTep,
        long expUnix,
        string sig);

    Task<(byte[] NoiDung, string ContentType)?> XacThucVaTaiTepRiengTuAsync(
        string duongDanTep,
        long expUnix,
        string sig,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Giao diện tích hợp cổng thanh toán VNPay Sandbox.
/// Cài đặt cụ thể đặt tại TutorHub.Infrastructure.Payments.
/// </summary>
public interface IVnPayClient
{
    string TaoUrlThanhToan(string maGiaoDichCong, int soTienVnd, string noiDung, string ipKhach);
    bool XacThucChuKyCallback(IReadOnlyDictionary<string, string> thamSoVnpay);
}

/// <summary>
/// Giao diện tích hợp cổng VietQR tự động PayOS API v2 (HMAC-SHA256).
/// Cài đặt cụ thể đặt tại TutorHub.Infrastructure.Payments.PayOsClient.
/// </summary>
public interface IPayOsClient
{
    bool DaCauHinh { get; }

    Task<PayOsTaoLinkKetQuaDto> TaoYeuCauThanhToanAsync(
        long orderCode,
        int amount,
        string description,
        string returnUrl,
        string cancelUrl,
        long expiredAtUnix,
        CancellationToken cancellationToken = default);

    Task<PayOsTrangThaiDonDto?> LayThongTinDonAsync(
        long orderCode,
        CancellationToken cancellationToken = default);

    Task HuyDonAsync(
        long orderCode,
        string lyDo,
        CancellationToken cancellationToken = default);

    bool XacThucChuKyWebhook(JsonElement dataElement, string signature);

    string LayTenNganHangTheoBin(string? bin);
}
