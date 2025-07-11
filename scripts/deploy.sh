#!/bin/bash

# BizConnect Deployment Script for Ubuntu 24.04
# This script sets up and deploys the BizConnect application

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
APP_NAME="bizconnect"
APP_DIR="/opt/bizconnect"
BACKUP_DIR="/opt/bizconnect/backups"
NGINX_DIR="/opt/bizconnect/nginx"
SSL_DIR="/opt/bizconnect/ssl"

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

# Check if running as root
check_root() {
    if [[ $EUID -eq 0 ]]; then
        log_error "This script should not be run as root for security reasons"
        exit 1
    fi
}

# Install Docker and Docker Compose
install_docker() {
    log_info "Installing Docker and Docker Compose..."
    
    # Update package index
    sudo apt-get update
    
    # Install prerequisites
    sudo apt-get install -y \
        ca-certificates \
        curl \
        gnupg \
        lsb-release
    
    # Add Docker's official GPG key
    sudo mkdir -p /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    
    # Set up the repository
    echo \
        "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
        $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
    
    # Install Docker Engine
    sudo apt-get update
    sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin
    
    # Add current user to docker group
    sudo usermod -aG docker $USER
    
    log_success "Docker installed successfully"
}

# Setup application directories
setup_directories() {
    log_info "Setting up application directories..."
    
    sudo mkdir -p $APP_DIR
    sudo mkdir -p $BACKUP_DIR
    sudo mkdir -p $NGINX_DIR/conf.d
    sudo mkdir -p $SSL_DIR
    
    # Set ownership
    sudo chown -R $USER:$USER $APP_DIR
    
    log_success "Directories created successfully"
}

# Create Nginx configuration
create_nginx_config() {
    log_info "Creating Nginx configuration..."
    
    cat > $NGINX_DIR/nginx.conf << 'EOF'
user nginx;
worker_processes auto;
error_log /var/log/nginx/error.log warn;
pid /var/run/nginx.pid;

events {
    worker_connections 1024;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;
    
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';
    
    access_log /var/log/nginx/access.log main;
    
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;
    
    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_min_length 1024;
    gzip_types text/plain text/css text/xml text/javascript application/javascript application/xml+rss application/json;
    
    # Security headers
    add_header X-Frame-Options DENY;
    add_header X-Content-Type-Options nosniff;
    add_header X-XSS-Protection "1; mode=block";
    
    include /etc/nginx/conf.d/*.conf;
}
EOF

    cat > $NGINX_DIR/conf.d/bizconnect.conf << 'EOF'
upstream bizconnect_app {
    server bizconnect-app:8080;
}

server {
    listen 80;
    server_name _;
    
    # Redirect HTTP to HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name _;
    
    # SSL Configuration
    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/private.key;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-RSA-AES256-GCM-SHA512:DHE-RSA-AES256-GCM-SHA512:ECDHE-RSA-AES256-GCM-SHA384:DHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;
    
    # Security headers
    add_header Strict-Transport-Security "max-age=63072000" always;
    
    # Health check endpoint
    location /health {
        proxy_pass http://bizconnect_app/health;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
    
    # Main application
    location / {
        proxy_pass http://bizconnect_app;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # Buffer settings
        proxy_buffering on;
        proxy_buffer_size 128k;
        proxy_buffers 4 256k;
        proxy_busy_buffers_size 256k;
    }
    
    # Static files
    location ~* \.(css|js|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        proxy_pass http://bizconnect_app;
        proxy_set_header Host $host;
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
EOF

    log_success "Nginx configuration created"
}

# Generate self-signed SSL certificate (for testing)
generate_ssl_cert() {
    log_info "Generating self-signed SSL certificate..."
    
    if [[ ! -f $SSL_DIR/cert.pem ]]; then
        sudo openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
            -keyout $SSL_DIR/private.key \
            -out $SSL_DIR/cert.pem \
            -subj "/C=TH/ST=Bangkok/L=Bangkok/O=BizConnect/CN=localhost"
        
        sudo chown $USER:$USER $SSL_DIR/*
        log_success "SSL certificate generated"
    else
        log_info "SSL certificate already exists"
    fi
}

# Create systemd service for auto-start
create_systemd_service() {
    log_info "Creating systemd service..."
    
    sudo tee /etc/systemd/system/bizconnect.service > /dev/null << EOF
[Unit]
Description=BizConnect Application
Requires=docker.service
After=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
WorkingDirectory=$APP_DIR
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
EOF

    sudo systemctl daemon-reload
    sudo systemctl enable bizconnect.service
    
    log_success "Systemd service created and enabled"
}

# Main deployment function
deploy() {
    log_info "Starting BizConnect deployment on Ubuntu 24.04..."
    
    check_root
    install_docker
    setup_directories
    create_nginx_config
    generate_ssl_cert
    create_systemd_service
    
    log_success "Deployment setup completed!"
    log_info "Next steps:"
    echo "1. Copy your docker-compose.yml to $APP_DIR"
    echo "2. Create .env file with your configuration"
    echo "3. Run: sudo systemctl start bizconnect"
    echo "4. Check status: sudo systemctl status bizconnect"
}

# Script execution
case "${1:-deploy}" in
    "deploy")
        deploy
        ;;
    "start")
        log_info "Starting BizConnect services..."
        cd $APP_DIR && docker compose up -d
        log_success "Services started"
        ;;
    "stop")
        log_info "Stopping BizConnect services..."
        cd $APP_DIR && docker compose down
        log_success "Services stopped"
        ;;
    "restart")
        log_info "Restarting BizConnect services..."
        cd $APP_DIR && docker compose restart
        log_success "Services restarted"
        ;;
    "status")
        cd $APP_DIR && docker compose ps
        ;;
    "logs")
        cd $APP_DIR && docker compose logs -f
        ;;
    *)
        echo "Usage: $0 {deploy|start|stop|restart|status|logs}"
        exit 1
        ;;
esac
