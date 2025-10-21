
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BuildingBlocks.Messaging.MassTransit
{
    public static class Extensions
    {

        public static IServiceCollection AddMessageBroker(this IServiceCollection services,Assembly? assembly = null)
        {

            return services;
        }
    }
}
