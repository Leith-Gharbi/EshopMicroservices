using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace BuildingBlocks.Resilience;

/// <summary>
/// Extension methods for adding resilience policies to HttpClient instances.
/// Simplifies the integration of Polly policies with IHttpClientBuilder.
/// </summary>
public static class HttpClientResilienceExtensions
{
    /// <summary>
    /// Adds standard resilience policies to HttpClient.
    /// Includes: Circuit Breaker → Retry → Timeout (execution order).
    /// Recommended for most HTTP client scenarios.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder to add policies to</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddStandardResilience(
        this IHttpClientBuilder builder,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var policy = PollyPolicies.GetStandardResilientPolicy(logger);

        return builder.AddPolicyHandler((request) =>
        {
            // Add service name to context for logging
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return policy;
        });
    }

    /// <summary>
    /// Adds resilience policies optimized for critical operations.
    /// Includes: Bulkhead → Circuit Breaker → Retry → Timeout.
    /// Use for high-value operations like payments, order creation, etc.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder to add policies to</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddCriticalResilience(
        this IHttpClientBuilder builder,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var policy = PollyPolicies.GetCriticalOperationPolicy(logger);

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return policy;
        });
    }

    /// <summary>
    /// Adds custom resilience policies with configurable parameters.
    /// Allows fine-tuning of retry count, circuit breaker threshold, and timeout.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder to add policies to</param>
    /// <param name="retryCount">Number of retry attempts (default: 3)</param>
    /// <param name="circuitBreakerThreshold">Consecutive failures before opening circuit (default: 5)</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 10)</param>
    /// <param name="circuitBreakerDurationSeconds">How long circuit stays open (default: 30)</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddCustomResilience(
        this IHttpClientBuilder builder,
        int retryCount = 3,
        int circuitBreakerThreshold = 5,
        int timeoutSeconds = 10,
        int circuitBreakerDurationSeconds = 30,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var policy = PollyPolicies.GetCustomResilientPolicy(
            retryCount,
            circuitBreakerThreshold,
            timeoutSeconds,
            circuitBreakerDurationSeconds,
            logger
        );

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return policy;
        });
    }

    /// <summary>
    /// Adds resilience policies configured from appsettings.json.
    /// Reads configuration from ResiliencePolicies:HttpClient section.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder to add policies to</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddConfiguredResilience(
        this IHttpClientBuilder builder,
        IConfiguration configuration,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var options = new ResiliencePolicyOptions();
        configuration.GetSection(ResiliencePolicyOptions.SectionName).Bind(options);

        var httpOptions = options.HttpClient;

        var policy = PollyPolicies.GetCustomResilientPolicy(
            httpOptions.RetryCount,
            httpOptions.CircuitBreakerThreshold,
            httpOptions.TimeoutSeconds,
            httpOptions.CircuitBreakerDurationSeconds,
            logger
        );

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return policy;
        });
    }

    /// <summary>
    /// Adds only retry policy (without circuit breaker or timeout).
    /// Use when you need just retry logic without other resilience patterns.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder to add policies to</param>
    /// <param name="retryCount">Number of retry attempts (default: 3)</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddRetryOnly(
        this IHttpClientBuilder builder,
        int retryCount = 3,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var policy = PollyPolicies.GetHttpRetryPolicy(retryCount, logger);

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return policy;
        });
    }

    /// <summary>
    /// Adds only circuit breaker policy (without retry or timeout).
    /// Use when you want to protect against cascading failures without retries.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder to add policies to</param>
    /// <param name="failureThreshold">Consecutive failures before opening (default: 5)</param>
    /// <param name="durationOfBreakSeconds">How long to stay open (default: 30)</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddCircuitBreakerOnly(
        this IHttpClientBuilder builder,
        int failureThreshold = 5,
        int durationOfBreakSeconds = 30,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var policy = PollyPolicies.GetHttpCircuitBreakerPolicy(
            failureThreshold,
            durationOfBreakSeconds,
            logger
        );

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return policy;
        });
    }

    /// <summary>
    /// Adds only timeout policy (without retry or circuit breaker).
    /// Use when you need to enforce SLA timeouts without other resilience patterns.
    /// </summary>
    /// <param name="builder">The IHttpClientBuilder to add policies to</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default: 10)</param>
    /// <param name="serviceName">Optional service name for logging and metrics</param>
    /// <param name="logger">Optional logger for policy events</param>
    /// <returns>The IHttpClientBuilder for method chaining</returns>
    public static IHttpClientBuilder AddTimeoutOnly(
        this IHttpClientBuilder builder,
        int timeoutSeconds = 10,
        string? serviceName = null,
        ILogger? logger = null)
    {
        var policy = PollyPolicies.GetHttpTimeoutPolicy(timeoutSeconds, logger);

        return builder.AddPolicyHandler((request) =>
        {
            var context = new Context($"{request.Method}:{request.RequestUri?.PathAndQuery}");
            if (!string.IsNullOrEmpty(serviceName))
            {
                context["ServiceName"] = serviceName;
            }
            return policy;
        });
    }
}
