using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Dot2_SoCaiViRutTienVaAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TaiKhoanVi_SoDu",
                table: "TaiKhoanVi");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KhieuNai_TrangThai_GiaiQuyet",
                table: "KhieuNai");

            migrationBuilder.DropIndex(
                name: "IX_GiaoDichVi_TaiKhoanViId",
                table: "GiaoDichVi");

            migrationBuilder.DropIndex(
                name: "IX_GiaoDichVi_ThanhToanId",
                table: "GiaoDichVi");

            // 1) YeuCauRutTien: ADD 3 cột snapshot ngân hàng NULL -> BACKFILL từ TaiKhoanNganHang -> ALTER NOT NULL
            migrationBuilder.AddColumn<string>(
                name: "TenNganHang",
                table: "YeuCauRutTien",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoTaiKhoan",
                table: "YeuCauRutTien",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChuTaiKhoan",
                table: "YeuCauRutTien",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE r
                SET r.TenNganHang = tk.TenNganHang,
                    r.SoTaiKhoan  = tk.SoTaiKhoan,
                    r.ChuTaiKhoan = tk.ChuTaiKhoan
                FROM YeuCauRutTien r
                INNER JOIN TaiKhoanNganHang tk ON tk.GiaSuId = r.GiaSuId;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TenNganHang",
                table: "YeuCauRutTien",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SoTaiKhoan",
                table: "YeuCauRutTien",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(60)",
                oldMaxLength: 60,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChuTaiKhoan",
                table: "YeuCauRutTien",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "YeuCauRutTien",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            // 2) TaiKhoanVi: CHỈ thêm cột SoDuDangXuLyRut + RowVersion (KHÔNG ghi đè SoDu trong migration)
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "TaiKhoanVi",
                type: "rowversion",
                rowVersion: true,
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "SoDuDangXuLyRut",
                table: "TaiKhoanVi",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GhiChuGiaiQuyet",
                table: "KhieuNai",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // 3) GiaoDichVi.SoThuTu: ADD NULL -> BACKFILL theo (TaoLuc, Id) -> ALTER NOT NULL
            migrationBuilder.AddColumn<int>(
                name: "SoThuTu",
                table: "GiaoDichVi",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                WITH cte AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY TaiKhoanViId ORDER BY TaoLuc ASC, Id ASC) AS rn
                    FROM GiaoDichVi
                )
                UPDATE g
                SET g.SoThuTu = cte.rn
                FROM GiaoDichVi g
                INNER JOIN cte ON cte.Id = g.Id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "SoThuTu",
                table: "GiaoDichVi",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiDungId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    HanhDong = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DoiTuong = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    GiaTriCuJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GiaTriMoiJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DiaChiIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauRutTien_IdempotencyKey",
                table: "YeuCauRutTien",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaiKhoanVi_SoDu",
                table: "TaiKhoanVi",
                sql: "[SoDu] >= 0 AND [SoDuDangXuLyRut] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KhieuNai_TrangThai_GiaiQuyet",
                table: "KhieuNai",
                sql: "([TrangThai] = 'MO' AND [GiaiQuyetLuc] IS NULL AND [NguoiGiaiQuyetId] IS NULL AND [GhiChuGiaiQuyet] IS NULL) OR ([TrangThai] IN ('DA_GIAI_QUYET', 'TU_CHOI') AND [GiaiQuyetLuc] IS NOT NULL AND [NguoiGiaiQuyetId] IS NOT NULL AND [GhiChuGiaiQuyet] IS NOT NULL AND LTRIM(RTRIM([GhiChuGiaiQuyet])) <> '')");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichVi_TaiKhoanViId_SoThuTu",
                table: "GiaoDichVi",
                columns: new[] { "TaiKhoanViId", "SoThuTu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichVi_ThanhToanId_Loai",
                table: "GiaoDichVi",
                columns: new[] { "ThanhToanId", "Loai" },
                unique: true,
                filter: "[ThanhToanId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaoDichVi_SoThuTu",
                table: "GiaoDichVi",
                sql: "[SoThuTu] >= 1");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_NguoiDungId_TaoLuc",
                table: "AuditLog",
                columns: new[] { "NguoiDungId", "TaoLuc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_YeuCauRutTien_IdempotencyKey",
                table: "YeuCauRutTien");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaiKhoanVi_SoDu",
                table: "TaiKhoanVi");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KhieuNai_TrangThai_GiaiQuyet",
                table: "KhieuNai");

            migrationBuilder.DropIndex(
                name: "IX_GiaoDichVi_TaiKhoanViId_SoThuTu",
                table: "GiaoDichVi");

            migrationBuilder.DropIndex(
                name: "IX_GiaoDichVi_ThanhToanId_Loai",
                table: "GiaoDichVi");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaoDichVi_SoThuTu",
                table: "GiaoDichVi");

            migrationBuilder.DropColumn(
                name: "ChuTaiKhoan",
                table: "YeuCauRutTien");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "YeuCauRutTien");

            migrationBuilder.DropColumn(
                name: "SoTaiKhoan",
                table: "YeuCauRutTien");

            migrationBuilder.DropColumn(
                name: "TenNganHang",
                table: "YeuCauRutTien");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "TaiKhoanVi");

            migrationBuilder.DropColumn(
                name: "SoDuDangXuLyRut",
                table: "TaiKhoanVi");

            migrationBuilder.DropColumn(
                name: "GhiChuGiaiQuyet",
                table: "KhieuNai");

            migrationBuilder.DropColumn(
                name: "SoThuTu",
                table: "GiaoDichVi");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaiKhoanVi_SoDu",
                table: "TaiKhoanVi",
                sql: "[SoDu] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KhieuNai_TrangThai_GiaiQuyet",
                table: "KhieuNai",
                sql: "([TrangThai] = 'MO' AND [GiaiQuyetLuc] IS NULL AND [NguoiGiaiQuyetId] IS NULL) OR ([TrangThai] IN ('DA_GIAI_QUYET', 'TU_CHOI') AND [GiaiQuyetLuc] IS NOT NULL AND [NguoiGiaiQuyetId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichVi_TaiKhoanViId",
                table: "GiaoDichVi",
                column: "TaiKhoanViId");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDichVi_ThanhToanId",
                table: "GiaoDichVi",
                column: "ThanhToanId");
        }
    }
}
