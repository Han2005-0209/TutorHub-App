using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using TutorHub.Application.Common;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Infrastructure;
using TutorHub.Infrastructure.Payments;
using TutorHub.Infrastructure.Persistence;
using Xunit;

namespace TutorHub.UnitTests;

public sealed class PayOsVietQrNapTienTests : IDisposable
{
    private const string ChecksumKeyTest = "test_checksum_key_payos_tutorhub_123456";
    private readonly string _dbName;
    private readonly ServiceProvider _sp;

    public PayOsVietQrNapTienTests()
    {
        _dbName = $"TutorHub_PayOs_{Guid.NewGuid():N}";
        var connStr = $"Server=localhost;Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True;";

        var config = new CauHinhTestPayOs(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ConnectionStrings:DefaultConnection"] = connStr,
            ["BookingPolicy:DefaultPlatformFeePercent"] = "12",
            ["BookingPolicy:AutoDisbursementHours"] = "24",
            ["Storage:PublicRootPath"] = "storage/public",
            ["Storage:PrivateRootPath"] = "storage/private",
            ["Storage:UrlSigningKey"] = "payos-test-signing-key-32-chars-min",
            ["PayOs:ClientId"] = "test-client-id",
            ["PayOs:ApiKey"] = "test-api-key",
            ["PayOs:ChecksumKey"] = ChecksumKeyTest,
            ["Jobs:Enabled"] = "false"
        });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructure(config);
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
        }
        finally
        {
            _sp.Dispose();
        }
    }

    [Fact]
    public void PayOs_TinhChuKyTaoYeuCau_KhopChuanHmacSha256()
    {
        const long orderCode = 2611220401123;
        const int amount = 50000;
        const string desc = "THNAP 401123";
        const string returnUrl = "https://tutorhub.vn/ok";
        const string cancelUrl = "https://tutorhub.vn/cancel";

        var sig = PayOsClient.TinhChuKyTaoYeuCau(ChecksumKeyTest, orderCode, amount, desc, returnUrl, cancelUrl);

        var expectedData = $"amount={amount}&cancelUrl={cancelUrl}&description={desc}&orderCode={orderCode}&returnUrl={returnUrl}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChecksumKeyTest));
        var expectedHex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(expectedData))).ToLowerInvariant();

        Assert.Equal(expectedHex, sig);
    }

    [Fact]
    public void PayOs_XacThucWebhook_ChongGiaMaoVaXuLyNull()
    {
        using var scope = _sp.CreateScope();
        var payOs = scope.ServiceProvider.GetRequiredService<IPayOsClient>();

        var dataJson = """
        {
          "orderCode": 2611220401999,
          "amount": 5000,
          "description": "THNAP 401999",
          "accountNumber": "0987654321",
          "reference": "FT2632600001",
          "transactionDateTime": "2026-11-22 04:01:00",
          "currency": "VND",
          "paymentLinkId": "plink_abc123",
          "code": "00",
          "desc": "success",
          "counterAccountBankId": null,
          "counterAccountName": ""
        }
        """;

        using var doc = JsonDocument.Parse(dataJson);
        var validSig = PayOsClient.TinhChuKyWebhookData(ChecksumKeyTest, doc.RootElement);

        Assert.True(payOs.XacThucChuKyWebhook(doc.RootElement, validSig));
        Assert.False(payOs.XacThucChuKyWebhook(doc.RootElement, "0000000000000000000000000000000000000000000000000000000000000000"));
    }

    [Fact]
    public async Task NapTienVi_WebhookPayOs_CongTienDungMotLanIdempotentVaGhiSoCai()
    {
        Guid hocSinhId;
        Guid viId;
        const long orderCode = 2611220405555;

        using (var setupScope = _sp.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hs = new HocSinh
            {
                Email = "hs.napvietqr@tutorhub.test",
                MatKhauHash = "hash",
                HoTen = "Nguyễn Văn Test",
                XacThucEmailLuc = DateTime.UtcNow,
                TrangThai = TrangThaiNguoiDung.HOAT_DONG
            };
            db.HocSinhs.Add(hs);
            await db.SaveChangesAsync();
            hocSinhId = hs.Id;

            var vi = new TaiKhoanVi
            {
                NguoiDungThamGiaId = hs.Id,
                SoDu = 100_000,
                SoDuDangXuLyRut = 0
            };
            db.TaiKhoanVis.Add(vi);
            await db.SaveChangesAsync();
            viId = vi.Id;

            db.YeuCauNapTiens.Add(new YeuCauNapTien
            {
                NguoiDungThamGiaId = hs.Id,
                MaDonPayOs = orderCode,
                SoTien = 50_000,
                CongThanhToan = "PAYOS_VIETQR",
                TrangThai = TrangThaiNapTien.CHO_THANH_TOAN,
                NoiDungChuyenKhoan = "THNAP 405555",
                MaBinNganHang = "970422",
                SoTaiKhoan = "0987654321",
                ChuTaiKhoan = "TRAN GIA HAN"
            });
            await db.SaveChangesAsync();
        }

        var dataObj = new
        {
            orderCode,
            amount = 50_000,
            description = "THNAP 405555",
            accountNumber = "0987654321",
            reference = "FT2632699999",
            transactionDateTime = "2026-11-22 04:05:00",
            currency = "VND",
            paymentLinkId = "plink_test_001",
            code = "00",
            desc = "success",
            counterAccountBankId = (string?)null,
            counterAccountName = ""
        };
        var dataRaw = JsonSerializer.Serialize(dataObj);
        using var dataDoc = JsonDocument.Parse(dataRaw);
        var sig = PayOsClient.TinhChuKyWebhookData(ChecksumKeyTest, dataDoc.RootElement);

        var webhookBody = JsonSerializer.Serialize(new
        {
            code = "00",
            desc = "success",
            success = true,
            data = dataObj,
            signature = sig
        });

        // Gửi Webhook 2 lần liên tiếp để kiểm tra tính bất biến (Idempotency)
        using (var s1 = _sp.CreateScope())
        {
            var svc = s1.ServiceProvider.GetRequiredService<IViVaTaiChinhService>();
            var ok1 = await svc.XuLyWebhookPayOsAsync(webhookBody);
            Assert.True(ok1);
        }

        using (var s2 = _sp.CreateScope())
        {
            var svc = s2.ServiceProvider.GetRequiredService<IViVaTaiChinhService>();
            var ok2 = await svc.XuLyWebhookPayOsAsync(webhookBody);
            Assert.True(ok2);
        }

        using var verifyScope = _sp.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var viSau = await verifyDb.TaiKhoanVis.AsNoTracking().FirstAsync(v => v.Id == viId);
        var dsGd = await verifyDb.GiaoDichVis.AsNoTracking().Where(g => g.TaiKhoanViId == viId).ToListAsync();
        var yeuCauSau = await verifyDb.YeuCauNapTiens.AsNoTracking().FirstAsync(n => n.MaDonPayOs == orderCode);
        var tb = await verifyDb.ThongBaos.AsNoTracking().FirstOrDefaultAsync(t => t.NguoiNhanId == hocSinhId);

        Assert.Equal(150_000, viSau.SoDu);
        Assert.Single(dsGd);
        Assert.Equal(LoaiGiaoDichVi.NAP_TIEN, dsGd[0].Loai);
        Assert.Equal(50_000, dsGd[0].SoTien);
        Assert.Equal(150_000, dsGd[0].SoDuSau);
        Assert.Null(dsGd[0].ThanhToanId);
        Assert.Null(dsGd[0].YeuCauRutTienId);
        Assert.Equal(TrangThaiNapTien.THANH_CONG, yeuCauSau.TrangThai);
        Assert.Equal("FT2632699999", yeuCauSau.MaThamChieuNganHang);
        Assert.Equal(dsGd[0].Id, yeuCauSau.GiaoDichViId);
        Assert.NotNull(tb);
    }

    [Fact]
    public async Task NapTienVi_WebhookPayOs_TuChoiChuKyGiaMaoVaChapNhanPing123()
    {
        using var scope = _sp.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IViVaTaiChinhService>();

        var pingPayload = """
        {
          "code": "00",
          "desc": "success",
          "success": true,
          "data": { "orderCode": 123, "amount": 3000, "description": "VQRIO123" },
          "signature": "test-ping"
        }
        """;
        Assert.True(await svc.XuLyWebhookPayOsAsync(pingPayload));

        var forgedPayload = """
        {
          "code": "00",
          "desc": "success",
          "success": true,
          "data": { "orderCode": 2611220408888, "amount": 9999000, "code": "00" },
          "signature": "forged_signature_hex"
        }
        """;
        var ex = await Assert.ThrowsAsync<LoiUngDungException>(() => svc.XuLyWebhookPayOsAsync(forgedPayload));
        Assert.Equal("CHU_KY_WEBHOOK_KHONG_HOP_LE", ex.MaLoi);
    }

    private sealed class CauHinhTestPayOs(Dictionary<string, string?> values, string prefix = "") : IConfigurationSection
    {
        public string? this[string key]
        {
            get
            {
                var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
                return values.TryGetValue(fullKey, out var val) ? val : null;
            }
            set => values[string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}"] = value;
        }

        public string Key => prefix.Contains(':') ? prefix[(prefix.LastIndexOf(':') + 1)..] : prefix;
        public string Path => prefix;
        public string? Value
        {
            get => values.TryGetValue(prefix, out var v) ? v : null;
            set => values[prefix] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => new CancellationChangeToken(CancellationToken.None);
        public IConfigurationSection GetSection(string key) =>
            new CauHinhTestPayOs(values, string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}");
    }
}
