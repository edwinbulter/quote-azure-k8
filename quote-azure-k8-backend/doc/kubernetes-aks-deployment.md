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

## 🚀 Step 1: Zero-Cost Testing Scripts

### Create Test Cluster Script

Create `create-test-cluster.sh`:

```bash
#!/bin/bash
# create-test-cluster.sh
RESOURCE_GROUP="quote-azure-k8-backend"  # Fixed name
AKS_NAME="test-aks"
LOCATION="westeurope"
ACR_NAME="kabulterquotek8acr"  # Fixed name
STORAGE_ACCOUNT="kabulterquotek8storage"  # Fixed name

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

# Build and push Docker image
echo "Building and pushing Docker image..."
az acr login --name $ACR_NAME
cd quote-azure-k8-backend
docker buildx build --platform linux/amd64 -t $ACR_NAME.azurecr.io/quote-azure-k8-backend:latest . --push

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

# Get credentials and setup Kubernetes resources (namespace, secrets, ConfigMap, deployment, service)
# ... (rest of the automated setup)
```

### Delete Everything Script

Create `delete-test-cluster.sh`:

```bash
#!/bin/bash
# delete-test-cluster.sh

echo "=== Finding All Test Resources ==="
# Find ALL test resource groups
TEST_GROUPS=$(az group list --query "[?contains(name, 'test-')].name" -o tsv)

if [ ! -z "$TEST_GROUPS" ]; then
    echo "Found test resource groups:"
    echo "$TEST_GROUPS"
    echo ""
    echo "Deleting ALL test resource groups..."
    
    # Delete all test resource groups
    for GROUP in $TEST_GROUPS; do
        echo "Deleting: $GROUP"
        az group delete --name $GROUP --yes --no-wait
    done
    
    echo ""
    echo "=== Complete Stop Achieved! ==="
    echo "All test resources deleted"
    echo "Cost: $0/hour"
    echo "Deleted groups: $(echo "$TEST_GROUPS" | wc -l | tr -d ' ')"
else
    echo "No test resource groups found"
fi

echo ""
echo "=== Checking Permanent Resources ==="

# Check if permanent group exists
PERMANENT_RG="quote-azure-k8-backend"
RG_EXISTS=$(az group show --name $PERMANENT_RG --query name --output tsv 2>/dev/null)

if [ ! -z "$RG_EXISTS" ]; then
    echo "Found permanent resource group: $PERMANENT_RG"
    echo "Deleting permanent resources:"
    echo "  📦 Container Registry: kabulterquoteazurek8acr"
    echo "  💾 Storage Account: kabulterquotek8store"
    echo "  💰 Saving ~€0.14/day"
    echo ""
    echo "Deleting permanent resource group: $PERMANENT_RG"
    az group delete --name $PERMANENT_RG --yes --no-wait
    echo "✅ Permanent resources deleted - Zero cost achieved!"
else
    echo "✅ No permanent resource group found - Zero cost achieved!"
fi

echo ""
echo "=== Summary ==="
echo "🧹 Test resources: Deleted"
echo "📦 Permanent resources: Deleted"
echo "💰 Total cost: €0.00/hour"
echo "🎉 Zero cost achieved!"
```

### Make Scripts Executable

```bash
chmod +x create-test-cluster.sh
chmod +x delete-test-cluster.sh
```

## 🏗️ Step 2: Zero-Cost Resource Strategy

### All-in-One Fixed Resource Groups

**New Approach - Fixed Names with Auto-Cleanup**:
- **`quote-azure-k8-backend`** - AKS + ACR + Storage (everything)
- **Cost**: ~$0.41/hour when testing, **€0.00 when stopped**
- **Benefit**: True zero cost when not testing
- **Smart cleanup**: Automatically deletes existing resources first

**What Gets Created Each Test**:
- 📦 **ACR Registry**: `kabulterquotek8acr`
- 💾 **Storage Account**: `kabulterquotek8storage`
- 🚀 **AKS Cluster**: `test-aks` with spot instances
- 🌐 **LoadBalancer**: Public IP for external access

**What Gets Deleted**:
- ✅ Fixed resource group (`quote-azure-k8-backend`)
- ✅ Old test resource groups (`test-*`) - backward compatible
- ✅ ACR, Storage, AKS - everything!
- ✅ **Zero cost achieved** when stopped

## 🎯 Step 3: Usage

### 🚀 Start Testing (Creates Everything)

```bash
# One command creates complete environment
./create-test-cluster.sh
```

**What happens automatically:**
- ✅ Creates temporary resource group
- ✅ Creates ACR registry
- ✅ Creates storage account
- ✅ Creates AKS cluster with spot instances
- ✅ Builds and pushes Docker image
- ✅ Deploys application
- ✅ Provides ready-to-use URL

### 🛑 Stop Testing (Deletes Everything)

```bash
# One command deletes everything - zero cost!
./delete-test-cluster.sh
```

**What gets deleted automatically:**
- 🗑️ All test resource groups
- 🗑️ ACR registries
- 🗑️ Storage accounts
- 🗑️ AKS clusters
- 🗑️ Old permanent resources
- 💰 **Result: €0.00/hour**

### 📊 Cost Summary

| Status | Cost | Resources |
|--------|------|-----------|
| **Testing** | ~$0.41/hour | AKS + ACR + Storage |
| **Stopped** | **€0.00/hour** | Everything deleted |
| **Weekend off** | **€0.00** | True zero cost |

## 📦 Step 4: Docker Build & Push (Now Automatic)

**The Docker build and push process is now fully automated in the create script!**

### 🔧 What the Script Does Automatically:
```bash
# ✅ All handled by create-test-cluster.sh:

# 1. Login to Azure Container Registry
az acr login --name $ACR_NAME

# 2. Build Docker image for AMD64 (required for AKS)
docker buildx build --platform linux/amd64 -t $ACR_NAME.azurecr.io/quote-azure-k8-backend:latest . --push

# 3. Push image to ACR
# (The --push flag builds and pushes in one command)
```

### 📋 Docker Build Process Explained:

1. **🔐 ACR Login**: Authenticates with Azure Container Registry
2. **🏗️ Build**: Creates Docker image with AMD64 architecture (required for AKS)
3. **📤 Push**: Uploads image to ACR for Kubernetes to pull
4. **✅ Verify**: Image is now available in `kabulterquotek8acr.azurecr.io`

### 🎯 Result:
- ✅ **Image**: `kabulterquotek8acr.azurecr.io/quote-azure-k8-backend:latest`
- ✅ **Architecture**: AMD64 (compatible with AKS)
- ✅ **Ready**: Kubernetes can pull and deploy the image

### 🔍 Manual Docker Build (If Needed):
```bash
# Navigate to project directory
cd quote-azure-k8-backend

# Build the production image (AMD64 for AKS)
docker buildx build --platform linux/amd64 -t quote-azure-k8-backend:latest .

# Tag for Azure Container Registry
docker tag quote-azure-k8-backend:latest kabulterquotek8acr.azurecr.io/quote-azure-k8-backend:latest

# Login to ACR
az acr login --name kabulterquotek8acr

# Push the image
docker push kabulterquotek8acr.azurecr.io/quote-azure-k8-backend:latest

# Verify the image
az acr repository list --name kabulterquotek8acr --output table
```

## ⚙️ Step 5: Kubernetes Manifests (Now Automatic)

**All Kubernetes manifests are now created automatically by the create script!**

### What the Script Creates Automatically:
```bash
# ✅ All handled by create-test-cluster.sh:
cat > temp-deployment.yaml << EOF
# ConfigMap with dynamic storage connection
# Deployment with spot tolerations
# Service with LoadBalancer
EOF
kubectl apply -f temp-deployment.yaml
```

### Manual Kubernetes Setup (If Needed):

#### Create ConfigMap for AKS

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

#### Create Secret for JWT

```bash
# Create JWT secret for AKS (use a secure key in production)
kubectl create secret generic quote-app-secret-aks \
  --from-literal=JwtSecret=$(echo -n "your-production-jwt-secret-key" | base64) \
  --namespace=quote-app
```

#### Deploy Application

```bash
# Apply ConfigMap
kubectl apply -f configmap-aks.yaml

# Apply deployment with spot tolerations
kubectl apply -f deployment-aks-spot.yaml

# Apply service
kubectl apply -f service-aks.yaml
```

## 🚀 Step 6: Testing Workflow

### 🎯 One-Command Complete Setup

**The updated `create-test-cluster.sh` now handles everything automatically!**

```bash
# 🚀 ONE COMMAND - Does everything!
./create-test-cluster.sh
```

**What this script now does automatically:**
- ✅ Creates complete temporary resource group (AKS + ACR + Storage)
- ✅ Sets up ACR authentication and credentials
- ✅ Builds and pushes correct AMD64 image
- ✅ Creates namespace, secrets, and ConfigMap with dynamic storage
- ✅ Deploys application to spot pool with all environment variables
- ✅ Tests the application
- ✅ Provides ready-to-use URL
- ✅ **Zero cost when stopped** - everything deleted together

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
- AKS Cluster (system + spot pool): ~$0.39/hour
- ACR Registry: ~$0.01/hour (Basic tier)
- Storage Account: ~$0.01/hour (minimal usage)
- **Total**: ~$0.41/hour when running
- **Zero cost**: When stopped with `./delete-test-cluster.sh`

**🎯 Key Benefit: No permanent storage costs!** Everything is deleted when you stop testing.

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
