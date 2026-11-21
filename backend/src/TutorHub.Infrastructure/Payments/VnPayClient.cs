using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Payments;

/// <summary>
/// Khung tích hợp cổng thanh toán VNPay Sandbox v2.1.0 (HMAC-SHA512).
/// Logic chi tiết sẽ được cài đặt ở module Thanh toán & Ví theo docs/migration/06-INTEGRATIONS.md.
/// </summary>
public sealed class VnPayClient : IVnPayClient
{
    public string TaoUrlThanhToan(string maGiaoDichCong, int soTienVnd, string noiDung, string ipKhach)
    {
        throw new NotImplementedException("Sẽ cài đặt trong module Thanh toán & Ví.");
    }

    public bool XacThucChuKyCallback(IReadOnlyDictionary<string, string> thamSoVnpay)
    {
        throw new NotImplementedException("Sẽ cài đặt trong module Thanh toán & Ví.");
    }
}
