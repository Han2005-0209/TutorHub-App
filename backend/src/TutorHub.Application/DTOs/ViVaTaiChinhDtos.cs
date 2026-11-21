using TutorHub.Domain.Enums;

namespace TutorHub.Application.DTOs;

public sealed record TaiKhoanNganHangCheDto(
    string TenNganHang,
    string SoTaiKhoanChe,
    string ChuTaiKhoan);

public sealed record TongQuanViGiaSuDto(
    int SoDuKhaDung,
    int SoDuSapNhan,
    int SoDuDangXuLyRut,
    int TongDaRut,
    decimal TyLePhiHienHanh,
    TaiKhoanNganHangCheDto? NganHang);

public sealed record GiaoDichViGiaSuDto(
    Guid Id,
    int SoThuTu,
    LoaiGiaoDichVi Loai,
    int SoTien,
    int SoDuTruoc,
    int SoDuSau,
    int? SoHocVienTra,
    int? PhiNenTang,
    decimal? TyLePhi,
    Guid? DonDatLichId,
    string? MaDon,
    string? TenHocVien,
    string? TenMonHoc,
    Guid? YeuCauRutTienId,
    string? TenNganHangRut,
    string? SoTaiKhoanRutChe,
    string? GhiChu,
    DateTime TaoLuc);

public sealed record YeuCauRutTienDto(
    Guid Id,
    Guid GiaSuId,
    string TenGiaSu,
    string EmailGiaSu,
    int SoTien,
    TrangThaiRutTien TrangThai,
    string TenNganHang,
    string SoTaiKhoanChe,
    string ChuTaiKhoan,
    string? GhiChu,
    DateTime TaoLuc,
    DateTime? XuLyLuc);

public sealed record TaoYeuCauRutTienDto(
    int SoTien,
    string MatKhauXacNhan,
    string? IdempotencyKey = null);

public sealed record KetQuaTaoYeuCauRutTienDto(
    YeuCauRutTienDto YeuCau,
    int SoDuKhaDungMoi,
    int SoDuDangXuLyRutMoi);

public sealed record CapNhatNganHangYeuCauDto(
    string TenNganHang,
    string SoTaiKhoan,
    string ChuTaiKhoan,
    string MatKhauXacNhan);

public sealed record XuLyYeuCauRutTienDto(
    bool Duyet,
    string? GhiChu = null);

public sealed record ThanhToanDonYeuCauDto(
    PhuongThucThanhToan PhuongThuc);

public sealed record ThanhToanKetQuaDto(
    Guid ThanhToanId,
    Guid DonDatLichId,
    int SoTien,
    PhuongThucThanhToan PhuongThuc,
    TrangThaiThanhToan TrangThai,
    string? MaGiaoDichCong,
    DateTime? ThanhToanLuc,
    int TienDaHoan,
    string? UrlChuyenHuong = null);

public sealed record MoKhieuNaiYeuCauDto(
    DanhMucKhieuNai DanhMuc,
    string MoTa,
    string? BangChungUrl = null);

public sealed record PhanHoiKhieuNaiGiaSuYeuCauDto(
    string PhanHoi,
    string? BangChungUrl = null);

public sealed record TepBangChungKetQuaDto(
    string DuongDanTep,
    string UrlXem);

public sealed record ThongBaoDto(
    Guid Id,
    LoaiThongBao Loai,
    string TieuDe,
    string NoiDung,
    DateTime? DocLuc,
    DateTime TaoLuc);

public sealed record GiaiQuyetKhieuNaiYeuCauDto(
    int SoTienHoan,
    string GhiChu);

public sealed record KhieuNaiChiTietDto(
    Guid Id,
    Guid DonDatLichId,
    string MaDon,
    Guid HocSinhId,
    string TenHocSinh,
    Guid GiaSuId,
    string TenGiaSu,
    string TenMonHoc,
    DanhMucKhieuNai DanhMuc,
    string MoTa,
    string? BangChungUrl,
    TrangThaiKhieuNai TrangThai,
    int SoTienThanhToan,
    int TienDaHoan,
    string? GhiChuGiaiQuyet,
    DateTime TaoLuc,
    DateTime? GiaiQuyetLuc,
    HinhThucBuoiHoc HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
    DateTime? BatDauLuc = null,
    DateTime? KetThucLuc = null,
    int ThoiLuongPhut = 60,
    int GiaMoiGioSnapshot = 0,
    int TienGiamSnapshot = 0,
    string? PhanHoiGiaSu = null,
    string? BangChungGiaSuUrl = null,
    DateTime? PhanHoiGiaSuLuc = null,
    string? UrlXemBangChungHocSinh = null,
    string? UrlXemBangChungGiaSu = null,
    decimal TyLePhiHienHanh = 12m,
    TomTatNhatKyPhongHocDto? NhatKyPhongHoc = null);

public sealed record TomTatNhatKyPhongHocDto(
    string TrangThaiMoLop,
    string MoTaTrangThaiMoLop,
    DateTime? GiaSuVaoDauTienLuc,
    DateTime? GiaSuRoiCuoiCungLuc,
    int? GiaSuTrePhut,
    int TongPhutGiaSuCoMat,
    DateTime? HocSinhVaoDauTienLuc,
    DateTime? HocSinhRoiCuoiCungLuc,
    int TongPhutHocSinhCoMat,
    int TongPhutHocCungNhau,
    int ThoiLuongDangKyPhut,
    bool Qua15PhutGiaSuChuaVao);

public sealed record ThongTinPhongHocDto(
    Guid DonDatLichId,
    string MaDon,
    string TenMonHoc,
    Guid GiaSuId,
    string TenGiaSu,
    Guid HocSinhId,
    string TenHocSinh,
    TrangThaiDonDatLich TrangThaiDon,
    DateTime BatDauLuc,
    DateTime KetThucLuc,
    int ThoiLuongPhut,
    string PhongHocUrl,
    string TenPhongJitsi,
    bool ChoPhepVaoPhong,
    string? LyDoChuaVaoDuoc,
    DateTime GioMayChuUtc,
    TomTatNhatKyPhongHocDto NhatKy);

public sealed record NhipPhongHocYeuCauDto(
    Guid PhienId,
    bool RoiPhong = false);

public sealed record PhienPhongHocKetQuaDto(
    Guid PhienId,
    DateTime VaoLuc,
    DateTime GioMayChuUtc,
    TomTatNhatKyPhongHocDto NhatKy);

public sealed record KetQuaChuKyTuDongDto(
    int SoDonDaGiaiNgan,
    int SoDonChuyenHetHan,
    int SoDonDaHoanTienKhiHetHan);
