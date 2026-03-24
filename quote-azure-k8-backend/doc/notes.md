## Goals
step 1:
- [X] Migrate from Azure Function App to Azure Container Web App
- [X] Enable Docker Desktop testing with Azurite for local storage emulation.

step 2:
- [ ] Run locally in Kubernetes function of Docker Desktop
- [ ] Write deployment.yaml and service.yaml for Kubernetes
- [ ] Try to run the application in kubernetes via kubectl apply

step 3:
- [ ] Use Terraform to deploy the application to Azure Container Apps


## Azurite
Azurite is a local Azure Storage emulator that provides a local environment for testing Azure Storage services. It can emulate:
- Blob Containers
- Queue Storage
- Table Storage

It is installed in this project as a docker container and can be started with the following command:
```bash
docker-compose up -d
```

## Microsoft Azure Storage Explorer
Microsoft Azure Storage Explorer is a free, standalone app from Microsoft that allows you to easily work with Azure Storage data on your local machine. It provides a user-friendly interface for managing Azure Storage resources, including blobs, queues and tables.

You can view the Storage Tables from the Azurite emulator by: 
- clicking the connector symbol and choosing "Local storage emulator".
- to connect to the Azurerite Table Storage, use the default Tables port 10002

## Test in Docker
```bash
# 1. Build de app
docker build --no-cache --build-arg BUILD_CONFIGURATION=Debug -t quote-azure-k8-backend:test .

# 2. Stop alles wat draait
docker-compose down

# 3. Start alles met docker-compose
docker-compose up -d

# 4. Check de logs van de backend
docker-compose logs -f quote-azure-k8-backend
```

## Kubernetes in Docker Desktop

### Azurite Compatibility
Azurite is not fully compatible with Kubernetes in Docker Desktop. It works fine with Docker Desktop, but not with Kubernetes. Because of this, Azurite is deployed in Docker Desktop and the backend is deployed in Kubernetes. This matches the production environment where instead of Azurite, Azure Storage Table is used which is also not in Kubernetes.

### Build for Kubernetes in Docker Desktop
The issue is that the Docker build is using cached layers, so the code changes aren't being included. Let's force a rebuild without cache:

```bash
docker build --no-cache -t quote-azure-k8-backend .
```

To avoid wrong line numbers in errors:
```bash
docker build --no-cache --build-arg BUILD_CONFIGURATION=Debug -t quote-azure-k8-backend .
```

### Test with Kubernetes in Docker Desktop
Docker Desktop's Kubernetes sometimes has issues with NodePort networking. This is a known limitation.

To be able to test the REST API, you need to start port forwarding:
```bash
kubectl port-forward service/quote-app-service 8080:80 -n quote-app &

Now you can use this curl to test the API:
```bash
curl http://localhost:8080/api/quotes/random
```

## Kubernetes in Azure

acr-name: kabulterquoteazurek8acr

```bash
# Navigate to project directory
cd quote-azure-k8-backend

# Build the production image
docker build -t quote-azure-k8-backend:latest .

# Tag for Azure Container Registry
docker tag quote-azure-k8-backend:latest kabulterquoteazurek8acr.azurecr.io/quote-azure-k8-backend:latest
```

```bash
# Create JWT secret for AKS (use a secure key in production)
kubectl create secret generic quote-app-secret-aks \
  --from-literal=JwtSecret=$(echo -n "your-production-jwt-secret-key" | base64) \
  --namespace=quote-app
```


## Kubernetes commands
kubectl config get-contexts
kubectl config use-context docker-desktop


kubectl get pods -n quote-app
kubectl get services -n quote-app
kubectl get deployments -n quote-app
kubectl get configmaps -n quote-app
kubectl get secrets -n quote-app

kubectl delete pod -n quote-app <pod-name>
kubectl delete deployment -n quote-app <deployment-name>
kubectl delete service -n quote-app <service-name>
kubectl delete configmap -n quote-app <configmap-name>
kubectl delete secret -n quote-app <secret-name>

kubectl logs -f deployment/quote-app-deployment -n quote-app
kubectl apply -f deployment.yaml -n quote-app
kubectl apply -f service.yaml -n quote-app
kubectl apply -f configmap.yaml -n quote-app
kubectl apply -f secret.yaml -n quote-app


