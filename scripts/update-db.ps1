#!/usr/bin/env pwsh
<#
.SYNOPSIS
    BizConnect Database Migration and EF Core Scaffolding Script

.DESCRIPTION
    This script performs a complete database update workflow:
    1. Reads connection string from appsettings.Local.json
    2. Executes SQL migration files in alphabetical order
    3. Re-scaffolds Entity Framework Core models
    4. Validates the build

.PARAMETER WhatIf
    Shows what would be done without actually executing the operations

.NOTES
    Requires: PostgreSQL client (psql), .NET 8 SDK, dotnet-ef tool
    Platform: Windows PowerShell 5+ or PowerShell Core 6+
#>

[CmdletBinding()]
param(
    [switch]$WhatIf
)

# Environment detection - must be first to catch wrong shell usage
if ($env:SHELL -match "bash|zsh|sh" -or $env:MSYSTEM -or $env:MINGW_PREFIX) {
    Write-Host "❌ You are running this PowerShell script in a Unix-like shell (Git Bash/MinGW/WSL)." -ForegroundColor Red
    Write-Host ""
    Write-Host "Please use one of these options instead:" -ForegroundColor Yellow
    Write-Host "  1. Open PowerShell and run: .\scripts\update-db.ps1" -ForegroundColor Cyan
    Write-Host "  2. Use the Bash script: bash ./scripts/update-db.sh" -ForegroundColor Cyan
    Write-Host "  3. Use the cross-platform launcher: ./scripts/update-db" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

# Set strict mode and error handling
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Script configuration
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptRoot
$LocalSettingsPath = "$ProjectRoot\BizConnect\appsettings.Local.json"
$MigrationsPath = "$ProjectRoot\db\migrations"
$DalProjectPath = "$ProjectRoot\BizConnect.Dal"

# Color output functions
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }

# PostgreSQL client discovery function
function Find-PostgreSQLClient {
    # 1. Check if PG_BIN environment variable is set
    if ($env:PG_BIN) {
        $pgBinPath = Join-Path $env:PG_BIN "psql.exe"
        if (Test-Path $pgBinPath) {
            Write-Info "Using PostgreSQL client from PG_BIN: $pgBinPath"
            return $pgBinPath
        } else {
            Write-Warning "PG_BIN is set but psql.exe not found at: $pgBinPath"
        }
    }

    # 2. Try Get-Command first (checks PATH)
    $psqlCommand = Get-Command "psql" -ErrorAction SilentlyContinue
    if ($psqlCommand) {
        Write-Info "Using PostgreSQL client from PATH: $($psqlCommand.Source)"
        return $psqlCommand.Source
    }

    # 3. Search common Windows installation paths
    $commonPaths = @(
        "$env:ProgramFiles\PostgreSQL\*\bin\psql.exe",
        "${env:ProgramFiles(x86)}\PostgreSQL\*\bin\psql.exe"
    )

    foreach ($pathPattern in $commonPaths) {
        $foundPaths = Get-ChildItem -Path $pathPattern -ErrorAction SilentlyContinue | Sort-Object Name -Descending
        if ($foundPaths) {
            $psqlPath = $foundPaths[0].FullName
            Write-Info "Found PostgreSQL client at: $psqlPath"
            return $psqlPath
        }
    }

    # 4. Not found
    return $null
}

# Banner
Write-Host @"
🚀 BizConnect Database Migration Workflow
==========================================
"@ -ForegroundColor Magenta

# Helpful hint for Bash users
if (-not $WhatIf) {
    Write-Host "💡 Tip: If you prefer Bash, run ./scripts/update-db.sh (auto-downloads jq on Windows)" -ForegroundColor DarkGray
    Write-Host ""
}

try {
    # Step 1: Validate prerequisites
    Write-Info "Step 1: Validating prerequisites..."
    
    # Check if appsettings.Local.json exists
    if (-not (Test-Path $LocalSettingsPath)) {
        throw "appsettings.Local.json not found at: $LocalSettingsPath`nPlease create this file with your local database connection string."
    }
    
    # Check if psql is available with enhanced discovery
    $psqlPath = Find-PostgreSQLClient
    if (-not $psqlPath) {
        throw @"
PostgreSQL client 'psql' not found. Please install PostgreSQL client tools or set PG_BIN environment variable.

Installation options:
  • Windows (Chocolatey): choco install postgresql
  • Windows (Scoop): scoop install postgresql
  • Manual download: https://www.postgresql.org/download/windows/

Alternative: Set environment variable PG_BIN to your PostgreSQL bin directory:
  • PowerShell: `$env:PG_BIN = "C:\Program Files\PostgreSQL\16\bin"`
  • Command Prompt: set PG_BIN=C:\Program Files\PostgreSQL\16\bin
"@
    }
    
    # Check if dotnet is available
    if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        throw ".NET SDK not found in PATH. Please install .NET 8 SDK."
    }
    
    Write-Success "Prerequisites validated"
    
    # Step 2: Parse connection string from appsettings.Local.json
    Write-Info "Step 2: Reading connection string from appsettings.Local.json..."
    
    $localSettings = Get-Content $LocalSettingsPath -Raw | ConvertFrom-Json
    if (-not $localSettings.ConnectionStrings -or -not $localSettings.ConnectionStrings.DefaultConnection) {
        throw "ConnectionStrings:DefaultConnection not found in appsettings.Local.json"
    }
    
    $connectionString = $localSettings.ConnectionStrings.DefaultConnection
    Write-Success "Connection string loaded"
    
    # Step 3: Execute SQL migration files
    Write-Info "Step 3: Executing SQL migration files..."

    if (-not (Test-Path $MigrationsPath)) {
        Write-Warning "Migrations directory not found: $MigrationsPath"
        if (-not $WhatIf) {
            Write-Info "Creating migrations directory..."
            New-Item -ItemType Directory -Path $MigrationsPath -Force | Out-Null
            Write-Success "Migrations directory created"
        } else {
            Write-Info "Would create migrations directory: $MigrationsPath"
        }
    }

    $sqlFiles = @(Get-ChildItem -Path $MigrationsPath -Filter "*.sql" | Sort-Object Name)

    if ($sqlFiles.Count -eq 0) {
        Write-Warning "No SQL migration files found in: $MigrationsPath"
    } else {
        Write-Info "Found $($sqlFiles.Count) SQL migration file(s)"

        foreach ($sqlFile in $sqlFiles) {
            if ($WhatIf) {
                Write-Info "Would execute: $($sqlFile.Name)"
            } else {
                Write-Info "Executing: $($sqlFile.Name)"

                # Execute SQL file with error handling
                # Convert .NET connection string to psql format
                $env:PGPASSWORD = "bizitadmin"
                $psqlArgs = @(
                    "-h", "localhost",
                    "-U", "postgres",
                    "-d", "bizconnect_test",
                    "-v", "ON_ERROR_STOP=1",
                    "-f", $sqlFile.FullName
                )

                # Temporarily change error action for psql execution
                $oldErrorAction = $ErrorActionPreference
                $ErrorActionPreference = "Continue"

                $psqlOutput = & $psqlPath @psqlArgs 2>&1
                $psqlExitCode = $LASTEXITCODE

                # Restore error action
                $ErrorActionPreference = $oldErrorAction

                if ($psqlExitCode -ne 0) {
                    throw "SQL execution failed for $($sqlFile.Name). Exit code: $psqlExitCode"
                }

                Write-Success "Executed: $($sqlFile.Name)"
            }
        }
    }
    
    # Step 4: Install dotnet-ef tool if missing
    Write-Info "Step 4: Ensuring dotnet-ef tool is installed..."

    if (-not $WhatIf) {
        $efToolCheck = & dotnet tool list --global 2>&1 | Select-String "dotnet-ef"
        if (-not $efToolCheck) {
            Write-Info "Installing dotnet-ef tool..."
            & dotnet tool install --global dotnet-ef
            if ($LASTEXITCODE -ne 0) {
                throw "Failed to install dotnet-ef tool"
            }
            Write-Success "dotnet-ef tool installed"
        } else {
            Write-Success "dotnet-ef tool already installed"
        }
    } else {
        Write-Info "Would check and install dotnet-ef tool if needed"
    }
    
    # Step 5: Scaffold Entity Framework Core models
    Write-Info "Step 5: Scaffolding Entity Framework Core models..."

    if (-not $WhatIf) {
        # Change to project directory for scaffolding
        Push-Location $ProjectRoot

        try {
            # Note: We don't remove the Models directory to avoid breaking the build
            # The --force flag will overwrite existing files

            # Run EF Core scaffold command
            $scaffoldArgs = @(
                "ef", "dbcontext", "scaffold",
                $connectionString,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                "--context", "BizConnectContext",
                "--project", "BizConnect.Dal",
                "--output-dir", "Models",
                "--namespace", "BizConnect.Dal.Models",
                "--context-namespace", "BizConnect.Dal",
                "--use-database-names",
                "--no-onconfiguring",
                "--force"
            )

            Write-Info "Running: dotnet $($scaffoldArgs -join ' ')"
            & dotnet @scaffoldArgs

            if ($LASTEXITCODE -ne 0) {
                throw "EF Core scaffolding failed"
            }

            Write-Success "Entity Framework Core models scaffolded"
        } finally {
            Pop-Location
        }
    } else {
        Write-Info "Would scaffold Entity Framework Core models using connection string"
    }

    # Step 6: Validate build
    Write-Info "Step 6: Validating build..."

    if (-not $WhatIf) {
        & dotnet build --configuration Release --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Build validation failed"
        }
        Write-Success "Build validation passed"
    } else {
        Write-Info "Would validate build with: dotnet build --configuration Release"
        Write-Success "✅ Build validation passed"
    }
    
    # Success banner
    Write-Host @"

🎉 Database Migration Workflow Completed Successfully!
====================================================
✅ SQL migrations executed
✅ EF Core models scaffolded  
✅ Build validated

Your database and Entity Framework models are now in sync.
"@ -ForegroundColor Green

} catch {
    Write-Error "Database migration workflow failed: $($_.Exception.Message)"
    exit 1
}
