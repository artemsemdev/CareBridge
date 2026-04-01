namespace CareBridge.AuditService.Models;

public class PaginatedResult<T>
{
    public List<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
    public int TotalCount { get; init; }
}
