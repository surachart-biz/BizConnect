/**
 * BizConnect Security Validation System
 * Enterprise-grade client-side security validation and monitoring
 * 
 * Features:
 * - OTAC input sanitization and validation
 * - CSRF token management
 * - Session timeout warnings
 * - Security event logging
 * - Rate limiting feedback
 */

class SecurityValidation {
    constructor() {
        this.sessionTimeoutWarning = 25; // Warning at 25 minutes (5 min before 30 min timeout)
        this.sessionTimeout = 30; // Full timeout at 30 minutes
        this.otacAttempts = 0;
        this.maxOtacAttempts = 5;
        this.warningTimer = null;
        this.timeoutTimer = null;
        this.sessionStartTime = Date.now();
        this.csrfToken = null;
        
        this.init();
    }

    /**
     * Initialize security validation system
     */
    init() {
        this.initCSRFProtection();
        this.initSessionManagement();
        this.initOtacValidation();
        this.bindEventHandlers();
        
        console.log('[Security] BizConnect Security Validation System initialized');
    }

    /**
     * Initialize CSRF protection for all forms and AJAX requests
     */
    initCSRFProtection() {
        // Get CSRF token from meta tag or hidden input
        this.csrfToken = this.getCSRFToken();
        
        if (!this.csrfToken) {
            console.warn('[Security] CSRF token not found. Some requests may fail.');
            return;
        }

        // Add CSRF token to all AJAX requests
        $.ajaxSetup({
            beforeSend: (xhr, settings) => {
                if (settings.type && settings.type.toUpperCase() !== 'GET') {
                    xhr.setRequestHeader('X-CSRF-TOKEN', this.csrfToken);
                }
            }
        });

        // Add CSRF tokens to forms that don't have them
        this.addCSRFTokensToForms();
        
        console.log('[Security] CSRF protection initialized');
    }

    /**
     * Get CSRF token from various sources
     */
    getCSRFToken() {
        // Try meta tag first
        const metaToken = $('meta[name="__RequestVerificationToken"]').attr('content');
        if (metaToken) return metaToken;

        // Try hidden input
        const inputToken = $('input[name="__RequestVerificationToken"]').val();
        if (inputToken) return inputToken;

        // Try cookie (if configured)
        const cookieToken = this.getCookie('BizConnect.Antiforgery');
        if (cookieToken) return cookieToken;

        return null;
    }

    /**
     * Add CSRF tokens to forms missing them
     */
    addCSRFTokensToForms() {
        $('form[method="POST"], form[method="post"]').each((index, form) => {
            const $form = $(form);
            if (!$form.find('input[name="__RequestVerificationToken"]').length) {
                $form.append(`<input type="hidden" name="__RequestVerificationToken" value="${this.csrfToken}">`);
            }
        });
    }

    /**
     * Initialize session management with timeout warnings
     */
    initSessionManagement() {
        this.startSessionTimer();
        this.bindActivityHandlers();
        
        console.log('[Security] Session management initialized');
    }

    /**
     * Start session timeout timers
     */
    startSessionTimer() {
        this.clearTimers();
        
        // Warning timer (25 minutes)
        this.warningTimer = setTimeout(() => {
            this.showSessionWarning();
        }, this.sessionTimeoutWarning * 60 * 1000);

        // Timeout timer (30 minutes)
        this.timeoutTimer = setTimeout(() => {
            this.handleSessionTimeout();
        }, this.sessionTimeout * 60 * 1000);
    }

    /**
     * Reset session timers on user activity
     */
    resetSessionTimer() {
        this.sessionStartTime = Date.now();
        this.startSessionTimer();
        this.hideSessionWarning();
    }

    /**
     * Show session timeout warning
     */
    showSessionWarning() {
        const remainingTime = this.sessionTimeout - this.sessionTimeoutWarning;
        
        const warningHtml = `
            <div class="alert alert-warning alert-dismissible session-timeout-warning" role="alert">
                <i class="fas fa-clock me-2"></i>
                <strong>Session Expiring Soon!</strong>
                <p>Your session will expire in ${remainingTime} minutes due to inactivity.</p>
                <button type="button" class="btn btn-sm btn-primary me-2" onclick="securityValidation.extendSession()">
                    <i class="fas fa-refresh me-1"></i>Extend Session
                </button>
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            </div>
        `;
        
        // Show warning at top of page
        if (!$('.session-timeout-warning').length) {
            $('body').prepend(warningHtml);
        }
        
        console.log('[Security] Session timeout warning displayed');
    }

    /**
     * Hide session timeout warning
     */
    hideSessionWarning() {
        $('.session-timeout-warning').remove();
    }

    /**
     * Handle session timeout
     */
    handleSessionTimeout() {
        console.log('[Security] Session timed out - redirecting to login');
        
        // Show timeout message
        const timeoutHtml = `
            <div class="modal fade" id="sessionTimeoutModal" tabindex="-1" data-bs-backdrop="static" data-bs-keyboard="false">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header bg-danger text-white">
                            <h5 class="modal-title"><i class="fas fa-clock me-2"></i>Session Expired</h5>
                        </div>
                        <div class="modal-body text-center">
                            <i class="fas fa-exclamation-triangle text-warning fa-3x mb-3"></i>
                            <p>Your session has expired due to inactivity.</p>
                            <p>You will be redirected to the login page for security.</p>
                        </div>
                        <div class="modal-footer justify-content-center">
                            <button type="button" class="btn btn-primary" onclick="securityValidation.redirectToLogin()">
                                <i class="fas fa-sign-in-alt me-1"></i>Go to Login
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        $('body').append(timeoutHtml);
        const modal = new bootstrap.Modal(document.getElementById('sessionTimeoutModal'));
        modal.show();
        
        // Auto-redirect after 10 seconds
        setTimeout(() => {
            this.redirectToLogin();
        }, 10000);
    }

    /**
     * Extend session by making a keep-alive request
     */
    extendSession() {
        $.ajax({
            url: '/api/session/extend',
            type: 'POST',
            headers: {
                'X-CSRF-TOKEN': this.csrfToken
            },
            success: () => {
                this.resetSessionTimer();
                this.showToast('Session extended successfully', 'success');
                console.log('[Security] Session extended successfully');
            },
            error: () => {
                console.error('[Security] Failed to extend session');
                this.handleSessionTimeout();
            }
        });
    }

    /**
     * Redirect to login page
     */
    redirectToLogin() {
        window.location.href = '/Account/Login';
    }

    /**
     * Initialize OTAC input validation
     */
    initOtacValidation() {
        this.bindOtacInputHandlers();
        console.log('[Security] OTAC validation initialized');
    }

    /**
     * Bind OTAC input handlers
     */
    bindOtacInputHandlers() {
        $(document).on('input', 'input[data-otac="true"], .otac-input', (e) => {
            this.validateOtacInput(e.target);
        });

        $(document).on('paste', 'input[data-otac="true"], .otac-input', (e) => {
            setTimeout(() => this.validateOtacInput(e.target), 10);
        });
    }

    /**
     * Validate and sanitize OTAC input
     */
    validateOtacInput(input) {
        const $input = $(input);
        let value = $input.val().toUpperCase();
        
        // Remove non-alphanumeric characters
        value = value.replace(/[^A-Z0-9]/g, '');
        
        // Limit to 8 characters
        if (value.length > 8) {
            value = value.substring(0, 8);
        }
        
        $input.val(value);
        
        // Update security indicators
        this.updateOtacSecurityIndicators($input, value);
        
        // Log security event
        console.log(`[Security] OTAC input validated: ${value.length}/8 characters`);
    }

    /**
     * Update OTAC security indicators
     */
    updateOtacSecurityIndicators($input, value) {
        const container = $input.closest('.otac-container, .form-group');
        
        // Remove existing indicators
        container.find('.security-indicator').remove();
        
        // Add security indicator
        const isValid = value.length === 8;
        const indicator = `
            <div class="security-indicator ${isValid ? 'valid' : 'incomplete'} mt-1">
                <small>
                    <i class="fas ${isValid ? 'fa-check-circle text-success' : 'fa-info-circle text-muted'}"></i>
                    ${value.length}/8 characters (uppercase letters and numbers only)
                    ${this.otacAttempts > 0 ? `<span class="text-warning ms-2">Attempts: ${this.otacAttempts}/${this.maxOtacAttempts}</span>` : ''}
                </small>
            </div>
        `;
        
        $input.after(indicator);
    }

    /**
     * Increment OTAC attempt counter
     */
    incrementOtacAttempts() {
        this.otacAttempts++;
        
        if (this.otacAttempts >= this.maxOtacAttempts) {
            this.handleOtacLockout();
        }
        
        // Update all OTAC indicators
        $('.otac-input, input[data-otac="true"]').each((index, input) => {
            this.updateOtacSecurityIndicators($(input), $(input).val());
        });
        
        console.log(`[Security] OTAC attempt ${this.otacAttempts}/${this.maxOtacAttempts}`);
    }

    /**
     * Handle OTAC lockout
     */
    handleOtacLockout() {
        const lockoutMessage = `
            <div class="alert alert-danger" role="alert">
                <i class="fas fa-lock me-2"></i>
                <strong>Account Temporarily Locked</strong>
                <p>Too many invalid OTAC attempts. Please wait 15 minutes before trying again.</p>
            </div>
        `;
        
        $('.otac-container, .otac-form').prepend(lockoutMessage);
        $('.otac-input, input[data-otac="true"]').prop('disabled', true);
        
        console.error('[Security] OTAC lockout triggered');
    }

    /**
     * Bind activity handlers for session management
     */
    bindActivityHandlers() {
        const activityEvents = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart', 'click'];
        
        activityEvents.forEach(event => {
            $(document).on(event, () => {
                if (Date.now() - this.sessionStartTime > 60000) { // Reset only if more than 1 minute since last activity
                    this.resetSessionTimer();
                }
            });
        });
    }

    /**
     * Bind general event handlers
     */
    bindEventHandlers() {
        // Handle form submissions with security validation
        $(document).on('submit', 'form', (e) => {
            const $form = $(e.target);
            
            // Validate CSRF token presence
            if ($form.attr('method')?.toLowerCase() === 'post' && !this.hasValidCSRFToken($form)) {
                e.preventDefault();
                this.showToast('Security validation failed. Please refresh the page.', 'error');
                return false;
            }
            
            // Additional security checks
            if (!this.validateFormSecurity($form)) {
                e.preventDefault();
                return false;
            }
        });

        // Handle page visibility changes
        $(document).on('visibilitychange', () => {
            if (document.hidden) {
                console.log('[Security] Page hidden - pausing timers');
                this.clearTimers();
            } else {
                console.log('[Security] Page visible - resuming timers');
                this.startSessionTimer();
            }
        });
    }

    /**
     * Validate form security
     */
    validateFormSecurity($form) {
        // Check for suspicious form modifications
        const requiredFields = $form.find('[required]');
        for (let field of requiredFields) {
            if (!$(field).val() && $(field).is(':visible')) {
                this.showToast('Please fill in all required fields', 'warning');
                return false;
            }
        }
        
        return true;
    }

    /**
     * Check if form has valid CSRF token
     */
    hasValidCSRFToken($form) {
        const token = $form.find('input[name="__RequestVerificationToken"]').val();
        return token && token.length > 0;
    }

    /**
     * Clear all timers
     */
    clearTimers() {
        if (this.warningTimer) {
            clearTimeout(this.warningTimer);
            this.warningTimer = null;
        }
        if (this.timeoutTimer) {
            clearTimeout(this.timeoutTimer);
            this.timeoutTimer = null;
        }
    }

    /**
     * Get cookie value by name
     */
    getCookie(name) {
        const value = `; ${document.cookie}`;
        const parts = value.split(`; ${name}=`);
        if (parts.length === 2) return parts.pop().split(';').shift();
        return null;
    }

    /**
     * Show toast notification
     */
    showToast(message, type = 'info') {
        const toastId = `toast-${Date.now()}`;
        const bgClass = {
            'success': 'bg-success',
            'error': 'bg-danger',
            'warning': 'bg-warning',
            'info': 'bg-info'
        }[type] || 'bg-info';
        
        const toastHtml = `
            <div class="toast ${bgClass} text-white" id="${toastId}" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="toast-header ${bgClass} text-white border-0">
                    <i class="fas fa-shield-alt me-2"></i>
                    <strong class="me-auto">Security</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;
        
        // Create toast container if it doesn't exist
        if (!$('.toast-container').length) {
            $('body').append('<div class="toast-container position-fixed top-0 end-0 p-3"></div>');
        }
        
        $('.toast-container').append(toastHtml);
        const toast = new bootstrap.Toast(document.getElementById(toastId));
        toast.show();
        
        // Auto-remove after hide
        $(`#${toastId}`).on('hidden.bs.toast', function() {
            $(this).remove();
        });
    }

    /**
     * Log security event
     */
    logSecurityEvent(eventType, details) {
        const event = {
            type: eventType,
            timestamp: new Date().toISOString(),
            details: details,
            userAgent: navigator.userAgent,
            url: window.location.href
        };
        
        console.log(`[Security] ${eventType}:`, event);
        
        // Send to server if endpoint exists
        if (typeof window.logSecurityEvent === 'function') {
            window.logSecurityEvent(event);
        }
    }

    /**
     * Get security status
     */
    getSecurityStatus() {
        const sessionAge = (Date.now() - this.sessionStartTime) / 1000 / 60; // minutes
        const sessionHealthy = sessionAge < this.sessionTimeoutWarning;
        
        return {
            csrfProtected: !!this.csrfToken,
            sessionHealthy: sessionHealthy,
            sessionAge: Math.round(sessionAge),
            otacAttempts: this.otacAttempts,
            otacLocked: this.otacAttempts >= this.maxOtacAttempts,
            timestamp: new Date().toISOString()
        };
    }
}

// Initialize security validation when DOM is ready
$(document).ready(() => {
    window.securityValidation = new SecurityValidation();
    
    // Global error handler for AJAX requests
    $(document).ajaxError((event, xhr, settings) => {
        if (xhr.status === 401) {
            console.error('[Security] Unauthorized request detected');
            window.securityValidation.handleSessionTimeout();
        } else if (xhr.status === 403) {
            console.error('[Security] Forbidden request detected');
            window.securityValidation.showToast('Access denied. Please check your permissions.', 'error');
        } else if (xhr.status === 419) {
            console.error('[Security] CSRF token mismatch');
            window.securityValidation.showToast('Security token expired. Please refresh the page.', 'error');
        }
    });
});