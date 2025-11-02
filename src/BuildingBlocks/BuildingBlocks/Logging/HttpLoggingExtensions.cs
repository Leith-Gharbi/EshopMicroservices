using Microsoft.AspNetCore.Builder;

namespace BuildingBlocks.Logging;

/// <summary>
/// Extension methods for HTTP logging middleware
/// </summary>
public static class HttpLoggingExtensions
{
    /// <summary>
    /// Adds HTTP request/response logging middleware to enrich Elasticsearch logs
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseElasticsearchHttpLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<HttpLoggingMiddleware>();
    }
}
