namespace CareBridge.Shared.Infrastructure.Correlation;

public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    public string CorrelationId => _correlationId.Value ?? string.Empty;

    public void Set(string correlationId)
    {
        _correlationId.Value = correlationId;
    }
}
