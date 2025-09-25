
namespace Catalog.API.Products.GetProductById
{

    public record GetProductByIdQuery(Guid Id) : IQuery<GetProductByIdResult>;


    public record GetProductByIdResult(Product? Product);


    internal class GetProductByIdQueryHandler(IDocumentSession session, ILogger<GetProductByIdQueryHandler> logger) : IQueryHandler<GetProductByIdQuery, GetProductByIdResult>
    {
        public async Task<GetProductByIdResult> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
        {
            logger.LogInformation("GetProductByIdQueryHandler.Handle called with {@Query}", query);

            // Business logic to get product by id
            var product = await session.LoadAsync<Product>(query.Id, cancellationToken);
            if (product is null)
            {
                logger.LogWarning("Product with Id {Id} not found", query.Id);
                throw new ProductNotFoundException();
            }

            logger.LogInformation("Retrieved product: {@Product}", product);
            return new GetProductByIdResult(product);


        }
    }
}
