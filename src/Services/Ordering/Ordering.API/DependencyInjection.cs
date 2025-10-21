using BuildingBlocks.Exceptions.Handler;
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


            app.MapCarter();
            app.UseExceptionHandler(opt => { }); // Use the exception handler middleware ( pour gérer les exceptions globalement) [ doit etre ajouté pour que AddExceptionHandler fonctionne (Appelle l’IExceptionHandler que tu as enregistré (CustomExceptionHandler dans ton cas))  ]
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            return app;
        }
    }
}
