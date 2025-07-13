# KBank Online Direct Debit (ODD) Setup Guide

This document provides instructions for setting up KBank Online Direct Debit integration in the BizConnect application.

## 🔧 Configuration Setup

### 1. PassPhrase Configuration (SECRETS ONLY)

⚠️ **IMPORTANT**: The PassPhrase is **NEVER** stored in configuration files. It must be configured through secrets management only:

- **Development**: `dotnet user-secrets` (key: `KBankODD:PassPhrase`)
- **CI/Production**: GitLab variable `KBankODD__PassPhrase`

The application reads the PassPhrase via `IConfiguration["KBankODD:PassPhrase"]` - never hard-coded.

#### Local Development (User Secrets)
Configure using dotnet user-secrets:
```bash
# Navigate to the BizConnect project directory
cd BizConnect

# Initialize user secrets (if not already done)
dotnet user-secrets init

# Set the KBank PassPhrase
dotnet user-secrets set "KBankODD:PassPhrase" "your-actual-kbank-passphrase-here"

# Verify the secret is set
dotnet user-secrets list
```

#### CI/Production (GitLab Variables)
Configure in GitLab CI/CD Variables:
- **Variable Name**: `KBankODD__PassPhrase`
- **Value**: Your actual KBank PassPhrase
- **Type**: Variable (masked)
- **Environment**: Specific to UAT/Production
- **Protected**: Yes (for production)

### 2. Alternative: Environment Variables

You can also set the PassPhrase via environment variables:

```bash
# Linux/macOS
export KBankODD__PassPhrase="your-production-passphrase"

# Windows PowerShell
$env:KBankODD__PassPhrase="your-production-passphrase"

# Windows Command Prompt
set KBankODD__PassPhrase=your-production-passphrase
```

### 3. Docker Deployment

For Docker deployments:
```yaml
# docker-compose.yml
environment:
  - KBankODD__PassPhrase=your-production-passphrase
```

### 4. Azure Key Vault (Enterprise)

For enterprise deployments with Azure Key Vault integration:
```bash
# Set as GitLab variable that references Key Vault
KBankODD__PassPhrase="@Microsoft.KeyVault(SecretUri=https://your-keyvault.vault.azure.net/secrets/kbank-passphrase/)"
```

## 🌐 Environment URLs

The configuration includes different URLs for different environments:

- **Local/Development**: `https://ws06.uat.kasikornbank.com` (UAT environment)
- **UAT**: `https://ws06.uat.kasikornbank.com` (UAT environment)
- **Production**: `https://ws06.kasikornbank.com` (Production environment)

## 🔐 Security Policy Compliance

✅ **ACCEPTANCE CHECKLIST**:
- [x] PassPhrase pulled from config via `IConfiguration["KBankODD:PassPhrase"]`, never literal in code
- [x] Development uses `dotnet user-secrets` (key: `KBankODD:PassPhrase`)
- [x] CI/Production uses GitLab variable `KBankODD__PassPhrase`
- [x] Tests inject dummy PassPhrase via in-memory configuration
- [x] No PassPhrase values in any appsettings files

### Security Best Practices:

1. **✅ NEVER commit PassPhrases to version control** - All appsettings files exclude PassPhrase
2. **✅ Use secrets management only** - dotnet user-secrets for dev, GitLab variables for CI/Prod
3. **✅ Code reads from IConfiguration** - All PassPhrase access via `_configuration["KBankODD:PassPhrase"]`
4. **✅ Tests use dummy values** - In-memory configuration with test PassPhrases
5. **Rotate PassPhrases regularly** as per KBank's security policy
6. **Ensure HTTPS is enabled** for all environments
7. **Monitor and log authentication failures** (without exposing PassPhrase)

## 🚀 API Endpoints

Once configured, the following endpoints will be available:

### User Registration Form
- **URL**: `GET /kbank/odd/register`
- **Authentication**: Required (User must be logged in)
- **Description**: Displays KBank ODD registration form for collecting user contact information
- **Response**: Returns HTML form with email, mobile, ID type, and ID value fields

### User Registration Processing
- **URL**: `POST /kbank/odd/register`
- **Authentication**: Required (User must be logged in)
- **Description**: Processes registration form and initiates KBank ODD registration
- **Request Body**: Form data with Email, MobileNo, IdType, IdValue
- **Response**: Redirects to KBank's registration page with user contact information

### Status Update Callback
- **URL**: `POST /kbank/odd/status-update`
- **Authentication**: SHA-256 hash validation
- **Description**: Receives status updates from KBank
- **Content-Type**: `application/x-www-form-urlencoded`

## 📋 Registration Flow

The KBank ODD registration process follows these steps:

1. **User Authentication**: User logs into BizConnect
2. **Automatic Redirect**: After successful login, user is redirected to `/kbank/odd/register`
3. **Form Display**: Registration form is displayed with the following fields:
   - **Email**: User's email address (validated format)
   - **Mobile Number**: Thai mobile format (08xxxxxxxx or +66xxxxxxxx)
   - **ID Type**: Dropdown selection (National ID, Passport, Tax ID, Company Tax ID)
   - **ID Value**: ID number with type-specific validation
4. **Form Validation**: Client-side and server-side validation ensures data quality
5. **Form Submission**: Valid form data is submitted to KBank's RegisterInit API
6. **KBank Redirect**: User is redirected to KBank's secure registration platform
7. **Registration Completion**: User completes registration on KBank's platform
8. **Status Updates**: KBank sends status updates via webhook to update registration status

## 🧪 Testing

### Unit Tests
Run the KBank ODD tests to verify configuration:

```bash
# Test all KBank ODD components
dotnet test --filter "FullyQualifiedName~KbankOdd"

# Test utilities
dotnet test --filter "FullyQualifiedName~OddUtils"

# Test controller
dotnet test --filter "FullyQualifiedName~KBankController"
```

### Integration Testing

1. **Configure UAT PassPhrase** in your local environment
2. **Run the application** in Development mode
3. **Navigate to** `/kbank/odd/register` (requires login)
4. **Verify redirect** to KBank's UAT registration page

## 📋 Database Schema

The integration uses the existing `KbankOddRegistration` table:

```sql
-- Table: KbankOddRegistration
-- Columns:
-- - Id (Primary Key)
-- - ExternalReference (Unique, format: BIZyyyyMMddHHmmssfff)
-- - RegId (KBank Registration ID)
-- - EspaId (KBank ESPA ID, populated after successful registration)
-- - Status (Pending/Success/Fail)
-- - ReturnCode (KBank return code)
-- - Email (User email address for ODD registration)
-- - MobileNo (User mobile number, format: 08xxxxxxxx or +66xxxxxxxx)
-- - IdType (ID type: National ID, Passport, Tax ID, or Company Tax ID)
-- - IdValue (ID number/value corresponding to the selected ID type)
-- - CreatedAt (Timestamp)
-- - UpdatedAt (Timestamp)
```

### Form Validation Rules

The registration form implements comprehensive validation:

- **Email**: Required, valid email format, max 256 characters
- **Mobile Number**: Required, Thai format (08xxxxxxxx or +66xxxxxxxx), max 20 characters
- **ID Type**: Required, must be one of: National ID, Passport, Tax ID, Company Tax ID
- **ID Value**: Required, 8-30 characters with type-specific validation:
  - **National ID**: Exactly 13 digits
  - **Passport**: 8-20 alphanumeric characters
  - **Tax ID**: 10-13 digits
  - **Company Tax ID**: Exactly 13 digits

## 🔍 Troubleshooting

### Common Issues

1. **"PassPhrase not configured" error**
   - Ensure PassPhrase is set in the correct appsettings file
   - Check environment variable configuration

2. **"Invalid authentication" in status updates**
   - Verify PassPhrase matches KBank's configuration
   - Check SHA-256 hash generation logic

3. **"KBank initialization failed"**
   - Verify BaseUrl is correct for the environment
   - Check network connectivity to KBank servers
   - Validate ExternalSystem and ServiceName configuration

### Logging

Enable detailed logging for troubleshooting:

```json
{
  "Logging": {
    "LogLevel": {
      "BizConnect.Services.KbankOddService": "Debug",
      "BizConnect.Services.Clients.KBankOddClient": "Debug"
    }
  }
}
```

## 📞 Support

For KBank ODD integration issues:
1. Check application logs for detailed error messages
2. Verify configuration against this guide
3. Contact KBank technical support for API-related issues
4. Review KBank ODD API documentation for latest requirements
