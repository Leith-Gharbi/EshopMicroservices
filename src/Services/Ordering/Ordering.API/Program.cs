using Ordering.API;
using Ordering.Application;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Data.Extensions;
using BuildingBlocks.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog logging with Elasticsearch, File, and Console sinks
builder.AddSerilogLogging();

// Add Services to the container.

builder.Services.AddApplicationServices(builder.Configuration)
.AddInfrastructureServices(builder.Configuration)
.AddApiServices(builder.Configuration);
        


var app = builder.Build();

// Configure the HTTP request pipeline.
app.useApiServices();
if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}

app.Run();
