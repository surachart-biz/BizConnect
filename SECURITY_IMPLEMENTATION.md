# BizConnect Authentication & Authorization Security Implementation

## Overview

This document describes the comprehensive three-tier authentication and authorization system implemented for BizConnect, featuring role-based access control, OTAC (One-Time Access Code) functionality, and enterprise-grade security controls.

## Three-Tier Role Hierarchy

### 1. Admin Role
- **Full System Access**: Complete access to all system features and data
- **User Management**: Can create, update, delete, and reset passwords for all users
- **Role Management**: Can assign any role (Admin, Employee, User) to users
- **OTAC Management**: Can generate, validate, and purge OTAC codes
- **System Administration**: Access to Hangfire dashboard and system monitoring

### 2. Employee Role
- **Admin Area Access**: Can access administrative dashboard and tools
- **OTAC Operations**: Can generate and validate OTAC codes for secure operations
- **Limited User Management**: Cannot manage users or roles
- **Business Operations**: Full access to business logic and KBank ODD functionality
- **Monitoring Access**: Can view Hangfire dashboard for job monitoring

### 3. User/Guest Role
- **Public Access**: Limited to public application areas
- **KBank Registration**: Can access KBank ODD registration functionality
- **No Administrative Access**: Cannot access admin areas or sensitive operations

## Security Features Implemented

### Authentication Security
- **BCrypt Password Hashing**: All passwords hashed with BCrypt for security
- **Account Lockout**: 5 failed attempts result in 15-minute IP-based lockout
- **Session Management**: 30-minute idle timeout with sliding expiration
- **Secure Cookies**: HttpOnly, Secure, SameSite=Strict cookie configuration
- **Rate Limiting**: Per-IP login attempt tracking and blocking

### Authorization Controls
- **Policy-Based Authorization**: Comprehensive policies for different access levels
- **Global Authorization**: Default require authentication for all controllers
- **Role-Based Access**: Granular permissions based on user roles
- **Anti-Forgery Protection**: CSRF protection on all state-changing operations

### OTAC (One-Time Access Code) System
- **Secure Code Generation**: Cryptographically secure 8-character alphanumeric codes
- **Attempt Limiting**: Maximum 5 validation attempts per code
- **Auto-Expiration**: Codes expire after 10 minutes
- **Audit Trail**: Complete logging of generation, validation, and usage
- **IP Tracking**: Records IP addresses for security monitoring

### Security Headers & Middleware
- **Content Security Policy**: Prevents XSS and injection attacks
- **Security Headers**: X-Frame-Options, X-Content-Type-Options, X-XSS-Protection
- **HSTS Configuration**: HTTP Strict Transport Security for HTTPS enforcement
- **Server Header Removal**: Removes server identification headers

## Database Security

### User Table Structure
```sql
CREATE TABLE "Users" (
    "Id" SERIAL PRIMARY KEY,
    "Username" VARCHAR(100) UNIQUE NOT NULL,
    "PasswordHash" VARCHAR(255) NOT NULL, -- BCrypt hashed
    "Role" VARCHAR(50) NOT NULL, -- Admin, Employee, or User
    "IsActive" BOOLEAN DEFAULT TRUE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);
```

### OTAC Table Structure
```sql
CREATE TABLE "OtacCode" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(8) NOT NULL,
    "Purpose" VARCHAR(100) NOT NULL,
    "IssuedTo" VARCHAR(256) NOT NULL,
    "AttemptCount" INTEGER DEFAULT 0,
    "IsLocked" BOOLEAN DEFAULT FALSE,
    "IsUsed" BOOLEAN DEFAULT FALSE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    "ExpiresAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "UsedAt" TIMESTAMP WITH TIME ZONE NULL,
    "ValidatedFromIp" VARCHAR(45) NULL,
    "GeneratedByUserId" INTEGER REFERENCES "Users"("Id")
);
```

## Controller Security Implementation

### Public Controllers
```csharp
[AllowAnonymous] // Explicitly allow anonymous access
public class HomeController : Controller
```

### Admin Area Controllers
```csharp
[Area("Admin")]
[Authorize(Policy = "AdminOrEmployee")] // Admin and Employee access
public class HomeController : Controller

[Area("Admin")]
[Authorize(Policy = "AdminOnly")] // Admin-only access
public class UsersController : Controller
```

### OTAC Controller
```csharp
[Area("Admin")]
[Authorize(Policy = "AdminOrEmployee")] // Admin and Employee can use OTAC
public class OtacController : Controller
```

## Security Policies Configuration

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AdminOrEmployee", policy => policy.RequireRole("Admin", "Employee"));
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("OTACVerified", policy => 
        policy.RequireAssertion(context => 
            context.User.HasClaim("otac_verified", "true")));
    
    // Default policy - require authentication
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```

## Session & Cookie Security

### Cookie Configuration
```csharp
options.Cookie.Name = "BizConnect.Auth";
options.Cookie.HttpOnly = true;
options.Cookie.SameSite = SameSiteMode.Strict;
options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Production
options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
options.SlidingExpiration = true;
```

### Data Protection
```csharp
builder.Services.AddDataProtection()
    .SetApplicationName("BizConnect")
    .PersistKeysToFileSystem(new DirectoryInfo("DataProtection-Keys"))
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
```

## Background Jobs & Maintenance

### OTAC Cleanup Job
- **Frequency**: Every 15 minutes
- **Purpose**: Automatically purge expired OTAC codes
- **Security**: Prevents database bloat and removes old access codes

### Login Attempt Cleanup
- **Implementation**: In-memory cleanup of old login attempts
- **Security**: Prevents memory leaks from long-running processes

## Logging & Monitoring

### Security Events Logged
- Successful and failed login attempts with IP addresses
- OTAC code generation, validation, and usage
- Authorization failures and access denied events
- Account lockouts and security violations
- Session creation and destruction

### Log Levels
- **Information**: Successful operations and normal flow
- **Warning**: Failed authentication attempts and security events
- **Error**: System errors and security exceptions
- **Critical**: Security breaches and system compromises

## Deployment Security Checklist

### Development Environment
- [ ] Set `CookieSecure: false` in appsettings.Development.json
- [ ] Set `RequireHttps: false` for local development
- [ ] Enable detailed logging for debugging

### Production Environment
- [ ] Set `CookieSecure: true` in appsettings.Production.json
- [ ] Set `RequireHttps: true` to enforce HTTPS
- [ ] Configure HSTS with appropriate max-age
- [ ] Set up proper SSL/TLS certificates
- [ ] Configure security headers in reverse proxy
- [ ] Set up log aggregation and monitoring
- [ ] Enable real-time security alerting

## Security Best Practices Implemented

1. **Least Privilege**: Users have minimum necessary permissions
2. **Defense in Depth**: Multiple layers of security controls
3. **Fail Secure**: Default to denying access when uncertain
4. **Zero Trust**: Verify every request, trust nothing implicitly
5. **Audit Everything**: Comprehensive logging of security events
6. **Regular Cleanup**: Automated removal of expired codes and attempts
7. **Secure Defaults**: Safe configuration out of the box

## Future Security Enhancements

1. **Two-Factor Authentication**: SMS/Email-based 2FA
2. **Account Lockout Escalation**: Progressive lockout periods
3. **Geo-location Tracking**: Monitor login locations
4. **Security Questions**: Additional authentication factors
5. **Password Complexity Rules**: Configurable password policies
6. **Token-Based API Authentication**: JWT for API endpoints
7. **Database Encryption**: Encrypt sensitive data at rest

## Compliance Considerations

This implementation provides a foundation for meeting various compliance requirements:
- **GDPR**: Data protection and user consent mechanisms
- **SOX**: Access controls and audit trails
- **HIPAA**: Authentication and authorization controls
- **PCI DSS**: Secure authentication and session management

## Support & Maintenance

For security-related issues or questions:
1. Review logs for security events
2. Check rate limiting and lockout status
3. Verify role assignments and permissions
4. Monitor OTAC usage patterns
5. Review failed authentication attempts

Regular security maintenance tasks:
- Review and rotate encryption keys
- Update security configurations
- Monitor for new vulnerabilities
- Test security controls
- Review access logs and patterns