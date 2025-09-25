
using Catalog.API.Models;
using ImTools;

namespace Catalog.API.Products.GetProductByCategory
{


    public record GetProductByCategoryQuery(string Category) : IQuery<GetProductByCategoryResult>;

    public record GetProductByCategoryResult(IEnumerable<Product> Products);

    internal class GetProductByCategoryCommandHandler(IDocumentSession session , ILogger<GetProductByCategoryCommandHandler> logger) : IQueryHandler<GetProductByCategoryQuery, GetProductByCategoryResult>
    {
        public async Task<GetProductByCategoryResult> Handle(GetProductByCategoryQuery query, CancellationToken cancellationToken)
        {

            logger.LogInformation("GetProductByCategoryCommandHandler.Handle called with {@Query}", query );
            var products = await session.Query<Product>()
                .Where(p => p.Category.Contains(query.Category)).ToListAsync(cancellationToken);
            logger.LogInformation("Retrieved products: {@Product}", products);

            return new GetProductByCategoryResult(products);
        }
    }
}
