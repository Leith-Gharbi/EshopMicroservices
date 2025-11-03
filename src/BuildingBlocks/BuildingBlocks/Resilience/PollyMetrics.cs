using System.Diagnostics.Metrics;

namespace BuildingBlocks.Resilience;

/// <summary>
/// Metrics for Polly resilience policies using .NET Metrics API.
/// These metrics can be exported to Prometheus, Application Insights, or other monitoring systems.
/// </summary>
public static class PollyMetrics
{
    /// <summary>
    /// Meter name for the resilience metrics
    /// </summary>
    private const string MeterName = "Eshop.Resilience";

    /// <summary>
    /// Meter version
    /// </summary>
    private const string MeterVersion = "1.0.0";

    /// <summary>
    /// The meter instance for creating instruments
    /// </summary>
    private static readonly Meter Meter = new(MeterName, MeterVersion);

    /// <summary>
    /// Counter for total number of retries across all services.
    /// Tags: service (service name)
    /// </summary>
    public static readonly Counter<int> RetryCounter = Meter.CreateCounter<int>(
        name: "resilience_retry_total",
        unit: "retries",
        description: "Total number of retry attempts"
    );

    /// <summary>
    /// Counter for circuit breaker state changes.
    /// Tags: service (service name), state (open/closed), type (simple/advanced)
    /// </summary>
    public static readonly Counter<int> CircuitBreakerCounter = Meter.CreateCounter<int>(
        name: "resilience_circuit_breaker_state_changes",
        unit: "changes",
        description: "Circuit breaker state change events"
    );

    /// <summary>
    /// Counter for total number of timeouts.
    /// Tags: service (service name)
    /// </summary>
    public static readonly Counter<int> TimeoutCounter = Meter.CreateCounter<int>(
        name: "resilience_timeout_total",
        unit: "timeouts",
        description: "Total number of timeout events"
    );

    /// <summary>
    /// Counter for bulkhead rejections (when max concurrency is reached).
    /// Tags: service (service name)
    /// </summary>
    public static readonly Counter<int> BulkheadRejectionCounter = Meter.CreateCounter<int>(
        name: "resilience_bulkhead_rejection_total",
        unit: "rejections",
        description: "Total number of bulkhead rejections"
    );

    /// <summary>
    /// Counter for fallback activations.
    /// Tags: service (service name)
    /// </summary>
    public static readonly Counter<int> FallbackCounter = Meter.CreateCounter<int>(
        name: "resilience_fallback_total",
        unit: "fallbacks",
        description: "Total number of fallback activations"
    );

    /// <summary>
    /// Histogram for retry delay distribution in seconds.
    /// Useful for analyzing backoff strategies.
    /// </summary>
    public static readonly Histogram<double> RetryDelayHistogram = Meter.CreateHistogram<double>(
        name: "resilience_retry_delay_seconds",
        unit: "s",
        description: "Distribution of retry delays"
    );

    /// <summary>
    /// ObservableGauge for current circuit breaker state.
    /// 0 = Closed, 1 = Open, 2 = Half-Open
    /// Note: This is a placeholder - actual implementation would require state tracking
    /// </summary>
    public static readonly ObservableGauge<int> CircuitBreakerStateGauge = Meter.CreateObservableGauge<int>(
        name: "resilience_circuit_breaker_state",
        observeValue: () => new Measurement<int>(0), // Placeholder - implement state tracking if needed
        unit: "state",
        description: "Current circuit breaker state (0=Closed, 1=Open, 2=Half-Open)"
    );

    /// <summary>
    /// Histogram for HTTP request duration in milliseconds.
    /// Includes both successful and failed requests.
    /// </summary>
    public static readonly Histogram<double> RequestDurationHistogram = Meter.CreateHistogram<double>(
        name: "resilience_http_request_duration_ms",
        unit: "ms",
        description: "HTTP request duration distribution"
    );
}

/// <summary>
/// Extension methods for recording resilience metrics
/// </summary>
public static class PollyMetricsExtensions
{
    /// <summary>
    /// Records a retry event with service context
    /// </summary>
    public static void RecordRetry(string serviceName)
    {
        PollyMetrics.RetryCounter.Add(1, new KeyValuePair<string, object?>("service", serviceName));
    }

    /// <summary>
    /// Records a circuit breaker state change
    /// </summary>
    public static void RecordCircuitBreakerStateChange(string serviceName, string state, string type = "simple")
    {
        PollyMetrics.CircuitBreakerCounter.Add(1,
            new KeyValuePair<string, object?>("service", serviceName),
            new KeyValuePair<string, object?>("state", state),
            new KeyValuePair<string, object?>("type", type));
    }

    /// <summary>
    /// Records a timeout event
    /// </summary>
    public static void RecordTimeout(string serviceName)
    {
        PollyMetrics.TimeoutCounter.Add(1, new KeyValuePair<string, object?>("service", serviceName));
    }

    /// <summary>
    /// Records a bulkhead rejection
    /// </summary>
    public static void RecordBulkheadRejection(string serviceName)
    {
        PollyMetrics.BulkheadRejectionCounter.Add(1, new KeyValuePair<string, object?>("service", serviceName));
    }

    /// <summary>
    /// Records a fallback activation
    /// </summary>
    public static void RecordFallback(string serviceName)
    {
        PollyMetrics.FallbackCounter.Add(1, new KeyValuePair<string, object?>("service", serviceName));
    }

    /// <summary>
    /// Records retry delay
    /// </summary>
    public static void RecordRetryDelay(double delaySeconds)
    {
        PollyMetrics.RetryDelayHistogram.Record(delaySeconds);
    }

    /// <summary>
    /// Records HTTP request duration
    /// </summary>
    public static void RecordRequestDuration(string serviceName, double durationMs, bool success)
    {
        PollyMetrics.RequestDurationHistogram.Record(durationMs,
            new KeyValuePair<string, object?>("service", serviceName),
            new KeyValuePair<string, object?>("success", success));
    }
}
