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


## Build for Kubernetes in Docker Desktop
The issue is that the Docker build is using cached layers, so the code changes aren't being included. Let's force a rebuild without cache:

```bash
docker build --no-cache -t quote-azure-k8-backend .
```

To avoid wrong line numbers in errors:
```bash
docker build --no-cache --build-arg BUILD_CONFIGURATION=Debug -t quote-azure-k8-backend .
```

## Test with Kubernetes in Docker Desktop
Docker Desktop's Kubernetes sometimes has issues with NodePort networking. This is a known limitation.

To be able to test the REST API, you need to start port forwarding:
```bash
kubectl port-forward service/quote-app-service 8080:80 -n quote-app &

Now you can use this curl to test the API:
```bash
curl http://localhost:8080/api/quotes/random
```

