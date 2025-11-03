using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace BuildingBlocks.Resilience;

/// <summary>
/// Centralized Polly resilience policies for the Eshop microservices architecture.
/// Provides reusable retry, circuit breaker, timeout, bulkhead, and fallback policies.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Standard retry policy for HTTP requests with exponential backoff and jitter.
    /// Retries on transient HTTP errors (5xx, 408) and timeout exceptions.
    /// </summary>
    /// <param name="retryCount">Number of retry attempts (default: 3)</param>
    /// <param name="logger">Optional logger for retry events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetHttpRetryPolicy(
        int retryCount = 3,
        ILogger? logger = null)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // Handles 5xx and 408
            .Or<TimeoutRejectedException>() // Handle timeout
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) // 2, 4, 8 seconds
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)), // Jitter to prevent thundering herd
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";

                    logger?.LogWarning(
                        "Retry {RetryAttempt}/{MaxRetries} for {ServiceName} after {Delay}s. Reason: {Reason}",
                        retryAttempt,
                        retryCount,
                        serviceName,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );

                    // Increment retry metrics
                    PollyMetrics.RetryCounter.Add(1, new KeyValuePair<string, object?>("service", serviceName));
                    PollyMetrics.RetryDelayHistogram.Record(timespan.TotalSeconds);
                }
            );
    }

    /// <summary>
    /// Simple circuit breaker policy that opens after a fixed number of consecutive failures.
    /// Stays open for a specified duration before allowing test requests (half-open state).
    /// </summary>
    /// <param name="failureThreshold">Number of consecutive failures before opening (default: 5)</param>
    /// <param name="durationOfBreakSeconds">How long to stay open in seconds (default: 30)</param>
    /// <param name="logger">Optional logger for circuit breaker events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetHttpCircuitBreakerPolicy(
        int failureThreshold = 5,
        int durationOfBreakSeconds = 30,
        ILogger? logger = null)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: failureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(durationOfBreakSeconds),
                onBreak: (outcome, duration, context) =>
                {
                    var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";

                    logger?.LogError(
                        "Circuit breaker OPENED for {ServiceName} for {Duration}s. Reason: {Reason}",
                        serviceName,
                        duration.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );

                    PollyMetrics.CircuitBreakerCounter.Add(1,
                        new KeyValuePair<string, object?>("service", serviceName),
                        new KeyValuePair<string, object?>("state", "open"));
                },
                onReset: (context) =>
                {
                    var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";
                    logger?.LogInformation("Circuit breaker RESET for {ServiceName}", serviceName);

                    PollyMetrics.CircuitBreakerCounter.Add(1,
                        new KeyValuePair<string, object?>("service", serviceName),
                        new KeyValuePair<string, object?>("state", "closed"));
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Circuit breaker HALF-OPEN (testing)");
                }
            );
    }

    /// <summary>
    /// Advanced circuit breaker that opens based on failure percentage rather than consecutive failures.
    /// More sophisticated than simple circuit breaker - recommended for production use.
    /// </summary>
    /// <param name="failureThreshold">Percentage of failures before opening (0.0 to 1.0, default: 0.5 = 50%)</param>
    /// <param name="samplingDurationSeconds">Time window for measuring failure rate (default: 10)</param>
    /// <param name="minimumThroughput">Minimum requests before evaluating failure rate (default: 8)</param>
    /// <param name="durationOfBreakSeconds">How long to stay open in seconds (default: 30)</param>
    /// <param name="logger">Optional logger for circuit breaker events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetAdvancedCircuitBreakerPolicy(
        double failureThreshold = 0.5,
        int samplingDurationSeconds = 10,
        int minimumThroughput = 8,
        int durationOfBreakSeconds = 30,
        ILogger? logger = null)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: failureThreshold,
                samplingDuration: TimeSpan.FromSeconds(samplingDurationSeconds),
                minimumThroughput: minimumThroughput,
                durationOfBreak: TimeSpan.FromSeconds(durationOfBreakSeconds),
                onBreak: (outcome, duration, context) =>
                {
                    var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";

                    logger?.LogError(
                        "Advanced circuit breaker OPENED for {ServiceName} for {Duration}s (>{Threshold:P} failures). Reason: {Reason}",
                        serviceName,
                        duration.TotalSeconds,
                        failureThreshold,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );

                    PollyMetrics.CircuitBreakerCounter.Add(1,
                        new KeyValuePair<string, object?>("service", serviceName),
                        new KeyValuePair<string, object?>("state", "open"),
                        new KeyValuePair<string, object?>("type", "advanced"));
                },
                onReset: (context) =>
                {
                    var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";
                    logger?.LogInformation("Advanced circuit breaker RESET for {ServiceName}", serviceName);

                    PollyMetrics.CircuitBreakerCounter.Add(1,
                        new KeyValuePair<string, object?>("service", serviceName),
                        new KeyValuePair<string, object?>("state", "closed"),
                        new KeyValuePair<string, object?>("type", "advanced"));
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Advanced circuit breaker HALF-OPEN (testing)");
                }
            );
    }

    /// <summary>
    /// Timeout policy to prevent hanging requests and ensure SLA compliance.
    /// Uses optimistic timeout strategy (relies on cooperative cancellation).
    /// </summary>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 10)</param>
    /// <param name="logger">Optional logger for timeout events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetHttpTimeoutPolicy(
        int timeoutSeconds = 10,
        ILogger? logger = null)
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            timeout: TimeSpan.FromSeconds(timeoutSeconds),
            timeoutStrategy: TimeoutStrategy.Optimistic, // Relies on CancellationToken
            onTimeoutAsync: (context, timespan, task) =>
            {
                var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";

                logger?.LogWarning(
                    "Request to {ServiceName} timed out after {Timeout}s",
                    serviceName,
                    timespan.TotalSeconds
                );

                PollyMetrics.TimeoutCounter.Add(1,
                    new KeyValuePair<string, object?>("service", serviceName));

                return Task.CompletedTask;
            }
        );
    }

    /// <summary>
    /// Bulkhead isolation policy to limit concurrent operations and prevent resource exhaustion.
    /// Implements the Bulkhead pattern to isolate resources for different operations.
    /// </summary>
    /// <param name="maxParallelization">Maximum concurrent operations (default: 10)</param>
    /// <param name="maxQueuingActions">Maximum queued operations (default: 20)</param>
    /// <param name="logger">Optional logger for bulkhead events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetBulkheadPolicy(
        int maxParallelization = 10,
        int maxQueuingActions = 20,
        ILogger? logger = null)
    {
        return Policy.BulkheadAsync<HttpResponseMessage>(
            maxParallelization: maxParallelization,
            maxQueuingActions: maxQueuingActions,
            onBulkheadRejectedAsync: context =>
            {
                var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";

                logger?.LogWarning(
                    "Bulkhead rejected request to {ServiceName} - too many concurrent operations",
                    serviceName
                );

                PollyMetrics.BulkheadRejectionCounter.Add(1,
                    new KeyValuePair<string, object?>("service", serviceName));

                return Task.CompletedTask;
            }
        );
    }

    /// <summary>
    /// Fallback policy for graceful degradation when operations fail.
    /// Returns a predefined fallback response instead of propagating the failure.
    /// </summary>
    /// <param name="fallbackResponse">The response to return on failure</param>
    /// <param name="logger">Optional logger for fallback events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetFallbackPolicy(
        HttpResponseMessage fallbackResponse,
        ILogger? logger = null)
    {
        return Policy<HttpResponseMessage>
            .Handle<Exception>()
            .OrResult(r => !r.IsSuccessStatusCode)
            .FallbackAsync(
                fallbackValue: fallbackResponse,
                onFallbackAsync: (result, context) =>
                {
                    var serviceName = context.TryGetValue("ServiceName", out var name) ? name : "Unknown";

                    logger?.LogWarning(
                        "Fallback activated for {ServiceName}. Reason: {Reason}",
                        serviceName,
                        result.Exception?.Message ?? result.Result?.StatusCode.ToString()
                    );

                    PollyMetrics.FallbackCounter.Add(1,
                        new KeyValuePair<string, object?>("service", serviceName));

                    return Task.CompletedTask;
                }
            );
    }

    /// <summary>
    /// Standard resilient policy combining timeout, retry, and circuit breaker.
    /// Execution order: Timeout → Retry → Circuit Breaker (outermost to innermost).
    /// This is the recommended policy for most HTTP client calls.
    /// </summary>
    /// <param name="logger">Optional logger for policy events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetStandardResilientPolicy(
        ILogger? logger = null)
    {
        var timeout = GetHttpTimeoutPolicy(10, logger);
        var retry = GetHttpRetryPolicy(3, logger);
        var circuitBreaker = GetHttpCircuitBreakerPolicy(5, 30, logger);

        // Wrap policies from outermost to innermost
        return Policy.WrapAsync(circuitBreaker, retry, timeout);
    }

    /// <summary>
    /// Resilient policy for critical operations with stricter limits and bulkhead isolation.
    /// Use for payment processing, order creation, or other high-value operations.
    /// </summary>
    /// <param name="logger">Optional logger for policy events</param>
    public static IAsyncPolicy<HttpResponseMessage> GetCriticalOperationPolicy(
        ILogger? logger = null)
    {
        var timeout = GetHttpTimeoutPolicy(15, logger); // Longer timeout for critical ops
        var retry = GetHttpRetryPolicy(2, logger); // Fewer retries
        var circuitBreaker = GetAdvancedCircuitBreakerPolicy(logger: logger);
        var bulkhead = GetBulkheadPolicy(5, 10, logger); // Stricter limits

        return Policy.WrapAsync(bulkhead, circuitBreaker, retry, timeout);
    }

    /// <summary>
    /// Custom resilient policy with configurable parameters.
    /// Allows fine-tuning of retry, circuit breaker, and timeout settings.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCustomResilientPolicy(
        int retryCount,
        int circuitBreakerThreshold,
        int timeoutSeconds,
        int circuitBreakerDurationSeconds = 30,
        ILogger? logger = null)
    {
        var timeout = GetHttpTimeoutPolicy(timeoutSeconds, logger);
        var retry = GetHttpRetryPolicy(retryCount, logger);
        var circuitBreaker = GetHttpCircuitBreakerPolicy(
            circuitBreakerThreshold,
            circuitBreakerDurationSeconds,
            logger
        );

        return Policy.WrapAsync(circuitBreaker, retry, timeout);
    }
}
