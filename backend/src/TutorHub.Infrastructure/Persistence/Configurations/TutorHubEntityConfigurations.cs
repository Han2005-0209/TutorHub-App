using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TutorHub.Domain.Entities;

namespace TutorHub.Infrastructure.Persistence.Configurations;

// ══════════════════════════════════════════════════════════════════════════════
// 1. CÂY KẾ THỪA TPT: NguoiDung → NguoiDungThamGia → HocSinh / GiaSu & QuanTriVien
// ══════════════════════════════════════════════════════════════════════════════

public sealed class NguoiDungConfiguration : IEntityTypeConfiguration<NguoiDung>
{
    public void Configure(EntityTypeBuilder<NguoiDung> builder)
    {
        builder.ToTable("NguoiDung");
        builder.UseTptMappingStrategy();

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(255);
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.MatKhauHash).IsRequired().HasMaxLength(255);
        builder.Property(x => x.HoTen).IsRequired().HasMaxLength(150);
        builder.Property(x => x.SoDienThoai).HasMaxLength(30);
        builder.Property(x => x.AnhDaiDienUrl).HasMaxLength(500);
        builder.Property(x => x.KhuVuc).HasMaxLength(200);
        builder.Property(x => x.TrangThai).IsRequired();
        builder.Property(x => x.TaoLuc).IsRequired();

        // Thuộc tính dẫn xuất # /vaiTro KHÔNG tạo cột vật lý
        builder.Ignore(x => x.VaiTro);
    }
}

public sealed class NguoiDungThamGiaConfiguration : IEntityTypeConfiguration<NguoiDungThamGia>
{
    public void Configure(EntityTypeBuilder<NguoiDungThamGia> builder)
    {
        builder.ToTable("NguoiDungThamGia");

        // Composition #1: NguoiDungThamGia (1) ◆—> (1) TaiKhoanVi => DeleteBehavior.Cascade
        builder.HasOne(x => x.TaiKhoanVi)
            .WithOne(v => v.NguoiDungThamGia)
            .HasForeignKey<TaiKhoanVi>(v => v.NguoiDungThamGiaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuanTriVienConfiguration : IEntityTypeConfiguration<QuanTriVien>
{
    public void Configure(EntityTypeBuilder<QuanTriVien> builder)
    {
        builder.ToTable("QuanTriVien");
    }
}

public sealed class HocSinhConfiguration : IEntityTypeConfiguration<HocSinh>
{
    public void Configure(EntityTypeBuilder<HocSinh> builder)
    {
        builder.ToTable("HocSinh");
    }
}

public sealed class GiaSuConfiguration : IEntityTypeConfiguration<GiaSu>
{
    public void Configure(EntityTypeBuilder<GiaSu> builder)
    {
        builder.ToTable("GiaSu", t =>
        {
            t.HasCheckConstraint(
                "CK_GiaSu_HinhThucDay",
                "[DayTrucTuyen] = 1 OR [DayTrucTiep] = 1");
            t.HasCheckConstraint(
                "CK_GiaSu_SoNamKinhNghiem",
                "[SoNamKinhNghiem] BETWEEN 0 AND 60");
            t.HasCheckConstraint(
                "CK_GiaSu_BanKinhPhucVuKm",
                "[BanKinhPhucVuKm] BETWEEN 0 AND 200");
            t.HasCheckConstraint(
                "CK_GiaSu_CoHocThu_TrucTuyen",
                "[CoHocThu] = 0 OR [DayTrucTuyen] = 1");
        });

        builder.Property(x => x.TieuDe).IsRequired().HasMaxLength(250);
        builder.Property(x => x.GioiThieu).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.HocVan).IsRequired().HasMaxLength(500);
        builder.Property(x => x.LyDoTuChoi).HasMaxLength(1000);
        builder.Property(x => x.TrangThaiHoSo).IsRequired();
        builder.Property(x => x.CoHocThu).HasDefaultValue(false);
        builder.Property(x => x.ThoiGianDemPhut).HasDefaultValue(30);
        builder.Property(x => x.ThoiLuongToiThieuTaiNhaPhut).HasDefaultValue(90);

        // Các thuộc tính dẫn xuất (/) trên GiaSu KHÔNG tạo cột vật lý
        builder.Ignore(x => x.HinhThucDay);
        builder.Ignore(x => x.DaXacMinh);
        builder.Ignore(x => x.DsDonDatLich);
        builder.Ignore(x => x.SoBuoiHoanThanh);
        builder.Ignore(x => x.SoLuotDanhGia);
        builder.Ignore(x => x.DiemDanhGiaTrungBinh);

        // Association #24: GiaSu (0..*) ×—> (0..1) QuanTriVien (+nguoiDuyet) => Restrict
        builder.HasOne(x => x.NguoiDuyet)
            .WithMany()
            .HasForeignKey(x => x.NguoiDuyetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composition #4: GiaSu (1) ◆—— (0..*) GiaSuMonHoc => Cascade
        builder.HasMany(x => x.DsMonDay)
            .WithOne(m => m.GiaSu)
            .HasForeignKey(m => m.GiaSuId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composition #5: GiaSu (1) ◆—> (0..*) LichRanh => Cascade
        builder.HasMany(x => x.DsLichRanh)
            .WithOne(l => l.GiaSu)
            .HasForeignKey(l => l.GiaSuId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composition #6: GiaSu (1) ◆—> (0..*) GiayToGiaSu => Cascade
        builder.HasMany(x => x.DsGiayTo)
            .WithOne(g => g.GiaSu)
            .HasForeignKey(g => g.GiaSuId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composition #7: GiaSu (1) ◆—> (0..1) TaiKhoanNganHang => Cascade
        builder.HasOne(x => x.TaiKhoanNganHang)
            .WithOne(t => t.GiaSu)
            .HasForeignKey<TaiKhoanNganHang>(t => t.GiaSuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// 2. HỒ SƠ GIA SƯ, MÔN HỌC, LỊCH RẢNH (TPT) & MỤC YÊU THÍCH
// ══════════════════════════════════════════════════════════════════════════════

public sealed class TaiKhoanNganHangConfiguration : IEntityTypeConfiguration<TaiKhoanNganHang>
{
    public void Configure(EntityTypeBuilder<TaiKhoanNganHang> builder)
    {
        builder.ToTable("TaiKhoanNganHang");

        // Mỗi gia sư sở hữu tối đa 1 tài khoản ngân hàng hiện hành => PK trực tiếp trên GiaSuId
        builder.HasKey(x => x.GiaSuId);
        builder.Property(x => x.TenNganHang).IsRequired().HasMaxLength(120);
        builder.Property(x => x.SoTaiKhoan).IsRequired().HasMaxLength(60);
        builder.Property(x => x.ChuTaiKhoan).IsRequired().HasMaxLength(150);
    }
}

public sealed class GiayToGiaSuConfiguration : IEntityTypeConfiguration<GiayToGiaSu>
{
    public void Configure(EntityTypeBuilder<GiayToGiaSu> builder)
    {
        builder.ToTable("GiayToGiaSu", t =>
        {
            t.HasCheckConstraint(
                "CK_GiayToGiaSu_TrangThai_Duyet",
                "([TrangThai] = 'CHO_DUYET' AND [DuyetLuc] IS NULL AND [NguoiDuyetId] IS NULL) OR ([TrangThai] IN ('DA_DUYET', 'TU_CHOI') AND [DuyetLuc] IS NOT NULL AND [NguoiDuyetId] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DuongDanTep).IsRequired().HasMaxLength(500);
        builder.Property(x => x.TenTepGoc).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Loai).IsRequired();
        builder.Property(x => x.TrangThai).IsRequired();

        // Association #25: GiayToGiaSu (0..*) ×—> (0..1) QuanTriVien (+nguoiDuyet) => Restrict
        builder.HasOne(x => x.NguoiDuyet)
            .WithMany()
            .HasForeignKey(x => x.NguoiDuyetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MonHocConfiguration : IEntityTypeConfiguration<MonHoc>
{
    public void Configure(EntityTypeBuilder<MonHoc> builder)
    {
        builder.ToTable("MonHoc");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Ten).IsRequired().HasMaxLength(120);
        builder.HasIndex(x => x.Ten).IsUnique();

        // Thuộc tính dẫn xuất - /slug KHÔNG tạo cột vật lý
        builder.Ignore(x => x.Slug);
    }
}

public sealed class GiaSuMonHocConfiguration : IEntityTypeConfiguration<GiaSuMonHoc>
{
    public void Configure(EntityTypeBuilder<GiaSuMonHoc> builder)
    {
        builder.ToTable("GiaSuMonHoc", t =>
        {
            t.HasCheckConstraint("CK_GiaSuMonHoc_GiaMoiGio", "[GiaMoiGio] > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.GiaMoiGio).IsRequired();

        // UNIQUE(GiaSuId, MonHocId): Mỗi gia sư chỉ có 1 mức giá cho 1 môn học
        builder.HasIndex(x => new { x.GiaSuId, x.MonHocId }).IsUnique();

        // Association #22: GiaSuMonHoc (0..*) ×—> (1) MonHoc => Restrict
        builder.HasOne(x => x.MonHoc)
            .WithMany(m => m.DsGiaSuMonHoc)
            .HasForeignKey(x => x.MonHocId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LichRanhConfiguration : IEntityTypeConfiguration<LichRanh>
{
    public void Configure(EntityTypeBuilder<LichRanh> builder)
    {
        builder.ToTable("LichRanh");
        builder.UseTptMappingStrategy();
        builder.HasKey(x => x.Id);
    }
}

public sealed class LichLapHangTuanConfiguration : IEntityTypeConfiguration<LichLapHangTuan>
{
    public void Configure(EntityTypeBuilder<LichLapHangTuan> builder)
    {
        builder.ToTable("LichLapHangTuan", t =>
        {
            t.HasCheckConstraint(
                "CK_LichLapHangTuan_ThoiGian",
                "[ThuTrongTuan] BETWEEN 1 AND 7 AND [GioKetThuc] > [GioBatDau]");
        });

        builder.Property(x => x.GioBatDau).HasColumnType("time(0)");
        builder.Property(x => x.GioKetThuc).HasColumnType("time(0)");
        builder.Property(x => x.HinhThucLich).IsRequired().HasMaxLength(30);
    }
}

public sealed class LichNgayCuTheConfiguration : IEntityTypeConfiguration<LichNgayCuThe>
{
    public void Configure(EntityTypeBuilder<LichNgayCuThe> builder)
    {
        builder.ToTable("LichNgayCuThe", t =>
        {
            t.HasCheckConstraint(
                "CK_LichNgayCuThe_ThoiGian",
                "(([GioBatDau] IS NULL AND [GioKetThuc] IS NULL) OR ([GioBatDau] IS NOT NULL AND [GioKetThuc] IS NOT NULL AND [GioKetThuc] > [GioBatDau])) AND ([NgayKetThuc] IS NULL OR [NgayKetThuc] >= [NgayCuThe])");
        });

        builder.Property(x => x.NgayCuThe).HasColumnType("date");
        builder.Property(x => x.NgayKetThuc).HasColumnType("date");
        builder.Property(x => x.GioBatDau).HasColumnType("time(0)");
        builder.Property(x => x.GioKetThuc).HasColumnType("time(0)");
        builder.Property(x => x.Loai).IsRequired();
        builder.Property(x => x.LyDo).HasMaxLength(200);
    }
}

public sealed class MucYeuThichConfiguration : IEntityTypeConfiguration<MucYeuThich>
{
    public void Configure(EntityTypeBuilder<MucYeuThich> builder)
    {
        builder.ToTable("MucYeuThich");
        builder.HasKey(x => new { x.HocSinhId, x.GiaSuId });

        // Association #13: MucYeuThich (HocSinh — GiaSu) => Restrict để tránh multiple cascade paths
        builder.HasOne(x => x.HocSinh)
            .WithMany(h => h.DsMucYeuThich)
            .HasForeignKey(x => x.HocSinhId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GiaSu)
            .WithMany(g => g.DsMucYeuThich)
            .HasForeignKey(x => x.GiaSuId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// 3. ĐƠN ĐẶT LỊCH, MÃ GIẢM GIÁ, THANH TOÁN, VÍ, RÚT TIỀN, ĐÁNH GIÁ & KHIẾU NẠI
// ══════════════════════════════════════════════════════════════════════════════

public sealed class DonDatLichConfiguration : IEntityTypeConfiguration<DonDatLich>
{
    public void Configure(EntityTypeBuilder<DonDatLich> builder)
    {
        builder.ToTable("DonDatLich", t =>
        {
            t.HasCheckConstraint(
                "CK_DonDatLich_ThoiGianVaTien",
                "[KetThucLuc] > [BatDauLuc] AND [GiaMoiGio] >= 0 AND [TienGiam] >= 0");
            t.HasCheckConstraint(
                "CK_DonDatLich_HocThu",
                "[LaBuoiHocThu] = 0 OR ([HinhThuc] = 'TRUC_TUYEN' AND [GiaMoiGio] = 0 AND [TienGiam] = 0)");
            t.HasCheckConstraint(
                "CK_DonDatLich_MaGiamGia",
                "[MaGiamGiaId] IS NOT NULL OR [TienGiam] = 0");
            t.HasCheckConstraint(
                "CK_DonDatLich_TrucTiep_DiaChi",
                "[HinhThuc] <> 'TRUC_TIEP' OR ([DiaChiHocSinh] IS NOT NULL AND LTRIM(RTRIM([DiaChiHocSinh])) <> '')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaDon).IsRequired().HasMaxLength(40);
        builder.HasIndex(x => x.MaDon).IsUnique();

        builder.Property(x => x.PhongHocUrl).HasMaxLength(500);
        builder.Property(x => x.LyDoHuy).HasMaxLength(1000);
        builder.Property(x => x.DiaChiHocSinh).HasMaxLength(300);

        // Các thuộc tính dẫn xuất (/) trên DonDatLich KHÔNG tạo cột vật lý
        builder.Ignore(x => x.TienGoc);
        builder.Ignore(x => x.TongTien);

        // Association #16: DonDatLich (0..*) ×—> (1) HocSinh => Restrict
        builder.HasOne(x => x.HocSinh)
            .WithMany(h => h.DsDonDatLich)
            .HasForeignKey(x => x.HocSinhId)
            .OnDelete(DeleteBehavior.Restrict);

        // Association #17: DonDatLich (0..*) ×—> (1) GiaSuMonHoc => Restrict
        builder.HasOne(x => x.GiaSuMonHoc)
            .WithMany(m => m.DsDonDatLich)
            .HasForeignKey(x => x.GiaSuMonHocId)
            .OnDelete(DeleteBehavior.Restrict);

        // Association #18: DonDatLich (0..*) ×—> (0..1) MaGiamGia => Restrict
        builder.HasOne(x => x.MaGiamGia)
            .WithMany(m => m.DsDonDatLich)
            .HasForeignKey(x => x.MaGiamGiaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.GiaSuMonHocId, x.BatDauLuc, x.KetThucLuc });
        builder.HasIndex(x => new { x.HocSinhId, x.BatDauLuc, x.KetThucLuc });
    }
}

public sealed class MaGiamGiaConfiguration : IEntityTypeConfiguration<MaGiamGia>
{
    public void Configure(EntityTypeBuilder<MaGiamGia> builder)
    {
        builder.ToTable("MaGiamGia", t =>
        {
            t.HasCheckConstraint(
                "CK_MaGiamGia_GiaTri",
                "[GiaTri] > 0 AND [GiaTriDonToiThieu] >= 0 AND [SoLuotToiDa] > 0 AND [HetHanLuc] > [BatDauLuc]");
            t.HasCheckConstraint(
                "CK_MaGiamGia_PhanTram",
                "[Loai] <> 'PHAN_TRAM' OR ([GiaTri] > 0 AND [GiaTri] <= 100)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Ma).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.Ma).IsUnique();

        // Thuộc tính dẫn xuất - /soLanSuDung KHÔNG tạo cột vật lý
        builder.Ignore(x => x.SoLanSuDung);
    }
}

public sealed class ThanhToanConfiguration : IEntityTypeConfiguration<ThanhToan>
{
    public void Configure(EntityTypeBuilder<ThanhToan> builder)
    {
        builder.ToTable("ThanhToan", t =>
        {
            t.HasCheckConstraint(
                "CK_ThanhToan_SoTien",
                "[SoTien] > 0 AND [TienDaHoan] >= 0 AND [TienDaHoan] <= [SoTien]");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.MaGiaoDichCong).HasMaxLength(100);
        builder.HasIndex(x => x.DonDatLichId).IsUnique();
        builder.HasIndex(x => x.MaGiaoDichCong)
            .IsUnique()
            .HasFilter("[MaGiaoDichCong] IS NOT NULL");

        // Association #19: ThanhToan (0..1) ×—> (1) DonDatLich => Restrict
        builder.HasOne(x => x.DonDatLich)
            .WithMany(d => d.DsThanhToan)
            .HasForeignKey(x => x.DonDatLichId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TaiKhoanViConfiguration : IEntityTypeConfiguration<TaiKhoanVi>
{
    public void Configure(EntityTypeBuilder<TaiKhoanVi> builder)
    {
        builder.ToTable("TaiKhoanVi", t =>
        {
            t.HasCheckConstraint(
                "CK_TaiKhoanVi_SoDu",
                "[SoDu] >= 0 AND [SoDuDangXuLyRut] >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.SoDuDangXuLyRut).HasDefaultValue(0);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.NguoiDungThamGiaId).IsUnique();

        // Composition #2: TaiKhoanVi (1) ◆—— (0..*) GiaoDichVi => Cascade
        builder.HasMany(x => x.DsGiaoDich)
            .WithOne(g => g.TaiKhoanVi)
            .HasForeignKey(g => g.TaiKhoanViId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GiaoDichViConfiguration : IEntityTypeConfiguration<GiaoDichVi>
{
    public void Configure(EntityTypeBuilder<GiaoDichVi> builder)
    {
        builder.ToTable("GiaoDichVi", t =>
        {
            // NOTE 402: Tối đa 1 trong 2 liên kết (ThanhToan, YeuCauRutTien); Loai = NAP_TIEN => cả hai rỗng
            t.HasCheckConstraint(
                "CK_GiaoDichVi_ThamChieu",
                "([ThanhToanId] IS NULL OR [YeuCauRutTienId] IS NULL) AND ([Loai] <> 'NAP_TIEN' OR ([ThanhToanId] IS NULL AND [YeuCauRutTienId] IS NULL))");
            t.HasCheckConstraint(
                "CK_GiaoDichVi_SoThuTu",
                "[SoThuTu] >= 1");
            t.HasCheckConstraint(
                "CK_GiaoDichVi_SoTien_SoDuSau",
                "[SoTien] <> 0 AND [SoDuSau] >= 0");
            t.HasCheckConstraint(
                "CK_GiaoDichVi_DauTheoLoai",
                "([Loai] IN ('THANH_TOAN', 'RUT_TIEN') AND [SoTien] < 0) OR ([Loai] IN ('NAP_TIEN', 'THU_NHAP', 'HOAN_TIEN') AND [SoTien] > 0)");
            t.HasCheckConstraint(
                "CK_GiaoDichVi_RutTien_FK",
                "[Loai] <> 'RUT_TIEN' OR [YeuCauRutTienId] IS NOT NULL");
            t.HasCheckConstraint(
                "CK_GiaoDichVi_ThanhToan_FK",
                "[Loai] NOT IN ('THU_NHAP', 'THANH_TOAN', 'HOAN_TIEN') OR [ThanhToanId] IS NOT NULL");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.SoThuTu).IsRequired();
        builder.Property(x => x.GhiChu).HasMaxLength(500);

        // Thứ tự chuỗi sổ cái tuần tự tuyệt đối theo từng ví
        builder.HasIndex(x => new { x.TaiKhoanViId, x.SoThuTu }).IsUnique();

        // Chống ghi trùng bút toán theo (ThanhToanId, Loai)
        builder.HasIndex(x => new { x.ThanhToanId, x.Loai })
            .IsUnique()
            .HasFilter("[ThanhToanId] IS NOT NULL");

        // Bản số 0..1 trên YeuCauRutTien
        builder.HasIndex(x => x.YeuCauRutTienId)
            .IsUnique()
            .HasFilter("[YeuCauRutTienId] IS NOT NULL");

        // Association #9: GiaoDichVi (0..*) ×—> (0..1) ThanhToan => Restrict
        builder.HasOne(x => x.ThanhToan)
            .WithMany()
            .HasForeignKey(x => x.ThanhToanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Association #10: GiaoDichVi (0..1) ×—> (0..1) YeuCauRutTien => Restrict
        builder.HasOne(x => x.YeuCauRutTien)
            .WithMany()
            .HasForeignKey(x => x.YeuCauRutTienId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class YeuCauRutTienConfiguration : IEntityTypeConfiguration<YeuCauRutTien>
{
    public void Configure(EntityTypeBuilder<YeuCauRutTien> builder)
    {
        builder.ToTable("YeuCauRutTien", t =>
        {
            t.HasCheckConstraint("CK_YeuCauRutTien_SoTien", "[SoTien] >= 100000");
            t.HasCheckConstraint(
                "CK_YeuCauRutTien_TrangThai_XuLy",
                "([TrangThai] = 'CHO_XU_LY' AND [XuLyLuc] IS NULL AND [NguoiXuLyId] IS NULL) OR ([TrangThai] IN ('DA_CHI_TRA', 'TU_CHOI') AND [XuLyLuc] IS NOT NULL AND [NguoiXuLyId] IS NOT NULL)");
            t.HasCheckConstraint(
                "CK_YeuCauRutTien_TuChoi_GhiChu",
                "[TrangThai] <> 'TU_CHOI' OR ([GhiChu] IS NOT NULL AND LTRIM(RTRIM([GhiChu])) <> '')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.GhiChu).HasMaxLength(500);
        builder.Property(x => x.TenNganHang).IsRequired().HasMaxLength(120);
        builder.Property(x => x.SoTaiKhoan).IsRequired().HasMaxLength(60);
        builder.Property(x => x.ChuTaiKhoan).IsRequired().HasMaxLength(150);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120);

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        // Association #23: YeuCauRutTien (0..*) ×—> (1) GiaSu => Restrict
        builder.HasOne(x => x.GiaSu)
            .WithMany(g => g.DsYeuCauRutTien)
            .HasForeignKey(x => x.GiaSuId)
            .OnDelete(DeleteBehavior.Restrict);

        // Association #26: YeuCauRutTien (0..*) ×—> (0..1) QuanTriVien (+nguoiXuLy) => Restrict
        builder.HasOne(x => x.NguoiXuLy)
            .WithMany()
            .HasForeignKey(x => x.NguoiXuLyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DanhGiaConfiguration : IEntityTypeConfiguration<DanhGia>
{
    public void Configure(EntityTypeBuilder<DanhGia> builder)
    {
        builder.ToTable("DanhGia", t =>
        {
            t.HasCheckConstraint("CK_DanhGia_Diem", "[Diem] BETWEEN 1 AND 5");
            t.HasCheckConstraint(
                "CK_DanhGia_An_KiemDuyet",
                "([LyDoAn] IS NULL AND [NguoiKiemDuyetId] IS NULL) OR ([LyDoAn] IS NOT NULL AND LTRIM(RTRIM([LyDoAn])) <> '' AND [NguoiKiemDuyetId] IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.BinhLuan).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.PhanHoiGiaSu).HasMaxLength(1000);
        builder.Property(x => x.LyDoAn).HasMaxLength(500);
        builder.Ignore(x => x.BiAn);

        // Mỗi DonDatLich có tối đa 0..1 DanhGia
        builder.HasIndex(x => x.DonDatLichId).IsUnique();

        // Association #21: DanhGia (0..1) ×—> (1) DonDatLich => Restrict
        builder.HasOne(x => x.DonDatLich)
            .WithOne(d => d.DanhGia)
            .HasForeignKey<DanhGia>(x => x.DonDatLichId)
            .OnDelete(DeleteBehavior.Restrict);

        // Association #28: DanhGia (0..*) ×—> (0..1) QuanTriVien (+nguoiKiemDuyet) => Restrict
        builder.HasOne(x => x.NguoiKiemDuyet)
            .WithMany()
            .HasForeignKey(x => x.NguoiKiemDuyetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class KhieuNaiConfiguration : IEntityTypeConfiguration<KhieuNai>
{
    public void Configure(EntityTypeBuilder<KhieuNai> builder)
    {
        builder.ToTable("KhieuNai", t =>
        {
            t.HasCheckConstraint(
                "CK_KhieuNai_TrangThai_GiaiQuyet",
                "([TrangThai] = 'MO' AND [GiaiQuyetLuc] IS NULL AND [NguoiGiaiQuyetId] IS NULL AND [GhiChuGiaiQuyet] IS NULL) OR ([TrangThai] IN ('DA_GIAI_QUYET', 'TU_CHOI') AND [GiaiQuyetLuc] IS NOT NULL AND [NguoiGiaiQuyetId] IS NOT NULL AND [GhiChuGiaiQuyet] IS NOT NULL AND LTRIM(RTRIM([GhiChuGiaiQuyet])) <> '')");
            t.HasCheckConstraint(
                "CK_KhieuNai_PhanHoiGiaSu",
                "([PhanHoiGiaSu] IS NULL AND [PhanHoiGiaSuLuc] IS NULL AND [BangChungGiaSuUrl] IS NULL) OR ([PhanHoiGiaSu] IS NOT NULL AND LTRIM(RTRIM([PhanHoiGiaSu])) <> '' AND [PhanHoiGiaSuLuc] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MoTa).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.BangChungUrl).HasMaxLength(500);
        builder.Property(x => x.PhanHoiGiaSu).HasMaxLength(2000);
        builder.Property(x => x.BangChungGiaSuUrl).HasMaxLength(500);
        builder.Property(x => x.GhiChuGiaiQuyet).HasMaxLength(1000);

        // Association #20: KhieuNai (0..*) ×—> (1) DonDatLich => Restrict
        builder.HasOne(x => x.DonDatLich)
            .WithMany(d => d.DsKhieuNai)
            .HasForeignKey(x => x.DonDatLichId)
            .OnDelete(DeleteBehavior.Restrict);

        // Association #27: KhieuNai (0..*) ×—> (0..1) QuanTriVien (+nguoiGiaiQuyet) => Restrict
        builder.HasOne(x => x.NguoiGiaiQuyet)
            .WithMany()
            .HasForeignKey(x => x.NguoiGiaiQuyetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HanhDong).IsRequired().HasMaxLength(80);
        builder.Property(x => x.DoiTuong).IsRequired().HasMaxLength(120);
        builder.Property(x => x.GiaTriCuJson).HasMaxLength(2000);
        builder.Property(x => x.GiaTriMoiJson).HasMaxLength(2000);
        builder.Property(x => x.DiaChiIp).HasMaxLength(64);
        builder.Property(x => x.TaoLuc).IsRequired();

        builder.HasIndex(x => new { x.NguoiDungId, x.TaoLuc });
    }
}

public sealed class NhatKyPhongHocConfiguration : IEntityTypeConfiguration<NhatKyPhongHoc>
{
    public void Configure(EntityTypeBuilder<NhatKyPhongHoc> builder)
    {
        builder.ToTable("NhatKyPhongHoc", t =>
        {
            t.HasCheckConstraint("CK_NhatKyPhongHoc_ThoiGian", "[CapNhatCuoiLuc] >= [VaoLuc]");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VaiTroLucVao).IsRequired();
        builder.Property(x => x.VaoLuc).IsRequired();
        builder.Property(x => x.CapNhatCuoiLuc).IsRequired();
        builder.Property(x => x.DaRoiPhong).HasDefaultValue(false);

        builder.HasOne(x => x.DonDatLich)
            .WithMany(d => d.DsNhatKyPhongHoc)
            .HasForeignKey(x => x.DonDatLichId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.NguoiDung)
            .WithMany()
            .HasForeignKey(x => x.NguoiDungId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.DonDatLichId, x.NguoiDungId, x.VaoLuc });
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// 4. HỘI THOẠI, TIN NHẮN & THÔNG BÁO
// ══════════════════════════════════════════════════════════════════════════════

public sealed class HoiThoaiConfiguration : IEntityTypeConfiguration<HoiThoai>
{
    public void Configure(EntityTypeBuilder<HoiThoai> builder)
    {
        builder.ToTable("HoiThoai", t =>
        {
            t.HasCheckConstraint("CK_HoiThoai_KhacNguoi", "[HocSinhId] <> [GiaSuId]");
        });

        builder.HasKey(x => x.Id);

        // NOTE 403: UNIQUE(HocSinhId, GiaSuId)
        builder.HasIndex(x => new { x.HocSinhId, x.GiaSuId }).IsUnique();

        // Thuộc tính dẫn xuất - /tinNhanCuoiLuc KHÔNG tạo cột vật lý
        builder.Ignore(x => x.TinNhanCuoiLuc);

        // Association #14: HoiThoai (0..*) ×—> (1) HocSinh => Restrict
        builder.HasOne(x => x.HocSinh)
            .WithMany()
            .HasForeignKey(x => x.HocSinhId)
            .OnDelete(DeleteBehavior.Restrict);

        // Association #15: HoiThoai (0..*) ×—> (1) GiaSu => Restrict
        builder.HasOne(x => x.GiaSu)
            .WithMany()
            .HasForeignKey(x => x.GiaSuId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composition #3: HoiThoai (1) ◆—— (0..*) TinNhan => Cascade
        builder.HasMany(x => x.DsTinNhan)
            .WithOne(t => t.HoiThoai)
            .HasForeignKey(t => t.HoiThoaiId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TinNhanConfiguration : IEntityTypeConfiguration<TinNhan>
{
    public void Configure(EntityTypeBuilder<TinNhan> builder)
    {
        builder.ToTable("TinNhan");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.NoiDung).IsRequired().HasMaxLength(2000);

        // Association #11: TinNhan (0..*) ×—> (1) NguoiDungThamGia (+nguoiGui) => Restrict (Tránh multiple cascade paths!)
        builder.HasOne(x => x.NguoiGui)
            .WithMany()
            .HasForeignKey(x => x.NguoiGuiId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ThongBaoConfiguration : IEntityTypeConfiguration<ThongBao>
{
    public void Configure(EntityTypeBuilder<ThongBao> builder)
    {
        builder.ToTable("ThongBao");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TieuDe).IsRequired().HasMaxLength(250);
        builder.Property(x => x.NoiDung).IsRequired().HasMaxLength(1000);

        // Association #8: ThongBao (0..*) ×—> (1) NguoiDung (+nguoiNhan) => Restrict
        builder.HasOne(x => x.NguoiNhan)
            .WithMany()
            .HasForeignKey(x => x.NguoiNhanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class YeuCauNapTienConfiguration : IEntityTypeConfiguration<YeuCauNapTien>
{
    public void Configure(EntityTypeBuilder<YeuCauNapTien> builder)
    {
        builder.ToTable("YeuCauNapTien", t =>
        {
            t.HasCheckConstraint("CK_YeuCauNapTien_SoTien", "[SoTien] >= 2000 AND [SoTien] <= 50000000");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.CongThanhToan).IsRequired().HasMaxLength(30);
        builder.Property(x => x.PaymentLinkId).HasMaxLength(100);
        builder.Property(x => x.CheckoutUrl).HasMaxLength(500);
        builder.Property(x => x.QrCode).HasMaxLength(1000);
        builder.Property(x => x.MaBinNganHang).HasMaxLength(20);
        builder.Property(x => x.SoTaiKhoan).HasMaxLength(50);
        builder.Property(x => x.ChuTaiKhoan).HasMaxLength(150);
        builder.Property(x => x.NoiDungChuyenKhoan).IsRequired().HasMaxLength(100);
        builder.Property(x => x.MaThamChieuNganHang).HasMaxLength(100);

        builder.HasIndex(x => x.MaDonPayOs).IsUnique();
        builder.HasIndex(x => x.GiaoDichViId)
            .IsUnique()
            .HasFilter("[GiaoDichViId] IS NOT NULL");
        builder.HasIndex(x => new { x.NguoiDungThamGiaId, x.TaoLuc });

        builder.HasOne(x => x.NguoiDungThamGia)
            .WithMany()
            .HasForeignKey(x => x.NguoiDungThamGiaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.GiaoDichVi)
            .WithMany()
            .HasForeignKey(x => x.GiaoDichViId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
