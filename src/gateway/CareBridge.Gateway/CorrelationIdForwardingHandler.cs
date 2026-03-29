using CareBridge.Shared.Infrastructure.Correlation;

namespace CareBridge.Gateway;

public class CorrelationIdForwardingHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _correlationAccessor;

    public CorrelationIdForwardingHandler(ICorrelationIdAccessor correlationAccessor)
    {
        _correlationAccessor = correlationAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", _correlationAccessor.CorrelationId);
        return base.SendAsync(request, cancellationToken);
    }
}
