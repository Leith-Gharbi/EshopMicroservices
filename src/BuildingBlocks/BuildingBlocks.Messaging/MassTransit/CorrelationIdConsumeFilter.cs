using BuildingBlocks.Logging;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Messaging.MassTransit;

/// <summary>
/// MassTransit consume filter to extract correlation ID from incoming messages
/// </summary>
public class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<CorrelationIdConsumeFilter<T>> _logger;
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public CorrelationIdConsumeFilter(
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<CorrelationIdConsumeFilter<T>> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        // Try to extract correlation ID from message headers
        var correlationId = context.Headers.Get<string>(CorrelationIdHeaderName)
                            ?? context.CorrelationId?.ToString()
                            ?? Guid.NewGuid().ToString();

        _correlationIdAccessor.SetCorrelationId(correlationId);

        _logger.LogDebug(
            "Message received with Correlation ID: {CorrelationId} for message type: {MessageType}",
            correlationId,
            typeof(T).Name);

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("correlationIdConsume");
    }
}
