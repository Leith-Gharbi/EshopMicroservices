
using Discount.Grpc.Data;
using Discount.Grpc.Services;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog logging with Elasticsearch, File, and Console sinks
builder.AddSerilogLogging();

// Add Correlation ID services
builder.Services.AddCorrelationId();

// Add services to the container.
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<CorrelationIdGrpcServerInterceptor>();
});

builder.Services.AddDbContext<DiscountContext>(opts =>
opts.UseSqlite(builder.Configuration.GetConnectionString("Database")));

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<DiscountContext>();


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMigration();

// Add HTTP logging middleware for Elasticsearch enrichment
app.UseElasticsearchHttpLogging();

// Health checks MUST be registered before other route handlers
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapGrpcService<DiscountService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
