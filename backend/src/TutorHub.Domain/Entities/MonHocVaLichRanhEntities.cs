using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TutorHub.Domain.Enums;

namespace TutorHub.Domain.Entities;

/// <summary>
/// Danh mục Môn học (`03-class-v4.drawio`, mxCell id="59").
/// Thuộc tính `/slug` được dẫn xuất từ `Ten`, KHÔNG tạo cột vật lý.
/// </summary>
public sealed class MonHoc : ThucTheGoc
{
    public string Ten { get; set; } = string.Empty;
    public bool DangHoatDong { get; set; } = true;

    public ICollection<GiaSuMonHoc> DsGiaSuMonHoc { get; set; } = new List<GiaSuMonHoc>();

    /// <summary>
    /// Thuộc tính dẫn xuất `- /slug : String {unique}` từ `Ten`. KHÔNG tạo cột vật lý.
    /// </summary>
    public string Slug => TaoSlug(Ten);

    public static string TaoSlug(string? chuoi)
    {
        if (string.IsNullOrWhiteSpace(chuoi))
        {
            return string.Empty;
        }

        var normalized = chuoi.Trim().ToLowerInvariant()
            .Replace('đ', 'd')
            .Replace('Đ', 'd')
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
    }
}

/// <summary>
/// Cấu hình Môn dạy & Học phí của Gia sư (`03-class-v4.drawio`, mxCell id="63").
/// Quan hệ Composition `GiaSu 1 ◆—— 0..* GiaSuMonHoc`, ràng buộc `{unique(GiaSuId, MonHocId)}`.
/// </summary>
public sealed class GiaSuMonHoc : ThucTheGoc
{
    public Guid GiaSuId { get; set; }
    public GiaSu? GiaSu { get; set; }

    public Guid MonHocId { get; set; }
    public MonHoc? MonHoc { get; set; }

    public int GiaMoiGio { get; set; }

    public ICollection<DonDatLich> DsDonDatLich { get; set; } = new List<DonDatLich>();
}

/// <summary>
/// Lớp trừu tượng Lịch rảnh của Gia sư (`03-class-v4.drawio`, mxCell id="67").
/// Ánh xạ TPT (Table-per-Type) sang bảng `LichRanh` và 2 bảng con `LichLapHangTuan`, `LichNgayCuThe`.
/// </summary>
public abstract class LichRanh : ThucTheGoc
{
    public Guid GiaSuId { get; set; }
    public GiaSu? GiaSu { get; set; }
}

/// <summary>
/// Lịch rảnh lặp hằng tuần của Gia sư (`03-class-v4.drawio`, mxCell id="348").
/// Ràng buộc NOTE 406: `ThuTrongTuan ∈ 1..7`, `GioKetThuc > GioBatDau`.
/// Hỗ trợ phân biệt hình thức nhận lớp: TRUC_TUYEN, TRUC_TIEP, CA_HAI (mặc định).
/// </summary>
public sealed class LichLapHangTuan : LichRanh
{
    public int ThuTrongTuan { get; set; }
    public TimeOnly GioBatDau { get; set; }
    public TimeOnly GioKetThuc { get; set; }
    public HinhThucLichTuan HinhThucLich { get; set; } = HinhThucLichTuan.CA_HAI;
}

/// <summary>
/// Lịch ngày cụ thể (bổ sung hoặc nghỉ phép / báo bận) của Gia sư (`03-class-v4.drawio`, mxCell id="352").
/// Hỗ trợ khoảng ngày nghỉ: `NgayCuThe` (từ ngày) đến `NgayKetThuc` (đến ngày) và lý do nghỉ.
/// Ràng buộc NOTE 406: `GioBatDau/GioKetThuc` rỗng = cả ngày; nếu có đủ 2 mốc thì `GioKetThuc > GioBatDau`.
/// </summary>
public sealed class LichNgayCuThe : LichRanh
{
    public DateOnly NgayCuThe { get; set; }
    public DateOnly? NgayKetThuc { get; set; }
    public LoaiLichNgayCuThe Loai { get; set; }
    public TimeOnly? GioBatDau { get; set; }
    public TimeOnly? GioKetThuc { get; set; }
    public string? LyDo { get; set; }
}
