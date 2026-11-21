using TutorHub.Application.DTOs;

namespace TutorHub.Application.Services;

/// <summary>
/// Dịch vụ kiểm duyệt nội dung hiển thị trên web TutorHub:
/// 1. Kiểm duyệt nội dung do người dùng nhập (hồ sơ, tiêu đề, giới thiệu) để chặn từ ngữ không phù hợp và lộ liên hệ ngoài luồng.
/// 2. Kiểm tra văn bản hiển thị giao diện web để ngăn rò rỉ thông tin kỹ thuật nội bộ (.NET, EF Core, SQL Server, BCNF/4NF, Class v4, mã tài liệu, tên bảng, enum thô).
/// </summary>
public interface IKiemDuyetNoiDungService
{
    KetQuaKiemDuyetNoiDungDto KiemTraNoiDungNguoiDung(
        string tenTruong,
        string? noiDung,
        bool kiemTraLienHeNgoaiLuong = true);

    void BatBuocNoiDungNguoiDungHopLe(
        IEnumerable<(string TenTruong, string? NoiDung, bool KiemTraLienHeNgoaiLuong)> dsTruong);

    KetQuaKiemDuyetNoiDungDto KiemTraVanBanHienThiWeb(
        string? vanBanHienThi,
        string nguon = "web");
}
