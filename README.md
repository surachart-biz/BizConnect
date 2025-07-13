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

### 2. Database-First Migration Workflow

BizConnect follows a **Database-First** approach. When you add or modify SQL migration files in `/db/migrations/`, use these scripts to update your local database and Entity Framework models:

#### Script Options

**🚀 Cross-Platform Launcher (Recommended):**
```bash
./scripts/update-db
```
*Automatically detects your environment and calls the appropriate script.*

**Windows PowerShell:**
```powershell
.\scripts\update-db.ps1
```

**macOS/Linux/WSL/Git Bash:**
```bash
bash ./scripts/update-db.sh
```

#### Prerequisites

| Tool | Windows | macOS | Linux | Notes |
|------|---------|-------|-------|-------|
| **PowerShell** | 5.1+ or Core 6+ | Core 6+ | Core 6+ | For `.ps1` script |
| **Bash + jq** | Git Bash + `choco install jq` | Built-in + `brew install jq` | Built-in + `apt install jq` | For `.sh` script |
| **.NET 8 SDK** | Required | Required | Required | All platforms |
| **PostgreSQL Client** | `psql` in PATH | `psql` in PATH | `psql` in PATH | All platforms |
| **dotnet-ef tool** | Auto-installed | Auto-installed | Auto-installed | All platforms |

#### What These Scripts Do

1. **Validate Prerequisites** - Check for required tools and configuration
2. **Read Connection String** - From `appsettings.Local.json`
3. **Execute SQL Migrations** - Run all `.sql` files in alphabetical order
4. **Scaffold EF Models** - Re-generate Entity Framework Core models
5. **Validate Build** - Ensure the solution compiles successfully

#### Installation Help

If you get errors about missing tools:

**jq (for Bash script):**
```bash
# Windows (Chocolatey)
choco install jq

# Windows (Scoop)
scoop install jq

# macOS (Homebrew)
brew install jq

# Ubuntu/Debian
sudo apt-get install jq

# RHEL/CentOS
sudo yum install jq
```

**PostgreSQL Client (psql):**

The migration scripts require the PostgreSQL client (`psql`) to execute SQL files. Install it using your platform's package manager:

```bash
# Windows (Chocolatey)
choco install postgresql

# Windows (Scoop)
scoop install postgresql

# macOS (Homebrew)
brew install postgresql

# macOS (MacPorts)
sudo port install postgresql16

# Ubuntu/Debian
sudo apt-get install postgresql-client

# RHEL/CentOS
sudo yum install postgresql

# Fedora
sudo dnf install postgresql

# Arch Linux
sudo pacman -S postgresql
```

**Manual Installation:**
- Windows: Download from [postgresql.org](https://www.postgresql.org/download/windows/)
- Other platforms: [postgresql.org/download](https://www.postgresql.org/download/)

**Custom Installation Path:**

If PostgreSQL is installed in a non-standard location, set the `PG_BIN` environment variable:

```bash
# Bash/Zsh
export PG_BIN="/usr/local/pgsql/bin"
export PG_BIN="/c/Program Files/PostgreSQL/16/bin"  # Git Bash on Windows

# PowerShell
$env:PG_BIN = "C:\Program Files\PostgreSQL\16\bin"

# Command Prompt
set PG_BIN=C:\Program Files\PostgreSQL\16\bin
```

#### Dry Run Mode

Test what the PowerShell script would do without making changes:
```powershell
.\scripts\update-db.ps1 -WhatIf
```

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
