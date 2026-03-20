# Azure Container Web App Migration Guide

## Overview

This document outlines the migration approach for converting the existing Azure Function App to an Azure Container Web App that maintains identical functionality while enabling Docker Desktop testing with Azurite for local storage emulation.

## Current Azure Function App Analysis

### Architecture
- **Framework**: .NET 8 Azure Functions v4
- **Storage**: Azure Table Storage
- **Authentication**: JWT-based authentication
- **Architecture**: Service-oriented with dependency injection

### Key Components
1. **Handlers**: Function triggers for different endpoints
   - `QuoteHandler.cs` - Quote-related endpoints
   - `AuthHandler.cs` - Authentication endpoints
   - `AdminHandler.cs` - Admin management endpoints
   - `UserManagementHandler.cs` - User management endpoints

2. **Services**: Business logic layer
   - `QuoteService.cs` - Quote management
   - `UserService.cs` - User management
   - `JwtService.cs` - JWT token handling
   - `AdminService.cs` - Admin operations
   - `ZenQuotesService.cs` - External API integration

3. **Data Layer**: Table Storage repositories
   - `IQuoteRepository`, `IUserRepository`, `IUserActivityRepository`, `IUserRoleRepository`

4. **Models**: Data transfer objects
   - Quote, User, UserProgress, UserLike, UserView, UserRole
   - Auth models (RegisterRequest, LoginRequest, etc.)

## Required Endpoints (from test-api.http)

### Authentication Endpoints
- `POST /auth/register` - User registration
- `POST /auth/login` - User login
- `POST /auth/change-password` - Password change
- `DELETE /auth/unregister` - Account deletion

### Quote Endpoints
- `GET /quote` - Get random quote (authenticated)
- `GET /quotes/random` - Get random quote (unauthenticated)
- `POST /quote` - Get quote with exclusions
- `GET /quote/viewed` - Get view history
- `GET /quote/progress` - Get user progress
- `GET /quote/liked` - Get liked quotes
- `POST /quote/{id}/like` - Like quote
- `DELETE /quote/{id}/unlike` - Unlike quote
- `PUT /quote/{id}/reorder` - Reorder liked quotes

### Admin Endpoints
- `GET /manage/users` - Get all users
- `GET /manage/quotes` - Get quotes with pagination
- `POST /manage/quotes/fetch` - Fetch quotes from external API
- `GET /manage/stats` - Get statistics
- `PUT /manage/users/role` - Update user role
- `DELETE /manage/users/role` - Remove user role
- `DELETE /manage/users/account` - Delete user account

### Utility Endpoints
- `POST /seed-users` - Seed initial admin users

## Migration Strategy

### Phase 1: Project Setup
1. **Create ASP.NET Core Web API Project**
   - Target .NET 8
   - Use minimal APIs or traditional controllers
   - Configure for container deployment

2. **Docker Configuration**
   - Create `Dockerfile` for multi-stage build
   - Create `docker-compose.yml` for local development
   - Include Azurite container for local Table Storage

### Phase 2: Core Migration
1. **Controllers Setup**
   - Convert Function Handlers to API Controllers
   - Maintain same route structure
   - Preserve HTTP methods and response formats

2. **Dependency Injection**
   - Reuse existing service registrations
   - Configure Table Storage client for Azurite
   - Set up JWT authentication middleware

3. **Configuration Management**
   - Convert `local.settings.json` to `appsettings.json`
   - Support environment-specific configurations
   - Configure Azurite connection strings for local development

### Phase 3: Storage Integration
1. **Azurite Setup**
   - Configure Azurite for Table Storage emulation
   - Update connection strings for local development
   - Ensure table creation and seeding work correctly

2. **Data Layer Adaptation**
   - Verify Table Storage operations work with Azurite
   - Test all repository operations
   - Validate entity serialization/deserialization

### Phase 4: Testing & Validation
1. **API Testing**
   - Use existing `test-api.http` file
   - Update base URL for local container
   - Verify all endpoints work identically

2. **Integration Testing**
   - Test authentication flow
   - Verify quote management functionality
   - Validate admin operations

## Technical Implementation Details

### Docker Configuration
```dockerfile
# Multi-stage build for optimized container
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/QuoteAzureBackend.Web/QuoteAzureBackend.Web.csproj", "src/QuoteAzureBackend.Web/"]
RUN dotnet restore "src/QuoteAzureBackend.Web/QuoteAzureBackend.Web.csproj"
COPY . .
WORKDIR "/src/src/QuoteAzureBackend.Web"
RUN dotnet build "QuoteAzureBackend.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QuoteAzureBackend.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QuoteAzureBackend.Web.dll"]
```

### Docker Compose for Local Development
```yaml
version: '3.8'
services:
  azurite:
    image: mcr.microsoft.com/azure-storage/azurite
    ports:
      - "10001:10001"  # Blob service
      - "10002:10002"  # Queue service  
      - "10000:10000"  # Table service
    volumes:
      - azurite-data:/data
    
  quote-web-app:
    build: .
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - TableStorageConnectionString=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://azurite:10000/devstoreaccount1;
    depends_on:
      - azurite

volumes:
  azurite-data:
```

### Configuration Updates
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "TableStorageConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://localhost:10000/devstoreaccount1;",
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "QuoteAzureBackend",
    "Audience": "QuoteAzureUsers",
    "ExpiryMinutes": 60
  }
}
```

### Controller Structure Example
```csharp
[ApiController]
[Route("api")]
public class QuoteController : ControllerBase
{
    private readonly IQuoteService _quoteService;
    private readonly JwtAuthenticationMiddleware _authMiddleware;

    public QuoteController(IQuoteService quoteService, JwtAuthenticationMiddleware authMiddleware)
    {
        _quoteService = quoteService;
        _authMiddleware = authMiddleware;
    }

    [HttpGet("quotes/random")]
    public async Task<ActionResult<Quote>> GetRandomQuote()
    {
        var quote = await _quoteService.GetQuoteAsync(null, new HashSet<int>());
        if (quote == null)
            return NotFound();
        
        return Ok(quote);
    }

    [HttpGet("quote")]
    [Authorize]
    public async Task<ActionResult<Quote>> GetRandomQuoteAuthenticated()
    {
        var userId = await _authMiddleware.GetUserIdFromTokenAsync(Request);
        var quote = await _quoteService.GetQuoteAsync(userId, new HashSet<int>());
        
        if (quote == null)
            return NotFound();
        
        return Ok(quote);
    }
}
```

## Migration Benefits

1. **Container Orchestration**: Enable Kubernetes deployment
2. **Local Development**: Full local testing with Azurite
3. **Scalability**: Better horizontal scaling capabilities
4. **Portability**: Container-based deployment flexibility
5. **Monitoring**: Enhanced observability options

## Next Steps

1. Create the ASP.NET Core Web API project structure
2. Set up Docker configuration
3. Migrate handlers to controllers
4. Configure Azurite integration
5. Test with existing API test suite
6. Deploy to Azure Container Instances/App Service

## Testing Strategy

1. **Unit Tests**: Reuse existing service logic tests
2. **Integration Tests**: Test API endpoints with Azurite
3. **End-to-End Tests**: Use `test-api.http` for validation
4. **Container Tests**: Verify Docker functionality

This migration ensures zero functional changes while modernizing the deployment architecture for better scalability and development experience.
