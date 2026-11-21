namespace TutorHub.Domain.Entities;

/// <summary>
/// Lớp cơ sở cho các thực thể có khóa chính đơn `Id` (Guid) trong `TutorHub.Domain`.
/// </summary>
public abstract class ThucTheGoc
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
