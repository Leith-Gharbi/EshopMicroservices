#!/bin/bash
# Deploy to Development environment (eshop-dev namespace)

set -e
set -o pipefail

# Trap errors to show where the script failed
trap 'echo "ERROR: Script failed at line $LINENO with exit code $?"' ERR

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "========================================="
echo "Deploying EshopMicroservices to Development"
echo "========================================="
echo "Script directory: $SCRIPT_DIR"
echo "Project root: $PROJECT_ROOT"

# Configuration
CLUSTER_NAME="lgharbi-eshop-cluster-k8s"
NAMESPACE="eshop-dev"
HELM_CHART_PATH="$PROJECT_ROOT/deploy/helm/eshop-microservices"
IMAGE_TAG="${1:-latest}"

# Verify helm chart exists
if [ ! -d "$HELM_CHART_PATH" ]; then
  echo "ERROR: Helm chart not found at $HELM_CHART_PATH"
  exit 1
fi

if [ ! -f "$HELM_CHART_PATH/values-dev.yaml" ]; then
  echo "ERROR: values-dev.yaml not found at $HELM_CHART_PATH/values-dev.yaml"
  exit 1
fi

echo "Helm chart path: $HELM_CHART_PATH"

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

echo "Running helm upgrade..."
helm upgrade --install eshop-dev $HELM_CHART_PATH \
  --namespace $NAMESPACE \
  --create-namespace \
  --values $HELM_CHART_PATH/values-dev.yaml \
  --set global.imageTag=$IMAGE_TAG \
  --set global.imageRegistry=de.icr.io/eshop-images \
  --wait --timeout 15m \
  --debug 2>&1 | tee helm-deploy.log

HELM_EXIT_CODE=${PIPESTATUS[0]}
if [ $HELM_EXIT_CODE -ne 0 ]; then
  echo ""
  echo "========================================="
  echo "ERROR: Helm upgrade failed with exit code $HELM_EXIT_CODE"
  echo "========================================="
  echo "Check helm-deploy.log for details"
  echo ""
  echo "Checking pod status..."
  kubectl get pods -n $NAMESPACE 2>/dev/null || true
  echo ""
  echo "Recent events:"
  kubectl get events -n $NAMESPACE --sort-by='.lastTimestamp' 2>/dev/null | tail -20 || true
  echo ""
  echo "Press Enter to exit..."
  read -r
  exit $HELM_EXIT_CODE
fi

# Display deployment status
echo ""
echo "========================================="
echo "Deployment Complete!"
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
echo "  kubectl logs -f deployment/eshop-dev-catalog-api -n $NAMESPACE"
echo ""
echo "To access the application:"
echo "  kubectl port-forward svc/eshop-dev-yarp-gateway 8080:80 -n $NAMESPACE"
echo "  Then visit: http://localhost:8080"
