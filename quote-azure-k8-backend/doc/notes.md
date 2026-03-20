## Goals
step 1:
- [ ] Migrate from Azure Function App to Azure Container Web App
- [ ] Enable Docker Desktop testing with Azurite for local storage emulation
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

