using TutorHub.Domain.Enums;

namespace TutorHub.Application.DTOs;

// ── Đặt lịch học (Module 4 — DonDatLich) ──

public sealed record TaoDonDatLichYeuCauDto(
    Guid GiaSuMonHocId,
    HinhThucBuoiHoc HinhThuc,
    string? DiaChiHocSinh,
    DateTime BatDauLuc,
    int ThoiLuongPhut,
    bool LaBuoiHocThu = false,
    string? MaGiamGia = null);

public sealed record LyDoDonDatLichYeuCauDto(
    string LyDo);

public sealed record DonDatLichChiTietDto(
    Guid Id,
    string MaDon,
    TrangThaiDonDatLich TrangThai,
    HinhThucBuoiHoc HinhThuc,
    string? DiaChiHocSinh,
    DateTime BatDauLuc,
    DateTime KetThucLuc,
    int GiaMoiGio,
    int TienGoc,
    int TienGiam,
    int TongTien,
    int PhiNenTang,
    int ThuNhapGiaSu,
    bool LaBuoiHocThu,
    string? PhongHocUrl,
    string? LyDoHuy,
    DateTime HanPhanHoi,
    DateTime TaoLuc,
    Guid GiaSuMonHocId,
    Guid MonHocId,
    string TenMonHoc,
    Guid GiaSuId,
    string TenGiaSu,
    Guid HocSinhId,
    string TenHocSinh,
    TrangThaiThanhToan? TrangThaiThanhToan = null,
    DanhGiaDonTomTatDto? DanhGia = null);

// ── Nhắn tin & Hội thoại (Module 8 — HoiThoai & TinNhan) ──

public sealed record MoHoiThoaiYeuCauDto(
    Guid GiaSuId);

public sealed record GuiTinNhanYeuCauDto(
    string NoiDung);

public sealed record TinNhanDto(
    Guid Id,
    Guid HoiThoaiId,
    Guid NguoiGuiId,
    string TenNguoiGui,
    bool LaCuaToi,
    string NoiDung,
    bool BiGanCo,
    DateTime? DocLuc,
    DateTime TaoLuc);

public sealed record HoiThoaiTomTatDto(
    Guid Id,
    Guid DoiTacId,
    string TenDoiTac,
    string? AnhDoiTac,
    VaiTro VaiTroDoiTac,
    string? TinNhanCuoi,
    DateTime? TinNhanCuoiLuc,
    int SoChuaDoc);

public sealed record HoiThoaiChiTietDto(
    Guid Id,
    Guid DoiTacId,
    string TenDoiTac,
    string? AnhDoiTac,
    VaiTro VaiTroDoiTac,
    IReadOnlyList<TinNhanDto> DsTinNhan);
