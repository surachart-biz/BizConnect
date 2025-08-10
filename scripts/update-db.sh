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

# Environment detection - check if running in PowerShell (rare but possible)
if [[ -n "${PSVersionTable:-}" ]] || [[ -n "${PSHOME:-}" ]]; then
    echo -e "\033[31m❌ You are running this Bash script in PowerShell.\033[0m"
    echo ""
    echo -e "\033[33mPlease use one of these options instead:\033[0m"
    echo -e "\033[36m  1. Use PowerShell script: .\scripts\update-db.ps1\033[0m"
    echo -e "\033[36m  2. Use cross-platform launcher: ./scripts/update-db\033[0m"
    echo ""
    exit 1
fi

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

# PostgreSQL client discovery function
find_postgresql_client() {
    # 1. Check if PG_BIN environment variable is set
    if [[ -n "${PG_BIN:-}" ]]; then
        local pg_bin_path="$PG_BIN/psql"
        if [[ -x "$pg_bin_path" ]]; then
            print_info "Using PostgreSQL client from PG_BIN: $pg_bin_path" >&2
            echo "$pg_bin_path"
            return 0
        else
            print_warning "PG_BIN is set but psql not found or not executable at: $pg_bin_path"
        fi
    fi

    # 2. Try which/command -v first (checks PATH)
    local psql_in_path
    if psql_in_path=$(command -v psql 2>/dev/null); then
        # On Windows, command -v might return path without .exe extension
        # Try both with and without .exe
        local psql_candidates=("$psql_in_path" "${psql_in_path}.exe")
        for candidate in "${psql_candidates[@]}"; do
            if [[ -x "$candidate" ]]; then
                print_info "Using PostgreSQL client from PATH: $candidate" >&2
                echo "$candidate"
                return 0
            fi
        done
        print_warning "Found psql in PATH but it's not executable: $psql_in_path"
    fi

    # 3. Search common installation paths
    local common_paths=(
        "/usr/bin/psql"
        "/usr/local/bin/psql"
        "/usr/local/pgsql/bin/psql"
        "/opt/postgresql/bin/psql"
        "/Applications/Postgres.app/Contents/Versions/*/bin/psql"
    )

    # Add Windows paths for Git Bash/MSYS2/Cygwin
    if [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]] || uname -o 2>/dev/null | grep -qi mingw; then
        common_paths+=(
            "/c/Program Files/PostgreSQL/*/bin/psql.exe"
            "/c/Program Files (x86)/PostgreSQL/*/bin/psql.exe"
        )
    fi

    for path_pattern in "${common_paths[@]}"; do
        # Handle glob patterns - use compgen for proper expansion
        if [[ "$path_pattern" == *"*"* ]]; then
            # Use find for wildcard patterns
            local found_paths
            if [[ "$path_pattern" == "/c/Program Files"* ]]; then
                # Windows paths - search for psql.exe in PostgreSQL directories
                if [[ -d "/c/Program Files" ]]; then
                    found_paths=$(find "/c/Program Files" -name "psql.exe" -path "*/PostgreSQL/*/bin/psql.exe" 2>/dev/null | sort -V | tail -1)
                    if [[ -n "$found_paths" && -x "$found_paths" ]]; then
                        print_info "Found PostgreSQL client at: $found_paths" >&2
                        echo "$found_paths"
                        return 0
                    fi
                fi
                if [[ -d "/c/Program Files (x86)" ]]; then
                    found_paths=$(find "/c/Program Files (x86)" -name "psql.exe" -path "*/PostgreSQL/*/bin/psql.exe" 2>/dev/null | sort -V | tail -1)
                    if [[ -n "$found_paths" && -x "$found_paths" ]]; then
                        print_info "Found PostgreSQL client at: $found_paths" >&2
                        echo "$found_paths"
                        return 0
                    fi
                fi
            else
                # Unix paths with wildcards
                for psql_path in $path_pattern; do
                    if [[ -x "$psql_path" ]]; then
                        print_info "Found PostgreSQL client at: $psql_path" >&2
                        echo "$psql_path"
                        return 0
                    fi
                done
            fi
        else
            # Direct path without wildcards
            if [[ -x "$path_pattern" ]]; then
                print_info "Found PostgreSQL client at: $path_pattern" >&2
                echo "$path_pattern"
                return 0
            fi
        fi
    done

    # 4. Not found
    return 1
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

# Check if required tools are available with enhanced psql discovery
print_info "Discovering PostgreSQL client..."
PSQL_PATH=$(find_postgresql_client)
print_info "PostgreSQL client path resolved to: $PSQL_PATH"
if [[ -z "$PSQL_PATH" ]]; then
    print_error "PostgreSQL client 'psql' not found."
    echo ""
    echo -e "\033[33mInstallation instructions:\033[0m"

    # Detect platform and provide specific instructions
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        echo -e "\033[36m  macOS (Homebrew): brew install postgresql\033[0m"
        echo -e "\033[36m  macOS (MacPorts): sudo port install postgresql16\033[0m"
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        # Linux
        if command -v apt-get >/dev/null 2>&1; then
            echo -e "\033[36m  Ubuntu/Debian: sudo apt-get install postgresql-client\033[0m"
        fi
        if command -v yum >/dev/null 2>&1; then
            echo -e "\033[36m  RHEL/CentOS: sudo yum install postgresql\033[0m"
        fi
        if command -v dnf >/dev/null 2>&1; then
            echo -e "\033[36m  Fedora: sudo dnf install postgresql\033[0m"
        fi
        if command -v pacman >/dev/null 2>&1; then
            echo -e "\033[36m  Arch Linux: sudo pacman -S postgresql\033[0m"
        fi
    elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
        # Windows (Git Bash/MSYS2/Cygwin)
        echo -e "\033[36m  Windows (Chocolatey): choco install postgresql\033[0m"
        echo -e "\033[36m  Windows (Scoop): scoop install postgresql\033[0m"
        echo -e "\033[36m  Git Bash/MSYS2: pacman -S mingw-w64-x86_64-postgresql\033[0m"
    fi

    echo -e "\033[36m  Or download from: https://www.postgresql.org/download/\033[0m"
    echo ""
    echo -e "\033[33mAlternative: Set environment variable PG_BIN to your PostgreSQL bin directory:\033[0m"
    echo -e "\033[36m  export PG_BIN=\"/usr/local/pgsql/bin\"\033[0m"
    echo -e "\033[36m  export PG_BIN=\"/c/Program Files/PostgreSQL/16/bin\"\033[0m"
    echo ""
    exit 1
fi

command -v dotnet >/dev/null 2>&1 || error_exit ".NET SDK not found in PATH. Please install .NET 8 SDK."

# Enhanced jq check with auto-download for Windows and platform-specific instructions
ensure_jq_available() {
    # Check if jq is already available
    if command -v jq >/dev/null 2>&1; then
        return 0
    fi

    print_info "jq not found in PATH. Attempting to resolve..."

    # Detect platform and handle accordingly
    if [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]] || uname -o 2>/dev/null | grep -qi mingw; then
        # Windows (Git Bash/MSYS2/Cygwin) - auto-download portable jq
        print_info "Windows environment detected. Downloading portable jq..."

        local tools_dir="$SCRIPT_DIR/tools"
        local jq_path="$tools_dir/jq.exe"

        # Create tools directory if it doesn't exist
        if [[ ! -d "$tools_dir" ]]; then
            mkdir -p "$tools_dir" || error_exit "Failed to create tools directory: $tools_dir"
        fi

        # Download jq if not already present
        if [[ ! -f "$jq_path" ]]; then
            print_info "Downloading jq-win64.exe..."
            if command -v curl >/dev/null 2>&1; then
                curl -L "https://github.com/stedolan/jq/releases/download/jq-1.6/jq-win64.exe" -o "$jq_path" --silent --show-error
            elif command -v wget >/dev/null 2>&1; then
                wget -q "https://github.com/stedolan/jq/releases/download/jq-1.6/jq-win64.exe" -O "$jq_path"
            else
                error_exit "Neither curl nor wget found. Cannot download jq automatically."
            fi

            if [[ ! -f "$jq_path" ]]; then
                error_exit "Failed to download jq to: $jq_path"
            fi

            # Make executable
            chmod +x "$jq_path"
            print_success "Downloaded portable jq to scripts/tools/"
        else
            print_info "Using existing portable jq from scripts/tools/"
        fi

        # Add tools directory to PATH for this session
        export PATH="$tools_dir:$PATH"

        # Verify jq is now available
        if ! command -v jq >/dev/null 2>&1; then
            error_exit "jq still not available after download. Please check scripts/tools/jq.exe"
        fi

        return 0
    else
        # macOS/Linux - provide installation instructions and exit
        print_error "jq not found in PATH. Please install jq for JSON parsing."
        echo ""
        echo -e "\033[33mInstallation instructions:\033[0m"

        if [[ "$OSTYPE" == "darwin"* ]]; then
            # macOS
            echo -e "\033[36m  macOS (Homebrew): brew install jq\033[0m"
            echo -e "\033[36m  macOS (MacPorts): sudo port install jq\033[0m"
        elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
            # Linux
            if command -v apt-get >/dev/null 2>&1; then
                echo -e "\033[36m  Ubuntu/Debian: sudo apt-get install jq\033[0m"
            fi
            if command -v yum >/dev/null 2>&1; then
                echo -e "\033[36m  RHEL/CentOS: sudo yum install jq\033[0m"
            fi
            if command -v dnf >/dev/null 2>&1; then
                echo -e "\033[36m  Fedora: sudo dnf install jq\033[0m"
            fi
            if command -v pacman >/dev/null 2>&1; then
                echo -e "\033[36m  Arch Linux: sudo pacman -S jq\033[0m"
            fi
        fi

        echo -e "\033[36m  Or download from: https://stedolan.github.io/jq/download/\033[0m"
        echo ""
        echo -e "\033[33mAfter installation, re-run this script.\033[0m"
        exit 1
    fi
}

# Call the enhanced jq check
ensure_jq_available

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

# Define migrations in the correct execution order (updated 2025-08-10)
MIGRATION_ORDER=(
    "20250805-03_ConsolidatedSchema.sql"
    "20250805-04_EnhanceOtacStateConstraint.sql"
    "20250805-05_EMERGENCY_FixExternalReferenceForOTAC.sql"
    "20250805-06_AddMultiLanguageStatusColumns.sql"
    "20250805-07_EnhanceMultiLanguageViews.sql"
    "20250806-01_ModernUIPerformanceOptimization.sql"
)

# Get SQL files in the specified order, excluding master_migration.sql and README.md
SQL_FILES=()
for filename in "${MIGRATION_ORDER[@]}"; do
    filepath="$MIGRATIONS_PATH/$filename"
    if [[ -f "$filepath" ]]; then
        SQL_FILES+=("$filepath")
    fi
done

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
        print_info "Executing command: $PSQL_PATH $PSQL_CONNECTION -v ON_ERROR_STOP=1 -f $sql_file"
        if ! "$PSQL_PATH" "$PSQL_CONNECTION" -v ON_ERROR_STOP=1 -f "$sql_file"; then
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
