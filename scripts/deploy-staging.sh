#!/bin/bash
# Deploy to Staging environment (eshop-staging namespace)

set -e

echo "========================================="
echo "Deploying EshopMicroservices to Staging"
echo "========================================="

# Configuration
CLUSTER_NAME="lgharbi-eshop-cluster-k8s"
NAMESPACE="eshop-staging"
HELM_CHART_PATH="../deploy/helm/eshop-microservices"
IMAGE_TAG="${1:-latest}"

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

helm upgrade --install eshop-staging $HELM_CHART_PATH \
  --namespace $NAMESPACE \
  --create-namespace \
  --values $HELM_CHART_PATH/values-staging.yaml \
  --set global.imageTag=$IMAGE_TAG \
  --set global.imageRegistry=de.icr.io/eshop-images \
  --wait --timeout 15m

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
echo "  kubectl logs -f deployment/eshop-staging-catalog-api -n $NAMESPACE"
