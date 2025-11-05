using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Logging;

/// <summary>
/// Extension methods for registering correlation ID services
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Adds correlation ID services to the service collection
    /// </summary>
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        // Register the correlation ID accessor as a singleton
        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();

        // Register the delegating handler for HttpClient
        services.AddTransient<CorrelationIdDelegatingHandler>();

        // Register the gRPC client interceptor
        services.AddSingleton<Interceptor, CorrelationIdGrpcInterceptor>();

        // Register the gRPC server interceptor
        services.AddSingleton<CorrelationIdGrpcServerInterceptor>();

        return services;
    }

    /// <summary>
    /// Adds correlation ID delegating handler to an HttpClient registration
    /// </summary>
    public static IHttpClientBuilder AddCorrelationIdHandler(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
    }
}
