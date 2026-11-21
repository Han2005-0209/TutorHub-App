using Microsoft.EntityFrameworkCore;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Infrastructure.Services;

namespace TutorHub.Infrastructure.Persistence;

/// <summary>
/// Khởi tạo dữ liệu mẫu ẩn danh hóa cho môi trường Development (theo `docs/migration/08-DATA-SEED.md`).
/// Mật khẩu mặc định được băm động tại runtime từ biến môi trường `SEED_DEFAULT_PASSWORD` (mặc định dev: `Password123!`).
/// </summary>
public static class DuLieuMauSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.MonHocs.AnyAsync(cancellationToken))
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

            var dsDonTrucTiepThieuDiaChi = await db.DonDatLichs
                .Where(d => d.HinhThuc == HinhThucBuoiHoc.TRUC_TIEP && d.DiaChiHocSinh == null)
                .ToListAsync(cancellationToken);

            if (dsDonTrucTiepThieuDiaChi.Count > 0)
            {
                foreach (var d in dsDonTrucTiepThieuDiaChi)
                {
                    d.DiaChiHocSinh = "Số 128 đường 30/4, P. An Phú, Q. Ninh Kiều, Cần Thơ";
                }

                await db.SaveChangesAsync(cancellationToken);
            }

            // Bảo đảm gia sư không dạy trực tuyến không thể bật CoHocThu (CK_GiaSu_CoHocThu_TrucTuyen)
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

            // Bổ sung idempotent danh mục đầy đủ 160 môn học (không ảnh hưởng môn đã tồn tại)
            await DongBoDanhMucMonHocDayDuAsync(db, cancellationToken);

            // Bảo đảm gia sư có thông báo thật cho khiếu nại mẫu đang mở (nếu chưa có)
            await DongBoThongBaoKhieuNaiMauAsync(db, cancellationToken);

            // Bảo đảm học sinh mẫu (hocsinh1) có 1 buổi học vừa kết thúc trong cửa sổ 24h (DA_XAC_NHAN + DANG_GIU)
            // để kiểm thử ngay nút "Khiếu nại" và "Xác nhận hoàn thành"
            await DongBoDonDemoTrongCuaSoKhieuNaiAsync(db, cancellationToken);

            // Đồng bộ idempotent số dư ví cho các tài khoản seed demo (*.demo@example.tutorhub.test)
            // Đảm bảo bất biến sổ cái: SoDu + SoDuDangXuLyRut == SUM(GiaoDichVi.SoTien) và SoDuDangXuLyRut == SUM(YeuCauRutTien CHO_XU_LY)
            await DongBoViTaiKhoanDemoIdempotentAsync(db, cancellationToken);

            return;
        }

        var matKhauDemo = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD") ?? "Password123!";
        var hashMacDinh = XacThucService.BamMatKhau(matKhauDemo);

        // 1. Danh mục 6 Môn học chuẩn
        var monToanId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var monVatLyId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var monHoaHocId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var monTiengAnhId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var monNguVanId = Guid.Parse("10000000-0000-0000-0000-000000000005");
        var monTinHocId = Guid.Parse("10000000-0000-0000-0000-000000000006");

        db.MonHocs.AddRange(
            new MonHoc { Id = monToanId, Ten = "Toán", DangHoatDong = true },
            new MonHoc { Id = monVatLyId, Ten = "Vật lý", DangHoatDong = true },
            new MonHoc { Id = monHoaHocId, Ten = "Hóa học", DangHoatDong = true },
            new MonHoc { Id = monTiengAnhId, Ten = "Tiếng Anh", DangHoatDong = true },
            new MonHoc { Id = monNguVanId, Ten = "Ngữ văn", DangHoatDong = true },
            new MonHoc { Id = monTinHocId, Ten = "Tin học", DangHoatDong = true });

        // 2. Mã giảm giá
        var maWelcomeId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var maGiam50kId = Guid.Parse("20000000-0000-0000-0000-000000000002");

        db.MaGiamGias.AddRange(
            new MaGiamGia
            {
                Id = maWelcomeId,
                Ma = "WELCOME10",
                Loai = LoaiMaGiamGia.PHAN_TRAM,
                GiaTri = 10,
                GiaTriDonToiThieu = 100_000,
                SoLuotToiDa = 100,
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
                SoLuotToiDa = 50,
                BatDauLuc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                HetHanLuc = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc),
                DangHoatDong = true
            });

        // 3. Quản trị viên
        var adminId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        db.QuanTriViens.Add(new QuanTriVien
        {
            Id = adminId,
            Email = "admin.demo@example.tutorhub.test",
            MatKhauHash = hashMacDinh,
            HoTen = "Quản Trị Viên Mẫu",
            SoDienThoai = "0900000001",
            KhuVuc = "Ninh Kiều, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc)
        });

        // 4. Học sinh (2 tài khoản)
        var hocSinh1Id = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var hocSinh2Id = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var viHs1Id = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var viHs2Id = Guid.Parse("40000000-0000-0000-0000-000000000002");

        db.HocSinhs.AddRange(
            new HocSinh
            {
                Id = hocSinh1Id,
                Email = "hocsinh1.demo@example.tutorhub.test",
                MatKhauHash = hashMacDinh,
                HoTen = "Lê Minh Khoa",
                SoDienThoai = "0900000002",
                KhuVuc = "Ninh Kiều, Cần Thơ",
                TrangThai = TrangThaiNguoiDung.HOAT_DONG,
                XacThucEmailLuc = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
                TaoLuc = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc),
                TaiKhoanVi = new TaiKhoanVi
                {
                    Id = viHs1Id,
                    NguoiDungThamGiaId = hocSinh1Id,
                    SoDu = 500_000,
                    TaoLuc = new DateTime(2026, 2, 1, 8, 0, 0, DateTimeKind.Utc)
                }
            },
            new HocSinh
            {
                Id = hocSinh2Id,
                Email = "hocsinh2.demo@example.tutorhub.test",
                MatKhauHash = hashMacDinh,
                HoTen = "Phạm Thu Trang",
                SoDienThoai = "0900000003",
                KhuVuc = "Quận 1, TP. Hồ Chí Minh",
                TrangThai = TrangThaiNguoiDung.HOAT_DONG,
                XacThucEmailLuc = new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc),
                TaoLuc = new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc),
                TaiKhoanVi = new TaiKhoanVi
                {
                    Id = viHs2Id,
                    NguoiDungThamGiaId = hocSinh2Id,
                    SoDu = 0,
                    TaoLuc = new DateTime(2026, 2, 5, 8, 0, 0, DateTimeKind.Utc)
                }
            });

        // 5. Gia sư (4 Gia sư ĐÃ DUYỆT + 1 Gia sư CHỜ DUYỆT)
        var giaSu1Id = Guid.Parse("30000000-0000-0000-0000-000000000004");
        var giaSu2Id = Guid.Parse("30000000-0000-0000-0000-000000000005");
        var giaSu3Id = Guid.Parse("30000000-0000-0000-0000-000000000006");
        var giaSu4Id = Guid.Parse("30000000-0000-0000-0000-000000000007");
        var giaSu5Id = Guid.Parse("30000000-0000-0000-0000-000000000008");

        var viGs1Id = Guid.Parse("40000000-0000-0000-0000-000000000003");
        var viGs2Id = Guid.Parse("40000000-0000-0000-0000-000000000004");
        var viGs3Id = Guid.Parse("40000000-0000-0000-0000-000000000005");
        var viGs4Id = Guid.Parse("40000000-0000-0000-0000-000000000006");
        var viGs5Id = Guid.Parse("40000000-0000-0000-0000-000000000007");

        var gsm1Id = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var gsm2Id = Guid.Parse("50000000-0000-0000-0000-000000000002");
        var gsm3Id = Guid.Parse("50000000-0000-0000-0000-000000000003");
        var gsm4Id = Guid.Parse("50000000-0000-0000-0000-000000000004");
        var gsm5Id = Guid.Parse("50000000-0000-0000-0000-000000000005");
        var gsm6Id = Guid.Parse("50000000-0000-0000-0000-000000000006");
        var gsm7Id = Guid.Parse("50000000-0000-0000-0000-000000000007");

        var giaSu1 = new GiaSu
        {
            Id = giaSu1Id,
            Email = "giasu1.demo@example.tutorhub.test",
            MatKhauHash = hashMacDinh,
            HoTen = "ThS. Nguyễn Hoàng Minh",
            SoDienThoai = "0900000004",
            AnhDaiDienUrl = "/assets/img/tutors/tutor-hoang-minh.jpg",
            KhuVuc = "Ninh Kiều, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Thạc sĩ Toán học chuyên luyện thi THPT Quốc gia & Đại học",
            GioiThieu = "Gia sư có 8 năm kinh nghiệm giảng dạy Toán và Vật lý cấp THPT, phương pháp trực quan giúp học sinh nắm chắc bản chất, luyện đề mục tiêu 9+.",
            HocVan = "Thạc sĩ Phương pháp giảng dạy Toán — ĐH Cần Thơ",
            SoNamKinhNghiem = 8,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 10,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs1Id, NguoiDungThamGiaId = giaSu1Id, SoDu = 240_000, SoDuDangXuLyRut = 200_000, TaoLuc = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = giaSu1Id, TenNganHang = "Vietcombank", SoTaiKhoan = "0000123456789", ChuTaiKhoan = "NGUYEN HOANG MINH" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsm1Id, GiaSuId = giaSu1Id, MonHocId = monToanId, GiaMoiGio = 250_000 },
                new GiaSuMonHoc { Id = gsm2Id, GiaSuId = giaSu1Id, MonHocId = monVatLyId, GiaMoiGio = 240_000 }
            ],
            DsLichRanh =
            [
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000001"), GiaSuId = giaSu1Id, ThuTrongTuan = 2, GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(21, 0) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000002"), GiaSuId = giaSu1Id, ThuTrongTuan = 4, GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(21, 0) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000003"), GiaSuId = giaSu1Id, ThuTrongTuan = 6, GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(21, 0) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000007"), GiaSuId = giaSu1Id, ThuTrongTuan = 7, GioBatDau = new TimeOnly(8, 0), GioKetThuc = new TimeOnly(12, 0) },
                new LichNgayCuThe { Id = Guid.Parse("60000000-0000-0000-0000-000000000004"), GiaSuId = giaSu1Id, NgayCuThe = new DateOnly(2026, 12, 25), Loai = LoaiLichNgayCuThe.NGHI_PHEP }
            ],
            DsGiayTo =
            [
                new GiayToGiaSu
                {
                    Id = Guid.Parse("70000000-0000-0000-0000-000000000001"),
                    GiaSuId = giaSu1Id,
                    Loai = LoaiGiayTo.BANG_CAP,
                    DuongDanTep = "giay-to-gia-su/bang-thac-si-toan-demo.pdf",
                    TenTepGoc = "bang-thac-si-toan.pdf",
                    TrangThai = TrangThaiGiayTo.DA_DUYET,
                    DuyetLuc = new DateTime(2026, 1, 16, 9, 0, 0, DateTimeKind.Utc),
                    NguoiDuyetId = adminId
                }
            ]
        };

        var giaSu2 = new GiaSu
        {
            Id = giaSu2Id,
            Email = "giasu2.demo@example.tutorhub.test",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Trần Thị Thu Hà",
            SoDienThoai = "0900000005",
            AnhDaiDienUrl = "/assets/img/tutors/tutor-thu-ha.jpg",
            KhuVuc = "Ninh Kiều, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giáo viên Tiếng Anh luyện giao tiếp & IELTS 8.0",
            GioiThieu = "Sở hữu chứng chỉ IELTS 8.0 và TESOL quốc tế, nhận kèm trực tuyến 1-1 cho học sinh mất gốc, ôn thi THPT và luyện thi chứng chỉ IELTS.",
            HocVan = "Cử nhân Sư phạm Tiếng Anh (IELTS 8.0, TESOL)",
            SoNamKinhNghiem = 6,
            DayTrucTuyen = true,
            DayTrucTiep = false,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true, // Miễn phí học thử Online
            BanKinhPhucVuKm = 0,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs2Id, NguoiDungThamGiaId = giaSu2Id, SoDu = 0, TaoLuc = new DateTime(2026, 1, 18, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = giaSu2Id, TenNganHang = "ACB", SoTaiKhoan = "0000987654321", ChuTaiKhoan = "TRAN THI THU HA" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsm3Id, GiaSuId = giaSu2Id, MonHocId = monTiengAnhId, GiaMoiGio = 280_000 }
            ],
            DsLichRanh =
            [
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000005"), GiaSuId = giaSu2Id, ThuTrongTuan = 3, GioBatDau = new TimeOnly(19, 0), GioKetThuc = new TimeOnly(21, 30) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000006"), GiaSuId = giaSu2Id, ThuTrongTuan = 5, GioBatDau = new TimeOnly(19, 0), GioKetThuc = new TimeOnly(21, 30) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000008"), GiaSuId = giaSu2Id, ThuTrongTuan = 1, GioBatDau = new TimeOnly(9, 0), GioKetThuc = new TimeOnly(11, 30) }
            ],
            DsGiayTo =
            [
                new GiayToGiaSu
                {
                    Id = Guid.Parse("70000000-0000-0000-0000-000000000002"),
                    GiaSuId = giaSu2Id,
                    Loai = LoaiGiayTo.CHUNG_CHI,
                    DuongDanTep = "giay-to-gia-su/chung-chi-ielts-demo.pdf",
                    TenTepGoc = "chung-chi-ielts.pdf",
                    TrangThai = TrangThaiGiayTo.DA_DUYET,
                    DuyetLuc = new DateTime(2026, 1, 19, 10, 0, 0, DateTimeKind.Utc),
                    NguoiDuyetId = adminId
                }
            ]
        };

        var giaSu3 = new GiaSu
        {
            Id = giaSu3Id,
            Email = "giasu3.demo@example.tutorhub.test",
            MatKhauHash = hashMacDinh,
            HoTen = "KS. Võ Thanh Tùng",
            SoDienThoai = "0900000006",
            AnhDaiDienUrl = "/assets/img/tutors/tutor-thanh-tung.jpg",
            KhuVuc = "Bình Thủy, Cần Thơ",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Kỹ sư Công nghệ thông tin dạy Tin học và Lập trình cơ bản",
            GioiThieu = "Hướng dẫn tư duy thuật toán, Python/C++ và lập trình căn bản cho học sinh THCS, THPT và sinh viên năm nhất.",
            HocVan = "Kỹ sư Công nghệ thông tin",
            SoNamKinhNghiem = 4,
            DayTrucTuyen = true,
            DayTrucTiep = false,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.CHO_DUYET,
            CoHocThu = false,
            BanKinhPhucVuKm = 0,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs3Id, NguoiDungThamGiaId = giaSu3Id, SoDu = 0, TaoLuc = new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc) },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsm4Id, GiaSuId = giaSu3Id, MonHocId = monTinHocId, GiaMoiGio = 200_000 }
            ],
            DsGiayTo =
            [
                new GiayToGiaSu
                {
                    Id = Guid.Parse("70000000-0000-0000-0000-000000000003"),
                    GiaSuId = giaSu3Id,
                    Loai = LoaiGiayTo.CAN_CUOC,
                    DuongDanTep = "giay-to-gia-su/can-cuoc-demo.pdf",
                    TenTepGoc = "can-cuoc.pdf",
                    TrangThai = TrangThaiGiayTo.CHO_DUYET
                }
            ]
        };

        var giaSu4 = new GiaSu
        {
            Id = giaSu4Id,
            Email = "giasu4.demo@example.tutorhub.test",
            MatKhauHash = hashMacDinh,
            HoTen = "TS. Đặng Quốc Bảo",
            SoDienThoai = "0900000007",
            AnhDaiDienUrl = "/assets/img/tutors/tutor-quoc-bao.jpg",
            KhuVuc = "Quận 1, TP. Hồ Chí Minh",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giảng viên Hóa học — Chuyên bồi dưỡng HSG & Luyện thi Đại học khối A, B",
            GioiThieu = "10 năm kinh nghiệm luyện thi Hóa học cấp THPT và Đại học, hệ thống hóa sơ đồ tư duy phản ứng hữu cơ - vô cơ giúp giải nhanh trắc nghiệm.",
            HocVan = "Tiến sĩ Hóa học ứng dụng — ĐH Quốc gia TP.HCM",
            SoNamKinhNghiem = 10,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 15,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs4Id, NguoiDungThamGiaId = giaSu4Id, SoDu = 0, TaoLuc = new DateTime(2026, 1, 20, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = giaSu4Id, TenNganHang = "Techcombank", SoTaiKhoan = "19035566778899", ChuTaiKhoan = "DANG QUOC BAO" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsm6Id, GiaSuId = giaSu4Id, MonHocId = monHoaHocId, GiaMoiGio = 280_000 }
            ],
            DsLichRanh =
            [
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000009"), GiaSuId = giaSu4Id, ThuTrongTuan = 2, GioBatDau = new TimeOnly(19, 0), GioKetThuc = new TimeOnly(21, 30) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000010"), GiaSuId = giaSu4Id, ThuTrongTuan = 5, GioBatDau = new TimeOnly(19, 0), GioKetThuc = new TimeOnly(21, 30) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000011"), GiaSuId = giaSu4Id, ThuTrongTuan = 7, GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(17, 30) }
            ],
            DsGiayTo =
            [
                new GiayToGiaSu
                {
                    Id = Guid.Parse("70000000-0000-0000-0000-000000000004"),
                    GiaSuId = giaSu4Id,
                    Loai = LoaiGiayTo.BANG_CAP,
                    DuongDanTep = "giay-to-gia-su/bang-tien-si-hoa-demo.pdf",
                    TenTepGoc = "bang-tien-si-hoa.pdf",
                    TrangThai = TrangThaiGiayTo.DA_DUYET,
                    DuyetLuc = new DateTime(2026, 1, 21, 10, 0, 0, DateTimeKind.Utc),
                    NguoiDuyetId = adminId
                }
            ]
        };

        var giaSu5 = new GiaSu
        {
            Id = giaSu5Id,
            Email = "giasu5.demo@example.tutorhub.test",
            MatKhauHash = hashMacDinh,
            HoTen = "Cô Lương Ngọc Mai",
            SoDienThoai = "0900000008",
            AnhDaiDienUrl = "/assets/img/tutors/tutor-ngoc-mai.jpg",
            KhuVuc = "Cầu Giấy, Hà Nội",
            TrangThai = TrangThaiNguoiDung.HOAT_DONG,
            XacThucEmailLuc = new DateTime(2026, 1, 25, 8, 0, 0, DateTimeKind.Utc),
            TaoLuc = new DateTime(2026, 1, 25, 8, 0, 0, DateTimeKind.Utc),
            TieuDe = "Giáo viên Ngữ văn & Kỹ năng nghị luận xã hội THCS - THPT",
            GioiThieu = "Rèn kỹ năng cảm thụ văn học, lập dàn ý nghị luận xã hội sâu sắc, sửa bài chi tiết từng đoạn văn cho học sinh lớp 9 và lớp 12.",
            HocVan = "Cử nhân Sư phạm Ngữ văn — ĐH Sư phạm Hà Nội",
            SoNamKinhNghiem = 5,
            DayTrucTuyen = true,
            DayTrucTiep = true,
            TrangThaiHoSo = TrangThaiHoSoGiaSu.DA_DUYET,
            CoHocThu = true,
            BanKinhPhucVuKm = 12,
            NguoiDuyetId = adminId,
            TaiKhoanVi = new TaiKhoanVi { Id = viGs5Id, NguoiDungThamGiaId = giaSu5Id, SoDu = 0, TaoLuc = new DateTime(2026, 1, 25, 8, 0, 0, DateTimeKind.Utc) },
            TaiKhoanNganHang = new TaiKhoanNganHang { GiaSuId = giaSu5Id, TenNganHang = "MBBank", SoTaiKhoan = "098877665544", ChuTaiKhoan = "LUONG NGOC MAI" },
            DsMonDay =
            [
                new GiaSuMonHoc { Id = gsm7Id, GiaSuId = giaSu5Id, MonHocId = monNguVanId, GiaMoiGio = 190_000 }
            ],
            DsLichRanh =
            [
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000012"), GiaSuId = giaSu5Id, ThuTrongTuan = 3, GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 30) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000013"), GiaSuId = giaSu5Id, ThuTrongTuan = 6, GioBatDau = new TimeOnly(18, 0), GioKetThuc = new TimeOnly(20, 30) },
                new LichLapHangTuan { Id = Guid.Parse("60000000-0000-0000-0000-000000000014"), GiaSuId = giaSu5Id, ThuTrongTuan = 1, GioBatDau = new TimeOnly(14, 0), GioKetThuc = new TimeOnly(17, 0) }
            ],
            DsGiayTo =
            [
                new GiayToGiaSu
                {
                    Id = Guid.Parse("70000000-0000-0000-0000-000000000005"),
                    GiaSuId = giaSu5Id,
                    Loai = LoaiGiayTo.BANG_CAP,
                    DuongDanTep = "giay-to-gia-su/bang-su-pham-van-demo.pdf",
                    TenTepGoc = "bang-su-pham-van.pdf",
                    TrangThai = TrangThaiGiayTo.DA_DUYET,
                    DuyetLuc = new DateTime(2026, 1, 26, 10, 0, 0, DateTimeKind.Utc),
                    NguoiDuyetId = adminId
                }
            ]
        };

        db.GiaSus.AddRange(giaSu1, giaSu2, giaSu3, giaSu4, giaSu5);

        // 6. Mục yêu thích mẫu
        db.MucYeuThichs.Add(new MucYeuThich
        {
            HocSinhId = hocSinh1Id,
            GiaSuId = giaSu1Id,
            TaoLuc = new DateTime(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc)
        });

        // 7. Đơn đặt lịch, Thanh toán, Giao dịch ví, Đánh giá, Khiếu nại, Yêu cầu rút tiền mẫu
        var don1Id = Guid.Parse("80000000-0000-0000-0000-000000000001");
        var don2Id = Guid.Parse("80000000-0000-0000-0000-000000000002");
        var don3Id = Guid.Parse("80000000-0000-0000-0000-000000000003");

        db.DonDatLichs.AddRange(
            new DonDatLich
            {
                Id = don1Id,
                MaDon = "BK-20261110-0001",
                HocSinhId = hocSinh1Id,
                GiaSuMonHocId = gsm1Id,
                MaGiamGiaId = null,
                HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
                BatDauLuc = new DateTime(2026, 11, 10, 11, 0, 0, DateTimeKind.Utc),
                KetThucLuc = new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc),
                GiaMoiGio = 250_000,
                TienGiam = 0,
                TrangThai = TrangThaiDonDatLich.HOAN_THANH,
                LaBuoiHocThu = false,
                PhongHocUrl = $"/pages/phong-hoc.html?donId={don1Id}",
                HanPhanHoi = new DateTime(2026, 11, 9, 11, 0, 0, DateTimeKind.Utc),
                TaoLuc = new DateTime(2026, 11, 8, 11, 0, 0, DateTimeKind.Utc)
            },
            new DonDatLich
            {
                Id = don2Id,
                MaDon = "BK-20261125-0002",
                HocSinhId = hocSinh1Id,
                GiaSuMonHocId = gsm3Id,
                MaGiamGiaId = maWelcomeId,
                HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
                BatDauLuc = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc),
                KetThucLuc = new DateTime(2026, 11, 25, 13, 0, 0, DateTimeKind.Utc),
                GiaMoiGio = 300_000,
                TienGiam = 30_000,
                TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
                LaBuoiHocThu = false,
                PhongHocUrl = $"/pages/phong-hoc.html?donId={don2Id}",
                HanPhanHoi = new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc),
                TaoLuc = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc)
            },
            new DonDatLich
            {
                Id = don3Id,
                MaDon = "BK-20261115-0003",
                HocSinhId = hocSinh2Id,
                GiaSuMonHocId = gsm2Id,
                MaGiamGiaId = null,
                HinhThuc = HinhThucBuoiHoc.TRUC_TIEP,
                DiaChiHocSinh = "Số 128 đường 30/4, P. An Phú, Q. Ninh Kiều, Cần Thơ",
                BatDauLuc = new DateTime(2026, 11, 15, 11, 0, 0, DateTimeKind.Utc),
                KetThucLuc = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc),
                GiaMoiGio = 240_000,
                TienGiam = 0,
                TrangThai = TrangThaiDonDatLich.TRANH_CHAP,
                LaBuoiHocThu = false,
                HanPhanHoi = new DateTime(2026, 11, 14, 11, 0, 0, DateTimeKind.Utc),
                TaoLuc = new DateTime(2026, 11, 13, 11, 0, 0, DateTimeKind.Utc)
            });

        var tt1Id = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var tt2Id = Guid.Parse("90000000-0000-0000-0000-000000000002");
        var tt3Id = Guid.Parse("90000000-0000-0000-0000-000000000003");

        db.ThanhToans.AddRange(
            new ThanhToan
            {
                Id = tt1Id,
                DonDatLichId = don1Id,
                SoTien = 500_000,
                PhuongThuc = PhuongThucThanhToan.VNPAY,
                TrangThai = TrangThaiThanhToan.DA_GIAI_NGAN,
                MaGiaoDichCong = "PAY-20261108-000001",
                ThanhToanLuc = new DateTime(2026, 11, 8, 11, 5, 0, DateTimeKind.Utc),
                GiaiNganLuc = new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc),
                TienDaHoan = 0,
                TaoLuc = new DateTime(2026, 11, 8, 11, 2, 0, DateTimeKind.Utc)
            },
            new ThanhToan
            {
                Id = tt2Id,
                DonDatLichId = don2Id,
                SoTien = 270_000,
                PhuongThuc = PhuongThucThanhToan.VI_NOI_BO,
                TrangThai = TrangThaiThanhToan.DANG_GIU,
                MaGiaoDichCong = null,
                ThanhToanLuc = new DateTime(2026, 11, 19, 12, 1, 0, DateTimeKind.Utc),
                TienDaHoan = 0,
                TaoLuc = new DateTime(2026, 11, 19, 12, 1, 0, DateTimeKind.Utc)
            },
            new ThanhToan
            {
                Id = tt3Id,
                DonDatLichId = don3Id,
                SoTien = 240_000,
                PhuongThuc = PhuongThucThanhToan.GIA_LAP,
                TrangThai = TrangThaiThanhToan.DANG_GIU,
                MaGiaoDichCong = null,
                ThanhToanLuc = new DateTime(2026, 11, 13, 11, 5, 0, DateTimeKind.Utc),
                TienDaHoan = 0,
                TaoLuc = new DateTime(2026, 11, 13, 11, 3, 0, DateTimeKind.Utc)
            });

        db.GiaoDichVis.AddRange(
            new GiaoDichVi
            {
                Id = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                TaiKhoanViId = viHs1Id,
                SoThuTu = 1,
                Loai = LoaiGiaoDichVi.NAP_TIEN,
                SoTien = 770_000,
                SoDuSau = 770_000,
                GhiChu = "Nạp tiền mẫu vào ví học sinh",
                TaoLuc = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc)
            },
            new GiaoDichVi
            {
                Id = Guid.Parse("a0000000-0000-0000-0000-000000000002"),
                TaiKhoanViId = viHs1Id,
                SoThuTu = 2,
                Loai = LoaiGiaoDichVi.THANH_TOAN,
                SoTien = -270_000,
                SoDuSau = 500_000,
                GhiChu = "Thanh toán đơn BK-20261125-0002 bằng ví nội bộ",
                ThanhToanId = tt2Id,
                TaoLuc = new DateTime(2026, 11, 19, 12, 1, 0, DateTimeKind.Utc)
            },
            new GiaoDichVi
            {
                Id = Guid.Parse("a0000000-0000-0000-0000-000000000003"),
                TaiKhoanViId = viGs1Id,
                SoThuTu = 1,
                Loai = LoaiGiaoDichVi.THU_NHAP,
                SoTien = 440_000,
                SoDuSau = 440_000,
                GhiChu = "Giải ngân thu nhập đơn BK-20261110-0001 (đã trừ 12% phí nền tảng)",
                ThanhToanId = tt1Id,
                TaoLuc = new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc)
            });

        db.DanhGias.Add(new DanhGia
        {
            Id = Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            DonDatLichId = don1Id,
            Diem = 5,
            BinhLuan = "Thầy giảng bài rất dễ hiểu, nhiệt tình và đúng giờ. Em đã nắm chắc phần hàm số và tích phân!",
            PhanHoiGiaSu = "Cảm ơn em đã chăm chỉ làm bài tập!",
            TaoLuc = new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc)
        });

        db.KhieuNais.Add(new KhieuNai
        {
            Id = Guid.Parse("c0000000-0000-0000-0000-000000000001"),
            DonDatLichId = don3Id,
            DanhMuc = DanhMucKhieuNai.CHAT_LUONG,
            MoTa = "Buổi học kết thúc sớm hơn thời lượng đã đăng ký 20 phút.",
            TrangThai = TrangThaiKhieuNai.MO,
            TaoLuc = new DateTime(2026, 11, 15, 14, 0, 0, DateTimeKind.Utc)
        });

        db.YeuCauRutTiens.Add(new YeuCauRutTien
        {
            Id = Guid.Parse("d0000000-0000-0000-0000-000000000001"),
            GiaSuId = giaSu1Id,
            SoTien = 200_000,
            TrangThai = TrangThaiRutTien.CHO_XU_LY,
            GhiChu = "Yêu cầu rút tiền mẫu",
            TenNganHang = "Vietcombank",
            SoTaiKhoan = "0000123456789",
            ChuTaiKhoan = "NGUYEN HOANG MINH",
            TaoLuc = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc)
        });

        await db.SaveChangesAsync(cancellationToken);

        await DongBoDanhMucMonHocDayDuAsync(db, cancellationToken);
        await DongBoThongBaoKhieuNaiMauAsync(db, cancellationToken);
        await DongBoDonDemoTrongCuaSoKhieuNaiAsync(db, cancellationToken);
    }

    private static async Task DongBoDonDemoTrongCuaSoKhieuNaiAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var hs1 = await db.HocSinhs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Email == "hocsinh1.demo@example.tutorhub.test", cancellationToken);
        var gs1 = await db.GiaSus
            .AsNoTracking()
            .Include(g => g.DsMonDay)
            .FirstOrDefaultAsync(g => g.Email == "giasu1.demo@example.tutorhub.test", cancellationToken);
        var gsm = gs1?.DsMonDay.FirstOrDefault();

        if (hs1 is null || gs1 is null || gsm is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var giaSnapshot = 250_000;
        var coThayDoi = false;

        const string maDonDemo = "BK-20261121-0004";
        if (!await db.DonDatLichs.AnyAsync(d => d.MaDon == maDonDemo, cancellationToken))
        {
            var batDau = now.AddHours(-3);
            var ketThuc = now.AddHours(-2);
            var donId = Guid.Parse("a0000000-0000-0000-0000-000000000004");

            db.DonDatLichs.Add(new DonDatLich
            {
                Id = donId,
                MaDon = maDonDemo,
                HocSinhId = hs1.Id,
                GiaSuMonHocId = gsm.Id,
                HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
                BatDauLuc = batDau,
                KetThucLuc = ketThuc,
                GiaMoiGio = giaSnapshot,
                TienGiam = 0,
                LaBuoiHocThu = false,
                TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
                PhongHocUrl = $"/pages/phong-hoc.html?donId={donId}",
                HanPhanHoi = batDau.AddHours(-6),
                TaoLuc = batDau.AddDays(-1)
            });

            db.ThanhToans.Add(new ThanhToan
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000004"),
                DonDatLichId = donId,
                PhuongThuc = PhuongThucThanhToan.GIA_LAP,
                SoTien = giaSnapshot,
                TrangThai = TrangThaiThanhToan.DANG_GIU,
                MaGiaoDichCong = null,
                ThanhToanLuc = batDau.AddHours(-12),
                TienDaHoan = 0,
                TaoLuc = batDau.AddHours(-12)
            });
            coThayDoi = true;
        }

        // Thêm 1 buổi học Online đang diễn ra ngay hiện tại để kiểm thử tức thì phòng học điểm danh + bảng trắng
        const string maDonLive = "BK-20261121-LIVE";
        if (!await db.DonDatLichs.AnyAsync(d => d.MaDon == maDonLive, cancellationToken))
        {
            var donLiveId = Guid.Parse("a0000000-0000-0000-0000-000000000005");
            var bdLive = now.AddMinutes(-5);
            var ktLive = now.AddMinutes(55);

            db.DonDatLichs.Add(new DonDatLich
            {
                Id = donLiveId,
                MaDon = maDonLive,
                HocSinhId = hs1.Id,
                GiaSuMonHocId = gsm.Id,
                HinhThuc = HinhThucBuoiHoc.TRUC_TUYEN,
                BatDauLuc = bdLive,
                KetThucLuc = ktLive,
                GiaMoiGio = giaSnapshot,
                TienGiam = 0,
                LaBuoiHocThu = false,
                TrangThai = TrangThaiDonDatLich.DA_XAC_NHAN,
                PhongHocUrl = $"/pages/phong-hoc.html?donId={donLiveId}",
                HanPhanHoi = bdLive.AddHours(-6),
                TaoLuc = bdLive.AddDays(-1)
            });

            db.ThanhToans.Add(new ThanhToan
            {
                Id = Guid.Parse("b0000000-0000-0000-0000-000000000005"),
                DonDatLichId = donLiveId,
                PhuongThuc = PhuongThucThanhToan.GIA_LAP,
                SoTien = giaSnapshot,
                TrangThai = TrangThaiThanhToan.DANG_GIU,
                MaGiaoDichCong = null,
                ThanhToanLuc = bdLive.AddHours(-10),
                TienDaHoan = 0,
                TaoLuc = bdLive.AddHours(-10)
            });
            coThayDoi = true;
        }

        // Chuyển toàn bộ link phòng học mẫu còn dùng Jitsi ngoài sang phòng học nội bộ TutorHub
        var donJitsiCu = await db.DonDatLichs
            .Where(d => d.PhongHocUrl != null && d.PhongHocUrl.StartsWith("https://meet.jit.si"))
            .ToListAsync(cancellationToken);
        foreach (var d in donJitsiCu)
        {
            d.PhongHocUrl = $"/pages/phong-hoc.html?donId={d.Id}";
            coThayDoi = true;
        }

        if (coThayDoi)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
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

    private static readonly (int SoThuTu, string Ten)[] DanhMucMonHocBoSung =
    [
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

        foreach (var (soThuTu, ten) in DanhMucMonHocBoSung)
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

    private static async Task DongBoViTaiKhoanDemoIdempotentAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var dsViDemo = await db.TaiKhoanVis
            .Include(v => v.NguoiDungThamGia)
            .Include(v => v.DsGiaoDich)
            .Where(v => v.NguoiDungThamGia != null &&
                        v.NguoiDungThamGia.Email.EndsWith(".demo@example.tutorhub.test"))
            .ToListAsync(cancellationToken);

        if (dsViDemo.Count == 0)
        {
            return;
        }

        var dsGiaSuIds = dsViDemo.Select(v => v.NguoiDungThamGiaId).ToList();
        var mapDangRut = await db.YeuCauRutTiens
            .AsNoTracking()
            .Where(r => dsGiaSuIds.Contains(r.GiaSuId) && r.TrangThai == TrangThaiRutTien.CHO_XU_LY)
            .GroupBy(r => r.GiaSuId)
            .Select(g => new { GiaSuId = g.Key, Tong = g.Sum(x => x.SoTien) })
            .ToDictionaryAsync(x => x.GiaSuId, x => x.Tong, cancellationToken);

        var coThayDoi = false;
        foreach (var vi in dsViDemo)
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
