using TutorHub.Application.DTOs;

namespace TutorHub.Application.Services;

/// <summary>
/// Dịch vụ nghiệp vụ Module 1 — Xác thực & Hồ sơ tài khoản (`UC_DangKy`, `UC_DangNhapXuat`, `UC_QuanLyHoSo`).
/// </summary>
public interface IXacThucService
{
    Task<KetQuaDangNhapDto> DangKyAsync(DangKyYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task<KetQuaDangNhapDto> DangNhapAsync(DangNhapYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task<KetQuaDangNhapDto> LamMoiTokenAsync(LamMoiTokenYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task DangXuatAsync(Guid nguoiDungId, CancellationToken cancellationToken = default);
    Task<NguoiDungHienTaiDto> XacThucEmailAsync(Guid? nguoiDungId, string token, CancellationToken cancellationToken = default);
    Task<KetQuaGuiEmailOtpDto> GuiLaiXacThucEmailAsync(Guid nguoiDungId, CancellationToken cancellationToken = default);
    Task<KetQuaGuiEmailOtpDto> QuenMatKhauAsync(string email, CancellationToken cancellationToken = default);
    Task DatLaiMatKhauAsync(DatLaiMatKhauYeuCauDto yeuCau, CancellationToken cancellationToken = default);

    Task<NguoiDungHienTaiDto> LayThongTinToiAsync(Guid nguoiDungId, CancellationToken cancellationToken = default);
    Task<NguoiDungHienTaiDto> CapNhatHoSoToiAsync(Guid nguoiDungId, CapNhatHoSoNguoiDungDto yeuCau, CancellationToken cancellationToken = default);
    Task<NguoiDungHienTaiDto> CapNhatAnhDaiDienAsync(Guid nguoiDungId, string anhDaiDienUrl, CancellationToken cancellationToken = default);
    Task DoiMatKhauAsync(Guid nguoiDungId, DoiMatKhauYeuCauDto yeuCau, CancellationToken cancellationToken = default);
    Task VoHieuHoaTaiKhoanAsync(Guid nguoiDungId, CancellationToken cancellationToken = default);
}
