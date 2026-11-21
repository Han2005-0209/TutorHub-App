using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutorHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Dot1_DonDepSchemaVaChuanHoaDuLieu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KhieuNai_NguoiDungThamGia_NguoiKhieuNaiId",
                table: "KhieuNai");

            migrationBuilder.DropIndex(
                name: "IX_ThanhToan_DonDatLichId",
                table: "ThanhToan");

            migrationBuilder.DropIndex(
                name: "IX_ThanhToan_MaGiaoDichCong",
                table: "ThanhToan");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToan");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaiKhoanNganHang",
                table: "TaiKhoanNganHang");

            migrationBuilder.DropIndex(
                name: "IX_TaiKhoanNganHang_GiaSuId",
                table: "TaiKhoanNganHang");

            migrationBuilder.DropIndex(
                name: "IX_KhieuNai_NguoiKhieuNaiId",
                table: "KhieuNai");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaSu_SoNamKinhNghiem",
                table: "GiaSu");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DonDatLich_ThoiGianVaTien",
                table: "DonDatLich");

            migrationBuilder.DropColumn(
                name: "CachGiaiQuyet",
                table: "KhieuNai");

            migrationBuilder.DropColumn(
                name: "NguoiKhieuNaiId",
                table: "KhieuNai");

            migrationBuilder.DropColumn(
                name: "SoTienHoan",
                table: "KhieuNai");

            // Đổi GiaSu.GiaHocThu -> CoHocThu theo đúng thứ tự: ADD NULL -> BACKFILL -> ALTER NOT NULL -> DROP cột cũ
            migrationBuilder.AddColumn<bool>(
                name: "CoHocThu",
                table: "GiaSu",
                type: "bit",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE GiaSu SET CoHocThu = CASE WHEN GiaHocThu IS NOT NULL AND DayTrucTuyen = 1 THEN 1 ELSE 0 END;");

            migrationBuilder.AlterColumn<bool>(
                name: "CoHocThu",
                table: "GiaSu",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "GiaHocThu",
                table: "GiaSu");

            migrationBuilder.DropColumn(
                name: "SoLanDoiLich",
                table: "DonDatLich");

            migrationBuilder.DropColumn(
                name: "BiAn",
                table: "DanhGia");

            migrationBuilder.AlterColumn<string>(
                name: "MaGiaoDichCong",
                table: "ThanhToan",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            // Chuẩn hóa dữ liệu cũ theo điều kiện (KHÔNG dùng Guid cứng) trước khi siết CHECK/UNIQUE
            migrationBuilder.Sql(
                """
                -- 1) Đơn học thử Online cũ có giá > 0 (khớp BK-20261130-1708): chuyển về buổi thường, GIỮ NGUYÊN GiaMoiGio
                UPDATE DonDatLich
                SET LaBuoiHocThu = 0
                WHERE LaBuoiHocThu = 1 AND HinhThuc = N'TRUC_TUYEN' AND GiaMoiGio > 0;

                -- 2) Đơn học thử Trực tiếp đã hủy cũ không có bản ghi con (khớp BK-20261123-2975): xóa sạch
                DELETE FROM DonDatLich
                WHERE LaBuoiHocThu = 1
                  AND HinhThuc = N'TRUC_TIEP'
                  AND TrangThai = N'DA_HUY'
                  AND NOT EXISTS (SELECT 1 FROM ThanhToan t WHERE t.DonDatLichId = DonDatLich.Id)
                  AND NOT EXISTS (SELECT 1 FROM KhieuNai k WHERE k.DonDatLichId = DonDatLich.Id)
                  AND NOT EXISTS (SELECT 1 FROM DanhGia dg WHERE dg.DonDatLichId = DonDatLich.Id);

                -- 3) ThanhToan khác VNPAY không lưu mã cổng giả
                UPDATE ThanhToan
                SET MaGiaoDichCong = NULL
                WHERE PhuongThuc <> N'VNPAY' AND MaGiaoDichCong IS NOT NULL;

                -- 4) GiaoDichVi chi tiền (THANH_TOAN, RUT_TIEN) mang dấu âm
                UPDATE GiaoDichVi
                SET SoTien = -ABS(SoTien)
                WHERE Loai IN (N'THANH_TOAN', N'RUT_TIEN') AND SoTien > 0;
                """);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaiKhoanNganHang",
                table: "TaiKhoanNganHang",
                column: "GiaSuId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_YeuCauRutTien_TrangThai_XuLy",
                table: "YeuCauRutTien",
                sql: "([TrangThai] = 'CHO_XU_LY' AND [XuLyLuc] IS NULL AND [NguoiXuLyId] IS NULL) OR ([TrangThai] IN ('DA_CHI_TRA', 'TU_CHOI') AND [XuLyLuc] IS NOT NULL AND [NguoiXuLyId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_YeuCauRutTien_TuChoi_GhiChu",
                table: "YeuCauRutTien",
                sql: "[TrangThai] <> 'TU_CHOI' OR ([GhiChu] IS NOT NULL AND LTRIM(RTRIM([GhiChu])) <> '')");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_DonDatLichId",
                table: "ThanhToan",
                column: "DonDatLichId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_MaGiaoDichCong",
                table: "ThanhToan",
                column: "MaGiaoDichCong",
                unique: true,
                filter: "[MaGiaoDichCong] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToan",
                sql: "[SoTien] > 0 AND [TienDaHoan] >= 0 AND [TienDaHoan] <= [SoTien]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaGiamGia_PhanTram",
                table: "MaGiamGia",
                sql: "[Loai] <> 'PHAN_TRAM' OR ([GiaTri] > 0 AND [GiaTri] <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_KhieuNai_TrangThai_GiaiQuyet",
                table: "KhieuNai",
                sql: "([TrangThai] = 'MO' AND [GiaiQuyetLuc] IS NULL AND [NguoiGiaiQuyetId] IS NULL) OR ([TrangThai] IN ('DA_GIAI_QUYET', 'TU_CHOI') AND [GiaiQuyetLuc] IS NOT NULL AND [NguoiGiaiQuyetId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiayToGiaSu_TrangThai_Duyet",
                table: "GiayToGiaSu",
                sql: "([TrangThai] = 'CHO_DUYET' AND [DuyetLuc] IS NULL AND [NguoiDuyetId] IS NULL) OR ([TrangThai] IN ('DA_DUYET', 'TU_CHOI') AND [DuyetLuc] IS NOT NULL AND [NguoiDuyetId] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaSu_BanKinhPhucVuKm",
                table: "GiaSu",
                sql: "[BanKinhPhucVuKm] BETWEEN 0 AND 200");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaSu_CoHocThu_TrucTuyen",
                table: "GiaSu",
                sql: "[CoHocThu] = 0 OR [DayTrucTuyen] = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaSu_SoNamKinhNghiem",
                table: "GiaSu",
                sql: "[SoNamKinhNghiem] BETWEEN 0 AND 60");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaoDichVi_DauTheoLoai",
                table: "GiaoDichVi",
                sql: "([Loai] IN ('THANH_TOAN', 'RUT_TIEN') AND [SoTien] < 0) OR ([Loai] IN ('NAP_TIEN', 'THU_NHAP', 'HOAN_TIEN') AND [SoTien] > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaoDichVi_RutTien_FK",
                table: "GiaoDichVi",
                sql: "[Loai] <> 'RUT_TIEN' OR [YeuCauRutTienId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaoDichVi_SoTien_SoDuSau",
                table: "GiaoDichVi",
                sql: "[SoTien] <> 0 AND [SoDuSau] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaoDichVi_ThanhToan_FK",
                table: "GiaoDichVi",
                sql: "[Loai] NOT IN ('THU_NHAP', 'THANH_TOAN', 'HOAN_TIEN') OR [ThanhToanId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DonDatLich_HocThu",
                table: "DonDatLich",
                sql: "[LaBuoiHocThu] = 0 OR ([HinhThuc] = 'TRUC_TUYEN' AND [GiaMoiGio] = 0 AND [TienGiam] = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DonDatLich_MaGiamGia",
                table: "DonDatLich",
                sql: "[MaGiamGiaId] IS NOT NULL OR [TienGiam] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DonDatLich_ThoiGianVaTien",
                table: "DonDatLich",
                sql: "[KetThucLuc] > [BatDauLuc] AND [GiaMoiGio] >= 0 AND [TienGiam] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DonDatLich_TrucTiep_DiaChi",
                table: "DonDatLich",
                sql: "[HinhThuc] <> 'TRUC_TIEP' OR ([DiaChiHocSinh] IS NOT NULL AND LTRIM(RTRIM([DiaChiHocSinh])) <> '')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DanhGia_An_KiemDuyet",
                table: "DanhGia",
                sql: "([LyDoAn] IS NULL AND [NguoiKiemDuyetId] IS NULL) OR ([LyDoAn] IS NOT NULL AND LTRIM(RTRIM([LyDoAn])) <> '' AND [NguoiKiemDuyetId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_YeuCauRutTien_TrangThai_XuLy",
                table: "YeuCauRutTien");

            migrationBuilder.DropCheckConstraint(
                name: "CK_YeuCauRutTien_TuChoi_GhiChu",
                table: "YeuCauRutTien");

            migrationBuilder.DropIndex(
                name: "IX_ThanhToan_DonDatLichId",
                table: "ThanhToan");

            migrationBuilder.DropIndex(
                name: "IX_ThanhToan_MaGiaoDichCong",
                table: "ThanhToan");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToan");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaiKhoanNganHang",
                table: "TaiKhoanNganHang");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaGiamGia_PhanTram",
                table: "MaGiamGia");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KhieuNai_TrangThai_GiaiQuyet",
                table: "KhieuNai");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiayToGiaSu_TrangThai_Duyet",
                table: "GiayToGiaSu");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaSu_BanKinhPhucVuKm",
                table: "GiaSu");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaSu_CoHocThu_TrucTuyen",
                table: "GiaSu");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaSu_SoNamKinhNghiem",
                table: "GiaSu");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaoDichVi_DauTheoLoai",
                table: "GiaoDichVi");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaoDichVi_RutTien_FK",
                table: "GiaoDichVi");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaoDichVi_SoTien_SoDuSau",
                table: "GiaoDichVi");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GiaoDichVi_ThanhToan_FK",
                table: "GiaoDichVi");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DonDatLich_HocThu",
                table: "DonDatLich");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DonDatLich_MaGiamGia",
                table: "DonDatLich");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DonDatLich_ThoiGianVaTien",
                table: "DonDatLich");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DonDatLich_TrucTiep_DiaChi",
                table: "DonDatLich");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DanhGia_An_KiemDuyet",
                table: "DanhGia");

            migrationBuilder.AddColumn<int>(
                name: "GiaHocThu",
                table: "GiaSu",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE GiaSu SET GiaHocThu = CASE WHEN CoHocThu = 1 THEN 0 ELSE NULL END;");

            migrationBuilder.DropColumn(
                name: "CoHocThu",
                table: "GiaSu");

            migrationBuilder.Sql(
                "UPDATE ThanhToan SET MaGiaoDichCong = CONCAT(N'ROLLBACK-', CONVERT(nvarchar(36), Id)) WHERE MaGiaoDichCong IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "MaGiaoDichCong",
                table: "ThanhToan",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CachGiaiQuyet",
                table: "KhieuNai",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NguoiKhieuNaiId",
                table: "KhieuNai",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE k SET k.NguoiKhieuNaiId = d.HocSinhId FROM KhieuNai k INNER JOIN DonDatLich d ON d.Id = k.DonDatLichId;");

            migrationBuilder.AlterColumn<Guid>(
                name: "NguoiKhieuNaiId",
                table: "KhieuNai",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoTienHoan",
                table: "KhieuNai",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SoLanDoiLich",
                table: "DonDatLich",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "BiAn",
                table: "DanhGia",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE DanhGia SET BiAn = CASE WHEN NguoiKiemDuyetId IS NOT NULL THEN 1 ELSE 0 END;");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaiKhoanNganHang",
                table: "TaiKhoanNganHang",
                columns: new[] { "TenNganHang", "SoTaiKhoan" });

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_DonDatLichId",
                table: "ThanhToan",
                column: "DonDatLichId");

            migrationBuilder.CreateIndex(
                name: "IX_ThanhToan_MaGiaoDichCong",
                table: "ThanhToan",
                column: "MaGiaoDichCong",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ThanhToan_SoTien",
                table: "ThanhToan",
                sql: "[SoTien] >= 0 AND [TienDaHoan] >= 0 AND [TienDaHoan] <= [SoTien]");

            migrationBuilder.CreateIndex(
                name: "IX_TaiKhoanNganHang_GiaSuId",
                table: "TaiKhoanNganHang",
                column: "GiaSuId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KhieuNai_NguoiKhieuNaiId",
                table: "KhieuNai",
                column: "NguoiKhieuNaiId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GiaSu_SoNamKinhNghiem",
                table: "GiaSu",
                sql: "[SoNamKinhNghiem] >= 0 AND [BanKinhPhucVuKm] >= 0 AND ([GiaHocThu] IS NULL OR [GiaHocThu] >= 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DonDatLich_ThoiGianVaTien",
                table: "DonDatLich",
                sql: "[KetThucLuc] > [BatDauLuc] AND [GiaMoiGio] >= 0 AND [TienGiam] >= 0 AND [SoLanDoiLich] BETWEEN 0 AND 2");

            migrationBuilder.AddForeignKey(
                name: "FK_KhieuNai_NguoiDungThamGia_NguoiKhieuNaiId",
                table: "KhieuNai",
                column: "NguoiKhieuNaiId",
                principalTable: "NguoiDungThamGia",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
