using TutorHub.Application.DTOs;
using TutorHub.Domain.Enums;

namespace TutorHub.Application.Services;

/// <summary>
/// Dịch vụ nghiệp vụ Module 2 (Hồ sơ Gia sư & Giấy tờ) và Module 3 (Môn học, Lịch rảnh, Tìm kiếm Gia sư & Mục yêu thích).
/// </summary>
public interface IGiaSuVaMonHocService
{
    // Danh mục môn học & Đặc tả 2 hình thức học
    Task<IReadOnlyList<MonHocDto>> LayDsMonHocAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<ThongTinHinhThucHocDto> LayThongTinHinhThucHoc();

    // Module 2 — Hồ sơ gia sư & Giấy tờ của tôi (GIA_SU)
    Task<HoSoGiaSuToiDto> LayHoSoGiaSuToiAsync(Guid giaSuId, CancellationToken cancellationToken = default);
    Task<HoSoGiaSuToiDto> CapNhatHoSoGiaSuToiAsync(Guid giaSuId, CapNhatHoSoGiaSuYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task<HoSoGiaSuToiDto> GuiDuyetHoSoAsync(Guid giaSuId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GiayToGiaSuDto>> LayDsGiayToToiAsync(Guid giaSuId, CancellationToken cancellationToken = default);
    Task<GiayToGiaSuDto> TaiLenGiayToAsync(Guid giaSuId, LoaiGiayTo loai, string tenTepGoc, Stream luongTep, long kichThuocByte, CancellationToken cancellationToken = default);
    Task XoaGiayToAsync(Guid giaSuId, Guid giayToId, CancellationToken cancellationToken = default);
    Task<UrlXemGiayToDto> TaoUrlXemGiayToAsync(Guid nguoiYeuCauId, VaiTro vaiTroNguoiYeuCau, Guid giayToId, CancellationToken cancellationToken = default);

    // Module 3 — Cấu hình Môn dạy & Lịch rảnh của tôi (GIA_SU)
    Task<IReadOnlyList<GiaSuMonHocDto>> CapNhatDsMonDayAsync(Guid giaSuId, CapNhatDsMonDayYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task<LichRanhGiaSuDto> LayLichRanhToiAsync(Guid giaSuId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LichLapHangTuanDto>> CapNhatLichLapHangTuanAsync(Guid giaSuId, CapNhatLichLapHangTuanYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task<LichNgayCuTheDto> ThemLichNgayCuTheAsync(Guid giaSuId, TaoLichNgayCuTheYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task<KiemTraXungDotNgayNghiKetQuaDto> KiemTraXungDotNgayNghiAsync(Guid giaSuId, DateOnly tuNgay, DateOnly denNgay, TimeOnly? gioBatDau = null, TimeOnly? gioKetThuc = null, CancellationToken cancellationToken = default);
    Task XoaLichNgayCuTheAsync(Guid giaSuId, Guid lichNgayCuTheId, CancellationToken cancellationToken = default);

    // Module 3 — Tìm kiếm Gia sư công khai, Khung giờ trống, Đánh giá & Mục yêu thích
    Task<KetQuaPhanTrangDto<ThongTinGiaSuTomTatDto>> TimKiemGiaSuAsync(BoLocTimKiemGiaSuDto boLoc, Guid? hocSinhHienTaiId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThongTinGiaSuTomTatDto>> LayDsGiaSuGoiYAsync(Guid? hocSinhHienTaiId = null, int soLuong = 6, CancellationToken cancellationToken = default);
    Task<ChiTietGiaSuCongKhaiDto> LayChiTietGiaSuCongKhaiAsync(Guid giaSuId, Guid? hocSinhHienTaiId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KhungGioTrongDto>> LayKhungGioTrongAsync(Guid giaSuId, DateOnly tuNgay, DateOnly denNgay, CancellationToken cancellationToken = default);
    Task<KetQuaPhanTrangDto<DanhGiaCongKhaiDto>> LayDsDanhGiaGiaSuAsync(Guid giaSuId, int trang = 1, int kichThuocTrang = 10, CancellationToken cancellationToken = default);

    // Mục yêu thích (HOC_SINH)
    Task<IReadOnlyList<MucYeuThichDto>> LayDsMucYeuThichAsync(Guid hocSinhId, CancellationToken cancellationToken = default);
    Task<MucYeuThichDto> ThemMucYeuThichAsync(Guid hocSinhId, Guid giaSuId, CancellationToken cancellationToken = default);
    Task XoaMucYeuThichAsync(Guid hocSinhId, Guid giaSuId, CancellationToken cancellationToken = default);
}
