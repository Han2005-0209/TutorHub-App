using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TutorHub.Api.Contracts;
using TutorHub.Application.Common;

namespace TutorHub.Api.Middleware;

/// <summary>
/// Middleware xử lý lỗi tập trung: gắn header X-Request-Id và chuẩn hóa mọi ngoại lệ
/// thành envelope JSON camelCase { loi: { ma, thongBao, chiTiet, maYeuCau } }.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var maYeuCau = context.Request.Headers["X-Request-Id"].FirstOrDefault()
                       ?? context.TraceIdentifier;
        context.Response.Headers["X-Request-Id"] = maYeuCau;

        try
        {
            await _next(context);
        }
        catch (LoiUngDungException ex)
        {
            context.Response.StatusCode = ex.MaHttp;
            context.Response.ContentType = "application/json; charset=utf-8";

            var body = new PhanHoiLoiApi(new ThongTinLoiDto(
                ex.MaLoi,
                ex.Message,
                ex.ChiTiet,
                maYeuCau));

            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Xung dot dong thoi khi cap nhat CSDL (MaYeuCau={MaYeuCau})", maYeuCau);

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json; charset=utf-8";

            var body = new PhanHoiLoiApi(new ThongTinLoiDto(
                "XUNG_DOT_DONG_THOI",
                "Dữ liệu vừa được cập nhật bởi một thao tác khác. Vui lòng tải lại và thử lại.",
                null,
                maYeuCau));

            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Loi he thong chua xu ly (MaYeuCau={MaYeuCau})", maYeuCau);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";

            var body = new PhanHoiLoiApi(new ThongTinLoiDto(
                "LOI_HE_THONG",
                "Có lỗi xảy ra, vui lòng thử lại sau.",
                null,
                maYeuCau));

            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
    }
}
