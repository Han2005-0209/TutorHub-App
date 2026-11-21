namespace TutorHub.Domain.Enums;

/// <summary>
/// Trạng thái tài khoản người dùng (`03-class-v4.drawio`, mxCell id="88").
/// </summary>
public enum TrangThaiNguoiDung
{
    HOAT_DONG,
    BI_KHOA,
    DA_XOA
}

/// <summary>
/// Trạng thái xét duyệt hồ sơ gia sư (`03-class-v4.drawio`, mxCell id="89").
/// </summary>
public enum TrangThaiHoSoGiaSu
{
    NHAP,
    CHO_DUYET,
    DA_DUYET,
    TU_CHOI
}

/// <summary>
/// Hình thức buổi học (`03-class-v4.drawio`, mxCell id="91").
/// </summary>
public enum HinhThucBuoiHoc
{
    TRUC_TUYEN,
    TRUC_TIEP
}

/// <summary>
/// Hình thức tiếp nhận trên từng khung giờ lặp hằng tuần của Gia sư:
/// TRUC_TUYEN (Chỉ Online 1-1), TRUC_TIEP (Chỉ Tại nhà 1-1), CA_HAI (Cả hai hình thức - 1-1 ai đặt trước khoá hình thức đó).
/// </summary>
public enum HinhThucLichTuan
{
    TRUC_TUYEN,
    TRUC_TIEP,
    CA_HAI
}

/// <summary>
/// Loại lịch ngày cụ thể (`03-class-v4.drawio`, mxCell id="92").
/// </summary>
public enum LoaiLichNgayCuThe
{
    BO_SUNG,
    NGHI_PHEP
}

/// <summary>
/// Trạng thái đơn đặt lịch (`03-class-v4.drawio`, mxCell id="93").
/// </summary>
public enum TrangThaiDonDatLich
{
    CHO_XAC_NHAN,
    DA_XAC_NHAN,
    BI_TU_CHOI,
    HET_HAN,
    DA_HUY,
    HOAN_THANH,
    TRANH_CHAP
}

/// <summary>
/// Trạng thái giao dịch thanh toán (`03-class-v4.drawio`, mxCell id="94").
/// </summary>
public enum TrangThaiThanhToan
{
    CHO_THANH_TOAN,
    DANG_GIU,
    DA_GIAI_NGAN,
    DA_HOAN,
    THAT_BAI
}

/// <summary>
/// Phương thức thanh toán (`03-class-v4.drawio`, mxCell id="95").
/// </summary>
public enum PhuongThucThanhToan
{
    VNPAY,
    VI_NOI_BO,
    GIA_LAP
}

/// <summary>
/// Loại biến động số dư ví nội bộ (`03-class-v4.drawio`, mxCell id="96").
/// </summary>
public enum LoaiGiaoDichVi
{
    NAP_TIEN,
    THANH_TOAN,
    THU_NHAP,
    HOAN_TIEN,
    RUT_TIEN
}

/// <summary>
/// Loại mã giảm giá (`03-class-v4.drawio`, mxCell id="97").
/// </summary>
public enum LoaiMaGiamGia
{
    PHAN_TRAM,
    SO_TIEN_CO_DINH
}

/// <summary>
/// Loại thông báo hệ thống (`03-class-v4.drawio`, mxCell id="98").
/// </summary>
public enum LoaiThongBao
{
    DAT_LICH_MOI,
    XAC_NHAN_LICH,
    TU_CHOI_LICH,
    THANH_TOAN,
    KHIEU_NAI,
    DANH_GIA,
    HE_THONG
}

/// <summary>
/// Loại giấy tờ xác minh gia sư (`03-class-v4.drawio`, mxCell id="99").
/// </summary>
public enum LoaiGiayTo
{
    BANG_CAP,
    CHUNG_CHI,
    CAN_CUOC,
    KHAC
}

/// <summary>
/// Trạng thái duyệt giấy tờ gia sư (`03-class-v4.drawio`, mxCell id="100").
/// </summary>
public enum TrangThaiGiayTo
{
    CHO_DUYET,
    DA_DUYET,
    TU_CHOI
}

/// <summary>
/// Trạng thái yêu cầu rút tiền (`03-class-v4.drawio`, mxCell id="101").
/// </summary>
public enum TrangThaiRutTien
{
    CHO_XU_LY,
    DA_CHI_TRA,
    TU_CHOI
}

/// <summary>
/// Danh mục khiếu nại buổi học (chỉ áp dụng cho Học sinh trong đợt này).
/// </summary>
public enum DanhMucKhieuNai
{
    GIA_SU_VANG_MAT,
    CHAT_LUONG,
    THANH_TOAN,
    KHAC
}

/// <summary>
/// Trạng thái xử lý khiếu nại.
/// </summary>
public enum TrangThaiKhieuNai
{
    MO,
    DA_GIAI_QUYET,
    TU_CHOI
}

/// <summary>
/// Trạng thái yêu cầu nạp tiền vào Ví học tập qua VietQR (PayOS).
/// </summary>
public enum TrangThaiNapTien
{
    CHO_THANH_TOAN,
    THANH_CONG,
    DA_HUY,
    HET_HAN
}
