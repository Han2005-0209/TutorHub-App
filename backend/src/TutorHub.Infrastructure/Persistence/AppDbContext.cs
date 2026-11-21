using Microsoft.EntityFrameworkCore;
using TutorHub.Application.Common;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;

namespace TutorHub.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của TutorHub trên EF Core 10 (SQL Server).
/// Ánh xạ đầy đủ 24 lớp thực thể «persistent» và 18 «enumeration» theo chuẩn
/// `docs/diagrams/03-class-v4.drawio` và `docs/migration/11-DB-DESIGN-NOTES.md`.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Cây kế thừa TPT NguoiDung (5 lớp)
    public DbSet<NguoiDung> NguoiDungs => Set<NguoiDung>();
    public DbSet<NguoiDungThamGia> NguoiDungThamGias => Set<NguoiDungThamGia>();
    public DbSet<QuanTriVien> QuanTriViens => Set<QuanTriVien>();
    public DbSet<HocSinh> HocSinhs => Set<HocSinh>();
    public DbSet<GiaSu> GiaSus => Set<GiaSu>();

    // Hồ sơ gia sư, môn học, lịch rảnh (TPT 3 lớp), mục yêu thích
    public DbSet<TaiKhoanNganHang> TaiKhoanNganHangs => Set<TaiKhoanNganHang>();
    public DbSet<GiayToGiaSu> GiayToGiaSus => Set<GiayToGiaSu>();
    public DbSet<MonHoc> MonHocs => Set<MonHoc>();
    public DbSet<GiaSuMonHoc> GiaSuMonHocs => Set<GiaSuMonHoc>();
    public DbSet<LichRanh> LichRanhs => Set<LichRanh>();
    public DbSet<LichLapHangTuan> LichLapHangTuans => Set<LichLapHangTuan>();
    public DbSet<LichNgayCuThe> LichNgayCuThes => Set<LichNgayCuThe>();
    public DbSet<MucYeuThich> MucYeuThichs => Set<MucYeuThich>();

    // Đặt lịch, mã giảm giá, thanh toán, ví nội bộ, rút tiền, đánh giá, khiếu nại
    public DbSet<DonDatLich> DonDatLichs => Set<DonDatLich>();
    public DbSet<MaGiamGia> MaGiamGias => Set<MaGiamGia>();
    public DbSet<ThanhToan> ThanhToans => Set<ThanhToan>();
    public DbSet<TaiKhoanVi> TaiKhoanVis => Set<TaiKhoanVi>();
    public DbSet<GiaoDichVi> GiaoDichVis => Set<GiaoDichVi>();
    public DbSet<YeuCauRutTien> YeuCauRutTiens => Set<YeuCauRutTien>();
    public DbSet<DanhGia> DanhGias => Set<DanhGia>();
    public DbSet<KhieuNai> KhieuNais => Set<KhieuNai>();
    public DbSet<NhatKyPhongHoc> NhatKyPhongHocs => Set<NhatKyPhongHoc>();

    // Hội thoại, tin nhắn, thông báo & kiểm toán bảo mật
    public DbSet<HoiThoai> HoiThoais => Set<HoiThoai>();
    public DbSet<TinNhan> TinNhans => Set<TinNhan>();
    public DbSet<ThongBao> ThongBaos => Set<ThongBao>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        KiemTraBatBienTaiChinh();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        KiemTraBatBienTaiChinh();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Bảo vệ bất biến tầng ứng dụng:
    /// 1) Sổ cái `GiaoDichVi` và `AuditLog` là append-only (cấm Update/Delete).
    /// 2) `ThanhToan` sau khi đã `DA_GIAI_NGAN` hoặc `DA_HOAN` bị khóa bất biến (cấm sửa/xóa).
    /// </summary>
    private void KiemTraBatBienTaiChinh()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is GiaoDichVi or AuditLog)
            {
                if (entry.State is EntityState.Modified or EntityState.Deleted)
                {
                    throw new LoiUngDungException(
                        "SO_CAI_BAT_BIEN",
                        "Sổ cái giao dịch và nhật ký kiểm toán là bất biến, không được phép sửa hoặc xóa.",
                        409);
                }
            }

            if (entry.Entity is DonDatLich && entry.State == EntityState.Modified)
            {
                var giaCu = entry.OriginalValues.GetValue<int>(nameof(DonDatLich.GiaMoiGio));
                var giaMoi = entry.CurrentValues.GetValue<int>(nameof(DonDatLich.GiaMoiGio));
                var giamCu = entry.OriginalValues.GetValue<int>(nameof(DonDatLich.TienGiam));
                var giamMoi = entry.CurrentValues.GetValue<int>(nameof(DonDatLich.TienGiam));
                if (giaCu != giaMoi || giamCu != giamMoi)
                {
                    throw new LoiUngDungException(
                        "DON_DAT_LICH_GIA_BAT_BIEN",
                        "Học phí và mức giảm giá đã chụp tại thời điểm đặt đơn là bất biến, không được thay đổi.",
                        409);
                }
            }

            if (entry.Entity is ThanhToan && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var trangThaiGoc = entry.OriginalValues.GetValue<TrangThaiThanhToan>(nameof(ThanhToan.TrangThai));
                if (trangThaiGoc is TrangThaiThanhToan.DA_GIAI_NGAN or TrangThaiThanhToan.DA_HOAN)
                {
                    throw new LoiUngDungException(
                        "THANH_TOAN_DA_KHOA",
                        "Giao dịch thanh toán đã giải ngân hoặc đã hoàn tiền bị khóa, không thể thay đổi.",
                        409);
                }

                if (entry.State == EntityState.Modified && trangThaiGoc == TrangThaiThanhToan.DANG_GIU)
                {
                    var soTienGoc = entry.OriginalValues.GetValue<int>(nameof(ThanhToan.SoTien));
                    var soTienMoi = entry.CurrentValues.GetValue<int>(nameof(ThanhToan.SoTien));
                    if (soTienGoc != soTienMoi)
                    {
                        throw new LoiUngDungException(
                            "THANH_TOAN_DA_KHOA",
                            "Số tiền đã thanh toán vào ký quỹ là bất biến theo lịch sử, không thể thay đổi.",
                            409);
                    }
                }
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Quy tắc bắt buộc tại 11-DB-DESIGN-NOTES.md: lưu toàn bộ 18 Enum dạng chuỗi UPPER_SNAKE_CASE
        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
