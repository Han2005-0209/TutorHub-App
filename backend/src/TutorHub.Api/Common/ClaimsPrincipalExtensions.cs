using System.Security.Claims;
using TutorHub.Application.Common;
using TutorHub.Domain.Enums;

namespace TutorHub.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid LayNguoiDungId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub")
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(raw, out var id))
        {
            throw new LoiUngDungException("CHUA_DANG_NHAP", "Vui lòng đăng nhập để tiếp tục.", 401);
        }

        return id;
    }

    public static Guid? LayNguoiDungIdTuyChon(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("sub")
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static VaiTro LayVaiTro(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("vaiTro")
                  ?? user.FindFirstValue(ClaimTypes.Role);

        if (!Enum.TryParse<VaiTro>(raw, ignoreCase: true, out var vaiTro))
        {
            throw new LoiUngDungException("KHONG_CO_QUYEN", "Vai trò người dùng không hợp lệ.", 403);
        }

        return vaiTro;
    }
}
