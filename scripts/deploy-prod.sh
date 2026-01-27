#!/bin/bash
# Deploy to Production environment (eshop-prod namespace)

set -e

echo "========================================="
echo "Deploying EshopMicroservices to Production"
echo "========================================="

# Configuration
CLUSTER_NAME="lgharbi-eshop-cluster-k8s"
NAMESPACE="eshop-prod"
HELM_CHART_PATH="../deploy/helm/eshop-microservices"
IMAGE_TAG="${1}"

# Validation
if [ -z "$IMAGE_TAG" ]; then
  echo "ERROR: Image tag is required for production deployment"
  echo "Usage: $0 <version-tag>"
  echo "Example: $0 v1.0.0"
  exit 1
fi

# Confirmation prompt
echo ""
echo "WARNING: You are about to deploy to PRODUCTION!"
echo "Namespace: $NAMESPACE"
echo "Image tag: $IMAGE_TAG"
echo ""
read -p "Are you sure you want to continue? (yes/no): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
  echo "Deployment cancelled."
  exit 0
fi

# Login to IBM Cloud (optional - uncomment if needed)
# echo "Logging in to IBM Cloud..."
# ibmcloud login --apikey $IBM_CLOUD_API_KEY -r us-south -g default

# Configure kubectl for IKS cluster
echo "Configuring kubectl for cluster: $CLUSTER_NAME"
ibmcloud ks cluster config --cluster $CLUSTER_NAME

# Verify connection
echo "Verifying cluster connection..."
kubectl get nodes

# Deploy with Helm
echo ""
echo "Deploying Helm chart to namespace: $NAMESPACE"
echo "Image tag: $IMAGE_TAG"
echo ""

helm upgrade --install eshop-prod $HELM_CHART_PATH \
  --namespace $NAMESPACE \
  --create-namespace \
  --values $HELM_CHART_PATH/values-prod.yaml \
  --set global.imageTag=$IMAGE_TAG \
  --set global.imageRegistry=de.icr.io/eshop-images \
  --wait --timeout 15m

# Display deployment status
echo ""
echo "========================================="
echo "Production Deployment Complete!"
echo "========================================="
echo ""
echo "Pods:"
kubectl get pods -n $NAMESPACE
echo ""
echo "Services:"
kubectl get svc -n $NAMESPACE
echo ""
echo "Ingress:"
kubectl get ingress -n $NAMESPACE
echo ""
echo "To view logs:"
echo "  kubectl logs -f deployment/eshop-prod-catalog-api -n $NAMESPACE"
echo ""
echo "IMPORTANT: Monitor the deployment closely!"
