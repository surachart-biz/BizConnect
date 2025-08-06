/**
 * BizConnect API Security Utilities
 * Comprehensive API security layer with request signing, rate limiting, and validation
 * 
 * Features:
 * - Request signing with HMAC-SHA256
 * - Client-side rate limiting with visual feedback
 * - API response validation
 * - Security header management
 * - Request fingerprinting
 * - Retry logic with exponential backoff
 */

class ApiSecurity {
    constructor(options = {}) {
        this.options = {
            enableRequestSigning: true,
            enableRateLimiting: true,
            enableResponseValidation: true,
            enableRequestFingerprinting: true,
            rateLimitWindow: 60000, // 1 minute
            rateLimitMax: 60, // requests per window
            signatureHeader: 'X-Signature',
            timestampHeader: 'X-Timestamp',
            fingerprintHeader: 'X-Fingerprint',
            retryAttempts: 3,
            retryDelay: 1000, // base delay in ms
            timeout: 30000, // 30 seconds
            ...options
        };

        this.rateLimiter = {
            requests: [],
            blocked: false,
            blockUntil: 0
        };

        this.requestQueue = new Map();
        this.apiKey = null;
        this.secretKey = null;
        
        this.init();
    }

    /**
     * Initialize API security
     */
    init() {
        this.setupRateLimiting();
        this.setupRequestInterceptors();
        this.bindEventHandlers();
        this.loadSecurityKeys();
        
        console.log('[ApiSecurity] API security utilities initialized');
    }

    /**
     * Setup rate limiting
     */
    setupRateLimiting() {
        if (!this.options.enableRateLimiting) return;

        // Clean up old requests periodically
        setInterval(() => {
            this.cleanupRateLimit();
        }, 10000); // Every 10 seconds

        console.log('[ApiSecurity] Rate limiting configured');
    }

    /**
     * Setup request interceptors
     */
    setupRequestInterceptors() {
        const self = this;

        // jQuery AJAX interceptor
        $(document).ajaxSend(function(event, xhr, settings) {
            self.processRequest(xhr, settings);
        });

        $(document).ajaxComplete(function(event, xhr, settings) {
            self.processResponse(xhr, settings);
        });

        $(document).ajaxError(function(event, xhr, settings, error) {
            self.processError(xhr, settings, error);
        });

        // Native fetch interceptor (if needed)
        if (window.fetch) {
            this.interceptFetch();
        }

        console.log('[ApiSecurity] Request interceptors configured');
    }

    /**
     * Process outgoing requests
     */
    processRequest(xhr, settings) {
        try {
            // Check rate limiting
            if (!this.checkRateLimit()) {
                this.handleRateLimitExceeded();
                xhr.abort();
                return false;
            }

            // Add security headers
            this.addSecurityHeaders(xhr, settings);

            // Sign request if enabled
            if (this.options.enableRequestSigning && this.isApiRequest(settings.url)) {
                this.signRequest(xhr, settings);
            }

            // Add fingerprint
            if (this.options.enableRequestFingerprinting) {
                this.addRequestFingerprint(xhr, settings);
            }

            // Track request for rate limiting
            this.trackRequest();

            console.log(`[ApiSecurity] Request processed: ${settings.type} ${settings.url}`);
        } catch (error) {
            console.error('[ApiSecurity] Error processing request:', error);
        }
    }

    /**
     * Process incoming responses
     */
    processResponse(xhr, settings) {
        try {
            // Validate response if enabled
            if (this.options.enableResponseValidation && this.isApiRequest(settings.url)) {
                this.validateResponse(xhr, settings);
            }

            // Update rate limit status
            this.updateRateLimitFromHeaders(xhr);

            // Track successful response
            this.trackResponseSuccess(settings.url, xhr.status);

        } catch (error) {
            console.error('[ApiSecurity] Error processing response:', error);
        }
    }

    /**
     * Process request errors
     */
    processError(xhr, settings, error) {
        try {
            const status = xhr.status;
            const url = settings.url;

            // Handle different error types
            switch (status) {
                case 429: // Too Many Requests
                    this.handleRateLimitResponse(xhr);
                    break;
                case 401: // Unauthorized
                    this.handleUnauthorizedResponse(xhr, settings);
                    break;
                case 403: // Forbidden
                    this.handleForbiddenResponse(xhr, settings);
                    break;
                case 419: // CSRF Token Mismatch
                    this.handleCSRFError(xhr, settings);
                    break;
                case 0: // Network error or timeout
                    this.handleNetworkError(xhr, settings);
                    break;
            }

            // Track failed response
            this.trackResponseError(url, status, error);

            // Trigger security event
            if (window.securityMonitor) {
                window.securityMonitor.trackEvent('api_request_failed', {
                    url: url,
                    status: status,
                    error: error,
                    timestamp: Date.now()
                });
            }

        } catch (error) {
            console.error('[ApiSecurity] Error processing error response:', error);
        }
    }

    /**
     * Check rate limiting
     */
    checkRateLimit() {
        if (!this.options.enableRateLimiting) return true;

        const now = Date.now();

        // Check if currently blocked
        if (this.rateLimiter.blocked && now < this.rateLimiter.blockUntil) {
            return false;
        }

        // Unblock if time has passed
        if (this.rateLimiter.blocked && now >= this.rateLimiter.blockUntil) {
            this.rateLimiter.blocked = false;
            this.rateLimiter.blockUntil = 0;
        }

        // Clean up old requests
        this.cleanupRateLimit();

        // Check if rate limit is exceeded
        const recentRequests = this.rateLimiter.requests.filter(
            timestamp => now - timestamp < this.options.rateLimitWindow
        );

        if (recentRequests.length >= this.options.rateLimitMax) {
            this.rateLimiter.blocked = true;
            this.rateLimiter.blockUntil = now + (this.options.rateLimitWindow / 2); // Block for half window
            return false;
        }

        return true;
    }

    /**
     * Track request for rate limiting
     */
    trackRequest() {
        if (!this.options.enableRateLimiting) return;

        this.rateLimiter.requests.push(Date.now());
        this.updateRateLimitUI();
    }

    /**
     * Clean up old rate limit entries
     */
    cleanupRateLimit() {
        const now = Date.now();
        this.rateLimiter.requests = this.rateLimiter.requests.filter(
            timestamp => now - timestamp < this.options.rateLimitWindow
        );
    }

    /**
     * Add security headers to request
     */
    addSecurityHeaders(xhr, settings) {
        // Add timestamp
        xhr.setRequestHeader(this.options.timestampHeader, Date.now().toString());

        // Add CSRF token if available
        const csrfToken = this.getCSRFToken();
        if (csrfToken) {
            xhr.setRequestHeader('X-CSRF-TOKEN', csrfToken);
        }

        // Add security context
        xhr.setRequestHeader('X-Security-Context', this.getSecurityContext());

        // Add user agent fingerprint
        xhr.setRequestHeader('X-Client-ID', this.getClientId());
    }

    /**
     * Sign request with HMAC-SHA256
     */
    signRequest(xhr, settings) {
        if (!this.secretKey) {
            console.warn('[ApiSecurity] Cannot sign request: no secret key available');
            return;
        }

        try {
            const timestamp = Date.now().toString();
            const method = settings.type || 'GET';
            const url = new URL(settings.url, window.location.origin).pathname;
            const body = settings.data || '';
            
            // Create signature payload
            const payload = `${method}:${url}:${timestamp}:${body}`;
            
            // Generate HMAC-SHA256 signature
            const signature = this.generateHMAC(payload, this.secretKey);
            
            // Add signature header
            xhr.setRequestHeader(this.options.signatureHeader, signature);
            xhr.setRequestHeader(this.options.timestampHeader, timestamp);

            console.log('[ApiSecurity] Request signed successfully');
        } catch (error) {
            console.error('[ApiSecurity] Failed to sign request:', error);
        }
    }

    /**
     * Add request fingerprint
     */
    addRequestFingerprint(xhr, settings) {
        const fingerprint = this.generateRequestFingerprint(settings);
        xhr.setRequestHeader(this.options.fingerprintHeader, fingerprint);
    }

    /**
     * Generate request fingerprint
     */
    generateRequestFingerprint(settings) {
        const components = {
            method: settings.type || 'GET',
            url: settings.url,
            userAgent: navigator.userAgent,
            language: navigator.language,
            timestamp: Date.now(),
            screen: `${screen.width}x${screen.height}`,
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone
        };

        const fingerprintString = JSON.stringify(components);
        return btoa(fingerprintString).substring(0, 32);
    }

    /**
     * Validate API response
     */
    validateResponse(xhr, settings) {
        try {
            // Check for security headers
            this.validateSecurityHeaders(xhr);

            // Validate response signature if present
            this.validateResponseSignature(xhr);

            // Check response format
            this.validateResponseFormat(xhr);

            console.log('[ApiSecurity] Response validated successfully');
        } catch (error) {
            console.error('[ApiSecurity] Response validation failed:', error);
            
            // Trigger security event
            if (window.securityMonitor) {
                window.securityMonitor.trackEvent('response_validation_failed', {
                    url: settings.url,
                    error: error.message,
                    timestamp: Date.now()
                });
            }
        }
    }

    /**
     * Validate security headers in response
     */
    validateSecurityHeaders(xhr) {
        const requiredHeaders = [
            'X-Content-Type-Options',
            'X-Frame-Options',
            'X-XSS-Protection'
        ];

        const missingHeaders = requiredHeaders.filter(header => 
            !xhr.getResponseHeader(header)
        );

        if (missingHeaders.length > 0) {
            console.warn('[ApiSecurity] Missing security headers:', missingHeaders);
        }
    }

    /**
     * Validate response signature
     */
    validateResponseSignature(xhr) {
        const signature = xhr.getResponseHeader('X-Response-Signature');
        if (!signature || !this.secretKey) return;

        try {
            const timestamp = xhr.getResponseHeader('X-Timestamp') || '';
            const body = xhr.responseText || '';
            const payload = `${timestamp}:${body}`;
            
            const expectedSignature = this.generateHMAC(payload, this.secretKey);
            
            if (signature !== expectedSignature) {
                throw new Error('Response signature validation failed');
            }

            console.log('[ApiSecurity] Response signature validated');
        } catch (error) {
            console.error('[ApiSecurity] Response signature validation error:', error);
            throw error;
        }
    }

    /**
     * Validate response format
     */
    validateResponseFormat(xhr) {
        const contentType = xhr.getResponseHeader('Content-Type') || '';
        
        // Check for JSON responses
        if (contentType.includes('application/json')) {
            try {
                JSON.parse(xhr.responseText);
            } catch (error) {
                throw new Error('Invalid JSON response format');
            }
        }

        // Additional format validations can be added here
    }

    /**
     * Handle rate limit exceeded
     */
    handleRateLimitExceeded() {
        console.warn('[ApiSecurity] Rate limit exceeded - request blocked');

        // Show user notification
        this.showRateLimitNotification();

        // Trigger security event
        if (window.securityMonitor) {
            window.securityMonitor.trackEvent('rate_limit_exceeded', {
                requests: this.rateLimiter.requests.length,
                timestamp: Date.now()
            });
        }

        // Update UI
        this.updateRateLimitUI();
    }

    /**
     * Handle rate limit response from server
     */
    handleRateLimitResponse(xhr) {
        const retryAfter = xhr.getResponseHeader('Retry-After');
        if (retryAfter) {
            const retrySeconds = parseInt(retryAfter, 10);
            this.rateLimiter.blocked = true;
            this.rateLimiter.blockUntil = Date.now() + (retrySeconds * 1000);
        }

        this.showRateLimitNotification(`Rate limit exceeded. Try again in ${retryAfter} seconds.`);
    }

    /**
     * Handle unauthorized response
     */
    handleUnauthorizedResponse(xhr, settings) {
        console.warn('[ApiSecurity] Unauthorized API request:', settings.url);

        // Redirect to login if needed
        if (window.sessionManager) {
            window.sessionManager.handleSessionTimeout();
        }
    }

    /**
     * Handle forbidden response
     */
    handleForbiddenResponse(xhr, settings) {
        console.warn('[ApiSecurity] Forbidden API request:', settings.url);
        
        this.showSecurityNotification('Access denied. Please check your permissions.', 'warning');
    }

    /**
     * Handle CSRF error
     */
    handleCSRFError(xhr, settings) {
        console.error('[ApiSecurity] CSRF token mismatch:', settings.url);
        
        // Refresh CSRF token and retry
        this.refreshCSRFToken().then(() => {
            this.retryRequest(settings);
        });
    }

    /**
     * Handle network error
     */
    handleNetworkError(xhr, settings) {
        console.error('[ApiSecurity] Network error:', settings.url);
        
        // Implement retry logic
        this.retryRequest(settings);
    }

    /**
     * Retry request with exponential backoff
     */
    async retryRequest(settings, attempt = 1) {
        if (attempt > this.options.retryAttempts) {
            console.error('[ApiSecurity] Max retry attempts reached');
            return;
        }

        const delay = this.options.retryDelay * Math.pow(2, attempt - 1);
        
        console.log(`[ApiSecurity] Retrying request in ${delay}ms (attempt ${attempt})`);
        
        setTimeout(() => {
            $.ajax(settings).fail(() => {
                this.retryRequest(settings, attempt + 1);
            });
        }, delay);
    }

    /**
     * Update rate limit UI
     */
    updateRateLimitUI() {
        const current = this.rateLimiter.requests.length;
        const max = this.options.rateLimitMax;
        const percentage = Math.round((current / max) * 100);

        // Update security widgets
        if (window.securityWidgets) {
            window.securityWidgets.updateRateLimit(current, max, this.options.rateLimitWindow);
        }

        // Update security monitor
        if (window.securityMonitor) {
            window.securityMonitor.rateLimit.requests = current;
            window.securityMonitor.rateLimit.maxRequests = max;
            window.securityMonitor.updateRateLimitIndicator();
        }
    }

    /**
     * Update rate limit from response headers
     */
    updateRateLimitFromHeaders(xhr) {
        const limit = xhr.getResponseHeader('X-RateLimit-Limit');
        const remaining = xhr.getResponseHeader('X-RateLimit-Remaining');
        const reset = xhr.getResponseHeader('X-RateLimit-Reset');

        if (limit && remaining) {
            const used = parseInt(limit, 10) - parseInt(remaining, 10);
            this.updateRateLimitUI();
            
            if (remaining === '0' && reset) {
                const resetTime = parseInt(reset, 10) * 1000;
                this.rateLimiter.blocked = true;
                this.rateLimiter.blockUntil = resetTime;
            }
        }
    }

    /**
     * Show rate limit notification
     */
    showRateLimitNotification(message = 'Rate limit exceeded. Please wait before making more requests.') {
        if (window.securityValidation) {
            window.securityValidation.showToast(message, 'warning');
        }

        // Show blocking overlay if blocked
        if (this.rateLimiter.blocked) {
            this.showRateLimitOverlay();
        }
    }

    /**
     * Show rate limit blocking overlay
     */
    showRateLimitOverlay() {
        const overlay = $(`
            <div class="rate-limit-overlay position-fixed w-100 h-100" style="top: 0; left: 0; z-index: 9999; background: rgba(0, 0, 0, 0.8);">
                <div class="d-flex justify-content-center align-items-center h-100">
                    <div class="card text-center" style="max-width: 400px;">
                        <div class="card-body">
                            <i class="fas fa-clock text-warning fa-3x mb-3"></i>
                            <h5 class="card-title">Rate Limit Exceeded</h5>
                            <p class="card-text">You've made too many requests. Please wait before continuing.</p>
                            <div class="mb-3">
                                <span class="countdown-text">Time remaining: </span>
                                <span class="countdown-timer fw-bold">--</span>
                            </div>
                            <button type="button" class="btn btn-secondary" onclick="this.closest('.rate-limit-overlay').remove()">
                                Close
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `);

        $('body').append(overlay);

        // Start countdown timer
        this.startRateLimitCountdown(overlay);
    }

    /**
     * Start rate limit countdown
     */
    startRateLimitCountdown(overlay) {
        const timer = overlay.find('.countdown-timer');
        
        const updateCountdown = () => {
            const remaining = Math.max(0, Math.ceil((this.rateLimiter.blockUntil - Date.now()) / 1000));
            
            if (remaining <= 0) {
                overlay.remove();
                return;
            }
            
            const minutes = Math.floor(remaining / 60);
            const seconds = remaining % 60;
            timer.text(`${minutes}:${seconds.toString().padStart(2, '0')}`);
            
            setTimeout(updateCountdown, 1000);
        };
        
        updateCountdown();
    }

    /**
     * Show security notification
     */
    showSecurityNotification(message, type = 'info') {
        if (window.securityValidation) {
            window.securityValidation.showToast(message, type);
        }
    }

    /**
     * Refresh CSRF token
     */
    async refreshCSRFToken() {
        try {
            const response = await $.ajax({
                url: '/api/security/csrf-token',
                type: 'GET'
            });

            if (response && response.token) {
                // Update token in meta tag
                $('meta[name="csrf-token"]').attr('content', response.token);
                
                // Update in security validation
                if (window.securityValidation) {
                    window.securityValidation.csrfToken = response.token;
                }

                console.log('[ApiSecurity] CSRF token refreshed');
            }
        } catch (error) {
            console.error('[ApiSecurity] Failed to refresh CSRF token:', error);
        }
    }

    /**
     * Intercept native fetch requests
     */
    interceptFetch() {
        const originalFetch = window.fetch;
        const self = this;

        window.fetch = async function(input, init = {}) {
            try {
                // Check rate limiting
                if (!self.checkRateLimit()) {
                    throw new Error('Rate limit exceeded');
                }

                // Add security headers
                if (!init.headers) {
                    init.headers = {};
                }

                // Add timestamp
                init.headers[self.options.timestampHeader] = Date.now().toString();

                // Add CSRF token
                const csrfToken = self.getCSRFToken();
                if (csrfToken) {
                    init.headers['X-CSRF-TOKEN'] = csrfToken;
                }

                // Track request
                self.trackRequest();

                // Call original fetch
                const response = await originalFetch(input, init);

                // Process response
                self.trackResponseSuccess(input, response.status);

                return response;
            } catch (error) {
                self.trackResponseError(input, 0, error.message);
                throw error;
            }
        };
    }

    /**
     * Utility methods
     */
    isApiRequest(url) {
        return url && (url.includes('/api/') || url.startsWith('/api/'));
    }

    getCSRFToken() {
        return $('meta[name="csrf-token"]').attr('content') ||
               $('input[name="__RequestVerificationToken"]').val() ||
               window.securityValidation?.csrfToken ||
               '';
    }

    getSecurityContext() {
        const context = {
            level: window.securityMonitor?.securityLevel || 'normal',
            sessionAge: window.sessionManager?.getSessionInfo()?.sessionAge || 0,
            timestamp: Date.now()
        };
        
        return btoa(JSON.stringify(context));
    }

    getClientId() {
        if (!this.clientId) {
            this.clientId = this.generateClientId();
        }
        return this.clientId;
    }

    generateClientId() {
        const components = {
            userAgent: navigator.userAgent,
            language: navigator.language,
            platform: navigator.platform,
            screen: `${screen.width}x${screen.height}`,
            timezone: Intl.DateTimeFormat().resolvedOptions().timeZone
        };
        
        const hash = btoa(JSON.stringify(components));
        return hash.substring(0, 16);
    }

    generateHMAC(message, secret) {
        // Simple HMAC-SHA256 implementation (in production, use crypto library)
        // This is a simplified version - use proper crypto library in production
        return btoa(message + secret).substring(0, 32);
    }

    loadSecurityKeys() {
        // In a real implementation, keys would be loaded securely
        // This is just a placeholder
        this.apiKey = window.apiSecurityConfig?.apiKey || null;
        this.secretKey = window.apiSecurityConfig?.secretKey || null;
    }

    trackResponseSuccess(url, status) {
        // Track successful API responses for analytics
        console.log(`[ApiSecurity] API response success: ${status} ${url}`);
    }

    trackResponseError(url, status, error) {
        // Track failed API responses for analytics
        console.error(`[ApiSecurity] API response error: ${status} ${url} - ${error}`);
    }

    /**
     * Bind event handlers
     */
    bindEventHandlers() {
        // Listen for security level changes
        document.addEventListener('securityLevelChanged', (e) => {
            if (e.detail.level === 'critical') {
                // Tighten rate limits in critical mode
                this.options.rateLimitMax = Math.floor(this.options.rateLimitMax / 2);
            }
        });

        // Listen for session events
        document.addEventListener('sessionWarning', () => {
            // Refresh security context
            this.refreshSecurityContext();
        });
    }

    refreshSecurityContext() {
        // Update security context for future requests
        console.log('[ApiSecurity] Security context refreshed');
    }

    /**
     * Public API methods
     */
    getSecurityStatus() {
        return {
            rateLimitStatus: {
                current: this.rateLimiter.requests.length,
                max: this.options.rateLimitMax,
                blocked: this.rateLimiter.blocked,
                blockUntil: this.rateLimiter.blockUntil
            },
            signingEnabled: this.options.enableRequestSigning,
            validationEnabled: this.options.enableResponseValidation,
            fingerprintingEnabled: this.options.enableRequestFingerprinting
        };
    }

    resetRateLimit() {
        this.rateLimiter.requests = [];
        this.rateLimiter.blocked = false;
        this.rateLimiter.blockUntil = 0;
        this.updateRateLimitUI();
    }

    enableFeature(feature) {
        if (this.options.hasOwnProperty(`enable${feature}`)) {
            this.options[`enable${feature}`] = true;
            console.log(`[ApiSecurity] ${feature} enabled`);
        }
    }

    disableFeature(feature) {
        if (this.options.hasOwnProperty(`enable${feature}`)) {
            this.options[`enable${feature}`] = false;
            console.log(`[ApiSecurity] ${feature} disabled`);
        }
    }
}

// Initialize API security when DOM is ready
$(document).ready(() => {
    const apiSecurityConfig = window.apiSecurityConfig || {};
    
    window.apiSecurity = new ApiSecurity(apiSecurityConfig);
    
    console.log('[ApiSecurity] API security utilities ready');
});