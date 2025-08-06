/**
 * BizConnect Security Monitoring System
 * Real-time security event tracking and anomalous activity detection
 * 
 * Features:
 * - Failed attempt monitoring
 * - Anomalous activity detection
 * - Rate limiting visual feedback
 * - Security event aggregation
 * - Real-time threat assessment
 */

class SecurityMonitor {
    constructor() {
        this.failedAttempts = new Map(); // Track by type
        this.securityEvents = [];
        this.maxEventHistory = 100;
        this.rateLimit = {
            requests: 0,
            windowStart: Date.now(),
            windowSize: 60000, // 1 minute
            maxRequests: 60 // Max 60 requests per minute
        };
        this.anomalyThresholds = {
            rapidClicks: { count: 10, timeWindow: 5000 }, // 10 clicks in 5 seconds
            formResubmissions: { count: 5, timeWindow: 10000 }, // 5 form submissions in 10 seconds
            failedLogins: { count: 3, timeWindow: 300000 }, // 3 failed logins in 5 minutes
            suspiciousPatterns: { count: 3, timeWindow: 60000 } // 3 suspicious events in 1 minute
        };
        this.monitoringActive = true;
        this.securityLevel = 'normal'; // normal, elevated, high, critical
        
        this.init();
    }

    /**
     * Initialize security monitoring
     */
    init() {
        this.bindMonitoringHandlers();
        this.startPeriodicChecks();
        this.initializeSecurityWidgets();
        
        console.log('[SecurityMonitor] Real-time security monitoring initialized');
    }

    /**
     * Bind monitoring event handlers
     */
    bindMonitoringHandlers() {
        // Monitor login attempts
        $(document).on('submit', 'form[action*="/Account/Login"], form[action*="/login"]', (e) => {
            this.trackEvent('login_attempt', {
                form: e.target.action,
                timestamp: Date.now()
            });
        });

        // Monitor OTAC submissions
        $(document).on('submit', 'form[data-form-type="otac"], .otac-form', (e) => {
            this.trackEvent('otac_attempt', {
                form: e.target.action,
                timestamp: Date.now()
            });
        });

        // Monitor rapid clicking (potential bot behavior)
        let clickCount = 0;
        let clickTimer = null;
        $(document).on('click', (e) => {
            clickCount++;
            
            if (!clickTimer) {
                clickTimer = setTimeout(() => {
                    if (clickCount >= this.anomalyThresholds.rapidClicks.count) {
                        this.detectAnomaly('rapid_clicking', {
                            count: clickCount,
                            target: e.target.tagName,
                            timeWindow: this.anomalyThresholds.rapidClicks.timeWindow
                        });
                    }
                    clickCount = 0;
                    clickTimer = null;
                }, this.anomalyThresholds.rapidClicks.timeWindow);
            }
        });

        // Monitor form resubmissions
        const formSubmissions = new Map();
        $(document).on('submit', 'form', (e) => {
            const formId = e.target.id || e.target.action || 'unknown';
            const now = Date.now();
            
            if (!formSubmissions.has(formId)) {
                formSubmissions.set(formId, []);
            }
            
            const submissions = formSubmissions.get(formId);
            submissions.push(now);
            
            // Clean old submissions
            const cutoff = now - this.anomalyThresholds.formResubmissions.timeWindow;
            const recentSubmissions = submissions.filter(time => time > cutoff);
            formSubmissions.set(formId, recentSubmissions);
            
            if (recentSubmissions.length >= this.anomalyThresholds.formResubmissions.count) {
                this.detectAnomaly('rapid_form_submissions', {
                    formId: formId,
                    count: recentSubmissions.length,
                    timeWindow: this.anomalyThresholds.formResubmissions.timeWindow
                });
            }
        });

        // Monitor AJAX rate limiting
        $(document).ajaxSend((event, xhr, settings) => {
            this.checkRateLimit();
        });

        // Monitor security-sensitive page access
        $(document).ready(() => {
            if (this.isSensitivePage()) {
                this.trackEvent('sensitive_page_access', {
                    page: window.location.pathname,
                    timestamp: Date.now(),
                    referrer: document.referrer
                });
            }
        });

        // Monitor console access attempts (developer tools)
        this.monitorConsoleAccess();

        // Monitor copy/paste activities in sensitive forms
        $(document).on('paste', 'input[type="password"], .otac-input, input[data-sensitive="true"]', (e) => {
            this.trackEvent('sensitive_input_paste', {
                inputType: e.target.type || 'unknown',
                fieldName: e.target.name || 'unknown',
                timestamp: Date.now()
            });
        });
    }

    /**
     * Track security events
     */
    trackEvent(eventType, details) {
        const event = {
            id: this.generateEventId(),
            type: eventType,
            details: details,
            timestamp: Date.now(),
            url: window.location.href,
            userAgent: navigator.userAgent,
            securityLevel: this.securityLevel
        };

        this.securityEvents.push(event);
        
        // Maintain event history size
        if (this.securityEvents.length > this.maxEventHistory) {
            this.securityEvents.shift();
        }

        // Update failed attempts if this is a failure event
        if (eventType.includes('failed') || eventType.includes('invalid')) {
            this.trackFailedAttempt(eventType, details);
        }

        // Send to server for logging
        this.sendSecurityEvent(event);
        
        console.log(`[SecurityMonitor] Event tracked: ${eventType}`, details);
    }

    /**
     * Track failed attempts by type
     */
    trackFailedAttempt(attemptType, details) {
        if (!this.failedAttempts.has(attemptType)) {
            this.failedAttempts.set(attemptType, []);
        }

        const attempts = this.failedAttempts.get(attemptType);
        attempts.push({
            timestamp: Date.now(),
            details: details
        });

        // Clean old attempts (keep last 24 hours)
        const cutoff = Date.now() - (24 * 60 * 60 * 1000);
        const recentAttempts = attempts.filter(attempt => attempt.timestamp > cutoff);
        this.failedAttempts.set(attemptType, recentAttempts);

        // Check if we need to escalate security level
        this.assessSecurityLevel();
        
        // Update UI indicators
        this.updateSecurityWidgets();
    }

    /**
     * Detect anomalous activity patterns
     */
    detectAnomaly(anomalyType, details) {
        const anomaly = {
            id: this.generateEventId(),
            type: 'anomaly_detected',
            anomalyType: anomalyType,
            details: details,
            timestamp: Date.now(),
            securityLevel: this.securityLevel
        };

        this.trackEvent('anomaly_detected', anomaly);
        
        // Escalate security level
        this.escalateSecurityLevel('elevated');
        
        // Show warning to user
        this.showSecurityWarning(anomalyType, details);
        
        console.warn(`[SecurityMonitor] Anomaly detected: ${anomalyType}`, details);
    }

    /**
     * Check rate limiting
     */
    checkRateLimit() {
        const now = Date.now();
        
        // Reset window if needed
        if (now - this.rateLimit.windowStart > this.rateLimit.windowSize) {
            this.rateLimit.windowStart = now;
            this.rateLimit.requests = 0;
        }
        
        this.rateLimit.requests++;
        
        // Check if rate limit exceeded
        if (this.rateLimit.requests > this.rateLimit.maxRequests) {
            this.detectAnomaly('rate_limit_exceeded', {
                requests: this.rateLimit.requests,
                timeWindow: this.rateLimit.windowSize,
                maxRequests: this.rateLimit.maxRequests
            });
            
            return false; // Rate limit exceeded
        }
        
        // Update UI with rate limit status
        this.updateRateLimitIndicator();
        
        return true;
    }

    /**
     * Assess and update security level
     */
    assessSecurityLevel() {
        let score = 0;
        const now = Date.now();
        
        // Calculate threat score based on recent events
        this.securityEvents.forEach(event => {
            const age = now - event.timestamp;
            const weight = Math.max(0, 1 - (age / (60 * 60 * 1000))); // Weight decreases over 1 hour
            
            switch (event.type) {
                case 'anomaly_detected':
                    score += 10 * weight;
                    break;
                case 'failed_login':
                    score += 5 * weight;
                    break;
                case 'invalid_otac':
                    score += 3 * weight;
                    break;
                case 'rate_limit_exceeded':
                    score += 7 * weight;
                    break;
                case 'sensitive_input_paste':
                    score += 1 * weight;
                    break;
            }
        });

        // Determine security level based on score
        let newLevel = 'normal';
        if (score >= 50) {
            newLevel = 'critical';
        } else if (score >= 25) {
            newLevel = 'high';
        } else if (score >= 10) {
            newLevel = 'elevated';
        }

        if (newLevel !== this.securityLevel) {
            this.escalateSecurityLevel(newLevel);
        }
    }

    /**
     * Escalate security level
     */
    escalateSecurityLevel(newLevel) {
        const oldLevel = this.securityLevel;
        this.securityLevel = newLevel;
        
        console.log(`[SecurityMonitor] Security level changed: ${oldLevel} → ${newLevel}`);
        
        // Update UI indicators
        this.updateSecurityLevelIndicator();
        
        // Apply security measures based on level
        this.applySecurityMeasures(newLevel);
        
        // Track the escalation
        this.trackEvent('security_level_change', {
            oldLevel: oldLevel,
            newLevel: newLevel,
            timestamp: Date.now()
        });
    }

    /**
     * Apply security measures based on level
     */
    applySecurityMeasures(level) {
        switch (level) {
            case 'elevated':
                // Enable additional validation
                this.enableEnhancedValidation();
                break;
            case 'high':
                // Reduce session timeout
                this.reduceSessionTimeout(15); // 15 minutes instead of 30
                // Enable additional logging
                this.enableVerboseLogging();
                break;
            case 'critical':
                // Lock sensitive functions
                this.lockSensitiveFunctions();
                // Show security alert
                this.showCriticalSecurityAlert();
                break;
        }
    }

    /**
     * Start periodic security checks
     */
    startPeriodicChecks() {
        // Check every 30 seconds
        setInterval(() => {
            this.performSecurityCheck();
        }, 30000);
        
        // Clean up old events every 5 minutes
        setInterval(() => {
            this.cleanupOldEvents();
        }, 300000);
    }

    /**
     * Perform periodic security assessment
     */
    performSecurityCheck() {
        this.assessSecurityLevel();
        this.checkForSuspiciousPatterns();
        this.updateSecurityWidgets();
        
        // Log health status
        console.log(`[SecurityMonitor] Security check completed - Level: ${this.securityLevel}`);
    }

    /**
     * Check for suspicious patterns
     */
    checkForSuspiciousPatterns() {
        const now = Date.now();
        const recentEvents = this.securityEvents.filter(event => 
            now - event.timestamp < this.anomalyThresholds.suspiciousPatterns.timeWindow
        );

        // Check for multiple different types of suspicious events
        const eventTypes = new Set(recentEvents.map(event => event.type));
        if (eventTypes.size >= this.anomalyThresholds.suspiciousPatterns.count) {
            this.detectAnomaly('multiple_suspicious_patterns', {
                eventTypes: Array.from(eventTypes),
                eventCount: recentEvents.length,
                timeWindow: this.anomalyThresholds.suspiciousPatterns.timeWindow
            });
        }
    }

    /**
     * Initialize security status widgets
     */
    initializeSecurityWidgets() {
        const widgetHtml = `
            <div class="security-status-widget position-fixed" style="top: 70px; right: 20px; z-index: 1050;">
                <div class="card shadow-sm" style="width: 300px;">
                    <div class="card-header bg-primary text-white d-flex align-items-center">
                        <i class="fas fa-shield-alt me-2"></i>
                        <span class="fw-bold">Security Status</span>
                        <button class="btn btn-sm btn-outline-light ms-auto" type="button" data-bs-toggle="collapse" data-bs-target="#securityStatusBody">
                            <i class="fas fa-chevron-down"></i>
                        </button>
                    </div>
                    <div class="collapse show" id="securityStatusBody">
                        <div class="card-body p-3">
                            <div class="security-level-indicator mb-2">
                                <small class="text-muted">Security Level:</small>
                                <span class="badge bg-success ms-2" id="securityLevelBadge">Normal</span>
                            </div>
                            <div class="rate-limit-indicator mb-2">
                                <small class="text-muted">API Rate Limit:</small>
                                <div class="progress mt-1" style="height: 4px;">
                                    <div class="progress-bar bg-info" id="rateLimitBar" style="width: 0%"></div>
                                </div>
                                <small class="text-muted" id="rateLimitText">0/60 requests</small>
                            </div>
                            <div class="connection-status mb-2">
                                <small class="text-muted">Connection:</small>
                                <span class="badge bg-success ms-2" id="connectionStatusBadge">
                                    <i class="fas fa-check-circle me-1"></i>Secure
                                </span>
                            </div>
                            <div class="session-indicator">
                                <small class="text-muted">Session:</small>
                                <span class="text-success ms-2" id="sessionStatusText">
                                    <i class="fas fa-clock me-1"></i>Active
                                </span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Add widget to page if not in admin areas (to avoid conflicts)
        if (!window.location.pathname.startsWith('/Admin')) {
            $('body').append(widgetHtml);
            
            // Make widget draggable
            $('.security-status-widget').draggable({
                handle: '.card-header',
                containment: 'window'
            });
        }
    }

    /**
     * Update security widgets with current status
     */
    updateSecurityWidgets() {
        // Update security level badge
        const levelBadge = $('#securityLevelBadge');
        if (levelBadge.length) {
            const levelConfig = {
                normal: { class: 'bg-success', text: 'Normal' },
                elevated: { class: 'bg-warning', text: 'Elevated' },
                high: { class: 'bg-danger', text: 'High' },
                critical: { class: 'bg-dark', text: 'Critical' }
            };
            
            const config = levelConfig[this.securityLevel] || levelConfig.normal;
            levelBadge.removeClass('bg-success bg-warning bg-danger bg-dark')
                     .addClass(config.class)
                     .text(config.text);
        }

        // Update connection status
        this.updateConnectionStatus();
        
        // Update session status if security validation is available
        if (window.securityValidation) {
            this.updateSessionStatus(window.securityValidation.getSecurityStatus());
        }
    }

    /**
     * Update rate limit indicator
     */
    updateRateLimitIndicator() {
        const percentage = (this.rateLimit.requests / this.rateLimit.maxRequests) * 100;
        
        $('#rateLimitBar').css('width', `${percentage}%`);
        $('#rateLimitText').text(`${this.rateLimit.requests}/${this.rateLimit.maxRequests} requests`);
        
        // Change color based on usage
        const bar = $('#rateLimitBar');
        bar.removeClass('bg-info bg-warning bg-danger');
        if (percentage >= 90) {
            bar.addClass('bg-danger');
        } else if (percentage >= 70) {
            bar.addClass('bg-warning');
        } else {
            bar.addClass('bg-info');
        }
    }

    /**
     * Update security level indicator
     */
    updateSecurityLevelIndicator() {
        this.updateSecurityWidgets();
    }

    /**
     * Update connection status
     */
    updateConnectionStatus() {
        const isSecure = window.location.protocol === 'https:';
        const badge = $('#connectionStatusBadge');
        
        if (badge.length) {
            if (isSecure) {
                badge.removeClass('bg-warning bg-danger')
                     .addClass('bg-success')
                     .html('<i class="fas fa-check-circle me-1"></i>Secure');
            } else {
                badge.removeClass('bg-success')
                     .addClass('bg-warning')
                     .html('<i class="fas fa-exclamation-triangle me-1"></i>Insecure');
            }
        }
    }

    /**
     * Update session status
     */
    updateSessionStatus(status) {
        const sessionText = $('#sessionStatusText');
        
        if (sessionText.length && status) {
            if (status.sessionHealthy) {
                sessionText.removeClass('text-warning text-danger')
                           .addClass('text-success')
                           .html(`<i class="fas fa-clock me-1"></i>Active (${status.sessionAge}m)`);
            } else {
                sessionText.removeClass('text-success text-danger')
                           .addClass('text-warning')
                           .html(`<i class="fas fa-exclamation-triangle me-1"></i>Expiring Soon`);
            }
        }
    }

    /**
     * Send security event to server
     */
    sendSecurityEvent(event) {
        // Only send high-priority events to avoid overwhelming the server
        const highPriorityEvents = [
            'anomaly_detected', 'failed_login', 'invalid_otac', 
            'rate_limit_exceeded', 'security_level_change'
        ];
        
        if (highPriorityEvents.includes(event.type)) {
            $.ajax({
                url: '/api/security/events',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(event),
                headers: {
                    'X-CSRF-TOKEN': window.securityValidation?.csrfToken || ''
                },
                success: () => {
                    console.log(`[SecurityMonitor] Event sent to server: ${event.type}`);
                },
                error: (xhr, status, error) => {
                    console.error(`[SecurityMonitor] Failed to send event: ${error}`);
                }
            });
        }
    }

    /**
     * Show security warning to user
     */
    showSecurityWarning(anomalyType, details) {
        const warningMessages = {
            rapid_clicking: 'Unusual clicking activity detected. Please slow down.',
            rapid_form_submissions: 'Multiple form submissions detected. Please wait before submitting again.',
            rate_limit_exceeded: 'Too many requests. Please wait before continuing.',
            multiple_suspicious_patterns: 'Multiple security events detected. Your session is being monitored.'
        };
        
        const message = warningMessages[anomalyType] || 'Unusual activity detected.';
        
        if (window.securityValidation) {
            window.securityValidation.showToast(message, 'warning');
        }
    }

    /**
     * Show critical security alert
     */
    showCriticalSecurityAlert() {
        const alertHtml = `
            <div class="modal fade" id="criticalSecurityAlert" tabindex="-1" data-bs-backdrop="static" data-bs-keyboard="false">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content border-danger">
                        <div class="modal-header bg-danger text-white">
                            <h5 class="modal-title">
                                <i class="fas fa-exclamation-triangle me-2"></i>
                                Critical Security Alert
                            </h5>
                        </div>
                        <div class="modal-body text-center">
                            <i class="fas fa-shield-alt text-danger fa-4x mb-3"></i>
                            <h6>Suspicious Activity Detected</h6>
                            <p>Multiple security events have been detected on your session.</p>
                            <p class="text-muted">For your protection, some functions have been temporarily restricted.</p>
                        </div>
                        <div class="modal-footer justify-content-center">
                            <button type="button" class="btn btn-danger" data-bs-dismiss="modal">
                                <i class="fas fa-check me-1"></i>I Understand
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;
        
        $('body').append(alertHtml);
        const modal = new bootstrap.Modal(document.getElementById('criticalSecurityAlert'));
        modal.show();
        
        // Remove modal after closing
        $('#criticalSecurityAlert').on('hidden.bs.modal', function() {
            $(this).remove();
        });
    }

    /**
     * Utility methods
     */
    isSensitivePage() {
        const sensitivePaths = ['/Admin', '/Account', '/Profile', '/Settings', '/Payment'];
        return sensitivePaths.some(path => window.location.pathname.startsWith(path));
    }

    generateEventId() {
        return `evt_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
    }

    monitorConsoleAccess() {
        // Detect developer tools access attempts
        let devtools = { opened: false, orientation: null };
        
        setInterval(() => {
            if (window.outerHeight - window.innerHeight > 200 || 
                window.outerWidth - window.innerWidth > 200) {
                if (!devtools.opened) {
                    devtools.opened = true;
                    this.trackEvent('developer_tools_access', {
                        timestamp: Date.now(),
                        viewport: { width: window.innerWidth, height: window.innerHeight }
                    });
                }
            } else {
                devtools.opened = false;
            }
        }, 500);
    }

    enableEnhancedValidation() {
        // Enable stricter input validation
        console.log('[SecurityMonitor] Enhanced validation enabled');
    }

    enableVerboseLogging() {
        // Enable more detailed logging
        console.log('[SecurityMonitor] Verbose logging enabled');
    }

    reduceSessionTimeout(minutes) {
        if (window.securityValidation) {
            window.securityValidation.sessionTimeout = minutes;
            window.securityValidation.sessionTimeoutWarning = minutes - 5;
            console.log(`[SecurityMonitor] Session timeout reduced to ${minutes} minutes`);
        }
    }

    lockSensitiveFunctions() {
        // Disable sensitive form submissions
        $('form[data-sensitive="true"], .sensitive-form').find('input, button').prop('disabled', true);
        console.log('[SecurityMonitor] Sensitive functions locked');
    }

    cleanupOldEvents() {
        const cutoff = Date.now() - (24 * 60 * 60 * 1000); // 24 hours
        this.securityEvents = this.securityEvents.filter(event => event.timestamp > cutoff);
        
        // Clean up failed attempts
        for (const [type, attempts] of this.failedAttempts) {
            const recentAttempts = attempts.filter(attempt => attempt.timestamp > cutoff);
            this.failedAttempts.set(type, recentAttempts);
        }
    }

    /**
     * Get security monitoring report
     */
    getSecurityReport() {
        return {
            securityLevel: this.securityLevel,
            eventCount: this.securityEvents.length,
            failedAttemptTypes: Array.from(this.failedAttempts.keys()),
            rateLimit: this.rateLimit,
            monitoringActive: this.monitoringActive,
            timestamp: Date.now()
        };
    }
}

// Initialize security monitor when DOM is ready
$(document).ready(() => {
    window.securityMonitor = new SecurityMonitor();
    
    // Integrate with security validation if available
    if (window.securityValidation) {
        window.securityValidation.securityMonitor = window.securityMonitor;
    }
});