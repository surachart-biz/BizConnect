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
    
.NOTES
    Requires: PostgreSQL client (psql), .NET 8 SDK, dotnet-ef tool
    Platform: Windows PowerShell 5+ or PowerShell Core 6+
#>

[CmdletBinding()]
param()

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

# Banner
Write-Host @"
🚀 BizConnect Database Migration Workflow
==========================================
"@ -ForegroundColor Magenta

try {
    # Step 1: Validate prerequisites
    Write-Info "Step 1: Validating prerequisites..."
    
    # Check if appsettings.Local.json exists
    if (-not (Test-Path $LocalSettingsPath)) {
        throw "appsettings.Local.json not found at: $LocalSettingsPath`nPlease create this file with your local database connection string."
    }
    
    # Check if psql is available
    if (-not (Get-Command "psql" -ErrorAction SilentlyContinue)) {
        throw "PostgreSQL client 'psql' not found in PATH. Please install PostgreSQL client tools."
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
        Write-Info "Creating migrations directory..."
        New-Item -ItemType Directory -Path $MigrationsPath -Force | Out-Null
        Write-Success "Migrations directory created"
    }
    
    $sqlFiles = @(Get-ChildItem -Path $MigrationsPath -Filter "*.sql" | Sort-Object Name)

    if ($sqlFiles.Count -eq 0) {
        Write-Warning "No SQL migration files found in: $MigrationsPath"
    } else {
        Write-Info "Found $($sqlFiles.Count) SQL migration file(s)"
        
        foreach ($sqlFile in $sqlFiles) {
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

            $result = & psql @psqlArgs 2>&1
            $psqlExitCode = $LASTEXITCODE

            # Restore error action
            $ErrorActionPreference = $oldErrorAction

            if ($psqlExitCode -ne 0) {
                throw "SQL execution failed for $($sqlFile.Name). Exit code: $psqlExitCode"
            }
            
            Write-Success "Executed: $($sqlFile.Name)"
        }
    }
    
    # Step 4: Install dotnet-ef tool if missing
    Write-Info "Step 4: Ensuring dotnet-ef tool is installed..."
    
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
    
    # Step 5: Scaffold Entity Framework Core models
    Write-Info "Step 5: Scaffolding Entity Framework Core models..."
    
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
        
        # Step 6: Validate build
        Write-Info "Step 6: Validating build..."
        
        & dotnet build --configuration Release --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Build validation failed"
        }
        
        Write-Success "Build validation passed"
        
    } finally {
        Pop-Location
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
