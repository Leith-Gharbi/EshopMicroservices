#!/bin/bash
# Cleanup script to remove deployments from all environments

set -e

echo "========================================="
echo "EshopMicroservices Cleanup Tool"
echo "========================================="

CLUSTER_NAME="lgharbi-eshop-cluster-k8s"

# Show menu
echo ""
echo "Select environment to cleanup:"
echo "  1) Development (eshop-dev)"
echo "  2) Staging (eshop-staging)"
echo "  3) Production (eshop-prod)"
echo "  4) All environments (DANGER!)"
echo "  5) Cancel"
echo ""
read -p "Enter choice [1-5]: " CHOICE

case $CHOICE in
  1)
    NAMESPACE="eshop-dev"
    ;;
  2)
    NAMESPACE="eshop-staging"
    ;;
  3)
    NAMESPACE="eshop-prod"
    echo ""
    echo "WARNING: You are about to cleanup PRODUCTION!"
    read -p "Type 'DELETE PRODUCTION' to confirm: " CONFIRM
    if [ "$CONFIRM" != "DELETE PRODUCTION" ]; then
      echo "Cleanup cancelled."
      exit 0
    fi
    ;;
  4)
    echo ""
    echo "DANGER: You are about to cleanup ALL ENVIRONMENTS!"
    read -p "Type 'DELETE ALL' to confirm: " CONFIRM
    if [ "$CONFIRM" != "DELETE ALL" ]; then
      echo "Cleanup cancelled."
      exit 0
    fi
    ALL_ENVS=true
    ;;
  5)
    echo "Cleanup cancelled."
    exit 0
    ;;
  *)
    echo "Invalid choice."
    exit 1
    ;;
esac

# Configure kubectl
echo "Configuring kubectl for cluster: $CLUSTER_NAME"
ibmcloud ks cluster config --cluster $CLUSTER_NAME

# Cleanup function
cleanup_namespace() {
  local NS=$1
  echo ""
  echo "Cleaning up namespace: $NS"
  echo "========================================="

  # Uninstall Helm release
  RELEASE_NAME="eshop-${NS#eshop-}"
  if helm list -n $NS | grep -q $RELEASE_NAME; then
    echo "Uninstalling Helm release: $RELEASE_NAME"
    helm uninstall $RELEASE_NAME --namespace $NS
  else
    echo "No Helm release found for $RELEASE_NAME"
  fi

  # Optional: Delete namespace entirely
  read -p "Delete entire namespace '$NS'? (yes/no): " DELETE_NS
  if [ "$DELETE_NS" == "yes" ]; then
    echo "Deleting namespace: $NS"
    kubectl delete namespace $NS --ignore-not-found=true
  fi

  echo "✓ Cleanup complete for $NS"
}

# Execute cleanup
if [ "$ALL_ENVS" == "true" ]; then
  cleanup_namespace "eshop-dev"
  cleanup_namespace "eshop-staging"
  cleanup_namespace "eshop-prod"
else
  cleanup_namespace "$NAMESPACE"
fi

echo ""
echo "========================================="
echo "Cleanup completed!"
echo "========================================="
