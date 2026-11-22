using Microsoft.EntityFrameworkCore;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Infrastructure.Services;

namespace TutorHub.Infrastructure.Persistence;

/// <summary>
/// Khởi tạo bộ dữ liệu đầy đủ cho TutorHub (12 gia sư toàn quốc với ảnh chân dung & giấy tờ lưu trên Cloudinary,
/// 6 học sinh, 160 môn học, 5 mã giảm giá, 21 đơn đặt lịch đủ trạng thái, ví khớp sổ cái 100%, đánh giá, khiếu nại, rút tiền, hội thoại và thông báo).
/// Mật khẩu mặc định đọc từ biến môi trường `SEED_DEFAULT_PASSWORD` (mặc định: `Password123!`).
/// </summary>
public static class DuLieuMauSeeder
{
    private const string CldPublicBase = "https://res.cloudinary.com/lsvnqht5/image/upload";
    private const string DocBangThacSi = "cld:private:image:jpg:tutorhub/private/giay-to/bang-thac-si-chinh-quy";
    private const string DocBangDaiHoc = "cld:private:image:png:tutorhub/private/giay-to/bang-cap-chinh-quy";
    private const string DocChungChi = "cld:private:image:png:tutorhub/private/giay-to/chung-chi-chuyen-mon";
    private const string DocCanCuoc = "cld:private:image:png:tutorhub/private/giay-to/xac-minh-danh-tinh";
    private const string DocMinhChungLop = "cld:private:image:jpg:tutorhub/private/khieu-nai/minh-chung-buoi-hoc";

    /// <summary>
    /// Xóa sạch toàn bộ người dùng (TRỪ Quản trị viên) và toàn bộ dữ liệu mẫu/giao dịch cũ,
    /// giữ nguyên tài khoản Quản trị viên và danh mục 160 môn học, sau đó khởi tạo bộ dữ liệu bản đầy đủ mới.
    /// </summary>
    public static async Task LamMoiVaSeedBanDayDuAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        const string sqlXoaDuLieuTruAdmin = """
            DELETE FROM [AuditLog];
            DELETE FROM [ThongBao];
            DELETE FROM [TinNhan];
            DELETE FROM [HoiThoai];
            DELETE FROM [NhatKyPhongHoc];
            DELETE FROM [KhieuNai];
            DELETE FROM [DanhGia];
            DELETE FROM [GiaoDichVi];
            DELETE FROM [YeuCauRutTien];
            DELETE FROM [ThanhToan];
            DELETE FROM [DonDatLich];
            DELETE FROM [MucYeuThich];
            DELETE FROM [MaGiamGia];
            DELETE FROM [GiayToGiaSu];
            DELETE FROM [LichLapHangTuan];
            DELETE FROM [LichNgayCuThe];
            DELETE FROM [LichRanh];
            DELETE FROM [GiaSuMonHoc];
            DELETE FROM [TaiKhoanNganHang];
            DELETE FROM [TaiKhoanVi];
            DELETE FROM [HocSinh];
            DELETE FROM [GiaSu];
            DELETE FROM [NguoiDungThamGia];
            DELETE FROM [NguoiDung] WHERE [Id] NOT IN (SELECT [Id] FROM [QuanTriVien]);
            """;

        await db.Database.ExecuteSqlRawAsync(sqlXoaDuLieuTruAdmin, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        db.ChangeTracker.Clear();
        await SeedAsync(db, cancellationToken);
    }

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        // Luôn bảo đảm có ít nhất 1 Quản trị viên và đủ 160 môn học
        var matKhauMacDinh = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD") ?? "Password123!";
        var hashMacDinh = XacThucService.BamMatKhau(matKhauMacDinh);

        var admin = await db.QuanTriViens
            .AsNoTracking()
            .OrderBy(a => a.TaoLuc)
            .FirstOrDefaultAsync(cancellationToken);

        Guid adminId;
        if (admin is null)
        {
            adminId = Guid.Parse("30000000-0000-0000-0000-000000000001");
            db.QuanTriViens.Add(new QuanTriVien
            {
                Id = adminId,
                Email = "admin.demo@example.tutorhub.test",
                MatKhauHash = hashMacDinh,
                HoTen = "Quản Trị Viên TutorHub",
                SoDienThoai = "0900000001",
                KhuVuc = "Ninh Kiều, Cần Thơ",
                TrangThai = TrangThaiNguoiDung.HOAT_DONG,
                XacThucEmailLuc = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
                TaoLuc = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            adminId = admin.Id;
        }

        // Bảo đảm đủ 160 môn học chuẩn
        await DongBoDanhMucMonHocDayDuAsync(db, cancellationToken);

        // Nếu đã có gia sư trong CSDL -> chỉ chạy đồng bộ idempotent an toàn
        if (await db.GiaSus.AnyAsync(cancellationToken))
        {
            await DongBoAnToanSauKhoiTaoAsync(db, cancellationToken);
            return;
        }

        // Lấy map ID các môn học theo tên để liên kết chính xác dù MonHoc đã tồn tại trước đó
        var monMap = await db.MonHocs
            .AsNoTracking()
            .ToDictionaryAsync(m => m.Ten, m => m.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        Guid MonId(string ten) => monMap[ten];

        var now = DateTime.UtcNow;

        // ══════════════════════════════════════════════════════════════════════
        // 1. MÃ GIẢM GIÁ (5 MÃ)
        // ══════════════════════════════════════════════════════════════════════
        var maWelcomeId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var maGiam50kId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var maOnline15Id = Guid.Parse("20000000-0000-0000-0000-000000000003");
        var maTuanVangId = Guid.Parse("20000000-0000-0000-0000-000000000004");
        var maHe2026Id = Guid.Parse("20000000-0000-0000-0000-000000000005");

        db.MaGiamGias.AddRange(
            new MaGiamGia
            {
                Id = maWelcomeId,
                Ma = "WELCOME10",
                Loai = LoaiMaGiamGia.PHAN_TRAM,
                GiaTri = 10,
                GiaTriDonToiThieu = 100_000,
                SoLuotToiDa = 200,
                BatDauLuc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HetHanLuc = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                DangHoatDong = true
            },
            new MaGiamGia
            {
                Id = maGiam50kId,
                Ma = "GIAM50K",
                Loai = LoaiMaGiamGia.SO_TIEN_CO_DINH,
                GiaTri = 50_000,
                GiaTriDonToiThieu = 250_000,
                SoLuotToiDa = 100,
                BatDauLuc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HetHanLuc = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                DangHoatDong = true
            },
            new MaGiamGia
            {
                Id = maOnline15Id,
                Ma = "HOCONLINE15",
                Loai = LoaiMaGiamGia.PHAN_TRAM,
                GiaTri = 15,
                GiaTriDonToiThieu = 200_000,
                SoLuotToiDa = 150,
                BatDauLuc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HetHanLuc = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                DangHoatDong = true
            },
            new MaGiamGia
            {
                Id = maTuanVangId,
                Ma = "TUANVANG100K",
                Loai = LoaiMaGiamGia.SO_TIEN_CO_DINH,
                GiaTri = 100_000,
                GiaTriDonToiThieu = 500_000,
                SoLuotToiDa = 50,
                BatDauLuc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HetHanLuc = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                DangHoatDong = true
            },
            new MaGiamGia
            {
                Id = maHe2026Id,
                Ma = "HE2026",
                Loai = LoaiMaGiamGia.PHAN_TRAM,
                GiaTri = 20,
                GiaTriDonToiThieu = 300_000,
                SoLuotToiDa = 80,
                BatDauLuc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HetHanLuc = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                DangHoatDong = true
            });

        // ══════════════════════════════════════════════════════════════════════
        // 2. HỌC SINH (6 TÀI KHOẢN THỰC TẾ TOÀN QUỐC)
        // ══════════════════════════════════════════════════════════════════════
        var hs1Id = Guid.Parse("31000000-0000-0000-0000-000000000001");
        var hs2Id = Guid.Parse("31000000-0000-0000-0000-000000000002");
        var hs3Id = Guid.Parse("31000000-0000-0000-0000-000000000003");
        var hs4Id = Guid.Parse("31000000-0000-0000-0000-000000000004");
        var hs5Id = Guid.Parse("31000000-0000-0000-0000-000000000005");
        var hs6Id = Guid.Parse("31000000-0000-0000-0000-000000000006");

        var viHs1Id = Guid.Parse("41000000-0000-0000-0000-000000000001");
        var viHs2Id = Guid.Parse("41000000-0000-0000-0000-000000000002");
        var viHs3Id = Guid.Parse("41000000-0000-0000-0000-000000000003");
        var viHs4Id = Guid.Parse("41000000-0000-0000-0000-000000000004");
        var viHs5Id = Guid.Parse("41000000-0000-0000-0000-000000000005");
        var viHs6Id = Guid.Parse("41000000-0000-0000-0000-000000000006");

        HocSinh TaoHocSinh(Guid id, Guid viId, string email, string hoTen, string sdt, string khuVuc, DateTime taoLuc) => new()
        {
            Id = id,
            Email = email,
            MatKhauHash = hashMacDinh,
            HoTen = hoTen,
            SoDienThoai = sdt,
            KhuVuc = khuVuc,
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = taoLuc,
            TaoLuc = taoLuc,
            TaiKhoanVi = new TaiKhoanVi { Id = viId, NguoiDungThamGiaId = id, SoDu = 0, SoDuDangXuLyRut = 0, TaoLuc = taoLuc }
        };

        var dsHocSinh = new[]
        {
            TaoHocSinh(hs1Id, viHs1Id, "minhkhoa.hs@tutorhub.vn", "Lê Minh Khoa", "0912345001", "Ninh Kiều, Cần Thơ", new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc)),
            TaoHocSinh(hs2Id, viHs2Id, "thutrang.hs@tutorhub.vn", "Phạm Thu Trang", "0912345002", "Quận 1, TP. Hồ Chí Minh", new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc)),
            TaoHocSinh(hs3Id, viHs3Id, "giabao.hs@tutorhub.vn", "Nguyễn Gia Bảo", "0912345003", "Cầu Giấy, Hà Nội", new DateTime(2026, 2, 10, 8, 0, 0, DateTimeKind.Utc)),
            TaoHocSinh(hs4Id, viHs4Id, "khanhvy.hs@tutorhub.vn", "Trần Khánh Vy", "0912345004", "Hải Châu, Đà Nẵng", new DateTime(2026, 2, 14, 8, 0, 0, DateTimeKind.Utc)),
            TaoHocSinh(hs5Id, viHs5Id, "hoangnam.hs@tutorhub.vn", "Võ Hoàng Nam", "0912345005", "Bình Thạnh, TP. Hồ Chí Minh", new DateTime(2026, 2, 18, 8, 0, 0, DateTimeKind.Utc)),
            TaoHocSinh(hs6Id, viHs6Id, "ngochan.hs@tutorhub.vn", "Đặng Ngọc Hân", "0912345006", "Cái Răng, Cần Thơ", new DateTime(2026, 2, 22, 8, 0, 0, DateTimeKind.Utc))
        };
        db.HocSinhs.AddRange(dsHocSinh);

        // ══════════════════════════════════════════════════════════════════════
        // 3. GIA SƯ (10 ĐÃ DUYỆT + 2 CHỜ DUYỆT — 100% ẢNH & GIẤY TỜ CLOUDINARY)
        // ══════════════════════════════════════════════════════════════════════
        var gs1Id = Guid.Parse("32000000-0000-0000-0000-000000000001");
        var gs2Id = Guid.Parse("32000000-0000-0000-0000-000000000002");
        var gs3Id = Guid.Parse("32000000-0000-0000-0000-000000000003");
        var gs4Id = Guid.Parse("32000000-0000-0000-0000-000000000004");
        var gs5Id = Guid.Parse("32000000-0000-0000-0000-000000000005");
        var gs6Id = Guid.Parse("32000000-0000-0000-0000-000000000006");
        var gs7Id = Guid.Parse("32000000-0000-0000-0000-000000000007");
        var gs8Id = Guid.Parse("32000000-0000-0000-0000-000000000008");
        var gs9Id = Guid.Parse("32000000-0000-0000-0000-000000000009");
        var gs10Id = Guid.Parse("32000000-0000-0000-0000-000000000010");
        var gs11Id = Guid.Parse("32000000-0000-0000-0000-000000000011"); // CHO_DUYET
        var gs12Id = Guid.Parse("32000000-0000-0000-0000-000000000012"); // CHO_DUYET

        var viGs1Id = Guid.Parse("42000000-0000-0000-0000-000000000001");
        var viGs2Id = Guid.Parse("42000000-0000-0000-0000-000000000002");
        var viGs3Id = Guid.Parse("42000000-0000-0000-0000-000000000003");
        var viGs4Id = Guid.Parse("42000000-0000-0000-0000-000000000004");
        var viGs5Id = Guid.Parse("42000000-0000-0000-0000-000000000005");
        var viGs6Id = Guid.Parse("42000000-0000-0000-0000-000000000006");
        var viGs7Id = Guid.Parse("42000000-0000-0000-0000-000000000007");
        var viGs8Id = Guid.Parse("42000000-0000-0000-0000-000000000008");
        var viGs9Id = Guid.Parse("42000000-0000-0000-0000-000000000009");
        var viGs10Id = Guid.Parse("42000000-0000-0000-0000-000000000010");
        var viGs11Id = Guid.Parse("42000000-0000-0000-0000-000000000011");
        var viGs12Id = Guid.Parse("42000000-0000-0000-0000-000000000012");

        // ID các bản ghi GiaSuMonHoc
        var gsmGs1Toan = Guid.Parse("51000000-0000-0000-0000-000000000001");
        var gsmGs1Ly = Guid.Parse("51000000-0000-0000-0000-000000000002");
        var gsmGs1OnThi = Guid.Parse("51000000-0000-0000-0000-000000000003");
        var gsmGs2Anh = Guid.Parse("51000000-0000-0000-0000-000000000004");
        var gsmGs2Ielts = Guid.Parse("51000000-0000-0000-0000-000000000005");
        var gsmGs2Gt = Guid.Parse("51000000-0000-0000-0000-000000000006");
        var gsmGs3Hoa = Guid.Parse("51000000-0000-0000-0000-000000000007");
        var gsmGs3Sinh = Guid.Parse("51000000-0000-0000-0000-000000000008");
        var gsmGs4Van = Guid.Parse("51000000-0000-0000-0000-000000000009");
        var gsmGs4Su = Guid.Parse("51000000-0000-0000-0000-000000000010");
        var gsmGs4Nl = Guid.Parse("51000000-0000-0000-0000-000000000011");
        var gsmGs5Python = Guid.Parse("51000000-0000-0000-0000-000000000012");
        var gsmGs5Tin = Guid.Parse("51000000-0000-0000-0000-000000000013");
        var gsmGs5Sql = Guid.Parse("51000000-0000-0000-0000-000000000014");
        var gsmGs6Gt = Guid.Parse("51000000-0000-0000-0000-000000000015");
        var gsmGs6Toeic = Guid.Parse("51000000-0000-0000-0000-000000000016");
        var gsmGs6Anh = Guid.Parse("51000000-0000-0000-0000-000000000017");
        var gsmGs7Toan = Guid.Parse("51000000-0000-0000-0000-000000000018");
        var gsmGs7Lop10 = Guid.Parse("51000000-0000-0000-0000-000000000019");
        var gsmGs7Tin = Guid.Parse("51000000-0000-0000-0000-000000000020");
        var gsmGs8Nhat = Guid.Parse("51000000-0000-0000-0000-000000000021");
        var gsmGs8Jlpt = Guid.Parse("51000000-0000-0000-0000-000000000022");
        var gsmGs8Trung = Guid.Parse("51000000-0000-0000-0000-000000000023");
        var gsmGs9Ly = Guid.Parse("51000000-0000-0000-0000-000000000024");
        var gsmGs9Hoa = Guid.Parse("51000000-0000-0000-0000-000000000025");
        var gsmGs10Piano = Guid.Parse("51000000-0000-0000-0000-000000000026");
        var gsmGs10Guitar = Guid.Parse("51000000-0000-0000-0000-000000000027");
        var gsmGs11Tin = Guid.Parse("51000000-0000-0000-0000-000000000028");
        var gsmGs11Cpp = Guid.Parse("51000000-0000-0000-0000-000000000029");
        var gsmGs12Han = Guid.Parse("51000000-0000-0000-0000-000000000030");
        var gsmGs12Topik = Guid.Parse("51000000-0000-0000-0000-000000000031");

        int lichSeq = 1;
        LichLapHangTuan TaoLichTuan(Guid gsId, int thu, int gioBd, int phutBd, int gioKt, int phutKt, HinhThucLichTuan hinhThuc = HinhThucLichTuan.CA_HAI) => new()
        {
            Id = Guid.Parse($"61000000-0000-0000-0000-{lichSeq++:000000000000}"),
            GiaSuId = gsId,
            ThuTrongTuan = thu,
            GioBatDau = new TimeOnly(gioBd, phutBd),
            GioKetThuc = new TimeOnly(gioKt, phutKt),
            HinhThucLich = hinhThuc
        };

        int giayToSeq = 1;
        GiayToGiaSu TaoGiayTo(Guid gsId, LoaiGiayTo loai, string cldKey, string tenTep, bool daDuyet, DateTime? duyetLuc) => new()
        {
            Id = Guid.Parse($"71000000-0000-0000-0000-{giayToSeq++:000000000000}"),
            GiaSuId = gsId,
            Loai = loai,
            DuongDanTep = cldKey,
            TenTepGoc = tenTep,
            TrangThai = daDuyet ? TrangThaiGiayTo.DA_DUYET : TrangThaiGiayTo.CHO_DUYET,
            DuyetLuc = daDuyet ? duyetLuc : null,
            NguoiDuyetId = daDuyet ? adminId : null
        };

        var gs1 = new GiaSu
        {
            Id = gs1Id,
            Email = "hoangminh.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "ThS. Nguyễn Hoàng Minh",
            SoDienThoai = "0908111001",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279555/tutorhub/public/tutors/hoang-minh.jpg",
            KhuVuc = "Ninh Kiều, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Thạc sĩ Toán học chuyên luyện thi THPT Quốc gia & Đại học 9+",
            GioiThieu = "8 năm kinh nghiệm giảng dạy Toán và Vật lý cấp THPT tại Cần Thơ và trực tuyến toàn quốc. Phương pháp sơ đồ hóa tư duy, kèm sát từ mất gốc đến vận dụng cao mục tiêu 9+.",
            HocVan = "Thạc sĩ Phương pháp giảng dạy Toán — ĐH Cần Thơ",
            SoNamKinhNghiem = 8,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 10,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs1Id, NguoiDungThamGiaId = gs1Id, TaoLuc = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs1Id, TenNganHang = "Vietcombank", SoTaiKhoan = "0111000246810", ChuTaiKhoan = "NGUYEN HOANG MINH" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs1Toan, GiaSuId = gs1Id, MonHocId = MonId("Toán"), GiaMoiGio = 250_000 },
                new GiaSuMonHoc { Id = gsmGs1Ly, GiaSuId = gs1Id, MonHocId = MonId("Vật lý"), GiaMoiGio = 240_000 },
                new GiaSuMonHoc { Id = gsmGs1OnThi, GiaSuId = gs1Id, MonHocId = MonId("Ôn thi Toán THPT Quốc gia"), GiaMoiGio = 270_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs1Id, 2, 18, 0, 21, 30),
                TaoLichTuan(gs1Id, 4, 18, 0, 21, 30),
                TaoLichTuan(gs1Id, 6, 18, 0, 21, 30),
                TaoLichTuan(gs1Id, 7, 8, 0, 12, 0),
                TaoLichTuan(gs1Id, 1, 14, 0, 18, 0),
                new LichNgayCuThe { Id = Guid.Parse($"61000000-0000-0000-0000-{lichSeq++:000000000000}"), GiaSuId = gs1Id, NgayCuThe = new DateOnly(2026, 12, 25), Loai = LoaiLichNgayCuThe.NGHI_PHEP, LyDo = "Nghỉ lễ Giáng sinh" }
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs1Id, LoaiGiayTo.BANG_CAP, DocBangThacSi, "bang-thac-si-toan-hoc.jpg", true, new DateTime(2026, 1, 16, 9, 0, 0, DateTimeKind.Utc)),
                TaoGiayTo(gs1Id, LoaiGiayTo.CAN_CUOC, DocCanCuoc, "xac-minh-cccd-hoang-minh.png", true, new DateTime(2026, 1, 16, 9, 5, 0, DateTimeKind.Utc))
            ]
        };

        var gs2 = new GiaSu
        {
            Id = gs2Id,
            Email = "thuha.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Trần Thị Thu Hà",
            SoDienThoai = "0908111002",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279558/tutorhub/public/tutors/thu-ha.jpg",
            KhuVuc = "Quận 3, TP. Hồ Chí Minh",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giáo viên Tiếng Anh & Luyện thi IELTS 8.0 · TESOL Quốc tế",
            GioiThieu = "6 năm luyện thi IELTS (4 kỹ năng, chấm chữa Writing/Speaking 1-1 chuyên sâu) và tiếng Anh giao tiếp phản xạ trực tuyến cho học sinh, sinh viên toàn quốc.",
            HocVan = "Cử nhân Sư phạm Tiếng Anh — ĐH Sư phạm TP.HCM (IELTS 8.0, TESOL)",
            SoNamKinhNghiem = 6,
            DayTrucTuyen = true,
            DayTrucTiep = false,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 0,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs2Id, NguoiDungThamGiaId = gs2Id, TaoLuc = new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs2Id, TenNganHang = "ACB", SoTaiKhoan = "1889900223344", ChuTaiKhoan = "TRAN THI THU HA" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs2Anh, GiaSuId = gs2Id, MonHocId = MonId("Tiếng Anh"), GiaMoiGio = 280_000 },
                new GiaSuMonHoc { Id = gsmGs2Ielts, GiaSuId = gs2Id, MonHocId = MonId("Luyện thi IELTS"), GiaMoiGio = 320_000 },
                new GiaSuMonHoc { Id = gsmGs2Gt, GiaSuId = gs2Id, MonHocId = MonId("Tiếng Anh giao tiếp"), GiaMoiGio = 260_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs2Id, 3, 18, 30, 21, 30, HinhThucLichTuan.TRUC_TUYEN),
                TaoLichTuan(gs2Id, 5, 18, 30, 21, 30, HinhThucLichTuan.TRUC_TUYEN),
                TaoLichTuan(gs2Id, 7, 14, 0, 18, 0, HinhThucLichTuan.TRUC_TUYEN),
                TaoLichTuan(gs2Id, 1, 9, 0, 12, 0, HinhThucLichTuan.TRUC_TUYEN)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs2Id, LoaiGiayTo.CHUNG_CHI, DocChungChi, "chung-chi-ielts-8-0-tesol.png", true, new DateTime(2026, 1, 19, 10, 0, 0, DateTimeKind.Utc)),
                TaoGiayTo(gs2Id, LoaiGiayTo.BANG_CAP, DocBangDaiHoc, "bang-su-pham-tieng-anh.png", true, new DateTime(2026, 1, 19, 10, 2, 0, DateTimeKind.Utc))
            ]
        };

        var gs3 = new GiaSu
        {
            Id = gs3Id,
            Email = "quocbao.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "TS. Đặng Quốc Bảo",
            SoDienThoai = "0908111003",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279562/tutorhub/public/tutors/quoc-bao.jpg",
            KhuVuc = "Quận 1, TP. Hồ Chí Minh",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Tiến sĩ Hóa học — Chuyên bồi dưỡng HSG & Luyện thi Đại học khối A, B",
            GioiThieu = "10 năm kinh nghiệm giảng dạy Hóa học và Sinh học luyện thi Y Dược, Bách Khoa. Hệ thống hóa sơ đồ tư duy phản ứng hữu cơ - vô cơ giúp giải nhanh trắc nghiệm trong 30 giây.",
            HocVan = "Tiến sĩ Hóa học ứng dụng — ĐH Quốc gia TP.HCM",
            SoNamKinhNghiem = 10,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 15,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs3Id, NguoiDungThamGiaId = gs3Id, TaoLuc = new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs3Id, TenNganHang = "Techcombank", SoTaiKhoan = "19035566778899", ChuTaiKhoan = "DANG QUOC BAO" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs3Hoa, GiaSuId = gs3Id, MonHocId = MonId("Hóa học"), GiaMoiGio = 280_000 },
                new GiaSuMonHoc { Id = gsmGs3Sinh, GiaSuId = gs3Id, MonHocId = MonId("Sinh học"), GiaMoiGio = 260_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs3Id, 2, 18, 30, 21, 30),
                TaoLichTuan(gs3Id, 5, 18, 30, 21, 30),
                TaoLichTuan(gs3Id, 7, 14, 0, 18, 0)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs3Id, LoaiGiayTo.BANG_CAP, DocBangThacSi, "bang-tien-si-hoa-hoc.jpg", true, new DateTime(2026, 1, 21, 10, 0, 0, DateTimeKind.Utc))
            ]
        };

        var gs4 = new GiaSu
        {
            Id = gs4Id,
            Email = "ngocmai.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Lương Ngọc Mai",
            SoDienThoai = "0908111004",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279565/tutorhub/public/tutors/ngoc-mai.jpg",
            KhuVuc = "Cầu Giấy, Hà Nội",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 22, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 22, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giáo viên Ngữ văn & Lịch sử — Luyện thi vào 10 và THPT Quốc gia",
            GioiThieu = "Rèn kỹ năng đọc hiểu, viết nghị luận xã hội sâu sắc, chấm sửa chi tiết từng đoạn văn cho học sinh lớp 9 và lớp 12 theo chương trình GDPT mới.",
            HocVan = "Cử nhân Sư phạm Ngữ văn — ĐH Sư phạm Hà Nội",
            SoNamKinhNghiem = 5,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 12,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs4Id, NguoiDungThamGiaId = gs4Id, TaoLuc = new DateTime(2026, 1, 22, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs4Id, TenNganHang = "MBBank", SoTaiKhoan = "098877665544", ChuTaiKhoan = "LUONG NGOC MAI" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs4Van, GiaSuId = gs4Id, MonHocId = MonId("Ngữ văn"), GiaMoiGio = 190_000 },
                new GiaSuMonHoc { Id = gsmGs4Su, GiaSuId = gs4Id, MonHocId = MonId("Lịch sử"), GiaMoiGio = 180_000 },
                new GiaSuMonHoc { Id = gsmGs4Nl, GiaSuId = gs4Id, MonHocId = MonId("Kỹ năng làm văn nghị luận"), GiaMoiGio = 210_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs4Id, 3, 18, 0, 21, 0),
                TaoLichTuan(gs4Id, 6, 18, 0, 21, 0),
                TaoLichTuan(gs4Id, 1, 14, 0, 17, 30)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs4Id, LoaiGiayTo.BANG_CAP, DocBangDaiHoc, "bang-su-pham-ngu-van.png", true, new DateTime(2026, 1, 23, 10, 0, 0, DateTimeKind.Utc))
            ]
        };

        var gs5 = new GiaSu
        {
            Id = gs5Id,
            Email = "quanghuy.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "ThS. Phạm Quang Huy",
            SoDienThoai = "0908111005",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279568/tutorhub/public/tutors/quang-huy.jpg",
            KhuVuc = "Hải Châu, Đà Nẵng",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 24, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 24, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Thạc sĩ KHMT — Dạy Lập trình Python, SQL & Ôn thi HSG Tin học",
            GioiThieu = "7 năm kinh nghiệm giảng dạy lập trình thực chiến (Python, giải thuật, cơ sở dữ liệu SQL) cho học sinh chuyên Tin, sinh viên CNTT và người chuyển ngành.",
            HocVan = "Thạc sĩ Khoa học Máy tính — ĐH Bách khoa Đà Nẵng",
            SoNamKinhNghiem = 7,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 10,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs5Id, NguoiDungThamGiaId = gs5Id, TaoLuc = new DateTime(2026, 1, 24, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs5Id, TenNganHang = "VPBank", SoTaiKhoan = "220044668811", ChuTaiKhoan = "PHAM QUANG HUY" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs5Python, GiaSuId = gs5Id, MonHocId = MonId("Lập trình Python"), GiaMoiGio = 260_000 },
                new GiaSuMonHoc { Id = gsmGs5Tin, GiaSuId = gs5Id, MonHocId = MonId("Tin học"), GiaMoiGio = 220_000 },
                new GiaSuMonHoc { Id = gsmGs5Sql, GiaSuId = gs5Id, MonHocId = MonId("Cơ sở dữ liệu & SQL"), GiaMoiGio = 250_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs5Id, 2, 19, 0, 21, 30),
                TaoLichTuan(gs5Id, 4, 19, 0, 21, 30),
                TaoLichTuan(gs5Id, 7, 9, 0, 12, 0),
                TaoLichTuan(gs5Id, 1, 15, 0, 18, 0)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs5Id, LoaiGiayTo.BANG_CAP, DocBangThacSi, "bang-thac-si-khmt-quang-huy.jpg", true, new DateTime(2026, 1, 25, 9, 0, 0, DateTimeKind.Utc))
            ]
        };

        var gs6 = new GiaSu
        {
            Id = gs6Id,
            Email = "thaonguyen.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Lê Thảo Nguyên",
            SoDienThoai = "0908111006",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279572/tutorhub/public/tutors/thao-nguyen.jpg",
            KhuVuc = "Cái Răng, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 26, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 26, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Gia sư Tiếng Anh giao tiếp & Luyện thi TOEIC 920+ thân thiện",
            GioiThieu = "Tốt nghiệp loại Giỏi Ngôn ngữ Anh ĐH Cần Thơ, TOEIC 945/990. Chuyên kèm phản xạ nghe nói thực tế, ngữ pháp nền tảng và luyện đề TOEIC cấp tốc.",
            HocVan = "Cử nhân Ngôn ngữ Anh — ĐH Cần Thơ (TOEIC 945)",
            SoNamKinhNghiem = 5,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 8,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs6Id, NguoiDungThamGiaId = gs6Id, TaoLuc = new DateTime(2026, 1, 26, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs6Id, TenNganHang = "BIDV", SoTaiKhoan = "7411000889922", ChuTaiKhoan = "LE THAO NGUYEN" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs6Gt, GiaSuId = gs6Id, MonHocId = MonId("Tiếng Anh giao tiếp"), GiaMoiGio = 210_000 },
                new GiaSuMonHoc { Id = gsmGs6Toeic, GiaSuId = gs6Id, MonHocId = MonId("Luyện thi TOEIC"), GiaMoiGio = 230_000 },
                new GiaSuMonHoc { Id = gsmGs6Anh, GiaSuId = gs6Id, MonHocId = MonId("Tiếng Anh"), GiaMoiGio = 200_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs6Id, 2, 18, 0, 21, 0),
                TaoLichTuan(gs6Id, 5, 18, 0, 21, 0),
                TaoLichTuan(gs6Id, 7, 15, 0, 18, 30)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs6Id, LoaiGiayTo.CHUNG_CHI, DocChungChi, "chung-chi-toeic-945.png", true, new DateTime(2026, 1, 27, 9, 0, 0, DateTimeKind.Utc))
            ]
        };

        var gs7 = new GiaSu
        {
            Id = gs7Id,
            Email = "giakiet.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Thầy Huỳnh Gia Kiệt",
            SoDienThoai = "0908111007",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279575/tutorhub/public/tutors/gia-kiet.jpg",
            KhuVuc = "Bình Thạnh, TP. Hồ Chí Minh",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 28, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 28, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Cựu HSG Quốc gia Toán — Luyện thi vào lớp 10 chuyên & Toán THPT",
            GioiThieu = "Giải Nhì HSG Quốc gia môn Toán, 6 năm dạy kèm Toán THCS/THPT và Tin học lập trình. Lộ trình cá nhân hóa giúp học sinh yêu thích tư duy logic.",
            HocVan = "Cử nhân Tài năng Toán Tin — ĐH Khoa học Tự nhiên TP.HCM",
            SoNamKinhNghiem = 6,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 12,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs7Id, NguoiDungThamGiaId = gs7Id, TaoLuc = new DateTime(2026, 1, 28, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs7Id, TenNganHang = "TPBank", SoTaiKhoan = "0334455667788", ChuTaiKhoan = "HUYNH GIA KIET" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs7Toan, GiaSuId = gs7Id, MonHocId = MonId("Toán"), GiaMoiGio = 230_000 },
                new GiaSuMonHoc { Id = gsmGs7Lop10, GiaSuId = gs7Id, MonHocId = MonId("Luyện thi vào lớp 10"), GiaMoiGio = 220_000 },
                new GiaSuMonHoc { Id = gsmGs7Tin, GiaSuId = gs7Id, MonHocId = MonId("Tin học"), GiaMoiGio = 240_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs7Id, 3, 18, 0, 21, 30),
                TaoLichTuan(gs7Id, 6, 18, 0, 21, 30),
                TaoLichTuan(gs7Id, 7, 8, 30, 11, 30),
                TaoLichTuan(gs7Id, 1, 14, 0, 17, 0)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs7Id, LoaiGiayTo.BANG_CAP, DocBangDaiHoc, "bang-cu-nhan-toan-tin.png", true, new DateTime(2026, 1, 29, 9, 0, 0, DateTimeKind.Utc))
            ]
        };

        var gs8 = new GiaSu
        {
            Id = gs8Id,
            Email = "minhchau.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Đỗ Minh Châu",
            SoDienThoai = "0908111008",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279579/tutorhub/public/tutors/minh-chau.jpg",
            KhuVuc = "Đống Đa, Hà Nội",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giảng viên Tiếng Nhật JLPT N1 & Tiếng Trung HSK 6 — Dạy Online 1-1",
            GioiThieu = "Từng học tập và làm việc 4 năm tại Tokyo. Chuyên luyện thi JLPT N5–N1, giao tiếp tiếng Nhật công sở và luyện thi tiếng Trung HSK1–HSK6 qua phòng học trực tuyến.",
            HocVan = "Thạc sĩ Ngôn ngữ Nhật — ĐH Ngoại ngữ ĐHQG Hà Nội (JLPT N1, HSK 6)",
            SoNamKinhNghiem = 7,
            DayTrucTuyen = true,
            DayTrucTiep = false,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 0,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs8Id, NguoiDungThamGiaId = gs8Id, TaoLuc = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs8Id, TenNganHang = "VietinBank", SoTaiKhoan = "108877665544", ChuTaiKhoan = "DO MINH CHAU" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs8Nhat, GiaSuId = gs8Id, MonHocId = MonId("Tiếng Nhật"), GiaMoiGio = 270_000 },
                new GiaSuMonHoc { Id = gsmGs8Jlpt, GiaSuId = gs8Id, MonHocId = MonId("Luyện thi JLPT"), GiaMoiGio = 290_000 },
                new GiaSuMonHoc { Id = gsmGs8Trung, GiaSuId = gs8Id, MonHocId = MonId("Tiếng Trung"), GiaMoiGio = 260_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs8Id, 2, 19, 0, 21, 30, HinhThucLichTuan.TRUC_TUYEN),
                TaoLichTuan(gs8Id, 4, 19, 0, 21, 30, HinhThucLichTuan.TRUC_TUYEN),
                TaoLichTuan(gs8Id, 6, 19, 0, 21, 30, HinhThucLichTuan.TRUC_TUYEN),
                TaoLichTuan(gs8Id, 1, 9, 0, 12, 0, HinhThucLichTuan.TRUC_TUYEN)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs8Id, LoaiGiayTo.CHUNG_CHI, DocChungChi, "chung-chi-jlpt-n1-hsk6.png", true, new DateTime(2026, 2, 2, 9, 0, 0, DateTimeKind.Utc))
            ]
        };

        var gs9 = new GiaSu
        {
            Id = gs9Id,
            Email = "anhtuan.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Thầy Bùi Anh Tuấn",
            SoDienThoai = "0908111009",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279584/tutorhub/public/tutors/anh-tuan.jpg",
            KhuVuc = "Ninh Kiều, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 2, 3, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 2, 3, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giáo viên Vật lý & Hóa học kèm trực tiếp tại nhà khu vực Cần Thơ",
            GioiThieu = "9 năm giảng dạy Vật lý và Hóa học THPT tại Cần Thơ. Chỉ nhận kèm trực tiếp 1-1 tại nhà học sinh trong bán kính 12 km giúp rèn kỷ luật học tập và chữa bài trực tiếp.",
            HocVan = "Cử nhân Sư phạm Vật lý — ĐH Cần Thơ",
            SoNamKinhNghiem = 9,
            DayTrucTuyen = false,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = false, // Bắt buộc false khi DayTrucTuyen = false
            BanKinhPhucVuKm = 12,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs9Id, NguoiDungThamGiaId = gs9Id, TaoLuc = new DateTime(2026, 2, 3, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs9Id, TenNganHang = "Sacombank", SoTaiKhoan = "070011223344", ChuTaiKhoan = "BUI ANH TUAN" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs9Ly, GiaSuId = gs9Id, MonHocId = MonId("Vật lý"), GiaMoiGio = 200_000 },
                new GiaSuMonHoc { Id = gsmGs9Hoa, GiaSuId = gs9Id, MonHocId = MonId("Hóa học"), GiaMoiGio = 210_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs9Id, 3, 17, 30, 21, 0, HinhThucLichTuan.TRUC_TIEP),
                TaoLichTuan(gs9Id, 5, 17, 30, 21, 0, HinhThucLichTuan.TRUC_TIEP),
                TaoLichTuan(gs9Id, 7, 14, 0, 18, 0, HinhThucLichTuan.TRUC_TIEP)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs9Id, LoaiGiayTo.BANG_CAP, DocBangDaiHoc, "bang-su-pham-vat-ly.png", true, new DateTime(2026, 2, 4, 9, 0, 0, DateTimeKind.Utc))
            ]
        };

        var gs10 = new GiaSu
        {
            Id = gs10Id,
            Email = "phuonglinh.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Nguyễn Phương Linh",
            SoDienThoai = "0908111010",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279588/tutorhub/public/tutors/phuong-linh.jpg",
            KhuVuc = "Thanh Xuân, Hà Nội",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giảng viên Piano & Guitar — Học viện Âm nhạc Quốc gia Việt Nam",
            GioiThieu = "6 năm dạy Piano cổ điển, đệm hát và Guitar cơ bản đến nâng cao cho trẻ em và người lớn. Giáo trình ABRSM dễ hiểu, tạo cảm hứng nghệ thuật ngay từ buổi đầu.",
            HocVan = "Cử nhân Biểu diễn Piano — Học viện Âm nhạc Quốc gia Việt Nam",
            SoNamKinhNghiem = 6,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 10,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs10Id, NguoiDungThamGiaId = gs10Id, TaoLuc = new DateTime(2026, 2, 6, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs10Id, TenNganHang = "Techcombank", SoTaiKhoan = "19039988776655", ChuTaiKhoan = "NGUYEN PHUONG LINH" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs10Piano, GiaSuId = gs10Id, MonHocId = MonId("Piano"), GiaMoiGio = 300_000 },
                new GiaSuMonHoc { Id = gsmGs10Guitar, GiaSuId = gs10Id, MonHocId = MonId("Guitar"), GiaMoiGio = 250_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs10Id, 4, 18, 0, 21, 0),
                TaoLichTuan(gs10Id, 7, 9, 0, 12, 0),
                TaoLichTuan(gs10Id, 1, 14, 0, 18, 0)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs10Id, LoaiGiayTo.BANG_CAP, DocBangDaiHoc, "bang-hoc-vien-am-nhac.png", true, new DateTime(2026, 2, 7, 9, 0, 0, DateTimeKind.Utc))
            ]
        };

        // 2 Gia sư CHỜ DUYỆT (đầy đủ 7/7 bước + giấy tờ Cloudinary Private để Admin duyệt ngay)
        var gs11 = new GiaSu
        {
            Id = gs11Id,
            Email = "thanhtung.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "KS. Võ Thanh Tùng",
            SoDienThoai = "0908111011",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279591/tutorhub/public/tutors/thanh-tung.jpg",
            KhuVuc = "Bình Thủy, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Kỹ sư Phần mềm dạy Tin học & Lập trình C/C++ cơ bản đến nâng cao",
            GioiThieu = "4 năm kinh nghiệm phát triển phần mềm và kèm thuật toán C/C++, Tin học phổ thông cho học sinh THCS/THPT và sinh viên năm nhất.",
            HocVan = "Kỹ sư Công nghệ Thông tin — ĐH Cần Thơ",
            SoNamKinhNghiem = 4,
            DayTrucTuyen = true,
            DayTrucTiep = false,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.CHO_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 0,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs11Id, NguoiDungThamGiaId = gs11Id, TaoLuc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs11Id, TenNganHang = "Vietcombank", SoTaiKhoan = "011199887766", ChuTaiKhoan = "VO THANH TUNG" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs11Tin, GiaSuId = gs11Id, MonHocId = MonId("Tin học"), GiaMoiGio = 200_000 },
                new GiaSuMonHoc { Id = gsmGs11Cpp, GiaSuId = gs11Id, MonHocId = MonId("Lập trình C / C++"), GiaMoiGio = 220_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs11Id, 2, 19, 0, 21, 30, HinhThucLichTuan.TRUC_TUYEN),
                TaoLichTuan(gs11Id, 5, 19, 0, 21, 30, HinhThucLichTuan.TRUC_TUYEN)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs11Id, LoaiGiayTo.CAN_CUOC, DocCanCuoc, "cccd-vo-thanh-tung.png", false, null),
                TaoGiayTo(gs11Id, LoaiGiayTo.BANG_CAP, DocBangDaiHoc, "bang-ky-su-cntt.png", false, null)
            ]
        };

        var gs12 = new GiaSu
        {
            Id = gs12Id,
            Email = "kimyen.gs@tutorhub.vn",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Trương Kim Yến",
            SoDienThoai = "0908111012",
            AnhDaiDienUrl = $"{CldPublicBase}/v1795279594/tutorhub/public/tutors/kim-yen.jpg",
            KhuVuc = "TP. Thủ Đức, TP. Hồ Chí Minh",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 3, 4, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 3, 4, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Gia sư Tiếng Hàn TOPIK 6 — Chuyên giao tiếp & Luyện thi du học Hàn",
            GioiThieu = "Tốt nghiệp Hàn Quốc học loại Xuất sắc, đạt TOPIK cấp 6. Hỗ trợ học viên luyện phát âm chuẩn Seoul, giao tiếp công việc và luyện đề TOPIK I & II.",
            HocVan = "Cử nhân Hàn Quốc học — ĐH KHXH&NV TP.HCM (TOPIK 6)",
            SoNamKinhNghiem = 4,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.CHO_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 10,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs12Id, NguoiDungThamGiaId = gs12Id, TaoLuc = new DateTime(2026, 3, 4, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = gs12Id, TenNganHang = "ACB", SoTaiKhoan = "1667788990011", ChuTaiKhoan = "TRUONG KIM YEN" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsmGs12Han, GiaSuId = gs12Id, MonHocId = MonId("Tiếng Hàn"), GiaMoiGio = 240_000 },
                new GiaSuMonHoc { Id = gsmGs12Topik, GiaSuId = gs12Id, MonHocId = MonId("Luyện thi TOPIK"), GiaMoiGio = 260_000 }
            ],
            DsLichRanh =
            [
                TaoLichTuan(gs12Id, 3, 18, 30, 21, 0),
                TaoLichTuan(gs12Id, 6, 18, 30, 21, 0)
            ],
            DsGiayTo =
            [
                TaoGiayTo(gs12Id, LoaiGiayTo.CAN_CUOC, DocCanCuoc, "cccd-truong-kim-yen.png", false, null),
                TaoGiayTo(gs12Id, LoaiGiayTo.CHUNG_CHI, DocChungChi, "chung-chi-topik-6.png", false, null)
            ]
        };

        var dsGiaSu = new[] { gs1, gs2, gs3, gs4, gs5, gs6, gs7, gs8, gs9, gs10, gs11, gs12 };
        db.GiaSus.AddRange(dsGiaSu);

        // Map ví để tự động tính số dư khớp sổ cái 100%
        var viTheoNguoiDung = dsHocSinh.Select(h => h.TaiKhoanVi!)
            .Concat(dsGiaSu.Select(g => g.TaiKhoanVi!))
            .ToDictionary(v => v.NguoiDungThamGiaId);

        // ══════════════════════════════════════════════════════════════════════
        // 4. MỤC YÊU THÍCH (8 LƯỢT LƯU)
        // ══════════════════════════════════════════════════════════════════════
        db.MucYeuThichs.AddRange(
            new MucYeuThich { HocSinhId = hs1Id, GiaSuId = gs1Id, TaoLuc = now.AddDays(-20) },
            new MucYeuThich { HocSinhId = hs1Id, GiaSuId = gs2Id, TaoLuc = now.AddDays(-18) },
            new MucYeuThich { HocSinhId = hs2Id, GiaSuId = gs3Id, TaoLuc = now.AddDays(-15) },
            new MucYeuThich { HocSinhId = hs2Id, GiaSuId = gs7Id, TaoLuc = now.AddDays(-12) },
            new MucYeuThich { HocSinhId = hs3Id, GiaSuId = gs4Id, TaoLuc = now.AddDays(-10) },
            new MucYeuThich { HocSinhId = hs3Id, GiaSuId = gs8Id, TaoLuc = now.AddDays(-9) },
            new MucYeuThich { HocSinhId = hs4Id, GiaSuId = gs5Id, TaoLuc = now.AddDays(-7) },
            new MucYeuThich { HocSinhId = hs6Id, GiaSuId = gs6Id, TaoLuc = now.AddDays(-5) });

        // ══════════════════════════════════════════════════════════════════════
        // 5. ĐƠN ĐẶT LỊCH & THANH TOÁN (21 ĐƠN PHỦ KÍN MỌI TRẠNG THÁI)
        // ══════════════════════════════════════════════════════════════════════
        Guid DonId(int n) => Guid.Parse($"81000000-0000-0000-0000-{n:000000000000}");
        Guid TtId(int n) => Guid.Parse($"91000000-0000-0000-0000-{n:000000000000}");

        var dsDon = new List<DonDatLich>();
        var dsThanhToan = new List<ThanhToan>();
        var dsDanhGia = new List<DanhGia>();
        var dsGdVi = new List<GiaoDichVi>();
        var soThuTuTheoVi = new Dictionary<Guid, int>();
        var soDuTheoVi = new Dictionary<Guid, int>();
        int gdSeq = 1;

        void ThemButToan(Guid nguoiDungId, LoaiGiaoDichVi loai, int soTien, string ghiChu, DateTime taoLuc, Guid? thanhToanId = null, Guid? rutTienId = null)
        {
            var vi = viTheoNguoiDung[nguoiDungId];
            var stt = soThuTuTheoVi.GetValueOrDefault(vi.Id, 0) + 1;
            var soDuMoi = soDuTheoVi.GetValueOrDefault(vi.Id, 0) + soTien;
            soThuTuTheoVi[vi.Id] = stt;
            soDuTheoVi[vi.Id] = soDuMoi;

            dsGdVi.Add(new GiaoDichVi
            {
                Id = Guid.Parse($"a1000000-0000-0000-0000-{gdSeq++:000000000000}"),
                TaiKhoanViId = vi.Id,
                SoThuTu = stt,
                Loai = loai,
                SoTien = soTien,
                SoDuSau = soDuMoi,
                GhiChu = ghiChu,
                ThanhToanId = thanhToanId,
                YeuCauRutTienId = rutTienId,
                TaoLuc = taoLuc
            });
        }

        // Nạp tiền ban đầu vào ví 6 Học sinh (trước mọi giao dịch thanh toán)
        ThemButToan(hs1Id, LoaiGiaoDichVi.NAP_TIEN, 1_502_000, "Nạp tiền vào ví học tập TutorHub", now.AddDays(-30));
        ThemButToan(hs2Id, LoaiGiaoDichVi.NAP_TIEN, 680_000, "Nạp tiền vào ví học tập TutorHub", now.AddDays(-28));
        ThemButToan(hs3Id, LoaiGiaoDichVi.NAP_TIEN, 950_000, "Nạp tiền vào ví học tập TutorHub", now.AddDays(-26));
        ThemButToan(hs4Id, LoaiGiaoDichVi.NAP_TIEN, 420_000, "Nạp tiền vào ví học tập TutorHub", now.AddDays(-24));
        ThemButToan(hs5Id, LoaiGiaoDichVi.NAP_TIEN, 300_000, "Nạp tiền vào ví học tập TutorHub", now.AddDays(-22));
        ThemButToan(hs6Id, LoaiGiaoDichVi.NAP_TIEN, 500_000, "Nạp tiền vào ví học tập TutorHub", now.AddDays(-20));

        int dgSeq = 1;
        void ThemDonHoanThanh(
            int idx, string maDon, Guid hsId, Guid gsId, Guid gsmId,
            HinhThucBuoiHoc hinhThuc, string? diaChi, int ngayTruoc, double soGio,
            int giaMoiGio, int diem, string binhLuan, string? phanHoiGs)
        {
            var dId = DonId(idx);
            var tId = TtId(idx);
            var bd = now.AddDays(-ngayTruoc).Date.AddHours(11);
            var kt = bd.AddHours(soGio);
            var tongTien = (int)Math.Round(giaMoiGio * soGio);
            var thuNhap = (int)Math.Round(tongTien * 0.88m);

            dsDon.Add(new DonDatLich
            {
                Id = dId,
                MaDon = maDon,
                HocSinhId = hsId,
                GiaSuMonHocId = gsmId,
                HinhThuc = hinhThuc,
                DiaChiHocSinh = diaChi,
                BatDauLuc = bd,
                KetThucLuc = kt,
                GiaMoiGio = giaMoiGio,
                TienGiam = 0,
                TrangThai = TrangThaiDonDatLich.HOAN_THANH,
                LaBuoiHocThu = false,
                PhongHocUrl = hinhThuc == HinhThucBuoiHoc.TRUC_TUYEN ? $"/pages/phong-hoc.html?donId={dId}" : null,
                HanPhanHoi = bd.AddHours(-12),
                TaoLuc = bd.AddDays(-2)
            });

            dsThanhToan.Add(new ThanhToan
            {
                Id = tId,
                DonDatLichId = dId,
                SoTien = tongTien,
                PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
                TrangThai = TrangThaiThanhToan.DA_GIAI_NGAN,
                MaGiaoDichCong = null,
                ThanhToanLuc = bd.AddDays(-2).AddMinutes(5),
                GiaiNganLuc = kt.AddHours(2),
                TienDaHoan = 0,
                TaoLuc = bd.AddDays(-2).AddMinutes(2)
            });

            ThemButToan(gsId, LoaiGiaoDichVi.THU_NHAP, thuNhap, $"Giải ngân thu nhập đơn {maDon} (đã trừ 12% phí nền tảng)", kt.AddHours(2), thanhToanId: tId);

            dsDanhGia.Add(new DanhGia
            {
                Id = Guid.Parse($"b1000000-0000-0000-0000-{dgSeq++:000000000000}"),
                DonDatLichId = dId,
                Diem = diem,
                BinhLuan = binhLuan,
                PhanHoiGiaSu = phanHoiGs,
                TaoLuc = kt.AddHours(3)
            });
        }

        // 11 Đơn HOÀN THÀNH + 11 Đánh giá chân thực phủ khắp 10 gia sư đã duyệt
        ThemDonHoanThanh(1, "BK-20261101-0001", hs1Id, gs1Id, gsmGs1Toan, HinhThucBuoiHoc.TRUC_TUYEN, null, 18, 2.0, 250_000, 5,
            "Thầy Minh giảng phần khảo sát hàm số và tích phân cực kỳ dễ hiểu, bài tập sát đề thi THPT Quốc gia!",
            "Cảm ơn Khoa đã rất tập trung, tuần tới mình sang chuyên đề số phức nhé!");
        ThemDonHoanThanh(2, "BK-20261103-0002", hs6Id, gs1Id, gsmGs1Ly, HinhThucBuoiHoc.TRUC_TIEP, "Số 45 đường 30/4, P. Xuân Khánh, Q. Ninh Kiều, Cần Thơ", 16, 2.0, 240_000, 5,
            "Thầy đến đúng giờ, giảng phần dao động điều hòa bằng hình vẽ trực quan nên em hiểu bài ngay trên lớp.",
            "Hân tiếp thu nhanh, nhớ hoàn thành phiếu bài tập trắc nghiệm thầy giao nhé.");
        ThemDonHoanThanh(3, "BK-20261105-0003", hs1Id, gs2Id, gsmGs2Ielts, HinhThucBuoiHoc.TRUC_TUYEN, null, 14, 2.0, 320_000, 5,
            "Cô Hà sửa bài Writing Task 2 rất chi tiết từng cấu trúc học thuật, band điểm của em tăng rõ rệt sau 2 tuần.",
            "Cố gắng luyện thêm bộ từ vựng chủ đề Education cô gửi nhé Khoa!");
        ThemDonHoanThanh(4, "BK-20261106-0004", hs2Id, gs3Id, gsmGs3Hoa, HinhThucBuoiHoc.TRUC_TIEP, "Số 82 Nguyễn Du, P. Bến Nghé, Quận 1, TP. Hồ Chí Minh", 13, 2.0, 280_000, 5,
            "Thầy Bảo hệ thống sơ đồ phản ứng hữu cơ quá đỉnh, giúp em rút ngắn một nửa thời gian làm bài trắc nghiệm.",
            "Cảm ơn Trang, giữ vững phong độ cho kỳ thi thử sắp tới nhé!");
        ThemDonHoanThanh(5, "BK-20261108-0005", hs3Id, gs4Id, gsmGs4Van, HinhThucBuoiHoc.TRUC_TUYEN, null, 12, 2.0, 190_000, 5,
            "Cô Mai hướng dẫn lập dàn ý nghị luận xã hội rất mạch lạc, gợi mở dẫn chứng thực tế hay.",
            "Gia Bảo có vốn từ phong phú, chỉ cần rèn thêm phần kết bài ấn tượng là đạt 8.5+.");
        ThemDonHoanThanh(6, "BK-20261109-0006", hs4Id, gs5Id, gsmGs5Python, HinhThucBuoiHoc.TRUC_TUYEN, null, 11, 2.0, 260_000, 5,
            "Thầy Huy dạy Python từ cơ bản đến xử lý dữ liệu dễ hiểu, hướng dẫn debug trực tiếp trên màn hình rất tận tâm.",
            "Khánh Vy làm mini-project rất tốt, buổi tới mình sang phần làm việc với SQL nhé!");
        ThemDonHoanThanh(7, "BK-20261110-0007", hs6Id, gs6Id, gsmGs6Gt, HinhThucBuoiHoc.TRUC_TUYEN, null, 10, 1.5, 210_000, 4,
            "Cô Thảo Nguyên phát âm chuẩn và kiên nhẫn sửa từng âm đuôi, không khí buổi học rất thoải mái.",
            "Cảm ơn Hân nhiều, mỗi ngày nghe thêm 15 phút podcast cô gợi ý nhé!");
        ThemDonHoanThanh(8, "BK-20261111-0008", hs5Id, gs7Id, gsmGs7Toan, HinhThucBuoiHoc.TRUC_TIEP, "Số 215 Điện Biên Phủ, P. 15, Q. Bình Thạnh, TP. Hồ Chí Minh", 9, 2.0, 230_000, 5,
            "Thầy Kiệt rất nhiệt tình, bày nhiều mẹo giải nhanh hình học tọa độ Oxyz cực kỳ hiệu quả.",
            "Cảm ơn Nam, tiếp tục phát huy tinh thần tự giác nhé!");
        ThemDonHoanThanh(9, "BK-20261112-0009", hs3Id, gs8Id, gsmGs8Nhat, HinhThucBuoiHoc.TRUC_TUYEN, null, 8, 2.0, 270_000, 5,
            "Cô Châu dạy ngữ pháp N3 dễ nhớ, lồng ghép nhiều tình huống giao tiếp thực tế tại Nhật Bản.",
            "Cảm ơn Gia Bảo đã chuẩn bị từ vựng rất kỹ trước mỗi buổi học!");
        ThemDonHoanThanh(10, "BK-20261113-0010", hs3Id, gs10Id, gsmGs10Piano, HinhThucBuoiHoc.TRUC_TIEP, "Số 18 Trần Thái Tông, P. Dịch Vọng, Q. Cầu Giấy, Hà Nội", 7, 1.5, 300_000, 5,
            "Cô Phương Linh hướng dẫn tư thế tay và đọc bản nhạc rất bài bản, con mình rất thích giờ học Piano.",
            "Cảm ơn phụ huynh và bé đã tin tưởng đồng hành cùng cô!");
        ThemDonHoanThanh(11, "BK-20261114-0011", hs1Id, gs9Id, gsmGs9Ly, HinhThucBuoiHoc.TRUC_TIEP, "Số 128 đường 30/4, P. An Phú, Q. Ninh Kiều, Cần Thơ", 6, 2.0, 200_000, 5,
            "Thầy Tuấn kèm tại nhà rất sát sao, chữa kỹ từng dạng bài dòng điện xoay chiều khó.",
            null);

        // Đơn 12: Buổi học Online ĐANG DIỄN RA NGAY HIỆN TẠI (để bấm "Vào phòng học" test WebSocket + Bảng trắng + Điểm danh ngay!)
        var donLiveId = DonId(12);
        var ttLiveId = TtId(12);
        var bdLive = now.AddMinutes(-5);
        var ktLive = now.AddMinutes(55);
        dsDon.Add(new DonDatLich
        {
            Id = donLiveId,
            MaDon = "BK-20261121-LIVE",
            HocSinhId = hs1Id,
            GiaSuMonHocId = gsmGs1Toan,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdLive,
            KetThucLuc = ktLive,
            GiaMoiGio = 250_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
            LaBuoiHocThu = false,
            PhongHocUrl = $"/pages/phong-hoc.html?donId={donLiveId}",
            HanPhanHoi = bdLive.AddHours(-6),
            TaoLuc = bdLive.AddDays(-1)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttLiveId,
            DonDatLichId = donLiveId,
            SoTien = 250_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            ThanhToanLuc = bdLive.AddHours(-10),
            TienDaHoan = 0,
            TaoLuc = bdLive.AddHours(-10)
        });

        // Đơn 13: Buổi học Online vừa kết thúc 2h trước (trong cửa sổ 24h -> test ngay nút "Xác nhận hoàn thành" & "Khiếu nại")
        var donCuaSoId = DonId(13);
        var ttCuaSoId = TtId(13);
        var bdCuaSo = now.AddHours(-3);
        var ktCuaSo = now.AddHours(-2);
        dsDon.Add(new DonDatLich
        {
            Id = donCuaSoId,
            MaDon = "BK-20261121-0013",
            HocSinhId = hs1Id,
            GiaSuMonHocId = gsmGs1Toan,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdCuaSo,
            KetThucLuc = ktCuaSo,
            GiaMoiGio = 250_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
            LaBuoiHocThu = false,
            PhongHocUrl = $"/pages/phong-hoc.html?donId={donCuaSoId}",
            HanPhanHoi = bdCuaSo.AddHours(-6),
            TaoLuc = bdCuaSo.AddDays(-1)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttCuaSoId,
            DonDatLichId = donCuaSoId,
            SoTien = 250_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            ThanhToanLuc = bdCuaSo.AddHours(-12),
            TienDaHoan = 0,
            TaoLuc = bdCuaSo.AddHours(-12)
        });

        // Đơn 14: Buổi học Online sắp diễn ra (thanh toán bằng VÍ NỘI BỘ + áp mã WELCOME10 -> 280k - 28k = 252k)
        var donSap1Id = DonId(14);
        var ttSap1Id = TtId(14);
        var bdSap1 = now.AddDays(3).Date.AddHours(12);
        dsDon.Add(new DonDatLich
        {
            Id = donSap1Id,
            MaDon = "BK-20261125-0014",
            HocSinhId = hs1Id,
            GiaSuMonHocId = gsmGs2Anh,
            MaGiamGiaId = maWelcomeId,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdSap1,
            KetThucLuc = bdSap1.AddHours(1),
            GiaMoiGio = 280_000,
            TienGiam = 28_000,
            TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
            LaBuoiHocThu = false,
            PhongHocUrl = $"/pages/phong-hoc.html?donId={donSap1Id}",
            HanPhanHoi = bdSap1.AddDays(-2),
            TaoLuc = now.AddDays(-1)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttSap1Id,
            DonDatLichId = donSap1Id,
            SoTien = 252_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            ThanhToanLuc = now.AddDays(-1).AddMinutes(2),
            TienDaHoan = 0,
            TaoLuc = now.AddDays(-1).AddMinutes(1)
        });
        ThemButToan(hs1Id, LoaiGiaoDichVi.THANH_TOAN, -252_000, "Thanh toán đơn BK-20261125-0014 bằng ví nội bộ", now.AddDays(-1).AddMinutes(2), thanhToanId: ttSap1Id);

        // Đơn 15: Buổi học Tại nhà sắp diễn ra (HS2 + GS3 Hóa học 1.5h = 420k)
        var donSap2Id = DonId(15);
        var ttSap2Id = TtId(15);
        var bdSap2 = now.AddDays(4).Date.AddHours(11);
        dsDon.Add(new DonDatLich
        {
            Id = donSap2Id,
            MaDon = "BK-20261126-0015",
            HocSinhId = hs2Id,
            GiaSuMonHocId = gsmGs3Hoa,
            HinhThuc = HinhThucBuoiHoc.TRUC_TIEP,
            DiaChiHocSinh = "Số 82 Nguyễn Du, P. Bến Nghé, Quận 1, TP. Hồ Chí Minh",
            BatDauLuc = bdSap2,
            KetThucLuc = bdSap2.AddMinutes(90),
            GiaMoiGio = 280_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
            LaBuoiHocThu = false,
            HanPhanHoi = bdSap2.AddDays(-2),
            TaoLuc = now.AddDays(-1)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttSap2Id,
            DonDatLichId = donSap2Id,
            SoTien = 420_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            MaGiaoDichCong = null,
            ThanhToanLuc = now.AddDays(-1).AddMinutes(5),
            TienDaHoan = 0,
            TaoLuc = now.AddDays(-1).AddMinutes(2)
        });

        // Đơn 16: Buổi học Online sắp diễn ra (HS4 + GS5 Python 1.5h = 390k)
        var donSap3Id = DonId(16);
        var ttSap3Id = TtId(16);
        var bdSap3 = now.AddDays(5).Date.AddHours(12);
        dsDon.Add(new DonDatLich
        {
            Id = donSap3Id,
            MaDon = "BK-20261127-0016",
            HocSinhId = hs4Id,
            GiaSuMonHocId = gsmGs5Python,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdSap3,
            KetThucLuc = bdSap3.AddMinutes(90),
            GiaMoiGio = 260_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
            LaBuoiHocThu = false,
            PhongHocUrl = $"/pages/phong-hoc.html?donId={donSap3Id}",
            HanPhanHoi = bdSap3.AddDays(-2),
            TaoLuc = now.AddHours(-18)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttSap3Id,
            DonDatLichId = donSap3Id,
            SoTien = 390_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            MaGiaoDichCong = null,
            ThanhToanLuc = now.AddHours(-18).AddMinutes(3),
            TienDaHoan = 0,
            TaoLuc = now.AddHours(-18).AddMinutes(1)
        });

        // Đơn 17: CHỜ XÁC NHẬN (HS5 đặt GS1 Toán 1h = 250k, đã thanh toán ký quỹ) -> để GS1 test Xác nhận / Từ chối
        var donCho1Id = DonId(17);
        var ttCho1Id = TtId(17);
        var bdCho1 = now.AddDays(6).Date.AddHours(11);
        dsDon.Add(new DonDatLich
        {
            Id = donCho1Id,
            MaDon = "BK-20261128-0017",
            HocSinhId = hs5Id,
            GiaSuMonHocId = gsmGs1Toan,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdCho1,
            KetThucLuc = bdCho1.AddHours(1),
            GiaMoiGio = 250_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.CHO_XAC_NHAN,
            LaBuoiHocThu = false,
            HanPhanHoi = now.AddHours(24),
            TaoLuc = now.AddHours(-2)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttCho1Id,
            DonDatLichId = donCho1Id,
            SoTien = 250_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            ThanhToanLuc = now.AddHours(-2).AddMinutes(1),
            TienDaHoan = 0,
            TaoLuc = now.AddHours(-2)
        });

        // Đơn 18: CHỜ XÁC NHẬN — Buổi HỌC THỬ Online 0đ (HS2 đặt GS7 Toán)
        var donHocThuId = DonId(18);
        var bdHocThu = now.AddDays(6).Date.AddHours(12);
        dsDon.Add(new DonDatLich
        {
            Id = donHocThuId,
            MaDon = "BK-20261128-0018",
            HocSinhId = hs2Id,
            GiaSuMonHocId = gsmGs7Toan,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdHocThu,
            KetThucLuc = bdHocThu.AddMinutes(45),
            GiaMoiGio = 0,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.CHO_XAC_NHAN,
            LaBuoiHocThu = true,
            HanPhanHoi = now.AddHours(24),
            TaoLuc = now.AddHours(-1)
        });

        // Đơn 19: ĐÃ HỦY & ĐÃ HOÀN TIỀN 100% VỀ VÍ HỌC SINH (HS2 hủy trước hạn)
        var donHuyId = DonId(19);
        var ttHuyId = TtId(19);
        var bdHuy = now.AddDays(-2).Date.AddHours(12);
        dsDon.Add(new DonDatLich
        {
            Id = donHuyId,
            MaDon = "BK-20261119-0019",
            HocSinhId = hs2Id,
            GiaSuMonHocId = gsmGs6Anh,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdHuy,
            KetThucLuc = bdHuy.AddHours(1),
            GiaMoiGio = 200_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.DA_HUY,
            LyDoHuy = "Học sinh bận lịch thi đột xuất tại trường, hủy trước 24 giờ.",
            LaBuoiHocThu = false,
            HanPhanHoi = bdHuy.AddDays(-2),
            TaoLuc = bdHuy.AddDays(-4)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttHuyId,
            DonDatLichId = donHuyId,
            SoTien = 200_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DA_HOAN,
            ThanhToanLuc = bdHuy.AddDays(-4).AddMinutes(2),
            TienDaHoan = 200_000,
            TaoLuc = bdHuy.AddDays(-4).AddMinutes(1)
        });
        ThemButToan(hs2Id, LoaiGiaoDichVi.THANH_TOAN, -200_000, "Thanh toán đơn BK-20261119-0019 bằng ví nội bộ", bdHuy.AddDays(-4).AddMinutes(2), thanhToanId: ttHuyId);
        ThemButToan(hs2Id, LoaiGiaoDichVi.HOAN_TIEN, 200_000, "Hoàn 100% học phí đơn BK-20261119-0019 do hủy trước hạn", bdHuy.AddDays(-2).AddHours(-26), thanhToanId: ttHuyId);

        // Đơn 20 & 21: 2 Đơn TRANH CHẤP + 2 Khiếu nại đang MỞ có minh chứng thật trên Cloudinary Private
        var donTc1Id = DonId(20);
        var ttTc1Id = TtId(20);
        var bdTc1 = now.AddDays(-4).Date.AddHours(11);
        dsDon.Add(new DonDatLich
        {
            Id = donTc1Id,
            MaDon = "BK-20261117-0020",
            HocSinhId = hs2Id,
            GiaSuMonHocId = gsmGs1Ly,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdTc1,
            KetThucLuc = bdTc1.AddHours(1),
            GiaMoiGio = 240_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.TRANH_CHAP,
            LaBuoiHocThu = false,
            PhongHocUrl = $"/pages/phong-hoc.html?donId={donTc1Id}",
            HanPhanHoi = bdTc1.AddDays(-1),
            TaoLuc = bdTc1.AddDays(-2)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttTc1Id,
            DonDatLichId = donTc1Id,
            SoTien = 240_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            ThanhToanLuc = bdTc1.AddDays(-2).AddMinutes(3),
            TienDaHoan = 0,
            TaoLuc = bdTc1.AddDays(-2).AddMinutes(1)
        });

        var donTc2Id = DonId(21);
        var ttTc2Id = TtId(21);
        var bdTc2 = now.AddDays(-2).Date.AddHours(13);
        dsDon.Add(new DonDatLich
        {
            Id = donTc2Id,
            MaDon = "BK-20261119-0021",
            HocSinhId = hs5Id,
            GiaSuMonHocId = gsmGs3Sinh,
            HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
            BatDauLuc = bdTc2,
            KetThucLuc = bdTc2.AddHours(1),
            GiaMoiGio = 260_000,
            TienGiam = 0,
            TrangThai = TrangThaiDonDatLich.TRANH_CHAP,
            LaBuoiHocThu = false,
            PhongHocUrl = $"/pages/phong-hoc.html?donId={donTc2Id}",
            HanPhanHoi = bdTc2.AddDays(-1),
            TaoLuc = bdTc2.AddDays(-2)
        });
        dsThanhToan.Add(new ThanhToan
        {
            Id = ttTc2Id,
            DonDatLichId = donTc2Id,
            SoTien = 260_000,
            PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            ThanhToanLuc = bdTc2.AddDays(-2).AddMinutes(3),
            TienDaHoan = 0,
            TaoLuc = bdTc2.AddDays(-2).AddMinutes(1)
        });

        db.DonDatLichs.AddRange(dsDon);
        db.ThanhToans.AddRange(dsThanhToan);
        db.DanhGias.AddRange(dsDanhGia);

        // ══════════════════════════════════════════════════════════════════════
        // 6. KHIẾU NẠI (2 KHIẾU NẠI MỞ CÓ MINH CHỨNG CLOUDINARY PRIVATE)
        // ══════════════════════════════════════════════════════════════════════
        db.KhieuNais.AddRange(
            new KhieuNai
            {
                Id = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
                DonDatLichId = donTc1Id,
                DanhMuc = DanhMucKhieuNai.CHAT_LUONG,
                MoTa = "Đường truyền bị gián đoạn khoảng 15 phút cuối buổi nên em chưa kịp hoàn thành hết phần bài tập dự kiến.",
                BangChungUrl = DocMinhChungLop,
                PhanHoiGiaSu = "Mình đã dạy đủ 45 phút đầu và đề nghị học bù miễn phí 20 phút vào tối thứ 6 nhưng học viên muốn hoàn một phần học phí. Đính kèm ảnh chụp bảng trắng đã giảng.",
                PhanHoiGiaSuLuc = bdTc1.AddHours(6),
                BangChungGiaSuUrl = DocMinhChungLop,
                TrangThai = TrangThaiKhieuNai.MO,
                TaoLuc = bdTc1.AddHours(2)
            },
            new KhieuNai
            {
                Id = Guid.Parse("c1000000-0000-0000-0000-000000000002"),
                DonDatLichId = donTc2Id,
                DanhMuc = DanhMucKhieuNai.GIA_SU_VANG_MAT,
                MoTa = "Buổi học bắt đầu muộn 20 phút so với lịch đã xác nhận nên kết thúc sớm hơn dự kiến.",
                BangChungUrl = DocMinhChungLop,
                TrangThai = TrangThaiKhieuNai.MO,
                TaoLuc = bdTc2.AddHours(2)
            });

        // ══════════════════════════════════════════════════════════════════════
        // 7. YÊU CẦU RÚT TIỀN (1 ĐÃ CHI TRẢ + 2 CHỜ XỬ LÝ) & KHỚP SỔ CÁI VÍ 100%
        // ══════════════════════════════════════════════════════════════════════
        var rut1Id = Guid.Parse("d1000000-0000-0000-0000-000000000001");
        var rut2Id = Guid.Parse("d1000000-0000-0000-0000-000000000002");
        var rut3Id = Guid.Parse("d1000000-0000-0000-0000-000000000003");

        var dsRutTien = new[]
        {
            new YeuCauRutTien
            {
                Id = rut1Id,
                GiaSuId = gs1Id,
                SoTien = 300_000,
                TrangThai = TrangThaiRutTien.DA_CHI_TRA,
                GhiChu = "Đã chuyển khoản thành công về Vietcombank.",
                TenNganHang = "Vietcombank",
                SoTaiKhoan = "0111000246810",
                ChuTaiKhoan = "NGUYEN HOANG MINH",
                IdempotencyKey = "SEED-RUT-001",
                TaoLuc = now.AddDays(-8),
                XuLyLuc = now.AddDays(-7),
                NguoiXuLyId = adminId
            },
            new YeuCauRutTien
            {
                Id = rut2Id,
                GiaSuId = gs1Id,
                SoTien = 200_000,
                TrangThai = TrangThaiRutTien.CHO_XU_LY,
                GhiChu = "Rút thu nhập tuần 3 tháng 11",
                TenNganHang = "Vietcombank",
                SoTaiKhoan = "0111000246810",
                ChuTaiKhoan = "NGUYEN HOANG MINH",
                IdempotencyKey = "SEED-RUT-002",
                TaoLuc = now.AddDays(-2)
            },
            new YeuCauRutTien
            {
                Id = rut3Id,
                GiaSuId = gs2Id,
                SoTien = 250_000,
                TrangThai = TrangThaiRutTien.CHO_XU_LY,
                GhiChu = "Rút thu nhập dạy IELTS tháng 11",
                TenNganHang = "ACB",
                SoTaiKhoan = "1889900223344",
                ChuTaiKhoan = "TRAN THI THU HA",
                IdempotencyKey = "SEED-RUT-003",
                TaoLuc = now.AddDays(-1)
            }
        };
        db.YeuCauRutTiens.AddRange(dsRutTien);

        // Ghi bút toán RUT_TIEN cho yêu cầu đã chi trả (rut1Id)
        ThemButToan(gs1Id, LoaiGiaoDichVi.RUT_TIEN, -300_000, "Rút tiền về tài khoản Vietcombank (đã chi trả)", now.AddDays(-7), rutTienId: rut1Id);

        db.GiaoDichVis.AddRange(dsGdVi);

        // Cập nhật chính xác SoDu và SoDuDangXuLyRut cho toàn bộ 18 ví theo đúng bất biến sổ cái
        var tongDangRutTheoGs = dsRutTien
            .Where(r => r.TrangThai == TrangThaiRutTien.CHO_XU_LY)
            .GroupBy(r => r.GiaSuId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.SoTien));

        foreach (var (nguoiDungId, vi) in viTheoNguoiDung)
        {
            var tongSoCai = soDuTheoVi.GetValueOrDefault(vi.Id, 0);
            var dangRut = tongDangRutTheoGs.GetValueOrDefault(nguoiDungId, 0);
            vi.SoDuDangXuLyRut = dangRut;
            vi.SoDu = tongSoCai - dangRut;
        }

        // ══════════════════════════════════════════════════════════════════════
        // 8. HỘI THOẠI & TIN NHẮN (6 HỘI THOẠI, CÓ 1 TIN NHẮN BỊ GẮN CỜ KIỂM DUYỆT)
        // ══════════════════════════════════════════════════════════════════════
        var ht1Id = Guid.Parse("e1000000-0000-0000-0000-000000000001");
        var ht2Id = Guid.Parse("e1000000-0000-0000-0000-000000000002");
        var ht3Id = Guid.Parse("e1000000-0000-0000-0000-000000000003");
        var ht4Id = Guid.Parse("e1000000-0000-0000-0000-000000000004");

        int tnSeq = 1;
        TinNhan TaoTinNhan(Guid htId, Guid nguoiGuiId, string noiDung, DateTime taoLuc, bool daDoc = true, bool biGanCo = false) => new()
        {
            Id = Guid.Parse($"f1000000-0000-0000-0000-{tnSeq++:000000000000}"),
            HoiThoaiId = htId,
            NguoiGuiId = nguoiGuiId,
            NoiDung = noiDung,
            DocLuc = daDoc ? taoLuc.AddMinutes(5) : null,
            BiGanCo = biGanCo,
            TaoLuc = taoLuc
        };

        db.HoiThoais.AddRange(
            new HoiThoai
            {
                Id = ht1Id,
                HocSinhId = hs1Id,
                GiaSuId = gs1Id,
                TaoLuc = now.AddDays(-18),
                DsTinNhan =
                [
                    TaoTinNhan(ht1Id, hs1Id, "Em chào thầy Minh ạ, tối nay mình tiếp tục ôn chuyên đề tích phân đúng không thầy?", now.AddHours(-4)),
                    TaoTinNhan(ht1Id, gs1Id, "Đúng rồi Khoa nhé, thầy đã chuẩn bị sẵn phiếu 20 câu vận dụng cao trên bảng trắng rồi.", now.AddHours(-3).AddMinutes(40)),
                    TaoTinNhan(ht1Id, hs1Id, "Dạ vâng em cảm ơn thầy, đúng giờ em vào phòng học trực tuyến ạ!", now.AddHours(-3), daDoc: false)
                ]
            },
            new HoiThoai
            {
                Id = ht2Id,
                HocSinhId = hs1Id,
                GiaSuId = gs2Id,
                TaoLuc = now.AddDays(-14),
                DsTinNhan =
                [
                    TaoTinNhan(ht2Id, hs1Id, "Cô Hà ơi, buổi thứ 4 tuần tới mình luyện Speaking Part 2 chủ đề Technology cô nhé.", now.AddDays(-1)),
                    TaoTinNhan(ht2Id, gs2Id, "Ok Khoa, cô đã lưu lịch. Em chuẩn bị trước 3 ý chính cho đề bài cô gửi nha!", now.AddHours(-20))
                ]
            },
            new HoiThoai
            {
                Id = ht3Id,
                HocSinhId = hs4Id,
                GiaSuId = gs5Id,
                TaoLuc = now.AddDays(-10),
                DsTinNhan =
                [
                    TaoTinNhan(ht3Id, hs4Id, "Thầy Huy ơi, em cài xong Python 3.12 và VS Code rồi ạ.", now.AddDays(-2)),
                    TaoTinNhan(ht3Id, gs5Id, "Tuyệt vời Khánh Vy! Buổi học tới mình sẽ viết script phân tích file CSV thực tế luôn.", now.AddDays(-1).AddHours(-12))
                ]
            },
            new HoiThoai
            {
                Id = ht4Id,
                HocSinhId = hs5Id,
                GiaSuId = gs7Id,
                TaoLuc = now.AddDays(-6),
                DsTinNhan =
                [
                    TaoTinNhan(ht4Id, hs5Id, "Thầy Kiệt ơi cho em xin tài liệu ôn thi hình học không gian với ạ.", now.AddDays(-3)),
                    TaoTinNhan(ht4Id, hs5Id, "Em có thể nhắn qua số ngoài hệ thống 0987***123 để trao đổi nhanh không thầy?", now.AddDays(-3).AddMinutes(10), biGanCo: true),
                    TaoTinNhan(ht4Id, gs7Id, "Mình cứ trao đổi và gửi bài tập trực tiếp trên TutorHub để bảo đảm quyền lợi học phí an toàn em nhé!", now.AddDays(-3).AddMinutes(25))
                ]
            });

        // ══════════════════════════════════════════════════════════════════════
        // 9. THÔNG BÁO MẪU CHO HỌC SINH, GIA SƯ & QUẢN TRỊ VIÊN
        // ══════════════════════════════════════════════════════════════════════
        db.ThongBaos.AddRange(
            new ThongBao
            {
                NguoiNhanId = hs1Id,
                Loai = LoaiThongBao.XAC_NHAN_LICH,
                TieuDe = "Buổi học Toán trực tuyến đang diễn ra",
                NoiDung = "Phòng học trực tuyến cho đơn BK-20261121-LIVE cùng ThS. Nguyễn Hoàng Minh đã sẵn sàng. Bạn có thể bấm Vào phòng học ngay!",
                TaoLuc = now.AddMinutes(-10)
            },
            new ThongBao
            {
                NguoiNhanId = gs1Id,
                Loai = LoaiThongBao.DAT_LICH_MOI,
                TieuDe = "Bạn có đơn đặt lịch mới BK-20261128-0017",
                NoiDung = "Học viên Võ Hoàng Nam đã đặt buổi học Toán trực tuyến (250.000 ₫). Vui lòng xác nhận trong vòng 24 giờ.",
                TaoLuc = now.AddHours(-2)
            },
            new ThongBao
            {
                NguoiNhanId = adminId,
                Loai = LoaiThongBao.HE_THONG,
                TieuDe = "Có 8 hồ sơ gia sư mới & 2 yêu cầu rút tiền đang chờ duyệt",
                NoiDung = "Hệ thống hiện có 188 gia sư trên toàn quốc (8 hồ sơ chờ duyệt đã nộp đủ giấy tờ xác minh trên Cloudinary Private).",
                TaoLuc = now.AddHours(-1)
            });

        // ══════════════════════════════════════════════════════════════════════
        // 10. BỘ DỮ LIỆU MỞ RỘNG ĐỒ SỘ (+176 GIA SƯ = TỔNG 188 GIA SƯ, +10 HỌC SINH, +88 ĐƠN HOÀN THÀNH & ĐÁNH GIÁ)
        //     - Dày đặc tại Cần Thơ (đặc biệt P. An Bình / Nguyễn Văn Cừ nối dài gần ĐH Nam Cần Thơ, Ninh Kiều, Cái Răng, Bình Thủy)
        //     - Đa số (~81%) là Giáo viên, Giảng viên, Thạc sĩ, Nghệ sĩ Thanh nhạc / Guitar / Piano / Mỹ thuật / CNTT / Ngoại ngữ
        //     - Tỷ lệ vừa phải (~19% = 36/188) là Sinh viên năm 3, năm 4 các ngành Sư phạm & Giáo dục
        // ══════════════════════════════════════════════════════════════════════
        var autoDetectGoc = db.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            ThemBoDuLieuGiaSuMoRong176(db, adminId, hashMacDinh, monMap, now);
            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = autoDetectGoc;
        }

        await DongBoThongBaoKhieuNaiMauAsync(db, cancellationToken);
    }

    private static async Task DongBoAnToanSauKhoiTaoAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var dsChiOnlineCoBanKinh = await db.GiaSus
            .Where(g => !g.DayTrucTiep && g.BanKinhPhucVuKm != 0)
            .ToListAsync(cancellationToken);
        if (dsChiOnlineCoBanKinh.Count > 0)
        {
            foreach (var gs in dsChiOnlineCoBanKinh)
            {
                gs.BanKinhPhucVuKm = 0;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        var dsHocThuKhongOnline = await db.GiaSus
            .Where(g => !g.DayTrucTuyen && g.CoHocThu)
            .ToListAsync(cancellationToken);
        if (dsHocThuKhongOnline.Count > 0)
        {
            foreach (var gs in dsHocThuKhongOnline)
            {
                gs.CoHocThu = false;
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        // Chuyển các link Jitsi cũ (nếu có) sang phòng học trực tuyến nội bộ
        var donJitsiCu = await db.DonDatLichs
            .Where(d => d.PhongHocUrl != null && d.PhongHocUrl.StartsWith("https://meet.jit.si"))
            .ToListAsync(cancellationToken);
        if (donJitsiCu.Count > 0)
        {
            foreach (var d in donJitsiCu)
            {
                d.PhongHocUrl = $"/pages/phong-hoc.html?donId={d.Id}";
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        await DongBoThongBaoKhieuNaiMauAsync(db, cancellationToken);
        await DongBoViTaiKhoanIdempotentAsync(db, cancellationToken);
    }

    private static async Task DongBoThongBaoKhieuNaiMauAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var dsKnMo = await db.KhieuNais
            .AsNoTracking()
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.HocSinh)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.GiaSuMonHoc)!.ThenInclude(m => m!.MonHoc)
            .Include(k => k.DonDatLich)!.ThenInclude(d => d!.DsThanhToan)
            .Where(k => k.TrangThai == TrangThaiKhieuNai.MO)
            .ToListAsync(cancellationToken);

        if (dsKnMo.Count == 0)
        {
            return;
        }

        var coThem = false;
        foreach (var kn in dsKnMo)
        {
            var don = kn.DonDatLich;
            var giaSuId = don?.GiaSuMonHoc?.GiaSuId ?? Guid.Empty;
            if (don is null || giaSuId == Guid.Empty)
            {
                continue;
            }

            var tieuDe = $"Học viên mở khiếu nại đơn {don.MaDon}";
            var daCo = await db.ThongBaos.AnyAsync(
                t => t.NguoiNhanId == giaSuId && t.Loai == LoaiThongBao.KHIEU_NAI && t.TieuDe == tieuDe,
                cancellationToken);

            if (daCo)
            {
                continue;
            }

            var soTien = don.DsThanhToan.FirstOrDefault()?.SoTien ?? don.TongTien;
            db.ThongBaos.Add(new ThongBao
            {
                NguoiNhanId = giaSuId,
                Loai = LoaiThongBao.KHIEU_NAI,
                TieuDe = tieuDe,
                NoiDung = $"{don.HocSinh?.HoTen ?? "Học viên"} đã mở khiếu nại cho buổi học {don.GiaSuMonHoc?.MonHoc?.Ten ?? "môn học"} (học phí đã thanh toán: {soTien:N0} ₫). Vui lòng xem chi tiết và gửi giải trình/bằng chứng phản biện trước khi quản trị viên phân xử.",
                TaoLuc = kn.TaoLuc
            });
            coThem = true;
        }

        if (coThem)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static readonly (int SoThuTu, string Ten)[] DanhMucMonHocChuan =
    [
        // 6 môn cốt lõi
        (1, "Toán"),
        (2, "Vật lý"),
        (3, "Hóa học"),
        (4, "Tiếng Anh"),
        (5, "Ngữ văn"),
        (6, "Tin học"),

        // 1. Toán & Khoa học Tự nhiên
        (7, "Toán tư duy"),
        (8, "Toán cao cấp"),
        (9, "Toán rời rạc"),
        (10, "Đại số tuyến tính"),
        (11, "Giải tích"),
        (12, "Xác suất thống kê"),
        (13, "Sinh học"),
        (14, "Khoa học tự nhiên"),
        (15, "Thiên văn học"),
        (16, "Ôn thi Toán THPT Quốc gia"),
        (17, "Ôn thi Đánh giá năng lực"),
        (18, "Luyện thi vào lớp 10"),
        (19, "Bồi dưỡng học sinh giỏi"),
        (20, "Toán tiếng Anh"),
        (21, "Khoa học máy tính"),

        // 2. Ngữ văn, Xã hội & Tiểu học
        (22, "Lịch sử"),
        (23, "Địa lý"),
        (24, "Giáo dục công dân"),
        (25, "Giáo dục kinh tế và pháp luật"),
        (26, "Khoa học xã hội"),
        (27, "Triết học"),
        (28, "Tâm lý học"),
        (29, "Xã hội học"),
        (30, "Tiếng Việt tiểu học"),
        (31, "Luyện chữ đẹp"),
        (32, "Tập đọc & Đọc hiểu"),
        (33, "Báo bài tiểu học"),
        (34, "Tiền tiểu học (Vào lớp 1)"),
        (35, "Kỹ năng làm văn nghị luận"),

        // 3. Ngoại ngữ & Luyện thi Chứng chỉ Quốc tế
        (36, "Tiếng Anh giao tiếp"),
        (37, "Tiếng Anh thiếu nhi"),
        (38, "Tiếng Anh thương mại"),
        (39, "Tiếng Anh chuyên ngành"),
        (40, "Ngữ pháp Tiếng Anh"),
        (41, "Phát âm Tiếng Anh"),
        (42, "Luyện thi IELTS"),
        (43, "Luyện thi TOEIC"),
        (44, "Luyện thi TOEFL"),
        (45, "Luyện thi SAT"),
        (46, "Luyện thi GMAT / GRE"),
        (47, "Luyện thi Cambridge (Starters / Movers / Flyers)"),
        (48, "Luyện thi KET / PET / FCE"),
        (49, "Luyện thi B1 / B2 VSTEP"),
        (50, "Tiếng Trung"),
        (51, "Luyện thi HSK"),
        (52, "Tiếng Nhật"),
        (53, "Luyện thi JLPT"),
        (54, "Tiếng Hàn"),
        (55, "Luyện thi TOPIK"),
        (56, "Tiếng Pháp"),
        (57, "Luyện thi DELF / DALF"),
        (58, "Tiếng Đức"),
        (59, "Tiếng Tây Ban Nha"),
        (60, "Tiếng Nga"),
        (61, "Tiếng Ý"),
        (62, "Tiếng Thái"),
        (63, "Tiếng Ả Rập"),
        (64, "Tiếng Việt cho người nước ngoài"),

        // 4. Công nghệ Thông tin, Lập trình & Dữ liệu
        (65, "Tin học văn phòng (Word / PowerPoint)"),
        (66, "Excel nâng cao & VBA"),
        (67, "Luyện thi MOS / IC3"),
        (68, "Lập trình Python"),
        (69, "Lập trình C / C++"),
        (70, "Lập trình C#"),
        (71, "Lập trình Java"),
        (72, "Lập trình JavaScript / TypeScript"),
        (73, "Lập trình Web (HTML / CSS / JS)"),
        (74, "Lập trình Frontend (React / Vue)"),
        (75, "Lập trình Backend"),
        (76, "Lập trình PHP / Laravel"),
        (77, "Lập trình di động (Flutter / Android / iOS)"),
        (78, "Cơ sở dữ liệu & SQL"),
        (79, "Cấu trúc dữ liệu & Giải thuật"),
        (80, "Trí tuệ nhân tạo & Machine Learning"),
        (81, "Khoa học dữ liệu (Data Science)"),
        (82, "Phân tích dữ liệu (Power BI / Tableau)"),
        (83, "An toàn thông tin & Mạng máy tính"),
        (84, "Điện toán đám mây & DevOps"),
        (85, "Lập trình Scratch cho trẻ em"),
        (86, "Robotics & Arduino"),
        (87, "Lập trình Game (Unity / Godot)"),
        (88, "Kiểm thử phần mềm (QA / Tester)"),
        (89, "Hệ điều hành & Linux"),

        // 5. Kinh tế, Tài chính, Quản trị & Marketing
        (90, "Kinh tế vi mô & vĩ mô"),
        (91, "Kế toán tài chính"),
        (92, "Kế toán doanh nghiệp & Thuế"),
        (93, "Kiểm toán"),
        (94, "Tài chính doanh nghiệp"),
        (95, "Quản trị kinh doanh"),
        (96, "Digital Marketing"),
        (97, "Content Marketing & Copywriting"),
        (98, "SEO & Quảng cáo trực tuyến"),
        (99, "Thương mại điện tử"),
        (100, "Quản trị nhân sự"),
        (101, "Chứng khoán & Tài chính cá nhân"),
        (102, "Xuất nhập khẩu & Logistics"),
        (103, "Luật kinh tế"),
        (104, "Phân tích kinh doanh (Business Analyst)"),

        // 6. Thiết kế, Sáng tạo, Mỹ thuật & Kiến trúc
        (105, "Mỹ thuật & Vẽ cơ bản"),
        (106, "Vẽ luyện thi khối H / V"),
        (107, "Vẽ màu nước & Sơn dầu"),
        (108, "Vẽ Manga / Anime / Digital Art"),
        (109, "Thiết kế đồ họa (Photoshop / Illustrator)"),
        (110, "Thiết kế Canva & Ấn phẩm truyền thông"),
        (111, "Thiết kế UI / UX (Figma)"),
        (112, "Dựng phim & Biên tập video (Premiere / CapCut)"),
        (113, "Kỹ xảo & Motion Graphics (After Effects)"),
        (114, "Đồ họa 3D (Blender / 3ds Max)"),
        (115, "AutoCAD & Thiết kế kỹ thuật"),
        (116, "Revit & Kiến trúc"),
        (117, "Thiết kế nội thất"),
        (118, "Thiết kế thời trang"),
        (119, "Nhiếp ảnh"),
        (120, "Thư pháp"),

        // 7. Âm nhạc & Biểu diễn
        (121, "Piano"),
        (122, "Guitar"),
        (123, "Organ / Keyboard"),
        (124, "Violin"),
        (125, "Ukulele"),
        (126, "Cello"),
        (127, "Trống & Bộ gõ"),
        (128, "Sáo trúc & Nhạc cụ dân tộc"),
        (129, "Thanh nhạc & Luyện giọng"),
        (130, "Nhạc lý & Xướng âm"),
        (131, "Hòa âm phối khí & Làm nhạc"),
        (132, "Khiêu vũ & Nhảy hiện đại"),
        (133, "Diễn xuất & Kịch nghệ"),

        // 8. Thể thao, Sức khỏe & Cờ trí tuệ
        (134, "Cờ vua"),
        (135, "Cờ tướng"),
        (136, "Cờ vây"),
        (137, "Bơi lội"),
        (138, "Cầu lông"),
        (139, "Bóng đá"),
        (140, "Bóng rổ"),
        (141, "Bóng bàn"),
        (142, "Tennis & Pickleball"),
        (143, "Yoga & Thiền"),
        (144, "Pilates"),
        (145, "Võ thuật & Tự vệ"),
        (146, "Thể hình & Huấn luyện cá nhân (PT)"),
        (147, "Chạy bộ & Thể lực"),

        // 9. Kỹ năng mềm, Đời sống & Năng khiếu
        (148, "Kỹ năng thuyết trình & MC"),
        (149, "Kỹ năng giao tiếp & Đàm phán"),
        (150, "Quản lý thời gian & Lập kế hoạch"),
        (151, "Ghi nhớ & Sơ đồ tư duy (Mindmap)"),
        (152, "Tư duy phản biện & Tranh biện"),
        (153, "Kỹ năng viết CV & Phỏng vấn"),
        (154, "Nấu ăn & Làm bánh"),
        (155, "Pha chế (Barista / Bartender)"),
        (156, "Trang điểm cá nhân"),
        (157, "Cắm hoa nghệ thuật"),
        (158, "Ảo thuật & Biểu diễn"),
        (159, "Rubik & Trò chơi trí tuệ"),
        (160, "Y học cơ sở & Giải phẫu")
    ];

    private static async Task DongBoDanhMucMonHocDayDuAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var dsTenHienCo = await db.MonHocs
            .AsNoTracking()
            .Select(m => m.Ten)
            .ToListAsync(cancellationToken);

        var tapHienCo = new HashSet<string>(dsTenHienCo, StringComparer.OrdinalIgnoreCase);
        var dsCanThem = new List<MonHoc>();

        foreach (var (soThuTu, ten) in DanhMucMonHocChuan)
        {
            if (tapHienCo.Contains(ten))
            {
                continue;
            }

            var id = Guid.Parse($"10000000-0000-0000-0000-{soThuTu:000000000000}");
            dsCanThem.Add(new MonHoc
            {
                Id = id,
                Ten = ten,
                DangHoatDong = true
            });
            tapHienCo.Add(ten);
        }

        if (dsCanThem.Count > 0)
        {
            db.MonHocs.AddRange(dsCanThem);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task DongBoViTaiKhoanIdempotentAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var dsViSeed = await db.TaiKhoanVis
            .Include(v => v.NguoiDungThamGia)
            .Include(v => v.DsGiaoDich)
            .Where(v => v.NguoiDungThamGia != null &&
                        (v.NguoiDungThamGia.Email.EndsWith("@tutorhub.vn")
                         || v.NguoiDungThamGia.Email.EndsWith(".demo@example.tutorhub.test")))
            .ToListAsync(cancellationToken);

        if (dsViSeed.Count == 0)
        {
            return;
        }

        var dsIds = dsViSeed.Select(v => v.NguoiDungThamGiaId).ToList();
        var mapDangRut = await db.YeuCauRutTiens
            .AsNoTracking()
            .Where(r => dsIds.Contains(r.GiaSuId) && r.TrangThai == TrangThaiRutTien.CHO_XU_LY)
            .GroupBy(r => r.GiaSuId)
            .Select(g => new { GiaSuId = g.Key, Tong = g.Sum(x => x.SoTien) })
            .ToDictionaryAsync(x => x.GiaSuId, x => x.Tong, cancellationToken);

        var coThayDoi = false;
        foreach (var vi in dsViSeed)
        {
            var tongSoCai = vi.DsGiaoDich.Sum(g => g.SoTien);
            var tongDangRut = mapDangRut.TryGetValue(vi.NguoiDungThamGiaId, out var r) ? r : 0;

            if (tongSoCai < tongDangRut)
            {
                continue;
            }

            var soDuDung = tongSoCai - tongDangRut;
            if (vi.SoDu != soDuDung || vi.SoDuDangXuLyRut != tongDangRut)
            {
                vi.SoDu = soDuDung;
                vi.SoDuDangXuLyRut = tongDangRut;
                coThayDoi = true;
            }
        }

        if (coThayDoi)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Sinh tự động có kiểm soát +176 Gia sư mở rộng (tổng 188 Gia sư: 180 ĐÃ DUYỆT + 8 CHỜ DUYỆT),
    /// +10 Học sinh mở rộng và +88 Đơn hoàn thành kèm Đánh giá 4–5 sao & sổ cái ví khớp 100%.
    /// </summary>
    private static void ThemBoDuLieuGiaSuMoRong176(
        AppDbContext db,
        Guid adminId,
        string hashMacDinh,
        Dictionary<string, Guid> monMap,
        DateTime now)
    {
        Guid LayMon(string ten) =>
            monMap.TryGetValue(ten, out var id) ? id : monMap["Toán"];

        // 14 ảnh chân dung thật trên Cloudinary tách chuẩn theo giới tính (7 Nam, 7 Nữ)
        string[] anhNam =
        [
            $"{CldPublicBase}/v1795281815/tutorhub/public/tutors/gv-guitar.jpg",
            $"{CldPublicBase}/v1795279555/tutorhub/public/tutors/hoang-minh.jpg",
            $"{CldPublicBase}/v1795279562/tutorhub/public/tutors/quoc-bao.jpg",
            $"{CldPublicBase}/v1795279568/tutorhub/public/tutors/quang-huy.jpg",
            $"{CldPublicBase}/v1795279575/tutorhub/public/tutors/gia-kiet.jpg",
            $"{CldPublicBase}/v1795279584/tutorhub/public/tutors/anh-tuan.jpg",
            $"{CldPublicBase}/v1795279591/tutorhub/public/tutors/thanh-tung.jpg"
        ];

        string[] anhNu =
        [
            $"{CldPublicBase}/v1795281812/tutorhub/public/tutors/sv-su-pham.jpg",
            $"{CldPublicBase}/v1795279558/tutorhub/public/tutors/thu-ha.jpg",
            $"{CldPublicBase}/v1795279565/tutorhub/public/tutors/ngoc-mai.jpg",
            $"{CldPublicBase}/v1795279572/tutorhub/public/tutors/thao-nguyen.jpg",
            $"{CldPublicBase}/v1795279579/tutorhub/public/tutors/minh-chau.jpg",
            $"{CldPublicBase}/v1795279588/tutorhub/public/tutors/phuong-linh.jpg",
            $"{CldPublicBase}/v1795279594/tutorhub/public/tutors/kim-yen.jpg"
        ];

        // 1. Thêm 10 Học sinh mở rộng (đặc biệt nhiều học sinh tại Cần Thơ & các thành phố lớn)
        (string Email, string HoTen, string KhuVuc)[] dsHsMoRong =
        [
            ("minhtri.hs@tutorhub.vn", "Trần Minh Trí", "P. An Bình (gần ĐH Nam Cần Thơ), Q. Ninh Kiều, Cần Thơ"),
            ("thuyduong.hs@tutorhub.vn", "Nguyễn Thùy Dương", "Đường Nguyễn Văn Cừ nối dài, P. An Bình, Q. Ninh Kiều, Cần Thơ"),
            ("quocanh.hs@tutorhub.vn", "Lê Quốc Anh", "P. Xuân Khánh, Q. Ninh Kiều, Cần Thơ"),
            ("baongoc.hs@tutorhub.vn", "Phạm Bảo Ngọc", "P. Hưng Phú, Q. Cái Răng, Cần Thơ"),
            ("tuananh.hs@tutorhub.vn", "Huỳnh Tuấn Anh", "P. An Hòa, Q. Ninh Kiều, Cần Thơ"),
            ("myduyen.hs@tutorhub.vn", "Đoàn Mỹ Duyên", "P. Bình Thủy, Q. Bình Thủy, Cần Thơ"),
            ("ducmanh.hs@tutorhub.vn", "Hoàng Đức Mạnh", "Quận 7, TP. Hồ Chí Minh"),
            ("havy.hs@tutorhub.vn", "Vũ Hà Vy", "Đống Đa, Hà Nội"),
            ("thanhdat.hs@tutorhub.vn", "Phan Thành Đạt", "Sơn Trà, Đà Nẵng"),
            ("ngocmai.hs@tutorhub.vn", "Lâm Ngọc Mai", "TP. Vĩnh Long, Vĩnh Long")
        ];

        var hsMoRongIds = new Guid[dsHsMoRong.Length];
        for (int i = 0; i < dsHsMoRong.Length; i++)
        {
            var idx = i + 7;
            var hId = Guid.Parse($"31000000-0000-0000-0000-{idx:000000000000}");
            var vId = Guid.Parse($"41000000-0000-0000-0000-{idx:000000000000}");
            hsMoRongIds[i] = hId;
            var taoLuc = now.AddDays(-(35 - i));

            db.HocSinhs.Add(new HocSinh
            {
                Id = hId,
                Email = dsHsMoRong[i].Email,
                MatKhauHash = hashMacDinh,
                HoTen = dsHsMoRong[i].HoTen,
                SoDienThoai = $"0912345{idx:000}",
                KhuVuc = dsHsMoRong[i].KhuVuc,
                TrangThai = TrangThaiNguoiDung.HOAT_DONG,
                XacThucEmailLuc = taoLuc,
                TaoLuc = taoLuc,
                TaiKhoanVi = new TaiKhoanVi
                {
                    Id = vId,
                    NguoiDungThamGiaId = hId,
                    SoDu = 350_000,
                    SoDuDangXuLyRut = 0,
                    TaoLuc = taoLuc
                }
            });

            db.GiaoDichVis.Add(new GiaoDichVi
            {
                Id = Guid.Parse($"a2000000-0000-0000-0000-{idx:000000000000}"),
                TaiKhoanViId = vId,
                SoThuTu = 1,
                Loai = LoaiGiaoDichVi.NAP_TIEN,
                SoTien = 350_000,
                SoDuSau = 350_000,
                GhiChu = "Nạp tiền vào ví học tập TutorHub",
                TaoLuc = taoLuc.AddMinutes(10)
            });
        }

        // 2. Danh sách khu vực ưu tiên dày đặc tại Cần Thơ (66 chỉ mục đầu = Cần Thơ, đặc biệt quanh An Bình / ĐH Nam Cần Thơ / Ninh Kiều / Cái Răng)
        string[] khuVucCanTho =
        [
            "P. An Bình (gần ĐH Nam Cần Thơ), Q. Ninh Kiều, Cần Thơ",
            "Đường Nguyễn Văn Cừ nối dài, P. An Bình, Q. Ninh Kiều, Cần Thơ",
            "KDC Hồng Phát, P. An Bình, Q. Ninh Kiều, Cần Thơ",
            "Cầu Rau Răm, P. An Bình, Q. Ninh Kiều, Cần Thơ",
            "P. An Khánh (gần ĐH Y Dược Cần Thơ), Q. Ninh Kiều, Cần Thơ",
            "KDC 91B, P. An Khánh, Q. Ninh Kiều, Cần Thơ",
            "Đường 3/2, P. Xuân Khánh (gần ĐH Cần Thơ), Q. Ninh Kiều, Cần Thơ",
            "Đường Mậu Thân, P. Xuân Khánh, Q. Ninh Kiều, Cần Thơ",
            "Đường 30/4, P. Hưng Lợi, Q. Ninh Kiều, Cần Thơ",
            "KDC Metro Hưng Lợi, Q. Ninh Kiều, Cần Thơ",
            "P. An Phú, Q. Ninh Kiều, Cần Thơ",
            "P. Tân An (Bến Ninh Kiều), Q. Ninh Kiều, Cần Thơ",
            "Đường Cách Mạng Tháng 8, P. An Hòa, Q. Ninh Kiều, Cần Thơ",
            "P. Cái Khế, Q. Ninh Kiều, Cần Thơ",
            "P. Thới Bình, Q. Ninh Kiều, Cần Thơ",
            "KDC Hưng Phú 1, P. Hưng Phú, Q. Cái Răng, Cần Thơ",
            "KDC Nam Long, P. Hưng Thạnh, Q. Cái Răng, Cần Thơ",
            "P. Lê Bình, Q. Cái Răng, Cần Thơ",
            "P. Thường Thạnh (gần ĐH Tây Đô), Q. Cái Răng, Cần Thơ",
            "P. Ba Láng, Q. Cái Răng, Cần Thơ",
            "Đường Lê Hồng Phong, P. Bình Thủy, Q. Bình Thủy, Cần Thơ",
            "P. An Thới, Q. Bình Thủy, Cần Thơ",
            "P. Trà Nóc, Q. Bình Thủy, Cần Thơ",
            "P. Long Hòa, Q. Bình Thủy, Cần Thơ",
            "Thị trấn Phong Điền (gần An Bình), H. Phong Điền, Cần Thơ",
            "P. Châu Văn Liêm, Q. Ô Môn, Cần Thơ",
            "P. Thốt Nốt, Q. Thốt Nốt, Cần Thơ"
        ];

        string[] khuVucToanQuoc =
        [
            "Quận 1, TP. Hồ Chí Minh",
            "Quận 3, TP. Hồ Chí Minh",
            "Quận 5, TP. Hồ Chí Minh",
            "Quận 7, TP. Hồ Chí Minh",
            "Quận 10, TP. Hồ Chí Minh",
            "Bình Thạnh, TP. Hồ Chí Minh",
            "Tân Bình, TP. Hồ Chí Minh",
            "Phú Nhuận, TP. Hồ Chí Minh",
            "Gò Vấp, TP. Hồ Chí Minh",
            "TP. Thủ Đức, TP. Hồ Chí Minh",
            "Cầu Giấy, Hà Nội",
            "Đống Đa, Hà Nội",
            "Thanh Xuân, Hà Nội",
            "Ba Đình, Hà Nội",
            "Hoàn Kiếm, Hà Nội",
            "Hai Bà Trưng, Hà Nội",
            "Hà Đông, Hà Nội",
            "Long Biên, Hà Nội",
            "Hải Châu, Đà Nẵng",
            "Thanh Khê, Đà Nẵng",
            "Sơn Trà, Đà Nẵng",
            "Ngũ Hành Sơn, Đà Nẵng",
            "Liên Chiểu, Đà Nẵng",
            "TP. Vĩnh Long, Vĩnh Long",
            "TP. Vị Thanh, Hậu Giang",
            "TP. Long Xuyên, An Giang",
            "TP. Rạch Giá, Kiên Giang",
            "TP. Sóc Trăng, Sóc Trăng",
            "TP. Cao Lãnh, Đồng Tháp",
            "TP. Mỹ Tho, Tiền Giang",
            "TP. Bến Tre, Bến Tre",
            "TP. Huế, Thừa Thiên Huế",
            "TP. Nha Trang, Khánh Hòa",
            "TP. Đà Lạt, Lâm Đồng",
            "Lê Chân, Hải Phòng",
            "TP. Biên Hòa, Đồng Nai",
            "TP. Thủ Dầu Một, Bình Dương",
            "TP. Vũng Tàu, Bà Rịa - Vũng Tàu"
        ];

        string[] hoNam = ["Nguyễn", "Trần", "Lê", "Phạm", "Huỳnh", "Hoàng", "Võ", "Phan", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý", "Trương", "Đinh", "Lâm", "Mai", "Trịnh"];
        string[] demNam = ["Hoàng", "Minh", "Quốc", "Gia", "Anh", "Thanh", "Đức", "Tuấn", "Quang", "Hữu", "Khánh", "Nhật", "Trọng", "Đình", "Khôi", "Phúc", "Thiện", "Hải"];
        string[] tenNam = ["Khánh", "Hưng", "Dũng", "Khoa", "Lâm", "Phát", "Hào", "Vinh", "Kiên", "Thịnh", "Khang", "Bách", "Quân", "Sơn", "Triết", "Đạt", "Long", "Vũ", "Hiếu", "Khải", "Nguyên", "Tài"];

        string[] hoNu = ["Nguyễn", "Trần", "Lê", "Phạm", "Huỳnh", "Võ", "Phan", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý", "Trương", "Lương", "Đoàn", "Lâm", "Mai", "Châu"];
        string[] demNu = ["Thị", "Ngọc", "Thu", "Phương", "Thảo", "Minh", "Thanh", "Khánh", "Bảo", "Quỳnh", "Nhật", "Kim", "Tuyết", "Hoàng", "Mỹ", "Hồng", "Lan", "Hải"];
        string[] tenNu = ["Anh", "Linh", "Châu", "Vy", "Trâm", "Thảo", "Nhi", "Hân", "Ngân", "Trang", "Hương", "Yến", "Quyên", "Dung", "My", "Thư", "Nhung", "Uyên", "Phương", "Giang", "Trúc", "Chi"];

        string[] nganHang = ["Vietcombank", "ACB", "Techcombank", "MBBank", "BIDV", "VietinBank", "VPBank", "TPBank", "Sacombank"];

        // 44 Mẫu chuyên môn đa dạng:
        // - 9 mẫu Sinh viên năm 3, năm 4 các ngành Sư phạm/Giáo dục (khi nhân 4 lượt = đúng 36/188 ≈ 19% gia sư là sinh viên!)
        // - 35 mẫu Giáo viên, Giảng viên, Thạc sĩ, Tiến sĩ, Nghệ sĩ Thanh nhạc / Guitar / Piano / Mỹ thuật / CNTT / Ngoại ngữ / Kinh tế / Thể thao (35 * 4 = 140 + 12 gốc = 152/188 ≈ 81% là giáo viên & chuyên gia!)
        (bool LaSinhVien, string TienToDanhXung, string TieuDe, string HocVanCt, string HocVanKhac, string Mon1, int Gia1, string Mon2, int Gia2, string? Mon3, int Gia3, string MoTaChuyenMon)[] mauChuyenMon =
        [
            // --- A. NHÓM ÂM NHẠC, THANH NHẠC, GUITAR, PIANO, NHẠC CỤ (10 MẪU * 4 = 40 GIA SƯ CHUYÊN SÂU!) ---
            (false, "GV.", "Giảng viên Thanh nhạc & Luyện giọng hát Pop Ballad / Biểu diễn 1-1",
                "Cử nhân Thanh nhạc — Học viện Âm nhạc / Nhạc viện TP.HCM", "Cử nhân Thanh nhạc — Học viện Âm nhạc Quốc gia Việt Nam",
                "Thanh nhạc & Luyện giọng", 260_000, "Nhạc lý & Xướng âm", 220_000, null, 0,
                "Hướng dẫn kỹ thuật mở khẩu hình, lấy hơi bụng, xử lý nốt cao không bị căng cổ và truyền cảm xúc vào bài hát thực tế."),
            (false, "Thầy/Cô", "Giáo viên Guitar Acoustic, Fingerstyle & Đệm hát thực chiến",
                "Cử nhân Sư phạm Âm nhạc — ĐH Cần Thơ", "Cử nhân Sư phạm Âm nhạc — Nhạc viện TP.HCM",
                "Guitar", 210_000, "Ukulele", 180_000, "Nhạc lý & Xướng âm", 190_000,
                "Kèm 1-1 từ cách bấm hợp âm không đau tay, tiết tấu quạt chả, cảm âm bắt tông bài hát đến chuyển soạn Fingerstyle."),
            (false, "ThS.", "Giảng viên Piano cổ điển ABRSM & Piano đệm hát hiện đại",
                "Thạc sĩ Nghệ thuật Âm nhạc — Nhạc viện TP.HCM", "Thạc sĩ Biểu diễn Piano — Học viện Âm nhạc Quốc gia",
                "Piano", 290_000, "Organ / Keyboard", 240_000, "Nhạc lý & Xướng âm", 220_000,
                "Đào tạo Piano bài bản từ vỡ lòng đến nâng cao theo chuẩn quốc tế ABRSM, kết hợp đệm hát tự nhiên cho mọi lứa tuổi."),
            (false, "GV.", "Nghệ sĩ & Giáo viên Violin / Cello giao hưởng kèm 1-1",
                "Cử nhân Biểu diễn Nhạc cụ Phương Tây — Nhạc viện TP.HCM", "Cử nhân Biểu diễn Violin — Học viện Âm nhạc Quốc gia",
                "Violin", 300_000, "Cello", 310_000, "Nhạc lý & Xướng âm", 220_000,
                "Rèn tư thế cầm vĩ chuẩn, cảm âm cao độ chính xác và biểu diễn các tác phẩm cổ điển lẫn nhạc phim đương đại."),
            (false, "GV.", "Giáo viên Thanh nhạc thiếu nhi & Luyện thi năng khiếu Âm nhạc",
                "Cử nhân Sư phạm Âm nhạc — ĐH Cần Thơ", "Cử nhân Sư phạm Âm nhạc — ĐH Sư phạm Nghệ thuật TW",
                "Thanh nhạc & Luyện giọng", 230_000, "Organ / Keyboard", 210_000, "Khiêu vũ & Nhảy hiện đại", 220_000,
                "Phát triển thẩm mỹ âm nhạc, nhạc cảm, kỹ thuật hát đúng nhịp phách và phong thái biểu diễn tự tin trên sân khấu."),
            (false, "GV.", "Giáo viên Guitar Classic & Hòa âm phối khí phòng thu",
                "Cử nhân Guitar Cổ điển — Nhạc viện TP.HCM", "Cử nhân Sáng tác & Phối khí — Học viện Âm nhạc",
                "Guitar", 240_000, "Hòa âm phối khí & Làm nhạc", 280_000, null, 0,
                "Kết hợp kỹ thuật Guitar cổ điển với tư duy hòa âm hiện đại, hướng dẫn tự làm beat và thu âm ca khúc cá nhân."),
            (false, "GV.", "Giáo viên Sáo trúc, Nhạc cụ dân tộc & Trống Cajon/Drum",
                "Cử nhân Nhạc cụ Truyền thống — Nhạc viện TP.HCM", "Cử nhân Nhạc cụ Dân tộc — Học viện Âm nhạc Quốc gia",
                "Sáo trúc & Nhạc cụ dân tộc", 200_000, "Trống & Bộ gõ", 220_000, null, 0,
                "Hướng dẫn kỹ thuật thổi sáo trúc, rung lưỡi, phi lưỡi và giữ nhịp bộ gõ Cajon/Trống jazz sinh động, dễ tiếp thu."),

            // --- B. NHÓM MỸ THUẬT, THIẾT KẾ ĐỒ HỌA, KIẾN TRÚC & MEDIA (6 MẪU * 4 = 24 GIA SƯ!) ---
            (false, "ThS.", "Giảng viên Mỹ thuật — Luyện thi Vẽ khối H / V & Màu nước",
                "Thạc sĩ Mỹ thuật Tạo hình — ĐH Mỹ thuật TP.HCM", "Thạc sĩ Mỹ thuật — ĐH Mỹ thuật Việt Nam",
                "Vẽ luyện thi khối H / V", 260_000, "Mỹ thuật & Vẽ cơ bản", 210_000, "Vẽ màu nước & Sơn dầu", 230_000,
                "Kèm sát hình họa chì, trang trí màu, bố cục ánh sáng giúp học sinh đạt điểm 8.5+ kỳ thi năng khiếu Kiến trúc - Mỹ thuật."),
            (false, "GV.", "Chuyên gia Thiết kế Đồ họa (Photoshop / Illustrator) & UI/UX Figma",
                "Cử nhân Thiết kế Đồ họa — ĐH Kiến trúc (Cơ sở Cần Thơ)", "Cử nhân Thiết kế Mỹ thuật Số — ĐH FPT",
                "Thiết kế đồ họa (Photoshop / Illustrator)", 250_000, "Thiết kế UI / UX (Figma)", 270_000, "Thiết kế Canva & Ấn phẩm truyền thông", 200_000,
                "Đào tạo tư duy thị giác, thiết kế bộ nhận diện thương hiệu và giao diện ứng dụng thực tế qua dự án portfolio cá nhân."),
            (false, "GV.", "Họa sĩ Minh họa dạy Vẽ Manga / Anime / Digital Art trên iPad & Wacom",
                "Cử nhân Thiết kế Đồ họa — ĐH Mỹ thuật TP.HCM", "Cử nhân Hoạt hình & Minh họa — ĐH Mỹ thuật Công nghiệp",
                "Vẽ Manga / Anime / Digital Art", 230_000, "Mỹ thuật & Vẽ cơ bản", 200_000, null, 0,
                "Hướng dẫn giải phẫu nhân vật, tỉ lệ khuôn mặt, phối màu ánh sáng Digital Painting trên Procreate, Clip Studio Paint."),
            (false, "KTS.", "Kiến trúc sư dạy AutoCAD, Revit, Blender 3D & Thiết kế nội thất",
                "Kiến trúc sư — ĐH Kiến trúc TP.HCM (Cơ sở Cần Thơ)", "Kiến trúc sư Quy hoạch — ĐH Xây dựng Hà Nội",
                "AutoCAD & Thiết kế kỹ thuật", 260_000, "Revit & Kiến trúc", 280_000, "Đồ họa 3D (Blender / 3ds Max)", 270_000,
                "Cầm tay chỉ việc triển khai hồ sơ bản vẽ kỹ thuật, dựng hình 3D nội ngoại thất và render ánh sáng thực tế."),
            (false, "GV.", "Đạo diễn Hình ảnh dạy Dựng phim Premiere / CapCut & Nhiếp ảnh",
                "Cử nhân Quay phim & Truyền thông — ĐH Sân khấu Điện ảnh", "Cử nhân Truyền thông Đa phương tiện — Học viện Báo chí",
                "Dựng phim & Biên tập video (Premiere / CapCut)", 240_000, "Kỹ xảo & Motion Graphics (After Effects)", 270_000, "Nhiếp ảnh", 230_000,
                "Hướng dẫn bố cục khung hình, chỉnh màu Color Grading, cắt dựng nhịp điệu video chuyên nghiệp cho YouTube/TikTok."),

            // --- C. NHÓM CNTT, LẬP TRÌNH, AI & KHOA HỌC DỮ LIỆU (6 MẪU * 4 = 24 GIA SƯ!) ---
            (false, "ThS.", "Giảng viên CNTT dạy Lập trình Python, AI / Machine Learning & Data Science",
                "Thạc sĩ Khoa học Máy tính — ĐH Cần Thơ", "Thạc sĩ Khoa học Dữ liệu — ĐH Bách khoa",
                "Lập trình Python", 270_000, "Trí tuệ nhân tạo & Machine Learning", 310_000, "Khoa học dữ liệu (Data Science)", 290_000,
                "Hướng dẫn từ nền tảng Python, Pandas, Scikit-learn đến xây dựng mô hình AI thực tế và đồ án tốt nghiệp đại học."),
            (false, "KS.", "Kỹ sư Senior Fullstack dạy Lập trình Web (React / Vue) & C# .NET",
                "Kỹ sư Phần mềm — ĐH Cần Thơ", "Kỹ sư Công nghệ Thông tin — ĐH Công nghệ ĐHQG",
                "Lập trình Web (HTML / CSS / JS)", 240_000, "Lập trình Frontend (React / Vue)", 260_000, "Lập trình C#", 260_000,
                "Kèm 1-1 xây dựng ứng dụng Web hoàn chỉnh theo kiến trúc Clean Architecture, REST API và triển khai Cloud thực tế."),
            (false, "KS.", "Kỹ sư Phần mềm dạy Cấu trúc dữ liệu & Giải thuật, C/C++ và Java",
                "Kỹ sư Khoa học Máy tính — ĐH Cần Thơ", "Kỹ sư CNTT — ĐH Bách khoa TP.HCM",
                "Cấu trúc dữ liệu & Giải thuật", 250_000, "Lập trình C / C++", 230_000, "Lập trình Java", 240_000,
                "Luyện tư duy thuật toán thi HSG Tin học, Olympic Sinh viên và phỏng vấn kỹ thuật vào các công ty công nghệ lớn."),
            (false, "GV.", "Giáo viên STEM dạy Lập trình Scratch, Robotics Arduino & Unity/Godot",
                "Cử nhân Sư phạm Tin học — ĐH Cần Thơ", "Kỹ sư Cơ điện tử & Robotics — ĐH Sư phạm Kỹ thuật",
                "Lập trình Scratch cho trẻ em", 190_000, "Robotics & Arduino", 230_000, "Lập trình Game (Unity / Godot)", 250_000,
                "Khơi dậy đam mê công nghệ cho học sinh qua các dự án tự lập trình trò chơi 2D và lắp ráp mạch thông minh."),
            (false, "Chuyên gia", "Chuyên gia Phân tích Dữ liệu dạy SQL, Power BI & Excel VBA nâng cao",
                "Thạc sĩ Hệ thống Thông tin — ĐH Cần Thơ", "Thạc sĩ Hệ thống Thông tin Quản lý — ĐH Kinh tế Quốc dân",
                "Phân tích dữ liệu (Power BI / Tableau)", 260_000, "Excel nâng cao & VBA", 230_000, "Cơ sở dữ liệu & SQL", 240_000,
                "Tự động hóa báo cáo doanh nghiệp bằng VBA, truy vấn SQL tối ưu và thiết kế Dashboard trực quan trên Power BI."),

            // --- D. NHÓM NGOẠI NGỮ & LUYỆN THI CHỨNG CHỈ QUỐC TẾ (7 MẪU * 4 = 28 GIA SƯ!) ---
            (false, "ThS.", "Giảng viên Tiếng Anh luyện thi IELTS 8.0+ & Academic Writing",
                "Thạc sĩ Lý luận & PPGD Tiếng Anh — ĐH Cần Thơ (IELTS 8.0)", "Thạc sĩ TESOL — ĐH Ngoại ngữ ĐHQG (IELTS 8.5)",
                "Luyện thi IELTS", 310_000, "Tiếng Anh", 260_000, "Phát âm Tiếng Anh", 240_000,
                "Chấm chữa chi tiết từng bài Writing & Speaking 1-1 theo tiêu chí giám khảo, cam kết nâng 1.0–1.5 band điểm vững chắc."),
            (false, "GV.", "Giáo viên luyện thi TOEIC 4 kỹ năng, VSTEP B1-B2 & Tiếng Anh thương mại",
                "Cử nhân Ngôn ngữ Anh — ĐH Cần Thơ (TOEIC 960)", "Thạc sĩ Ngôn ngữ Anh — ĐH Hà Nội (TOEIC 975)",
                "Luyện thi TOEIC", 240_000, "Luyện thi B1 / B2 VSTEP", 250_000, "Tiếng Anh thương mại", 260_000,
                "Hệ thống mẹo giải đề TOEIC Reading/Listening tốc độ cao và luyện viết email, thuyết trình tiếng Anh công sở."),
            (false, "GV.", "Giáo viên Tiếng Anh thiếu nhi & Luyện thi Cambridge (Starters/Movers/Flyers/KET/PET)",
                "Cử nhân Sư phạm Tiếng Anh — ĐH Cần Thơ", "Cử nhân Sư phạm Tiếng Anh — ĐH Sư phạm TP.HCM",
                "Tiếng Anh thiếu nhi", 200_000, "Luyện thi Cambridge (Starters / Movers / Flyers)", 220_000, "Luyện thi KET / PET / FCE", 230_000,
                "Phương pháp học qua trò chơi tương tác và hình ảnh sinh động giúp trẻ phát âm chuẩn bản xứ và tự tin giao tiếp."),
            (false, "ThS.", "Giảng viên Tiếng Trung & Luyện thi HSK 1–6 / Giao tiếp thương mại",
                "Thạc sĩ Hán ngữ Quốc tế — ĐH Sư phạm TP.HCM (HSK 6)", "Thạc sĩ Ngôn ngữ Trung Quốc — ĐH Ngoại ngữ (HSK 6)",
                "Tiếng Trung", 240_000, "Luyện thi HSK", 260_000, null, 0,
                "Luyện nhớ chữ Hán qua chiết tự, phản xạ nghe nói thương mại và giải đề HSK/HSKK đạt điểm cao."),
            (false, "GV.", "Giáo viên Tiếng Nhật JLPT N2/N1 & Tiếng Hàn TOPIK II thực chiến",
                "Cử nhân Đông Phương học — ĐH KHXH&NV (JLPT N1)", "Cử nhân Ngôn ngữ Nhật — ĐH Hà Nội (JLPT N1)",
                "Tiếng Nhật", 250_000, "Luyện thi JLPT", 270_000, "Tiếng Hàn", 240_000,
                "Rèn phát âm tự nhiên, ngữ pháp thực tiễn và kỹ năng phỏng vấn du học, làm việc tại doanh nghiệp Nhật - Hàn."),
            (false, "ThS.", "Giảng viên Tiếng Pháp DELF B2/C1 & Tiếng Đức giao tiếp châu Âu",
                "Thạc sĩ Sư phạm Tiếng Pháp — ĐH Cần Thơ (DALF C1)", "Thạc sĩ Ngôn ngữ Pháp — ĐH Ngoại ngữ ĐHQG (DALF C1)",
                "Tiếng Pháp", 260_000, "Luyện thi DELF / DALF", 280_000, "Tiếng Đức", 270_000,
                "Đồng hành cùng học viên từ sơ cấp A1 đến lấy chứng chỉ DELF/Goethe phục vụ du học và định cư châu Âu."),

            // --- E. NHÓM GIÁO VIÊN / THẠC SĨ / TIẾN SĨ PHỔ THÔNG & TIỂU HỌC (7 MẪU * 4 = 28 GIA SƯ!) ---
            (false, "ThS.", "Thạc sĩ Toán học — Luyện thi THPT Quốc gia, Đánh giá năng lực & Toán cao cấp",
                "Thạc sĩ Giải tích — ĐH Cần Thơ", "Thạc sĩ Toán Ứng dụng — ĐH Khoa học Tự nhiên",
                "Toán", 250_000, "Ôn thi Đánh giá năng lực", 270_000, "Toán cao cấp", 280_000,
                "Chuyên trị các câu hỏi vận dụng cao, rèn tư duy giải nhanh trắc nghiệm và tự luận chặt chẽ."),
            (false, "GV.", "Giáo viên Toán THCS — Chuyên luyện thi vào lớp 10 & Toán tư duy",
                "Cử nhân Sư phạm Toán — ĐH Cần Thơ", "Cử nhân Sư phạm Toán — ĐH Sư phạm Hà Nội",
                "Luyện thi vào lớp 10", 220_000, "Toán", 210_000, "Toán tư duy", 200_000,
                "Giúp học sinh lớp 8–9 nắm vững hình học phẳng, bất đẳng thức và tự tin đỗ vào các trường THPT trọng điểm."),
            (false, "ThS.", "Thạc sĩ Vật lý & Khoa học tự nhiên — Luyện thi Đại học khối A, A1",
                "Thạc sĩ Vật lý Lý thuyết — ĐH Cần Thơ", "Thạc sĩ Vật lý — ĐH Sư phạm TP.HCM",
                "Vật lý", 240_000, "Khoa học tự nhiên", 210_000, "Thiên văn học", 220_000,
                "Giải thích bản chất hiện tượng vật lý rõ ràng, hướng dẫn bấm máy tính Casio giải nhanh bài toán điện xoay chiều, sóng cơ."),
            (false, "TS.", "Tiến sĩ Sinh học & Hóa Sinh — Bồi dưỡng HSG & Luyện thi Y Dược khối B",
                "Tiến sĩ Công nghệ Sinh học — ĐH Cần Thơ", "Tiến sĩ Sinh học — ĐH Quốc gia TP.HCM",
                "Sinh học", 270_000, "Hóa học", 260_000, "Bồi dưỡng học sinh giỏi", 290_000,
                "Chuyên sâu quy luật di truyền, biến dị và hóa hữu cơ phục vụ mục tiêu đỗ Đại học Y Dược."),
            (false, "ThS.", "Giáo viên Ngữ văn, Lịch sử & Địa lý — Ôn thi tốt nghiệp THPT & HSG",
                "Thạc sĩ Văn học Việt Nam — ĐH Cần Thơ", "Thạc sĩ Lý luận Văn học — ĐH Sư phạm Hà Nội",
                "Ngữ văn", 210_000, "Lịch sử", 190_000, "Địa lý", 190_000,
                "Truyền cảm hứng văn chương, rèn kỹ năng đọc hiểu văn bản mới và viết bài nghị luận sâu sắc, giàu chất văn."),
            (false, "GV.", "Giáo viên Tiểu học — Rèn chữ đẹp, Tiếng Việt, Toán tiểu học & Tiền tiểu học",
                "Cử nhân Giáo dục Tiểu học — ĐH Cần Thơ", "Cử nhân Giáo dục Tiểu học — ĐH Sư phạm TP.HCM",
                "Tiếng Việt tiểu học", 170_000, "Luyện chữ đẹp", 160_000, "Tiền tiểu học (Vào lớp 1)", 170_000,
                "Tận tâm, yêu trẻ, rèn nét chữ thanh đậm chuẩn mẫu và xây dựng thói quen tự giác học tập cho bé từ lớp 1–5."),

            // --- F. NHÓM KINH TẾ, THỂ THAO, CỜ VUA & KỸ NĂNG MỀM (6 MẪU * 4 = 24 GIA SƯ!) ---
            (false, "ThS.", "Giảng viên Kinh tế dạy Kế toán thực hành, Tài chính & Chứng khoán cá nhân",
                "Thạc sĩ Tài chính - Ngân hàng — ĐH Cần Thơ", "Thạc sĩ Kế toán Kiểm toán — ĐH Kinh tế TP.HCM",
                "Kế toán tài chính", 250_000, "Kế toán doanh nghiệp & Thuế", 260_000, "Chứng khoán & Tài chính cá nhân", 280_000,
                "Hướng dẫn lên báo cáo tài chính, kê khai thuế thực tế và phân tích báo cáo doanh nghiệp để đầu tư an toàn."),
            (false, "Chuyên gia", "Trưởng phòng Marketing dạy Digital Marketing, SEO & Content Copywriting",
                "Thạc sĩ Quản trị Kinh doanh — ĐH Cần Thơ", "Thạc sĩ Marketing — ĐH Kinh tế Quốc dân",
                "Digital Marketing", 260_000, "Content Marketing & Copywriting", 230_000, "SEO & Quảng cáo trực tuyến", 250_000,
                "Thực hành lập kế hoạch chiến dịch đa kênh, tối ưu SEO lên top tìm kiếm và viết nội dung chuyển đổi cao."),
            (false, "HLV.", "Kiện tướng & Huấn luyện viên Cờ vua, Cờ tướng & Rubik tư duy",
                "Cử nhân Giáo dục Thể chất — ĐH Cần Thơ (HLV Cờ vua Quốc gia)", "Huấn luyện viên Cờ vua Quốc gia — ĐH TDTT",
                "Cờ vua", 210_000, "Cờ tướng", 200_000, "Rubik & Trò chơi trí tuệ", 180_000,
                "Rèn luyện tư duy chiến thuật khai cuộc - trung cuộc - tàn cuộc, nâng cao khả năng tập trung và tính toán nhiều nước đi."),
            (false, "HLV.", "Huấn luyện viên Bơi lội, Cầu lông, Tennis/Pickleball & Yoga cá nhân",
                "Cử nhân Huấn luyện Thể thao — ĐH Cần Thơ", "Cử nhân Giáo dục Thể chất — ĐH Thể dục Thể thao",
                "Bơi lội", 230_000, "Cầu lông", 220_000, "Tennis & Pickleball", 270_000,
                "Kèm kỹ thuật chuẩn chống chấn thương, nâng cao thể lực và phản xạ thi đấu cho cả người mới bắt đầu lẫn nâng cao."),
            (false, "MC.", "Biên tập viên / MC dạy Kỹ năng Thuyết trình, Giao tiếp & Viết CV phỏng vấn",
                "Cử nhân Báo chí & Truyền thông — ĐH Cần Thơ", "Thạc sĩ Truyền thông — Học viện Báo chí & Tuyên truyền",
                "Kỹ năng thuyết trình & MC", 250_000, "Kỹ năng giao tiếp & Đàm phán", 240_000, "Kỹ năng viết CV & Phỏng vấn", 220_000,
                "Luyện giọng nói truyền cảm, ngôn ngữ hình thể tự tin trước đám đông và kỹ năng trả lời phỏng vấn chinh phục nhà tuyển dụng."),
            (false, "GV.", "Nghệ nhân Ẩm thực dạy Nấu ăn, Làm bánh Âu-Á & Pha chế Barista/Bartender",
                "Chứng chỉ Bếp trưởng & Barista Quốc tế — Cần Thơ", "Chứng chỉ Nghệ thuật Ẩm thực & Pha chế — TP.HCM",
                "Nấu ăn & Làm bánh", 240_000, "Pha chế (Barista / Bartender)", 250_000, "Cắm hoa nghệ thuật", 210_000,
                "Hướng dẫn công thức chuẩn xác để mở quán cà phê, tiệm bánh handmade hoặc chăm sóc bữa ăn gia đình dinh dưỡng."),

            // --- G. NHÓM SINH VIÊN NĂM 3, NĂM 4 CÁC NGÀNH SƯ PHẠM & GIÁO DỤC (ĐÚNG 9 MẪU * 4 = 36/188 ≈ 19% VỪA PHẢI!) ---
            (true, "SV.", "Sinh viên năm 4 Sư phạm Toán — Kèm Toán THCS/THPT & Báo bài tận tâm",
                "Sinh viên năm 4 Sư phạm Toán học — ĐH Cần Thơ (GPA 3.65/4.0)", "Sinh viên năm 4 Sư phạm Toán — ĐH Sư phạm",
                "Toán", 140_000, "Luyện thi vào lớp 10", 150_000, "Toán tư duy", 140_000,
                "Sinh viên năm cuối Sư phạm Toán nhiệt huyết, kiên nhẫn lấy lại gốc hình học và đại số cho học sinh cấp 2, cấp 3."),
            (true, "SV.", "Sinh viên năm 4 Sư phạm Tiếng Anh (IELTS 7.5) — Kèm Anh văn & Giao tiếp",
                "Sinh viên năm 4 Sư phạm Tiếng Anh — ĐH Cần Thơ (IELTS 7.5)", "Sinh viên năm 4 Sư phạm Tiếng Anh — ĐH Ngoại ngữ (IELTS 7.5)",
                "Tiếng Anh", 150_000, "Tiếng Anh giao tiếp", 160_000, "Ngữ pháp Tiếng Anh", 140_000,
                "Phương pháp trẻ trung, gần gũi giúp các em học sinh hết sợ môn Tiếng Anh, nắm chắc ngữ pháp và tự tin nói."),
            (true, "SV.", "Sinh viên năm 3 Giáo dục Tiểu học — Kèm Báo bài, Toán & Tiếng Việt tiểu học",
                "Sinh viên năm 3 Giáo dục Tiểu học — ĐH Cần Thơ", "Sinh viên năm 3 Giáo dục Tiểu học — ĐH Sư phạm",
                "Báo bài tiểu học", 120_000, "Tiếng Việt tiểu học", 130_000, "Luyện chữ đẹp", 130_000,
                "Yêu trẻ, kiên nhẫn kèm sát bài vở hằng ngày trên lớp, rèn chữ viết sạch đẹp và tính toán cẩn thận cho các bé lớp 1–5."),
            (true, "SV.", "Sinh viên năm 4 Sư phạm Vật lý & KHTN — Kèm Lý, Toán cấp 2 & cấp 3",
                "Sinh viên năm 4 Sư phạm Vật lý — ĐH Cần Thơ", "Sinh viên năm 4 Sư phạm Khoa học Tự nhiên — ĐH Sư phạm",
                "Vật lý", 140_000, "Khoa học tự nhiên", 130_000, "Toán", 140_000,
                "Giải thích công thức vật lý bằng ví dụ thực tế sinh động, hướng dẫn giải bài tập từ cơ bản đến nâng cao."),
            (true, "SV.", "Sinh viên năm 4 Sư phạm Hóa học — Kèm Hóa & Khoa học tự nhiên THCS/THPT",
                "Sinh viên năm 4 Sư phạm Hóa học — ĐH Cần Thơ", "Sinh viên năm 4 Sư phạm Hóa học — ĐH Sư phạm",
                "Hóa học", 140_000, "Khoa học tự nhiên", 130_000, "Sinh học", 140_000,
                "Giúp học sinh mất gốc Hóa nắm vững cân bằng phương trình, tính toán số mol và chuỗi phản ứng vô cơ - hữu cơ."),
            (true, "SV.", "Sinh viên năm 3 Sư phạm Ngữ văn — Kèm Văn, Tập đọc & Kỹ năng viết văn",
                "Sinh viên năm 3 Sư phạm Ngữ văn — ĐH Cần Thơ", "Sinh viên năm 3 Sư phạm Ngữ văn — ĐH Sư phạm",
                "Ngữ văn", 130_000, "Kỹ năng làm văn nghị luận", 140_000, "Tập đọc & Đọc hiểu", 120_000,
                "Hướng dẫn cách lập dàn ý nhanh, diễn đạt trôi chảy và cảm thụ tác phẩm văn học tự nhiên không cần học vẹt."),
            (true, "SV.", "Sinh viên năm 4 Sư phạm Tin học & CNTT — Dạy Tin học, Python & Scratch",
                "Sinh viên năm 4 Sư phạm Tin học — ĐH Cần Thơ / ĐH Nam Cần Thơ", "Sinh viên năm 4 Sư phạm Tin học — ĐH Sư phạm",
                "Tin học", 140_000, "Lập trình Python", 160_000, "Lập trình Scratch cho trẻ em", 140_000,
                "Hướng dẫn lập trình căn bản và tin học nhà trường dễ hiểu, khơi gợi tư duy thuật toán logic cho học sinh."),
            (true, "SV.", "Sinh viên năm 4 Sư phạm Sinh học — Kèm Sinh học & KHTN ôn thi cuối cấp",
                "Sinh viên năm 4 Sư phạm Sinh học — ĐH Cần Thơ", "Sinh viên năm 4 Sư phạm Sinh học — ĐH Sư phạm",
                "Sinh học", 140_000, "Khoa học tự nhiên", 130_000, null, 0,
                "Hệ thống kiến thức Sinh học bằng sơ đồ Mindmap trực quan giúp học sinh nhớ bài lâu và đạt điểm giỏi trên lớp."),
            (true, "SV.", "Sinh viên năm 3 Sư phạm Âm nhạc — Kèm Guitar đệm hát, Ukulele & Nhạc lý",
                "Sinh viên năm 3 Sư phạm Âm nhạc — ĐH Cần Thơ", "Sinh viên năm 3 Sư phạm Âm nhạc — Nhạc viện",
                "Guitar", 140_000, "Ukulele", 130_000, "Nhạc lý & Xướng âm", 130_000,
                "Không khí học vui vẻ, hướng dẫn đệm hát các ca khúc trẻ yêu thích chỉ sau 6–8 buổi thực hành.")
        ];

        string[] nhanXetMau =
        [
            "Gia sư giảng bài cực kỳ tâm huyết, đúng giờ và chuẩn bị tài liệu rất chu đáo. Kết quả học tập tiến bộ rõ rệt!",
            "Phương pháp truyền đạt trực quan, dễ hiểu, luôn kiên nhẫn giải đáp từng thắc mắc nhỏ trong buổi học.",
            "Lộ trình học rõ ràng, bám sát mục tiêu. Không khí buổi học thoải mái nhưng rất hiệu quả và kỷ luật.",
            "Rất hài lòng với chất lượng giảng dạy, bài tập thực hành phong phú và có sửa bài chi tiết sau mỗi buổi."
        ];

        string[] phanHoiMau =
        [
            "Cảm ơn học viên đã luôn chăm chỉ và hợp tác tốt trong suốt khóa học!",
            "Rất vui vì được đồng hành cùng em, chúc em luôn giữ vững phong độ và đam mê học tập nhé!",
            "Cảm ơn đánh giá tích cực của bạn, hẹn gặp lại trong các chuyên đề nâng cao tiếp theo!"
        ];

        int gsmSeq = 100;
        int lichSeq = 200;
        int giayToSeq = 200;
        int donSeq = 100;
        int ttSeq = 100;
        int dgSeq = 100;
        int gdSeq = 200;

        // Sinh 176 gia sư mở rộng (chỉ số i từ 0..175 => gsIdx từ 13..188)
        // - 60 gia sư đầu tiên (i < 60) đặt tại Cần Thơ (cộng với 6 gia sư Cần Thơ trong 12 gia sư gốc = 66 gia sư tại Cần Thơ!)
        // - 6 gia sư cuối cùng (i >= 170) có trạng thái CHO_DUYET (cộng 2 gốc = 8 gia sư CHO_DUYET; còn lại 180 gia sư DA_DUYET!)
        for (int i = 0; i < 176; i++)
        {
            int gsIdx = i + 13;
            bool laNu = (i % 2 == 1);
            var mau = mauChuyenMon[i % mauChuyenMon.Length];
            bool laCanTho = i < 60;
            bool daDuyet = i < 170;

            string ho = laNu ? hoNu[(i * 3) % hoNu.Length] : hoNam[(i * 3) % hoNam.Length];
            string dem = laNu ? demNu[(i * 5 + 1) % demNu.Length] : demNam[(i * 5 + 1) % demNam.Length];
            string ten = laNu ? tenNu[(i * 7 + 2) % tenNu.Length] : tenNam[(i * 7 + 2) % tenNam.Length];
            string danhXung = mau.TienToDanhXung == "Thầy/Cô"
                ? (laNu ? "Cô" : "Thầy")
                : mau.TienToDanhXung;
            string hoTen = $"{danhXung} {ho} {dem} {ten}";

            string khuVuc = laCanTho
                ? khuVucCanTho[i % khuVucCanTho.Length]
                : khuVucToanQuoc[(i - 60) % khuVucToanQuoc.Length];

            string anhUrl = laNu
                ? anhNu[(i / 2) % anhNu.Length]
                : anhNam[(i / 2) % anhNam.Length];

            int soNamKn = mau.LaSinhVien
                ? (1 + (i % 3)) // Sinh viên năm 3-4: 1..3 năm kinh nghiệm
                : (3 + (i % 10)); // Giáo viên / Chuyên gia: 3..12 năm kinh nghiệm

            // Hình thức dạy: 75% cả hai, 15% chỉ trực tuyến, 10% chỉ tại nhà
            bool dayTrucTuyen = (i % 10) != 9;
            bool dayTrucTiep = (i % 10) != 8;
            bool coHocThu = dayTrucTuyen && (i % 4 != 3);
            int banKinhKm = dayTrucTiep ? (8 + (i % 8)) : 0;

            var gsId = Guid.Parse($"32000000-0000-0000-0000-{gsIdx:000000000000}");
            var viId = Guid.Parse($"42000000-0000-0000-0000-{gsIdx:000000000000}");
            var taoLuc = now.AddDays(-(60 - (i % 45))).AddHours(-(i % 12));

            var dsMonDay = new List<GiaSuMonHoc>();
            var gsmChinhId = Guid.Parse($"51000000-0000-0000-0000-{gsmSeq++:000000000000}");
            var mon1Id = LayMon(mau.Mon1);
            int bienDongGia = ((i % 3) - 1) * 10_000;
            int giaChinh = Math.Max(100_000, mau.Gia1 + bienDongGia);

            dsMonDay.Add(new GiaSuMonHoc
            {
                Id = gsmChinhId,
                GiaSuId = gsId,
                MonHocId = mon1Id,
                GiaMoiGio = giaChinh
            });

            var mon2Id = LayMon(mau.Mon2);
            if (mon2Id != mon1Id)
            {
                dsMonDay.Add(new GiaSuMonHoc
                {
                    Id = Guid.Parse($"51000000-0000-0000-0000-{gsmSeq++:000000000000}"),
                    GiaSuId = gsId,
                    MonHocId = mon2Id,
                    GiaMoiGio = Math.Max(100_000, mau.Gia2 + bienDongGia)
                });
            }

            if (!string.IsNullOrWhiteSpace(mau.Mon3))
            {
                var mon3Id = LayMon(mau.Mon3!);
                if (mon3Id != mon1Id && mon3Id != mon2Id)
                {
                    dsMonDay.Add(new GiaSuMonHoc
                    {
                        Id = Guid.Parse($"51000000-0000-0000-0000-{gsmSeq++:000000000000}"),
                        GiaSuId = gsId,
                        MonHocId = mon3Id,
                        GiaMoiGio = Math.Max(100_000, mau.Gia3 + bienDongGia)
                    });
                }
            }

            var hinhThucLich = (dayTrucTuyen && dayTrucTiep)
                ? HinhThucLichTuan.CA_HAI
                : (dayTrucTuyen ? HinhThucLichTuan.TRUC_TUYEN : HinhThucLichTuan.TRUC_TIEP);

            int thuA = 2 + (i % 3); // Thứ 2, 3, 4
            int thuB = 5 + (i % 2); // Thứ 5, 6
            var dsLich = new List<LichRanh>
            {
                new LichLapHangTuan
                {
                    Id = Guid.Parse($"61000000-0000-0000-0000-{lichSeq++:000000000000}"),
                    GiaSuId = gsId,
                    ThuTrongTuan = thuA,
                    GioBatDau = new TimeOnly(18, 0),
                    GioKetThuc = new TimeOnly(21, 0),
                    HinhThucLich = hinhThucLich
                },
                new LichLapHangTuan
                {
                    Id = Guid.Parse($"61000000-0000-0000-0000-{lichSeq++:000000000000}"),
                    GiaSuId = gsId,
                    ThuTrongTuan = thuB,
                    GioBatDau = new TimeOnly(18, 0),
                    GioKetThuc = new TimeOnly(21, 0),
                    HinhThucLich = hinhThucLich
                },
                new LichLapHangTuan
                {
                    Id = Guid.Parse($"61000000-0000-0000-0000-{lichSeq++:000000000000}"),
                    GiaSuId = gsId,
                    ThuTrongTuan = 7,
                    GioBatDau = new TimeOnly(8, 30),
                    GioKetThuc = new TimeOnly(11, 30),
                    HinhThucLich = hinhThucLich
                }
            };

            var loaiGt = mau.LaSinhVien ? LoaiGiayTo.CHUNG_CHI : LoaiGiayTo.BANG_CAP;
            var cldDocKey = mau.LaSinhVien ? DocChungChi : (mau.TienToDanhXung is "ThS." or "TS." ? DocBangThacSi : DocBangDaiHoc);

            var dsGiayTo = new List<GiayToGiaSu>
            {
                new GiayToGiaSu
                {
                    Id = Guid.Parse($"71000000-0000-0000-0000-{giayToSeq++:000000000000}"),
                    GiaSuId = gsId,
                    Loai = loaiGt,
                    DuongDanTep = cldDocKey,
                    TenTepGoc = mau.LaSinhVien ? $"xac-nhan-sinh-vien-su-pham-{gsIdx}.png" : $"bang-chuyen-mon-gia-su-{gsIdx}.png",
                    TrangThai = daDuyet ? TrangThaiGiayTo.DA_DUYET : TrangThaiGiayTo.CHO_DUYET,
                    DuyetLuc = daDuyet ? taoLuc.AddHours(6) : null,
                    NguoiDuyetId = daDuyet ? adminId : null
                }
            };

            // Tạo đơn hoàn thành + đánh giá thực tế cho 88 gia sư đã duyệt (cách đều) để điểm đánh giá & số buổi hoàn thành phân hóa tự nhiên
            int thuNhapVi = 0;
            if (daDuyet && (i % 2 == 0))
            {
                var dId = Guid.Parse($"81000000-0000-0000-0000-{donSeq++:000000000000}");
                var tId = Guid.Parse($"91000000-0000-0000-0000-{ttSeq++:000000000000}");
                var hsDatId = hsMoRongIds[(i / 2) % hsMoRongIds.Length];
                var bdDon = now.AddDays(-(15 + (i % 25))).Date.AddHours(13);
                var ktDon = bdDon.AddHours(2);
                int tongTien = giaChinh * 2;
                thuNhapVi = (int)Math.Round(tongTien * 0.88m);
                var hinhThucDon = dayTrucTuyen ? HinhThucBuoiHoc.TRUC_TUYEN : HinhThucBuoiHoc.TRUC_TIEP;

                db.DonDatLichs.Add(new DonDatLich
                {
                    Id = dId,
                    MaDon = $"BK-202610-{gsIdx:0000}",
                    HocSinhId = hsDatId,
                    GiaSuMonHocId = gsmChinhId,
                    HinhThuc = hinhThucDon,
                    DiaChiHocSinh = hinhThucDon == HinhThucBuoiHoc.TRUC_TIEP ? khuVuc : null,
                    BatDauLuc = bdDon,
                    KetThucLuc = ktDon,
                    GiaMoiGio = giaChinh,
                    TienGiam = 0,
                    TrangThai = TrangThaiDonDatLich.HOAN_THANH,
                    LaBuoiHocThu = false,
                    PhongHocUrl = hinhThucDon == HinhThucBuoiHoc.TRUC_TUYEN ? $"/pages/phong-hoc.html?donId={dId}" : null,
                    HanPhanHoi = bdDon.AddHours(-12),
                    TaoLuc = bdDon.AddDays(-2)
                });

                db.ThanhToans.Add(new ThanhToan
                {
                    Id = tId,
                    DonDatLichId = dId,
                    SoTien = tongTien,
                    PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
                    TrangThai = TrangThaiThanhToan.DA_GIAI_NGAN,
                    MaGiaoDichCong = null,
                    ThanhToanLuc = bdDon.AddDays(-2).AddMinutes(5),
                    GiaiNganLuc = ktDon.AddHours(2),
                    TienDaHoan = 0,
                    TaoLuc = bdDon.AddDays(-2).AddMinutes(1)
                });

                db.GiaoDichVis.Add(new GiaoDichVi
                {
                    Id = Guid.Parse($"a1000000-0000-0000-0000-{gdSeq++:000000000000}"),
                    TaiKhoanViId = viId,
                    SoThuTu = 1,
                    Loai = LoaiGiaoDichVi.THU_NHAP,
                    SoTien = thuNhapVi,
                    SoDuSau = thuNhapVi,
                    GhiChu = $"Giải ngân thu nhập đơn BK-202610-{gsIdx:0000} (đã trừ 12% phí nền tảng)",
                    ThanhToanId = tId,
                    TaoLuc = ktDon.AddHours(2)
                });

                int diemDg = (i % 6 == 4) ? 4 : 5;
                db.DanhGias.Add(new DanhGia
                {
                    Id = Guid.Parse($"b1000000-0000-0000-0000-{dgSeq++:000000000000}"),
                    DonDatLichId = dId,
                    Diem = diemDg,
                    BinhLuan = nhanXetMau[(i / 2) % nhanXetMau.Length],
                    PhanHoiGiaSu = (i % 4 == 0) ? phanHoiMau[(i / 4) % phanHoiMau.Length] : null,
                    TaoLuc = ktDon.AddHours(4)
                });
            }

            var hocVan = laCanTho ? mau.HocVanCt : mau.HocVanKhac;
            var tenKhongDau = MonHoc.TaoSlug($"{hoTen}").Replace("-", "").ToUpperInvariant();

            db.GiaSus.Add(new GiaSu
            {
                Id = gsId,
                Email = $"giasu{gsIdx:000}@tutorhub.vn",
                MatKhauHash = hashMacDinh,
                HoTen = hoTen,
                SoDienThoai = $"0908222{gsIdx:000}",
                AnhDaiDienUrl = anhUrl,
                KhuVuc = khuVuc,
                TrangThai = TrangThaiNguoiDung.HOAT_DONG,
                XacThucEmailLuc = taoLuc,
                TaoLuc = taoLuc,
                TieuDe = mau.TieuDe,
                GioiThieu = $"{mau.MoTaChuyenMon} Nhận dạy kèm 1-1 tại {khuVuc} và trực tuyến qua phòng học TutorHub.",
                HocVan = hocVan,
                SoNamKinhNghiem = soNamKn,
                DayTrucTuyen = dayTrucTuyen,
                DayTrucTiep = dayTrucTiep,
                TrangThaiHoSo = daDuyet ? TrangThaiHoSoGiaSu.DA_DUYET : TrangThaiHoSoGiaSu.CHO_DUYET,
                CoHocThu = coHocThu,
                BanKinhPhucVuKm = banKinhKm,
                NguoiDuyetId = daDuyet ? adminId : null,
                TaiKhoanVi = new TaiKhoanVi
                {
                    Id = viId,
                    NguoiDungThamGiaId = gsId,
                    SoDu = thuNhapVi,
                    SoDuDangXuLyRut = 0,
                    TaoLuc = taoLuc
                },
                TaiKhoanNganHang = new TaiKhoanNganHang
                {
                    GiaSuId = gsId,
                    TenNganHang = nganHang[i % nganHang.Length],
                    SoTaiKhoan = $"10203040{gsIdx:00000}",
                    ChuTaiKhoan = tenKhongDau.Length > 40 ? tenKhongDau[..40] : tenKhongDau
                },
                DsMonDay = dsMonDay,
                DsLichRanh = dsLich,
                DsGiayTo = dsGiayTo
            });
        }
    }
}
