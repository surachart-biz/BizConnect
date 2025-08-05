#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Tests BizConnect configuration before startup to identify potential issues.

.DESCRIPTION
    This script validates the BizConnect application configuration including:
    - Connection strings
    - Database connectivity
    - Required files and dependencies
    - Environment setup

.PARAMETER WhatIf
    Shows what the script would test without actually performing database connections.

.EXAMPLE
    .\scripts\test-configuration.ps1
    Tests the configuration with actual database connections.

.EXAMPLE
    .\scripts\test-configuration.ps1 -WhatIf
    Shows what would be tested without making database connections.
#>

param(
    [switch]$WhatIf
)

# Script metadata
$scriptName = "BizConnect Configuration Test"
$scriptVersion = "1.0.0"
$requiredPowerShellVersion = "5.1"

# Colors for output
$colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
    Header = "Magenta"
}

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White",
        [switch]$NoNewline
    )
    
    if ($NoNewline) {
        Write-Host $Message -ForegroundColor $Color -NoNewline
    } else {
        Write-Host $Message -ForegroundColor $Color
    }
}

function Write-Header {
    param([string]$Title)
    
    Write-Host "`n" -NoNewline
    Write-ColorOutput "=" * 80 -Color $colors.Header
    Write-ColorOutput "  $Title" -Color $colors.Header
    Write-ColorOutput "=" * 80 -Color $colors.Header
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✅ $Message" -Color $colors.Success
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠️  $Message" -Color $colors.Warning
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "❌ $Message" -Color $colors.Error
}

function Write-Info {
    param([string]$Message)
    Write-ColorOutput "ℹ️  $Message" -Color $colors.Info
}

function Test-Prerequisite {
    param(
        [string]$Name,
        [string]$Command,
        [string]$ExpectedPattern,
        [string]$InstallationNote
    )
    
    Write-ColorOutput "Testing $Name... " -NoNewline
    
    try {
        $result = Invoke-Expression $Command 2>$null
        if ($result -match $ExpectedPattern) {
            Write-ColorOutput "✅ Found" -Color $colors.Success
            return $true
        } else {
            Write-ColorOutput "❌ Not found or wrong version" -Color $colors.Error
            Write-Warning "Installation: $InstallationNote"
            return $false
        }
    }
    catch {
        Write-ColorOutput "❌ Not found" -Color $colors.Error
        Write-Warning "Installation: $InstallationNote"
        return $false
    }
}

function Test-FileExists {
    param(
        [string]$FilePath,
        [string]$Description
    )
    
    Write-ColorOutput "Checking $Description... " -NoNewline
    
    if (Test-Path $FilePath) {
        Write-ColorOutput "✅ Found" -Color $colors.Success
        return $true
    } else {
        Write-ColorOutput "❌ Missing" -Color $colors.Error
        return $false
    }
}

function Test-DatabaseConnection {
    param(
        [string]$ConnectionString,
        [string]$DatabaseName
    )
    
    if ($WhatIf) {
        Write-Info "Would test connection to: $DatabaseName"
        return $true
    }
    
    Write-ColorOutput "Testing connection to $DatabaseName... " -NoNewline
    
    try {
        # Parse connection string
        $connParams = @{}
        $ConnectionString.Split(';') | ForEach-Object {
            if ($_ -match '(.+)=(.+)') {
                $connParams[$matches[1].Trim()] = $matches[2].Trim()
            }
        }
        
        $host = $connParams['Host'] ?? 'localhost'
        $database = $connParams['Database'] ?? ''
        $username = $connParams['Username'] ?? 'postgres'
        $password = $connParams['Password'] ?? ''
        
        # Test connection using psql
        $env:PGPASSWORD = $password
        $testResult = & psql -h $host -U $username -d $database -c "SELECT 1;" -t -A 2>$null
        
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "✅ Connected" -Color $colors.Success
            return $true
        } else {
            Write-ColorOutput "❌ Failed" -Color $colors.Error
            return $false
        }
    }
    catch {
        Write-ColorOutput "❌ Error: $($_.Exception.Message)" -Color $colors.Error
        return $false
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Get-ConfigurationValue {
    param(
        [string]$ConfigPath,
        [string]$Key
    )
    
    try {
        $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $keyParts = $Key.Split(':')
        $value = $config
        
        foreach ($part in $keyParts) {
            $value = $value.$part
        }
        
        return $value
    }
    catch {
        return $null
    }
}

# Main execution
Write-Header "$scriptName v$scriptVersion"

if ($WhatIf) {
    Write-Warning "Running in WhatIf mode - no actual database connections will be made"
}

$allTestsPassed = $true
$projectRoot = Split-Path -Parent $PSScriptRoot

# Test 1: PowerShell Version
Write-Header "PowerShell Environment"
$psVersion = $PSVersionTable.PSVersion
Write-Info "PowerShell Version: $psVersion"

if ($psVersion -ge [version]$requiredPowerShellVersion) {
    Write-Success "PowerShell version is compatible"
} else {
    Write-Error "PowerShell version $requiredPowerShellVersion or higher is required"
    $allTestsPassed = $false
}

# Test 2: Prerequisites
Write-Header "Prerequisites Check"

$prerequisites = @(
    @{
        Name = ".NET SDK"
        Command = "dotnet --version"
        Pattern = "8\."
        Installation = "Download from https://dotnet.microsoft.com/download/dotnet/8.0"
    },
    @{
        Name = "PostgreSQL Client (psql)"
        Command = "psql --version"
        Pattern = "psql \(PostgreSQL\)"
        Installation = "Install PostgreSQL from https://www.postgresql.org/download/"
    }
)

foreach ($prereq in $prerequisites) {
    $testResult = Test-Prerequisite -Name $prereq.Name -Command $prereq.Command -ExpectedPattern $prereq.Pattern -InstallationNote $prereq.Installation
    if (-not $testResult) {
        $allTestsPassed = $false
    }
}

# Test 3: Project Structure
Write-Header "Project Structure"

$requiredFiles = @(
    @{
        Path = "$projectRoot\BizConnect\BizConnect.csproj"
        Description = "Main project file"
    },
    @{
        Path = "$projectRoot\BizConnect.sln"
        Description = "Solution file"
    },
    @{
        Path = "$projectRoot\db\migrations"
        Description = "Migrations directory"
    }
)

foreach ($file in $requiredFiles) {
    $testResult = Test-FileExists -FilePath $file.Path -Description $file.Description
    if (-not $testResult) {
        $allTestsPassed = $false
    }
}

# Test 4: Configuration Files
Write-Header "Configuration Files"

$configFiles = @(
    "$projectRoot\BizConnect\appsettings.json",
    "$projectRoot\BizConnect\appsettings.Development.json",
    "$projectRoot\BizConnect\appsettings.Local.json"
)

$hasValidConfig = $false

foreach ($configFile in $configFiles) {
    $fileName = Split-Path $configFile -Leaf
    if (Test-Path $configFile) {
        Write-Success "Found $fileName"
        
        # Check for connection strings
        $defaultConn = Get-ConfigurationValue -ConfigPath $configFile -Key "ConnectionStrings:DefaultConnection"
        $hangfireConn = Get-ConfigurationValue -ConfigPath $configFile -Key "ConnectionStrings:HangfireConnection"
        
        if ($defaultConn) {
            Write-Success "  DefaultConnection found"
        }
        if ($hangfireConn) {
            Write-Success "  HangfireConnection found"
        }
        
        if ($defaultConn -and $hangfireConn) {
            $hasValidConfig = $true
        }
    } else {
        Write-Warning "Missing $fileName"
    }
}

if (-not $hasValidConfig) {
    Write-Error "No configuration file found with both DefaultConnection and HangfireConnection"
    Write-Info "Create appsettings.Local.json with your database connections"
    $allTestsPassed = $false
}

# Test 5: Database Connectivity
Write-Header "Database Connectivity"

$localConfigPath = "$projectRoot\BizConnect\appsettings.Local.json"

if (Test-Path $localConfigPath) {
    $defaultConn = Get-ConfigurationValue -ConfigPath $localConfigPath -Key "ConnectionStrings:DefaultConnection"
    $hangfireConn = Get-ConfigurationValue -ConfigPath $localConfigPath -Key "ConnectionStrings:HangfireConnection"
    
    if ($defaultConn) {
        $testResult = Test-DatabaseConnection -ConnectionString $defaultConn -DatabaseName "Default Database"
        if (-not $testResult) {
            $allTestsPassed = $false
        }
    }
    
    if ($hangfireConn) {
        $testResult = Test-DatabaseConnection -ConnectionString $hangfireConn -DatabaseName "Hangfire Database"  
        if (-not $testResult) {
            $allTestsPassed = $false
        }
    }
} else {
    Write-Warning "Skipping database tests - appsettings.Local.json not found"
}

# Test 6: Build Test
Write-Header "Build Test"

Write-ColorOutput "Testing solution build... " -NoNewline

try {
    $buildOutput = & dotnet build "$projectRoot\BizConnect.sln" --verbosity quiet 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "✅ Build successful" -Color $colors.Success
    } else {
        Write-ColorOutput "❌ Build failed" -Color $colors.Error
        Write-Error "Build output: $buildOutput"
        $allTestsPassed = $false
    }
}
catch {
    Write-ColorOutput "❌ Build error: $($_.Exception.Message)" -Color $colors.Error
    $allTestsPassed = $false
}

# Summary
Write-Header "Test Results Summary"

if ($allTestsPassed) {
    Write-Success "All configuration tests passed! ✨"
    Write-Info "Your BizConnect setup is ready. You can now run:"
    Write-Info "  dotnet run --project BizConnect"
} else {
    Write-Error "Some configuration tests failed."
    Write-Info "Please fix the issues above before running the application."
    Write-Info "For detailed setup instructions, see README.md"
}

Write-Header "Additional Resources"
Write-Info "📚 README.md - Complete setup guide"
Write-Info "📖 CLAUDE.md - Development instructions" 
Write-Info "🔧 ./scripts/update-db - Database migration script"
Write-Info "🏥 http://localhost:5000/health - Health check endpoint (when running)"

# Return appropriate exit code
if ($allTestsPassed) {
    exit 0
} else {
    exit 1
}