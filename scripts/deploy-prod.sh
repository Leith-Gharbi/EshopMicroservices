#!/bin/bash
# Deploy to Production environment (eshop-prod namespace)
# This script mirrors the GitLab CI/CD deploy:prod stage for local deployment
#
# IMPORTANT: This script requires a version tag for production deployment!
#
# Usage (from Git Bash on Windows or bash on Linux/Mac):
#   ./scripts/deploy-prod.sh <version-tag>
#
# Example:
#   ./scripts/deploy-prod.sh v1.0.0
#   ./scripts/deploy-prod.sh abc123def

echo "========================================="
echo "EshopMicroservices - PRODUCTION Deployment"
echo "========================================="
echo ""
echo "Starting at: $(date)"
echo "Shell: $SHELL"
echo "Bash version: $BASH_VERSION"
echo ""

# Exit on error, but show where it failed
set -e
set -o pipefail

# Trap errors to show where the script failed and pause
trap 'echo ""; echo "ERROR: Script failed at line $LINENO with exit code $?"; echo "Press Enter to exit..."; read -r' ERR

# Get the directory where the script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "Script directory: $SCRIPT_DIR"
echo "Project root: $PROJECT_ROOT"
echo ""

# Load .env file FIRST if exists
if [ -f "$SCRIPT_DIR/.env" ]; then
  echo "Loading environment variables from .env file..."
  set -a  # automatically export all variables
  source "$SCRIPT_DIR/.env"
  set +a
  echo "  .env file loaded successfully"
else
  echo "No .env file found at $SCRIPT_DIR/.env"
  echo "You can create one from .env.example:"
  echo "  cp scripts/.env.example scripts/.env"
  echo ""
fi

# Configuration (after loading .env)
CLUSTER_NAME="${IKS_CLUSTER_NAME:-lgharbi-eshop-cluster-k8s}"
NAMESPACE="eshop-prod"
RELEASE_NAME="eshop-prod"
HELM_CHART_PATH="$PROJECT_ROOT/deploy/helm/eshop-microservices"
IMAGE_TAG="${1}"
ICR_REGISTRY="${ICR_REGISTRY:-de.icr.io}"
ICR_NAMESPACE="${ICR_NAMESPACE:-eshop-images}"

# =========================================
# Validation: Image tag is REQUIRED for production
# =========================================
if [ -z "$IMAGE_TAG" ]; then
  echo "========================================="
  echo "ERROR: Image tag is REQUIRED for production deployment"
  echo "========================================="
  echo ""
  echo "Usage: $0 <version-tag>"
  echo ""
  echo "Examples:"
  echo "  $0 v1.0.0"
  echo "  $0 abc123def"
  echo "  $0 stable"
  echo ""
  echo "Press Enter to exit..."
  read -r
  exit 1
fi

# Check for required environment variable
if [ -z "$IBM_CLOUD_API_KEY" ]; then
  echo "========================================="
  echo "ERROR: IBM_CLOUD_API_KEY environment variable is not set"
  echo "========================================="
  echo ""
  echo "Please set your IBM Cloud API key using one of these methods:"
  echo ""
  echo "Option 1: Create a .env file in scripts directory:"
  echo "  cp scripts/.env.example scripts/.env"
  echo "  # Then edit scripts/.env and add your API key"
  echo ""
  echo "Option 2: Export the variable before running:"
  echo "  export IBM_CLOUD_API_KEY='your-api-key'"
  echo "  ./scripts/deploy-prod.sh v1.0.0"
  echo ""
  echo "Get your API key from: https://cloud.ibm.com/iam/apikeys"
  echo ""
  echo "Press Enter to exit..."
  read -r
  exit 1
fi

# Verify helm chart exists
if [ ! -d "$HELM_CHART_PATH" ]; then
  echo "ERROR: Helm chart not found at $HELM_CHART_PATH"
  echo "Press Enter to exit..."
  read -r
  exit 1
fi

if [ ! -f "$HELM_CHART_PATH/values-prod.yaml" ]; then
  echo "ERROR: values-prod.yaml not found at $HELM_CHART_PATH/values-prod.yaml"
  echo "Press Enter to exit..."
  read -r
  exit 1
fi

echo ""
echo "Configuration:"
echo "  Cluster: $CLUSTER_NAME"
echo "  Namespace: $NAMESPACE"
echo "  Helm chart: $HELM_CHART_PATH"
echo "  Image tag: $IMAGE_TAG"
echo "  Registry: $ICR_REGISTRY/$ICR_NAMESPACE"
echo "  API Key: ***${IBM_CLOUD_API_KEY: -4}"
echo ""

# =========================================
# PRODUCTION CONFIRMATION PROMPT
# =========================================
echo "========================================="
echo "⚠️  WARNING: PRODUCTION DEPLOYMENT ⚠️"
echo "========================================="
echo ""
echo "You are about to deploy to PRODUCTION!"
echo ""
echo "  Namespace: $NAMESPACE"
echo "  Image tag: $IMAGE_TAG"
echo "  Cluster:   $CLUSTER_NAME"
echo ""
echo "This action will affect real users and data."
echo ""
read -p "Are you sure you want to continue? (type 'yes' to confirm): " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
  echo ""
  echo "Deployment cancelled."
  echo ""
  echo "Press Enter to exit..."
  read -r
  exit 0
fi

echo ""
echo "Production deployment confirmed. Proceeding..."
echo ""

# Check required tools
echo "Checking required tools..."
for cmd in ibmcloud kubectl helm; do
  if ! command -v $cmd &> /dev/null; then
    echo "ERROR: $cmd is not installed or not in PATH"
    echo "Press Enter to exit..."
    read -r
    exit 1
  fi
  echo "  $cmd: OK"
done
echo ""

# =========================================
# Step 1: Login to IBM Cloud
# =========================================
echo "Step 1: Logging in to IBM Cloud..."
ibmcloud login --apikey "$IBM_CLOUD_API_KEY" -r "${IBM_CLOUD_REGION:-eu-de}" -g "${IBM_CLOUD_RESOURCE_GROUP:-Default}" --quiet

# =========================================
# Step 2: Configure kubectl for IKS cluster
# =========================================
echo ""
echo "Step 2: Configuring kubectl for cluster: $CLUSTER_NAME"
ibmcloud ks cluster config --cluster "$CLUSTER_NAME"

# Verify connection
echo "Verifying cluster connection..."
kubectl get nodes

# =========================================
# Step 3: Create namespace (if not exists)
# =========================================
echo ""
echo "Step 3: Creating namespace $NAMESPACE (if not exists)..."
kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

# =========================================
# Step 4: Create ImagePullSecret for IBM Container Registry
# =========================================
echo ""
echo "Step 4: Creating ImagePullSecret for IBM Container Registry..."
kubectl create secret docker-registry icr-secret \
  --docker-server="$ICR_REGISTRY" \
  --docker-username=iamapikey \
  --docker-password="$IBM_CLOUD_API_KEY" \
  --namespace="$NAMESPACE" \
  --dry-run=client -o yaml | kubectl apply -f -

echo "ImagePullSecret 'icr-secret' created/updated in namespace $NAMESPACE"

# =========================================
# Step 5: Check and fix stuck Helm releases
# =========================================
echo ""
echo "Step 5: Checking for stuck Helm releases..."

if command -v jq &> /dev/null; then
  RELEASE_STATUS=$(helm status "$RELEASE_NAME" -n "$NAMESPACE" -o json 2>/dev/null | jq -r '.info.status' || echo "not-found")

  if [ "$RELEASE_STATUS" = "pending-install" ] || [ "$RELEASE_STATUS" = "pending-upgrade" ] || [ "$RELEASE_STATUS" = "pending-rollback" ]; then
    echo "WARNING: Release is stuck in '$RELEASE_STATUS' state. Attempting rollback..."
    helm rollback "$RELEASE_NAME" -n "$NAMESPACE" || helm uninstall "$RELEASE_NAME" -n "$NAMESPACE" --no-hooks || true
    echo "Rollback completed."
  elif [ "$RELEASE_STATUS" = "not-found" ]; then
    echo "No existing release found. Will perform fresh install."
  else
    echo "Release status: $RELEASE_STATUS (OK)"
  fi
else
  echo "jq not installed, skipping stuck release detection"
fi

# =========================================
# Step 6: Deploy with Helm
# =========================================
echo ""
echo "Step 6: Deploying Helm chart to namespace: $NAMESPACE"
echo "========================================="
echo ""

DEPLOYMENT_TIMESTAMP=$(date +%s)

echo "Running helm upgrade..."
set +e
helm upgrade --install "$RELEASE_NAME" "$HELM_CHART_PATH" \
  --namespace "$NAMESPACE" \
  --create-namespace \
  --values "$HELM_CHART_PATH/values-prod.yaml" \
  --set global.imageTag="$IMAGE_TAG" \
  --set global.imageRegistry="$ICR_REGISTRY/$ICR_NAMESPACE" \
  --set "global.imagePullSecrets[0].name=icr-secret" \
  --set global.deploymentTimestamp="$DEPLOYMENT_TIMESTAMP" \
  --wait --timeout 15m \
  --debug 2>&1 | tee helm-deploy-prod.log

HELM_EXIT_CODE=${PIPESTATUS[0]}
set -e

if [ $HELM_EXIT_CODE -ne 0 ]; then
  echo ""
  echo "========================================="
  echo "ERROR: Helm upgrade failed with exit code $HELM_EXIT_CODE"
  echo "========================================="
  echo "Check helm-deploy-prod.log for details"
  echo ""
  echo "Checking pod status..."
  kubectl get pods -n "$NAMESPACE" 2>/dev/null || true
  echo ""
  echo "Recent events:"
  kubectl get events -n "$NAMESPACE" --sort-by='.lastTimestamp' 2>/dev/null | tail -20 || true
  echo ""
  echo "⚠️  PRODUCTION DEPLOYMENT FAILED!"
  echo "Consider rolling back to the previous version."
  echo ""
  echo "Press Enter to exit..."
  read -r
  exit $HELM_EXIT_CODE
fi

# =========================================
# Step 7: Display deployment status
# =========================================
echo ""
echo "========================================="
echo "🎉 PRODUCTION Deployment Complete!"
echo "========================================="
echo ""
echo "Version deployed: $IMAGE_TAG"
echo ""
echo "Pods:"
kubectl get pods -n "$NAMESPACE"
echo ""
echo "Services:"
kubectl get svc -n "$NAMESPACE"
echo ""
echo "Ingress:"
kubectl get ingress -n "$NAMESPACE"
echo ""
echo "========================================="
echo "Access URLs (Production):"
echo "========================================="
echo ""
echo "Shopping Web:     http://prod-eshop.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud"
echo "API Gateway:      http://prod-api.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud"
echo "Health Dashboard: http://prod-health.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud"
echo "Kibana:           http://prod-kibana.lgharbi-eshop-cluster-k8s-8a833d5c3d6a9b7ed1b32c0af11fc140-0000.eu-de.containers.appdomain.cloud"
echo ""
echo "========================================="
echo "⚠️  IMPORTANT: Monitor the deployment!"
echo "========================================="
echo ""
echo "Useful Commands:"
echo ""
echo "View logs:"
echo "  kubectl logs -f deployment/$RELEASE_NAME-eshop-microservices-catalog-api -n $NAMESPACE"
echo ""
echo "Check pod health:"
echo "  kubectl get pods -n $NAMESPACE -w"
echo ""
echo "Rollback if needed:"
echo "  helm rollback $RELEASE_NAME -n $NAMESPACE"
echo ""
echo "Uninstall (DANGER):"
echo "  helm uninstall $RELEASE_NAME -n $NAMESPACE"
echo ""
echo "========================================="
echo "Production deployment finished at: $(date)"
echo "Version: $IMAGE_TAG"
echo "========================================="
echo ""
echo "Press Enter to close this window..."
read -r
