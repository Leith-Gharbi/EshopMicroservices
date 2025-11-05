using Grpc.Core;
using Grpc.Core.Interceptors;

namespace BuildingBlocks.Logging;

/// <summary>
/// gRPC client interceptor to propagate correlation ID in outgoing gRPC calls
/// </summary>
public class CorrelationIdGrpcInterceptor : Interceptor
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private const string CorrelationIdHeaderName = "x-correlation-id";

    public CorrelationIdGrpcInterceptor(ICorrelationIdAccessor correlationIdAccessor)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var headers = context.Options.Headers ?? new Metadata();
            headers.Add(CorrelationIdHeaderName, correlationId);

            var newOptions = context.Options.WithHeaders(headers);
            context = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method,
                context.Host,
                newOptions);
        }

        return continuation(request, context);
    }

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCorrelationIdToContext(ref context);
        return continuation(context);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCorrelationIdToContext(ref context);
        return continuation(request, context);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCorrelationIdToContext(ref context);
        return continuation(context);
    }

    private void AddCorrelationIdToContext<TRequest, TResponse>(
        ref ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var headers = context.Options.Headers ?? new Metadata();
            headers.Add(CorrelationIdHeaderName, correlationId);

            var newOptions = context.Options.WithHeaders(headers);
            context = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method,
                context.Host,
                newOptions);
        }
    }
}
