using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Dot5_NapTienViPayOS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YeuCauNapTien",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiDungThamGiaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaDonPayOs = table.Column<long>(type: "bigint", nullable: false),
                    SoTien = table.Column<int>(type: "int", nullable: false),
                    CongThanhToan = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PaymentLinkId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QrCode = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaBinNganHang = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SoTaiKhoan = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ChuTaiKhoan = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    NoiDungChuyenKhoan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaThamChieuNganHang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GiaoDichViId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HetHanLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HoanThanhLuc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YeuCauNapTien", x => x.Id);
                    table.CheckConstraint("CK_YeuCauNapTien_SoTien", "[SoTien] >= 2000 AND [SoTien] <= 50000000");
                    table.ForeignKey(
                        name: "FK_YeuCauNapTien_GiaoDichVi_GiaoDichViId",
                        column: x => x.GiaoDichViId,
                        principalTable: "GiaoDichVi",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_YeuCauNapTien_NguoiDungThamGia_NguoiDungThamGiaId",
                        column: x => x.NguoiDungThamGiaId,
                        principalTable: "NguoiDungThamGia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauNapTien_GiaoDichViId",
                table: "YeuCauNapTien",
                column: "GiaoDichViId",
                unique: true,
                filter: "[GiaoDichViId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauNapTien_MaDonPayOs",
                table: "YeuCauNapTien",
                column: "MaDonPayOs",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YeuCauNapTien_NguoiDungThamGiaId_TaoLuc",
                table: "YeuCauNapTien",
                columns: new[] { "NguoiDungThamGiaId", "TaoLuc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YeuCauNapTien");
        }
    }
}
