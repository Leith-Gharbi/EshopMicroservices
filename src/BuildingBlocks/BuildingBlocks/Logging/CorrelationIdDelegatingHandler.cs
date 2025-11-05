namespace BuildingBlocks.Logging;

/// <summary>
/// DelegatingHandler to propagate correlation ID in outgoing HTTP requests
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public CorrelationIdDelegatingHandler(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add correlation ID to outgoing request if available
        var correlationId = _correlationIdAccessor.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            // Remove existing header if present to avoid duplicates
            if (request.Headers.Contains(CorrelationIdHeaderName))
            {
                request.Headers.Remove(CorrelationIdHeaderName);
            }

            request.Headers.Add(CorrelationIdHeaderName, correlationId);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
