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

### 1. Prerequisites Verification

Before starting, ensure you have all required tools installed:

| Tool | Version | Installation |
|------|---------|-------------|
| **.NET 8 SDK** | 8.0+ | [Download](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **PostgreSQL** | 16+ | [Download](https://www.postgresql.org/download/) |
| **psql** (PostgreSQL Client) | 16+ | Included with PostgreSQL |
| **Git** | Latest | [Download](https://git-scm.com/downloads) |
| **Visual Studio 2022** or **VS Code** | Latest | [VS](https://visualstudio.microsoft.com/) / [VSCode](https://code.visualstudio.com/) |

**Quick Verification:**
```bash
dotnet --version          # Should show 8.0.x
psql --version           # Should show PostgreSQL 16.x
git --version            # Should show git version
```

### 2. Configuration Setup

#### Step 2.1: Create Local Configuration

BizConnect requires two databases: one for application data and one for Hangfire background jobs.

Create `appsettings.Local.json` in the `BizConnect/` directory:

```bash
# Copy the example file (if it exists)
cp BizConnect/appsettings.Local.json.example BizConnect/appsettings.Local.json

# Or create manually
```

**Required Configuration** (`appsettings.Local.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=bizconnect_local;Username=postgres;Password=your_password",
    "HangfireConnection": "Host=localhost;Database=bizconnect_hangfire;Username=postgres;Password=your_password"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Hangfire": "Warning"
    }
  }
}
```

**⚠️ Important Notes:**
- Replace `your_password` with your actual PostgreSQL password
- Both databases must exist before running the application
- The file `appsettings.Local.json` is gitignored for security

#### Step 2.2: Create PostgreSQL Databases

Connect to PostgreSQL and create the required databases:

```bash
# Connect to PostgreSQL as superuser
psql -U postgres -h localhost

# Create databases
CREATE DATABASE bizconnect_local;
CREATE DATABASE bizconnect_hangfire;

# Verify databases were created
\l

# Exit PostgreSQL
\q
```

**Alternative Connection Methods:**
```bash
# If you have a different PostgreSQL setup
psql -h localhost -p 5432 -U your_username -d postgres

# For local PostgreSQL with different port
psql -h localhost -p 5433 -U postgres
```

### 3. Database-First Migration Workflow

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

### 4. Application Startup

#### Step 4.1: Install Dependencies

```bash
# Navigate to project root
cd BizConnect

# Restore NuGet packages
dotnet restore

# Build the solution to verify everything is set up correctly
dotnet build
```

#### Step 4.2: Run the Application

```bash
# Run the application
dotnet run --project BizConnect

# Or run with specific environment
dotnet run --project BizConnect --environment Development
```

**Expected Startup Output:**
```
info: Program[0]
      Starting BizConnect application configuration validation...
info: Program[0]
      Environment: Development
info: Program[0]
      ✅ Configuration validation successful:
info: Program[0]
         • Default Database: bizconnect_local
info: Program[0]
         • Hangfire Database: bizconnect_hangfire
info: Program[0]
         • Environment: Development
info: Program[0]
      ✅ Application built successfully
info: Program[0]
      🚀 Configuring middleware pipeline...
info: Program[0]
      🎉 BizConnect application startup completed successfully!
info: Program[0]
         • Health check available at: /health
info: Program[0]
         • API documentation available at: /api/docs
info: Program[0]
         • Hangfire dashboard available at: /hangfire
```

#### Step 4.3: Verify Application is Running

Open your browser and navigate to:
- **Application:** http://localhost:5000 or https://localhost:5001
- **Health Check:** http://localhost:5000/health
- **API Documentation:** http://localhost:5000/api/docs (Development only)
- **Hangfire Dashboard:** http://localhost:5000/hangfire (Development only)

## Troubleshooting

### Common Startup Issues

#### 🔴 Configuration Error: DefaultConnection not configured

**Error Message:**
```
❌ CONFIGURATION ERROR: DefaultConnection string is not configured.
```

**Solution:**
1. Ensure `appsettings.Local.json` exists in `BizConnect/` directory
2. Verify the connection string format is correct
3. Check database exists: `psql -U postgres -l`

#### 🔴 Database Connection Failed

**Error Message:**
```
Npgsql.NpgsqlException: FATAL: database "bizconnect_local" does not exist
```

**Solution:**
```bash
# Connect to PostgreSQL and create missing database
psql -U postgres
CREATE DATABASE bizconnect_local;
CREATE DATABASE bizconnect_hangfire;
\q
```

#### 🔴 Authentication Failed for User

**Error Message:**
```
Npgsql.NpgsqlException: FATAL: password authentication failed for user "postgres"
```

**Solution:**
1. Verify PostgreSQL password in `appsettings.Local.json`
2. Reset PostgreSQL password if needed:
   ```bash
   # On Windows (as Administrator)
   psql -U postgres
   ALTER USER postgres PASSWORD 'new_password';
   ```

#### 🔴 Port Already in Use

**Error Message:**
```
System.IO.IOException: Failed to bind to address https://127.0.0.1:5001: address already in use.
```

**Solution:**
```bash
# Check what's using the port
netstat -ano | findstr :5001  # Windows
lsof -i :5001                 # macOS/Linux

# Kill the process or use different port
dotnet run --project BizConnect --urls "http://localhost:5002;https://localhost:5003"
```

#### 🔴 Migration Scripts Fail

**Error Message:**
```
The system cannot find the path specified: psql
```

**Solution:**
1. Ensure PostgreSQL client tools are installed
2. Add PostgreSQL bin directory to PATH:
   - Windows: `C:\Program Files\PostgreSQL\16\bin`
   - macOS: `/opt/homebrew/bin` (Homebrew) or `/usr/local/bin`
   - Linux: Usually in PATH by default

### Configuration Testing

Before starting the application, test your configuration:

```bash
# Test all configuration and dependencies
.\scripts\test-configuration.ps1

# Test configuration without database connections (dry run)
.\scripts\test-configuration.ps1 -WhatIf
```

**What this script tests:**
- ✅ PowerShell version compatibility
- ✅ .NET 8 SDK installation
- ✅ PostgreSQL client (psql) availability  
- ✅ Project structure and required files
- ✅ Configuration files and connection strings
- ✅ Database connectivity (DefaultConnection & HangfireConnection)
- ✅ Solution build verification

**Expected output when everything is configured correctly:**
```
✅ All configuration tests passed! ✨
ℹ️  Your BizConnect setup is ready. You can now run:
ℹ️    dotnet run --project BizConnect
```

### 5. Running Tests

```bash
# Run all tests
dotnet test

# Run specific test categories
dotnet test --filter "FullyQualifiedName~KbankOdd"
dotnet test --filter "FullyQualifiedName~KBankController"
dotnet test --filter "FullyQualifiedName~UserService"

# Run tests with detailed output
dotnet test --verbosity normal
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
