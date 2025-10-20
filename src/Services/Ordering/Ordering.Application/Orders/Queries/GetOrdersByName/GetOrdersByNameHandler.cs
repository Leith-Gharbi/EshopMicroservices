

using Ordering.Application.Orders.Queries.GetOrderByName;

namespace Ordering.Application.Orders.Queries.GetOrdersByName
{
    public class GetOrdersByNameHandler(IApplicationDbContext dbContext)
     : IQueryHandler<GetOrdersByNameQuery, GetOrdersByNameResult>
    {
        public async Task<GetOrdersByNameResult> Handle(GetOrdersByNameQuery query, CancellationToken cancellationToken)
        {
            // get orders by name using dbContext
            // return result

            var orders = await dbContext.Orders
                    .Include(o => o.OrderItems) //C’est un eager loading (chargement anticipé) d’une relation. “Quand tu charges mes Orders, charge aussi la collection OrderItems liée.”
                    .AsNoTracking() //“Ne garde pas ces entités dans le ChangeTracker.”  (Ce morceau est important pour les performances ⚡
                    .Where(o => o.OrderName.Value.Contains(query.Name))
                    .OrderBy(o => o.OrderName.Value)
                    .ToListAsync(cancellationToken);

            return new GetOrdersByNameResult(orders.ToOrderDtoList());
        }
 
    }
}
