namespace TutorHub.Domain.Services;

/// <summary>
/// NƠI DUY NHẤT tính phí nền tảng và thu nhập thực nhận của gia sư trong toàn bộ hệ thống.
/// Tính trung gian bằng decimal để tránh sai số dấu phẩy động, làm tròn AwayFromZero về int (VNĐ).
/// </summary>
public static class BoTinhTaiChinhDon
{
    public static (int SoHocVienTra, int PhiNenTang, int ThucNhan) TinhTaiChinhDon(
        int soHocVienTra,
        decimal tyLePhiPhanTram)
    {
        var goc = Math.Max(0, soHocVienTra);
        var tyLe = Math.Clamp(tyLePhiPhanTram, 0m, 100m);
        var phi = (int)Math.Round(goc * tyLe / 100m, 0, MidpointRounding.AwayFromZero);
        phi = Math.Clamp(phi, 0, goc);
        return (goc, phi, goc - phi);
    }
}
