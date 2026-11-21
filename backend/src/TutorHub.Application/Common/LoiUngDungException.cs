namespace TutorHub.Application.Common;

/// <summary>
/// Ngoại lệ chuẩn của tầng Application gắn kèm mã lỗi UPPER_SNAKE_CASE và mã HTTP.
/// Middleware ở tầng Api sẽ bắt ngoại lệ này và đóng gói thành envelope { loi: ... }.
/// </summary>
public sealed class LoiUngDungException : Exception
{
    public string MaLoi { get; }
    public int MaHttp { get; }
    public IReadOnlyList<ChiTietLoiTruongDto>? ChiTiet { get; }

    public LoiUngDungException(
        string maLoi,
        string thongBao,
        int maHttp = 400,
        IReadOnlyList<ChiTietLoiTruongDto>? chiTiet = null)
        : base(thongBao)
    {
        MaLoi = maLoi;
        MaHttp = maHttp;
        ChiTiet = chiTiet;
    }
}

public sealed record ChiTietLoiTruongDto(string Truong, string ThongBao);
