namespace BuildingBlocks.Resilience;

/// <summary>
/// Configuration options for resilience policies.
/// Bind this from appsettings.json section "ResiliencePolicies".
/// </summary>
public class ResiliencePolicyOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "ResiliencePolicies";

    /// <summary>
    /// HTTP client resilience policy settings
    /// </summary>
    public HttpClientPolicyOptions HttpClient { get; set; } = new();

    /// <summary>
    /// gRPC client resilience policy settings
    /// </summary>
    public GrpcClientPolicyOptions GrpcClient { get; set; } = new();
}

/// <summary>
/// Configuration options for HTTP client resilience policies
/// </summary>
public class HttpClientPolicyOptions
{
    /// <summary>
    /// Number of retry attempts for transient failures (default: 3)
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Number of consecutive failures before opening circuit breaker (default: 5)
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 5;

    /// <summary>
    /// Duration in seconds that circuit breaker stays open (default: 30)
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout in seconds for individual HTTP requests (default: 10)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum number of concurrent requests (bulkhead pattern, default: 10)
    /// </summary>
    public int MaxParallelization { get; set; } = 10;

    /// <summary>
    /// Maximum number of queued requests (bulkhead pattern, default: 20)
    /// </summary>
    public int MaxQueuingActions { get; set; } = 20;
}

/// <summary>
/// Configuration options for gRPC client resilience policies
/// </summary>
public class GrpcClientPolicyOptions
{
    /// <summary>
    /// Number of retry attempts for transient failures (default: 3)
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// Failure percentage threshold (0.0 to 1.0) for advanced circuit breaker (default: 0.5 = 50%)
    /// </summary>
    public double CircuitBreakerFailureThreshold { get; set; } = 0.5;

    /// <summary>
    /// Time window in seconds for measuring failure rate (default: 10)
    /// </summary>
    public int CircuitBreakerSamplingDurationSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum number of requests before evaluating failure rate (default: 8)
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 8;

    /// <summary>
    /// Duration in seconds that circuit breaker stays open (default: 30)
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout in seconds for individual gRPC requests (default: 15)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// Service-specific resilience configuration.
/// Allows different resilience settings for different services.
/// </summary>
public class ServiceResilienceOptions
{
    /// <summary>
    /// Service name identifier
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Number of retry attempts (overrides default)
    /// </summary>
    public int? RetryCount { get; set; }

    /// <summary>
    /// Circuit breaker threshold (overrides default)
    /// </summary>
    public int? CircuitBreakerThreshold { get; set; }

    /// <summary>
    /// Timeout in seconds (overrides default)
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Whether to enable bulkhead isolation for this service
    /// </summary>
    public bool EnableBulkhead { get; set; }

    /// <summary>
    /// Whether to use advanced circuit breaker (percentage-based) instead of simple
    /// </summary>
    public bool UseAdvancedCircuitBreaker { get; set; }
}
