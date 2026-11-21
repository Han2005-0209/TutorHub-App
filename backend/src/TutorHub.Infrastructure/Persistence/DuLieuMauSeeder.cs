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
                PhuongThuc = PhuongThucThanhToan.VNPAY,
                TrangThai = TrangThaiThanhToan.DA_GIAI_NGAN,
                MaGiaoDichCong = $"VNP-{idx:000000}",
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
            PhuongThuc = PhuongThucThanhToan.GIA_LAP,
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
            PhuongThuc = PhuongThucThanhToan.GIA_LAP,
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
            PhuongThuc = PhuongThucThanhToan.VNPAY,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            MaGiaoDichCong = "VNP-000015",
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
            PhuongThuc = PhuongThucThanhToan.VNPAY,
            TrangThai = TrangThaiThanhToan.DANG_GIU,
            MaGiaoDichCong = "VNP-000016",
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
            PhuongThuc = PhuongThucThanhToan.GIA_LAP,
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
            PhuongThuc = PhuongThucThanhToan.GIA_LAP,
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
            PhuongThuc = PhuongThucThanhToan.GIA_LAP,
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
                TieuDe = "Có 2 hồ sơ gia sư mới & 2 yêu cầu rút tiền đang chờ duyệt",
                NoiDung = "KS. Võ Thanh Tùng và Cô Trương Kim Yến đã hoàn tất nộp giấy tờ xác minh trên Cloudinary Private, sẵn sàng kiểm duyệt.",
                TaoLuc = now.AddHours(-1)
            });

        await db.SaveChangesAsync(cancellationToken);

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
}
