
using BuildingBlocks.Logging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BuildingBlocks.Messaging.MassTransit;
public static class Extentions
{
    public static IServiceCollection AddMessageBroker
        (this IServiceCollection services, IConfiguration configuration, Assembly? assembly = null)
    {
        services.AddMassTransit(config =>
        {
            config.SetKebabCaseEndpointNameFormatter();

            if (assembly != null)
                config.AddConsumers(assembly);

            config.UsingRabbitMq((context, configurator) =>
            {
                configurator.Host(new Uri(configuration["MessageBroker:Host"]!), host =>
                {
                    host.Username(configuration["MessageBroker:UserName"]!);
                    host.Password(configuration["MessageBroker:Password"]!);
                });

                // Add correlation ID filters for message bus
                configurator.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);
                configurator.UseSendFilter(typeof(CorrelationIdSendFilter<>), context);
                configurator.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);

                configurator.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
