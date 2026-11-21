using TutorHub.Domain.Enums;

namespace TutorHub.Domain.Entities;

/// <summary>
/// Lớp trừu tượng gốc của cây kế thừa người dùng (`03-class-v4.drawio`, mxCell id="7").
/// Ánh xạ TPT (Table-per-Type) sang bảng `NguoiDung`.
/// </summary>
public abstract class NguoiDung : ThucTheGoc
{
    public string Email { get; set; } = string.Empty;
    public string MatKhauHash { get; set; } = string.Empty;
    public string HoTen { get; set; } = string.Empty;
    public string? SoDienThoai { get; set; }
    public string? AnhDaiDienUrl { get; set; }
    public string? KhuVuc { get; set; }
    public TrangThaiNguoiDung TrangThai { get; set; } = TrangThaiNguoiDung.HOAT_DONG;
    public DateTime? XacThucEmailLuc { get; set; }
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thuộc tính dẫn xuất `# /vaiTro : VaiTro {readOnly}` — suy ra từ lớp con cụ thể, KHÔNG tạo cột vật lý.
    /// </summary>
    public abstract VaiTro VaiTro { get; }

    public void CapNhatHoSo(string hoTen, string? soDienThoai, string? khuVuc)
    {
        HoTen = hoTen.Trim();
        SoDienThoai = string.IsNullOrWhiteSpace(soDienThoai) ? null : soDienThoai.Trim();
        KhuVuc = string.IsNullOrWhiteSpace(khuVuc) ? null : khuVuc.Trim();
    }

    public void DoiMatKhau(string matKhauHashMoi)
    {
        MatKhauHash = matKhauHashMoi;
    }

    public void VoHieuHoaTaiKhoan()
    {
        TrangThai = TrangThaiNguoiDung.DA_XOA;
    }
}

/// <summary>
/// Lớp trừu tượng đại diện cho thành viên tham gia giao dịch học tập (`03-class-v4.drawio`, mxCell id="19").
/// Sở hữu đúng 1 `TaiKhoanVi` theo quan hệ Composition (`◆—>`).
/// </summary>
public abstract class NguoiDungThamGia : NguoiDung
{
    public TaiKhoanVi? TaiKhoanVi { get; set; }
}

/// <summary>
/// Lớp thực thể Quản trị viên (`03-class-v4.drawio`, mxCell id="23").
/// </summary>
public sealed class QuanTriVien : NguoiDung
{
    public override VaiTro VaiTro => VaiTro.QUAN_TRI;
}

/// <summary>
/// Lớp thực thể Học sinh (`03-class-v4.drawio`, mxCell id="27").
/// </summary>
public sealed class HocSinh : NguoiDungThamGia
{
    public override VaiTro VaiTro => VaiTro.HOC_SINH;

    public ICollection<MucYeuThich> DsMucYeuThich { get; set; } = new List<MucYeuThich>();
    public ICollection<DonDatLich> DsDonDatLich { get; set; } = new List<DonDatLich>();
}

/// <summary>
/// Lớp thực thể Gia sư (`03-class-v4.drawio`, mxCell id="35").
/// </summary>
public sealed class GiaSu : NguoiDungThamGia
{
    public override VaiTro VaiTro => VaiTro.GIA_SU;

    public string TieuDe { get; set; } = string.Empty;
    public string GioiThieu { get; set; } = string.Empty;
    public string HocVan { get; set; } = string.Empty;
    public int SoNamKinhNghiem { get; set; }
    public bool DayTrucTuyen { get; set; } = true;
    public bool DayTrucTiep { get; set; }
    public TrangThaiHoSoGiaSu TrangThaiHoSo { get; set; } = TrangThaiHoSoGiaSu.NHAP;
    public string? LyDoTuChoi { get; set; }
    public bool CoHocThu { get; set; }
    public int BanKinhPhucVuKm { get; set; } = 10;
    public int ThoiGianDemPhut { get; set; } = 30;
    public int ThoiLuongToiThieuTaiNhaPhut { get; set; } = 90;

    public Guid? NguoiDuyetId { get; set; }
    public QuanTriVien? NguoiDuyet { get; set; }

    // 4 quan hệ Composition (◆) của GiaSu
    public ICollection<GiaSuMonHoc> DsMonDay { get; set; } = new List<GiaSuMonHoc>();
    public ICollection<LichRanh> DsLichRanh { get; set; } = new List<LichRanh>();
    public ICollection<GiayToGiaSu> DsGiayTo { get; set; } = new List<GiayToGiaSu>();
    public TaiKhoanNganHang? TaiKhoanNganHang { get; set; }

    public ICollection<MucYeuThich> DsMucYeuThich { get; set; } = new List<MucYeuThich>();
    public ICollection<YeuCauRutTien> DsYeuCauRutTien { get; set; } = new List<YeuCauRutTien>();

    /// <summary>
    /// Tập hợp `hinhThucDay : HinhThucBuoiHoc [1..2] {unique}` ánh xạ 1NF từ 2 cột BIT `DayTrucTuyen`, `DayTrucTiep`.
    /// </summary>
    public IReadOnlyList<HinhThucBuoiHoc> HinhThucDay
    {
        get
        {
            var ds = new List<HinhThucBuoiHoc>(2);
            if (DayTrucTuyen) ds.Add(HinhThucBuoiHoc.TRUC_TUYEN);
            if (DayTrucTiep) ds.Add(HinhThucBuoiHoc.TRUC_TIEP);
            if (ds.Count == 0) ds.Add(HinhThucBuoiHoc.TRUC_TUYEN);
            return ds;
        }
    }

    public void DatHinhThucDay(IEnumerable<HinhThucBuoiHoc>? dsHinhThuc)
    {
        var tap = dsHinhThuc?.Distinct().ToHashSet() ?? [];
        if (tap.Count == 0)
        {
            DayTrucTuyen = true;
            DayTrucTiep = false;
            return;
        }

        DayTrucTuyen = tap.Contains(HinhThucBuoiHoc.TRUC_TUYEN);
        DayTrucTiep = tap.Contains(HinhThucBuoiHoc.TRUC_TIEP);
    }

    /// <summary>
    /// Thuộc tính dẫn xuất `- /daXacMinh : Boolean` (BR-TUT-02) — chỉ true khi có ít nhất 1 giấy tờ
    /// thuộc loại `BANG_CAP` hoặc `CHUNG_CHI` ở trạng thái `DA_DUYET`. KHÔNG tạo cột vật lý.
    /// </summary>
    public bool DaXacMinh =>
        DsGiayTo.Any(g =>
            g.TrangThai == TrangThaiGiayTo.DA_DUYET &&
            (g.Loai == LoaiGiayTo.BANG_CAP || g.Loai == LoaiGiayTo.CHUNG_CHI));

    /// <summary>
    /// Thuộc tính dẫn xuất `- /dsDonDatLich : DonDatLich[*]` qua `DsMonDay`. KHÔNG tạo cột vật lý.
    /// </summary>
    public IEnumerable<DonDatLich> DsDonDatLich =>
        DsMonDay.SelectMany(m => m.DsDonDatLich);

    /// <summary>
    /// Thuộc tính dẫn xuất `- /soBuoiHoanThanh : Integer`. KHÔNG tạo cột vật lý.
    /// </summary>
    public int SoBuoiHoanThanh =>
        DsDonDatLich.Count(d => d.TrangThai == TrangThaiDonDatLich.HOAN_THANH);

    /// <summary>
    /// Thuộc tính dẫn xuất `- /soLuotDanhGia : Integer` (trên các đánh giá chưa bị ẩn `NguoiKiemDuyetId == null`). KHÔNG tạo cột vật lý.
    /// </summary>
    public int SoLuotDanhGia =>
        DsDonDatLich.Count(d => d.DanhGia is { NguoiKiemDuyetId: null });

    /// <summary>
    /// Thuộc tính dẫn xuất `- /diemDanhGiaTrungBinh : Float`. KHÔNG tạo cột vật lý.
    /// </summary>
    public double DiemDanhGiaTrungBinh
    {
        get
        {
            var dsDiem = DsDonDatLich
                .Where(d => d.DanhGia is { NguoiKiemDuyetId: null })
                .Select(d => d.DanhGia!.Diem)
                .ToList();

            return dsDiem.Count == 0
                ? 0.0
                : Math.Round(dsDiem.Average(), 2, MidpointRounding.AwayFromZero);
        }
    }
}

/// <summary>
/// Lớp liên kết (Association Class) `MucYeuThich` giữa `HocSinh` và `GiaSu` (`03-class-v4.drawio`, mxCell id="31").
/// Khóa chính kết hợp `(HocSinhId, GiaSuId)`.
/// </summary>
public sealed class MucYeuThich
{
    public Guid HocSinhId { get; set; }
    public HocSinh? HocSinh { get; set; }

    public Guid GiaSuId { get; set; }
    public GiaSu? GiaSu { get; set; }

    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tài khoản ngân hàng nhận chi trả của Gia sư (`03-class-v4.drawio`, mxCell id="356").
/// Quan hệ Composition `GiaSu 1 ◆—> 0..1 TaiKhoanNganHang`, khóa tự nhiên `(TenNganHang, SoTaiKhoan)`.
/// </summary>
public sealed class TaiKhoanNganHang
{
    public string TenNganHang { get; set; } = string.Empty;
    public string SoTaiKhoan { get; set; } = string.Empty;
    public string ChuTaiKhoan { get; set; } = string.Empty;

    public Guid GiaSuId { get; set; }
    public GiaSu? GiaSu { get; set; }
}

/// <summary>
/// Giấy tờ xác minh của Gia sư (`03-class-v4.drawio`, mxCell id="71").
/// Quan hệ Composition `GiaSu 1 ◆—> 0..* GiayToGiaSu`. Lưu trong kho Private.
/// </summary>
public sealed class GiayToGiaSu : ThucTheGoc
{
    public Guid GiaSuId { get; set; }
    public GiaSu? GiaSu { get; set; }

    public LoaiGiayTo Loai { get; set; }
    public string DuongDanTep { get; set; } = string.Empty;
    public string TenTepGoc { get; set; } = string.Empty;
    public TrangThaiGiayTo TrangThai { get; set; } = TrangThaiGiayTo.CHO_DUYET;
    public DateTime? DuyetLuc { get; set; }

    public Guid? NguoiDuyetId { get; set; }
    public QuanTriVien? NguoiDuyet { get; set; }
}
