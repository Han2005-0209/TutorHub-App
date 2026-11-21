using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KhoiTaoMoHinh24LopV4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaGiamGia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ma = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GiaTri = table.Column<int>(type: "int", nullable: false),
                    GiaTriDonToiThieu = table.Column<int>(type: "int", nullable: false),
                    SoLuotToiDa = table.Column<int>(type: "int", nullable: false),
                    BatDauLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HetHanLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DangHoatDong = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaGiamGia", x => x.Id);
                    table.CheckConstraint("CK_MaGiamGia_GiaTri", "[GiaTri] > 0 AND [GiaTriDonToiThieu] >= 0 AND [SoLuotToiDa] > 0 AND [HetHanLuc] > [BatDauLuc]");
                });

            migrationBuilder.CreateTable(
                name: "MonHoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DangHoatDong = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonHoc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NguoiDung",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MatKhauHash = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SoDienThoai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AnhDaiDienUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    KhuVuc = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    XacThucEmailLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NguoiDung", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NguoiDungThamGia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NguoiDungThamGia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NguoiDungThamGia_NguoiDung_Id",
                        column: x => x.Id,
                        principalTable: "NguoiDung",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuanTriVien",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuanTriVien", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuanTriVien_NguoiDung_Id",
                        column: x => x.Id,
                        principalTable: "NguoiDung",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThongBao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiNhanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DocLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThongBao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThongBao_NguoiDung_NguoiNhanId",
                        column: x => x.NguoiNhanId,
                        principalTable: "NguoiDung",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HocSinh",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HocSinh", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HocSinh_NguoiDungThamGia_Id",
                        column: x => x.Id,
                        principalTable: "NguoiDungThamGia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaiKhoanVi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiDungThamGiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoDu = table.Column<int>(type: "int", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoanVi", x => x.Id);
                    table.CheckConstraint("CK_TaiKhoanVi_SoDu", "[SoDu] >= 0");
                    table.ForeignKey(
                        name: "FK_TaiKhoanVi_NguoiDungThamGia_NguoiDungThamGiaId",
                        column: x => x.NguoiDungThamGiaId,
                        principalTable: "NguoiDungThamGia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GiaSu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TieuDe = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    GioiThieu = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    HocVan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SoNamKinhNghiem = table.Column<int>(type: "int", nullable: false),
                    DayTrucTuyen = table.Column<bool>(type: "bit", nullable: false),
                    DayTrucTiep = table.Column<bool>(type: "bit", nullable: false),
                    TrangThaiHoSo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LyDoTuChoi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GiaHocThu = table.Column<int>(type: "int", nullable: true),
                    BanKinhPhucVuKm = table.Column<int>(type: "int", nullable: false),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaSu", x => x.Id);
                    table.CheckConstraint("CK_GiaSu_HinhThucDay", "[DayTrucTuyen] = 1 OR [DayTrucTiep] = 1");
                    table.CheckConstraint("CK_GiaSu_SoNamKinhNghiem", "[SoNamKinhNghiem] >= 0 AND [BanKinhPhucVuKm] >= 0 AND ([GiaHocThu] IS NULL OR [GiaHocThu] >= 0)");
                    table.ForeignKey(
                        name: "FK_GiaSu_NguoiDungThamGia_Id",
                        column: x => x.Id,
                        principalTable: "NguoiDungThamGia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiaSu_QuanTriVien_NguoiDuyetId",
                        column: x => x.NguoiDuyetId,
                        principalTable: "QuanTriVien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiaSuMonHoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaSuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonHocId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapLop = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GiaMoiGio = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaSuMonHoc", x => x.Id);
                    table.CheckConstraint("CK_GiaSuMonHoc_GiaMoiGio", "[GiaMoiGio] > 0");
                    table.ForeignKey(
                        name: "FK_GiaSuMonHoc_GiaSu_GiaSuId",
                        column: x => x.GiaSuId,
                        principalTable: "GiaSu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiaSuMonHoc_MonHoc_MonHocId",
                        column: x => x.MonHocId,
                        principalTable: "MonHoc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiayToGiaSu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaSuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DuongDanTep = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TenTepGoc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DuyetLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiayToGiaSu", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiayToGiaSu_GiaSu_GiaSuId",
                        column: x => x.GiaSuId,
                        principalTable: "GiaSu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiayToGiaSu_QuanTriVien_NguoiDuyetId",
                        column: x => x.NguoiDuyetId,
                        principalTable: "QuanTriVien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HoiThoai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HocSinhId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaSuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoiThoai", x => x.Id);
                    table.CheckConstraint("CK_HoiThoai_KhacNguoi", "[HocSinhId] <> [GiaSuId]");
                    table.ForeignKey(
                        name: "FK_HoiThoai_GiaSu_GiaSuId",
                        column: x => x.GiaSuId,
                        principalTable: "GiaSu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoiThoai_HocSinh_HocSinhId",
                        column: x => x.HocSinhId,
                        principalTable: "HocSinh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LichRanh",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaSuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichRanh", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LichRanh_GiaSu_GiaSuId",
                        column: x => x.GiaSuId,
                        principalTable: "GiaSu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MucYeuThich",
                columns: table => new
                {
                    HocSinhId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaSuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MucYeuThich", x => new { x.HocSinhId, x.GiaSuId });
                    table.ForeignKey(
                        name: "FK_MucYeuThich_GiaSu_GiaSuId",
                        column: x => x.GiaSuId,
                        principalTable: "GiaSu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MucYeuThich_HocSinh_HocSinhId",
                        column: x => x.HocSinhId,
                        principalTable: "HocSinh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaiKhoanNganHang",
                columns: table => new
                {
                    TenNganHang = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    SoTaiKhoan = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ChuTaiKhoan = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    GiaSuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaiKhoanNganHang", x => new { x.TenNganHang, x.SoTaiKhoan });
                    table.ForeignKey(
                        name: "FK_TaiKhoanNganHang_GiaSu_GiaSuId",
                        column: x => x.GiaSuId,
                        principalTable: "GiaSu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YeuCauRutTien",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaSuId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoTien = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    XuLyLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NguoiXuLyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauRutTien", x => x.Id);
                    table.CheckConstraint("CK_YeuCauRutTien_SoTien", "[SoTien] >= 100000");
                    table.ForeignKey(
                        name: "FK_YeuCauRutTien_GiaSu_GiaSuId",
                        column: x => x.GiaSuId,
                        principalTable: "GiaSu",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YeuCauRutTien_QuanTriVien_NguoiXuLyId",
                        column: x => x.NguoiXuLyId,
                        principalTable: "QuanTriVien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonDatLich",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaDon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    HocSinhId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GiaSuMonHocId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaGiamGiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HinhThuc = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BatDauLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KetThucLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GiaMoiGio = table.Column<int>(type: "int", nullable: false),
                    TienGiam = table.Column<int>(type: "int", nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    LaBuoiHocThu = table.Column<bool>(type: "bit", nullable: false),
                    PhongHocUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LyDoHuy = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SoLanDoiLich = table.Column<int>(type: "int", nullable: false),
                    HanPhanHoi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonDatLich", x => x.Id);
                    table.CheckConstraint("CK_DonDatLich_ThoiGianVaTien", "[KetThucLuc] > [BatDauLuc] AND [GiaMoiGio] >= 0 AND [TienGiam] >= 0 AND [SoLanDoiLich] BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_DonDatLich_GiaSuMonHoc_GiaSuMonHocId",
                        column: x => x.GiaSuMonHocId,
                        principalTable: "GiaSuMonHoc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonDatLich_HocSinh_HocSinhId",
                        column: x => x.HocSinhId,
                        principalTable: "HocSinh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonDatLich_MaGiamGia_MaGiamGiaId",
                        column: x => x.MaGiamGiaId,
                        principalTable: "MaGiamGia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TinNhan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HoiThoaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiGuiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NoiDung = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BiGanCo = table.Column<bool>(type: "bit", nullable: false),
                    DocLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TinNhan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TinNhan_HoiThoai_HoiThoaiId",
                        column: x => x.HoiThoaiId,
                        principalTable: "HoiThoai",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TinNhan_NguoiDungThamGia_NguoiGuiId",
                        column: x => x.NguoiGuiId,
                        principalTable: "NguoiDungThamGia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LichLapHangTuan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThuTrongTuan = table.Column<int>(type: "int", nullable: false),
                    GioBatDau = table.Column<TimeOnly>(type: "time(0)", nullable: false),
                    GioKetThuc = table.Column<TimeOnly>(type: "time(0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichLapHangTuan", x => x.Id);
                    table.CheckConstraint("CK_LichLapHangTuan_ThoiGian", "[ThuTrongTuan] BETWEEN 1 AND 7 AND [GioKetThuc] > [GioBatDau]");
                    table.ForeignKey(
                        name: "FK_LichLapHangTuan_LichRanh_Id",
                        column: x => x.Id,
                        principalTable: "LichRanh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LichNgayCuThe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NgayCuThe = table.Column<DateOnly>(type: "date", nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    GioBatDau = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    GioKetThuc = table.Column<TimeOnly>(type: "time(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichNgayCuThe", x => x.Id);
                    table.CheckConstraint("CK_LichNgayCuThe_ThoiGian", "([GioBatDau] IS NULL AND [GioKetThuc] IS NULL) OR ([GioBatDau] IS NOT NULL AND [GioKetThuc] IS NOT NULL AND [GioKetThuc] > [GioBatDau])");
                    table.ForeignKey(
                        name: "FK_LichNgayCuThe_LichRanh_Id",
                        column: x => x.Id,
                        principalTable: "LichRanh",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DanhGia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonDatLichId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Diem = table.Column<int>(type: "int", nullable: false),
                    BinhLuan = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PhanHoiGiaSu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BiAn = table.Column<bool>(type: "bit", nullable: false),
                    LyDoAn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NguoiKiemDuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanhGia", x => x.Id);
                    table.CheckConstraint("CK_DanhGia_Diem", "[Diem] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_DanhGia_DonDatLich_DonDatLichId",
                        column: x => x.DonDatLichId,
                        principalTable: "DonDatLich",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DanhGia_QuanTriVien_NguoiKiemDuyetId",
                        column: x => x.NguoiKiemDuyetId,
                        principalTable: "QuanTriVien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KhieuNai",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonDatLichId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiKhieuNaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DanhMuc = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BangChungUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CachGiaiQuyet = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SoTienHoan = table.Column<int>(type: "int", nullable: true),
                    GiaiQuyetLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiGiaiQuyetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhieuNai", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KhieuNai_DonDatLich_DonDatLichId",
                        column: x => x.DonDatLichId,
                        principalTable: "DonDatLich",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KhieuNai_NguoiDungThamGia_NguoiKhieuNaiId",
                        column: x => x.NguoiKhieuNaiId,
                        principalTable: "NguoiDungThamGia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KhieuNai_QuanTriVien_NguoiGiaiQuyetId",
                        column: x => x.NguoiGiaiQuyetId,
                        principalTable: "QuanTriVien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ThanhToan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonDatLichId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoTien = table.Column<int>(type: "int", nullable: false),
                    PhuongThuc = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MaGiaoDichCong = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ThanhToanLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GiaiNganLuc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TienDaHoan = table.Column<int>(type: "int", nullable: false),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThanhToan", x => x.Id);
                    table.CheckConstraint("CK_ThanhToan_SoTien", "[SoTien] >= 0 AND [TienDaHoan] >= 0 AND [TienDaHoan] <= [SoTien]");
                    table.ForeignKey(
                        name: "FK_ThanhToan_DonDatLich_DonDatLichId",
                        column: x => x.DonDatLichId,
                        principalTable: "DonDatLich",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GiaoDichVi",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaiKhoanViId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SoTien = table.Column<int>(type: "int", nullable: false),
                    SoDuSau = table.Column<int>(type: "int", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ThanhToanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    YeuCauRutTienId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiaoDichVi", x => x.Id);
                    table.CheckConstraint("CK_GiaoDichVi_ThamChieu", "([ThanhToanId] IS NULL OR [YeuCauRutTienId] IS NULL) AND ([Loai] <> 'NAP_TIEN' OR ([ThanhToanId] IS NULL AND [YeuCauRutTienId] IS NULL))");
                    table.ForeignKey(
                        name: "FK_GiaoDichVi_TaiKhoanVi_TaiKhoanViId",
                        column: x => x.TaiKhoanViId,
                        principalTable: "TaiKhoanVi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GiaoDichVi_ThanhToan_ThanhToanId",
                        column: x => x.ThanhToanId,
                        principalTable: "ThanhToan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GiaoDichVi_YeuCauRutTien_YeuCauRutTienId",
                        column: x => x.YeuCauRutTienId,
                        principalTable: "YeuCauRutTien",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DanhGia_DonDatLichId",
                table: "DanhGia",
                column: "DonDatLichId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DanhGia_NguoiKiemDuyetId",
                table: "DanhGia",
                column: "NguoiKiemDuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_DonDatLich_GiaSuMonHocId_BatDauLuc_KetThucLuc",
                table: "DonDatLich",
                columns: new[] { "GiaSuMonHocId", "BatDauLuc", "KetThucLuc" });

            migrationBuilder.CreateIndex(
                name: "IX_DonDatLich_HocSinhId_BatDauLuc_KetThucLuc",
                table: "DonDatLich",
                columns: new[] { "HocSinhId", "BatDauLuc", "KetThucLuc" });

            migrationBuilder.CreateIndex(
                name: "IX_DonDatLich_MaDon",
                table: "DonDatLich",
                column: "MaDon",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonDatLich_MaGiamGiaId",
                table: "DonDatLich",
                column: "MaGiamGiaId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichVi_TaiKhoanViId",
                table: "GiaoDichVi",
                column: "TaiKhoanViId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichVi_ThanhToanId",
                table: "GiaoDichVi",
                column: "ThanhToanId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichVi_YeuCauRutTienId",
                table: "GiaoDichVi",
                column: "YeuCauRutTienId",
                unique: true,
                filter: "[YeuCauRutTienId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GiaSu_NguoiDuyetId",
                table: "GiaSu",
                column: "NguoiDuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaSuMonHoc_GiaSuId_MonHocId_CapLop",
                table: "GiaSuMonHoc",
                columns: new[] { "GiaSuId", "MonHocId", "CapLop" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiaSuMonHoc_MonHocId",
                table: "GiaSuMonHoc",
                column: "MonHocId");

            migrationBuilder.CreateIndex(
                name: "IX_GiayToGiaSu_GiaSuId",
                table: "GiayToGiaSu",
                column: "GiaSuId");

            migrationBuilder.CreateIndex(
                name: "IX_GiayToGiaSu_NguoiDuyetId",
                table: "GiayToGiaSu",
                column: "NguoiDuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_HoiThoai_GiaSuId",
                table: "HoiThoai",
                column: "GiaSuId");

            migrationBuilder.CreateIndex(
                name: "IX_HoiThoai_HocSinhId_GiaSuId",
                table: "HoiThoai",
                columns: new[] { "HocSinhId", "GiaSuId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KhieuNai_DonDatLichId",
                table: "KhieuNai",
                column: "DonDatLichId");

            migrationBuilder.CreateIndex(
                name: "IX_KhieuNai_NguoiGiaiQuyetId",
                table: "KhieuNai",
                column: "NguoiGiaiQuyetId");

            migrationBuilder.CreateIndex(
                name: "IX_KhieuNai_NguoiKhieuNaiId",
                table: "KhieuNai",
                column: "NguoiKhieuNaiId");

            migrationBuilder.CreateIndex(
                name: "IX_LichRanh_GiaSuId",
                table: "LichRanh",
                column: "GiaSuId");

            migrationBuilder.CreateIndex(
                name: "IX_MaGiamGia_Ma",
                table: "MaGiamGia",
                column: "Ma",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonHoc_Ten",
                table: "MonHoc",
                column: "Ten",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MucYeuThich_GiaSuId",
                table: "MucYeuThich",
                column: "GiaSuId");

            migrationBuilder.CreateIndex(
                name: "IX_NguoiDung_Email",
                table: "NguoiDung",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoanNganHang_GiaSuId",
                table: "TaiKhoanNganHang",
                column: "GiaSuId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoanVi_NguoiDungThamGiaId",
                table: "TaiKhoanVi",
                column: "NguoiDungThamGiaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_DonDatLichId",
                table: "ThanhToan",
                column: "DonDatLichId");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_MaGiaoDichCong",
                table: "ThanhToan",
                column: "MaGiaoDichCong",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThongBao_NguoiNhanId",
                table: "ThongBao",
                column: "NguoiNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_TinNhan_HoiThoaiId",
                table: "TinNhan",
                column: "HoiThoaiId");

            migrationBuilder.CreateIndex(
                name: "IX_TinNhan_NguoiGuiId",
                table: "TinNhan",
                column: "NguoiGuiId");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauRutTien_GiaSuId",
                table: "YeuCauRutTien",
                column: "GiaSuId");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauRutTien_NguoiXuLyId",
                table: "YeuCauRutTien",
                column: "NguoiXuLyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DanhGia");

            migrationBuilder.DropTable(
                name: "GiaoDichVi");

            migrationBuilder.DropTable(
                name: "GiayToGiaSu");

            migrationBuilder.DropTable(
                name: "KhieuNai");

            migrationBuilder.DropTable(
                name: "LichLapHangTuan");

            migrationBuilder.DropTable(
                name: "LichNgayCuThe");

            migrationBuilder.DropTable(
                name: "MucYeuThich");

            migrationBuilder.DropTable(
                name: "TaiKhoanNganHang");

            migrationBuilder.DropTable(
                name: "ThongBao");

            migrationBuilder.DropTable(
                name: "TinNhan");

            migrationBuilder.DropTable(
                name: "TaiKhoanVi");

            migrationBuilder.DropTable(
                name: "ThanhToan");

            migrationBuilder.DropTable(
                name: "YeuCauRutTien");

            migrationBuilder.DropTable(
                name: "LichRanh");

            migrationBuilder.DropTable(
                name: "HoiThoai");

            migrationBuilder.DropTable(
                name: "DonDatLich");

            migrationBuilder.DropTable(
                name: "GiaSuMonHoc");

            migrationBuilder.DropTable(
                name: "HocSinh");

            migrationBuilder.DropTable(
                name: "MaGiamGia");

            migrationBuilder.DropTable(
                name: "GiaSu");

            migrationBuilder.DropTable(
                name: "MonHoc");

            migrationBuilder.DropTable(
                name: "NguoiDungThamGia");

            migrationBuilder.DropTable(
                name: "QuanTriVien");

            migrationBuilder.DropTable(
                name: "NguoiDung");
        }
    }
}
