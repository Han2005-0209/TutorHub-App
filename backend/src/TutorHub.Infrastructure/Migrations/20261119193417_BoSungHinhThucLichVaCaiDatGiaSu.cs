using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BoSungHinhThucLichVaCaiDatGiaSu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LichNgayCuThe_ThoiGian",
                table: "LichNgayCuThe");

            migrationBuilder.AddColumn<string>(
                name: "LyDo",
                table: "LichNgayCuThe",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NgayKetThuc",
                table: "LichNgayCuThe",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HinhThucLich",
                table: "LichLapHangTuan",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ThoiGianDemPhut",
                table: "GiaSu",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "ThoiLuongToiThieuTaiNhaPhut",
                table: "GiaSu",
                type: "int",
                nullable: false,
                defaultValue: 90);

            migrationBuilder.AddColumn<string>(
                name: "DiaChiHocSinh",
                table: "DonDatLich",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LichNgayCuThe_ThoiGian",
                table: "LichNgayCuThe",
                sql: "(([GioBatDau] IS NULL AND [GioKetThuc] IS NULL) OR ([GioBatDau] IS NOT NULL AND [GioKetThuc] IS NOT NULL AND [GioKetThuc] > [GioBatDau])) AND ([NgayKetThuc] IS NULL OR [NgayKetThuc] >= [NgayCuThe])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LichNgayCuThe_ThoiGian",
                table: "LichNgayCuThe");

            migrationBuilder.DropColumn(
                name: "LyDo",
                table: "LichNgayCuThe");

            migrationBuilder.DropColumn(
                name: "NgayKetThuc",
                table: "LichNgayCuThe");

            migrationBuilder.DropColumn(
                name: "HinhThucLich",
                table: "LichLapHangTuan");

            migrationBuilder.DropColumn(
                name: "ThoiGianDemPhut",
                table: "GiaSu");

            migrationBuilder.DropColumn(
                name: "ThoiLuongToiThieuTaiNhaPhut",
                table: "GiaSu");

            migrationBuilder.DropColumn(
                name: "DiaChiHocSinh",
                table: "DonDatLich");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LichNgayCuThe_ThoiGian",
                table: "LichNgayCuThe",
                sql: "([GioBatDau] IS NULL AND [GioKetThuc] IS NULL) OR ([GioBatDau] IS NOT NULL AND [GioKetThuc] IS NOT NULL AND [GioKetThuc] > [GioBatDau])");
        }
    }
}
