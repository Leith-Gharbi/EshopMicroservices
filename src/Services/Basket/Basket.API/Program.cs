
using Discount.Grpc;

var builder = WebApplication.CreateBuilder(args);
var assembly = typeof(Program).Assembly; // Get the current assembly ( Basket.API )
#region Add services to the container.

builder.Services.AddCarter();  // register Carter in the DI container
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly); // register MediatR handlers in the DI container ( Detecte tous les classes qui implémentent IRequestHandler<T> dans l'assembly courant )
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
}
); // register MediatR in the DI container


#endregion


#region Data services

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Database")!);
    options.Schema.For<ShoppingCart>().Identity(x => x.UserName); // Register the ShoppingCart document ( pour que Marten sache comment stocker et récupérer les objets ShoppingCart )
}).UseLightweightSessions();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});


builder.Services.AddScoped<IBasketRepository, BasketRepository>(); // register BasketRepository in the DI container ( pour gérer les opérations CRUD sur les objets ShoppingCart )
builder.Services.Decorate<IBasketRepository, CachedBasketRepository>();
////builder.Services.AddScoped<IBasketRepository>(provider =>
////{
////    var basketRepository = provider.GetRequiredService<IBasketRepository>();
////    return new CachedBasketRepository(basketRepository, provider.GetRequiredService<IDistributedCache>());
////});
///

#endregion


#region Grpc Services

builder.Services.AddGrpcClient<DiscountProtoService.DiscountProtoServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:DiscountUrl"]!);
})
    .ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return handler;
});

#endregion

#region Cross-Cutting Services
builder.Services.AddExceptionHandler<CustomExceptionHandler>(); // register CustomExceptionHandler in the DI container ( pour gérer les exceptions globalement )

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);
#endregion


var app = builder.Build();

#region Configure the HTTP request pipeline.


app.MapCarter(); // Map Carter endpoints ( Map tous les endpoints Carter [ tous les classes qui implement ICarterModule)
app.UseExceptionHandler(options => { }); // Use the exception handler middleware ( pour gérer les exceptions globalement) [ doit etre ajouté pour que AddExceptionHandler fonctionne (Appelle l’IExceptionHandler que tu as enregistré (CustomExceptionHandler dans ton cas))  ]

app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse

}); // Map the health check endpoint ( pour vérifier la santé de l'application)


#endregion




app.Run();
