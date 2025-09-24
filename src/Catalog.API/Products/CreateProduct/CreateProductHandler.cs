

using BuildingBlocks.CQRS;
using Catalog.API.Models;
using System.Xml.Linq;

namespace Catalog.API.Products.CreateProduct;


public record CreateProductCommand(string Name, List<string> Category, string Description, string ImageFile, decimal Price)
    :ICommand<CreateProductResult>;
public record CreateProductResult(Guid Id);
internal class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        // Bussiness logic to create a product

        // cretae Product enity from command object 
        Product product = new Product
        {
            Name = command.Name,
            Category = command.Category,
            Description = command.Description,
            ImageFile = command.ImageFile,
            Price = command.Price
        };
        // TODO:
        // Save to DB
        // return CreateProductResult with new product id

        return new CreateProductResult(Guid.NewGuid());
    }
}

