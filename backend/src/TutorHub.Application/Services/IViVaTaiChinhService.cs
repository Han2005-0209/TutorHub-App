using TutorHub.Application.DTOs;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;

namespace TutorHub.Application.Services;

public interface IViVaTaiChinhService
{
    // API 1..5 — Cổng Ví Gia sư (chỉ GIA_SU)
    Task<TongQuanViGiaSuDto> LayTongQuanViGiaSuAsync(Guid giaSuId, CancellationToken cancellationToken = default);

    Task<KetQuaPhanTrangDto<GiaoDichViGiaSuDto>> LayDsGiaoDichViGiaSuAsync(
        Guid giaSuId,
        string? loai = null,
        DateOnly? tuNgay = null,
        DateOnly? denNgay = null,
        string? tuKhoa = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default);

    Task<GiaoDichViGiaSuDto> LayChiTietGiaoDichViGiaSuAsync(
        Guid giaSuId,
        Guid giaoDichId,
        CancellationToken cancellationToken = default);

    Task<KetQuaPhanTrangDto<YeuCauRutTienDto>> LayDsYeuCauRutTienGiaSuAsync(
        Guid giaSuId,
        TrangThaiRutTien? trangThai = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default);

    Task<KetQuaTaoYeuCauRutTienDto> TaoYeuCauRutTienAsync(
        Guid giaSuId,
        TaoYeuCauRutTienDto yeuCau,
        string? diaChiIp = null,
        CancellationToken cancellationToken = default);

    // API 6 — Đổi tài khoản ngân hàng có xác nhận mật khẩu + AuditLog
    Task<TaiKhoanNganHangCheDto> CapNhatNganHangGiaSuAsync(
        Guid giaSuId,
        CapNhatNganHangYeuCauDto yeuCau,
        string? diaChiIp = null,
        CancellationToken cancellationToken = default);

    // API 7..9 — Thanh toán, Xác nhận hoàn thành sớm & Khiếu nại (chỉ HOC_SINH)
    Task<ThanhToanKetQuaDto> ThanhToanDonAsync(
        Guid hocSinhId,
        Guid donId,
        ThanhToanDonYeuCauDto yeuCau,
        string? diaChiIp = null,
        CancellationToken cancellationToken = default);

    Task<DonDatLichChiTietDto> XacNhanHoanThanhSomAsync(
        Guid hocSinhId,
        Guid donId,
        CancellationToken cancellationToken = default);

    Task<KhieuNaiChiTietDto> MoKhieuNaiAsync(
        Guid hocSinhId,
        Guid donId,
        MoKhieuNaiYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    // Khiếu nại 2 chiều & Thông báo (HOC_SINH / GIA_SU / QUAN_TRI)
    Task<IReadOnlyList<KhieuNaiChiTietDto>> LayDsKhieuNaiCuaToiAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        CancellationToken cancellationToken = default);

    Task<KhieuNaiChiTietDto> PhanHoiKhieuNaiGiaSuAsync(
        Guid giaSuId,
        Guid khieuNaiId,
        PhanHoiKhieuNaiGiaSuYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    Task<TepBangChungKetQuaDto> TaiLenBangChungKhieuNaiAsync(
        Guid nguoiDungId,
        Stream luongTep,
        string tenTepGoc,
        long kichThuoc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ThongBaoDto>> LayDsThongBaoCuaToiAsync(
        Guid nguoiDungId,
        int gioiHan = 25,
        CancellationToken cancellationToken = default);

    Task DanhDauDaDocThongBaoAsync(
        Guid nguoiDungId,
        Guid? thongBaoId = null,
        CancellationToken cancellationToken = default);

    // Phòng học trực tuyến Mức 1 — Điểm danh giờ máy chủ & Heartbeat
    Task<ThongTinPhongHocDto> LayThongTinPhongHocAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        Guid donId,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default);

    Task<PhienPhongHocKetQuaDto> VaoPhongHocAsync(
        Guid nguoiDungId,
        VaiTro vaiTro,
        Guid donId,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default);

    Task<TomTatNhatKyPhongHocDto> CapNhatNhipPhongHocAsync(
        Guid nguoiDungId,
        Guid donId,
        NhipPhongHocYeuCauDto yeuCau,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default);

    // API 10..13 — Quản trị Rút tiền & Khiếu nại (chỉ QUAN_TRI)
    Task<KetQuaPhanTrangDto<YeuCauRutTienDto>> LayDsYeuCauRutTienQuanTriAsync(
        TrangThaiRutTien? trangThai = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default);

    Task<YeuCauRutTienDto> XuLyYeuCauRutTienAsync(
        Guid quanTriId,
        Guid yeuCauId,
        XuLyYeuCauRutTienDto yeuCau,
        CancellationToken cancellationToken = default);

    Task<KetQuaPhanTrangDto<KhieuNaiChiTietDto>> LayDsKhieuNaiQuanTriAsync(
        TrangThaiKhieuNai? trangThai = null,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default);

    Task<KhieuNaiChiTietDto> GiaiQuyetKhieuNaiAsync(
        Guid quanTriId,
        Guid khieuNaiId,
        GiaiQuyetKhieuNaiYeuCauDto yeuCau,
        CancellationToken cancellationToken = default);

    // Nội bộ: hoàn 100% về ví học sinh trong transaction hiện tại (dùng khi từ chối / hủy / hết hạn)
    Task HoanToanBoVeViHocSinhTrongGiaoDichAsync(
        ThanhToan thanhToan,
        Guid hocSinhId,
        string ghiChu,
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default);

    // Background Job định kỳ (tự giải ngân sau AutoDisbursementHours & hết hạn đơn quá hạn)
    Task<KetQuaChuKyTuDongDto> ChayChuKyTuDongXuLyDonVaGiaiNganAsync(
        DateTime? mocThoiGianUtc = null,
        CancellationToken cancellationToken = default);

    // Ví Học sinh & Nạp tiền VietQR tự động (PayOS API v2)
    Task<TongQuanViHocSinhDto> LayTongQuanViHocSinhAsync(
        Guid hocSinhId,
        CancellationToken cancellationToken = default);

    Task<KetQuaPhanTrangDto<GiaoDichViHocSinhDto>> LayDsGiaoDichViHocSinhAsync(
        Guid hocSinhId,
        int trang = 1,
        int kichThuocTrang = 20,
        CancellationToken cancellationToken = default);

    Task<YeuCauNapTienDto> TaoYeuCauNapTienVietQrAsync(
        Guid hocSinhId,
        TaoYeuCauNapTienDto yeuCau,
        string? originUrl = null,
        CancellationToken cancellationToken = default);

    Task<YeuCauNapTienDto> KiemTraVaDongBoNapTienAsync(
        Guid hocSinhId,
        Guid yeuCauNapTienId,
        CancellationToken cancellationToken = default);

    Task<YeuCauNapTienDto> HuyYeuCauNapTienAsync(
        Guid hocSinhId,
        Guid yeuCauNapTienId,
        CancellationToken cancellationToken = default);

    Task<bool> XuLyWebhookPayOsAsync(
        string rawJsonBody,
        CancellationToken cancellationToken = default);
}
