using Microsoft.AspNetCore.RateLimiting;
using BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog logging with Elasticsearch, File, and Console sinks
builder.AddSerilogLogging();


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




var app = builder.Build();


// Configure the HTTP request pipeline.
// Add HTTP logging middleware for Elasticsearch enrichment
app.UseElasticsearchHttpLogging();

app.UseRateLimiter();

app.MapReverseProxy();

app.Run();
