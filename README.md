# BizConnect

ASP.NET Core 8 MVC application with PostgreSQL database and Entity Framework Core.

## Architecture

BizConnect follows a three-tier architecture:

- **BizConnect/** - Presentation layer (MVC + Razor Pages)
- **BizConnect.Services/** - Business logic layer
- **BizConnect.Dal/** - Data access layer with Entity Framework Core
- **BizConnect.Tests/** - Unit and integration tests

## Prerequisites

- .NET 8 SDK
- PostgreSQL 16
- Visual Studio 2022 or VS Code

## Local Development Setup

### 1. Database Configuration

Create a local configuration file with your database connection:

```bash
# Copy the example file
cp BizConnect/appsettings.Local.json.example BizConnect/appsettings.Local.json

# Edit with your local database credentials
```

Example `appsettings.Local.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=bizconnect_local;Username=postgres;Password=your_password"
  }
}
```

### 2. Local DB Workflow

When you add or modify SQL migration files in `/db/migrations/`, use these single-command workflows to update your local database and Entity Framework models:

**Windows (PowerShell):**
```powershell
.\scripts\update-db.ps1
```

**macOS/Linux/WSL (Bash):**
```bash
bash ./scripts/update-db.sh
```

These scripts will:
1. Read your connection string from `appsettings.Local.json`
2. Execute all SQL migration files in alphabetical order
3. Re-scaffold Entity Framework Core models to match the database
4. Validate that the solution builds successfully

### 3. Running the Application

```bash
# Restore packages
dotnet restore

# Run the application
dotnet run --project BizConnect

# Or run tests
dotnet test
```

## Database Migrations

SQL migration files are stored in `/db/migrations/` and follow the naming convention:
```
yyyyMMdd-##_Description.sql
```

Examples:
- `20250710-01_InitialSchema.sql`
- `20250710-02_AddUserProfiles.sql`

See [db/migrations/README.md](db/migrations/README.md) for detailed migration guidelines.

## Authentication & Authorization

- **Cookie-based authentication** with username/password
- **Role-based authorization** (Admin, User)
- **BCrypt password hashing**
- **No self-registration** - admin creates users

### Default Credentials

- Username: `admin`
- Password: `admin123`

## Testing

Run the full test suite:
```bash
dotnet test
```

The project includes:
- Unit tests for services and business logic
- Integration tests for authentication and authorization
- Smoke tests for database scaffolding

## CI/CD & Deployment

The project uses GitLab CI/CD with Docker containerization for deployment to Ubuntu 24.04:

### Pipeline Stages
- **Build** - Compile the solution and restore packages
- **Test** - Run unit and integration tests with PostgreSQL service
- **Publish** - Build and push Docker images to GitLab Container Registry
- **Deploy** - Deploy to Production/UAT environments using Docker Compose

### Deployment Architecture
- **Application**: ASP.NET Core 8 MVC (containerized)
- **Database**: PostgreSQL 16 (containerized)
- **Web Server**: Nginx reverse proxy (containerized)
- **Target OS**: Ubuntu 24.04 LTS
- **Health Checks**: Built-in health endpoint at `/health`

### Quick Deployment
1. **Server Setup**: Run `bash scripts/deploy.sh deploy` on Ubuntu 24.04
2. **Configuration**: Copy `.env.example` to `.env` and configure
3. **Deploy**: Push to `main` (production) or `develop` (UAT) branch
4. **Manual Trigger**: Trigger deployment job in GitLab CI/CD

See [deployment-guide.md](deployment-guide.md) for detailed instructions.

## Contributing

1. Create feature branches from `develop`
2. Add/modify SQL migrations in `/db/migrations/`
3. Run `.\scripts\update-db.ps1` (Windows) or `bash ./scripts/update-db.sh` (Unix)
4. Ensure all tests pass: `dotnet test`
5. Create merge request to `develop`

## Security Notes

- Never commit `appsettings.Local.json` (already in .gitignore)
- Use strong passwords in production
- Review SQL migrations for security implications
- Keep dependencies updated
