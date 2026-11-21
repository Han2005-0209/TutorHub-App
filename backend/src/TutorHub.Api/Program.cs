using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TutorHub.Api.Contracts;
using TutorHub.Api.Middleware;
using TutorHub.Api.Realtime;
using TutorHub.Application.Common;
using TutorHub.Infrastructure;
using TutorHub.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký tầng Infrastructure (EF Core 10 SQL Server, Storage, VNPay Client) & Phòng học Realtime nội bộ
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<PhongHocRealtimeManager>();

// 2. Đăng ký Controllers + quy ước JSON camelCase & Enum UPPER_SNAKE_CASE + chuẩn hóa lỗi validation
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var chiTiet = context.ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(err =>
                    new ChiTietLoiTruongDto(kvp.Key, err.ErrorMessage)))
                .ToList();

            var maYeuCau = context.HttpContext.Request.Headers["X-Request-Id"].FirstOrDefault()
                           ?? context.HttpContext.TraceIdentifier;

            var body = new PhanHoiLoiApi(new ThongTinLoiDto(
                Ma: "DU_LIEU_KHONG_HOP_LE",
                ThongBao: "Dữ liệu chưa hợp lệ, vui lòng kiểm tra lại.",
                ChiTiet: chiTiet,
                MaYeuCau: maYeuCau));

            return new BadRequestObjectResult(body);
        };
    });

// 3. Cấu hình CORS đọc danh sách origin từ cấu hình (Cors:AllowedOrigins)
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(o => !string.IsNullOrWhiteSpace(o))
    .ToArray() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("TutorHubCors", policy =>
    {
        var allowWildcard = allowedOrigins.Contains("*", StringComparer.Ordinal);
        if (allowWildcard)
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("X-Request-Id", "Content-Disposition");
        }
        else if (allowedOrigins.Length > 0)
        {
            policy.SetIsOriginAllowed(origin =>
                  {
                      if (allowedOrigins.Any(o => string.Equals(o, origin, StringComparison.OrdinalIgnoreCase)))
                          return true;
                      if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                      {
                          foreach (var pattern in allowedOrigins)
                          {
                              if (pattern.StartsWith("https://*.", StringComparison.OrdinalIgnoreCase))
                              {
                                  var suffix = pattern["https://*".Length..]; // ".vercel.app"
                                  if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                                      return true;
                              }
                          }
                      }
                      return false;
                  })
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("X-Request-Id", "Content-Disposition");
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("X-Request-Id", "Content-Disposition");
        }
    });
});

// 4. Đăng ký xác thực JWT Bearer trong Header (KHÔNG dùng cookie vì khác domain với Vercel)
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TutorHub.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TutorHub.Client";
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(jwtSecretKey),
            IssuerSigningKey = !string.IsNullOrWhiteSpace(jwtSecretKey)
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
                : null,
            RoleClaimType = "vaiTro",
            NameClaimType = "sub"
        };
        // Hỗ trợ truyền JWT qua query `?access_token=` cho kết nối WebSocket phòng học nội bộ (`/ws/phong-hoc/*`)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/ws/phong-hoc", StringComparison.OrdinalIgnoreCase))
                {
                    var token = context.Request.Query["access_token"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        context.Token = token;
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// 4b. Phòng thủ chịu tải cao: Nén phản hồi (Brotli/Gzip) & Giới hạn tốc độ yêu cầu theo IP (Rate Limiting)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        var body = JsonSerializer.Serialize(new PhanHoiLoiApi(new ThongTinLoiDto(
            Ma: "QUA_NHIEU_YEU_CAU",
            ThongBao: "Bạn đang gửi yêu cầu quá nhanh. Vui lòng chờ vài giây rồi thử lại.",
            ChiTiet: null,
            MaYeuCau: context.HttpContext.TraceIdentifier)),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await context.HttpContext.Response.WriteAsync(body, token);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        // Không bóp băng thông health check & kết nối WebSocket phòng học
        if (path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/ws/", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("exempt");
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 240,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 20
        });
    });
});

// 5. Swagger / OpenAPI cho môi trường Development
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Hỗ trợ chạy migration bằng một lệnh riêng (Docker-ready): `dotnet run --project src/TutorHub.Api -- --migrate`
if (args.Contains("--migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DuLieuMauSeeder.SeedAsync(db);
    return;
}

// Hỗ trợ làm mới dữ liệu (xóa toàn bộ người dùng trừ Admin + seed bộ dữ liệu bản đầy đủ): `--reseed-full`
if (args.Contains("--reseed-full", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DuLieuMauSeeder.LamMoiVaSeedBanDayDuAsync(db);
    return;
}

var autoMigrateOnStartup = builder.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup");
var autoSeedOnStartup = builder.Configuration.GetValue<bool>("Database:AutoSeedOnStartup");

if (app.Environment.IsDevelopment() || autoMigrateOnStartup || autoSeedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (autoMigrateOnStartup)
        {
            await db.Database.MigrateAsync();
        }

        if ((app.Environment.IsDevelopment() || autoSeedOnStartup) && await db.Database.CanConnectAsync())
        {
            await DuLieuMauSeeder.SeedAsync(db);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Bỏ qua khởi tạo/seed CSDL tự động khi khởi động.");
    }
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseResponseCompression();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("TutorHubCors");
app.UseRateLimiter();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(25)
});
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Map("/ws/phong-hoc/{donId:guid}", PhongHocWebSocketEndpoint.HandleAsync)
   .RequireAuthorization();

await app.RunAsync();

public partial class Program;
