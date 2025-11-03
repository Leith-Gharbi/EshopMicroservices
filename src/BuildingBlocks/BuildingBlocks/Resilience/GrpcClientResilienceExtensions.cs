using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace BuildingBlocks.Resilience;

/// <summary>
/// Extension methods for adding resilience policies to gRPC clients.
/// Provides Polly-based retry, circuit breaker, and timeout policies specifically for gRPC.
/// </summary>
public static class GrpcClientResilienceExtensions
{
    /// <summary>
    /// Adds standard resilience policies to gRPC client.
    /// Includes: Advanced Circuit Breaker → Retry → Timeout.
    /// Uses advanced circuit breaker suitable for gRPC communication patterns.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder for the gRPC client</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddGrpcResilience(
        this IHttpClientBuilder builder,
        string? serviceName = null,
        ILogger? logger = null)
    {
        // gRPC-specific retry policy with exponential backoff
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) // 2, 4, 8 seconds
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";

                    logger?.LogWarning(
                        "gRPC Retry {RetryAttempt}/3 to {ServiceName} after {Delay}s. Reason: {Reason}",
                        retryAttempt,
                        svcName,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );

                    PollyMetrics.RetryCounter.Add(1,
                        new KeyValuePair<string, object?>("service", svcName),
                        new KeyValuePair<string, object?>("protocol", "grpc"));
                }
            );

        // Advanced circuit breaker for gRPC (percentage-based)
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5, // Break if 50% of requests fail
                samplingDuration: TimeSpan.FromSeconds(10), // Over 10 second window
                minimumThroughput: 8, // At least 8 requests before evaluating
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration, context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";

                    logger?.LogError(
                        "gRPC Circuit breaker OPENED for {ServiceName} for {Duration}s. Reason: {Reason}",
                        svcName,
                        duration.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()
                    );

                    PollyMetrics.CircuitBreakerCounter.Add(1,
                        new KeyValuePair<string, object?>("service", svcName),
                        new KeyValuePair<string, object?>("state", "open"),
                        new KeyValuePair<string, object?>("protocol", "grpc"));
                },
                onReset: (context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";
                    logger?.LogInformation("gRPC Circuit breaker RESET for {ServiceName}", svcName);

                    PollyMetrics.CircuitBreakerCounter.Add(1,
                        new KeyValuePair<string, object?>("service", svcName),
                        new KeyValuePair<string, object?>("state", "closed"),
                        new KeyValuePair<string, object?>("protocol", "grpc"));
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("gRPC Circuit breaker HALF-OPEN (testing)");
                }
            );

        // Add policies to gRPC client
        return builder
            .AddPolicyHandler((request) =>
            {
                var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
                if (!string.IsNullOrEmpty(serviceName))
                {
                    context["ServiceName"] = serviceName;
                }
                // Wrap: Circuit Breaker → Retry (timeout handled by gRPC channel configuration)
                return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
            });
    }

    /// <summary>
    /// Adds resilience policies for gRPC client configured from appsettings.json.
    /// Reads configuration from ResiliencePolicies:GrpcClient section.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder for the gRPC client</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddConfiguredGrpcResilience(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var options = new ResiliencePolicyOptions();
        configuration.GetSection(ResiliencePolicyOptions.SectionName).Bind(options);

        var grpcOptions = options.GrpcClient;

        // Custom retry policy from configuration
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: grpcOptions.RetryCount,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";

                    logger?.LogWarning(
                        "gRPC Retry {RetryAttempt}/{MaxRetries} to {ServiceName} after {Delay}s",
                        retryAttempt,
                        grpcOptions.RetryCount,
                        svcName,
                        timespan.TotalSeconds
                    );
                }
            );

        // Advanced circuit breaker from configuration
        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: grpcOptions.CircuitBreakerFailureThreshold,
                samplingDuration: TimeSpan.FromSeconds(grpcOptions.CircuitBreakerSamplingDurationSeconds),
                minimumThroughput: grpcOptions.CircuitBreakerMinimumThroughput,
                durationOfBreak: TimeSpan.FromSeconds(grpcOptions.CircuitBreakerDurationSeconds),
                onBreak: (outcome, duration, context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";
                    logger?.LogError(
                        "gRPC Circuit breaker OPENED for {ServiceName} for {Duration}s",
                        svcName,
                        duration.TotalSeconds
                    );
                },
                onReset: (context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";
                    logger?.LogInformation("gRPC Circuit breaker RESET for {ServiceName}", svcName);
                },
                onHalfOpen: () => logger?.LogInformation("gRPC Circuit breaker HALF-OPEN")
            );

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
        });
    }

    /// <summary>
    /// Adds custom resilience policies to gRPC client with specific parameters.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder for the gRPC client</param>
    /// <param name="retryCount">Number of retry attempts</param>
    /// <param name="failureThreshold">Failure percentage (0.0 to 1.0)</param>
    /// <param name="samplingDurationSeconds">Time window for measuring failures</param>
    /// <param name="minimumThroughput">Minimum requests before evaluating</param>
    /// <param name="durationOfBreakSeconds">How long circuit stays open</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddCustomGrpcResilience(
        this IHttpClientBuilder builder,
        int retryCount = 3,
        double failureThreshold = 0.5,
        int samplingDurationSeconds = 10,
        int minimumThroughput = 8,
        int durationOfBreakSeconds = 30,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)),
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";
                    logger?.LogWarning("gRPC Retry {RetryAttempt} to {ServiceName}", retryAttempt, svcName);
                }
            );

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold,
                TimeSpan.FromSeconds(samplingDurationSeconds),
                minimumThroughput,
                TimeSpan.FromSeconds(durationOfBreakSeconds),
                onBreak: (outcome, duration, context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";
                    logger?.LogError("gRPC Circuit breaker OPENED for {ServiceName}", svcName);
                },
                onReset: (context) =>
                {
                    var svcName = context.TryGetValue("ServiceName", out var name) ? name : serviceName ?? "gRPC";
                    logger?.LogInformation("gRPC Circuit breaker RESET for {ServiceName}", svcName);
                },
                onHalfOpen: () => logger?.LogInformation("gRPC Circuit breaker HALF-OPEN")
            );

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy);
        });
    }
}
