using System.Globalization;
using TutorHub.Application.DTOs;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;

namespace TutorHub.Infrastructure.Services;

/// <summary>
/// Dịch vụ tính toán đệm di chuyển thích ứng và phân tích khả thi lộ trình giữa các ca dạy kèm tại nhà.
/// </summary>
public static class DichVuUocTinhDiChuyen
{
    private const double VanTocTrungBinhDoThiKmH = 22.0; // Vận tốc xe máy trung bình trong đô thị Việt Nam
    private const double HeSoGioCaoDiem = 1.45; // Tăng 45% thời gian di chuyển vào giờ cao điểm
    private const int ThoiGianDemAnToanPhut = 15; // 15 phút nghỉ ngơi, chuẩn bị tài liệu giữa 2 ca

    /// <summary>
    /// Kiểm tra thời điểm có rơi vào khung giờ cao điểm giao thông (tan học / tan tầm) tại Việt Nam hay không.
    /// Khung sáng: 07:00 - 08:30 (420 - 510 phút)
    /// Khung chiều: 16:30 - 18:30 (990 - 1110 phút)
    /// </summary>
    public static bool KiemTraGioCaoDiem(DateTime thoiDiemUtc)
    {
        var gioVn = thoiDiemUtc.AddHours(7);
        var phutTrongNgay = gioVn.Hour * 60 + gioVn.Minute;

        var caoDiemSang = phutTrongNgay >= 420 && phutTrongNgay <= 510;
        var caoDiemChieu = phutTrongNgay >= 990 && phutTrongNgay <= 1110;

        return caoDiemSang || caoDiemChieu;
    }

    /// <summary>
    /// Tính khoảng cách địa lý theo công thức Haversine (km) giữa 2 cặp tọa độ.
    /// </summary>
    public static double TinhKhoangCachKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0; // Bán kính trái đất tính bằng km
        var dLat = ToRadian(lat2 - lat1);
        var dLon = ToRadian(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadian(lat1)) * Math.Cos(ToRadian(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(r * c, 2);
    }

    private static double ToRadian(double deg) => deg * (Math.PI / 180.0);

    /// <summary>
    /// Ước tính khoảng cách (km) giữa 2 địa chỉ văn bản khi chưa có tọa độ GPS chính xác.
    /// </summary>
    public static double UocTinhKhoangCachTuDiaChi(string? diaChi1, string? diaChi2)
    {
        if (string.IsNullOrWhiteSpace(diaChi1) || string.IsNullOrWhiteSpace(diaChi2))
        {
            return 3.5; // Khoảng cách mặc định an toàn trong cùng khu vực
        }

        var d1 = diaChi1.ToLower(CultureInfo.CurrentCulture);
        var d2 = diaChi2.ToLower(CultureInfo.CurrentCulture);

        if (d1 == d2) return 0.5;

        // Nếu có trùng từ khóa quận/huyện
        var tuKhoa = new[] { "ninh kiều", "cái răng", "bình thủy", "ô môn", "thốt nốt", "quận 1", "quận 3", "quận 7", "bình thạnh", "thủ đức", "hoàn kiếm", "cầu giấy", "đống đa" };
        var trungQuan = tuKhoa.Any(q => d1.Contains(q) && d2.Contains(q));

        return trungQuan ? 3.2 : 6.8;
    }

    /// <summary>
    /// Phân tích lộ trình di chuyển thích ứng giữa ca trước và ca hiện tại.
    /// </summary>
    public static UocTinhDiChuyenDto PhanTichLoTrinhDiChuyen(
        DonDatLich donHienTai,
        DonDatLich? donLienKeTruoc,
        GiaSu? giaSu)
    {
        var diaChiDich = donHienTai.DiaChiHocSinh ?? "Địa chỉ học sinh";
        string? diaChiXuatPhat = null;
        double khoangCachKm;

        if (donLienKeTruoc != null && donLienKeTruoc.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP && !string.IsNullOrWhiteSpace(donLienKeTruoc.DiaChiHocSinh))
        {
            diaChiXuatPhat = donLienKeTruoc.DiaChiHocSinh;
            khoangCachKm = UocTinhKhoangCachTuDiaChi(diaChiXuatPhat, diaChiDich);
        }
        else
        {
            diaChiXuatPhat = giaSu?.KhuVuc ?? "Vị trí gia sư";
            khoangCachKm = UocTinhKhoangCachTuDiaChi(diaChiXuatPhat, diaChiDich);
        }

        khoangCachKm = Math.Max(0.5, Math.Round(khoangCachKm, 1));

        // Thời điểm xuất phát để kiểm tra giờ cao điểm
        var thoiDiemXuatPhatUtc = donLienKeTruoc != null
            ? donLienKeTruoc.KetThucLuc
            : donHienTai.BatDauLuc.AddMinutes(-30);

        var laGioCaoDiem = KiemTraGioCaoDiem(thoiDiemXuatPhatUtc);

        var thoiGianDiChuyenPhut = (int)Math.Ceiling((khoangCachKm / VanTocTrungBinhDoThiKmH) * 60.0);
        if (laGioCaoDiem)
        {
            thoiGianDiChuyenPhut = (int)Math.Ceiling(thoiGianDiChuyenPhut * HeSoGioCaoDiem);
        }

        thoiGianDiChuyenPhut = Math.Clamp(thoiGianDiChuyenPhut, 10, 90);

        // Khoảng trống thực tế giữa 2 ca
        int khoangTrongThucTePhut;
        if (donLienKeTruoc != null)
        {
            khoangTrongThucTePhut = (int)Math.Max(0, (donHienTai.BatDauLuc - donLienKeTruoc.KetThucLuc).TotalMinutes);
        }
        else
        {
            khoangTrongThucTePhut = 120; // Ca đầu tiên trong ngày, thời gian chuẩn bị rộng rãi
        }

        // Đánh giá mức độ khả thi theo đúng chuẩn:
        // 1. Tối thiểu bắt buộc: Phải có đủ thời gian chạy xe (khoangTrongThucTePhut >= thoiGianDiChuyenPhut)
        // 2. Chuẩn an toàn lý tưởng: Thời gian di chuyển ước tính + 30 phút dự phòng
        string mucDoKhaThi;
        string thongBaoKhaThi;

        if (khoangTrongThucTePhut >= thoiGianDiChuyenPhut + ThoiGianDemAnToanPhut)
        {
            mucDoKhaThi = "THOAI_MAI";
            var phutDuPhong = khoangTrongThucTePhut - thoiGianDiChuyenPhut;
            thongBaoKhaThi = $"Lộ trình thuận lợi · Cách ~{khoangCachKm} km · Di chuyển ~{thoiGianDiChuyenPhut} phút + Dư {phutDuPhong} phút dự phòng (đạt chuẩn an toàn)";
        }
        else if (khoangTrongThucTePhut >= thoiGianDiChuyenPhut)
        {
            mucDoKhaThi = "KHA_GAP";
            var tagCaoDiem = laGioCaoDiem ? " [giờ cao điểm]" : "";
            var phutDuPhong = khoangTrongThucTePhut - thoiGianDiChuyenPhut;
            thongBaoKhaThi = $"Khá gấp về thời gian · Cách ~{khoangCachKm} km · Cần ~{thoiGianDiChuyenPhut} phút di chuyển{tagCaoDiem} · Chỉ còn {phutDuPhong} phút dự phòng (dưới 15 phút chuẩn)";
        }
        else
        {
            mucDoKhaThi = "KHONG_KHA_THI";
            thongBaoKhaThi = $"Không khả thi · Khoảng trống {khoangTrongThucTePhut} phút không đủ thời gian di chuyển ~{khoangCachKm} km (cần tối thiểu {thoiGianDiChuyenPhut} phút chạy xe)";
        }

        var googleMapsUrl = $"https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(diaChiDich)}";

        return new UocTinhDiChuyenDto(
            KhoangCachKm: khoangCachKm,
            ThoiGianDiChuyenPhut: thoiGianDiChuyenPhut,
            ThoiGianDemAnToanPhut: ThoiGianDemAnToanPhut,
            KhoangTrongThucTePhut: khoangTrongThucTePhut,
            LaGioCaoDiem: laGioCaoDiem,
            MucDoKhaThi: mucDoKhaThi,
            ThongBaoKhaThi: thongBaoKhaThi,
            GoogleMapsUrl: googleMapsUrl,
            DiaChiXuatPhat: diaChiXuatPhat,
            DiaChiDichDen: diaChiDich);
    }
}
