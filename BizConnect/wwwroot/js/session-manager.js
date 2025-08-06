/**
 * BizConnect Session Management System
 * Advanced session handling with timeout warnings, auto-refresh, and security integration
 * 
 * Features:
 * - Session timeout detection and warnings
 * - Auto-refresh mechanisms
 * - Activity-based session extension
 * - Secure session state management
 * - Integration with security monitoring
 */

class SessionManager {
    constructor(options = {}) {
        this.options = {
            sessionTimeout: 30, // minutes
            warningTime: 25, // minutes (warning starts at 25 min)
            extendSessionUrl: '/api/session/extend',
            logoutUrl: '/Account/Logout',
            checkInterval: 30, // seconds
            activityThreshold: 60, // seconds - minimum time between activity resets
            maxExtensions: 3, // maximum automatic extensions per session
            enableAutoRefresh: true,
            enableActivityTracking: true,
            enableSecurityIntegration: true,
            ...options
        };

        this.state = {
            sessionStart: Date.now(),
            lastActivity: Date.now(),
            lastExtension: 0,
            extensionCount: 0,
            warningShown: false,
            timeoutWarningActive: false,
            isActive: true,
            visibilityState: 'visible'
        };

        this.timers = {
            warning: null,
            timeout: null,
            activityCheck: null,
            heartbeat: null
        };

        this.activityEvents = [
            'mousedown', 'mousemove', 'keypress', 'keydown',
            'click', 'scroll', 'touchstart', 'touchmove',
            'focus', 'blur'
        ];

        this.init();
    }

    /**
     * Initialize session management
     */
    init() {
        this.bindEventHandlers();
        this.startSessionTimers();
        this.initializeHeartbeat();
        
        if (this.options.enableSecurityIntegration) {
            this.integrateWithSecuritySystem();
        }

        console.log('[SessionManager] Advanced session management initialized');
        console.log(`[SessionManager] Session timeout: ${this.options.sessionTimeout} minutes`);
        console.log(`[SessionManager] Warning time: ${this.options.warningTime} minutes`);
    }

    /**
     * Bind event handlers for activity tracking and session management
     */
    bindEventHandlers() {
        if (this.options.enableActivityTracking) {
            this.bindActivityHandlers();
        }

        // Handle page visibility changes
        document.addEventListener('visibilitychange', () => {
            this.handleVisibilityChange();
        });

        // Handle beforeunload for cleanup
        window.addEventListener('beforeunload', () => {
            this.cleanup();
        });

        // Handle storage events for multi-tab session management
        window.addEventListener('storage', (e) => {
            if (e.key === 'bizconnect_session_activity') {
                this.handleMultiTabActivity(e);
            }
        });

        // Handle custom session events
        document.addEventListener('sessionExtended', (e) => {
            this.handleSessionExtended(e.detail);
        });

        document.addEventListener('sessionWarning', (e) => {
            this.handleSessionWarning(e.detail);
        });
    }

    /**
     * Bind activity event handlers
     */
    bindActivityHandlers() {
        const throttledActivityHandler = this.throttle(() => {
            this.recordActivity();
        }, 1000); // Throttle to once per second

        this.activityEvents.forEach(event => {
            document.addEventListener(event, throttledActivityHandler, { passive: true });
        });

        // AJAX activity tracking
        $(document).ajaxComplete(() => {
            this.recordActivity('ajax_request');
        });

        // Form interaction tracking
        $(document).on('input change', 'input, textarea, select', () => {
            this.recordActivity('form_interaction');
        });
    }

    /**
     * Start session timeout timers
     */
    startSessionTimers() {
        this.clearTimers();

        const now = Date.now();
        const warningTime = this.options.warningTime * 60 * 1000;
        const timeoutTime = this.options.sessionTimeout * 60 * 1000;
        
        // Calculate time remaining
        const sessionAge = now - this.state.sessionStart;
        const timeToWarning = Math.max(0, warningTime - sessionAge);
        const timeToTimeout = Math.max(0, timeoutTime - sessionAge);

        // Set warning timer
        if (timeToWarning > 0 && !this.state.warningShown) {
            this.timers.warning = setTimeout(() => {
                this.showTimeoutWarning();
            }, timeToWarning);
        }

        // Set timeout timer
        if (timeToTimeout > 0) {
            this.timers.timeout = setTimeout(() => {
                this.handleSessionTimeout();
            }, timeToTimeout);
        }

        // Start activity check timer
        this.timers.activityCheck = setInterval(() => {
            this.performActivityCheck();
        }, this.options.checkInterval * 1000);

        console.log(`[SessionManager] Timers started - Warning in: ${Math.round(timeToWarning/1000/60)} min, Timeout in: ${Math.round(timeToTimeout/1000/60)} min`);
    }

    /**
     * Initialize heartbeat mechanism
     */
    initializeHeartbeat() {
        // Send heartbeat every 5 minutes to maintain server session
        this.timers.heartbeat = setInterval(() => {
            this.sendHeartbeat();
        }, 5 * 60 * 1000); // 5 minutes
    }

    /**
     * Record user activity
     */
    recordActivity(activityType = 'user_interaction') {
        const now = Date.now();
        
        // Check if enough time has passed since last activity record
        if (now - this.state.lastActivity < (this.options.activityThreshold * 1000)) {
            return;
        }

        const wasExpired = this.isSessionExpired();
        this.state.lastActivity = now;

        // Update multi-tab activity tracking
        this.updateMultiTabActivity();

        // Reset timers if session was about to expire
        if (this.state.timeoutWarningActive || wasExpired) {
            this.resetSessionTimers();
            this.hideTimeoutWarning();
        }

        // Log activity for security monitoring
        this.logActivity(activityType);

        console.log(`[SessionManager] Activity recorded: ${activityType}`);
    }

    /**
     * Reset session timers due to activity
     */
    resetSessionTimers() {
        this.state.sessionStart = Date.now();
        this.state.warningShown = false;
        this.state.timeoutWarningActive = false;
        
        this.startSessionTimers();
        
        console.log('[SessionManager] Session timers reset due to activity');
    }

    /**
     * Show session timeout warning with countdown
     */
    showTimeoutWarning() {
        if (this.state.timeoutWarningActive) {
            return; // Warning already shown
        }

        this.state.warningShown = true;
        this.state.timeoutWarningActive = true;

        const remainingTime = this.options.sessionTimeout - this.options.warningTime;
        
        const warningHtml = `
            <div class="modal fade session-timeout-modal" id="sessionTimeoutWarning" tabindex="-1" 
                 data-bs-backdrop="static" data-bs-keyboard="false">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content border-warning">
                        <div class="modal-header bg-warning text-dark">
                            <h5 class="modal-title">
                                <i class="fas fa-clock me-2"></i>Session Expiring Soon
                            </h5>
                        </div>
                        <div class="modal-body text-center">
                            <div class="session-warning-content">
                                <i class="fas fa-hourglass-half text-warning fa-3x mb-3"></i>
                                <h6>Your session will expire in:</h6>
                                <div class="countdown-display mb-3">
                                    <span class="countdown-time display-6 text-warning fw-bold" id="sessionCountdown">
                                        ${remainingTime}:00
                                    </span>
                                </div>
                                <p class="text-muted">You will be automatically logged out for security.</p>
                                
                                <div class="session-actions mt-3">
                                    <button type="button" class="btn btn-primary me-2" id="extendSessionBtn">
                                        <i class="fas fa-refresh me-1"></i>Stay Logged In
                                    </button>
                                    <button type="button" class="btn btn-outline-secondary" id="logoutNowBtn">
                                        <i class="fas fa-sign-out-alt me-1"></i>Logout Now
                                    </button>
                                </div>

                                <div class="auto-extend-option mt-3">
                                    <div class="form-check">
                                        <input class="form-check-input" type="checkbox" id="autoExtendCheckbox">
                                        <label class="form-check-label text-muted small" for="autoExtendCheckbox">
                                            Automatically extend session when active (max ${this.options.maxExtensions} times)
                                        </label>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing warning if any
        $('.session-timeout-modal').remove();
        
        $('body').append(warningHtml);
        const modal = new bootstrap.Modal(document.getElementById('sessionTimeoutWarning'));
        modal.show();

        // Bind button handlers
        $('#extendSessionBtn').on('click', () => {
            this.extendSession();
        });

        $('#logoutNowBtn').on('click', () => {
            this.logout();
        });

        // Start countdown
        this.startTimeoutCountdown(remainingTime * 60);

        // Track security event
        if (this.options.enableSecurityIntegration && window.securityMonitor) {
            window.securityMonitor.trackEvent('session_timeout_warning', {
                remainingTime: remainingTime,
                extensionCount: this.state.extensionCount,
                sessionAge: (Date.now() - this.state.sessionStart) / 1000 / 60
            });
        }

        console.log(`[SessionManager] Timeout warning shown - ${remainingTime} minutes remaining`);
    }

    /**
     * Start countdown timer in the warning modal
     */
    startTimeoutCountdown(seconds) {
        const countdownElement = $('#sessionCountdown');
        let remainingSeconds = seconds;

        const updateCountdown = () => {
            if (remainingSeconds <= 0) {
                this.handleSessionTimeout();
                return;
            }

            const minutes = Math.floor(remainingSeconds / 60);
            const secs = remainingSeconds % 60;
            countdownElement.text(`${minutes}:${secs.toString().padStart(2, '0')}`);

            // Change color as time runs out
            if (remainingSeconds <= 60) {
                countdownElement.removeClass('text-warning').addClass('text-danger');
            } else if (remainingSeconds <= 180) {
                countdownElement.removeClass('text-warning text-success').addClass('text-warning');
            }

            remainingSeconds--;
            setTimeout(updateCountdown, 1000);
        };

        updateCountdown();
    }

    /**
     * Hide timeout warning
     */
    hideTimeoutWarning() {
        $('.session-timeout-modal').modal('hide').remove();
        this.state.timeoutWarningActive = false;
        
        console.log('[SessionManager] Timeout warning hidden');
    }

    /**
     * Extend session
     */
    async extendSession() {
        try {
            const response = await $.ajax({
                url: this.options.extendSessionUrl,
                type: 'POST',
                headers: {
                    'X-CSRF-TOKEN': this.getCSRFToken()
                },
                dataType: 'json'
            });

            if (response && response.success) {
                this.state.extensionCount++;
                this.state.lastExtension = Date.now();
                
                this.resetSessionTimers();
                this.hideTimeoutWarning();
                
                this.showToast('Session extended successfully', 'success');
                
                // Dispatch custom event
                document.dispatchEvent(new CustomEvent('sessionExtended', {
                    detail: {
                        extensionCount: this.state.extensionCount,
                        timestamp: Date.now()
                    }
                }));

                console.log('[SessionManager] Session extended successfully');
            } else {
                throw new Error('Session extension failed');
            }
        } catch (error) {
            console.error('[SessionManager] Failed to extend session:', error);
            this.showToast('Failed to extend session. You may need to log in again.', 'error');
            
            // If extension fails, redirect to login
            setTimeout(() => {
                this.logout();
            }, 3000);
        }
    }

    /**
     * Handle session timeout
     */
    handleSessionTimeout() {
        console.log('[SessionManager] Session timed out - initiating logout');
        
        this.clearTimers();
        this.state.isActive = false;

        // Show timeout message
        const timeoutHtml = `
            <div class="modal fade session-timeout-modal" id="sessionTimeoutModal" tabindex="-1" 
                 data-bs-backdrop="static" data-bs-keyboard="false">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content border-danger">
                        <div class="modal-header bg-danger text-white">
                            <h5 class="modal-title">
                                <i class="fas fa-exclamation-triangle me-2"></i>Session Expired
                            </h5>
                        </div>
                        <div class="modal-body text-center">
                            <i class="fas fa-clock text-danger fa-4x mb-3"></i>
                            <h6>Your session has expired</h6>
                            <p>You have been logged out for security reasons.</p>
                            <p class="text-muted">You will be redirected to the login page in <span id="redirectCountdown">10</span> seconds.</p>
                        </div>
                        <div class="modal-footer justify-content-center">
                            <button type="button" class="btn btn-danger" onclick="sessionManager.logout()">
                                <i class="fas fa-sign-in-alt me-1"></i>Go to Login
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        $('.session-timeout-modal').remove();
        $('body').append(timeoutHtml);
        
        const modal = new bootstrap.Modal(document.getElementById('sessionTimeoutModal'));
        modal.show();

        // Start redirect countdown
        let countdown = 10;
        const countdownTimer = setInterval(() => {
            countdown--;
            $('#redirectCountdown').text(countdown);
            
            if (countdown <= 0) {
                clearInterval(countdownTimer);
                this.logout();
            }
        }, 1000);

        // Track security event
        if (this.options.enableSecurityIntegration && window.securityMonitor) {
            window.securityMonitor.trackEvent('session_timeout', {
                sessionDuration: (Date.now() - this.state.sessionStart) / 1000 / 60,
                extensionCount: this.state.extensionCount,
                lastActivity: (Date.now() - this.state.lastActivity) / 1000 / 60
            });
        }
    }

    /**
     * Perform periodic activity checks
     */
    performActivityCheck() {
        if (!this.state.isActive) {
            return;
        }

        const now = Date.now();
        const timeSinceActivity = (now - this.state.lastActivity) / 1000 / 60; // minutes
        
        // Check if we should auto-extend session
        if (this.shouldAutoExtendSession()) {
            this.extendSession();
            return;
        }

        // Update session status in UI
        this.updateSessionStatus();

        console.log(`[SessionManager] Activity check - Last activity: ${Math.round(timeSinceActivity)} minutes ago`);
    }

    /**
     * Check if session should be automatically extended
     */
    shouldAutoExtendSession() {
        const autoExtendEnabled = $('#autoExtendCheckbox').is(':checked');
        const hasRecentActivity = (Date.now() - this.state.lastActivity) < (5 * 60 * 1000); // 5 minutes
        const canExtend = this.state.extensionCount < this.options.maxExtensions;
        const nearTimeout = this.isNearTimeout();

        return autoExtendEnabled && hasRecentActivity && canExtend && nearTimeout;
    }

    /**
     * Check if session is near timeout
     */
    isNearTimeout() {
        const sessionAge = (Date.now() - this.state.sessionStart) / 1000 / 60;
        return sessionAge >= this.options.warningTime;
    }

    /**
     * Check if session has expired
     */
    isSessionExpired() {
        const sessionAge = (Date.now() - this.state.sessionStart) / 1000 / 60;
        return sessionAge >= this.options.sessionTimeout;
    }

    /**
     * Send heartbeat to maintain server session
     */
    async sendHeartbeat() {
        if (!this.state.isActive) {
            return;
        }

        try {
            await $.ajax({
                url: '/api/session/heartbeat',
                type: 'POST',
                headers: {
                    'X-CSRF-TOKEN': this.getCSRFToken()
                },
                timeout: 5000
            });

            console.log('[SessionManager] Heartbeat sent successfully');
        } catch (error) {
            console.warn('[SessionManager] Heartbeat failed:', error);
            
            // If heartbeat fails, it might indicate session is invalid
            if (error.status === 401) {
                this.handleSessionTimeout();
            }
        }
    }

    /**
     * Handle page visibility changes
     */
    handleVisibilityChange() {
        const isVisible = !document.hidden;
        this.state.visibilityState = isVisible ? 'visible' : 'hidden';

        if (isVisible) {
            console.log('[SessionManager] Page became visible - checking session status');
            
            // Check if session expired while page was hidden
            if (this.isSessionExpired()) {
                this.handleSessionTimeout();
            } else {
                this.recordActivity('page_visible');
            }
        } else {
            console.log('[SessionManager] Page became hidden');
        }
    }

    /**
     * Update multi-tab activity tracking
     */
    updateMultiTabActivity() {
        try {
            localStorage.setItem('bizconnect_session_activity', JSON.stringify({
                timestamp: Date.now(),
                tabId: this.getTabId()
            }));
        } catch (error) {
            console.warn('[SessionManager] Failed to update multi-tab activity:', error);
        }
    }

    /**
     * Handle multi-tab activity
     */
    handleMultiTabActivity(event) {
        try {
            const activityData = JSON.parse(event.newValue);
            if (activityData && activityData.tabId !== this.getTabId()) {
                // Activity in another tab - reset our timers
                this.recordActivity('multi_tab_activity');
            }
        } catch (error) {
            console.warn('[SessionManager] Failed to handle multi-tab activity:', error);
        }
    }

    /**
     * Get unique tab identifier
     */
    getTabId() {
        if (!window.sessionStorage.getItem('bizconnect_tab_id')) {
            window.sessionStorage.setItem('bizconnect_tab_id', `tab_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`);
        }
        return window.sessionStorage.getItem('bizconnect_tab_id');
    }

    /**
     * Update session status in UI
     */
    updateSessionStatus() {
        const sessionAge = (Date.now() - this.state.sessionStart) / 1000 / 60;
        const remainingTime = Math.max(0, this.options.sessionTimeout - sessionAge);

        // Update security monitor integration
        if (this.options.enableSecurityIntegration && window.securityMonitor) {
            window.securityMonitor.updateSessionStatus({
                sessionHealthy: remainingTime > 5,
                sessionAge: Math.round(sessionAge),
                remainingTime: Math.round(remainingTime)
            });
        }

        // Update any session status displays
        $('.session-time-remaining').text(`${Math.round(remainingTime)} min`);
        $('.session-age').text(`${Math.round(sessionAge)} min`);
    }

    /**
     * Integrate with security monitoring system
     */
    integrateWithSecuritySystem() {
        if (window.securityMonitor) {
            // Provide session status to security monitor
            window.securityMonitor.sessionManager = this;
            
            // Listen for security level changes
            document.addEventListener('securityLevelChanged', (e) => {
                if (e.detail.level === 'critical') {
                    // Reduce session timeout in critical security mode
                    this.options.sessionTimeout = 15;
                    this.options.warningTime = 12;
                    this.resetSessionTimers();
                }
            });
        }
    }

    /**
     * Log activity for security monitoring
     */
    logActivity(activityType) {
        if (this.options.enableSecurityIntegration && window.securityMonitor) {
            window.securityMonitor.trackEvent('session_activity', {
                activityType: activityType,
                sessionAge: (Date.now() - this.state.sessionStart) / 1000 / 60,
                timeSinceLastActivity: (Date.now() - this.state.lastActivity) / 1000,
                extensionCount: this.state.extensionCount
            });
        }
    }

    /**
     * Logout user
     */
    logout() {
        console.log('[SessionManager] Initiating logout');
        
        this.cleanup();
        
        // Clear session storage
        try {
            sessionStorage.removeItem('bizconnect_tab_id');
            localStorage.removeItem('bizconnect_session_activity');
        } catch (error) {
            console.warn('[SessionManager] Failed to clear storage:', error);
        }

        // Redirect to logout URL
        window.location.href = this.options.logoutUrl;
    }

    /**
     * Clean up timers and event listeners
     */
    cleanup() {
        this.clearTimers();
        this.state.isActive = false;
        
        // Clean up event listeners
        this.activityEvents.forEach(event => {
            document.removeEventListener(event, this.recordActivity);
        });

        console.log('[SessionManager] Cleanup completed');
    }

    /**
     * Clear all timers
     */
    clearTimers() {
        Object.values(this.timers).forEach(timer => {
            if (timer) {
                clearTimeout(timer);
                clearInterval(timer);
            }
        });
        
        this.timers = {
            warning: null,
            timeout: null,
            activityCheck: null,
            heartbeat: null
        };
    }

    /**
     * Utility methods
     */
    getCSRFToken() {
        return $('meta[name="csrf-token"]').attr('content') ||
               $('input[name="__RequestVerificationToken"]').val() ||
               window.securityValidation?.csrfToken ||
               '';
    }

    throttle(func, limit) {
        let inThrottle;
        return function(...args) {
            if (!inThrottle) {
                func.apply(this, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }

    showToast(message, type = 'info') {
        if (window.securityValidation) {
            window.securityValidation.showToast(message, type);
        } else {
            console.log(`[SessionManager] ${type.toUpperCase()}: ${message}`);
        }
    }

    /**
     * Handle session-related events
     */
    handleSessionExtended(detail) {
        console.log('[SessionManager] Session extended event received:', detail);
    }

    handleSessionWarning(detail) {
        console.log('[SessionManager] Session warning event received:', detail);
    }

    /**
     * Public API methods
     */
    getSessionInfo() {
        const sessionAge = (Date.now() - this.state.sessionStart) / 1000 / 60;
        const remainingTime = Math.max(0, this.options.sessionTimeout - sessionAge);
        
        return {
            sessionAge: Math.round(sessionAge),
            remainingTime: Math.round(remainingTime),
            extensionCount: this.state.extensionCount,
            maxExtensions: this.options.maxExtensions,
            lastActivity: new Date(this.state.lastActivity),
            isActive: this.state.isActive,
            warningActive: this.state.timeoutWarningActive
        };
    }

    forceExtendSession() {
        return this.extendSession();
    }

    forceLogout() {
        this.logout();
    }
}

// Initialize session manager when DOM is ready
$(document).ready(() => {
    // Get configuration from server-side rendered data
    const sessionConfig = window.sessionConfig || {};
    
    window.sessionManager = new SessionManager(sessionConfig);
    
    console.log('[SessionManager] Session management system ready');
});