using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Logging;

/// <summary>
/// gRPC server interceptor to extract correlation ID from incoming gRPC calls
/// </summary>
public class CorrelationIdGrpcServerInterceptor : Interceptor
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<CorrelationIdGrpcServerInterceptor> _logger;
    private const string CorrelationIdHeaderName = "x-correlation-id";

    public CorrelationIdGrpcServerInterceptor(
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<CorrelationIdGrpcServerInterceptor> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetCorrelationId(context);
        return await continuation(request, context);
    }

    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetCorrelationId(context);
        return await continuation(requestStream, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetCorrelationId(context);
        await continuation(request, responseStream, context);
    }

    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ExtractAndSetCorrelationId(context);
        await continuation(requestStream, responseStream, context);
    }

    private void ExtractAndSetCorrelationId(ServerCallContext context)
    {
        // Extract correlation ID from incoming headers
        var correlationIdEntry = context.RequestHeaders.FirstOrDefault(
            h => h.Key.Equals(CorrelationIdHeaderName, StringComparison.OrdinalIgnoreCase));

        var correlationId = correlationIdEntry?.Value ?? Guid.NewGuid().ToString();

        _correlationIdAccessor.SetCorrelationId(correlationId);

        _logger.LogDebug(
            "gRPC Request received with Correlation ID: {CorrelationId} for method: {Method}",
            correlationId,
            context.Method);
    }
}
