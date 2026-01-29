using BuildingBlocks.Exceptions.Handler;
using BuildingBlocks.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Ordering.API
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddApiServices(this IServiceCollection services , IConfiguration configuration)
        {

            services.AddCarter();
            services.AddExceptionHandler<CustomExceptionHandler>();
            services.AddHealthChecks().AddSqlServer(configuration.GetConnectionString("Database"));
            return services;
        }


        public static WebApplication useApiServices(this WebApplication app)
        {
            // Add HTTP logging middleware for Elasticsearch enrichment
            app.UseElasticsearchHttpLogging();

            // Health checks should be registered early
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });

            app.MapCarter();
            app.UseExceptionHandler(opt => { });
            return app;
        }
    }
}
