# Quote Azure K8 - Experimental Kubernetes Deployment Project

## 📋 Table of Contents

- [🎯 Project Overview](#-project-overview)
  - [🎓 Learning Objectives](#-learning-objectives)
  - [🚀 Project Goals](#-project-goals)
- [🏗️ Architecture](#️-architecture)
- [🛠️ Technology Stack](#️-technology-stack)
- [🚀 Quick Start](#-quick-start)
  - [Prerequisites](#prerequisites)
  - [🏃‍♂️ One-Command Azure Deployment](#️-one-command-azure-deployment)
  - [🧹 Complete Cleanup (Zero Cost)](#-complete-cleanup-zero-cost)
- [📋 Detailed Setup](#-detailed-setup)
  - [🔧 Environment Configuration](#-environment-configuration)
  - [🐳 Local Development](#-local-development)
  - [☁️ Azure Deployment Options](#️-azure-deployment-options)
- [📊 Cost Management](#-cost-management)
  - [💰 Zero-Cost Testing Strategy](#-zero-cost-testing-strategy)
  - [🎯 Cost Optimization Features](#-cost-optimization-features)
- [🔍 Monitoring & Troubleshooting](#-monitoring--troubleshooting)
  - [📊 Check Application Status](#-check-application-status)
  - [🐛 Common Issues](#-common-issues)
  - [🔧 Common Tasks](#-common-tasks)
- [🔐 Security Notes](#-security-notes)
- [🤝 Contributing](#-contributing)
- [📄 License](#-license)
- [🎓 Learning Path](#-learning-path)

## 🎯 Project Overview

**Quote Azure K8** is an experimental learning project focused on developing and deploying .NET applications to Azure Kubernetes Service (AKS). This project demonstrates the complete journey from local development to production deployment, exploring various deployment strategies and best practices.

### 🎓 Learning Objectives

This project serves as a comprehensive learning platform for:

- **Container Development**: Docker containerization of .NET applications
- **Local Kubernetes**: Testing with Docker Desktop Kubernetes
- **Azure Services Integration**: Working with Azure Storage, Container Registry, and AKS
- **Infrastructure as Code**: Automated resource provisioning and management
- **Cost Optimization**: Zero-cost testing with spot instances and automated cleanup
- **DevOps Practices**: CI/CD pipelines, GitOps concepts, and security best practices

### 🚀 Project Goals

✅ **Step 1**: Migrate from Azure Function App to Azure Container Web App  
✅ **Step 2**: Local Kubernetes deployment with Docker Desktop  
✅ **Step 3**: Azure Kubernetes Service deployment with automation  
🔄 **Step 4**: Terraform deployment to Azure Container Apps (planned)

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   .NET API      │    │   Docker Image   │    │   Azure AKS     │
│                 │───▶│                  │───▶│                 │
│  Quote Service  │    │   Containerized  │    │  Spot Instances │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │                       │
                                ▼                       ▼
                       ┌──────────────────┐    ┌─────────────────┐
                       │   Azure ACR      │    │  Azure Storage  │
                       │                  │    │                 │
                       │ Container Registry│    │   Table Storage │
                       └──────────────────┘    └─────────────────┘
```

## 🛠️ Technology Stack

- **Backend**: .NET 8.0 Web API
- **Containerization**: Docker & Docker Compose
- **Local Storage**: Azurite (Azure Storage Emulator)
- **Orchestration**: Kubernetes (Docker Desktop + AKS)
- **Cloud Provider**: Microsoft Azure
- **Container Registry**: Azure Container Registry (ACR)
- **Infrastructure**: Azure CLI Scripts (automation ready for Terraform)
- **Authentication**: JWT Tokens

## 🚀 Quick Start

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) with Kubernetes enabled
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- Azure subscription with AKS permissions

### 🏃‍♂️ One-Command Azure Deployment

**⚠️ EXPERIMENTAL CODE - FOR LEARNING PURPOSES ONLY**

This project provides automated scripts for zero-cost testing on Azure:

```bash
# 1. Clone and setup
git clone <repository-url>
cd quote-azure-k8/quote-azure-k8-backend

# 2. Configure environment
cp .env.example .env
# Edit .env with your preferred names (defaults provided)

# 3. Deploy everything to Azure
./create-test-cluster.sh
```

**What happens automatically:**
- ✅ Creates Azure Resource Group, ACR, and Storage Account
- ✅ Builds and pushes Docker image to ACR
- ✅ Deploys AKS cluster with cost-effective spot instances
- ✅ Configures Kubernetes resources (secrets, configmaps, deployments)
- ✅ Exposes the application via LoadBalancer
- ✅ Provides ready-to-use endpoint URL

### 🧹 Complete Cleanup (Zero Cost)

```bash
# Delete everything - achieve zero cost
./delete-test-cluster.sh
```

You can check the status of the resource groups in the Azure portal by clicking the specific resource group name. When it is being deleted, you will see a message saying "Deleting".


**What gets deleted:**
- 🗑️ All Azure resources (AKS, ACR, Storage)
- 🗑️ Resource groups and configurations
- 💰 **Result: €0.00/hour** - true zero cost

## 📋 Detailed Setup

### 🔧 Environment Configuration

Create your `.env` file:

```bash
# Copy the template
cp .env.example .env

# Edit with your preferences
nano .env
```

**Required Configuration:**
```bash
# JWT Configuration - REQUIRED FOR PRODUCTION
JWT_SECRET=your-secure-jwt-secret-here-min-32-characters

# Azure Resource Configuration
RESOURCE_GROUP=quote-azure-k8-backend
AKS_NAME=test-aks
LOCATION=westeurope
ACR_NAME=kabulterquotek8acr
STORAGE_ACCOUNT=kabulterquotek8storage
```

### 🐳 Local Development

#### Option 1: Docker Compose (Recommended for local testing)
```bash
# Start with Azurite storage emulator
docker-compose up -d

# View logs
docker-compose logs -f quote-azure-k8-backend

# Test API
curl http://localhost:5001/api/quotes/random
```

#### Option 2: Local Kubernetes
```bash
# Build and deploy to Docker Desktop Kubernetes
kubectl apply -f k8s-deployment/

# Port forward to access service
kubectl port-forward service/quote-app-service 8080:80 -n quote-app &

# Test API
curl http://localhost:8080/api/quotes/random
```

### ☁️ Azure Deployment

Automated Script:
```bash
# One-command deployment
./create-test-cluster.sh
```

## 📊 Cost Management

### 💰 Zero-Cost Testing Strategy

This project is designed for **zero-cost when not in use**:

| Status | Cost | Resources |
|--------|------|-----------|
| **Testing** | ~$0.41/hour | AKS + ACR + Storage |
| **Stopped** | **€0.00/hour** | Everything deleted |
| **Weekend** | **€0.00** | True zero cost |

### 🎯 Cost Optimization Features

- **Spot Instances**: Up to 80% cost reduction
- **Automated Cleanup**: No forgotten resources
- **Temporary Resource Groups**: Everything created and deleted together
- **Pay-per-use**: Only pay when actively testing

## 🔍 Monitoring & Troubleshooting

### 📊 Check Application Status
```bash
# Check cluster status
kubectl get nodes -o wide

# Check pod status
kubectl get pods -n quote-app -o wide

# Check service endpoints
kubectl get service quote-azure-k8-backend-service -n quote-app

# View pod logs
kubectl logs -n quote-app $(kubectl get pods -n quote-app -o jsonpath='{.items[0].metadata.name}')
```

### 🐛 Common Issues

#### Image Pull Errors
```bash
# Check ACR authentication
az acr login --name $ACR_NAME

# Verify image exists
az acr repository list --name $ACR_NAME --output table
```

#### Pod Not Starting
```bash
# Check pod events
kubectl describe pod -n quote-app <pod-name>

# Check resource constraints
kubectl top nodes
kubectl top pods -n quote-app
```

### Common Tasks

#### Login to ACR and build/push image
```bash
az acr login --name kabulterquotek8acr
cd quote-azure-k8-backend
docker buildx build --platform linux/amd64 -t kabulterquotek8acr.azurecr.io/quote-azure-k8-backend:latest . --push
```

#### Check and restart Pods
```bash
# Check pod status
kubectl get pods -n quote-app

# If pod is stuck in ImagePullBackOff, delete it
kubectl delete pod -n quote-app $(kubectl get pods -n quote-app -o jsonpath='{.items[0].metadata.name}')

# Wait for new pod to start
kubectl get pods -n quote-app --watch
```

#### Test the Application
```bash
# Once pod is Running, test the service
curl http://20.76.216.245/api/quotes/random
```

## 🔐 Security Notes

**⚠️ IMPORTANT**: This is experimental code for learning purposes.

- **Secrets Management**: All secrets are externalized to `.env` files (never committed)
- **Production Use**: NOT recommended for production without security review
- **Default Values**: Change all default secrets and configurations before production use
- **Network Security**: Configure proper network security groups and firewalls for production

## 🤝 Contributing

This is a learning project. Contributions are welcome for:

- 🐛 Bug fixes and improvements
- 📚 Documentation enhancements
- 🔧 Automation improvements
- 🧪 Testing strategies

## 📄 License

This project is for educational purposes. Feel free to use and modify for learning.

## 🎓 Learning Path

1. **Start Here**: Read this README and set up local development
2. **Local Testing**: Try Docker Compose and local Kubernetes
3. **Azure Deployment**: Use the automated scripts for AKS deployment
4. **Cost Management**: Practice cleanup and cost optimization
5. **Advanced Topics**: Explore GitOps, Terraform, and CI/CD integration

---

**🎯 Remember**: This is experimental code designed for learning Kubernetes and Azure deployment patterns. Always test thoroughly and consider security implications before using in production environments.
