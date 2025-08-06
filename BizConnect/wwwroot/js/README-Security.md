# BizConnect Security System

This document describes the comprehensive client-side security system implemented for BizConnect, designed to provide enterprise-grade security features for the modern web application.

## Overview

The BizConnect Security System consists of six main components that work together to provide:
- **Real-time security monitoring**
- **Session management with timeout warnings**
- **OTAC input validation and attempt tracking**
- **CSRF protection and form security**
- **API request signing and rate limiting**
- **Visual security status indicators**

## Components

### 1. SecurityValidation (`security-validation.js`)

**Purpose**: Core security validation and CSRF protection

**Features**:
- OTAC input sanitization (8-character alphanumeric, uppercase)
- CSRF token management for all AJAX requests
- Session timeout warnings with countdown
- Form validation with security indicators
- Rate limiting with visual feedback

**Usage**:
```javascript
// Automatically initialized, but can be accessed via:
window.securityValidation.validateOtacInput(inputElement);
window.securityValidation.extendSession();
window.securityValidation.getSecurityStatus();
```

### 2. SecurityMonitor (`security-monitor.js`)

**Purpose**: Real-time security event monitoring and anomaly detection

**Features**:
- Failed attempt tracking by type
- Anomalous activity detection (rapid clicking, form resubmissions)
- Security level assessment (normal → elevated → high → critical)
- Rate limiting with visual feedback
- Security event aggregation and reporting

**Usage**:
```javascript
// Track custom security events
window.securityMonitor.trackEvent('custom_event', { details: 'data' });

// Get security report
const report = window.securityMonitor.getSecurityReport();

// Manually escalate security level
window.securityMonitor.escalateSecurityLevel('high');
```

### 3. SessionManager (`session-manager.js`)

**Purpose**: Advanced session handling with timeout management

**Features**:
- Activity-based session extension
- Multi-tab session synchronization
- Configurable timeout warnings (default: 25 min warning, 30 min timeout)
- Auto-refresh mechanisms
- Heartbeat system for server communication
- Graceful logout handling

**Usage**:
```javascript
// Get session information
const info = window.sessionManager.getSessionInfo();

// Force session extension
window.sessionManager.forceExtendSession();

// Manual logout
window.sessionManager.forceLogout();
```

### 4. SecurityWidgets (`security-widgets.js`)

**Purpose**: Visual security status indicators and user interface components

**Features**:
- Floating security status widget (draggable)
- Header security badge
- OTAC attempt counters with progress bars
- Connection security indicators
- Session timer displays
- Rate limit progress indicators
- Real-time status updates

**Usage**:
```javascript
// Update security level display
window.securityWidgets.updateSecurityLevel('elevated');

// Update OTAC attempt counter
window.securityWidgets.updateOtacAttemptCounter('formId', 2, 5);

// Show security event notification
window.securityWidgets.showSecurityEvent('rate_limit_exceeded', {});
```

### 5. ApiSecurity (`api-security.js`)

**Purpose**: Secure API communication with request signing and validation

**Features**:
- HMAC-SHA256 request signing (when enabled)
- Client-side rate limiting (60 requests/minute default)
- Response validation and integrity checking
- Request fingerprinting
- Automatic retry with exponential backoff
- Security header management

**Usage**:
```javascript
// Get security status
const status = window.apiSecurity.getSecurityStatus();

// Reset rate limiting
window.apiSecurity.resetRateLimit();

// Enable/disable features
window.apiSecurity.enableFeature('RequestSigning');
window.apiSecurity.disableFeature('ResponseValidation');
```

### 6. SecurityIntegration (`security-integration.js`)

**Purpose**: Orchestration and integration of all security components

**Features**:
- Component cross-referencing and communication
- Unified event handling
- Security testing and validation
- Development debugging tools
- Programmatic secure form creation

**Usage**:
```javascript
// Create a secure form programmatically
const $form = window.securityIntegration.createSecureForm({
    id: 'mySecureForm',
    securityLevel: 'high',
    isOtacForm: true,
    isSensitive: true
});

// Get integration status
const status = window.securityIntegration.getIntegrationStatus();
```

## Secure Form Component

### Using the Secure Form Partial

The system includes a reusable secure form component (`_SecureForm.cshtml`) that can be used throughout the application:

```csharp
@{
    var secureFormModel = new SecureFormModel
    {
        Action = "/Account/Login",
        Method = "POST",
        FormId = "loginForm",
        SecurityLevel = "high",
        IsOtacForm = false,
        IsSensitive = true,
        ShowSecurityIndicators = true
    };
}

@await Html.PartialAsync("_SecureForm", secureFormModel)
```

### Security Levels

The system supports four security levels:

1. **Normal** (green) - Standard security measures
2. **Elevated** (yellow) - Enhanced validation and monitoring
3. **High** (orange) - Restricted functionality, increased security
4. **Critical** (red) - Maximum security, some features locked

## Configuration

### Session Configuration
```javascript
window.sessionConfig = {
    sessionTimeout: 30,        // minutes
    warningTime: 25,           // minutes
    enableAutoRefresh: true,
    enableActivityTracking: true,
    enableSecurityIntegration: true
};
```

### Widget Configuration
```javascript
window.securityWidgetsConfig = {
    enableFloatingWidget: true,
    enableHeaderWidget: true,
    enableFormWidgets: true,
    position: 'bottom-right',  // top-left, top-right, bottom-left, bottom-right
    theme: 'auto'              // light, dark, auto
};
```

### API Security Configuration
```javascript
window.apiSecurityConfig = {
    enableRequestSigning: false,        // Enable when API keys configured
    enableRateLimiting: true,
    enableResponseValidation: true,
    enableRequestFingerprinting: true,
    rateLimitMax: 60,                   // requests per minute
    retryAttempts: 3
};
```

## Server-Side Integration

### Session API Controller

The system includes a dedicated API controller (`SessionApiController`) for:
- Session extension: `POST /api/session/extend`
- Heartbeat maintenance: `POST /api/session/heartbeat`
- Session information: `GET /api/session/info`
- CSRF token refresh: `GET /api/session/csrf-token`
- Security event logging: `POST /api/session/security-event`

### Required Dependencies

Ensure these services are registered in `Program.cs`:
- Authentication with cookie configuration
- Authorization policies
- Anti-forgery token configuration
- Session management
- Data protection

## Security Events

The system tracks various security events:

### High Priority Events
- `anomaly_detected` - Suspicious activity patterns
- `failed_login` - Failed authentication attempts
- `invalid_otac` - Invalid OTAC entries
- `rate_limit_exceeded` - API rate limits exceeded
- `security_level_change` - Security level escalations

### Standard Events
- `session_activity` - User activity tracking
- `session_timeout_warning` - Session expiration warnings
- `secure_form_submission` - Secure form submissions
- `otac_input_changed` - OTAC input modifications

## Development and Testing

### Debug Mode Features

In development environments, additional features are available:

1. **Security Demo Controls** - Interactive testing widgets
2. **Automated Security Tests** - Comprehensive component testing
3. **Console Logging** - Detailed security event logging
4. **Security Status Display** - Real-time security status monitoring

### Testing URL Parameters

Add these parameters to URLs for testing:
- `?security-test=true` - Run automated security tests
- `?security-demo=true` - Show security demo controls

## Mobile Responsiveness

All security components are fully responsive:
- Widgets adjust size and position on mobile devices
- Touch-friendly interfaces
- Optimized for small screens
- Accessible via keyboard navigation

## Browser Compatibility

Supported browsers:
- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## Performance Considerations

The security system is optimized for performance:
- Throttled event handlers to prevent spam
- Efficient DOM manipulation
- Minimal memory footprint
- Lazy loading of non-essential features

## Security Best Practices

When implementing:

1. **Never disable CSRF protection** in production
2. **Use HTTPS** for all security-sensitive operations
3. **Regularly update** security configurations
4. **Monitor security events** in production logs
5. **Test security features** thoroughly before deployment

## Troubleshooting

### Common Issues

1. **Scripts not loading**: Check file paths and cache
2. **CSRF token issues**: Verify meta tags are present
3. **Session timeout**: Check server-side session configuration
4. **Widget not showing**: Verify user authentication status

### Debug Information

Access debug information via:
```javascript
// Component status
console.log(window.securityIntegration.getIntegrationStatus());

// Security report
console.log(window.securityMonitor.getSecurityReport());

// Session information
console.log(window.sessionManager.getSessionInfo());
```

## Future Enhancements

Planned improvements:
- Hardware security key support
- Biometric authentication integration
- Advanced threat detection
- Machine learning anomaly detection
- Real-time security dashboards

## Support

For technical support or security concerns, contact the development team or refer to the BizConnect documentation.

---

**Version**: 1.0
**Last Updated**: 2025-08-06
**Maintainer**: BizConnect Security Team