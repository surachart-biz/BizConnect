#!/bin/bash
#
# BizConnect Database Migration and EF Core Scaffolding Script
#
# This script performs a complete database update workflow:
# 1. Reads connection string from appsettings.Local.json
# 2. Executes SQL migration files in alphabetical order
# 3. Re-scaffolds Entity Framework Core models
# 4. Validates the build
#
# Requirements: PostgreSQL client (psql), .NET 8 SDK, dotnet-ef tool, jq
# Platform: macOS, Linux, WSL
#

# Strict error handling
set -euo pipefail

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
LOCAL_SETTINGS_PATH="$PROJECT_ROOT/BizConnect/appsettings.Local.json"
MIGRATIONS_PATH="$PROJECT_ROOT/db/migrations"
DAL_PROJECT_PATH="$PROJECT_ROOT/BizConnect.Dal"

# Color output functions
print_success() { echo -e "\033[32m✅ $1\033[0m"; }
print_info() { echo -e "\033[36mℹ️  $1\033[0m"; }
print_warning() { echo -e "\033[33m⚠️  $1\033[0m"; }
print_error() { echo -e "\033[31m❌ $1\033[0m"; }

# Error handler
error_exit() {
    print_error "Database migration workflow failed: $1"
    exit 1
}

# Banner
echo -e "\033[35m🚀 BizConnect Database Migration Workflow"
echo -e "==========================================\033[0m"

# Step 1: Validate prerequisites
print_info "Step 1: Validating prerequisites..."

# Check if appsettings.Local.json exists
if [[ ! -f "$LOCAL_SETTINGS_PATH" ]]; then
    error_exit "appsettings.Local.json not found at: $LOCAL_SETTINGS_PATH
Please create this file with your local database connection string."
fi

# Check if required tools are available
command -v psql >/dev/null 2>&1 || error_exit "PostgreSQL client 'psql' not found in PATH. Please install PostgreSQL client tools."
command -v dotnet >/dev/null 2>&1 || error_exit ".NET SDK not found in PATH. Please install .NET 8 SDK."
command -v jq >/dev/null 2>&1 || error_exit "jq not found in PATH. Please install jq for JSON parsing."

print_success "Prerequisites validated"

# Step 2: Parse connection string from appsettings.Local.json
print_info "Step 2: Reading connection string from appsettings.Local.json..."

CONNECTION_STRING=$(jq -r '.ConnectionStrings.DefaultConnection // empty' "$LOCAL_SETTINGS_PATH")
if [[ -z "$CONNECTION_STRING" || "$CONNECTION_STRING" == "null" ]]; then
    error_exit "ConnectionStrings:DefaultConnection not found in appsettings.Local.json"
fi

print_success "Connection string loaded"

# Step 3: Execute SQL migration files
print_info "Step 3: Executing SQL migration files..."

if [[ ! -d "$MIGRATIONS_PATH" ]]; then
    print_warning "Migrations directory not found: $MIGRATIONS_PATH"
    print_info "Creating migrations directory..."
    mkdir -p "$MIGRATIONS_PATH"
    print_success "Migrations directory created"
fi

# Find and sort SQL files
mapfile -t SQL_FILES < <(find "$MIGRATIONS_PATH" -name "*.sql" -type f | sort)

if [[ ${#SQL_FILES[@]} -eq 0 ]]; then
    print_warning "No SQL migration files found in: $MIGRATIONS_PATH"
else
    print_info "Found ${#SQL_FILES[@]} SQL migration file(s)"
    
    for sql_file in "${SQL_FILES[@]}"; do
        filename=$(basename "$sql_file")
        print_info "Executing: $filename"
        
        # Execute SQL file with error handling
        # Convert .NET connection string to psql format
        PSQL_CONNECTION=$(echo "$CONNECTION_STRING" | sed 's/Host=/host=/g; s/Database=/dbname=/g; s/Username=/user=/g; s/Password=/password=/g; s/;/ /g')
        if ! psql "$PSQL_CONNECTION" -v ON_ERROR_STOP=1 -f "$sql_file"; then
            error_exit "SQL execution failed for $filename"
        fi
        
        print_success "Executed: $filename"
    done
fi

# Step 4: Install dotnet-ef tool if missing
print_info "Step 4: Ensuring dotnet-ef tool is installed..."

if ! dotnet tool list --global | grep -q "dotnet-ef"; then
    print_info "Installing dotnet-ef tool..."
    if ! dotnet tool install --global dotnet-ef; then
        error_exit "Failed to install dotnet-ef tool"
    fi
    print_success "dotnet-ef tool installed"
else
    print_success "dotnet-ef tool already installed"
fi

# Step 5: Scaffold Entity Framework Core models
print_info "Step 5: Scaffolding Entity Framework Core models..."

# Change to project directory for scaffolding
cd "$PROJECT_ROOT"

# Remove existing Models directory if it exists
MODELS_PATH="$DAL_PROJECT_PATH/Models"
if [[ -d "$MODELS_PATH" ]]; then
    print_info "Removing existing Models directory..."
    rm -rf "$MODELS_PATH"
fi

# Run EF Core scaffold command
print_info "Running EF Core scaffold command..."
if ! dotnet ef dbcontext scaffold \
    "$CONNECTION_STRING" \
    "Npgsql.EntityFrameworkCore.PostgreSQL" \
    --context "BizConnectContext" \
    --project "BizConnect.Dal" \
    --output-dir "Models" \
    --namespace "BizConnect.Dal.Models" \
    --context-namespace "BizConnect.Dal" \
    --use-database-names \
    --no-onconfiguring \
    --force; then
    error_exit "EF Core scaffolding failed"
fi

print_success "Entity Framework Core models scaffolded"

# Step 6: Validate build
print_info "Step 6: Validating build..."

if ! dotnet build --configuration Release --verbosity minimal; then
    error_exit "Build validation failed"
fi

print_success "Build validation passed"

# Success banner
echo -e "\033[32m
🎉 Database Migration Workflow Completed Successfully!
====================================================
✅ SQL migrations executed
✅ EF Core models scaffolded  
✅ Build validated

Your database and Entity Framework models are now in sync.
\033[0m"
