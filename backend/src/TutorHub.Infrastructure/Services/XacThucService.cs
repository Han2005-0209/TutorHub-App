using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TutorHub.Application.Common;
using TutorHub.Application.DTOs;
using TutorHub.Application.Services;
using TutorHub.Domain.Entities;
using TutorHub.Domain.Enums;
using TutorHub.Infrastructure.Persistence;

namespace TutorHub.Infrastructure.Services;

public sealed class XacThucService : IXacThucService
{
    private const int SoLanSaiMatKhauToiDa = 5;
    private static readonly TimeSpan ThoiGianTamKhoa = TimeSpan.FromMinutes(15);

    private static readonly ConcurrentDictionary<string, (int SoLanSai, DateTime? KhoaDenUtc)> BoDemSaiMatKhau = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, (Guid NguoiDungId, DateTime HetHanUtc, bool DaThuHoi)> KhoRefreshToken = new();
    private static readonly ConcurrentDictionary<string, (Guid NguoiDungId, string Loai, DateTime HetHanUtc)> KhoTokenMotLan = new();

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IKiemDuyetNoiDungService _kiemDuyet;

    public XacThucService(AppDbContext db, IConfiguration config, IKiemDuyetNoiDungService kiemDuyet)
    {
        _db = db;
        _config = config;
        _kiemDuyet = kiemDuyet;
    }

    public static string BamMatKhau(string matKhau)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(matKhau),
            salt,
            100_000,
            HashAlgorithmName.SHA256,
            32);

        return $"PBKDF2$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool XacMinhMatKhau(string matKhau, string matKhauHash)
    {
        if (string.IsNullOrWhiteSpace(matKhau) || string.IsNullOrWhiteSpace(matKhauHash))
        {
            return false;
        }

        var parts = matKhauHash.Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(matKhau),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }

    public static void KiemTraDoManhMatKhau(string? matKhau)
    {
        if (string.IsNullOrWhiteSpace(matKhau) ||
            matKhau.Length < 8 ||
            matKhau.Length > 72 ||
            !Regex.IsMatch(matKhau, "[A-Za-z]") ||
            !Regex.IsMatch(matKhau, "\\d"))
        {
            throw new LoiUngDungException(
                "MAT_KHAU_YEU",
                "Mật khẩu phải từ 8 đến 72 ký tự, bao gồm ít nhất 1 chữ cái và 1 chữ số.",
                400);
        }
    }

    public async Task<KetQuaDangNhapDto> DangKyAsync(DangKyYeuCauDto yeuCau, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(yeuCau.Email) || !yeuCau.Email.Contains('@'))
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Địa chỉ email không hợp lệ.", 400);
        }

        if (string.IsNullOrWhiteSpace(yeuCau.HoTen) || yeuCau.HoTen.Trim().Length < 2)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Họ tên phải có ít nhất 2 ký tự.", 400);
        }

        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("hoTen", yeuCau.HoTen, true)]);

        if (yeuCau.VaiTro is not (VaiTro.HOC_SINH or VaiTro.GIA_SU))
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Chỉ cho phép đăng ký vai trò Học sinh hoặc Gia sư.", 400);
        }

        KiemTraDoManhMatKhau(yeuCau.MatKhau);

        var emailChuan = yeuCau.Email.Trim().ToLowerInvariant();
        var daTonTai = await _db.NguoiDungs
            .AnyAsync(x => x.Email == emailChuan && x.TrangThai != TrangThaiNguoiDung.DA_XOA, cancellationToken);

        if (daTonTai)
        {
            throw new LoiUngDungException("EMAIL_DA_DUOC_DUNG", "Email này đã được đăng ký trên hệ thống.", 409);
        }

        var now = DateTime.UtcNow;
        var hash = BamMatKhau(yeuCau.MatKhau);
        NguoiDungThamGia nguoiDungMoi;

        if (yeuCau.VaiTro == VaiTro.GIA_SU)
        {
            nguoiDungMoi = new GiaSu
            {
                Id = Guid.NewGuid(),
                Email = emailChuan,
                MatKhauHash = hash,
                HoTen = yeuCau.HoTen.Trim(),
                SoDienThoai = string.IsNullOrWhiteSpace(yeuCau.SoDienThoai) ? null : yeuCau.SoDienThoai.Trim(),
                KhuVuc = string.IsNullOrWhiteSpace(yeuCau.KhuVuc) ? null : yeuCau.KhuVuc.Trim(),
                TrangThai = TrangThaiNguoiDung.HOAT_DONG,
                XacThucEmailLuc = now,
                TaoLuc = now,
                TieuDe = string.Empty,
                GioiThieu = string.Empty,
                HocVan = string.Empty,
                SoNamKinhNghiem = 0,
                DayTrucTuyen = true,
                DayTrucTiep = false,
                TrangThaiHoSo = TrangThaiHoSoGiaSu.NHAP,
                BanKinhPhucVuKm = 10
            };
        }
        else
        {
            nguoiDungMoi = new HocSinh
            {
                Id = Guid.NewGuid(),
                Email = emailChuan,
                MatKhauHash = hash,
                HoTen = yeuCau.HoTen.Trim(),
                SoDienThoai = string.IsNullOrWhiteSpace(yeuCau.SoDienThoai) ? null : yeuCau.SoDienThoai.Trim(),
                KhuVuc = string.IsNullOrWhiteSpace(yeuCau.KhuVuc) ? null : yeuCau.KhuVuc.Trim(),
                TrangThai = TrangThaiNguoiDung.HOAT_DONG,
                XacThucEmailLuc = now,
                TaoLuc = now
            };
        }

        var vi = new TaiKhoanVi
        {
            Id = Guid.NewGuid(),
            NguoiDungThamGiaId = nguoiDungMoi.Id,
            SoDu = 0,
            TaoLuc = now
        };
        nguoiDungMoi.TaiKhoanVi = vi;

        _db.NguoiDungThamGias.Add(nguoiDungMoi);
        await _db.SaveChangesAsync(cancellationToken);

        var tokenEmailDemo = TaoTokenMotLan(nguoiDungMoi.Id, "XAC_THUC_EMAIL", TimeSpan.FromHours(24));
        return TaoKetQuaDangNhap(nguoiDungMoi, vi.SoDu, tokenEmailDemo);
    }

    public async Task<KetQuaDangNhapDto> DangNhapAsync(DangNhapYeuCauDto yeuCau, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(yeuCau.Email) || string.IsNullOrWhiteSpace(yeuCau.MatKhau))
        {
            throw new LoiUngDungException("SAI_THONG_TIN_DANG_NHAP", "Email hoặc mật khẩu không chính xác.", 401);
        }

        var emailChuan = yeuCau.Email.Trim().ToLowerInvariant();
        if (BoDemSaiMatKhau.TryGetValue(emailChuan, out var trangThaiKhoa) &&
            trangThaiKhoa.KhoaDenUtc.HasValue &&
            trangThaiKhoa.KhoaDenUtc.Value > DateTime.UtcNow)
        {
            var soPhutConLai = Math.Max(1, (int)Math.Ceiling((trangThaiKhoa.KhoaDenUtc.Value - DateTime.UtcNow).TotalMinutes));
            throw new LoiUngDungException(
                "TAI_KHOAN_TAM_KHOA",
                $"Tài khoản tạm thời bị khóa do nhập sai mật khẩu 5 lần. Vui lòng thử lại sau {soPhutConLai} phút.",
                423);
        }

        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Email == emailChuan, cancellationToken);

        if (nguoiDung is null || nguoiDung.TrangThai == TrangThaiNguoiDung.DA_XOA)
        {
            GhiNhanSaiMatKhau(emailChuan);
            throw new LoiUngDungException("SAI_THONG_TIN_DANG_NHAP", "Email hoặc mật khẩu không chính xác.", 401);
        }

        if (nguoiDung.TrangThai == TrangThaiNguoiDung.BI_KHOA)
        {
            throw new LoiUngDungException("TAI_KHOAN_BI_KHOA", "Tài khoản của bạn đã bị khóa bởi quản trị viên.", 403);
        }

        if (!XacMinhMatKhau(yeuCau.MatKhau, nguoiDung.MatKhauHash))
        {
            GhiNhanSaiMatKhau(emailChuan);
            throw new LoiUngDungException("SAI_THONG_TIN_DANG_NHAP", "Email hoặc mật khẩu không chính xác.", 401);
        }

        BoDemSaiMatKhau.TryRemove(emailChuan, out _);

        int? soDuVi = null;
        if (nguoiDung is NguoiDungThamGia)
        {
            soDuVi = await _db.TaiKhoanVis
                .Where(v => v.NguoiDungThamGiaId == nguoiDung.Id)
                .Select(v => (int?)v.SoDu)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;
        }

        return TaoKetQuaDangNhap(nguoiDung, soDuVi);
    }

    public async Task<KetQuaDangNhapDto> LamMoiTokenAsync(LamMoiTokenYeuCauDto yeuCau, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(yeuCau.RefreshToken))
        {
            throw new LoiUngDungException("TOKEN_KHONG_HOP_LE", "Refresh token không hợp lệ.", 401);
        }

        var hash = BamSha256(yeuCau.RefreshToken);
        if (!KhoRefreshToken.TryGetValue(hash, out var phien) ||
            phien.DaThuHoi ||
            phien.HetHanUtc <= DateTime.UtcNow)
        {
            throw new LoiUngDungException("TOKEN_KHONG_HOP_LE", "Phiên đăng nhập đã hết hạn hoặc bị thu hồi.", 401);
        }

        KhoRefreshToken[hash] = (phien.NguoiDungId, phien.HetHanUtc, true);

        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == phien.NguoiDungId, cancellationToken);

        if (nguoiDung is null || nguoiDung.TrangThai != TrangThaiNguoiDung.HOAT_DONG)
        {
            throw new LoiUngDungException("TOKEN_KHONG_HOP_LE", "Tài khoản không còn hoạt động.", 401);
        }

        int? soDuVi = null;
        if (nguoiDung is NguoiDungThamGia)
        {
            soDuVi = await _db.TaiKhoanVis
                .Where(v => v.NguoiDungThamGiaId == nguoiDung.Id)
                .Select(v => (int?)v.SoDu)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;
        }

        return TaoKetQuaDangNhap(nguoiDung, soDuVi);
    }

    public Task DangXuatAsync(Guid nguoiDungId, CancellationToken cancellationToken = default)
    {
        foreach (var kvp in KhoRefreshToken)
        {
            if (kvp.Value.NguoiDungId == nguoiDungId)
            {
                KhoRefreshToken[kvp.Key] = (kvp.Value.NguoiDungId, kvp.Value.HetHanUtc, true);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<NguoiDungHienTaiDto> XacThucEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new LoiUngDungException("TOKEN_KHONG_HOP_LE", "Mã xác thực email không hợp lệ.", 400);
        }

        var hash = BamSha256(token.Trim());
        if (!KhoTokenMotLan.TryRemove(hash, out var muc) ||
            muc.Loai != "XAC_THUC_EMAIL" ||
            muc.HetHanUtc <= DateTime.UtcNow)
        {
            throw new LoiUngDungException("TOKEN_KHONG_HOP_LE", "Mã xác thực email không hợp lệ hoặc đã hết hạn.", 400);
        }

        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == muc.NguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        nguoiDung.XacThucEmailLuc ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return await LayThongTinToiAsync(nguoiDung.Id, cancellationToken);
    }

    public async Task<string> GuiLaiXacThucEmailAsync(Guid nguoiDungId, CancellationToken cancellationToken = default)
    {
        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        return TaoTokenMotLan(nguoiDung.Id, "XAC_THUC_EMAIL", TimeSpan.FromHours(24));
    }

    public async Task<string?> QuenMatKhauAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var emailChuan = email.Trim().ToLowerInvariant();
        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Email == emailChuan && x.TrangThai == TrangThaiNguoiDung.HOAT_DONG, cancellationToken);

        return nguoiDung is null
            ? null
            : TaoTokenMotLan(nguoiDung.Id, "DAT_LAI_MAT_KHAU", TimeSpan.FromMinutes(30));
    }

    public async Task DatLaiMatKhauAsync(DatLaiMatKhauYeuCauDto yeuCau, CancellationToken cancellationToken = default)
    {
        KiemTraDoManhMatKhau(yeuCau.MatKhauMoi);

        var hash = BamSha256((yeuCau.Token ?? string.Empty).Trim());
        if (!KhoTokenMotLan.TryRemove(hash, out var muc) ||
            muc.Loai != "DAT_LAI_MAT_KHAU" ||
            muc.HetHanUtc <= DateTime.UtcNow)
        {
            throw new LoiUngDungException("TOKEN_KHONG_HOP_LE", "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.", 400);
        }

        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == muc.NguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        nguoiDung.DoiMatKhau(BamMatKhau(yeuCau.MatKhauMoi));
        await _db.SaveChangesAsync(cancellationToken);
        await DangXuatAsync(nguoiDung.Id, cancellationToken);
    }

    public async Task<NguoiDungHienTaiDto> LayThongTinToiAsync(Guid nguoiDungId, CancellationToken cancellationToken = default)
    {
        var nguoiDung = await _db.NguoiDungs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy thông tin người dùng.", 404);

        int? soDuVi = null;
        if (nguoiDung is NguoiDungThamGia)
        {
            soDuVi = await _db.TaiKhoanVis
                .AsNoTracking()
                .Where(v => v.NguoiDungThamGiaId == nguoiDung.Id)
                .Select(v => (int?)v.SoDu)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;
        }

        return AnhXaNguoiDungDto(nguoiDung, soDuVi);
    }

    public async Task<NguoiDungHienTaiDto> CapNhatHoSoToiAsync(
        Guid nguoiDungId,
        CapNhatHoSoNguoiDungDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(yeuCau.HoTen) || yeuCau.HoTen.Trim().Length < 2)
        {
            throw new LoiUngDungException("DU_LIEU_KHONG_HOP_LE", "Họ tên phải có ít nhất 2 ký tự.", 400);
        }

        _kiemDuyet.BatBuocNoiDungNguoiDungHopLe([("hoTen", yeuCau.HoTen, true)]);

        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        nguoiDung.CapNhatHoSo(yeuCau.HoTen, yeuCau.SoDienThoai, yeuCau.KhuVuc);
        await _db.SaveChangesAsync(cancellationToken);

        return await LayThongTinToiAsync(nguoiDungId, cancellationToken);
    }

    public async Task<NguoiDungHienTaiDto> CapNhatAnhDaiDienAsync(
        Guid nguoiDungId,
        string anhDaiDienUrl,
        CancellationToken cancellationToken = default)
    {
        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        nguoiDung.AnhDaiDienUrl = anhDaiDienUrl;
        await _db.SaveChangesAsync(cancellationToken);

        return await LayThongTinToiAsync(nguoiDungId, cancellationToken);
    }

    public async Task DoiMatKhauAsync(
        Guid nguoiDungId,
        DoiMatKhauYeuCauDto yeuCau,
        CancellationToken cancellationToken = default)
    {
        KiemTraDoManhMatKhau(yeuCau.MatKhauMoi);

        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        if (!XacMinhMatKhau(yeuCau.MatKhauCu, nguoiDung.MatKhauHash))
        {
            throw new LoiUngDungException("SAI_THONG_TIN_DANG_NHAP", "Mật khẩu hiện tại không chính xác.", 401);
        }

        nguoiDung.DoiMatKhau(BamMatKhau(yeuCau.MatKhauMoi));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task VoHieuHoaTaiKhoanAsync(Guid nguoiDungId, CancellationToken cancellationToken = default)
    {
        var nguoiDung = await _db.NguoiDungs
            .FirstOrDefaultAsync(x => x.Id == nguoiDungId, cancellationToken)
            ?? throw new LoiUngDungException("KHONG_TIM_THAY", "Không tìm thấy người dùng.", 404);

        var coDonDangMo = await _db.DonDatLichs
            .Include(d => d.GiaSuMonHoc)
            .AnyAsync(
                d => (d.HocSinhId == nguoiDungId || (d.GiaSuMonHoc != null && d.GiaSuMonHoc.GiaSuId == nguoiDungId)) &&
                     (d.TrangThai == TrangThaiDonDatLich.CHO_XAC_NHAN ||
                      d.TrangThai == TrangThaiDonDatLich.DA_XAC_NHAN ||
                      d.TrangThai == TrangThaiDonDatLich.TRANH_CHAP),
                cancellationToken);

        if (coDonDangMo)
        {
            throw new LoiUngDungException(
                "CHUYEN_TRANG_THAI_SAI",
                "Không thể vô hiệu hóa tài khoản khi vẫn còn đơn đặt lịch đang mở hoặc tranh chấp.",
                409);
        }

        nguoiDung.VoHieuHoaTaiKhoan();
        await _db.SaveChangesAsync(cancellationToken);
        await DangXuatAsync(nguoiDungId, cancellationToken);
    }

    private KetQuaDangNhapDto TaoKetQuaDangNhap(NguoiDung nguoiDung, int? soDuVi, string? tokenEmailDemo = null)
    {
        var issuer = _config["Jwt:Issuer"] ?? "TutorHub.Api";
        var audience = _config["Jwt:Audience"] ?? "TutorHub.Client";
        var secretKey = _config["Jwt:SecretKey"] ?? "TutorHub-Local-Dev-Jwt-Secret-Key-2026-Min32Chars!";
        var accessMinutes = int.TryParse(_config["Jwt:AccessTokenMinutes"], out var m) && m > 0 ? m : 120;
        var hetHanLuc = DateTime.UtcNow.AddMinutes(accessMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, nguoiDung.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, nguoiDung.Email),
            new Claim("hoTen", nguoiDung.HoTen),
            new Claim("vaiTro", nguoiDung.VaiTro.ToString()),
            new Claim(ClaimTypes.Role, nguoiDung.VaiTro.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: hetHanLuc,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        KhoRefreshToken[BamSha256(refreshToken)] = (nguoiDung.Id, DateTime.UtcNow.AddDays(7), false);

        return new KetQuaDangNhapDto(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            HetHanLuc: hetHanLuc,
            NguoiDung: AnhXaNguoiDungDto(nguoiDung, soDuVi),
            TokenXacThucEmailDemo: tokenEmailDemo);
    }

    private static NguoiDungHienTaiDto AnhXaNguoiDungDto(NguoiDung nguoiDung, int? soDuVi) =>
        new(
            Id: nguoiDung.Id,
            Email: nguoiDung.Email,
            HoTen: nguoiDung.HoTen,
            VaiTro: nguoiDung.VaiTro,
            SoDienThoai: nguoiDung.SoDienThoai,
            AnhDaiDienUrl: nguoiDung.AnhDaiDienUrl,
            KhuVuc: nguoiDung.KhuVuc,
            TrangThai: nguoiDung.TrangThai,
            DaXacThucEmail: nguoiDung.XacThucEmailLuc.HasValue,
            XacThucEmailLuc: nguoiDung.XacThucEmailLuc,
            SoDuVi: soDuVi,
            TaoLuc: nguoiDung.TaoLuc);

    private static void GhiNhanSaiMatKhau(string emailChuan)
    {
        BoDemSaiMatKhau.AddOrUpdate(
            emailChuan,
            _ => (1, null),
            (_, cu) =>
            {
                var soLanMoi = cu.SoLanSai + 1;
                var khoaDen = soLanMoi >= SoLanSaiMatKhauToiDa
                    ? DateTime.UtcNow.Add(ThoiGianTamKhoa)
                    : (DateTime?)null;
                return (soLanMoi, khoaDen);
            });
    }

    private static string TaoTokenMotLan(Guid nguoiDungId, string loai, TimeSpan thoiHan)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        KhoTokenMotLan[BamSha256(token)] = (nguoiDungId, loai, DateTime.UtcNow.Add(thoiHan));
        return token;
    }

    private static string BamSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
