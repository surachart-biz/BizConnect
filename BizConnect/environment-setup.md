# Environment Configuration Guide

## Overview
The BizConnect application now supports environment-specific configuration through appsettings files and environment variables.

## Environment Detection
The application automatically detects the environment using `ASPNETCORE_ENVIRONMENT` variable:
- `Development` - Development environment
- `Local` - Local development environment  
- `UAT` - User Acceptance Testing environment
- `Production` - Production environment

## Configuration Loading Order
1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific overrides)
3. Environment variables (highest priority)

## Setting Environment Variables

### Windows (PowerShell)
```powershell
# For UAT
$env:ASPNETCORE_ENVIRONMENT = "UAT"

# For Production
$env:ASPNETCORE_ENVIRONMENT = "Production"
```

### Linux/macOS (Bash)
```bash
# For UAT
export ASPNETCORE_ENVIRONMENT=UAT

# For Production
export ASPNETCORE_ENVIRONMENT=Production
```

### Docker
```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production
```

### Azure App Service
Set in Application Settings:
- Key: `ASPNETCORE_ENVIRONMENT`
- Value: `Production` or `UAT`

## Environment Variable Overrides
You can override any configuration using environment variables with double underscore notation:

```bash
# Override connection string
export ConnectionStrings__DefaultConnection="Host=prod-server;Database=bizconnect_prod;Username=user;Password=pass"

# Override security settings
export Security__RequireHttps=true
export Security__CookieSecure=true

# Override performance settings
export Performance__EnableResponseCaching=true
export Performance__EnableResponseCompression=true
```

## Running the Application

### Development
```bash
dotnet run --environment Development
```

### UAT
```bash
dotnet run --environment UAT
```

### Production
```bash
dotnet run --environment Production
```

## Security Notes
- Never commit sensitive passwords to source control
- Use environment variables or secure key vaults for production secrets
- Update placeholder passwords in appsettings files before deployment
