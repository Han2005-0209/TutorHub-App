using System.Text.RegularExpressions;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;

namespace TutorHub.Infrastructure.Services;

/// <summary>
/// Bộ máy kiểm duyệt nội dung hiển thị trên web TutorHub:
/// - Kiểm duyệt dữ liệu đầu vào của người dùng (chặn ngôn từ không phù hợp & lộ thông tin liên hệ ngoài luồng).
/// - Kiểm tra văn bản hiển thị giao diện web (chặn rò rỉ công nghệ nội bộ, kiến trúc, CSDL, mã tài liệu, enum thô).
/// </summary>
public sealed class KiemDuyetNoiDungService : IKiemDuyetNoiDungService
{
    private sealed record QuyTacKiemDuyet(
        string NhomQuyTac,
        string MaQuyTac,
        Regex BieuThuc,
        string LyDo);

    // Nhóm 1–4: Quy tắc chặn nội dung kỹ thuật / phát triển / nội bộ lọt lên giao diện người dùng cuối
    private static readonly IReadOnlyList<QuyTacKiemDuyet> QuyTacHienThiWeb =
    [
        // Nhóm 1: Lộ công nghệ & Kiến trúc nội bộ
        new("LO_CONG_NGHE_NOI_BO", "CONG_NGHE_BACKEND",
            new Regex(@"(?:\.NET\s*\d+|\b(?:ASP\.NET|EF\s*Core|Entity\s*Framework|SQL\s*Server|SSMS|PostgreSQL|Prisma|Express\.js|Node\.js)\b)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được hiển thị tên công nghệ backend/CSDL (.NET, EF Core, SQL Server, SSMS...) lên giao diện người dùng cuối."),
        new("LO_CONG_NGHE_NOI_BO", "KIEN_TRUC_CHUAN_HOA",
            new Regex(@"\b(Clean\s*Architecture|BCNF|4NF|3NF|2NF|1NF|Class\s*v4|UML|draw\.?io|Table-per-Type|TPT|TPH|ERD|OCL)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được hiển thị thuật ngữ kiến trúc phần mềm / chuẩn hóa CSDL (Clean Architecture, BCNF/4NF, Class v4...) lên web sản phẩm."),
        new("LO_CONG_NGHE_NOI_BO", "THUAT_NGU_KY_THUAT_API",
            new Regex(@"(?:/api/health|\b(?:Web\s*API|REST\s*API|JWT\s*Bearer|Bearer\s*Token|Signed\s*URL|Storage\s*Key|Endpoint\s*API|GET\s*/api/|POST\s*/api/|PUT\s*/api/|DELETE\s*/api/|localhost:\d+|Leaflet\s*\d+\.\d+|OpenStreetMap)\b)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được hiển thị thuật ngữ kỹ thuật giao thức/API/thư viện (Web API, JWT Bearer, Signed URL, Storage Key, endpoint /api/...) ra giao diện người dùng."),

        // Nhóm 2: Lộ mã tài liệu nghiệp vụ & tiến độ phát triển
        new("LO_MA_TAI_LIEU_DEV", "MA_LUAT_NGHIEP_VU",
            new Regex(@"\b(BR-(?:TUT|BKG|FIN|PAY|REV|CMP|MSG|ADM)-\d+|UC_[A-Za-z0-9_]+)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được hiển thị mã quy tắc nghiệp vụ/Use Case nội bộ (BR-*, UC_*) trên giao diện web."),
        new("LO_MA_TAI_LIEU_DEV", "TIEN_DO_PHAT_TRIEN",
            new Regex(@"(?:\(Dev\)|\b(?:Module\s*\d+|Seed\s*Data|TODO|FIXME|Lorem\s+ipsum|xóa\s*mềm)\b)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được hiển thị ghi chú tiến độ phát triển hoặc dữ liệu thử nghiệm kỹ thuật (Module 1..9, Seed Data, TODO, xóa mềm) trên giao diện người dùng."),

        // Nhóm 3: Lộ tên bảng CSDL / Lớp thực thể / Thuộc tính UML
        new("LO_TEN_THUC_THE_CSDL", "TEN_LOP_THUC_THE",
            new Regex(@"\b(GiaSuMonHoc|TaiKhoanNganHang|LichLapHangTuan|LichNgayCuThe|MucYeuThich|DonDatLich|GiayToGiaSu|NguoiDungThamGia|TaiKhoanVi|GiaoDichVi|YeuCauRutTien|QuanTriVien|AppDbContext)\b",
                RegexOptions.Compiled),
            "Không được hiển thị tên lớp/bảng cơ sở dữ liệu dạng PascalCase (GiaSuMonHoc, TaiKhoanNganHang, ...) lên giao diện web."),
        new("LO_TEN_THUC_THE_CSDL", "THUOC_TINH_UML_KHO_PRIVATE",
            new Regex(@"(/daXacMinh|/vaiTro|/tongTien|kho\s+Private|\?id=\.\.\.)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được hiển thị ký hiệu thuộc tính dẫn xuất UML (/daXacMinh) hoặc tên kỹ thuật (kho Private) ra giao diện người dùng."),

        // Nhóm 4: Lộ mã Enum thô chưa Việt hóa trong văn bản hiển thị
        new("LO_ENUM_THO_UI", "MA_ENUM_CHUA_VIET_HOA",
            new Regex(@"\b(HOC_SINH|GIA_SU|QUAN_TRI|CHO_DUYET|DA_DUYET|TU_CHOI|NHAP|DA_XOA|BI_KHOA|HOAT_DONG|DANG_GIU|DA_GIAI_NGAN|DA_HOAN|CHO_XAC_NHAN|DA_XAC_NHAN|HOAN_THANH|TRANH_CHAP|BANG_CAP|CHUNG_CHI|CAN_CUOC|NGHI_PHEP|BO_SUNG|TRUC_TUYEN|TRUC_TIEP|TIEU_HOC|DAI_HOC|LUYEN_THI|VI_NOI_BO|GIA_LAP)\b",
                RegexOptions.Compiled),
            "Không được hiển thị mã Enum thô dạng UPPER_SNAKE_CASE lên giao diện người dùng; phải ánh xạ sang nhãn tiếng Việt tự nhiên.")
    ];

    // Nhóm 5A: Từ ngữ thô tục / phản cảm / lừa đảo không phù hợp với môi trường giáo dục
    private static readonly Regex RegexTuNguKhongPhuHop = new(
        @"\b(đm|đkm|vcl|vl|cc|đéo|đụ|lồn|cặc|buồi|chó\s*đẻ|mất\s*dạy|lừa\s*đảo|cờ\s*bạc|cá\s*độ|tài\s*xỉu|lô\s*đề|sex|porn|xxx|fuck|shit|bitch)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Nhóm 5B: Lộ thông tin liên hệ ngoài luồng trong hồ sơ công khai (chống lách nền tảng)
    private static readonly IReadOnlyList<(string Ma, Regex Regex, string LyDo)> QuyTacLienHeNgoaiLuong =
    [
        ("LO_SO_DIEN_THOAI",
            new Regex(@"(?<!\d)(?:\+?84|0)(?:[\s.\-]*\d){8,9}(?!\d)", RegexOptions.Compiled),
            "Không được chèn số điện thoại cá nhân vào nội dung hiển thị công khai."),
        ("LO_EMAIL",
            new Regex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được chèn địa chỉ email cá nhân vào nội dung hiển thị công khai."),
        ("LO_ZALO_FACEBOOK",
            new Regex(@"\b(zalo|zl\s*[:.]|fb\.com|fb\.me|facebook\.com|telegram|t\.me|whatsapp|viber)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được chèn liên kết hoặc thông tin mạng xã hội ngoài nền tảng (Zalo, Facebook, Telegram...) vào nội dung công khai."),
        ("LO_URL_NGOAI",
            new Regex(@"\b(?:https?://|www\.)[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Không được chèn đường dẫn website bên ngoài vào nội dung hồ sơ công khai.")
    ];

    public KetQuaKiemDuyetNoiDungDto KiemTraNoiDungNguoiDung(
        string tenTruong,
        string? noiDung,
        bool kiemTraLienHeNgoaiLuong = true)
    {
        var dsViPham = new List<ViPhamNoiDungDto>();
        if (string.IsNullOrWhiteSpace(noiDung))
        {
            return new KetQuaKiemDuyetNoiDungDto(true, dsViPham);
        }

        // 1. Kiểm tra ngôn từ không phù hợp / phản cảm / cờ bạc
        foreach (Match match in RegexTuNguKhongPhuHop.Matches(noiDung))
        {
            dsViPham.Add(new ViPhamNoiDungDto(
                NhomQuyTac: "NOI_DUNG_KHONG_PHU_HOP",
                MaQuyTac: "NGON_TU_KHONG_PHU_HOP",
                Truong: tenTruong,
                DoanViPham: match.Value,
                LyDo: "Nội dung chứa ngôn từ không phù hợp với môi trường giáo dục TutorHub."));
        }

        // 2. Kiểm tra chèn thông tin liên hệ ngoài luồng
        if (kiemTraLienHeNgoaiLuong)
        {
            foreach (var (ma, regex, lyDo) in QuyTacLienHeNgoaiLuong)
            {
                foreach (Match match in regex.Matches(noiDung))
                {
                    dsViPham.Add(new ViPhamNoiDungDto(
                        NhomQuyTac: "NOI_DUNG_KHONG_PHU_HOP",
                        MaQuyTac: ma,
                        Truong: tenTruong,
                        DoanViPham: match.Value,
                        LyDo: lyDo));
                }
            }
        }

        return new KetQuaKiemDuyetNoiDungDto(dsViPham.Count == 0, dsViPham);
    }

    public void BatBuocNoiDungNguoiDungHopLe(
        IEnumerable<(string TenTruong, string? NoiDung, bool KiemTraLienHeNgoaiLuong)> dsTruong)
    {
        var tatCaViPham = new List<ViPhamNoiDungDto>();
        foreach (var (tenTruong, noiDung, kiemTraLienHe) in dsTruong)
        {
            var kq = KiemTraNoiDungNguoiDung(tenTruong, noiDung, kiemTraLienHe);
            if (!kq.HopLe)
            {
                tatCaViPham.AddRange(kq.DsViPham);
            }
        }

        if (tatCaViPham.Count > 0)
        {
            var chiTiet = tatCaViPham
                .Select(v => new ChiTietLoiTruongDto(v.Truong, $"{v.LyDo} (Phát hiện: \"{v.DoanViPham}\")"))
                .ToList();

            throw new LoiUngDungException(
                "NOI_DUNG_KHONG_PHU_HOP",
                "Nội dung nhập vào chứa từ ngữ không phù hợp hoặc thông tin liên hệ ngoài nền tảng. Vui lòng chỉnh sửa trước khi lưu.",
                422,
                chiTiet);
        }
    }

    public KetQuaKiemDuyetNoiDungDto KiemTraVanBanHienThiWeb(string? vanBanHienThi, string nguon = "web")
    {
        var dsViPham = new List<ViPhamNoiDungDto>();
        if (string.IsNullOrWhiteSpace(vanBanHienThi))
        {
            return new KetQuaKiemDuyetNoiDungDto(true, dsViPham);
        }

        foreach (var quyTac in QuyTacHienThiWeb)
        {
            foreach (Match match in quyTac.BieuThuc.Matches(vanBanHienThi))
            {
                dsViPham.Add(new ViPhamNoiDungDto(
                    NhomQuyTac: quyTac.NhomQuyTac,
                    MaQuyTac: quyTac.MaQuyTac,
                    Truong: nguon,
                    DoanViPham: match.Value,
                    LyDo: quyTac.LyDo));
            }
        }

        // Kiểm tra ngôn từ không phù hợp trên giao diện tĩnh
        foreach (Match match in RegexTuNguKhongPhuHop.Matches(vanBanHienThi))
        {
            dsViPham.Add(new ViPhamNoiDungDto(
                NhomQuyTac: "NOI_DUNG_KHONG_PHU_HOP",
                MaQuyTac: "NGON_TU_KHONG_PHU_HOP",
                Truong: nguon,
                DoanViPham: match.Value,
                LyDo: "Giao diện web chứa ngôn từ không phù hợp với môi trường giáo dục."));
        }

        return new KetQuaKiemDuyetNoiDungDto(dsViPham.Count == 0, dsViPham);
    }
}
