using MassTransit;
using Ordering.Domain.Abstractions;

namespace Ordering.Application.Orders.EventHandlers.Domain
{
    public class OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger ,IPublishEndpoint publishEndpoint) : INotificationHandler<OrderCreatedEvent>
    {
        public async Task Handle(OrderCreatedEvent domainEvent, CancellationToken cancellationToken)
        {
            logger.LogInformation("Domain Event handled: {DomainEvent}", domainEvent.GetType().Name);

            var orderCreatedIntergrationEvent = domainEvent.order.ToOrderDto();

            await publishEndpoint.Publish(orderCreatedIntergrationEvent, cancellationToken);
        }
    }
}
