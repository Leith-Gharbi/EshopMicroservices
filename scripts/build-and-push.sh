#!/bin/bash
# Build and push Docker images to IBM Container Registry
# For local development and testing

set -e

echo "========================================="
echo "Building and Pushing Docker Images"
echo "========================================="

# Configuration
REGISTRY="de.icr.io/eshop-images"
TAG="${1:-latest}"

# Services to build
declare -a SERVICES=(
  "catalog-api:src/Services/Catalog/Catalog.API:src"
  "basket-api:src/Services/Basket/Basket.API:src"
  "ordering-api:src/Services/Ordering/Ordering.API:src"
  "discount-grpc:src/Services/Discount/Discount.Grpc:src"
  "yarp-gateway:src/ApiGateways/YarpApiGateway:src"
  "shopping-web:src/WebApps/Shopping.Web:src"
  "healthdeck-web:src/WebApps/HealthDeck.Web:src"
)

# Login to IBM Cloud and Container Registry
echo "Logging in to IBM Cloud..."
ibmcloud login --apikey $IBM_CLOUD_API_KEY -r us-south -g default

echo "Logging in to IBM Container Registry..."
ibmcloud cr login

# Ensure namespace exists
echo "Ensuring namespace exists..."
ibmcloud cr namespace-list | grep -q eshop-images || ibmcloud cr namespace-add eshop-images

# Build and push each service
for SERVICE_INFO in "${SERVICES[@]}"; do
  IFS=':' read -r SERVICE DOCKERFILE_DIR BUILD_CONTEXT <<< "$SERVICE_INFO"

  echo ""
  echo "========================================="
  echo "Building: $SERVICE"
  echo "Tag: $TAG"
  echo "========================================="

  # Build image
  docker build -t $REGISTRY/$SERVICE:$TAG -f $DOCKERFILE_DIR/Dockerfile $BUILD_CONTEXT

  # Push to registry
  echo "Pushing: $REGISTRY/$SERVICE:$TAG"
  docker push $REGISTRY/$SERVICE:$TAG

  # Also tag and push as 'latest'
  if [ "$TAG" != "latest" ]; then
    docker tag $REGISTRY/$SERVICE:$TAG $REGISTRY/$SERVICE:latest
    docker push $REGISTRY/$SERVICE:latest
  fi

  echo "✓ $SERVICE completed"
done

echo ""
echo "========================================="
echo "All images built and pushed successfully!"
echo "========================================="
echo ""
echo "To deploy to dev:"
echo "  cd scripts"
echo "  ./deploy-dev.sh $TAG"
echo ""
echo "To view images in registry:"
echo "  ibmcloud cr images --restrict eshop-images"
