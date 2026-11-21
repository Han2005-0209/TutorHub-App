using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TutorHub.Application.Common;

namespace TutorHub.Infrastructure.Persistence;

/// <summary>
/// Khóa tài nguyên mức Transaction của SQL Server (`sp_getapplock`) theo thứ tự cố định
/// để chống race-condition đặt lịch trùng giờ và biến động số dư ví đồng thời mà không gây deadlock.
/// </summary>
public static class KhoaUngDungSqlHelper
{
    public static async Task KhoaTaiNguyenTrongGiaoDichAsync(
        AppDbContext db,
        IEnumerable<string> danhSachKhoa,
        CancellationToken cancellationToken = default)
    {
        var khoaSapXep = danhSachKhoa
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        if (khoaSapXep.Count == 0)
        {
            return;
        }

        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(cancellationToken);
        }

        var dbTx = db.Database.CurrentTransaction?.GetDbTransaction();

        foreach (var resource in khoaSapXep)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = dbTx;
            cmd.CommandText =
                "DECLARE @rc int; " +
                "EXEC @rc = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; " +
                "SELECT @rc;";

            var p = new SqlParameter("@res", SqlDbType.NVarChar, 255) { Value = resource };
            cmd.Parameters.Add(p);

            var result = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
            if (result < 0)
            {
                throw new LoiUngDungException(
                    "XUNG_DOT_DONG_THOI",
                    "Hệ thống đang xử lý một giao dịch khác cho tài nguyên này. Vui lòng thử lại sau giây lát.",
                    409);
            }
        }
    }
}
