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
