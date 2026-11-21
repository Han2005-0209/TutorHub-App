using TutorHub.Domain.Enums;

namespace TutorHub.Domain.Entities;

/// <summary>
/// Đơn đặt lịch học giữa Học sinh và Gia sư (`03-class-v4.drawio`, mxCell id="55").
/// Giữ 2 cột snapshot tài chính `GiaMoiGio`, `TienGiam`; phí nền tảng & thu nhập gia sư tính duy nhất qua `BoTinhTaiChinhDon.TinhTaiChinhDon`.
/// </summary>
public sealed class DonDatLich : ThucTheGoc
{
    public string MaDon { get; set; } = string.Empty;

    public Guid HocSinhId { get; set; }
    public HocSinh? HocSinh { get; set; }

    public Guid GiaSuMonHocId { get; set; }
    public GiaSuMonHoc? GiaSuMonHoc { get; set; }

    public Guid? MaGiamGiaId { get; set; }
    public MaGiamGia? MaGiamGia { get; set; }

    public HinhThucBuoiHoc HinhThuc { get; set; }
    public string? DiaChiHocSinh { get; set; }
    public DateTime BatDauLuc { get; set; }
    public DateTime KetThucLuc { get; set; }
    public int GiaMoiGio { get; set; }
    public int TienGiam { get; set; }
    public TrangThaiDonDatLich TrangThai { get; set; } = TrangThaiDonDatLich.CHO_XAC_NHAN;
    public bool LaBuoiHocThu { get; set; }
    public string? PhongHocUrl { get; set; }
    public string? LyDoHuy { get; set; }
    public DateTime HanPhanHoi { get; set; }
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;

    public ICollection<ThanhToan> DsThanhToan { get; set; } = new List<ThanhToan>();
    public ICollection<KhieuNai> DsKhieuNai { get; set; } = new List<KhieuNai>();
    public ICollection<NhatKyPhongHoc> DsNhatKyPhongHoc { get; set; } = new List<NhatKyPhongHoc>();
    public DanhGia? DanhGia { get; set; }

    /// <summary>
    /// Tiền gốc trước khi trừ giảm giá (buổi học thử Online luôn miễn phí `0`).
    /// </summary>
    public int TienGoc
    {
        get
        {
            if (LaBuoiHocThu)
            {
                return Math.Max(0, GiaMoiGio);
            }

            var soPhut = Math.Max(0.0, (KetThucLuc - BatDauLuc).TotalMinutes);
            return Math.Max(0, (int)Math.Round(GiaMoiGio * soPhut / 60.0, MidpointRounding.AwayFromZero));
        }
    }

    /// <summary>
    /// Thuộc tính dẫn xuất `- /tongTien : Integer` (BR-FIN-03). KHÔNG tạo cột vật lý.
    /// </summary>
    public int TongTien => Math.Max(0, TienGoc - Math.Max(0, TienGiam));
}

/// <summary>
/// Mã giảm giá (`03-class-v4.drawio`, mxCell id="51").
/// Thuộc tính `/soLanSuDung` dẫn xuất từ `COUNT(DsDonDatLich)`.
/// </summary>
public sealed class MaGiamGia : ThucTheGoc
{
    public string Ma { get; set; } = string.Empty;
    public LoaiMaGiamGia Loai { get; set; }
    public int GiaTri { get; set; }
    public int GiaTriDonToiThieu { get; set; }
    public int SoLuotToiDa { get; set; }
    public DateTime BatDauLuc { get; set; }
    public DateTime HetHanLuc { get; set; }
    public bool DangHoatDong { get; set; } = true;

    public ICollection<DonDatLich> DsDonDatLich { get; set; } = new List<DonDatLich>();

    /// <summary>
    /// Thuộc tính dẫn xuất `- /soLanSuDung : Integer`. KHÔNG tạo cột vật lý.
    /// </summary>
    public int SoLanSuDung => DsDonDatLich.Count;
}

/// <summary>
/// Giao dịch Thanh toán cho Đơn đặt lịch (`03-class-v4.drawio`, mxCell id="47").
/// Giữ cột snapshot `TienDaHoan`.
/// </summary>
public sealed class ThanhToan : ThucTheGoc
{
    public Guid DonDatLichId { get; set; }
    public DonDatLich? DonDatLich { get; set; }

    public int SoTien { get; set; }
    public PhuongThucThanhToan PhuongThuc { get; set; }
    public TrangThaiThanhToan TrangThai { get; set; } = TrangThaiThanhToan.CHO_THANH_TOAN;
    public string? MaGiaoDichCong { get; set; }
    public DateTime? ThanhToanLuc { get; set; }
    public DateTime? GiaiNganLuc { get; set; }
    public int TienDaHoan { get; set; }
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tài khoản Ví nội bộ của `NguoiDungThamGia` (`03-class-v4.drawio`, mxCell id="11").
/// Quan hệ Composition `NguoiDungThamGia 1 ◆—> 1 TaiKhoanVi`.
/// </summary>
public sealed class TaiKhoanVi : ThucTheGoc
{
    public Guid NguoiDungThamGiaId { get; set; }
    public NguoiDungThamGia? NguoiDungThamGia { get; set; }

    public int SoDu { get; set; }
    public int SoDuDangXuLyRut { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;

    public ICollection<GiaoDichVi> DsGiaoDich { get; set; } = new List<GiaoDichVi>();
}

/// <summary>
/// Sổ cái biến động số dư Ví nội bộ (`03-class-v4.drawio`, mxCell id="15").
/// Quan hệ Composition `TaiKhoanVi 1 ◆—— 0..* GiaoDichVi`. Append-only; giữ `SoThuTu` tuần tự theo ví và snapshot `SoDuSau`.
/// </summary>
public sealed class GiaoDichVi : ThucTheGoc
{
    public Guid TaiKhoanViId { get; set; }
    public TaiKhoanVi? TaiKhoanVi { get; set; }

    public int SoThuTu { get; set; }
    public LoaiGiaoDichVi Loai { get; set; }
    public int SoTien { get; set; }
    public int SoDuSau { get; set; }
    public string? GhiChu { get; set; }

    public Guid? ThanhToanId { get; set; }
    public ThanhToan? ThanhToan { get; set; }

    public Guid? YeuCauRutTienId { get; set; }
    public YeuCauRutTien? YeuCauRutTien { get; set; }

    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Yêu cầu rút tiền từ Ví nội bộ của Gia sư (`03-class-v4.drawio`, mxCell id="75").
/// Lưu snapshot tài khoản ngân hàng tại thời điểm tạo lệnh rút và IdempotencyKey chống bấm trùng.
/// </summary>
public sealed class YeuCauRutTien : ThucTheGoc
{
    public Guid GiaSuId { get; set; }
    public GiaSu? GiaSu { get; set; }

    public int SoTien { get; set; }
    public TrangThaiRutTien TrangThai { get; set; } = TrangThaiRutTien.CHO_XU_LY;
    public DateTime? XuLyLuc { get; set; }
    public string? GhiChu { get; set; }

    public string TenNganHang { get; set; } = string.Empty;
    public string SoTaiKhoan { get; set; } = string.Empty;
    public string ChuTaiKhoan { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }

    public Guid? NguoiXuLyId { get; set; }
    public QuanTriVien? NguoiXuLy { get; set; }

    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Đánh giá buổi học đã hoàn thành (`03-class-v4.drawio`, mxCell id="83").
/// Ràng buộc NOTE 408: `Diem ∈ 1..5`, mỗi `DonDatLich` tối đa `0..1` `DanhGia`.
/// </summary>
public sealed class DanhGia : ThucTheGoc
{
    public Guid DonDatLichId { get; set; }
    public DonDatLich? DonDatLich { get; set; }

    public int Diem { get; set; }
    public string BinhLuan { get; set; } = string.Empty;
    public string? PhanHoiGiaSu { get; set; }
    public string? LyDoAn { get; set; }

    public Guid? NguoiKiemDuyetId { get; set; }
    public QuanTriVien? NguoiKiemDuyet { get; set; }

    /// <summary>
    /// Thuộc tính dẫn xuất từ `NguoiKiemDuyetId != null`. KHÔNG tạo cột vật lý.
    /// </summary>
    public bool BiAn => NguoiKiemDuyetId.HasValue;

    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Khiếu nại tranh chấp buổi học (chỉ Học sinh chủ đơn mở khiếu nại; người khiếu nại suy từ `DonDatLich.HocSinhId`).
/// </summary>
public sealed class KhieuNai : ThucTheGoc
{
    public Guid DonDatLichId { get; set; }
    public DonDatLich? DonDatLich { get; set; }

    public DanhMucKhieuNai DanhMuc { get; set; }
    public string MoTa { get; set; } = string.Empty;
    public string? BangChungUrl { get; set; }
    public string? PhanHoiGiaSu { get; set; }
    public string? BangChungGiaSuUrl { get; set; }
    public DateTime? PhanHoiGiaSuLuc { get; set; }
    public TrangThaiKhieuNai TrangThai { get; set; } = TrangThaiKhieuNai.MO;
    public DateTime? GiaiQuyetLuc { get; set; }
    public string? GhiChuGiaiQuyet { get; set; }

    public Guid? NguoiGiaiQuyetId { get; set; }
    public QuanTriVien? NguoiGiaiQuyet { get; set; }

    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Nhật ký điểm danh & thời lượng có mặt thực tế trong phòng học trực tuyến (theo giờ máy chủ).
/// Tách thành bảng 1:N với DonDatLich để lưu nhiều phiên ra/vào (khi rớt mạng) và tính chính xác thời lượng học cùng nhau.
/// </summary>
public sealed class NhatKyPhongHoc : ThucTheGoc
{
    public Guid DonDatLichId { get; set; }
    public DonDatLich? DonDatLich { get; set; }

    public Guid NguoiDungId { get; set; }
    public NguoiDung? NguoiDung { get; set; }

    public VaiTro VaiTroLucVao { get; set; }
    public DateTime VaoLuc { get; set; } = DateTime.UtcNow;
    public DateTime CapNhatCuoiLuc { get; set; } = DateTime.UtcNow;
    public bool DaRoiPhong { get; set; }
}
