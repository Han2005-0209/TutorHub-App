using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Dot3_KhieuNaiHaiChieuVaPhanHoiGiaSu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BangChungGiaSuUrl",
                table: "KhieuNai",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhanHoiGiaSu",
                table: "KhieuNai",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhanHoiGiaSuLuc",
                table: "KhieuNai",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_KhieuNai_PhanHoiGiaSu",
                table: "KhieuNai",
                sql: "([PhanHoiGiaSu] IS NULL AND [PhanHoiGiaSuLuc] IS NULL AND [BangChungGiaSuUrl] IS NULL) OR ([PhanHoiGiaSu] IS NOT NULL AND LTRIM(RTRIM([PhanHoiGiaSu])) <> '' AND [PhanHoiGiaSuLuc] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_KhieuNai_PhanHoiGiaSu",
                table: "KhieuNai");

            migrationBuilder.DropColumn(
                name: "BangChungGiaSuUrl",
                table: "KhieuNai");

            migrationBuilder.DropColumn(
                name: "PhanHoiGiaSu",
                table: "KhieuNai");

            migrationBuilder.DropColumn(
                name: "PhanHoiGiaSuLuc",
                table: "KhieuNai");
        }
    }
}
