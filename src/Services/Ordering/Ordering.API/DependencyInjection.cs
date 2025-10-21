using BuildingBlocks.Exceptions.Handler;
using Carter;

namespace Ordering.API
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {

            services.AddCarter();
            services.AddExceptionHandler<CustomExceptionHandler>();
            return services;
        }


        public static WebApplication useApiServices(this WebApplication app)
        {


            app.MapCarter();
            app.UseExceptionHandler(opt => { }); // Use the exception handler middleware ( pour gérer les exceptions globalement) [ doit etre ajouté pour que AddExceptionHandler fonctionne (Appelle l’IExceptionHandler que tu as enregistré (CustomExceptionHandler dans ton cas))  ]

            return app;
        }
    }
}
