var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add HealthChecks UI
builder.Services
    .AddHealthChecksUI(settings =>
    {
        settings.SetEvaluationTimeInSeconds(10); // Evaluate health every 10 seconds
        settings.MaximumHistoryEntriesPerEndpoint(50); // Keep 50 entries per endpoint
        settings.SetApiMaxActiveRequests(1); // Max concurrent requests

        // Add health check endpoints from configuration
        // This allows dynamic configuration from appsettings.json
    })
    .AddInMemoryStorage(); // Use in-memory storage for health check history

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map HealthChecks UI endpoints
app.MapHealthChecksUI(config =>
{
    config.UIPath = "/healthchecks-ui"; // UI available at /healthchecks-ui
    config.ApiPath = "/healthchecks-api"; // API available at /healthchecks-api
});

app.Run();
