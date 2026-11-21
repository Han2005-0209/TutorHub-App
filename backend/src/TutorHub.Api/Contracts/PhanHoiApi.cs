using System.Text.Json.Serialization;
using TutorHub.Application.Common;

namespace TutorHub.Api.Contracts;

/// <summary>
/// Envelope phản hồi thành công chuẩn của TutorHub API (JSON camelCase: { duLieu, meta }).
/// </summary>
public sealed record PhanHoiApi<T>(
    T DuLieu,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    MetaPhanTrangDto? Meta = null);

public sealed record MetaPhanTrangDto(
    int Trang,
    int KichThuocTrang,
    int Tong,
    int TongTrang);

/// <summary>
/// Envelope phản hồi lỗi chuẩn của TutorHub API (JSON camelCase: { loi: { ma, thongBao, chiTiet, maYeuCau } }).
/// </summary>
public sealed record PhanHoiLoiApi(ThongTinLoiDto Loi);

public sealed record ThongTinLoiDto(
    string Ma,
    string ThongBao,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ChiTietLoiTruongDto>? ChiTiet,
    string MaYeuCau);
