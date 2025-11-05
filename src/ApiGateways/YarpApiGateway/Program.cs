using BuildingBlocks.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog logging with Elasticsearch, File, and Console sinks
builder.AddSerilogLogging();

// Add Correlation ID services
builder.Services.AddCorrelationId();

// Add services to the container.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddRateLimiter(rateLimiterOptions =>
{
    rateLimiterOptions.AddFixedWindowLimiter("fixed", options =>
    {
        options.Window = TimeSpan.FromSeconds(10);
        options.PermitLimit = 5;
    });
});

// Add Health Checks
builder.Services.AddHealthChecks();




var app = builder.Build();


// Configure the HTTP request pipeline.
// Add HTTP logging middleware for Elasticsearch enrichment
app.UseElasticsearchHttpLogging();

app.UseRateLimiter();

app.MapReverseProxy();

// Map Health Check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});


app.Run();
