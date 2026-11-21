using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Dot4_NhatKyPhongHocTrucTuyen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NhatKyPhongHoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DonDatLichId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NguoiDungId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaiTroLucVao = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VaoLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapNhatCuoiLuc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DaRoiPhong = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhatKyPhongHoc", x => x.Id);
                    table.CheckConstraint("CK_NhatKyPhongHoc_ThoiGian", "[CapNhatCuoiLuc] >= [VaoLuc]");
                    table.ForeignKey(
                        name: "FK_NhatKyPhongHoc_DonDatLich_DonDatLichId",
                        column: x => x.DonDatLichId,
                        principalTable: "DonDatLich",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NhatKyPhongHoc_NguoiDung_NguoiDungId",
                        column: x => x.NguoiDungId,
                        principalTable: "NguoiDung",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyPhongHoc_DonDatLichId_NguoiDungId_VaoLuc",
                table: "NhatKyPhongHoc",
                columns: new[] { "DonDatLichId", "NguoiDungId", "VaoLuc" });

            migrationBuilder.CreateIndex(
                name: "IX_NhatKyPhongHoc_NguoiDungId",
                table: "NhatKyPhongHoc",
                column: "NguoiDungId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhatKyPhongHoc");
        }
    }
}
