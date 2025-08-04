# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

BizConnect is an ASP.NET Core 8 MVC application with PostgreSQL database integration for managing business connections and KBank Online Direct Debit (ODD) services.

## Architecture

The solution follows a three-tier architecture:

- **BizConnect/** - Presentation layer (MVC + Razor Pages)
  - Controllers handle HTTP requests
  - Views use Razor templates
  - Areas for admin functionality
  - ViewModels for data transfer

- **BizConnect.Services/** - Business logic layer
  - Service interfaces and implementations
  - HTTP clients for external integrations
  - Utility classes and constants

- **BizConnect.Dal/** - Data access layer
  - Entity Framework Core with PostgreSQL
  - Database models and context

- **BizConnect.Tests/** - Unit and integration tests
  - xUnit test framework
  - Moq for mocking
  - In-memory database for testing

## Essential Commands

### Build and Run
```bash
# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run the main application
dotnet run --project BizConnect

# Run with specific environment
dotnet run --project BizConnect --environment Development
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~KbankOdd"
dotnet test --filter "FullyQualifiedName~KBankController"
dotnet test --filter "FullyQualifiedName~UserService"
```

### Database Management

The project uses a **Database-First** approach. When adding or modifying SQL migrations:

```bash
# Cross-platform script launcher (automatically detects OS)
./scripts/update-db

# Windows PowerShell
.\scripts\update-db.ps1

# macOS/Linux/WSL/Git Bash
bash ./scripts/update-db.sh
```

These scripts:
1. Execute SQL migrations from `/db/migrations/`
2. Re-scaffold Entity Framework models
3. Validate the build

### Local Configuration

Create `BizConnect/appsettings.Local.json` (gitignored) for local database:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=bizconnect_local;Username=postgres;Password=your_password"
  }
}
```

### Secrets Management

For sensitive configuration like KBank PassPhrase:
```bash
cd BizConnect
dotnet user-secrets init
dotnet user-secrets set "KBankODD:PassPhrase" "your-passphrase"
```

## Key Patterns and Conventions

### Dependency Injection
- Services registered in `Program.cs`
- Scoped lifetime for database-dependent services
- HttpClient configured per external service

### Authentication & Authorization
- Cookie-based authentication
- Role-based authorization (Admin, User roles)
- BCrypt password hashing
- No self-registration - admin creates users

### Database Migrations
- SQL files in `/db/migrations/` following pattern: `yyyyMMdd-##_Description.sql`
- Migrations must be idempotent (safe to run multiple times)
- Entity Framework models are scaffolded from database

### Configuration Hierarchy
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (environment-specific)
3. `appsettings.Local.json` (local override, gitignored)
4. Environment variables
5. User secrets (development)

### External Integrations

**KBank ODD Integration:**
- Client service pattern with interface
- Configuration-driven URLs per environment
- PassPhrase stored in secrets only (never in appsettings)
- SHA-256 hash validation for callbacks
- Comprehensive logging for troubleshooting

### Testing Strategy
- Unit tests for services and utilities
- Integration tests for authentication flows
- Mock external dependencies
- In-memory database for controller tests

### Security Considerations
- PassPhrases and secrets via user-secrets or environment variables only
- HTTPS enforcement in production
- HSTS configuration
- Input validation on all forms
- Anti-forgery tokens on POST requests

## Common Development Tasks

### Adding a New Service
1. Create interface in `BizConnect.Services/Interfaces/`
2. Implement service in `BizConnect.Services/`
3. Register in `Program.cs` dependency injection
4. Add unit tests in `BizConnect.Tests/Unit/Services/`

### Adding Database Migration
1. Create SQL file in `/db/migrations/` with proper naming
2. Run `./scripts/update-db` to apply and scaffold
3. Verify Entity Framework models are updated correctly

### Modifying Authentication
- Cookie options in `Program.cs`
- User service in `BizConnect.Services/UserService.cs`
- Account controller handles login/logout

## Environment-Specific Settings

- **Development**: Detailed logging, no caching, developer exception page
- **UAT**: Production-like with test endpoints
- **Production**: HTTPS enforcement, response caching, minimal logging

## Health Monitoring

Health check endpoint available at `/health` - includes database connectivity check.