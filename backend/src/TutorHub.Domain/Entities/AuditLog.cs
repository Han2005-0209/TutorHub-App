namespace TutorHub.Domain.Entities;

/// <summary>
/// Nhật ký kiểm toán bảo mật (append-only).
/// CHỈ ghi 3 nhóm sự kiện không có bảng nghiệp vụ khác lưu: đổi ngân hàng (che STK),
/// nhập sai mật khẩu xác nhận khi rút tiền / đổi ngân hàng, và địa chỉ IP thao tác nhạy cảm.
/// </summary>
public sealed class AuditLog : ThucTheGoc
{
    public Guid? NguoiDungId { get; set; }
    public string HanhDong { get; set; } = string.Empty;
    public string DoiTuong { get; set; } = string.Empty;
    public string? GiaTriCuJson { get; set; }
    public string? GiaTriMoiJson { get; set; }
    public string? DiaChiIp { get; set; }
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}
