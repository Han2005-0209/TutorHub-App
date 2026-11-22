using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChuanHoaPhuongThucThanhToanVeViNoiBo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE [ThanhToan]
                SET [PhuongThuc] = N'VI_NOI_BO',
                    [MaGiaoDichCong] = NULL
                WHERE [PhuongThuc] IN (N'VNPAY', N'GIA_LAP');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
