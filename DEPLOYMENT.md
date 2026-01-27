# EshopMicroservices - Deployment Guide

Quick reference guide for deploying EshopMicroservices to IBM Cloud IKS.

## Infrastructure Summary

- **Cluster:** lgharbi-eshop-cluster-k8s (IBM Cloud IKS)
- **Namespaces:** eshop-dev, eshop-staging, eshop-prod
- **Registry:** de.icr.io/eshop-images
- **CI/CD:** GitLab Pipelines

## Quick Deployment

### Prerequisites

```bash
# Install IBM Cloud CLI
curl -fsSL https://clis.cloud.ibm.com/install/linux | sh

# Install Kubernetes plugin
ibmcloud plugin install kubernetes-service

# Login to IBM Cloud
ibmcloud login --apikey YOUR_API_KEY

# Configure kubectl
ibmcloud ks cluster config --cluster lgharbi-eshop-cluster-k8s
```

### Deploy to Environments

#### Development (eshop-dev)

```bash
cd scripts
./deploy-dev.sh latest
```

Or manually:
```bash
helm upgrade --install eshop-dev ./deploy/helm/eshop-microservices \
  --namespace eshop-dev \
  --create-namespace \
  --values ./deploy/helm/eshop-microservices/values-dev.yaml \
  --set global.imageRegistry=de.icr.io/eshop-images \
  --wait
```

#### Staging (eshop-staging)

```bash
cd scripts
./deploy-staging.sh latest
```

#### Production (eshop-prod)

```bash
cd scripts
./deploy-prod.sh v1.0.0  # Must specify version tag
```

## GitLab CI/CD Pipeline

### Branching Strategy

| Branch/Tag | Deploys To | Trigger |
|------------|------------|---------|
| `develop` | eshop-dev | Automatic |
| `main` | eshop-staging | Automatic |
| `v*` (tags) | eshop-prod | Manual approval |

### Required GitLab Variables

Go to **Settings → CI/CD → Variables** and add:

- `IBM_CLOUD_API_KEY` (masked, protected)
- `IBM_CLOUD_REGION` = us-south
- `IKS_CLUSTER_NAME` = lgharbi-eshop-cluster-k8s

### Creating a Release

```bash
# Tag the release
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0

# GitLab pipeline will:
# 1. Build all images
# 2. Run tests
# 3. Push to registry
# 4. Wait for manual approval
# 5. Deploy to production after approval
```

## Building and Pushing Images Locally

```bash
# Set IBM Cloud API key
export IBM_CLOUD_API_KEY=your-api-key

# Build and push all images
cd scripts
./build-and-push.sh v1.0.0

# Or build individual service
docker build -t de.icr.io/eshop-images/catalog-api:v1.0.0 \
  -f src/Services/Catalog/Catalog.API/Dockerfile src
docker push de.icr.io/eshop-images/catalog-api:v1.0.0
```

## Monitoring Deployments

### Check Status

```bash
# View all pods
kubectl get pods -n eshop-dev

# Watch deployment progress
kubectl get pods -n eshop-dev --watch

# Check services
kubectl get svc -n eshop-dev

# View events
kubectl get events -n eshop-dev --sort-by='.lastTimestamp'
```

### Access Services Locally

```bash
# Access YARP Gateway
kubectl port-forward svc/eshop-dev-yarp-gateway 8080:80 -n eshop-dev
# Visit: http://localhost:8080

# Access Kibana (logs)
kubectl port-forward svc/eshop-dev-kibana 5601:5601 -n eshop-dev
# Visit: http://localhost:5601

# Access RabbitMQ Management
kubectl port-forward svc/eshop-dev-rabbitmq 15672:15672 -n eshop-dev
# Visit: http://localhost:15672 (guest/guest)

# Access HealthDeck
kubectl port-forward svc/eshop-dev-healthdeck-web 8081:80 -n eshop-dev
# Visit: http://localhost:8081
```

### View Logs

```bash
# Catalog API logs
kubectl logs -f deployment/eshop-dev-catalog-api -n eshop-dev

# Last 100 lines
kubectl logs --tail=100 deployment/eshop-dev-catalog-api -n eshop-dev

# All pods with label
kubectl logs -f -l app.kubernetes.io/component=catalog-api -n eshop-dev
```

## Troubleshooting

### Pods Not Starting

```bash
# Describe pod
kubectl describe pod <pod-name> -n eshop-dev

# Check logs
kubectl logs <pod-name> -n eshop-dev

# Previous container logs (if restarted)
kubectl logs <pod-name> -n eshop-dev --previous
```

### Image Pull Issues

```bash
# Verify images exist
ibmcloud cr images --restrict eshop-images

# Check quota
ibmcloud cr quota
```

### Database Connection Issues

```bash
# Check database pods
kubectl get pods -l app.kubernetes.io/component=postgresql-catalogdb -n eshop-dev

# Connect to PostgreSQL
kubectl exec -it eshop-dev-postgresql-catalogdb-0 -n eshop-dev -- \
  psql -U postgres -d CatalogDb

# Connect to SQL Server
kubectl exec -it eshop-dev-sqlserver-0 -n eshop-dev -- \
  /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P SwN12345678
```

### Rollback Deployment

```bash
# View release history
helm history eshop-dev -n eshop-dev

# Rollback to previous version
helm rollback eshop-dev -n eshop-dev

# Rollback to specific revision
helm rollback eshop-dev 3 -n eshop-dev
```

## Scaling

### Manual Scaling

```bash
# Scale deployment
kubectl scale deployment eshop-dev-catalog-api --replicas=5 -n eshop-dev

# Using Helm
helm upgrade eshop-dev ./deploy/helm/eshop-microservices \
  --namespace eshop-dev \
  --set replicaCount.catalogApi=5 \
  --reuse-values
```

### Autoscaling

Enabled in staging and production via HPA (Horizontal Pod Autoscaler):

```bash
# View HPA status
kubectl get hpa -n eshop-staging

# Describe HPA
kubectl describe hpa eshop-staging-catalog-api -n eshop-staging
```

## Cleanup

### Remove Deployment

```bash
# Using Helm
helm uninstall eshop-dev --namespace eshop-dev

# Using cleanup script
cd scripts
./cleanup.sh
# Select environment to cleanup
```

### Delete Namespace (CAUTION)

```bash
# This deletes ALL resources and data
kubectl delete namespace eshop-dev
```

## File Locations

- **Helm Chart:** `deploy/helm/eshop-microservices/`
- **Values Files:** `deploy/helm/eshop-microservices/values-*.yaml`
- **GitLab Pipeline:** `.gitlab-ci.yml` (root)
- **Deployment Scripts:** `scripts/`
- **Documentation:** `deploy/helm/eshop-microservices/README.md`

## Service Ports

| Service | Container Port | Service Port |
|---------|---------------|--------------|
| Catalog API | 8080 | 80 |
| Basket API | 8080 | 80 |
| Ordering API | 8080 | 80 |
| Discount gRPC | 8080 | 80 |
| YARP Gateway | 8080 | 80 |
| Shopping Web | 8080 | 80 |
| HealthDeck Web | 8080 | 80 |
| PostgreSQL | 5432 | 5432 |
| SQL Server | 1433 | 1433 |
| Redis | 6379 | 6379 |
| RabbitMQ | 5672, 15672 | 5672, 15672 |
| Elasticsearch | 9200, 9300 | 9200, 9300 |
| Kibana | 5601 | 5601 |

## Resource Requirements

### Development (eshop-dev)

- **Total CPU Requests:** ~1.5 cores
- **Total Memory Requests:** ~3-4 GB
- **Storage:** ~30 GB (Bronze)

### Production (eshop-prod)

- **Total CPU Requests:** ~6-8 cores
- **Total Memory Requests:** ~15-20 GB
- **Storage:** ~150 GB (Gold)

## Support

- **Full Documentation:** `deploy/helm/eshop-microservices/README.md`
- **GitLab Issues:** https://gitlab.com/your-org/eshop-microservices/issues
- **IBM Cloud Docs:** https://cloud.ibm.com/docs/containers

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-01-27 | Initial production release |
