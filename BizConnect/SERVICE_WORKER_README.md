# BizConnect Service Worker Implementation

This document explains the Service Worker implementation for BizConnect, which provides Progressive Web App (PWA) capabilities and solves caching issues.

## Overview

The Service Worker implementation includes:
- **Automatic cache management** with versioning
- **Immediate activation** of new versions
- **Update notifications** to users
- **Offline support** for cached resources
- **Background sync** capabilities

## Files Added

### Core Service Worker Files
- `wwwroot/service-worker.js` - Main Service Worker implementation
- `wwwroot/js/sw-register.js` - Registration and update management
- `wwwroot/manifest.json` - PWA manifest
- `wwwroot/browserconfig.xml` - Windows tile configuration

### Testing and Utilities
- `wwwroot/sw-test.html` - Service Worker testing dashboard
- `update-sw-version.ps1` - PowerShell version update script
- `update-sw-version.sh` - Bash version update script
- `wwwroot/icons/` - PWA icons directory

## How It Works

### 1. Service Worker Registration
The Service Worker is automatically registered when users visit the site:
- Registers with scope `/` to handle all requests
- Sets up update listeners
- Checks for updates every 30 minutes
- Checks for updates when page becomes visible

### 2. Caching Strategy
- **Static Assets**: Cache-first with network fallback
- **Runtime Assets**: Stale-while-revalidate for fonts and CDN resources
- **Navigation**: Network-first with cache fallback

### 3. Update Flow
1. New Service Worker version is detected
2. User receives update notification
3. User can choose to update immediately or later
4. Page reloads automatically after update

## Testing the Implementation

### 1. Initial Setup
1. Build and run the application
2. Visit the site in a modern browser
3. Open DevTools → Application → Service Workers
4. Verify the Service Worker is registered and running

### 2. Using the Test Dashboard
Visit `/sw-test.html` to access the testing dashboard:
- **Status Indicators**: Shows SW support, registration, controller, and update status
- **Action Buttons**: Test updates, force updates, unregister, clear cache
- **Cache Information**: View current cache contents
- **Console Log**: Real-time logging of SW activities

### 3. Testing Update Flow

#### Method 1: Using Update Scripts
```bash
# Auto-increment version (recommended)
./update-sw-version.sh --auto-increment

# Or specify version manually
./update-sw-version.sh --version "bizconnect-v1.1.0"
```

```powershell
# Auto-increment version (recommended)
.\update-sw-version.ps1 -AutoIncrement

# Or specify version manually
.\update-sw-version.ps1 -NewVersion "bizconnect-v1.1.0"
```

#### Method 2: Manual Version Update
1. Edit `wwwroot/service-worker.js`
2. Change `const CACHE_VERSION = 'bizconnect-v1.0.0';` to a new version
3. Build and deploy the application

### 4. Verification Checklist

#### Fresh Visit (No Hard Reload Needed)
- [ ] Latest assets load automatically
- [ ] No stale CSS/JS files
- [ ] Correct theme/styling applied

#### DevTools Network Tab
- [ ] New hashed CSS/JS files after rebuild
- [ ] Service Worker serves cached resources
- [ ] Network requests show proper cache headers

#### Service Worker Status
- [ ] Only one active worker in DevTools
- [ ] Worker status shows "Running"
- [ ] Scope is set to `/`

#### PWA Audit
- [ ] Lighthouse PWA audit passes
- [ ] No outdated Service Worker warnings
- [ ] Manifest is valid and accessible

## Troubleshooting

### Common Issues

#### 1. Service Worker Not Registering
- Check browser console for errors
- Ensure HTTPS is used (required for SW)
- Verify `service-worker.js` is accessible at root

#### 2. Updates Not Working
- Check if `skipWaiting()` is called in install event
- Verify `clients.claim()` is called in activate event
- Ensure version number is actually changed

#### 3. Caching Issues Persist
- Clear all browser data and test fresh
- Check if assets are properly versioned with `asp-append-version="true"`
- Verify cache patterns in Service Worker match your assets

#### 4. Update Notifications Not Showing
- Check if `BizConnectLoading` is available
- Verify message passing between SW and client
- Check browser console for registration errors

### Debug Commands

```javascript
// In browser console
// Check Service Worker status
navigator.serviceWorker.getRegistration().then(reg => console.log(reg));

// Get current version
window.swManager.getServiceWorkerVersion().then(v => console.log('Version:', v));

// Force update check
window.swManager.forceUpdate();

// Get cache contents
caches.keys().then(names => console.log('Caches:', names));
```

## Configuration

### Environment-Specific Behavior
- **Development**: Static files have no-cache headers
- **Production**: Static files use default caching with version hashing

### Customization Options

#### Cache Assets
Edit `STATIC_ASSETS` array in `service-worker.js`:
```javascript
const STATIC_ASSETS = [
    '/',
    '/css/site.css',
    // Add your assets here
];
```

#### Runtime Patterns
Edit `RUNTIME_PATTERNS` array for external resources:
```javascript
const RUNTIME_PATTERNS = [
    /^https:\/\/fonts\.googleapis\.com/,
    // Add your patterns here
];
```

#### Update Notification
Customize in `sw-register.js`:
```javascript
showUpdateNotification() {
    // Customize notification appearance and behavior
}
```

## Best Practices

1. **Always increment version** when deploying changes
2. **Test update flow** before production deployment
3. **Monitor Service Worker** status in production
4. **Use versioned assets** with `asp-append-version="true"`
5. **Clear old caches** regularly to prevent storage bloat

## Production Deployment

1. Update Service Worker version using provided scripts
2. Build application with production configuration
3. Deploy to server
4. Verify Service Worker registration
5. Test update flow with a small user group first

## Monitoring

Monitor these metrics in production:
- Service Worker registration success rate
- Update notification acceptance rate
- Cache hit/miss ratios
- Offline usage patterns

Use browser DevTools and the test dashboard to diagnose issues in production.
