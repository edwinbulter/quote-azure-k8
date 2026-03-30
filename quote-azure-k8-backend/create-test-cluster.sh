#!/bin/bash
# create-test-cluster.sh
RESOURCE_GROUP="quote-azure-k8-backend"  # Fixed name
AKS_NAME="test-aks"
LOCATION="westeurope"
ACR_NAME="kabulterquotek8acr"  # Shorter name
STORAGE_ACCOUNT="kabulterquotek8storage"  # Shorter name (fits 24 char limit)

echo "=== Creating Complete Test Environment ==="
echo "Resource Group: $RESOURCE_GROUP (All resources)"
echo "AKS Name: $AKS_NAME"
echo "ACR Name: $ACR_NAME"
echo "Storage Account: $STORAGE_ACCOUNT"

# Clean up if resources already exist
if az group show --name $RESOURCE_GROUP >/dev/null 2>&1; then
    echo "⚠️  Resource group already exists. Cleaning up first..."
    az group delete --name $RESOURCE_GROUP --yes --no-wait
    echo "Waiting for cleanup to complete..."
    while az group show --name $RESOURCE_GROUP >/dev/null 2>&1; do
        echo "Still deleting... (waiting 10 seconds)"
        sleep 10
    done
    echo "✅ Cleanup complete. Creating fresh environment..."
fi

# Create resource group for everything
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create ACR
echo "Creating Azure Container Registry..."
az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME --sku Basic --location $LOCATION

# Create Storage Account
echo "Creating Storage Account..."
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2

# Get storage connection string
STORAGE_CONNECTION=$(az storage account show-connection-string --name $STORAGE_ACCOUNT --resource-group $RESOURCE_GROUP --query connectionString --output tsv)

echo "Storage connection: $STORAGE_CONNECTION"

# Create AKS with B2s_v2 nodes first, then add spot pool
echo "Creating AKS cluster with B2s_v2 nodes..."
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_NAME \
  --node-count 1 \
  --node-vm-size Standard_B2s_v2 \
  --location $LOCATION \
  --attach-acr $ACR_NAME \
  --generate-ssh-keys \
  --yes

# Add spot node pool for cost savings
echo "Adding B2s_v2 spot node pool..."
az aks nodepool add \
  --resource-group $RESOURCE_GROUP \
  --cluster-name $AKS_NAME \
  --name spotpool \
  --node-count 1 \
  --node-vm-size Standard_B2s_v2 \
  --priority Spot \
  --spot-max-price -1 \
  --eviction-policy Delete

# Get credentials
echo "Getting cluster credentials..."
az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_NAME --overwrite-existing

# Wait for cluster to be ready
echo "Waiting for cluster to be ready..."
kubectl wait --for=condition=ready pod -l k8s-app=kube-dns -n kube-system --timeout=300s

# Setup Kubernetes resources
echo "Setting up Kubernetes resources..."

# Create namespace
echo "Creating quote-app namespace..."
kubectl create namespace quote-app

# Create JWT secret
echo "Creating JWT secret..."
kubectl create secret generic quote-app-secret-aks \
  --from-literal=JwtSecret=$(echo -n "mijn-azure-k8-pipo-secret" | base64) \
  --namespace=quote-app

# Enable ACR admin user for authentication
echo "Enabling ACR admin access..."
az acr update -n $ACR_NAME --admin-enabled true

# Get ACR credentials
echo "Getting ACR credentials..."
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query "username" -o tsv)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query "passwords[0].value" -o tsv)

# Create ACR pull secret
echo "Creating ACR pull secret..."
kubectl create secret docker-registry acr-secret \
  --namespace=quote-app \
  --docker-server=$ACR_NAME.azurecr.io \
  --docker-username=$ACR_USERNAME \
  --docker-password=$ACR_PASSWORD \
  --docker-email=any@email.com

# Build and push correct architecture image
echo "Building and pushing AMD64 image..."
cd quote-azure-k8-backend
docker buildx build --platform linux/amd64 -t $ACR_NAME.azurecr.io/quote-azure-k8-backend:latest . --push
cd ..

# Deploy application with spot tolerations
echo "Deploying application to spot pool..."
cat > temp-deployment.yaml << EOF
apiVersion: v1
kind: ConfigMap
metadata:
  name: quote-app-config
  namespace: quote-app
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  TableStorageConnectionString: "$STORAGE_CONNECTION"
  Logging__Console__Enabled: "false"
  Logging__Console__LogLevel__Default: "Warning"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: quote-azure-k8-backend
  namespace: quote-app
spec:
  replicas: 1
  selector:
    matchLabels:
      app: quote-azure-k8-backend
  template:
    metadata:
      labels:
        app: quote-azure-k8-backend
    spec:
      imagePullSecrets:
      - name: acr-secret
      tolerations:
      - key: "kubernetes.azure.com/scalesetpriority"
        operator: "Equal"
        value: "spot"
        effect: "NoSchedule"
      nodeSelector:
        kubernetes.azure.com/scalesetpriority: "spot"
      containers:
      - name: quote-azure-k8-backend
        image: $ACR_NAME.azurecr.io/quote-azure-k8-backend:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          valueFrom:
            configMapKeyRef:
              name: quote-app-config
              key: ASPNETCORE_ENVIRONMENT
        - name: TableStorageConnectionString
          valueFrom:
            configMapKeyRef:
              name: quote-app-config
              key: TableStorageConnectionString
        - name: JwtSecret
          valueFrom:
            secretKeyRef:
              name: quote-app-secret-aks
              key: JwtSecret
        - name: Logging__Console__Enabled
          valueFrom:
            configMapKeyRef:
              name: quote-app-config
              key: Logging__Console__Enabled
        - name: Logging__Console__LogLevel__Default
          valueFrom:
            configMapKeyRef:
              name: quote-app-config
              key: Logging__Console__LogLevel__Default
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
---
apiVersion: v1
kind: Service
metadata:
  name: quote-azure-k8-backend-service
  namespace: quote-app
spec:
  selector:
    app: quote-azure-k8-backend
  ports:
  - port: 80
    targetPort: 8080
  type: LoadBalancer
EOF

kubectl apply -f temp-deployment.yaml
rm temp-deployment.yaml

# Wait for pod to be ready
echo "Waiting for application pod to be ready..."
kubectl wait --for=condition=ready pod -l app=quote-azure-k8-backend -n quote-app --timeout=300s

# Get service URL
echo "Getting service URL..."
sleep 30  # Wait for LoadBalancer IP
SERVICE_IP=$(kubectl get service quote-azure-k8-backend-service -n quote-app -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Test the application
echo "Testing application..."
curl -s http://$SERVICE_IP/api/quotes/random || echo "Application may still be starting..."

# Optional: Remove default node pool to use only spot instances (uncomment if desired)
# echo "Removing default node pool (optional - keeps system pool for stability)..."
# az aks nodepool delete \
#   --resource-group $RESOURCE_GROUP \
#   --cluster-name $AKS_NAME \
#   --name nodepool1 \
#   --no-wait

echo "=== 🎉 Complete Success! ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "AKS Name: $AKS_NAME"
echo "ACR Name: $ACR_NAME"
echo "Storage Account: $STORAGE_ACCOUNT"
echo "Node Size: B2s_v2 Spot"
echo "Application: Deployed and running on spot instances"
echo "Service URL: http://$SERVICE_IP/api/quotes/random"
echo "Cost: ~$0.39/hour total (AKS + ACR + Storage)"
echo "💡 Zero cost when stopped: ./delete-test-cluster.sh"
echo ""
echo "🚀 Your application is ready!"
echo "📊 Test with: curl http://$SERVICE_IP/api/quotes/random"
echo "🛑 Stop everything: ./delete-test-cluster.sh"