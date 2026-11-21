using TutorHub.Domain.Enums;

namespace TutorHub.Domain.Entities;

/// <summary>
/// Hội thoại trao đổi giữa Học sinh và Gia sư (`03-class-v4.drawio`, mxCell id="39").
/// Ràng buộc NOTE 403: `{unique(HocSinhId, GiaSuId)}`. Thuộc tính `/tinNhanCuoiLuc` là dẫn xuất.
/// </summary>
public sealed class HoiThoai : ThucTheGoc
{
    public Guid HocSinhId { get; set; }
    public HocSinh? HocSinh { get; set; }

    public Guid GiaSuId { get; set; }
    public GiaSu? GiaSu { get; set; }

    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;

    public ICollection<TinNhan> DsTinNhan { get; set; } = new List<TinNhan>();

    /// <summary>
    /// Thuộc tính dẫn xuất `- /tinNhanCuoiLuc : DateTime [0..1]`. KHÔNG tạo cột vật lý.
    /// </summary>
    public DateTime? TinNhanCuoiLuc =>
        DsTinNhan.Count == 0 ? null : DsTinNhan.Max(t => (DateTime?)t.TaoLuc);
}

/// <summary>
/// Tin nhắn trong Hội thoại (`03-class-v4.drawio`, mxCell id="43").
/// Quan hệ Composition `HoiThoai 1 ◆—— 0..* TinNhan`.
/// </summary>
public sealed class TinNhan : ThucTheGoc
{
    public Guid HoiThoaiId { get; set; }
    public HoiThoai? HoiThoai { get; set; }

    public Guid NguoiGuiId { get; set; }
    public NguoiDungThamGia? NguoiGui { get; set; }

    public string NoiDung { get; set; } = string.Empty;
    public bool BiGanCo { get; set; }
    public DateTime? DocLuc { get; set; }
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Thông báo hệ thống gửi tới Người dùng (`03-class-v4.drawio`, mxCell id="3").
/// </summary>
public sealed class ThongBao : ThucTheGoc
{
    public Guid NguoiNhanId { get; set; }
    public NguoiDung? NguoiNhan { get; set; }

    public LoaiThongBao Loai { get; set; }
    public string TieuDe { get; set; } = string.Empty;
    public string NoiDung { get; set; } = string.Empty;
    public DateTime? DocLuc { get; set; }
    public DateTime TaoLuc { get; set; } = DateTime.UtcNow;
}
