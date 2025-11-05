using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace BuildingBlocks.Logging;

/// <summary>
/// Middleware to enrich logs with HTTP request and response data for Elasticsearch/Kibana analysis
/// </summary>
public class HttpLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HttpLoggingMiddleware> _logger;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public HttpLoggingMiddleware(
        RequestDelegate next,
        ILogger<HttpLoggingMiddleware> logger,
        ICorrelationIdAccessor correlationIdAccessor)
    {
        _next = next;
        _logger = logger;
        _correlationIdAccessor = correlationIdAccessor;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var request = context.Request;

        // Check for correlation ID in request header, generate if not present
        var correlationId = request.Headers[CorrelationIdHeaderName].FirstOrDefault()
                            ?? context.TraceIdentifier
                            ?? Guid.NewGuid().ToString();

        // Store correlation ID for access throughout the request pipeline
        _correlationIdAccessor.SetCorrelationId(correlationId);

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.TryAdd(CorrelationIdHeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        // Capture original response body stream
        var originalBodyStream = context.Response.Body;

        try
        {
            // Enrich log context with HTTP request data
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("HttpMethod", request.Method))
            using (LogContext.PushProperty("RequestPath", request.Path.Value))
            using (LogContext.PushProperty("RequestQueryString", request.QueryString.Value))
            using (LogContext.PushProperty("RequestScheme", request.Scheme))
            using (LogContext.PushProperty("RequestHost", request.Host.Value))
            using (LogContext.PushProperty("ClientIpAddress", GetClientIpAddress(context)))
            using (LogContext.PushProperty("UserAgent", request.Headers["User-Agent"].ToString()))
            using (LogContext.PushProperty("RequestContentType", request.ContentType))
            using (LogContext.PushProperty("RequestContentLength", request.ContentLength ?? 0))
            {
                // Log request start
                _logger.LogInformation(
                    "HTTP Request Started: {Method} {Path} from {ClientIp}",
                    request.Method,
                    request.Path,
                    GetClientIpAddress(context));

                // Call the next middleware in the pipeline
                await _next(context);

                stopwatch.Stop();

                // Capture response data
                var response = context.Response;
                var statusCode = response.StatusCode;
                var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                // Enrich log context with HTTP response data
                using (LogContext.PushProperty("HttpStatusCode", statusCode))
                using (LogContext.PushProperty("ResponseContentType", response.ContentType))
                using (LogContext.PushProperty("ResponseTimeMs", elapsedMilliseconds))
                using (LogContext.PushProperty("IsSuccessStatusCode", statusCode >= 200 && statusCode < 300))
                using (LogContext.PushProperty("IsClientError", statusCode >= 400 && statusCode < 500))
                using (LogContext.PushProperty("IsServerError", statusCode >= 500))
                {
                    // Determine log level based on status code
                    var logLevel = GetLogLevel(statusCode, elapsedMilliseconds);

                    _logger.Log(
                        logLevel,
                        "HTTP Request Completed: {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                        request.Method,
                        request.Path,
                        statusCode,
                        elapsedMilliseconds);

                    // Log performance warning for slow requests
                    if (elapsedMilliseconds > 5000)
                    {
                        _logger.LogWarning(
                            "Slow HTTP Request Detected: {Method} {Path} took {ElapsedMs}ms",
                            request.Method,
                            request.Path,
                            elapsedMilliseconds);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Log exception with request context
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("HttpMethod", request.Method))
            using (LogContext.PushProperty("RequestPath", request.Path.Value))
            using (LogContext.PushProperty("ResponseTimeMs", stopwatch.ElapsedMilliseconds))
            using (LogContext.PushProperty("HttpStatusCode", context.Response.StatusCode))
            {
                _logger.LogError(
                    ex,
                    "HTTP Request Failed: {Method} {Path} threw exception after {ElapsedMs}ms",
                    request.Method,
                    request.Path,
                    stopwatch.ElapsedMilliseconds);
            }

            throw;
        }
    }

    /// <summary>
    /// Get client IP address from various headers and connection info
    /// </summary>
    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded IP (common in load balancer/proxy scenarios)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        // Fallback to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Determine appropriate log level based on HTTP status code and response time
    /// </summary>
    private LogLevel GetLogLevel(int statusCode, long elapsedMilliseconds)
    {
        // Server errors (5xx) - Error level
        if (statusCode >= 500)
            return LogLevel.Error;

        // Client errors (4xx) - Warning level
        if (statusCode >= 400)
            return LogLevel.Warning;

        // Slow successful requests - Warning level
        if (statusCode >= 200 && statusCode < 300 && elapsedMilliseconds > 3000)
            return LogLevel.Warning;

        // Successful requests - Information level
        return LogLevel.Information;
    }
}
