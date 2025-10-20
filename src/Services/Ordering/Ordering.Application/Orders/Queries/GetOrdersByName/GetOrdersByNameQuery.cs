

namespace Ordering.Application.Orders.Queries.GetOrderByName
{

    public record GetOrdersByNameResult(IEnumerable<OrderDto> Orders);
    public record GetOrdersByNameQuery(string Name) :IQuery<GetOrdersByNameResult>;
}
