# Azure Kubernetes Service (AKS) Deployment Guide

This guide explains how to deploy the Quote Azure K8 Backend application to Azure Kubernetes Service (AKS) for production.

## 🎯 Cost-Effective Testing Strategy

**Important**: For cost-effective testing, use the **Delete/Recreate Strategy** instead of stop/start. This stops ALL billing (including control plane) when not testing.

### Quick Testing Commands
```bash
# Start testing (creates everything)
./create-test-cluster.sh

# Stop testing (deletes everything - $0/hour)
./delete-test-cluster.sh

# Check current test resources
az group list --query "[?contains(name, 'test-')].name" -o tsv
```

### Cost Comparison
| Strategy | Hourly Cost | When Testing | When Not Testing |
|----------|-------------|--------------|------------------|
| **Delete/Recreate (Spot)** | **$0.09/hour** | **$0.09/hour** | **$0/hour** |
| **Delete/Recreate (Regular)** | $0.30/hour | $0.30/hour | **$0/hour** |
| **Stop/Start** | $0.30/hour | $0.30/hour | $0.10/hour |
| **Always Running** | $0.30/hour | $0.30/hour | $0.30/hour |

## Prerequisites

1. **Azure CLI installed**
   ```bash
   # Install Azure CLI
   curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
   
   # Verify installation
   az --version
   ```

2. **kubectl installed**
   ```bash
   # Check if kubectl is already installed
   which kubectl
   kubectl version --client
   
   # If not installed, install via Homebrew (recommended for macOS)
   brew install kubectl
   
   # Alternative: Use Azure CLI to install (same kubectl, just convenient)
   az aks install-cli
   
   # Note: Azure CLI just installs standard kubectl - no special Azure-specific features
   # Your existing kubectl installation will work perfectly with AKS
   ```

3. **Docker installed**
   - Verify with: `docker --version`

4. **Azure Subscription**
   - You need an active Azure subscription with sufficient permissions

## 🚀 Step 1: Cost-Effective Testing Scripts

### Create Test Cluster Script

Create `create-test-cluster.sh`:

```bash
#!/bin/bash
# create-test-cluster.sh
PERMANENT_RG="quote-azure-k8-backend"  # Existing resource group with ACR/Storage
RESOURCE_GROUP="test-$(date +%s)"   # Temporary resource group for AKS
AKS_NAME="test-aks"
LOCATION="westeurope"
ACR_NAME="kabulterquoteazurek8acr"

echo "=== Creating Complete Test Environment ==="
echo "Permanent RG: $PERMANENT_RG (ACR, Storage)"
echo "Temporary RG: $RESOURCE_GROUP (AKS)"

# Create temporary resource group for AKS
az group create --name $RESOURCE_GROUP --location $LOCATION

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

# Remove default node pool to use only spot instances
echo "Removing default node pool..."
az aks nodepool delete \
  --resource-group $RESOURCE_GROUP \
  --cluster-name $AKS_NAME \
  --name nodepool1 \
  --no-wait

# Get credentials
echo "Getting cluster credentials..."
az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_NAME --overwrite-existing

# Wait for cluster to be ready
echo "Waiting for cluster to be ready..."
kubectl wait --for=condition=ready pod -l k8s-app=kube-dns -n kube-system --timeout=300s

echo "=== Cluster Ready! ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "AKS Name: $AKS_NAME"
echo "Node Size: B2s_v2 Spot"
echo "Cost: ~$0.09/hour (up to 70% savings!)"
echo ""
echo "To stop everything, run: ./delete-test-cluster.sh"
```

### Delete Everything Script

Create `delete-test-cluster.sh`:

```bash
#!/bin/bash
# delete-test-cluster.sh

echo "=== Finding Test Resources ==="
# Find the most recent test resource group
RESOURCE_GROUP=$(az group list --query "[?contains(name, 'test-')].name" -o tsv | sort -r | head -1)

if [ ! -z "$RESOURCE_GROUP" ]; then
    echo "Found test resource group: $RESOURCE_GROUP"
    echo "Deleting ALL resources..."
    
    # Delete entire resource group (stops ALL billing)
    az group delete --name $RESOURCE_GROUP --yes --no-wait
    
    echo "=== Complete Stop Achieved! ==="
    echo "All resources deleted"
    echo "Cost: $0/hour"
    echo "Resource Group: $RESOURCE_GROUP"
else
    echo "No test resource groups found"
fi
```

### Make Scripts Executable

```bash
chmod +x create-test-cluster.sh
chmod +x delete-test-cluster.sh
```

## 🏗️ Step 2: Resource Group Strategy

### Two Resource Groups Approach

**Permanent Resources** (keep these running):
- **`quote-azure-k8-backend`** - ACR, Storage Account
- **Cost**: ~$1-3/month (very cheap)
- **Breakdown**: ACR (~$0.05) + Storage (~$0.10) + Data transfer (~$0.50-2.00)

**Temporary Resources** (created/deleted by scripts):
- **`test-<timestamp>`** - AKS cluster only
- **Cost**: $0.15/hour when testing, $0 when deleted

## 🏗️ Step 3: One-Time Setup

```bash
# Create permanent resource group
PERMANENT_RG="quote-azure-k8-backend"
LOCATION="westeurope"
ACR_NAME="kabulterquoteazurek8acr"

az group create --name $PERMANENT_RG --location $LOCATION

# Create ACR (permanent)
az acr create --resource-group $PERMANENT_RG --name $ACR_NAME --sku Basic

# Create Storage Account (permanent)
az storage account create \
  --name kabulterquotek8store \
  --resource-group $PERMANENT_RG \
  --location $LOCATION \
  --sku Standard_LRS

# Get storage connection string for ConfigMap
STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name kabulterquotek8store \
  --resource-group $PERMANENT_RG \
  --query "connectionString" --output tsv)

echo "Storage Connection String: $STORAGE_CONNECTION"
```

## 📦 Step 4: Build and Push Docker Image

```bash
# Navigate to project directory
cd quote-azure-k8-backend

# Build the production image
docker build -t quote-azure-k8-backend:latest .

# Tag for Azure Container Registry
docker tag quote-azure-k8-backend:latest kabulterquoteazurek8acr.azurecr.io/quote-azure-k8-backend:latest

# Login to ACR
az acr login --name kabulterquoteazurek8acr

# Push the image
docker push kabulterquoteazurek8acr.azurecr.io/quote-azure-k8-backend:latest

# Verify the image
az acr repository list --name kabulterquoteazurek8acr --output table
```

## ⚙️ Step 5: Create Kubernetes Manifests

### Create ConfigMap for AKS

Create `configmap-aks.yaml` (replace with your actual connection string):

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: quote-app-config
  namespace: quote-app
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  TableStorageConnectionString: "YOUR_STORAGE_CONNECTION_STRING_HERE"
  Logging__Console__Enabled: "false"
  Logging__Console__LogLevel__Default: "Warning"
```

**Important**: Replace `YOUR_STORAGE_CONNECTION_STRING_HERE` with the connection string from the One-Time Setup.

### Create Secret for JWT

```bash
# Create JWT secret for AKS (use a secure key in production)
kubectl create secret generic quote-app-secret-aks \
  --from-literal=JwtSecret=$(echo -n "your-production-jwt-secret-key" | base64) \
  --namespace=quote-app
```

**Note**: This creates `quote-app-secret-aks` to avoid conflicts with local deployment's `quote-app-secret`.

### Create Deployment for AKS

Create `deployment-aks.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: quote-app-deployment
  namespace: quote-app
  labels:
    app: quote-app
spec:
  replicas: 1
  selector:
    matchLabels:
      app: quote-app
  template:
    metadata:
      labels:
        app: quote-app
    spec:
      containers:
      - name: quote-app
        image: kabulterquoteazurek8acr.azurecr.io/quote-azure-k8-backend:latest
        imagePullPolicy: Always
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
        livenessProbe:
          httpGet:
            path: /api/quotes/random
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
        readinessProbe:
          httpGet:
            path: /api/quotes/random
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
          timeoutSeconds: 3
```

### Create Service for AKS

Create `service-aks.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: quote-app-service
  namespace: quote-app
spec:
  selector:
    app: quote-app
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
```

## 🚀 Step 6: Testing Workflow

### 🎯 One-Command Complete Setup

**The updated `create-test-cluster.sh` now handles everything automatically!**

```bash
# 🚀 ONE COMMAND - Does everything!
./create-test-cluster.sh
```

**What this script now does automatically:**
- ✅ Creates AKS cluster with B2s_v2 spot instances
- ✅ Sets up ACR authentication and credentials
- ✅ Builds and pushes correct AMD64 image
- ✅ Creates namespace, secrets, and ConfigMap
- ✅ Deploys application to spot pool with all environment variables
- ✅ Tests the application
- ✅ Provides ready-to-use URL

### 📋 Manual Steps (If needed)

If you prefer manual setup or need to troubleshoot:

```bash
# Start testing (creates fresh cluster)
./create-test-cluster.sh

# Manual namespace creation (if script fails)
kubectl create namespace quote-app

# Manual secret creation (if script fails)
kubectl create secret generic quote-app-secret-aks \
  --from-literal=JwtSecret=$(echo -n "mijn-azure-k8-pipo-secret" | base64) \
  --namespace=quote-app

# Manual ConfigMap creation (if script fails)
kubectl apply -f quote-azure-k8-backend/k8s-aks-deployment/configmap-aks.yaml

# Manual deployment (if script fails)
kubectl apply -f quote-azure-k8-backend/k8s-aks-deployment/deployment-aks-spot.yaml

# Test your application
kubectl get service quote-azure-k8-backend-service -n quote-app --watch
curl http://<external-ip>/api/quotes/random

# Stop everything (complete cleanup)
./delete-test-cluster.sh
```

### 🔧 Troubleshooting Common Issues

| Issue | Cause | Fix |
|-------|-------|-----|
| `ImagePullBackOff` | Wrong architecture | Script builds AMD64 automatically |
| `401 Unauthorized` | ACR authentication | Script sets up pull secrets |
| `Namespace not found` | Order of operations | Create namespace first |
| Pod on regular pool | No spot tolerations | Use spot deployment file |
| `ConfigMap not found` | Missing storage connection | Script includes ConfigMap automatically |
| Environment variables missing | ConfigMap not referenced | Script includes all env variables |

### 🎉 Expected Output

After running the script, you should see:

```
=== 🎉 Complete Success! ===
Resource Group: test-1774363542
AKS Name: test-aks
Node Size: B2s_v2 Spot
Application: Deployed and running on spot instances
Service URL: http://4.175.2.76/api/quotes/random
Cost: ~$0.39/hour total (system + spot pool)

🚀 Your application is ready!
📊 Test with: curl http://4.175.2.76/api/quotes/random
🛑 Stop everything: ./delete-test-cluster.sh
```

### Cost Examples

| Testing Duration | Total Cost |
|------------------|------------|
| **1 hour** | ~$0.39 |
| **2 hours** | ~$0.78 |
| **4 hours** | ~$1.56 |
| **8 hours** | ~$3.12 |
| **Weekend (8h/day)** | ~$24.96 |

**💡 Cost Breakdown:**
- System Pool (required): ~$0.30/hour
- Your App on Spot Pool: ~$0.09/hour (70% savings!)
- **Total**: ~$0.39/hour vs ~$0.60/hour (all regular nodes)

---

## 📚 Additional Resources

### Production Deployment Considerations
For production deployment, consider:
- Using Azure Key Vault for secrets management
- Implementing CI/CD pipelines
- Setting up monitoring and alerting
- Configuring backup strategies
- Using managed identities instead of connection strings

### Troubleshooting
- Check pod logs: `kubectl logs -f deployment/quote-app-deployment -n quote-app`
- Verify service endpoints: `kubectl get endpoints -n quote-app`
- Test connectivity: `kubectl exec -it <pod-name> -n quote-app -- curl localhost:8080/api/quotes/random`

### Cost Optimization
- Use spot instances for non-critical workloads
- Implement auto-scaling based on demand
- Regular cleanup of unused resources
- Monitor usage with Azure Cost Management

---

**Happy testing with your cost-effective AKS setup!** 🚀
