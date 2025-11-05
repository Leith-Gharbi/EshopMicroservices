# Correlation ID Implementation

## Table of Contents
- [Overview](#overview)
- [Why Correlation ID?](#why-correlation-id)
- [Architecture](#architecture)
- [Components](#components)
- [How It Works](#how-it-works)
- [Usage Examples](#usage-examples)
- [Configuration](#configuration)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)
- [Best Practices](#best-practices)

## Overview

This document describes the **Correlation ID mechanism** implemented across all microservices in the EshopMicroservices solution. A correlation ID is a unique identifier that traces a single request through multiple services, making it easy to debug and monitor distributed systems.

### What is a Correlation ID?

A correlation ID (also called request ID or trace ID) is a **unique identifier** attached to every request that flows through your system. It allows you to:
- Track a request across multiple microservices
- Correlate logs from different services
- Debug issues faster
- Monitor end-to-end request flow

### Example Scenario

```
Client Request → API Gateway → Catalog Service → Basket Service → Discount Service (gRPC)
                                      ↓
                                Message Bus → Ordering Service
```

**Without Correlation ID**: Logs are scattered, hard to trace which logs belong to the same request.

**With Correlation ID**: All logs contain the same correlation ID, making it easy to filter and trace the entire request flow.

## Why Correlation ID?

### Benefits

1. **Distributed Tracing**: Track requests across multiple microservices
2. **Easier Debugging**: Find all logs related to a specific request in Elasticsearch/Kibana
3. **Better Support**: Customers can provide correlation ID from response headers for support tickets
4. **Performance Monitoring**: Measure end-to-end latency across services
5. **Error Investigation**: Quickly find what went wrong in a multi-service flow

## Architecture

### High-Level Flow

```
┌─────────────┐
│   Client    │
└──────┬──────┘
       │ X-Correlation-Id: abc123 (optional)
       ▼
┌─────────────────────────────────────────┐
│   API Gateway (YarpApiGateway)          │
│   - Receives or generates correlation ID │
│   - Adds to response headers             │
└──────┬──────────────────────────────────┘
       │ X-Correlation-Id: abc123
       ▼
┌─────────────────────────────────────────┐
│   Microservices                          │
│   - HTTP calls (with handler)            │
│   - gRPC calls (with interceptors)       │
│   - Message bus (with filters)           │
│   - All logs include correlation ID      │
└──────────────────────────────────────────┘
       │
       ▼
┌─────────────────────────────────────────┐
│   Elasticsearch                          │
│   - All logs tagged with correlation ID  │
│   - Query: CorrelationId: "abc123"      │
└──────────────────────────────────────────┘
```

## Components

### 1. Core Infrastructure (BuildingBlocks)

Located in `BuildingBlocks/BuildingBlocks/Logging/`:

#### **ICorrelationIdAccessor** & **CorrelationIdAccessor**
- **Purpose**: Provide thread-safe access to the current correlation ID
- **Implementation**: Uses `AsyncLocal<T>` for async context preservation
- **Location**: `ICorrelationIdAccessor.cs`, `CorrelationIdAccessor.cs`

```csharp
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
    void SetCorrelationId(string correlationId);
}
```

#### **HttpLoggingMiddleware** (Enhanced)
- **Purpose**: Entry point for HTTP requests
- **Features**:
  - Extracts `X-Correlation-Id` from request headers
  - Generates new ID if not provided (TraceIdentifier or GUID)
  - Stores in `ICorrelationIdAccessor`
  - Adds to response headers
  - Enriches logs with correlation ID
- **Location**: `HttpLoggingMiddleware.cs:28-49`

#### **CorrelationIdDelegatingHandler**
- **Purpose**: Propagate correlation ID in outgoing HTTP calls
- **Usage**: Automatically added to HttpClient pipeline
- **Location**: `CorrelationIdDelegatingHandler.cs`

#### **CorrelationIdGrpcInterceptor** (Client)
- **Purpose**: Add correlation ID to outgoing gRPC calls
- **Usage**: Registered with gRPC client
- **Location**: `CorrelationIdGrpcInterceptor.cs`

#### **CorrelationIdGrpcServerInterceptor** (Server)
- **Purpose**: Extract correlation ID from incoming gRPC calls
- **Usage**: Registered with gRPC server
- **Location**: `CorrelationIdGrpcServerInterceptor.cs`

#### **CorrelationIdExtensions**
- **Purpose**: Easy registration of all correlation ID services
- **Location**: `CorrelationIdExtensions.cs`

### 2. Message Bus Integration (BuildingBlocks.Messaging)

Located in `BuildingBlocks/BuildingBlocks.Messaging/MassTransit/`:

#### **CorrelationIdPublishFilter**
- **Purpose**: Add correlation ID to published events (domain events)
- **Location**: `CorrelationIdPublishFilter.cs`

#### **CorrelationIdSendFilter**
- **Purpose**: Add correlation ID to sent commands
- **Location**: `CorrelationIdSendFilter.cs`

#### **CorrelationIdConsumeFilter**
- **Purpose**: Extract correlation ID from incoming messages
- **Location**: `CorrelationIdConsumeFilter.cs`

### 3. Microservices Integration

All microservices have been updated:

- **Basket.API** (`Program.cs:13` & `Program.cs:71`)
- **Catalog.API** (`Program.cs:18`)
- **Ordering.API** (`Program.cs:13`)
- **Discount.Grpc** (`Program.cs:15` & `Program.cs:20`)
- **YarpApiGateway** (`Program.cs:12`)

## How It Works

### 1. Incoming Request Flow

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Client sends request                                     │
│    GET /api/products                                        │
│    Header: X-Correlation-Id: abc123 (optional)              │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. HttpLoggingMiddleware                                    │
│    - Checks for X-Correlation-Id header                     │
│    - If present: Use it                                     │
│    - If missing: Generate new (TraceIdentifier or GUID)     │
│    - Store in ICorrelationIdAccessor                        │
│    - Add to LogContext for Serilog                          │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Request Processing                                       │
│    - All logs automatically include correlation ID          │
│    - Business logic can access via ICorrelationIdAccessor   │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Response                                                 │
│    - X-Correlation-Id added to response headers             │
│    - Client receives correlation ID for reference           │
└─────────────────────────────────────────────────────────────┘
```

### 2. Outgoing HTTP Call Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Service A makes HTTP call to Service B                      │
│ var response = await httpClient.GetAsync("/api/endpoint");  │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ CorrelationIdDelegatingHandler                              │
│    - Gets correlation ID from ICorrelationIdAccessor        │
│    - Adds X-Correlation-Id header to request                │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ Service B receives request with correlation ID              │
│    - HttpLoggingMiddleware extracts correlation ID          │
│    - All logs in Service B have same correlation ID         │
└─────────────────────────────────────────────────────────────┘
```

### 3. gRPC Call Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Basket Service calls Discount Service (gRPC)                │
│ var discount = await grpcClient.GetDiscountAsync();         │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ CorrelationIdGrpcInterceptor (Client)                       │
│    - Gets correlation ID from ICorrelationIdAccessor        │
│    - Adds x-correlation-id metadata to gRPC call            │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ CorrelationIdGrpcServerInterceptor (Server)                 │
│    - Extracts x-correlation-id from metadata               │
│    - Stores in ICorrelationIdAccessor                       │
│    - All logs include correlation ID                        │
└─────────────────────────────────────────────────────────────┘
```

### 4. Message Bus Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Basket Service publishes BasketCheckoutEvent                │
│ await publishEndpoint.Publish(basketCheckoutEvent);         │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ CorrelationIdPublishFilter                                  │
│    - Gets correlation ID from ICorrelationIdAccessor        │
│    - Adds X-Correlation-Id to message headers               │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ RabbitMQ                                                    │
│    - Message contains correlation ID in headers             │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ Ordering Service consumes BasketCheckoutEvent               │
└────────────────────────┬────────────────────────────────────┘
                         ▼
┌─────────────────────────────────────────────────────────────┐
│ CorrelationIdConsumeFilter                                  │
│    - Extracts X-Correlation-Id from message headers         │
│    - Stores in ICorrelationIdAccessor                       │
│    - All logs include same correlation ID                   │
└─────────────────────────────────────────────────────────────┘
```

## Usage Examples

### 1. Access Correlation ID in Your Code

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<CreateOrderHandler> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        _logger.LogInformation(
            "Creating order for user {UserId} with correlation ID: {CorrelationId}",
            command.UserId,
            correlationId);

        // Your business logic...

        return new CreateOrderResult { OrderId = newOrderId };
    }
}
```

### 2. Add Correlation ID to Custom HttpClient

If you register a custom HttpClient in the future:

```csharp
// In Program.cs or DependencyInjection.cs
builder.Services.AddHttpClient<IProductService, ProductService>(client =>
{
    client.BaseAddress = new Uri("https://product-api.example.com");
})
.AddCorrelationIdHandler() // <-- Adds correlation ID to all outgoing requests
.AddHttpResilience(); // Your existing resilience policies
```

### 3. Testing with Postman/cURL

**Send request with correlation ID:**
```bash
curl -H "X-Correlation-Id: test-123" http://localhost:5000/api/products
```

**Send request without correlation ID (auto-generated):**
```bash
curl http://localhost:5000/api/products
```

**Check response headers:**
```bash
curl -v http://localhost:5000/api/products
# Look for: X-Correlation-Id: <generated-id>
```

## Configuration

### Registering Correlation ID Services

All microservices already have this configured in their `Program.cs`:

```csharp
// Add Correlation ID services
builder.Services.AddCorrelationId();
```

This registers:
- `ICorrelationIdAccessor` as singleton
- `CorrelationIdDelegatingHandler` as transient
- `CorrelationIdGrpcInterceptor` for gRPC clients
- `CorrelationIdGrpcServerInterceptor` for gRPC servers

### HTTP Logging Middleware

Ensure this middleware is registered **early** in the pipeline:

```csharp
// In Program.cs (after app.Build())
app.UseElasticsearchHttpLogging(); // Already configured
```

### gRPC Client Configuration

For gRPC clients (like Discount service in Basket.API):

```csharp
builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]!);
})
.AddInterceptor<CorrelationIdGrpcInterceptor>(); // Already configured
```

### gRPC Server Configuration

For gRPC servers (like Discount.Grpc):

```csharp
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<CorrelationIdGrpcServerInterceptor>(); // Already configured
});
```

### MassTransit Configuration

Already configured in `BuildingBlocks.Messaging/MassTransit/Extensions.cs`:

```csharp
configurator.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);
configurator.UseSendFilter(typeof(CorrelationIdSendFilter<>), context);
configurator.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);
```

## Testing

### 1. Test End-to-End Flow

**Scenario**: Create an order through the API Gateway

```bash
# 1. Send request with custom correlation ID
curl -X POST http://localhost:8000/basket-service/basket/checkout \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: test-order-12345" \
  -d '{"userName": "testuser"}'

# 2. Check response headers - should return same correlation ID
# Response Headers:
# X-Correlation-Id: test-order-12345
```

### 2. Verify in Elasticsearch/Kibana

**Query logs for specific correlation ID:**

```
CorrelationId: "test-order-12345"
```

**Expected results**: You should see logs from:
- API Gateway
- Basket Service
- Discount Service (gRPC call)
- Ordering Service (message bus)

### 3. Test Auto-Generation

```bash
# Send request WITHOUT correlation ID
curl http://localhost:5000/api/products

# Check response headers - should have auto-generated correlation ID
# Response Headers:
# X-Correlation-Id: <auto-generated-guid>
```

### 4. Verify gRPC Propagation

Enable debug logging to see gRPC correlation IDs:

```json
// appsettings.Development.json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "BuildingBlocks.Logging.CorrelationIdGrpcServerInterceptor": "Debug"
      }
    }
  }
}
```

Expected log:
```
[DEBUG] gRPC Request received with Correlation ID: abc123 for method: /discount.DiscountProtoService/GetDiscount
```

## Troubleshooting

### Correlation ID Not Appearing in Logs

**Problem**: Logs don't contain correlation ID

**Solutions**:
1. Verify `UseElasticsearchHttpLogging()` is called in `Program.cs`
2. Check that `AddCorrelationId()` is registered in DI
3. Ensure middleware is called early in pipeline (before endpoints)

### Correlation ID Not Propagated to Downstream Services

**Problem**: Each service has different correlation ID

**Solutions**:

**For HTTP calls:**
```csharp
// Ensure HttpClient has the handler
builder.Services.AddHttpClient<IMyService, MyService>()
    .AddCorrelationIdHandler(); // Add this!
```

**For gRPC calls:**
```csharp
// Ensure interceptor is registered
builder.Services.AddGrpcClient<MyGrpcService>(...)
    .AddInterceptor<CorrelationIdGrpcInterceptor>(); // Add this!
```

**For Message Bus:**
- Already configured in `Extensions.cs`
- Verify `BuildingBlocks.Messaging` is referenced

### Correlation ID is NULL in Code

**Problem**: `ICorrelationIdAccessor.CorrelationId` returns null

**Solutions**:
1. Verify the request came through `HttpLoggingMiddleware`
2. For background jobs/startup code: Manually set correlation ID:
```csharp
_correlationIdAccessor.SetCorrelationId(Guid.NewGuid().ToString());
```

### Different Header Names

**Problem**: External services use different header names

**Solution**: Modify `HttpLoggingMiddleware.cs` to check multiple headers:

```csharp
var correlationId = request.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? request.Headers["X-Request-Id"].FirstOrDefault()
                    ?? request.Headers["traceparent"].FirstOrDefault()
                    ?? context.TraceIdentifier
                    ?? Guid.NewGuid().ToString();
```

## Best Practices

### 1. Always Use ICorrelationIdAccessor

**Good:**
```csharp
public class MyService
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public MyService(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    public void DoWork()
    {
        var correlationId = _correlationIdAccessor.CorrelationId;
        // Use it...
    }
}
```

**Bad:**
```csharp
// Don't pass correlation ID as method parameters everywhere
public void DoWork(string correlationId) // ❌ Avoid this pattern
{
    // ...
}
```

### 2. Log Correlation ID in Critical Operations

```csharp
_logger.LogInformation(
    "Payment processed for order {OrderId} with correlation ID: {CorrelationId}",
    orderId,
    _correlationIdAccessor.CorrelationId);
```

### 3. Include Correlation ID in Error Responses

```csharp
public class CustomExceptionHandler : IExceptionHandler
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Detail = exception.Message,
            Extensions =
            {
                ["correlationId"] = correlationId, // <-- Add to response
                ["traceId"] = httpContext.TraceIdentifier
            }
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
```

### 4. Use Correlation ID in Support Tickets

Train your support team to ask customers for the correlation ID from error responses. This makes debugging much faster.

### 5. Set Retention Policies

In Elasticsearch, ensure you have appropriate retention policies based on correlation ID usage:

```json
{
  "index_patterns": ["eshop-microservices-*"],
  "settings": {
    "number_of_shards": 2,
    "number_of_replicas": 1,
    "index.lifecycle.name": "eshop-logs-policy",
    "index.lifecycle.rollover_alias": "eshop-logs"
  }
}
```

### 6. Monitor Correlation ID Coverage

Create Kibana dashboards to monitor:
- Percentage of requests with correlation IDs
- Most frequently used correlation IDs (potential retry storms)
- Average response time per correlation ID

## Kibana Query Examples

### Find All Logs for a Request

```
CorrelationId: "abc123"
```

### Find Slow Requests Across All Services

```
CorrelationId: * AND ResponseTimeMs: > 3000
```

### Find Errors in a Specific Flow

```
CorrelationId: "abc123" AND Level: "Error"
```

### Find All gRPC Calls for a Request

```
CorrelationId: "abc123" AND Message: "gRPC"
```

### Find All Message Bus Events for a Request

```
CorrelationId: "abc123" AND Message: "Message received"
```

### Visualize Request Flow

Create a Kibana timeline visualization:
- X-axis: Timestamp
- Y-axis: ApplicationName
- Filter: CorrelationId: "abc123"

This shows the chronological flow of a request through services.

## Summary

The correlation ID implementation provides:

✅ **Full traceability** across HTTP, gRPC, and Message Bus
✅ **Automatic propagation** - no manual header management
✅ **Thread-safe access** via `ICorrelationIdAccessor`
✅ **Elasticsearch integration** - all logs tagged with correlation ID
✅ **Client support** - external clients can provide/receive correlation IDs
✅ **Easy debugging** - query logs by correlation ID in Kibana

All microservices are now equipped with this infrastructure and ready to trace distributed requests end-to-end!

---

**Need Help?**
- Check existing logs in Kibana with `CorrelationId: *`
- Verify middleware registration in `Program.cs`
- Review component documentation in source files
