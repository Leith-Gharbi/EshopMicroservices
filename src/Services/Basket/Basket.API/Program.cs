



using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var assembly = typeof(Program).Assembly; // Get the current assembly ( Basket.API )
#region Add services to the container.

builder.Services.AddCarter();  // register Carter in the DI container

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Database")!);
    options.Schema.For<ShoppingCart>().Identity(x => x.UserName); // Register the ShoppingCart document ( pour que Marten sache comment stocker et récupérer les objets ShoppingCart )
}).UseLightweightSessions();

builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly); // register MediatR handlers in the DI container ( Detecte tous les classes qui implémentent IRequestHandler<T> dans l'assembly courant )
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
}
); // register MediatR in the DI container

#endregion


var app = builder.Build();

#region Configure the HTTP request pipeline.


app.MapCarter(); // Map Carter endpoints ( Map tous les endpoints Carter [ tous les classes qui implement ICarterModule)

#endregion




app.Run();
