
using BuildingBlocks.Messaging.MassTransit;
using BuildingBlocks.Logging;
using BuildingBlocks.Resilience;
using Discount.Grpc;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog logging with Elasticsearch, File, and Console sinks
builder.AddSerilogLogging();

// Add Correlation ID services
builder.Services.AddCorrelationId();

var assembly = typeof(Program).Assembly; // Get the current assembly ( Basket.API )
#region Add services to the container.

builder.Services.AddCarter();  // register Carter in the DI container
builder.Services.AddMediatR(config =>
{
    config.RegisterServicesFromAssembly(assembly); // register MediatR handlers in the DI container ( Detecte tous les classes qui impl�mentent IRequestHandler<T> dans l'assembly courant )
    config.AddOpenBehavior(typeof(ValidationBehavior<,>));
    config.AddOpenBehavior(typeof(LoggingBehavior<,>));
}
); // register MediatR in the DI container

#endregion


#region Data services

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Database")!);
    options.Schema.For<ShoppingCart>().Identity(x => x.UserName); // Register the ShoppingCart document ( pour que Marten sache comment stocker et r�cup�rer les objets ShoppingCart )
}).UseLightweightSessions();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});


builder.Services.AddScoped<IBasketRepository, BasketRepository>(); // register BasketRepository in the DI container ( pour g�rer les op�rations CRUD sur les objets ShoppingCart )
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
        // TODO: Replace with proper certificate validation in production
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    return handler;
})
.AddInterceptor<CorrelationIdGrpcInterceptor>() // Add correlation ID interceptor for gRPC
.AddGrpcResilience(serviceName: "DiscountService") // Add Polly resilience policies for gRPC
.ConfigureChannel(options =>
{
    // gRPC-specific configurations
    options.MaxRetryAttempts = 3;
    options.MaxRetryBufferSize = 1024 * 1024; // 1MB
    options.MaxRetryBufferPerCallSize = 512 * 1024; // 512KB
});

#endregion

#region Cross-Cutting Services
builder.Services.AddExceptionHandler<CustomExceptionHandler>(); // register CustomExceptionHandler in the DI container ( pour g�rer les exceptions globalement )

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Database")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

builder.Services.AddMessageBroker(builder.Configuration); // register MassTransit in the DI container ( pour la communication asynchrone entre les microservices via RabbitMQ )
#endregion


var app = builder.Build();

#region Configure the HTTP request pipeline.

// Add HTTP logging middleware for Elasticsearch enrichment
app.UseElasticsearchHttpLogging();

app.MapCarter(); // Map Carter endpoints ( Map tous les endpoints Carter [ tous les classes qui implement ICarterModule)
app.UseExceptionHandler(options => { }); // Use the exception handler middleware ( pour g�rer les exceptions globalement) [ doit etre ajout� pour que AddExceptionHandler fonctionne (Appelle l�IExceptionHandler que tu as enregistr� (CustomExceptionHandler dans ton cas))  ]

app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse

}); // Map the health check endpoint ( pour v�rifier la sant� de l'application)


#endregion




app.Run();
