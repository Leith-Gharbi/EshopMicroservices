# HealthDeck Docker Setup Guide

This guide explains how to run the HealthDeck Health Monitoring Dashboard in Docker containers.

## 🐳 Docker Architecture

### Container Network Communication

When running in Docker, services communicate using **Docker service names** on an internal network, not `localhost`.

| Service | Docker Service Name | Internal Port | External Port | Health Endpoint |
|---------|---------------------|---------------|---------------|-----------------|
| **Catalog API** | `catalog.api` | 8080 | 6000 | http://catalog.api:8080/health |
| **Basket API** | `basket.api` | 8080 | 6001 | http://basket.api:8080/health |
| **Discount gRPC** | `discount.grpc` | 8080 | 6002 | http://discount.grpc:8080/health |
| **Ordering API** | `ordering.api` | 8080 | 6003 | http://ordering.api:8080/health |
| **API Gateway** | `yarpapigateway` | 8080 | 6004 | http://yarpapigateway:8080/health |
| **HealthDeck** | `healthdeck.web` | 8080 | 6010 | - |

### Key Differences: Development vs Docker

| Aspect | Development (localhost) | Docker |
|--------|------------------------|--------|
| **Service URLs** | https://localhost:5050 | http://catalog.api:8080 |
| **Protocol** | HTTPS | HTTP (internal) |
| **Port** | External port (5050) | Internal port (8080) |
| **DNS** | localhost | Docker service name |

---

## 🚀 Running HealthDeck in Docker

### **1. Start All Services with Docker Compose**

From the `src` directory:

```bash
cd D:\Projects\EshopMicroservices\src

# Build and start all containers
docker-compose up -d --build
```

This will:
- Build Docker images for all services
- Start all containers
- Create internal Docker network
- Configure health check endpoints

### **2. Verify Containers Are Running**

```bash
# Check container status
docker-compose ps

# Should show all containers as "Up"
```

Expected output:
```
NAME                STATUS              PORTS
catalog.api         Up                  0.0.0.0:6000->8080/tcp
basket.api          Up                  0.0.0.0:6001->8080/tcp
discount.grpc       Up                  0.0.0.0:6002->8080/tcp
ordering.api        Up                  0.0.0.0:6003->8080/tcp
yarpapigateway      Up                  0.0.0.0:6004->8080/tcp
healthdeck.web      Up                  0.0.0.0:6010->8080/tcp
```

### **3. Access HealthDeck Dashboard**

Once containers are running:

- **Dashboard Home:** http://localhost:6010
- **Live Health Checks UI:** http://localhost:6010/healthchecks-ui
- **Health Checks API:** http://localhost:6010/healthchecks-api

---

## ⚙️ Docker Configuration

### Environment Variables (docker-compose.override.yml)

HealthDeck is configured via environment variables:

```yaml
healthdeck.web:
  environment:
    - ASPNETCORE_ENVIRONMENT=Development
    - ASPNETCORE_HTTP_PORTS=8080
    - ASPNETCORE_HTTPS_PORTS=8081
    - HealthChecksUI__HealthChecks__0__Name=Catalog API
    - HealthChecksUI__HealthChecks__0__Uri=http://catalog.api:8080/health
    - HealthChecksUI__HealthChecks__1__Name=Basket API
    - HealthChecksUI__HealthChecks__1__Uri=http://basket.api:8080/health
    - HealthChecksUI__HealthChecks__2__Name=Discount gRPC
    - HealthChecksUI__HealthChecks__2__Uri=http://discount.grpc:8080/health
    - HealthChecksUI__HealthChecks__3__Name=Ordering API
    - HealthChecksUI__HealthChecks__3__Uri=http://ordering.api:8080/health
    - HealthChecksUI__HealthChecks__4__Name=API Gateway (YARP)
    - HealthChecksUI__HealthChecks__4__Uri=http://yarpapigateway:8080/health
    - HealthChecksUI__EvaluationTimeInSeconds=10
    - HealthChecksUI__MinimumSecondsBetweenFailureNotifications=60
  depends_on:
    - catalog.api
    - basket.api
    - discount.grpc
    - ordering.api
    - yarpapigateway
```

**Key Points:**
- ✅ Uses Docker service names (e.g., `catalog.api`)
- ✅ Uses internal port `8080` (not external ports)
- ✅ Uses `http://` protocol (not `https://`)
- ✅ Includes `depends_on` to ensure services start in order

---

## 🔍 Troubleshooting

### Issue 1: All Services Show as "Unhealthy" with "Connection Refused"

**Cause:** Services not started or HealthDeck started before services.

**Solution:**
```bash
# Stop all containers
docker-compose down

# Start services in order (with dependencies)
docker-compose up -d

# Check logs
docker-compose logs healthdeck.web
docker-compose logs catalog.api
```

### Issue 2: Some Services Healthy, Others Unhealthy

**Cause:** Individual service startup issues or database connectivity.

**Solution:**
```bash
# Check specific service logs
docker-compose logs [service-name]

# Example: Check Basket API logs
docker-compose logs basket.api

# Restart specific service
docker-compose restart basket.api
```

### Issue 3: HealthDeck Can't Reach Services

**Cause:** Incorrect service names or ports in configuration.

**Solution:**
1. Verify service names match `docker-compose.yml`:
   ```bash
   docker-compose ps --services
   ```

2. Test connectivity from HealthDeck container:
   ```bash
   # Access HealthDeck container shell
   docker exec -it healthdeck.web sh

   # Test connectivity to a service
   curl http://catalog.api:8080/health

   # Exit container
   exit
   ```

### Issue 4: Database Connection Errors

**Cause:** Database containers not ready when services start.

**Solution:**
```bash
# Restart in correct order
docker-compose down
docker-compose up -d catalogdb basketdb orderdb distributedcache
sleep 10  # Wait for databases to initialize
docker-compose up -d catalog.api basket.api ordering.api
sleep 5
docker-compose up -d discount.grpc yarpapigateway healthdeck.web
```

### Issue 5: Port Already in Use

**Cause:** Port conflict with existing services.

**Solution:**
```bash
# Find process using port
netstat -ano | findstr :6010

# Stop the process or change ports in docker-compose.override.yml
```

---

## 📊 Verifying Health Checks

### Test Individual Service Health Endpoints

From your host machine (Windows):

```bash
# Catalog API (external port 6000)
curl http://localhost:6000/health

# Basket API (external port 6001)
curl http://localhost:6001/health

# Discount gRPC (external port 6002)
curl http://localhost:6002/health

# Ordering API (external port 6003)
curl http://localhost:6003/health

# API Gateway (external port 6004)
curl http://localhost:6004/health
```

Expected response (Healthy):
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "npgsql": {
      "status": "Healthy",
      "description": "Postgres is healthy"
    }
  }
}
```

---

## 🔄 Docker Commands Reference

### Start/Stop Services

```bash
# Start all services
docker-compose up -d

# Stop all services
docker-compose down

# Stop and remove volumes (clean state)
docker-compose down -v

# Restart specific service
docker-compose restart healthdeck.web

# Rebuild and restart
docker-compose up -d --build healthdeck.web
```

### View Logs

```bash
# View all logs
docker-compose logs

# Follow logs (real-time)
docker-compose logs -f

# View specific service logs
docker-compose logs healthdeck.web

# Last 50 lines
docker-compose logs --tail=50 healthdeck.web
```

### Container Management

```bash
# List running containers
docker-compose ps

# View container resource usage
docker stats

# Access container shell
docker exec -it healthdeck.web sh

# Inspect container
docker inspect healthdeck.web
```

---

## 🌐 Network Configuration

### Docker Network Name

Services communicate on: `src_default` (auto-created by Docker Compose)

### View Network Details

```bash
# List Docker networks
docker network ls

# Inspect the network
docker network inspect src_default
```

### Service Discovery

Docker provides automatic DNS resolution:
- Service name `catalog.api` resolves to container IP
- No need for manual IP configuration
- Built-in load balancing for scaled services

---

## 📝 Configuration Files

### docker-compose.yml
Defines services, images, and build contexts.

### docker-compose.override.yml
**This file is crucial!** It contains:
- Environment variables for each service
- Port mappings (external:internal)
- Database connection strings
- Health check URLs for HealthDeck
- Service dependencies

### appsettings.json (HealthDeck.Web)
Used for **local development only**.
Docker uses environment variables from `docker-compose.override.yml`.

---

## 🎯 Best Practices

### 1. Service Startup Order

Use `depends_on` in docker-compose.override.yml:
```yaml
healthdeck.web:
  depends_on:
    - catalog.api
    - basket.api
    - discount.grpc
    - ordering.api
    - yarpapigateway
```

### 2. Health Check Configuration

- **EvaluationTimeInSeconds:** 10 (check every 10 seconds)
- **MinimumSecondsBetweenFailureNotifications:** 60 (avoid spam)
- **Use HTTP (not HTTPS)** for internal Docker communication

### 3. Resource Management

```bash
# Remove unused images
docker image prune -a

# Remove unused volumes
docker volume prune

# Clean everything (careful!)
docker system prune -a --volumes
```

### 4. Production Deployment

For production, consider:
- Use specific image tags (not `latest`)
- Enable HTTPS with proper certificates
- Use external secret management
- Configure resource limits (CPU, memory)
- Set up persistent storage for databases
- Implement container orchestration (Kubernetes)

---

## 🔐 Security Considerations

### Development vs Production

**Development (Current Setup):**
- ✅ HTTP for internal communication
- ✅ Simple environment variables
- ✅ Development certificates
- ⚠️ Not production-ready

**Production Requirements:**
- 🔒 HTTPS with valid certificates
- 🔒 Secrets in Azure Key Vault or similar
- 🔒 Network policies and firewalls
- 🔒 Regular security updates
- 🔒 Container scanning

---

## 📚 Additional Resources

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [ASP.NET Core Docker Images](https://hub.docker.com/_/microsoft-dotnet-aspnet/)
- [Health Checks in .NET](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Docker Networking](https://docs.docker.com/network/)

---

## ✅ Quick Reference

### URLs for Docker Environment

| Service | URL |
|---------|-----|
| **HealthDeck Dashboard** | http://localhost:6010 |
| **HealthDeck UI** | http://localhost:6010/healthchecks-ui |
| **Catalog API** | http://localhost:6000 |
| **Basket API** | http://localhost:6001 |
| **Discount gRPC** | http://localhost:6002 |
| **Ordering API** | http://localhost:6003 |
| **API Gateway** | http://localhost:6004 |

### Health Check Endpoints (Internal)

```
http://catalog.api:8080/health
http://basket.api:8080/health
http://discount.grpc:8080/health
http://ordering.api:8080/health
http://yarpapigateway:8080/health
```

---

**Last Updated:** November 2025
**Docker Compose Version:** 3.8
**ASP.NET Core Version:** 8.0
