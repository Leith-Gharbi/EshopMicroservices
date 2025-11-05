using BuildingBlocks.Logging;
using MassTransit;

namespace BuildingBlocks.Messaging.MassTransit;

/// <summary>
/// MassTransit publish filter to add correlation ID to outgoing messages
/// </summary>
public class CorrelationIdPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public CorrelationIdPublishFilter(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
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
        context.CreateFilterScope("correlationIdPublish");
    }
}
