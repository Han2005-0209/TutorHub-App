using TutorHub.Domain.Enums;

namespace TutorHub.Application.DTOs;

public sealed record UocTinhDiChuyenDto(
    double KhoangCachKm,
    int ThoiGianDiChuyenPhut,
    int ThoiGianDemAnToanPhut,
    int KhoangTrongThucTePhut,
    bool LaGioCaoDiem,
    string MucDoKhaThi, // "THOAI_MAI" | "KHA_GAP" | "KHONG_KHA_THI"
    string ThongBaoKhaThi,
    string GoogleMapsUrl,
    string? DiaChiXuatPhat,
    string DiaChiDichDen);

public sealed record DonDatLichTomTatDto(
    Guid Id,
    string MaDon,
    Guid DoiTacId,
    string TenDoiTac,
    string TenMonHoc,
    HinhThucBuoiHoc HinhThuc,
    DateTime BatDauLuc,
    DateTime KetThucLuc,
    TrangThaiDonDatLich TrangThai,
    bool LaBuoiHocThu,
    int TongTien,
    int ThuNhapGiaSu,
    string? PhongHocUrl,
    TrangThaiThanhToan? TrangThaiThanhToan,
    string? DiaChiHocSinh = null,
    UocTinhDiChuyenDto? UocTinhDiChuyen = null,
    int PhiNenTang = 0);

public sealed record TongQuanHocSinhDto(
    Guid HocSinhId,
    string HoTen,
    string Email,
    string? KhuVuc,
    int SoDuVi,
    int SoBuoiSapToi,
    int SoBuoiHoanThanh,
    int SoGiaSuYeuThich,
    DonDatLichTomTatDto? BuoiHocSapToiGanNhat,
    IReadOnlyList<DonDatLichTomTatDto> DsDonDatLichGanDay);

public sealed record TongQuanGiaSuDto(
    Guid GiaSuId,
    string HoTen,
    string Email,
    string? KhuVuc,
    TrangThaiHoSoGiaSu TrangThaiHoSo,
    bool DaXacMinh,
    bool DayTrucTuyen,
    bool DayTrucTiep,
    int BanKinhPhucVuKm,
    int SoDuVi,
    int TongThuNhapDaHoanThanh,
    int SoBuoiSapToi,
    int SoBuoiHoanThanh,
    double DiemDanhGiaTrungBinh,
    int SoLuotDanhGia,
    int SoMonDay,
    int SoKhungGioHangTuan,
    int SoGiayTo,
    IReadOnlyList<DonDatLichTomTatDto> DsLichDayGanDay);

public sealed record TongQuanQuanTriDto(
    int TongNguoiDung,
    int TongHocSinh,
    int TongGiaSu,
    int GiaSuChoDuyet,
    int GiaSuDaDuyet,
    int GiayToChoDuyet,
    int TongMonHoc,
    int TongDonDatLich,
    int DonHoanThanh,
    int KhieuNaiDangMo,
    int YeuCauRutTienChoXuLy,
    int TongGiaTriGiaoDich);

public sealed record GiayToQuanTriDto(
    Guid Id,
    LoaiGiayTo Loai,
    string TenTepGoc,
    TrangThaiGiayTo TrangThai,
    DateTime? DuyetLuc,
    string? TenNguoiDuyet = null,
    string? EmailNguoiDuyet = null);

public sealed record GiaSuQuanTriDto(
    Guid Id,
    string HoTen,
    string Email,
    string? KhuVuc,
    string TieuDe,
    string HocVan,
    int SoNamKinhNghiem,
    bool DayTrucTuyen,
    bool DayTrucTiep,
    int BanKinhPhucVuKm,
    TrangThaiHoSoGiaSu TrangThaiHoSo,
    string? LyDoTuChoi,
    bool DaXacMinh,
    TrangThaiNguoiDung TrangThaiTaiKhoan,
    int SoMonDay,
    DateTime TaoLuc,
    IReadOnlyList<GiayToQuanTriDto> DsGiayTo,
    string? TenNguoiDuyet = null,
    string? EmailNguoiDuyet = null);

public sealed record DuyetHoSoGiaSuYeuCauDto(
    bool PheDuyet,
    string? LyDoTuChoi = null);

public sealed record DuyetGiayToYeuCauDto(
    bool PheDuyet);

public sealed record NguoiDungQuanTriDto(
    Guid Id,
    string HoTen,
    string Email,
    string? SoDienThoai,
    string? KhuVuc,
    VaiTro VaiTro,
    TrangThaiNguoiDung TrangThai,
    int? SoDuVi,
    DateTime TaoLuc);

public sealed record MonHocQuanTriDto(
    Guid Id,
    string Ten,
    string Slug,
    bool DangHoatDong,
    int SoGiaSuDangDay);

public sealed record TaoMonHocYeuCauDto(string Ten);

public sealed record CapNhatMonHocYeuCauDto(string Ten, bool DangHoatDong);

public sealed record DiemBieuDoThoiGianDto(
    string Nhan,
    string KhoaThoiGian,
    int TongDoanhThu,
    int PhiNenTang,
    int ThuNhapGiaSu,
    int TongNapVi,
    int TongRutTien,
    int TongHoanTien,
    int SoDonDatMoi,
    int SoBuoiHoanThanh,
    int HocSinhMoi,
    int GiaSuMoi);

public sealed record ThongKePhanBoMucDto(
    string Ma,
    string Nhan,
    int SoLuong,
    int GiaTri,
    double TyLePhanTram);

public sealed record TopGiaSuThongKeDto(
    Guid GiaSuId,
    string HoTen,
    string? KhuVuc,
    bool DaXacMinh,
    int SoBuoiHoanThanh,
    int TongDoanhThu,
    int ThuNhapGiaSu,
    double DiemDanhGiaTrungBinh,
    int SoLuotDanhGia);

public sealed record ThongKeChiTietQuanTriDto(
    string PhamVi,
    string NhanPhamVi,
    string DonViThoiGian,
    DateTime TuNgay,
    DateTime DenNgay,
    int TongDoanhThuTrongKy,
    double? TangTruongDoanhThuPhanTram,
    int TongPhiNenTangTrongKy,
    double? TangTruongPhiPhanTram,
    int TongThuNhapGiaSuTrongKy,
    int TongNapViTrongKy,
    int TongRutTienDaChiTraTrongKy,
    int TongHoanTienTrongKy,
    int SoDonDatTrongKy,
    double? TangTruongDonPhanTram,
    int SoDonHoanThanhTrongKy,
    double TyLeHoanThanhPhanTram,
    int GiaTriDonTrungBinh,
    int NguoiDungMoiTrongKy,
    double? TangTruongNguoiDungPhanTram,
    int TongSoDuViHeThong,
    int TongTienDangTamGiu,
    IReadOnlyList<DiemBieuDoThoiGianDto> BieuDoTheoThoiGian,
    IReadOnlyList<ThongKePhanBoMucDto> PhanBoTrangThaiDon,
    IReadOnlyList<ThongKePhanBoMucDto> TopMonHocTheoDoanhThu,
    IReadOnlyList<ThongKePhanBoMucDto> PhanBoHinhThucHoc,
    IReadOnlyList<ThongKePhanBoMucDto> PhanBoTrangThaiThanhToan,
    IReadOnlyList<TopGiaSuThongKeDto> TopGiaSu);
