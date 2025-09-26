




namespace Catalog.API.Products.CreateProduct;


public record CreateProductCommand(string Name, List<string> Category, string Description, string ImageFile, decimal Price)
    : ICommand<CreateProductResult>;
public record CreateProductResult(Guid Id);

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("Name is required");
        RuleFor(x => x.Category).NotEmpty().WithMessage("Category is required");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500).WithMessage("Description is required");
        RuleFor(x => x.ImageFile).NotEmpty().MaximumLength(200).WithMessage("ImageFile is required");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be grater than 0");
    }
}



internal class CreateProductCommandHandler(IDocumentSession session, ILogger<CreateProductCommandHandler> logger)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {



        // Bussiness logic to create a product


        logger.LogInformation("CreateProductCommandHandler.Handel called with command {@Command}", command);
        // create Product enity from command object 
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
        session.Store(product);
        await session.SaveChangesAsync(cancellationToken);

        // return CreateProductResult with new product id
        return new CreateProductResult(product.Id);
    }
}

