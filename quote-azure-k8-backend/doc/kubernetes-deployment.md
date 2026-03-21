# Kubernetes Deployment Guide

This guide explains how to deploy the Quote Azure K8 Backend application to Kubernetes using Docker Desktop's built-in Kubernetes cluster.

## Prerequisites

1. **Docker Desktop with Kubernetes enabled**
   - Install Docker Desktop
   - Go to Settings → Kubernetes
   - Enable Kubernetes
   - Wait for the cluster to start

2. **kubectl installed**
   - Docker Desktop includes kubectl
   - Verify with: `kubectl version`

3. **Application Docker image built**
   - Make sure you have built the Docker image:
   ```bash
   docker build -t quote-azure-k8-backend .
   ```

## Prerequisites: Start Azurite in Docker

**Important**: Before deploying to Kubernetes, you must start Azurite in Docker for local storage emulation.

```bash
# Go to project root (where compose.yaml is located)
cd ..

# Start only Azurite
docker-compose up -d azurite

# Verify Azurite is running
docker ps | grep azurite

# Check Azurite logs
docker-compose logs -f azurite
```

Azurite will be accessible at `http://host.docker.internal:10002` for Table Storage.

## Kubernetes Deployment Files

Create a new directory for your Kubernetes manifests:

```bash
mkdir -p k8s-deployment
cd k8s-deployment
```

Place all the following YAML files in the `k8s-deployment/` directory:

- `namespace.yaml`
- `configmap.yaml` 
- `secret.yaml`
- `deployment.yaml`
- `service.yaml`
- `hpa.yaml` (optional)

### 1. Namespace

Create a namespace for the application:

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: quote-app
```

### 2. ConfigMap

Configure application settings:

```yaml
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: quote-app-config
  namespace: quote-app
data:
  ASPNETCORE_ENVIRONMENT: "Development"
  TableStorageConnectionString: "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://host.docker.internal:10002/devstoreaccount1;"
  Logging__Console__Enabled: "true"
  Logging__Console__LogLevel__Default: "Debug"
```

### 3. Secret

Store sensitive configuration:

```yaml
# secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: quote-app-secret
  namespace: quote-app
type: Opaque
data:
  JwtSecret: "eXVyX3N1cGVyX3NlY3JldF9rZXlfd2hpY2hfaXNfdmVyeV9sb25nX2FuZF9zZWN1cmU=" # base64 encoded
```

*Note: The above is "ur_super_secret_key_which_is_very_long_and_secure" base64 encoded. Generate your own with:*
```bash
echo -n "your-secret-key" | base64
```

### 4. Deployment

Deploy the application:

```yaml
# deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: quote-app-deployment
  namespace: quote-app
  labels:
    app: quote-app
spec:
  replicas: 2
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
        image: quote-azure-k8-backend:latest
        imagePullPolicy: IfNotPresent
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
              name: quote-app-secret
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
            memory: "128Mi"
            cpu: "100m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
```

### 5. Service

Expose the application:

```yaml
# service.yaml
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


## Deployment Steps

### 1. Start Azurite (Prerequisite)

```bash
# Make sure Azurite is running in Docker
docker-compose up -d azurite

# Verify it's running
docker ps | grep azurite
```

### 2. Create the namespace

```bash
kubectl apply -f namespace.yaml
```

### 3. Deploy configuration

```bash
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml
```

### 4. Deploy the application

```bash
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml
```

### 5. Verify deployment

Check the pods:
```bash
kubectl get pods -n quote-app
```

Check the services:
```bash
kubectl get services -n quote-app
```

Check logs:
```bash
kubectl logs -f deployment/quote-app-deployment -n quote-app
```

## Access the Application

### Option 1: Port Forward

```bash
kubectl port-forward service/quote-app-service 8080:80 -n quote-app
```

Then access: `http://localhost:8080`

### Option 2: LoadBalancer (Docker Desktop)

If using Docker Desktop's Kubernetes, the LoadBalancer service should automatically get an external IP:

```bash
kubectl get service quote-app-service -n quote-app
```

Look for the `EXTERNAL-IP` column and access that IP.

## Testing the Deployment

### Health Check

```bash
curl http://localhost:8080/health
```

### Public Quote Endpoint

```bash
curl http://localhost:8080/api/quotes/random
```

### Admin Endpoints (with authentication)

1. First login to get a token:
```bash
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "Admin123!"}'
```

2. Use the token to access admin endpoints:
```bash
curl -X GET http://localhost:8080/api/manage/users \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

## Troubleshooting

### Common Issues

1. **Pods not starting**
   ```bash
   kubectl describe pod <pod-name> -n quote-app
   ```

2. **Service not accessible**
   ```bash
   kubectl get endpoints -n quote-app
   ```

3. **Azurite connectivity issues**
   ```bash
   # Check Azurite is running in Docker
   docker ps | grep azurite
   
   # Check Azurite logs
   docker-compose logs azurite
   
   # Test connectivity from Kubernetes pod
   kubectl run test-pod --rm -i --tty --image=busybox -n quote-app -- sh -c "nc -zv host.docker.internal 10002"
   ```

4. **Application logs**
   ```bash
   kubectl logs -f deployment/quote-app-deployment -n quote-app
   ```

### Reset the Cluster

If you need to start over:

```bash
# Stop Azurite
docker-compose down

# Delete Kubernetes namespace
kubectl delete namespace quote-app

# Re-deploy (start Azurite first, then Kubernetes)
docker-compose up -d azurite
kubectl apply -f namespace.yaml
# ... then follow the deployment steps again
```

## Scaling

### Scale the Application

```bash
# Scale to 3 replicas
kubectl scale deployment quote-app-deployment --replicas=3 -n quote-app

# Verify scaling
kubectl get pods -n quote-app
```

### Auto-scaling (Optional)

Create a Horizontal Pod Autoscaler:

```yaml
# hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: quote-app-hpa
  namespace: quote-app
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: quote-app-deployment
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

Apply with:
```bash
kubectl apply -f hpa.yaml
```

## Next Steps

1. **Monitor the deployment** using Kubernetes dashboard or monitoring tools
2. **Set up logging** with centralized logging solutions
3. **Configure persistent storage** for production workloads
4. **Set up ingress** for more sophisticated routing
5. **Deploy to Azure Kubernetes Service (AKS)** for production

## Clean Up

To remove all resources:

```bash
kubectl delete namespace quote-app
```

This will delete all resources in the quote-app namespace.
