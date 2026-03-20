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
Microsoft Azure Storage Explorer is a free, standalone app from Microsoft that allows you to easily work with Azure Storage data on your local machine. It provides a user-friendly interface for managing Azure Storage resources, including blobs, queues, tables, and files.

You can view the Storage Tables from the Azurite emulator by: 
- clicking the connector symbol and choosing "Local storage emulator".
- to connect to the Azurerite Table Storage, use the default Tables port 10002

