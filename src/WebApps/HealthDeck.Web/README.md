# EShop HealthDeck - Microservices Health Dashboard

A comprehensive health monitoring dashboard for the EShop Microservices architecture, built with ASP.NET Core HealthChecks UI.

## 🎯 Overview

HealthDeck provides real-time health monitoring for all microservices in the EShop solution. It automatically polls each service every 10 seconds and displays their health status in an intuitive dashboard.

## 📊 Monitored Services

| Service | Port | Endpoint | Type | Database |
|---------|------|----------|------|----------|
| **Catalog API** | 5050 | https://localhost:5050/health | REST API | PostgreSQL + Marten |
| **Basket API** | 5051 | https://localhost:5051/health | REST API | PostgreSQL + Redis |
| **Discount gRPC** | 5052 | https://localhost:5052/health | gRPC | SQLite + EF Core |
| **Ordering API** | 5053 | https://localhost:5053/health | REST API | SQL Server + EF Core |
| **API Gateway** | 7238 | https://localhost:7238/health | Gateway | YARP Reverse Proxy |
| **Shopping Web** | 7005 | https://localhost:7005/health | Web App | Razor Pages |

## 🚀 Getting Started

### Prerequisites

- .NET 8.0 SDK
- All microservices must be running
- Each microservice must have `/health` endpoint configured

### Running the Dashboard

1. **Start all microservices first:**
   ```bash
   # Navigate to each service and run
   dotnet run --project Catalog.API
   dotnet run --project Basket.API
   dotnet run --project Discount.Grpc
   dotnet run --project Ordering.API
   dotnet run --project YarpApiGateway
   dotnet run --project Shopping.Web
   ```

2. **Start the HealthDeck dashboard:**
   ```bash
   cd src/WebApps/HealthDeck.Web
   dotnet run
   ```

3. **Access the dashboard:**
   - **Landing Page:** https://localhost:7104
   - **Live Health Checks UI:** https://localhost:7104/healthchecks-ui
   - **Health Checks API:** https://localhost:7104/healthchecks-api

## 🎨 Features

### Custom Landing Page
- Visual overview of all monitored services
- Service type badges (API, gRPC, Gateway, Web)
- Technology stack indicators
- Direct link to live health checks

### Live Health Checks UI
- Real-time health status monitoring
- Historical health data (last 50 entries per service)
- Auto-refresh every 10 seconds
- Detailed health check responses
- Response time tracking
- Failure notifications

### Health Status Indicators
- **🟢 Healthy** - Service is fully operational
- **🟡 Degraded** - Service has issues but is operational
- **🔴 Unhealthy** - Service is down or unavailable

## ⚙️ Configuration

### appsettings.json

```json
{
  "HealthChecksUI": {
    "HealthChecks": [
      {
        "Name": "Catalog API",
        "Uri": "https://localhost:5050/health"
      },
      {
        "Name": "Basket API",
        "Uri": "https://localhost:5051/health"
      },
      {
        "Name": "Discount gRPC",
        "Uri": "https://localhost:5052/health"
      },
      {
        "Name": "Ordering API",
        "Uri": "https://localhost:5053/health"
      },
      {
        "Name": "API Gateway (YARP)",
        "Uri": "https://localhost:7238/health"
      },
      {
        "Name": "Shopping Web",
        "Uri": "https://localhost:7005/health"
      }
    ],
    "EvaluationTimeInSeconds": 10,
    "MinimumSecondsBetweenFailureNotifications": 60
  }
}
```

### Configuration Options

| Setting | Default | Description |
|---------|---------|-------------|
| `EvaluationTimeInSeconds` | 10 | How often to poll health endpoints |
| `MinimumSecondsBetweenFailureNotifications` | 60 | Minimum time between failure alerts |
| `MaximumHistoryEntriesPerEndpoint` | 50 | Number of history entries to keep |
| `SetApiMaxActiveRequests` | 1 | Max concurrent health check requests |

## 📦 NuGet Packages

The following packages are used:

```xml
<PackageReference Include="AspNetCore.HealthChecks.UI" Version="8.0.2" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="8.0.1" />
<PackageReference Include="AspNetCore.HealthChecks.UI.InMemory.Storage" Version="8.0.1" />
```

## 🔧 Implementation Details

### Program.cs Setup

```csharp
// Add HealthChecks UI
builder.Services
    .AddHealthChecksUI(settings =>
    {
        settings.SetEvaluationTimeInSeconds(10);
        settings.MaximumHistoryEntriesPerEndpoint(50);
        settings.SetApiMaxActiveRequests(1);
    })
    .AddInMemoryStorage();

// Map HealthChecks UI endpoints
app.MapHealthChecksUI(config =>
{
    config.UIPath = "/healthchecks-ui";
    config.ApiPath = "/healthchecks-api";
});
```

### Adding Health Checks to Services

Each microservice needs to expose a `/health` endpoint:

```csharp
// In microservice Program.cs
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString)  // For PostgreSQL
    .AddRedis(redisConnection)     // For Redis
    .AddSqlServer(sqlConnection);  // For SQL Server

// Map health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

## 📊 Health Check Endpoints

### Catalog API
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "npgsql": {
      "status": "Healthy",
      "description": "PostgreSQL is healthy"
    }
  }
}
```

### Basket API
```json
{
  "status": "Healthy",
  "entries": {
    "npgsql": { "status": "Healthy" },
    "redis": { "status": "Healthy" }
  }
}
```

## 🎯 Use Cases

### Development
- Quick health overview of all services during development
- Identify which services are running
- Troubleshoot connectivity issues

### Staging/Production
- Monitor service availability
- Track historical health data
- Receive failure notifications
- Verify deployments

### DevOps/SRE
- Integration with monitoring tools
- Health check API for automation
- Service dependency visualization

## 🔍 Troubleshooting

### Service Shows as Unhealthy

1. **Check if service is running:**
   ```bash
   netstat -ano | findstr :5050
   ```

2. **Verify health endpoint manually:**
   ```bash
   curl https://localhost:5050/health
   ```

3. **Check service logs:**
   - Look for startup errors
   - Verify database connections
   - Check configuration

### Certificate Errors

If you see SSL/TLS errors:

```bash
# Trust the development certificate
dotnet dev-certs https --trust
```

### Port Conflicts

If a service port is already in use:

1. Check the `launchSettings.json` file
2. Update port numbers in both service and HealthDeck configuration
3. Restart affected services

## 📈 Future Enhancements

- [ ] Slack/Teams notifications for failures
- [ ] Grafana dashboard integration
- [ ] Prometheus metrics export
- [ ] Custom health check logic
- [ ] Performance metrics tracking
- [ ] Service dependency graph
- [ ] Historical trend analysis
- [ ] Alert rules configuration

## 🔗 Related Documentation

- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)
- [Health Checks UI Documentation](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks#healthchecksui)

## 📝 License

This project is part of the EShop Microservices solution.

## 👥 Contributing

To add a new service to the dashboard:

1. **Add health checks to the service:**
   ```csharp
   builder.Services.AddHealthChecks();
   app.MapHealthChecks("/health", new HealthCheckOptions
   {
       ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
   });
   ```

2. **Add service to HealthDeck appsettings.json:**
   ```json
   {
     "Name": "New Service",
     "Uri": "https://localhost:PORT/health"
   }
   ```

3. **Update the dashboard view** (optional) to include service card

4. **Restart HealthDeck.Web**

---

**Dashboard URL:** https://localhost:7104
**Live Health Checks:** https://localhost:7104/healthchecks-ui
**Last Updated:** November 2025
