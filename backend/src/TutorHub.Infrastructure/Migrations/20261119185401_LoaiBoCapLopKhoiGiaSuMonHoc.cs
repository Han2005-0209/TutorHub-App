using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoaiBoCapLopKhoiGiaSuMonHoc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GiaSuMonHoc_GiaSuId_MonHocId_CapLop",
                table: "GiaSuMonHoc");

            migrationBuilder.DropColumn(
                name: "CapLop",
                table: "GiaSuMonHoc");

            migrationBuilder.Sql(@"
                WITH Dups AS (
                    SELECT Id, ROW_NUMBER() OVER(PARTITION BY GiaSuId, MonHocId ORDER BY GiaMoiGio DESC) as rn
                    FROM GiaSuMonHoc
                )
                DELETE FROM GiaSuMonHoc WHERE Id IN (SELECT Id FROM Dups WHERE rn > 1);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_GiaSuMonHoc_GiaSuId_MonHocId",
                table: "GiaSuMonHoc",
                columns: new[] { "GiaSuId", "MonHocId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GiaSuMonHoc_GiaSuId_MonHocId",
                table: "GiaSuMonHoc");

            migrationBuilder.AddColumn<string>(
                name: "CapLop",
                table: "GiaSuMonHoc",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_GiaSuMonHoc_GiaSuId_MonHocId_CapLop",
                table: "GiaSuMonHoc",
                columns: new[] { "GiaSuId", "MonHocId", "CapLop" },
                unique: true);
        }
    }
}
