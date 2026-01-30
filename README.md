# EshopMicroservices

E-commerce microservices application built with .NET 8, deployed on IBM Cloud Kubernetes Service (IKS) via GitLab CI/CD.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     INGRESS (IBM Cloud ALB)                      │
│        eshop.* │ api.* │ health.* │ kibana.*                    │
└─────┬─────────────┬─────────────┬─────────────┬─────────────────┘
      │             │             │             │
      ▼             ▼             ▼             ▼
┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
│ Shopping │  │   YARP   │  │  Health  │  │  Kibana  │
│   Web    │  │ Gateway  │  │   Deck   │  │  (Logs)  │
└──────────┘  └────┬─────┘  └──────────┘  └────┬─────┘
                   │                           │
     ┌─────────────┼─────────────┐             │
     ▼             ▼             ▼             ▼
┌──────────┐ ┌──────────┐ ┌──────────┐  ┌─────────────┐
│ Catalog  │ │  Basket  │ │ Ordering │  │Elasticsearch│
│   API    │ │   API    │ │   API    │  └─────────────┘
└────┬─────┘ └────┬─────┘ └────┬─────┘
     │            │            │
     │       ┌────┴────┐       │
     │       ▼         │       │
     │  ┌──────────┐   │       │
     │  │ Discount │   │       │
     │  │   gRPC   │   │       │
     │  └────┬─────┘   │       │
     │       │         │       │
     ▼       ▼         ▼       ▼
┌──────────┐ ┌─────┐ ┌──────────┐ ┌──────────┐
│PostgreSQL│ │Redis│ │ RabbitMQ │ │SQL Server│
│(Catalog) │ │     │ │          │ │ (Orders) │
└──────────┘ └─────┘ └──────────┘ └──────────┘
```

## Tech Stack

| Category | Technology |
|----------|------------|
| **Backend** | ASP.NET Core 8, Carter, MediatR, Entity Framework Core |
| **Databases** | PostgreSQL (Catalog, Basket), SQL Server (Ordering), SQLite (Discount), Redis (Cache) |
| **Messaging** | RabbitMQ with MassTransit |
| **API Gateway** | YARP (Yet Another Reverse Proxy) |
| **gRPC** | Discount service |
| **Logging** | Serilog → Elasticsearch → Kibana |
| **Health Checks** | ASP.NET Core Health Checks + HealthChecks UI |
| **Container Registry** | IBM Container Registry (ICR) |
| **Orchestration** | Kubernetes (IBM Cloud IKS) |
| **CI/CD** | GitLab CI/CD |
| **Infrastructure** | Helm Charts |

## Project Structure

```
EshopMicroservices/
├── src/
│   ├── Services/
│   │   ├── Catalog/Catalog.API/       # Product catalog service
│   │   ├── Basket/Basket.API/         # Shopping basket service
│   │   ├── Ordering/Ordering.API/     # Order management service
│   │   └── Discount/Discount.Grpc/    # Discount gRPC service
│   ├── ApiGateways/YarpApiGateway/    # API Gateway
│   ├── WebApps/
│   │   ├── Shopping.Web/              # Frontend web app
│   │   └── HealthDeck.Web/            # Health monitoring dashboard
│   └── BuildingBlocks/                # Shared libraries
├── deploy/helm/eshop-microservices/
│   ├── templates/                     # Kubernetes manifests
│   ├── values.yaml                    # Default values
│   ├── values-dev.yaml               # Development overrides
│   ├── values-staging.yaml           # Staging overrides
│   └── values-prod.yaml              # Production overrides
├── scripts/
│   ├── deploy-dev.sh                 # Deploy to development
│   ├── deploy-staging.sh             # Deploy to staging
│   ├── deploy-prod.sh                # Deploy to production
│   ├── .env.example                  # Environment template
│   └── .env                          # Local config (gitignored)
├── docs/                             # Documentation
└── .gitlab-ci.yml                    # CI/CD pipeline
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [IBM Cloud CLI](https://cloud.ibm.com/docs/cli)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)
- [Helm 3](https://helm.sh/docs/intro/install/)
- [Git Bash](https://git-scm.com/downloads) (for Windows)

### Local Development

1. Clone the repository:
```bash
git clone https://gitlab.com/your-repo/EshopMicroservices.git
cd EshopMicroservices
```

2. Run with Docker Compose:
```bash
docker-compose up -d
```

3. Access the services:
- Shopping Web: http://localhost:6060
- API Gateway: http://localhost:6064
- Health Dashboard: http://localhost:6066

### IBM Cloud Setup

1. Install IBM Cloud plugins:
```bash
ibmcloud plugin install container-service
ibmcloud plugin install container-registry
```

2. Login to IBM Cloud:
```bash
ibmcloud login --apikey YOUR_API_KEY -r eu-de -g Default
```

3. Configure kubectl:
```bash
ibmcloud ks cluster config --cluster lgharbi-eshop-cluster-k8s
```

## Deployment

### Configuration

Create the environment file for deployment scripts:

```bash
cp scripts/.env.example scripts/.env
```

Edit `scripts/.env` with your IBM Cloud credentials:

```bash
IBM_CLOUD_API_KEY=your-api-key
IBM_CLOUD_REGION=eu-de
IBM_CLOUD_RESOURCE_GROUP=Default
IKS_CLUSTER_NAME=lgharbi-eshop-cluster-k8s
ICR_REGISTRY=de.icr.io
ICR_NAMESPACE=eshop-images
```

### Deploy to Development

```bash
./scripts/deploy-dev.sh
# or with specific tag
./scripts/deploy-dev.sh abc123def
```

### Deploy to Staging

```bash
./scripts/deploy-staging.sh
# or with specific tag
./scripts/deploy-staging.sh abc123def
```

### Deploy to Production

```bash
# Production requires a version tag
./scripts/deploy-prod.sh v1.0.0
```

## CI/CD Pipeline

The GitLab CI/CD pipeline automatically builds, tests, and deploys the application.

### Pipeline Stages

| Stage | Description |
|-------|-------------|
| 🔨 build | Build Docker images for all services |
| 🧪 test | Run unit tests |
| 📦 push | Push images to IBM Container Registry |
| 🚀 deploy-dev | Deploy to development (auto) |
| 🎭 deploy-staging | Deploy to staging (auto) |
| 🏭 deploy-prod | Deploy to production (manual) |
| 🧹 cleanup | Clean up old images from registry |

### Branch Strategy

| Branch Pattern | Action |
|----------------|--------|
| `feat/*`, `fix/*`, `chore/*` | Deploy to **dev** |
| `dev`, `develop` | Deploy to **staging** |
| `main` | Deploy to **prod** (manual approval) |
| `v*` tags | Deploy to **prod** (manual approval) |

## Environment URLs

### Development (eshop-dev)

| Service | URL |
|---------|-----|
| Shopping Web | http://eshop-dev.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| API Gateway | http://api-dev.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| Health Dashboard | http://health-dev.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| Kibana | http://kibana-dev.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |

### Staging (eshop-staging)

| Service | URL |
|---------|-----|
| Shopping Web | http://eshop-staging.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| API Gateway | http://api-staging.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| Health Dashboard | http://health-staging.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| Kibana | http://kibana-staging.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |

### Production (eshop-prod)

| Service | URL |
|---------|-----|
| Shopping Web | http://prod-eshop.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| API Gateway | http://prod-api.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| Health Dashboard | http://prod-health.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |
| Kibana | http://prod-kibana.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud |

## Resource Requirements

### Per Environment

| Environment | CPU Requests | Memory Requests | Notes |
|-------------|--------------|-----------------|-------|
| Development | ~725m | ~3.6 Gi | ES/Kibana optional |
| Staging | ~2.5 | ~7.5 Gi | Full stack |
| Production | ~5.5 | ~15.5 Gi | Full stack + HA |

### Recommended Cluster

- **Flavor**: `bx2.8x32` (8 vCPU, 32 GB RAM)
- **Nodes**: 3 (one per availability zone)
- **Total**: 24 vCPU, 96 GB RAM

## Useful Commands

### Kubernetes

```bash
# Get pods status
kubectl get pods -n eshop-dev

# View pod logs
kubectl logs -f deployment/eshop-dev-eshop-microservices-catalog-api -n eshop-dev

# Describe pod (for troubleshooting)
kubectl describe pod <pod-name> -n eshop-dev

# Get events
kubectl get events -n eshop-dev --sort-by='.lastTimestamp'

# Port forward for local access
kubectl port-forward svc/eshop-dev-eshop-microservices-yarp-gateway 8080:80 -n eshop-dev
```

### Helm

```bash
# List releases
helm list -n eshop-dev

# Check release status
helm status eshop-dev -n eshop-dev

# Rollback to previous version
helm rollback eshop-dev -n eshop-dev

# Uninstall
helm uninstall eshop-dev -n eshop-dev

# Upgrade with specific values
helm upgrade eshop-dev ./deploy/helm/eshop-microservices \
  -n eshop-dev \
  -f ./deploy/helm/eshop-microservices/values-dev.yaml \
  --set global.imageTag=abc123def
```

### IBM Cloud

```bash
# Get cluster info
ibmcloud ks cluster get --cluster lgharbi-eshop-cluster-k8s

# Get Ingress subdomain
ibmcloud ks cluster get --cluster lgharbi-eshop-cluster-k8s | grep "Ingress Subdomain"

# List images in registry
ibmcloud cr images --restrict eshop-images

# Check registry quota
ibmcloud cr quota
```

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| **503 Service Unavailable** | Port mismatch in Ingress | Check service ports match values |
| **ImagePullBackOff** | Missing `icr-secret` | Run deployment script to create secret |
| **SQL Server OOM** | Insufficient memory | SQL Server requires minimum 2GB RAM |
| **Elasticsearch crash** | Low heap memory | Set `-Xms512m -Xmx512m` minimum |
| **Kibana 503** | Elasticsearch not ready | Wait for ES to be healthy first |
| **Helm stuck** | Failed previous deploy | Run `helm rollback` or `helm uninstall` |

### Health Check 404

If health checks return 404, ensure `UseHealthChecks()` is registered **before** `MapCarter()` or `MapGrpcService()`:

```csharp
var app = builder.Build();

// Health checks MUST be registered before other route handlers
app.UseHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Then map routes
app.MapCarter();
```

## Documentation

- [Kubernetes Resources Guide](docs/kubernetes-resources-guide.md)
- [Helm Best Practices](docs/helm-kubernetes-best-practices.md)
- [GitLab Runner Configuration](docs/gitlab-runner-configuration-guide.md)

## License

This project is for educational purposes.
