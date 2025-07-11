# GitLab CI/CD Variables Configuration

This document lists all the required GitLab CI/CD variables for the BizConnect deployment pipeline.

## How to Set Variables

1. Go to your GitLab project
2. Navigate to **Settings** > **CI/CD**
3. Expand **Variables** section
4. Click **Add variable** for each variable below

## Required Variables

### SSH Configuration

| Variable Name | Type | Protected | Masked | Description |
|---------------|------|-----------|--------|-------------|
| `SSH_PRIVATE_KEY` | Variable | ✅ | ❌ | SSH private key for server access (PEM format) |

**SSH_PRIVATE_KEY Format:**
```
-----BEGIN OPENSSH PRIVATE KEY-----
[Your private key content here]
-----END OPENSSH PRIVATE KEY-----
```

### Production Environment

| Variable Name | Type | Protected | Masked | Description |
|---------------|------|-----------|--------|-------------|
| `PRODUCTION_SERVER` | Variable | ✅ | ❌ | Production server IP address or hostname |
| `PRODUCTION_USER` | Variable | ✅ | ❌ | SSH username for production server |
| `PRODUCTION_DOMAIN` | Variable | ✅ | ❌ | Production domain name (e.g., bizconnect.com) |

### UAT Environment

| Variable Name | Type | Protected | Masked | Description |
|---------------|------|-----------|--------|-------------|
| `UAT_SERVER` | Variable | ✅ | ❌ | UAT server IP address or hostname |
| `UAT_USER` | Variable | ✅ | ❌ | SSH username for UAT server |
| `UAT_DOMAIN` | Variable | ✅ | ❌ | UAT domain name (e.g., uat.bizconnect.com) |

### Auto-Provided Variables

These variables are automatically provided by GitLab:

| Variable Name | Description |
|---------------|-------------|
| `CI_REGISTRY` | GitLab Container Registry URL |
| `CI_REGISTRY_USER` | GitLab Container Registry username |
| `CI_REGISTRY_PASSWORD` | GitLab Container Registry password |
| `CI_REGISTRY_IMAGE` | Full image name for the project |
| `CI_COMMIT_SHORT_SHA` | Short commit SHA for tagging |

## Server Environment Variables

These variables should be set on the target servers (in `.env` file):

### Production Server (.env)
```env
# Database
POSTGRES_PASSWORD=your_secure_production_password

# Docker Registry
CI_REGISTRY_IMAGE=registry.gitlab.com/your-group/bizconnect

# Domain
PRODUCTION_DOMAIN=bizconnect.com

# Optional: Redis
REDIS_PASSWORD=your_redis_password

# Optional: Email
SMTP_HOST=smtp.your-domain.com
SMTP_PORT=587
SMTP_USERNAME=noreply@your-domain.com
SMTP_PASSWORD=your_smtp_password
```

### UAT Server (.env)
```env
# Database
POSTGRES_PASSWORD=your_secure_uat_password

# Docker Registry
CI_REGISTRY_IMAGE=registry.gitlab.com/your-group/bizconnect

# Domain
UAT_DOMAIN=uat.bizconnect.com

# Optional: Redis
REDIS_PASSWORD=your_redis_password
```

## SSH Key Setup

### 1. Generate SSH Key Pair
```bash
# Generate new SSH key pair
ssh-keygen -t rsa -b 4096 -C "gitlab-ci@bizconnect" -f ~/.ssh/bizconnect_deploy

# Copy public key to servers
ssh-copy-id -i ~/.ssh/bizconnect_deploy.pub user@production-server
ssh-copy-id -i ~/.ssh/bizconnect_deploy.pub user@uat-server
```

### 2. Add Private Key to GitLab
```bash
# Display private key (copy this to GitLab variable)
cat ~/.ssh/bizconnect_deploy
```

## Security Best Practices

### Variable Protection
- ✅ **Protected**: Only available in protected branches (main, develop)
- ✅ **Masked**: Hidden in job logs (for sensitive values)

### SSH Security
- Use dedicated SSH keys for CI/CD (not personal keys)
- Restrict SSH key access to specific users
- Use strong passwords for database and services
- Regularly rotate SSH keys and passwords

### Server Security
```bash
# Configure firewall
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS
sudo ufw enable

# Disable password authentication (use keys only)
sudo sed -i 's/#PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
sudo systemctl restart sshd
```

## Testing Variables

You can test your variables by running a manual pipeline or checking the CI/CD logs.

### Test SSH Connection
```bash
# Test from your local machine
ssh -i ~/.ssh/bizconnect_deploy user@production-server "echo 'SSH connection successful'"
```

### Test Docker Registry Access
The pipeline will automatically test Docker registry access during the publish stage.

## Troubleshooting

### Common Issues

1. **SSH Permission Denied**
   - Check SSH key format in GitLab variable
   - Verify public key is installed on server
   - Check SSH user permissions

2. **Docker Registry Login Failed**
   - Verify GitLab Container Registry is enabled
   - Check project visibility settings
   - Ensure CI/CD is enabled for the project

3. **Server Connection Timeout**
   - Verify server IP/hostname
   - Check firewall settings
   - Ensure SSH service is running

### Debug Commands

Add these to your pipeline for debugging:

```yaml
debug_variables:
  stage: deploy
  script:
    - echo "Registry: $CI_REGISTRY"
    - echo "Image: $CI_REGISTRY_IMAGE"
    - echo "Production Server: $PRODUCTION_SERVER"
    - ssh -o StrictHostKeyChecking=no $PRODUCTION_USER@$PRODUCTION_SERVER "echo 'Connection test successful'"
  when: manual
```

## Environment-Specific Configurations

### Production
- Protected branches: `main`
- Manual deployment required
- Full SSL/TLS configuration
- Production database credentials

### UAT
- Protected branches: `develop`
- Manual deployment required
- Self-signed certificates acceptable
- UAT database credentials

### Development
- Any branch can trigger build/test
- No deployment to servers
- Local development only
