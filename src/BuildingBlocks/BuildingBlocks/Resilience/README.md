# Polly Resilience Patterns - Implementation Guide

This directory contains the centralized implementation of resilience patterns using **Polly** for the EshopMicroservices architecture.

## 📦 Contents

- **PollyPolicies.cs** - Core resilience policy definitions
- **HttpClientResilienceExtensions.cs** - Extension methods for HTTP clients
- **GrpcClientResilienceExtensions.cs** - Extension methods for gRPC clients
- **ResiliencePolicyOptions.cs** - Configuration classes for appsettings.json
- **PollyMetrics.cs** - Metrics and observability instrumentation

## 🎯 Implemented Patterns

### 1. **Retry Policy** (with Exponential Backoff + Jitter)
Automatically retries failed operations for transient failures.

- **Default**: 3 retry attempts
- **Backoff**: Exponential (2s, 4s, 8s)
- **Jitter**: Random 0-1000ms to prevent thundering herd
- **Handles**: HTTP 5xx, 408, TimeoutRejectedException

### 2. **Circuit Breaker**
Prevents cascading failures by temporarily stopping requests to failing services.

**Simple Circuit Breaker:**
- Opens after 5 consecutive failures
- Stays open for 30 seconds
- Allows 1 test request in half-open state

**Advanced Circuit Breaker:**
- Opens when 50% of requests fail (percentage-based)
- Evaluates over 10-second window
- Requires minimum 8 requests before evaluation

### 3. **Timeout Policy**
Cancels operations that take too long, ensuring SLA compliance.

- **Default**: 10 seconds for HTTP, 15 seconds for gRPC
- **Strategy**: Optimistic (relies on CancellationToken)

### 4. **Bulkhead Isolation**
Limits concurrent operations to prevent resource exhaustion.

- **Max Parallelization**: 10 concurrent requests
- **Max Queuing**: 20 queued requests
- Used in critical operations (e.g., OrderingService)

### 5. **Policy Wrapping**
Multiple policies combined in execution order:

**Standard HTTP Policy**: Circuit Breaker → Retry → Timeout
**Critical Operations**: Bulkhead → Circuit Breaker → Retry → Timeout

## 🚀 Usage

### HTTP Clients (Refit, HttpClient)

```csharp
using BuildingBlocks.Resilience;

// Standard resilience (recommended for most services)
builder.Services.AddRefitClient<ICatalogService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(gatewayUrl))
    .AddStandardResilience(serviceName: "CatalogService");

// Critical operations (stricter policies with bulkhead)
builder.Services.AddRefitClient<IOrderingService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(gatewayUrl))
    .AddCriticalResilience(serviceName: "OrderingService");

// Custom resilience (fine-tuned parameters)
builder.Services.AddHttpClient<IMyService>()
    .AddCustomResilience(
        retryCount: 5,
        circuitBreakerThreshold: 10,
        timeoutSeconds: 15,
        serviceName: "MyService"
    );

// Configuration-based (from appsettings.json)
builder.Services.AddRefitClient<IMyService>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(url))
    .AddConfiguredResilience(configuration, serviceName: "MyService");
```

### gRPC Clients

```csharp
using BuildingBlocks.Resilience;

// Standard gRPC resilience
builder.Services.AddGrpcClient<MyService.MyServiceClient>(options =>
{
    options.Address = new Uri(grpcUrl);
})
.AddGrpcResilience(serviceName: "MyGrpcService")
.ConfigureChannel(options =>
{
    options.MaxRetryAttempts = 3;
    options.MaxRetryBufferSize = 1024 * 1024; // 1MB
});

// Configuration-based gRPC resilience
builder.Services.AddGrpcClient<MyService.MyServiceClient>(options =>
{
    options.Address = new Uri(grpcUrl);
})
.AddConfiguredGrpcResilience(configuration, serviceName: "MyGrpcService");
```

## ⚙️ Configuration

Add to `appsettings.json`:

```json
{
  "ResiliencePolicies": {
    "HttpClient": {
      "RetryCount": 3,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationSeconds": 30,
      "TimeoutSeconds": 10,
      "MaxParallelization": 10,
      "MaxQueuingActions": 20
    },
    "GrpcClient": {
      "RetryCount": 3,
      "CircuitBreakerFailureThreshold": 0.5,
      "CircuitBreakerSamplingDurationSeconds": 10,
      "CircuitBreakerMinimumThroughput": 8,
      "CircuitBreakerDurationSeconds": 30,
      "TimeoutSeconds": 15
    }
  }
}
```

### Configuration Parameters

#### HTTP Client Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `RetryCount` | int | 3 | Number of retry attempts |
| `CircuitBreakerThreshold` | int | 5 | Consecutive failures before opening |
| `CircuitBreakerDurationSeconds` | int | 30 | How long circuit stays open |
| `TimeoutSeconds` | int | 10 | Request timeout |
| `MaxParallelization` | int | 10 | Max concurrent requests (bulkhead) |
| `MaxQueuingActions` | int | 20 | Max queued requests (bulkhead) |

#### gRPC Client Options

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `RetryCount` | int | 3 | Number of retry attempts |
| `CircuitBreakerFailureThreshold` | double | 0.5 | Failure percentage (0.0-1.0) |
| `CircuitBreakerSamplingDurationSeconds` | int | 10 | Time window for measurement |
| `CircuitBreakerMinimumThroughput` | int | 8 | Min requests before evaluation |
| `CircuitBreakerDurationSeconds` | int | 30 | How long circuit stays open |
| `TimeoutSeconds` | int | 15 | Request timeout |

## 📊 Metrics & Monitoring

The implementation includes built-in metrics using .NET Metrics API:

### Available Metrics

| Metric | Type | Tags | Description |
|--------|------|------|-------------|
| `resilience_retry_total` | Counter | service | Total retry attempts |
| `resilience_circuit_breaker_state_changes` | Counter | service, state, type | Circuit breaker state changes |
| `resilience_timeout_total` | Counter | service | Total timeouts |
| `resilience_bulkhead_rejection_total` | Counter | service | Bulkhead rejections |
| `resilience_fallback_total` | Counter | service | Fallback activations |
| `resilience_retry_delay_seconds` | Histogram | - | Retry delay distribution |
| `resilience_http_request_duration_ms` | Histogram | service, success | Request duration |

### Exporting Metrics

Metrics can be exported to:
- **Prometheus** (add OpenTelemetry.Exporter.Prometheus)
- **Application Insights** (add Azure.Monitor.OpenTelemetry)
- **Console** (for development/debugging)

Example setup:

```csharp
// In Program.cs
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Eshop.Resilience")
            .AddPrometheusExporter();
    });
```

## 📝 Current Implementation Status

### ✅ Implemented Services

1. **Shopping.Web**
   - `ICatalogService` → Standard resilience
   - `IBasketService` → Standard resilience
   - `IOrderingService` → Critical resilience (with bulkhead)

2. **Basket.API**
   - `DiscountProtoService.DiscountProtoServiceClient` (gRPC) → Standard gRPC resilience

### 🔜 Recommended Extensions

Other microservices that would benefit from resilience:

- **Catalog.API** - If it calls external services
- **Ordering.API** - For any HTTP/gRPC calls to external services
- **YarpApiGateway** - Already has rate limiting, could add retry policies

## 🧪 Testing Resilience

### Manual Testing

Create test endpoints to simulate failures:

```csharp
// In any API for testing
app.MapGet("/api/test/unstable", () =>
{
    // Randomly fail 50% of requests
    if (Random.Shared.Next(100) < 50)
    {
        throw new Exception("Simulated failure");
    }
    return Results.Ok(new { status = "success" });
});
```

### Chaos Engineering with Simmy

Install: `dotnet add package Polly.Contrib.Simmy`

```csharp
using Polly.Contrib.Simmy;
using Polly.Contrib.Simmy.Outcomes;

// Add chaos policy
var chaosPolicy = MonkeyPolicy.InjectException(with =>
    with.Fault(new Exception("Chaos!"))
        .InjectionRate(0.1) // 10% of requests
        .Enabled()
);

builder.Services.AddHttpClient<IMyService>()
    .AddPolicyHandler(chaosPolicy) // Add before resilience policies
    .AddStandardResilience();
```

### Load Testing

Use tools like:
- **k6** (https://k6.io/)
- **Apache JMeter**
- **Azure Load Testing**
- **Bombardier** (CLI tool)

Example k6 script:

```javascript
import http from 'k6/http';
import { check } from 'k6';

export let options = {
  stages: [
    { duration: '30s', target: 20 },
    { duration: '1m30s', target: 50 },
    { duration: '20s', target: 0 },
  ],
};

export default function() {
  let res = http.get('https://localhost:6064/api/catalog');
  check(res, {
    'status is 200': (r) => r.status === 200,
  });
}
```

## 📚 Best Practices

### ✅ DO:

1. **Use different policies for different services** based on criticality
2. **Log policy events** to understand failure patterns
3. **Monitor circuit breaker state** in your observability platform
4. **Test resilience** regularly with chaos engineering
5. **Configure timeouts** based on actual SLA requirements
6. **Use jitter** in retry delays to prevent thundering herd
7. **Combine policies** for comprehensive protection

### ❌ DON'T:

1. **Don't retry non-idempotent operations** without careful consideration
2. **Don't use infinite retries** - always have a max count
3. **Don't use the same policy for all services** - customize per service
4. **Don't ignore circuit breaker open state** - alert on it
5. **Don't retry on non-transient errors** (400, 401, 403, 404)
6. **Don't skip timeout policies** - always have a timeout
7. **Don't forget to test** - untested resilience is no resilience

## 🔍 Troubleshooting

### Issue: Requests timing out too quickly

**Solution**: Increase `TimeoutSeconds` in configuration or use custom policy:

```csharp
.AddCustomResilience(timeoutSeconds: 20)
```

### Issue: Too many retries causing delays

**Solution**: Reduce `RetryCount` or use exponential backoff:

```csharp
.AddCustomResilience(retryCount: 2)
```

### Issue: Circuit breaker opening too aggressively

**Solution**: Increase threshold or use advanced circuit breaker:

```csharp
.AddCustomResilience(circuitBreakerThreshold: 10)
```

### Issue: Bulkhead rejecting too many requests

**Solution**: Increase `MaxParallelization` and `MaxQueuingActions`:

```json
{
  "ResiliencePolicies": {
    "HttpClient": {
      "MaxParallelization": 20,
      "MaxQueuingActions": 40
    }
  }
}
```

## 📖 References

- **Polly Documentation**: https://www.pollydocs.org/
- **Polly GitHub**: https://github.com/App-vNext/Polly
- **Microsoft Docs**: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/
- **Circuit Breaker Pattern**: https://martinfowler.com/bliki/CircuitBreaker.html
- **Release It! Book**: Michael T. Nygard (excellent resource on resilience patterns)

## 📞 Support

For issues or questions about this implementation:

1. Check the troubleshooting section above
2. Review Polly documentation
3. Check application logs for policy events
4. Monitor metrics in your observability platform

---

**Implementation Date**: November 2025
**Polly Version**: 8.4.2
**Microsoft.Extensions.Http.Polly Version**: 8.0.0
