using Ordering.API;
using Ordering.Application;
using Ordering.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add Services to the container.

builder.Services.AddApplicationServices()
.AddInfrastructureServices(builder.Configuration)
.AddApiServices();
        


var app = builder.Build();

app.useApiServices();
// Configure the HTTP request pipeline.

app.Run();
