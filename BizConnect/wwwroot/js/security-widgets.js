/**
 * BizConnect Security Status Widgets
 * Modern UI components for displaying security status, attempt counters, and monitoring information
 * 
 * Features:
 * - Real-time security status display
 * - OTAC attempt counters with visual feedback
 * - Connection security indicators
 * - Session status widgets
 * - Rate limit progress indicators
 * - Security level badges
 */

class SecurityWidgets {
    constructor(options = {}) {
        this.options = {
            enableFloatingWidget: true,
            enableHeaderWidget: true,
            enableFormWidgets: true,
            updateInterval: 5000, // 5 seconds
            animationDuration: 300,
            position: 'bottom-right', // top-left, top-right, bottom-left, bottom-right
            theme: 'auto', // light, dark, auto
            ...options
        };

        this.widgets = new Map();
        this.updateTimer = null;
        this.isInitialized = false;

        this.init();
    }

    /**
     * Initialize security widgets system
     */
    init() {
        this.createWidgetTemplates();
        this.bindEventHandlers();
        this.startPeriodicUpdates();
        
        if (this.options.enableFloatingWidget) {
            this.createFloatingWidget();
        }
        
        if (this.options.enableHeaderWidget) {
            this.createHeaderWidget();
        }

        this.isInitialized = true;
        console.log('[SecurityWidgets] Security widgets system initialized');
    }

    /**
     * Create widget templates and styles
     */
    createWidgetTemplates() {
        // Add CSS styles for security widgets
        this.injectStyles();
        
        // Register widget templates
        this.registerWidgetTemplates();
    }

    /**
     * Inject CSS styles for security widgets
     */
    injectStyles() {
        const styles = `
            <style id="security-widgets-styles">
                .security-widget {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                    font-size: 13px;
                    border-radius: 8px;
                    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
                    background: rgba(255, 255, 255, 0.95);
                    backdrop-filter: blur(10px);
                    border: 1px solid rgba(0, 0, 0, 0.1);
                    transition: all 0.3s ease;
                }

                .security-widget.theme-dark {
                    background: rgba(33, 37, 41, 0.95);
                    border: 1px solid rgba(255, 255, 255, 0.1);
                    color: #ffffff;
                }

                .security-floating-widget {
                    position: fixed;
                    z-index: 1050;
                    min-width: 280px;
                    max-width: 350px;
                }

                .security-floating-widget.position-top-left {
                    top: 20px;
                    left: 20px;
                }

                .security-floating-widget.position-top-right {
                    top: 20px;
                    right: 20px;
                }

                .security-floating-widget.position-bottom-left {
                    bottom: 20px;
                    left: 20px;
                }

                .security-floating-widget.position-bottom-right {
                    bottom: 20px;
                    right: 20px;
                }

                .security-header-widget {
                    display: inline-flex;
                    align-items: center;
                    gap: 8px;
                    padding: 4px 12px;
                    border-radius: 20px;
                    background: rgba(40, 167, 69, 0.1);
                    border: 1px solid rgba(40, 167, 69, 0.2);
                }

                .security-status-indicator {
                    display: flex;
                    align-items: center;
                    gap: 6px;
                    padding: 8px 12px;
                }

                .security-status-badge {
                    display: inline-flex;
                    align-items: center;
                    gap: 4px;
                    padding: 2px 8px;
                    border-radius: 12px;
                    font-size: 11px;
                    font-weight: 500;
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                }

                .security-status-badge.level-normal {
                    background: rgba(40, 167, 69, 0.15);
                    color: #28a745;
                }

                .security-status-badge.level-elevated {
                    background: rgba(255, 193, 7, 0.15);
                    color: #ffc107;
                }

                .security-status-badge.level-high {
                    background: rgba(220, 53, 69, 0.15);
                    color: #dc3545;
                }

                .security-status-badge.level-critical {
                    background: rgba(108, 117, 125, 0.15);
                    color: #6c757d;
                    animation: pulse-warning 2s infinite;
                }

                @keyframes pulse-warning {
                    0%, 100% { opacity: 1; }
                    50% { opacity: 0.7; }
                }

                .attempt-counter {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    padding: 6px 10px;
                    background: rgba(13, 110, 253, 0.05);
                    border: 1px solid rgba(13, 110, 253, 0.1);
                    border-radius: 6px;
                }

                .attempt-counter.warning {
                    background: rgba(255, 193, 7, 0.1);
                    border-color: rgba(255, 193, 7, 0.2);
                }

                .attempt-counter.danger {
                    background: rgba(220, 53, 69, 0.1);
                    border-color: rgba(220, 53, 69, 0.2);
                }

                .attempt-progress {
                    width: 60px;
                    height: 4px;
                    background: rgba(0, 0, 0, 0.1);
                    border-radius: 2px;
                    overflow: hidden;
                }

                .attempt-progress-bar {
                    height: 100%;
                    transition: width 0.3s ease, background-color 0.3s ease;
                    border-radius: 2px;
                }

                .connection-status {
                    display: flex;
                    align-items: center;
                    gap: 6px;
                    padding: 4px 8px;
                    border-radius: 4px;
                }

                .connection-status.secure {
                    background: rgba(40, 167, 69, 0.1);
                    color: #28a745;
                }

                .connection-status.insecure {
                    background: rgba(220, 53, 69, 0.1);
                    color: #dc3545;
                }

                .session-status {
                    display: flex;
                    align-items: center;
                    gap: 6px;
                    padding: 4px 8px;
                }

                .session-timer {
                    font-family: 'Courier New', monospace;
                    font-weight: bold;
                    min-width: 60px;
                }

                .rate-limit-widget {
                    padding: 8px;
                    background: rgba(255, 193, 7, 0.05);
                    border-left: 3px solid #ffc107;
                    border-radius: 0 4px 4px 0;
                }

                .rate-limit-progress {
                    width: 100%;
                    height: 6px;
                    background: rgba(0, 0, 0, 0.1);
                    border-radius: 3px;
                    overflow: hidden;
                    margin: 4px 0;
                }

                .security-widget-collapsible .widget-header {
                    cursor: pointer;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    padding: 8px 12px;
                    border-bottom: 1px solid rgba(0, 0, 0, 0.1);
                }

                .security-widget-collapsible .widget-body {
                    padding: 8px 12px;
                    transition: max-height 0.3s ease;
                    overflow: hidden;
                }

                .security-widget-collapsible.collapsed .widget-body {
                    max-height: 0;
                    padding-top: 0;
                    padding-bottom: 0;
                }

                .security-widget-minimize-btn {
                    background: none;
                    border: none;
                    color: inherit;
                    cursor: pointer;
                    padding: 2px 4px;
                    border-radius: 2px;
                    opacity: 0.7;
                    transition: opacity 0.2s ease;
                }

                .security-widget-minimize-btn:hover {
                    opacity: 1;
                    background: rgba(0, 0, 0, 0.1);
                }

                .security-pulse {
                    animation: security-pulse 2s infinite;
                }

                @keyframes security-pulse {
                    0% { box-shadow: 0 0 0 0 rgba(40, 167, 69, 0.4); }
                    70% { box-shadow: 0 0 0 10px rgba(40, 167, 69, 0); }
                    100% { box-shadow: 0 0 0 0 rgba(40, 167, 69, 0); }
                }

                .security-alert-pulse {
                    animation: security-alert-pulse 1s infinite;
                }

                @keyframes security-alert-pulse {
                    0% { box-shadow: 0 0 0 0 rgba(220, 53, 69, 0.4); }
                    70% { box-shadow: 0 0 0 10px rgba(220, 53, 69, 0); }
                    100% { box-shadow: 0 0 0 0 rgba(220, 53, 69, 0); }
                }

                @media (max-width: 768px) {
                    .security-floating-widget {
                        min-width: 250px;
                        max-width: 280px;
                    }

                    .security-floating-widget.position-top-left,
                    .security-floating-widget.position-bottom-left {
                        left: 10px;
                    }

                    .security-floating-widget.position-top-right,
                    .security-floating-widget.position-bottom-right {
                        right: 10px;
                    }

                    .security-floating-widget.position-top-left,
                    .security-floating-widget.position-top-right {
                        top: 10px;
                    }

                    .security-floating-widget.position-bottom-left,
                    .security-floating-widget.position-bottom-right {
                        bottom: 10px;
                    }
                }
            </style>
        `;

        if (!$('#security-widgets-styles').length) {
            $('head').append(styles);
        }
    }

    /**
     * Register widget templates
     */
    registerWidgetTemplates() {
        this.templates = {
            floatingWidget: `
                <div class="security-widget security-floating-widget security-widget-collapsible position-{{position}}" id="securityFloatingWidget">
                    <div class="widget-header">
                        <div class="d-flex align-items-center">
                            <i class="fas fa-shield-alt text-success me-2"></i>
                            <span class="fw-semibold">Security Status</span>
                        </div>
                        <button class="security-widget-minimize-btn" onclick="securityWidgets.toggleFloatingWidget()">
                            <i class="fas fa-chevron-down"></i>
                        </button>
                    </div>
                    <div class="widget-body">
                        <div class="security-indicators">
                            <!-- Security Level -->
                            <div class="security-status-indicator">
                                <small class="text-muted">Security Level:</small>
                                <span class="security-status-badge level-normal" id="widgetSecurityLevel">
                                    <i class="fas fa-shield-check"></i> Normal
                                </span>
                            </div>

                            <!-- Connection Status -->
                            <div class="connection-status secure" id="widgetConnectionStatus">
                                <i class="fas fa-lock"></i>
                                <small>Secure Connection</small>
                            </div>

                            <!-- Session Status -->
                            <div class="session-status" id="widgetSessionStatus">
                                <i class="fas fa-clock text-success"></i>
                                <small>Session: <span class="session-timer text-success">Active</span></small>
                            </div>

                            <!-- Rate Limit Status -->
                            <div class="rate-limit-widget d-none" id="widgetRateLimit">
                                <small class="text-muted">Rate Limit:</small>
                                <div class="rate-limit-progress">
                                    <div class="progress-bar bg-info" style="width: 0%"></div>
                                </div>
                                <small class="rate-limit-text">0/60 requests</small>
                            </div>

                            <!-- OTAC Attempts (shown when applicable) -->
                            <div class="attempt-counter d-none" id="widgetOtacAttempts">
                                <i class="fas fa-key text-primary"></i>
                                <small>OTAC Attempts:</small>
                                <span class="fw-bold attempt-count">0</span>/<span class="max-attempts">5</span>
                                <div class="attempt-progress">
                                    <div class="attempt-progress-bar bg-primary" style="width: 0%"></div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `,

            headerWidget: `
                <div class="security-widget security-header-widget" id="securityHeaderWidget">
                    <i class="fas fa-shield-alt text-success"></i>
                    <span class="security-status-text">Secure</span>
                    <span class="security-status-badge level-normal" id="headerSecurityLevel">Normal</span>
                </div>
            `,

            attemptCounter: `
                <div class="attempt-counter" data-form-id="{{formId}}">
                    <i class="fas fa-key"></i>
                    <small>Attempts:</small>
                    <span class="fw-bold attempt-count">{{current}}</span>/<span class="max-attempts">{{max}}</span>
                    <div class="attempt-progress">
                        <div class="attempt-progress-bar bg-{{color}}" style="width: {{percentage}}%"></div>
                    </div>
                </div>
            `,

            sessionTimer: `
                <div class="session-status" id="sessionTimerWidget">
                    <i class="fas fa-clock {{iconClass}}"></i>
                    <small>Session: <span class="session-timer {{textClass}}">{{time}}</span></small>
                </div>
            `,

            rateLimitIndicator: `
                <div class="rate-limit-widget" id="rateLimitWidget">
                    <small class="text-muted">Rate Limit:</small>
                    <div class="rate-limit-progress">
                        <div class="progress-bar {{colorClass}}" style="width: {{percentage}}%"></div>
                    </div>
                    <small class="rate-limit-text">{{current}}/{{max}} requests</small>
                </div>
            `,

            securityBadge: `
                <span class="security-status-badge level-{{level}}">
                    <i class="fas {{icon}}"></i> {{text}}
                </span>
            `
        };
    }

    /**
     * Create floating security widget
     */
    createFloatingWidget() {
        const position = this.options.position;
        const template = this.templates.floatingWidget.replace('{{position}}', position);
        
        // Remove existing widget
        $('#securityFloatingWidget').remove();
        
        // Add new widget
        $('body').append(template);
        
        // Make widget draggable
        $('#securityFloatingWidget').draggable({
            handle: '.widget-header',
            containment: 'window',
            scroll: false
        });

        // Register widget
        this.widgets.set('floating', $('#securityFloatingWidget'));
        
        console.log('[SecurityWidgets] Floating widget created');
    }

    /**
     * Create header security widget
     */
    createHeaderWidget() {
        const template = this.templates.headerWidget;
        
        // Try to add to navbar or header
        const targetSelectors = ['.navbar', '.header', '.top-bar', 'header'];
        let added = false;
        
        for (const selector of targetSelectors) {
            const target = $(selector).first();
            if (target.length) {
                target.append(template);
                added = true;
                break;
            }
        }
        
        if (!added) {
            // Fallback: add to body
            $('body').append(`<div class="d-flex justify-content-end p-2">${template}</div>`);
        }

        // Register widget
        this.widgets.set('header', $('#securityHeaderWidget'));
        
        console.log('[SecurityWidgets] Header widget created');
    }

    /**
     * Create OTAC attempt counter widget
     */
    createOtacAttemptCounter(formId, current = 0, max = 5) {
        const percentage = Math.round((current / max) * 100);
        const color = this.getAttemptColor(current, max);
        
        const template = this.templates.attemptCounter
            .replace('{{formId}}', formId)
            .replace('{{current}}', current.toString())
            .replace('{{max}}', max.toString())
            .replace('{{percentage}}', percentage.toString())
            .replace('{{color}}', color);
        
        return template;
    }

    /**
     * Update OTAC attempt counter
     */
    updateOtacAttemptCounter(formId, current, max = 5) {
        const counter = $(`.attempt-counter[data-form-id="${formId}"]`);
        if (!counter.length) return;

        const percentage = Math.round((current / max) * 100);
        const color = this.getAttemptColor(current, max);
        
        counter.find('.attempt-count').text(current);
        counter.find('.max-attempts').text(max);
        counter.find('.attempt-progress-bar')
               .css('width', `${percentage}%`)
               .removeClass('bg-primary bg-warning bg-danger')
               .addClass(`bg-${color}`);

        // Update counter styling
        counter.removeClass('warning danger');
        if (current >= max * 0.8) {
            counter.addClass('danger');
        } else if (current >= max * 0.6) {
            counter.addClass('warning');
        }

        // Show in floating widget if OTAC form is active
        const floatingWidget = this.widgets.get('floating');
        if (floatingWidget && $('#widgetOtacAttempts').length) {
            $('#widgetOtacAttempts').removeClass('d-none');
            $('#widgetOtacAttempts .attempt-count').text(current);
            $('#widgetOtacAttempts .max-attempts').text(max);
            $('#widgetOtacAttempts .attempt-progress-bar')
                .css('width', `${percentage}%`)
                .removeClass('bg-primary bg-warning bg-danger')
                .addClass(`bg-${color}`);
        }
    }

    /**
     * Update security level in all widgets
     */
    updateSecurityLevel(level) {
        const levelConfig = {
            normal: { icon: 'fa-shield-check', text: 'Normal', class: 'level-normal' },
            elevated: { icon: 'fa-shield-alt', text: 'Elevated', class: 'level-elevated' },
            high: { icon: 'fa-exclamation-shield', text: 'High Alert', class: 'level-high' },
            critical: { icon: 'fa-skull', text: 'Critical', class: 'level-critical' }
        };
        
        const config = levelConfig[level] || levelConfig.normal;
        
        // Update floating widget
        $('#widgetSecurityLevel').removeClass('level-normal level-elevated level-high level-critical')
                                 .addClass(config.class)
                                 .html(`<i class="fas ${config.icon}"></i> ${config.text}`);
        
        // Update header widget
        $('#headerSecurityLevel').removeClass('level-normal level-elevated level-high level-critical')
                                 .addClass(config.class)
                                 .text(config.text);

        // Add pulse animation for critical level
        const floatingWidget = this.widgets.get('floating');
        if (floatingWidget) {
            floatingWidget.removeClass('security-pulse security-alert-pulse');
            if (level === 'critical') {
                floatingWidget.addClass('security-alert-pulse');
            } else if (level === 'elevated' || level === 'high') {
                floatingWidget.addClass('security-pulse');
            }
        }
        
        console.log(`[SecurityWidgets] Security level updated to: ${level}`);
    }

    /**
     * Update connection status
     */
    updateConnectionStatus() {
        const isSecure = window.location.protocol === 'https:';
        const statusElement = $('#widgetConnectionStatus');
        
        if (isSecure) {
            statusElement.removeClass('insecure').addClass('secure')
                        .html('<i class="fas fa-lock"></i><small>Secure Connection</small>');
        } else {
            statusElement.removeClass('secure').addClass('insecure')
                        .html('<i class="fas fa-unlock"></i><small>Insecure Connection</small>');
        }
    }

    /**
     * Update session status
     */
    updateSessionStatus(status) {
        const sessionElement = $('#widgetSessionStatus');
        const timerElement = sessionElement.find('.session-timer');
        const iconElement = sessionElement.find('i');
        
        if (status.sessionHealthy) {
            iconElement.removeClass('text-warning text-danger').addClass('text-success');
            timerElement.removeClass('text-warning text-danger').addClass('text-success')
                       .text(`${status.remainingTime || 0}m left`);
        } else if (status.remainingTime > 0) {
            iconElement.removeClass('text-success text-danger').addClass('text-warning');
            timerElement.removeClass('text-success text-danger').addClass('text-warning')
                       .text(`${status.remainingTime}m left`);
        } else {
            iconElement.removeClass('text-success text-warning').addClass('text-danger');
            timerElement.removeClass('text-success text-warning').addClass('text-danger')
                       .text('Expired');
        }
    }

    /**
     * Update rate limit status
     */
    updateRateLimit(current, max, windowMs = 60000) {
        const percentage = Math.round((current / max) * 100);
        const rateLimitWidget = $('#widgetRateLimit');
        
        if (current > 0) {
            rateLimitWidget.removeClass('d-none');
            
            const progressBar = rateLimitWidget.find('.progress-bar');
            const statusText = rateLimitWidget.find('.rate-limit-text');
            
            progressBar.css('width', `${percentage}%`)
                      .removeClass('bg-info bg-warning bg-danger');
            
            if (percentage >= 90) {
                progressBar.addClass('bg-danger');
            } else if (percentage >= 70) {
                progressBar.addClass('bg-warning');
            } else {
                progressBar.addClass('bg-info');
            }
            
            statusText.text(`${current}/${max} requests`);
        } else {
            rateLimitWidget.addClass('d-none');
        }
    }

    /**
     * Show security event notification
     */
    showSecurityEvent(eventType, details) {
        const floatingWidget = this.widgets.get('floating');
        if (!floatingWidget) return;

        // Add temporary event indicator
        const eventIndicator = $(`
            <div class="security-event-indicator alert alert-warning alert-dismissible fade show" style="font-size: 12px; padding: 6px 8px; margin-top: 8px;">
                <i class="fas fa-exclamation-triangle me-1"></i>
                <strong>Security Event:</strong> ${this.getEventMessage(eventType)}
                <button type="button" class="btn-close btn-close-sm" data-bs-dismiss="alert"></button>
            </div>
        `);
        
        floatingWidget.find('.widget-body').append(eventIndicator);
        
        // Auto-remove after 10 seconds
        setTimeout(() => {
            eventIndicator.fadeOut(() => eventIndicator.remove());
        }, 10000);
    }

    /**
     * Toggle floating widget visibility
     */
    toggleFloatingWidget() {
        const widget = $('#securityFloatingWidget');
        const body = widget.find('.widget-body');
        const icon = widget.find('.security-widget-minimize-btn i');
        
        if (widget.hasClass('collapsed')) {
            widget.removeClass('collapsed');
            body.slideDown(this.options.animationDuration);
            icon.removeClass('fa-chevron-up').addClass('fa-chevron-down');
        } else {
            widget.addClass('collapsed');
            body.slideUp(this.options.animationDuration);
            icon.removeClass('fa-chevron-down').addClass('fa-chevron-up');
        }
    }

    /**
     * Bind event handlers
     */
    bindEventHandlers() {
        // Listen for security events
        document.addEventListener('securityLevelChanged', (e) => {
            this.updateSecurityLevel(e.detail.level);
        });

        document.addEventListener('otacAttemptChanged', (e) => {
            this.updateOtacAttemptCounter(e.detail.formId, e.detail.current, e.detail.max);
        });

        document.addEventListener('sessionStatusChanged', (e) => {
            this.updateSessionStatus(e.detail);
        });

        document.addEventListener('rateLimitChanged', (e) => {
            this.updateRateLimit(e.detail.current, e.detail.max, e.detail.windowMs);
        });

        document.addEventListener('securityEvent', (e) => {
            this.showSecurityEvent(e.detail.type, e.detail.details);
        });

        // Handle theme changes
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
            this.updateTheme();
        });

        // Handle window resize for mobile
        $(window).on('resize', () => {
            this.handleResize();
        });
    }

    /**
     * Start periodic updates
     */
    startPeriodicUpdates() {
        if (this.updateTimer) {
            clearInterval(this.updateTimer);
        }

        this.updateTimer = setInterval(() => {
            this.updateWidgets();
        }, this.options.updateInterval);
    }

    /**
     * Update all widgets with current status
     */
    updateWidgets() {
        if (!this.isInitialized) return;

        // Update connection status
        this.updateConnectionStatus();

        // Get status from security systems
        if (window.securityMonitor) {
            const status = window.securityMonitor.getSecurityReport();
            this.updateSecurityLevel(status.securityLevel);
            this.updateRateLimit(status.rateLimit.requests, status.rateLimit.maxRequests);
        }

        if (window.sessionManager) {
            const sessionInfo = window.sessionManager.getSessionInfo();
            this.updateSessionStatus(sessionInfo);
        }

        if (window.securityValidation) {
            const securityStatus = window.securityValidation.getSecurityStatus();
            // Update any additional status from security validation
        }
    }

    /**
     * Update theme based on user preference or system setting
     */
    updateTheme() {
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        const theme = this.options.theme === 'auto' ? (prefersDark ? 'dark' : 'light') : this.options.theme;
        
        this.widgets.forEach(widget => {
            widget.removeClass('theme-light theme-dark').addClass(`theme-${theme}`);
        });
    }

    /**
     * Handle window resize
     */
    handleResize() {
        // Adjust widget positioning for mobile
        const isMobile = window.innerWidth <= 768;
        
        this.widgets.forEach((widget, key) => {
            if (key === 'floating' && isMobile) {
                widget.css({
                    'min-width': '250px',
                    'max-width': '280px'
                });
            }
        });
    }

    /**
     * Utility methods
     */
    getAttemptColor(current, max) {
        const percentage = current / max;
        if (percentage >= 0.8) return 'danger';
        if (percentage >= 0.6) return 'warning';
        return 'primary';
    }

    getEventMessage(eventType) {
        const messages = {
            'rapid_clicking': 'Rapid clicking detected',
            'rate_limit_exceeded': 'Rate limit exceeded',
            'failed_login': 'Login attempt failed',
            'invalid_otac': 'Invalid OTAC entered',
            'session_warning': 'Session expiring soon',
            'anomaly_detected': 'Suspicious activity detected'
        };
        
        return messages[eventType] || 'Security event occurred';
    }

    /**
     * Public API methods
     */
    showWidget(widgetId) {
        const widget = this.widgets.get(widgetId);
        if (widget) {
            widget.fadeIn(this.options.animationDuration);
        }
    }

    hideWidget(widgetId) {
        const widget = this.widgets.get(widgetId);
        if (widget) {
            widget.fadeOut(this.options.animationDuration);
        }
    }

    destroyWidget(widgetId) {
        const widget = this.widgets.get(widgetId);
        if (widget) {
            widget.remove();
            this.widgets.delete(widgetId);
        }
    }

    createCustomWidget(id, template, options = {}) {
        const widget = $(template);
        $('body').append(widget);
        this.widgets.set(id, widget);
        
        if (options.draggable) {
            widget.draggable({
                handle: options.dragHandle || '.widget-header',
                containment: 'window'
            });
        }
        
        return widget;
    }

    updateWidgetContent(widgetId, content) {
        const widget = this.widgets.get(widgetId);
        if (widget) {
            widget.find('.widget-body').html(content);
        }
    }

    /**
     * Cleanup
     */
    destroy() {
        if (this.updateTimer) {
            clearInterval(this.updateTimer);
        }

        this.widgets.forEach(widget => {
            widget.remove();
        });
        
        this.widgets.clear();
        $('#security-widgets-styles').remove();
        
        this.isInitialized = false;
        console.log('[SecurityWidgets] Security widgets destroyed');
    }
}

// Initialize security widgets when DOM is ready
$(document).ready(() => {
    // Get configuration from data attributes or global config
    const widgetConfig = window.securityWidgetsConfig || {};
    
    // Don't show widgets in admin areas by default (can be overridden)
    if (window.location.pathname.startsWith('/Admin') && !widgetConfig.forceShow) {
        widgetConfig.enableFloatingWidget = false;
    }
    
    window.securityWidgets = new SecurityWidgets(widgetConfig);
    
    console.log('[SecurityWidgets] Security widgets system ready');
});