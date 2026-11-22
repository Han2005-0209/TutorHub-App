using TutorHub.Application.DTOs;

namespace TutorHub.Application.Services;

/// <summary>
/// Dịch vụ Đánh giá buổi học (Module 6 — `DanhGia`):
/// - Học sinh tạo đánh giá 1–5 sao cho đơn `HOAN_THANH` và sửa trong 48 giờ.
/// - Gia sư xem danh sách đánh giá nhận được và phản hồi 1 lần.
/// - Quản trị viên kiểm duyệt, ẩn/khôi phục đánh giá vi phạm (ghi nhận tên Admin xử lý & AuditLog).
/// </summary>
public interface IDanhGiaService
{
    Task<DanhGiaChiTietDto> TaoDanhGiaAsync(
        Guid hocSinhId,
        TaoDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    Task<DanhGiaChiTietDto> SuaDanhGiaAsync(
        Guid hocSinhId,
        Guid danhGiaId,
        SuaDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    Task<KetQuaPhanTrangDto<DanhGiaChiTietDto>> LayDsDanhGiaCuaGiaSuAsync(
        Guid giaSuId,
        int trang = 1,
        int kichThuocTrang = 15,
        CancellationToken cancellationToken = default);

    Task<DanhGiaChiTietDto> PhanHoiDanhGiaAsync(
        Guid giaSuId,
        Guid danhGiaId,
        PhanHoiDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    Task<KetQuaPhanTrangDto<DanhGiaChiTietDto>> LayDsDanhGiaQuanTriAsync(
        bool? biAn = null,
        string? tuKhoa = null,
        int trang = 1,
        int kichThuocTrang = 15,
        CancellationToken cancellationToken = default);

    Task<DanhGiaChiTietDto> KiemDuyetDanhGiaAsync(
        Guid quanTriId,
        Guid danhGiaId,
        KiemDuyetDanhGiaYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);
}
