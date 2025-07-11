#!/bin/bash

# BizConnect Database Backup Script
# Automated backup solution for PostgreSQL database

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
APP_DIR="/opt/bizconnect"
BACKUP_DIR="/opt/bizconnect/backups"
RETENTION_DAYS=${BACKUP_RETENTION_DAYS:-7}
DB_CONTAINER="bizconnect-postgres"
DB_NAME="bizconnect_prod"
DB_USER="bizconnect"

# Functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Create backup
create_backup() {
    local timestamp=$(date +%Y%m%d_%H%M%S)
    local backup_file="$BACKUP_DIR/bizconnect_backup_$timestamp.sql"
    local compressed_file="$backup_file.gz"
    
    log_info "Creating database backup..."
    
    # Check if database container is running
    if ! docker ps | grep -q $DB_CONTAINER; then
        log_error "Database container $DB_CONTAINER is not running"
        exit 1
    fi
    
    # Create backup directory if it doesn't exist
    mkdir -p $BACKUP_DIR
    
    # Create database dump
    docker exec $DB_CONTAINER pg_dump -U $DB_USER -d $DB_NAME > $backup_file
    
    # Compress backup
    gzip $backup_file
    
    # Set permissions
    chmod 600 $compressed_file
    
    log_success "Backup created: $compressed_file"
    
    # Get file size
    local file_size=$(du -h $compressed_file | cut -f1)
    log_info "Backup size: $file_size"
}

# Clean old backups
cleanup_old_backups() {
    log_info "Cleaning up backups older than $RETENTION_DAYS days..."
    
    local deleted_count=0
    
    # Find and delete old backup files
    while IFS= read -r -d '' file; do
        rm "$file"
        ((deleted_count++))
        log_info "Deleted old backup: $(basename "$file")"
    done < <(find $BACKUP_DIR -name "bizconnect_backup_*.sql.gz" -mtime +$RETENTION_DAYS -print0)
    
    if [ $deleted_count -eq 0 ]; then
        log_info "No old backups to clean up"
    else
        log_success "Cleaned up $deleted_count old backup(s)"
    fi
}

# List backups
list_backups() {
    log_info "Available backups:"
    
    if [ ! -d $BACKUP_DIR ]; then
        log_warning "Backup directory does not exist"
        return
    fi
    
    local backup_files=($(find $BACKUP_DIR -name "bizconnect_backup_*.sql.gz" -type f | sort -r))
    
    if [ ${#backup_files[@]} -eq 0 ]; then
        log_warning "No backups found"
        return
    fi
    
    printf "%-30s %-10s %-20s\n" "Backup File" "Size" "Date"
    printf "%-30s %-10s %-20s\n" "----------" "----" "----"
    
    for file in "${backup_files[@]}"; do
        local filename=$(basename "$file")
        local size=$(du -h "$file" | cut -f1)
        local date=$(stat -c %y "$file" | cut -d' ' -f1,2 | cut -d'.' -f1)
        printf "%-30s %-10s %-20s\n" "$filename" "$size" "$date"
    done
}

# Restore backup
restore_backup() {
    local backup_file="$1"
    
    if [ -z "$backup_file" ]; then
        log_error "Please specify backup file to restore"
        echo "Usage: $0 restore <backup_file>"
        list_backups
        exit 1
    fi
    
    local full_path="$BACKUP_DIR/$backup_file"
    
    if [ ! -f "$full_path" ]; then
        log_error "Backup file not found: $full_path"
        exit 1
    fi
    
    log_warning "This will overwrite the current database!"
    read -p "Are you sure you want to continue? (y/N): " -n 1 -r
    echo
    
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        log_info "Restore cancelled"
        exit 0
    fi
    
    log_info "Restoring backup: $backup_file"
    
    # Stop application to prevent database access
    log_info "Stopping application..."
    cd $APP_DIR && docker compose stop bizconnect-app
    
    # Drop and recreate database
    log_info "Recreating database..."
    docker exec $DB_CONTAINER psql -U $DB_USER -c "DROP DATABASE IF EXISTS $DB_NAME;"
    docker exec $DB_CONTAINER psql -U $DB_USER -c "CREATE DATABASE $DB_NAME;"
    
    # Restore from backup
    log_info "Restoring data..."
    if [[ $backup_file == *.gz ]]; then
        zcat "$full_path" | docker exec -i $DB_CONTAINER psql -U $DB_USER -d $DB_NAME
    else
        cat "$full_path" | docker exec -i $DB_CONTAINER psql -U $DB_USER -d $DB_NAME
    fi
    
    # Start application
    log_info "Starting application..."
    cd $APP_DIR && docker compose start bizconnect-app
    
    log_success "Database restored successfully"
}

# Test backup integrity
test_backup() {
    local backup_file="$1"
    
    if [ -z "$backup_file" ]; then
        log_error "Please specify backup file to test"
        echo "Usage: $0 test <backup_file>"
        list_backups
        exit 1
    fi
    
    local full_path="$BACKUP_DIR/$backup_file"
    
    if [ ! -f "$full_path" ]; then
        log_error "Backup file not found: $full_path"
        exit 1
    fi
    
    log_info "Testing backup integrity: $backup_file"
    
    # Test if file can be decompressed and contains valid SQL
    if [[ $backup_file == *.gz ]]; then
        if zcat "$full_path" | head -n 10 | grep -q "PostgreSQL database dump"; then
            log_success "Backup file appears to be valid"
        else
            log_error "Backup file appears to be corrupted"
            exit 1
        fi
    else
        if head -n 10 "$full_path" | grep -q "PostgreSQL database dump"; then
            log_success "Backup file appears to be valid"
        else
            log_error "Backup file appears to be corrupted"
            exit 1
        fi
    fi
}

# Main script logic
case "${1:-backup}" in
    "backup")
        create_backup
        cleanup_old_backups
        ;;
    "list")
        list_backups
        ;;
    "restore")
        restore_backup "$2"
        ;;
    "test")
        test_backup "$2"
        ;;
    "cleanup")
        cleanup_old_backups
        ;;
    *)
        echo "Usage: $0 {backup|list|restore|test|cleanup}"
        echo ""
        echo "Commands:"
        echo "  backup          Create a new database backup"
        echo "  list            List all available backups"
        echo "  restore <file>  Restore database from backup file"
        echo "  test <file>     Test backup file integrity"
        echo "  cleanup         Remove old backup files"
        exit 1
        ;;
esac
