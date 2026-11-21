using TutorHub.Domain.Enums;

namespace TutorHub.Application.DTOs;

public sealed record MonHocDto(
    Guid Id,
    string Ten,
    string Slug,
    bool DangHoatDong);

/// <summary>
/// Đặc tả tường minh từng hình thức học (Học Trực tuyến vs. Học Trực tiếp tại nhà) cho cả Backend & Frontend.
/// </summary>
public sealed record ThongTinHinhThucHocDto(
    HinhThucBuoiHoc Ma,
    string TenHienThi,
    string KhauHieu,
    string MoTaNgan,
    string PhamViDiaLy,
    string KhongGianHoc,
    string ChinhSachLienHe,
    bool ApDungBanKinhDiChuyen,
    bool CoPhongHocVideoTichHop,
    IReadOnlyList<string> UuDiemNoiBat,
    IReadOnlyList<string> YeuCauChuanBi);

public sealed record GiaSuMonHocDto(
    Guid Id,
    Guid MonHocId,
    string TenMonHoc,
    int GiaMoiGio);

public sealed record CapNhatMonDayItemDto(
    Guid MonHocId,
    int GiaMoiGio);

public sealed record CapNhatDsMonDayYeuCauDto(
    IReadOnlyList<CapNhatMonDayItemDto> DsMonDay);

public sealed record TaiKhoanNganHangDto(
    string TenNganHang,
    string SoTaiKhoan,
    string ChuTaiKhoan);

public sealed record GiayToGiaSuDto(
    Guid Id,
    LoaiGiayTo Loai,
    string TenTepGoc,
    string DuongDanTep,
    TrangThaiGiayTo TrangThai,
    DateTime? DuyetLuc);

public sealed record UrlXemGiayToDto(
    Guid GiayToId,
    string TenTepGoc,
    LoaiGiayTo Loai,
    string UrlXem,
    DateTime HetHanLuc);

public sealed record LichLapHangTuanDto(
    Guid Id,
    int ThuTrongTuan,
    string GioBatDau,
    string GioKetThuc,
    HinhThucLichTuan HinhThucLich = HinhThucLichTuan.CA_HAI);

public sealed record CapNhatLichLapItemDto(
    int ThuTrongTuan,
    string GioBatDau,
    string GioKetThuc,
    HinhThucLichTuan HinhThucLich = HinhThucLichTuan.CA_HAI);

public sealed record CapNhatLichLapHangTuanYeuCauDto(
    IReadOnlyList<CapNhatLichLapItemDto> DsLichLapHangTuan,
    int? ThoiGianDemPhut = null,
    int? ThoiLuongToiThieuTaiNhaPhut = null);

public sealed record LichNgayCuTheDto(
    Guid Id,
    string NgayCuThe,
    LoaiLichNgayCuThe Loai,
    string? GioBatDau,
    string? GioKetThuc,
    string? NgayKetThuc = null,
    string? LyDo = null);

public sealed record TaoLichNgayCuTheYeuCauDto(
    string NgayCuThe,
    LoaiLichNgayCuThe Loai,
    string? GioBatDau,
    string? GioKetThuc,
    string? NgayKetThuc = null,
    string? LyDo = null);

public sealed record XungDotBuoiHocDto(
    Guid DonDatLichId,
    string MaDon,
    string TenHocSinh,
    string Ngay,
    string GioBatDau,
    string GioKetThuc,
    HinhThucBuoiHoc HinhThuc,
    TrangThaiDonDatLich TrangThai);

public sealed record KiemTraXungDotNgayNghiKetQuaDto(
    bool CoXungDot,
    int SoLuongXungDot,
    IReadOnlyList<XungDotBuoiHocDto> DsXungDot);

public sealed record LichRanhGiaSuDto(
    IReadOnlyList<LichLapHangTuanDto> DsLichLapHangTuan,
    IReadOnlyList<LichNgayCuTheDto> DsLichNgayCuThe,
    int ThoiGianDemPhut = 30,
    int ThoiLuongToiThieuTaiNhaPhut = 90);

public sealed record KhungGioTrongDto(
    string Ngay,
    int ThuTrongTuan,
    string GioBatDau,
    string GioKetThuc);

public sealed record HoSoGiaSuToiDto(
    Guid Id,
    string Email,
    string HoTen,
    string? SoDienThoai,
    string? AnhDaiDienUrl,
    string? KhuVuc,
    string TieuDe,
    string GioiThieu,
    string HocVan,
    int SoNamKinhNghiem,
    IReadOnlyList<HinhThucBuoiHoc> HinhThucDay,
    bool CoDayTrucTuyen,
    bool CoDayTrucTiep,
    string MoTaHinhThucDay,
    TrangThaiHoSoGiaSu TrangThaiHoSo,
    string? LyDoTuChoi,
    bool DaXacMinh,
    bool CoHocThu,
    int BanKinhPhucVuKm,
    int ThoiGianDemPhut,
    int ThoiLuongToiThieuTaiNhaPhut,
    double DiemDanhGiaTrungBinh,
    int SoLuotDanhGia,
    int SoBuoiHoanThanh,
    int SoDuVi,
    TaiKhoanNganHangDto? TaiKhoanNganHang,
    IReadOnlyList<GiaSuMonHocDto> DsMonDay,
    IReadOnlyList<GiayToGiaSuDto> DsGiayTo,
    LichRanhGiaSuDto LichRanh);

public sealed record CapNhatHoSoGiaSuYeuCauDto(
    string? HoTen,
    string? SoDienThoai,
    string? KhuVuc,
    string TieuDe,
    string GioiThieu,
    string HocVan,
    int SoNamKinhNghiem,
    IReadOnlyList<HinhThucBuoiHoc> HinhThucDay,
    bool CoHocThu,
    int BanKinhPhucVuKm = 10,
    int? ThoiGianDemPhut = null,
    int? ThoiLuongToiThieuTaiNhaPhut = null);

public sealed record ThongTinGiaSuTomTatDto(
    Guid Id,
    string HoTen,
    string? AnhDaiDienUrl,
    string? KhuVuc,
    string TieuDe,
    string GioiThieu,
    string HocVan,
    int SoNamKinhNghiem,
    IReadOnlyList<HinhThucBuoiHoc> HinhThucDay,
    bool CoDayTrucTuyen,
    bool CoDayTrucTiep,
    string MoTaHinhThucDay,
    bool DaXacMinh,
    bool CoHocThu,
    int BanKinhPhucVuKm,
    int GiaThapNhat,
    int GiaCaoNhat,
    double DiemDanhGiaTrungBinh,
    int SoLuotDanhGia,
    int SoBuoiHoanThanh,
    double DiemXepHang,
    double? ViDoMo,
    double? KinhDoMo,
    bool DaYeuThich,
    IReadOnlyList<GiaSuMonHocDto> DsMonDay);

public sealed record DanhGiaCongKhaiDto(
    Guid Id,
    Guid DonDatLichId,
    string TenHocSinh,
    string? AnhDaiDienHocSinhUrl,
    string TenMonHoc,
    int Diem,
    string BinhLuan,
    string? PhanHoiGiaSu,
    DateTime TaoLuc);

public sealed record ChiTietGiaSuCongKhaiDto(
    Guid Id,
    string HoTen,
    string? AnhDaiDienUrl,
    string? KhuVuc,
    string TieuDe,
    string GioiThieu,
    string HocVan,
    int SoNamKinhNghiem,
    IReadOnlyList<HinhThucBuoiHoc> HinhThucDay,
    bool CoDayTrucTuyen,
    bool CoDayTrucTiep,
    string MoTaHinhThucDay,
    bool DaXacMinh,
    bool CoHocThu,
    int BanKinhPhucVuKm,
    int GiaThapNhat,
    int GiaCaoNhat,
    double DiemDanhGiaTrungBinh,
    int SoLuotDanhGia,
    int SoBuoiHoanThanh,
    double? ViDoMo,
    double? KinhDoMo,
    bool DaYeuThich,
    IReadOnlyList<GiaSuMonHocDto> DsMonDay,
    IReadOnlyList<LichLapHangTuanDto> DsLichLapHangTuan,
    IReadOnlyList<DanhGiaCongKhaiDto> DsDanhGiaGanDay,
    IReadOnlyList<LoaiGiayTo>? DsLoaiGiayToDaDuyet = null,
    bool DuDieuKienHocThuLanDau = false);

public sealed record BoLocTimKiemGiaSuDto(
    string? TuKhoa = null,
    Guid? MonHocId = null,
    HinhThucBuoiHoc? HinhThuc = null,
    string? KhuVuc = null,
    int? GiaToiThieu = null,
    int? GiaToiDa = null,
    bool? ChiDaXacMinh = null,
    bool? CoHocThu = null,
    string? SapXep = "phu_hop",
    int Trang = 1,
    int KichThuocTrang = 12,
    double? ViDo = null,
    double? KinhDo = null,
    double? BanKinhQuetKm = null);

public sealed record KetQuaPhanTrangDto<T>(
    IReadOnlyList<T> DuLieu,
    int Trang,
    int KichThuocTrang,
    int Tong,
    int TongTrang);

public sealed record MucYeuThichDto(
    Guid GiaSuId,
    DateTime TaoLuc,
    ThongTinGiaSuTomTatDto GiaSu);
