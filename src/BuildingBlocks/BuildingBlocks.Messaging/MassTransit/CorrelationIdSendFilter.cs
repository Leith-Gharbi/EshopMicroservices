using BuildingBlocks.Logging;
using MassTransit;

namespace BuildingBlocks.Messaging.MassTransit;

/// <summary>
/// MassTransit send filter to add correlation ID to outgoing messages (commands)
/// </summary>
public class CorrelationIdSendFilter<T> : IFilter<SendContext<T>> where T : class
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public CorrelationIdSendFilter(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            context.Headers.Set(CorrelationIdHeaderName, correlationId);
        }

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("correlationIdSend");
    }
}
