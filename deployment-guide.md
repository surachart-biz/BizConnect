# BizConnect CI/CD Deployment Guide

## Overview

This guide explains how to deploy BizConnect ASP.NET Core 8 application to Ubuntu 24.04 using GitLab CI/CD with Docker containers.

## Architecture

- **Application**: ASP.NET Core 8 MVC
- **Database**: PostgreSQL 16
- **Web Server**: Nginx (reverse proxy)
- **Containerization**: Docker & Docker Compose
- **CI/CD**: GitLab CI/CD
- **Target OS**: Ubuntu 24.04 LTS

## Prerequisites

### Server Requirements

- Ubuntu 24.04 LTS server
- Minimum 2GB RAM, 20GB disk space
- Docker and Docker Compose installed
- SSH access configured
- Domain name (optional, for SSL)

### GitLab Configuration

1. **GitLab Variables** (Settings > CI/CD > Variables):
   ```
   SSH_PRIVATE_KEY          # SSH private key for server access
   PRODUCTION_SERVER        # Production server IP/hostname
   PRODUCTION_USER          # SSH username for production
   PRODUCTION_DOMAIN        # Production domain name
   UAT_SERVER              # UAT server IP/hostname
   UAT_USER                # SSH username for UAT
   UAT_DOMAIN              # UAT domain name
   CI_REGISTRY_USER        # GitLab registry username (auto-provided)
   CI_REGISTRY_PASSWORD    # GitLab registry password (auto-provided)
   ```

## Deployment Steps

### 1. Server Setup

Run the deployment script on your Ubuntu 24.04 server:

```bash
# Clone the repository
git clone <your-repo-url>
cd BizConnect

# Run deployment setup
sudo bash scripts/deploy.sh deploy
```

This script will:
- Install Docker and Docker Compose
- Create application directories
- Generate Nginx configuration
- Create SSL certificates (self-signed)
- Set up systemd service

### 2. Environment Configuration

Create environment file on the server:

```bash
# Copy example environment file
cp .env.example .env

# Edit with your configuration
nano .env
```

Required environment variables:
```env
POSTGRES_PASSWORD=your_secure_postgres_password
CI_REGISTRY_IMAGE=registry.gitlab.com/your-group/bizconnect
PRODUCTION_DOMAIN=your-domain.com
```

### 3. GitLab CI/CD Pipeline

The pipeline includes these stages:

#### Build Stage
- Restores NuGet packages
- Builds the solution
- Creates build artifacts

#### Test Stage
- Runs unit and integration tests
- Generates code coverage reports
- Performs security scans

#### Publish Stage
- Builds Docker image
- Pushes to GitLab Container Registry
- Creates deployment artifacts

#### Deploy Stage
- **Production**: Manual deployment to production server
- **UAT**: Manual deployment to UAT server
- **Rollback**: Manual rollback capability

### 4. Manual Deployment Trigger

1. Push code to `main` branch (for production) or `develop` branch (for UAT)
2. Wait for build, test, and publish stages to complete
3. Go to GitLab CI/CD > Pipelines
4. Click on the pipeline
5. Manually trigger the deployment job

## File Structure

```
BizConnect/
├── .gitlab-ci.yml              # CI/CD pipeline configuration
├── Dockerfile                  # Multi-stage Docker build
├── docker-compose.yml          # Production Docker Compose
├── docker-compose.uat.yml      # UAT Docker Compose
├── .env.example               # Environment variables template
├── scripts/
│   ├── deploy.sh              # Server setup and deployment
│   ├── backup.sh              # Database backup management
│   └── monitor.sh             # Health monitoring
└── deployment-guide.md        # This guide
```

## Server Management

### Application Management

```bash
# Start services
sudo systemctl start bizconnect

# Stop services
sudo systemctl stop bizconnect

# Check status
sudo systemctl status bizconnect

# View logs
cd /opt/bizconnect && docker compose logs -f
```

### Manual Docker Commands

```bash
cd /opt/bizconnect

# Start containers
docker compose up -d

# Stop containers
docker compose down

# Restart containers
docker compose restart

# View container status
docker compose ps

# View logs
docker compose logs -f [service-name]
```

### Database Management

```bash
# Create backup
bash /opt/bizconnect/scripts/backup.sh backup

# List backups
bash /opt/bizconnect/scripts/backup.sh list

# Restore backup
bash /opt/bizconnect/scripts/backup.sh restore <backup-file>

# Test backup integrity
bash /opt/bizconnect/scripts/backup.sh test <backup-file>
```

### Health Monitoring

```bash
# Full health check
bash /opt/bizconnect/scripts/monitor.sh health

# Check specific components
bash /opt/bizconnect/scripts/monitor.sh containers
bash /opt/bizconnect/scripts/monitor.sh app
bash /opt/bizconnect/scripts/monitor.sh database

# Generate system report
bash /opt/bizconnect/scripts/monitor.sh report
```

## SSL Configuration

### Self-Signed Certificate (Development)

The deployment script automatically generates self-signed certificates for testing.

### Let's Encrypt (Production)

For production, use Let's Encrypt:

```bash
# Install Certbot
sudo apt install certbot python3-certbot-nginx

# Generate certificate
sudo certbot --nginx -d your-domain.com

# Auto-renewal (already configured in Ubuntu 24.04)
sudo systemctl status certbot.timer
```

## Troubleshooting

### Common Issues

1. **Container won't start**
   ```bash
   docker compose logs [service-name]
   ```

2. **Database connection issues**
   ```bash
   docker exec bizconnect-postgres pg_isready -U bizconnect
   ```

3. **Application not accessible**
   ```bash
   curl -I http://localhost/health
   ```

4. **SSL certificate issues**
   ```bash
   openssl x509 -in /opt/bizconnect/ssl/cert.pem -text -noout
   ```

### Log Locations

- Application logs: `/opt/bizconnect/logs/`
- Nginx logs: Docker volume `bizconnect-nginx-logs`
- PostgreSQL logs: Docker volume `bizconnect-postgres-data`
- Docker logs: `docker compose logs`

## Security Considerations

1. **Firewall Configuration**
   ```bash
   sudo ufw allow 22/tcp    # SSH
   sudo ufw allow 80/tcp    # HTTP
   sudo ufw allow 443/tcp   # HTTPS
   sudo ufw enable
   ```

2. **Regular Updates**
   ```bash
   sudo apt update && sudo apt upgrade
   docker compose pull
   ```

3. **Backup Strategy**
   - Automated daily backups
   - 7-day retention policy
   - Test restore procedures regularly

4. **Monitoring**
   - Set up health check alerts
   - Monitor resource usage
   - Review logs regularly

## Support

For issues and questions:
1. Check the troubleshooting section
2. Review application logs
3. Run health checks
4. Contact the development team
