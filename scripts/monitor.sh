#!/bin/bash

# BizConnect Monitoring Script
# Health checks and monitoring for the application

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
APP_DIR="/opt/bizconnect"
LOG_DIR="/opt/bizconnect/logs"
ALERT_EMAIL=${ALERT_EMAIL:-"admin@example.com"}

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

# Check container health
check_containers() {
    log_info "Checking container health..."
    
    cd $APP_DIR
    
    local containers=("bizconnect-app" "bizconnect-postgres" "bizconnect-nginx")
    local unhealthy_containers=()
    
    for container in "${containers[@]}"; do
        if docker ps --filter "name=$container" --filter "status=running" | grep -q $container; then
            local health=$(docker inspect --format='{{.State.Health.Status}}' $container 2>/dev/null || echo "no-health-check")
            
            if [ "$health" = "healthy" ] || [ "$health" = "no-health-check" ]; then
                log_success "$container is running and healthy"
            else
                log_error "$container is unhealthy (status: $health)"
                unhealthy_containers+=($container)
            fi
        else
            log_error "$container is not running"
            unhealthy_containers+=($container)
        fi
    done
    
    if [ ${#unhealthy_containers[@]} -gt 0 ]; then
        log_error "Unhealthy containers detected: ${unhealthy_containers[*]}"
        return 1
    fi
    
    return 0
}

# Check disk usage
check_disk_usage() {
    log_info "Checking disk usage..."
    
    local usage=$(df /opt | awk 'NR==2 {print $5}' | sed 's/%//')
    
    if [ $usage -gt 90 ]; then
        log_error "Disk usage is critical: ${usage}%"
        return 1
    elif [ $usage -gt 80 ]; then
        log_warning "Disk usage is high: ${usage}%"
        return 1
    else
        log_success "Disk usage is normal: ${usage}%"
        return 0
    fi
}

# Check memory usage
check_memory_usage() {
    log_info "Checking memory usage..."
    
    local mem_info=$(free | grep Mem)
    local total=$(echo $mem_info | awk '{print $2}')
    local used=$(echo $mem_info | awk '{print $3}')
    local usage=$((used * 100 / total))
    
    if [ $usage -gt 90 ]; then
        log_error "Memory usage is critical: ${usage}%"
        return 1
    elif [ $usage -gt 80 ]; then
        log_warning "Memory usage is high: ${usage}%"
        return 1
    else
        log_success "Memory usage is normal: ${usage}%"
        return 0
    fi
}

# Check application response
check_app_response() {
    log_info "Checking application response..."
    
    local response=$(curl -s -o /dev/null -w "%{http_code}" http://localhost/health || echo "000")
    
    if [ "$response" = "200" ]; then
        log_success "Application is responding correctly"
        return 0
    else
        log_error "Application is not responding (HTTP $response)"
        return 1
    fi
}

# Check database connectivity
check_database() {
    log_info "Checking database connectivity..."
    
    if docker exec bizconnect-postgres pg_isready -U bizconnect -d bizconnect_prod >/dev/null 2>&1; then
        log_success "Database is accessible"
        return 0
    else
        log_error "Database is not accessible"
        return 1
    fi
}

# Check log files for errors
check_logs() {
    log_info "Checking recent log files for errors..."
    
    local error_count=0
    
    # Check application logs
    if [ -d "$LOG_DIR" ]; then
        local recent_errors=$(find $LOG_DIR -name "*.log" -mtime -1 -exec grep -i "error\|exception\|fatal" {} \; 2>/dev/null | wc -l)
        if [ $recent_errors -gt 0 ]; then
            log_warning "Found $recent_errors error(s) in application logs (last 24 hours)"
            error_count=$((error_count + recent_errors))
        fi
    fi
    
    # Check Docker container logs
    local docker_errors=$(docker compose logs --since 24h 2>/dev/null | grep -i "error\|exception\|fatal" | wc -l)
    if [ $docker_errors -gt 0 ]; then
        log_warning "Found $docker_errors error(s) in Docker logs (last 24 hours)"
        error_count=$((error_count + docker_errors))
    fi
    
    if [ $error_count -eq 0 ]; then
        log_success "No recent errors found in logs"
        return 0
    else
        log_warning "Total errors found: $error_count"
        return 1
    fi
}

# Generate system report
generate_report() {
    log_info "Generating system report..."
    
    local report_file="/tmp/bizconnect_report_$(date +%Y%m%d_%H%M%S).txt"
    
    {
        echo "BizConnect System Report"
        echo "Generated: $(date)"
        echo "========================"
        echo ""
        
        echo "System Information:"
        echo "- OS: $(lsb_release -d | cut -f2)"
        echo "- Kernel: $(uname -r)"
        echo "- Uptime: $(uptime -p)"
        echo ""
        
        echo "Docker Information:"
        docker --version
        docker compose version
        echo ""
        
        echo "Container Status:"
        cd $APP_DIR && docker compose ps
        echo ""
        
        echo "Resource Usage:"
        echo "- CPU: $(top -bn1 | grep "Cpu(s)" | awk '{print $2}' | cut -d'%' -f1)% used"
        echo "- Memory: $(free -h | grep Mem | awk '{printf "%.1f%% used (%s/%s)\n", $3/$2*100, $3, $2}')"
        echo "- Disk: $(df -h /opt | awk 'NR==2 {printf "%s used (%s/%s)\n", $5, $3, $2}')"
        echo ""
        
        echo "Network Connectivity:"
        echo "- Application: $(curl -s -o /dev/null -w "HTTP %{http_code} - %{time_total}s" http://localhost/health || echo "Failed")"
        echo ""
        
        echo "Recent Log Summary:"
        echo "- Application errors (24h): $(find $LOG_DIR -name "*.log" -mtime -1 -exec grep -i "error" {} \; 2>/dev/null | wc -l)"
        echo "- Docker errors (24h): $(docker compose logs --since 24h 2>/dev/null | grep -i "error" | wc -l)"
        
    } > $report_file
    
    log_success "Report generated: $report_file"
    cat $report_file
}

# Send alert email
send_alert() {
    local subject="$1"
    local message="$2"
    
    if command -v mail >/dev/null 2>&1; then
        echo "$message" | mail -s "$subject" $ALERT_EMAIL
        log_info "Alert sent to $ALERT_EMAIL"
    else
        log_warning "Mail command not available, cannot send alert"
    fi
}

# Full health check
health_check() {
    log_info "Starting comprehensive health check..."
    
    local issues=0
    
    check_containers || ((issues++))
    check_disk_usage || ((issues++))
    check_memory_usage || ((issues++))
    check_app_response || ((issues++))
    check_database || ((issues++))
    check_logs || ((issues++))
    
    echo ""
    if [ $issues -eq 0 ]; then
        log_success "All health checks passed!"
        return 0
    else
        log_error "Health check failed with $issues issue(s)"
        
        # Send alert if configured
        if [ -n "$ALERT_EMAIL" ]; then
            send_alert "BizConnect Health Check Failed" "Health check failed with $issues issue(s). Please check the system."
        fi
        
        return 1
    fi
}

# Main script logic
case "${1:-health}" in
    "health")
        health_check
        ;;
    "containers")
        check_containers
        ;;
    "disk")
        check_disk_usage
        ;;
    "memory")
        check_memory_usage
        ;;
    "app")
        check_app_response
        ;;
    "database")
        check_database
        ;;
    "logs")
        check_logs
        ;;
    "report")
        generate_report
        ;;
    *)
        echo "Usage: $0 {health|containers|disk|memory|app|database|logs|report}"
        echo ""
        echo "Commands:"
        echo "  health      Run comprehensive health check"
        echo "  containers  Check container status"
        echo "  disk        Check disk usage"
        echo "  memory      Check memory usage"
        echo "  app         Check application response"
        echo "  database    Check database connectivity"
        echo "  logs        Check for recent errors in logs"
        echo "  report      Generate detailed system report"
        exit 1
        ;;
esac
