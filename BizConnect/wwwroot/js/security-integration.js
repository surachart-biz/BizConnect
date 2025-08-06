/**
 * BizConnect Security System Integration
 * Demonstrates integration of all security components and provides helper functions
 * 
 * This script shows how to:
 * - Use the SecurityValidation class for form validation
 * - Integrate with SecurityMonitor for event tracking
 * - Implement SessionManager for timeout handling
 * - Use SecurityWidgets for status display
 * - Apply ApiSecurity for secure requests
 */

class SecurityIntegration {
    constructor() {
        this.initialized = false;
        this.integrationHandlers = new Map();
        
        this.init();
    }

    /**
     * Initialize security system integration
     */
    init() {
        // Wait for all security components to be ready
        this.waitForSecurityComponents().then(() => {
            this.setupIntegration();
            this.bindIntegrationHandlers();
            this.initializeSecurityDemonstration();
            
            this.initialized = true;
            console.log('[SecurityIntegration] Security system integration complete');
        });
    }

    /**
     * Wait for all security components to initialize
     */
    async waitForSecurityComponents() {
        return new Promise((resolve) => {
            const checkComponents = () => {
                if (window.securityValidation && 
                    window.securityMonitor && 
                    window.sessionManager && 
                    window.securityWidgets && 
                    window.apiSecurity) {
                    resolve();
                } else {
                    setTimeout(checkComponents, 100);
                }
            };
            checkComponents();
        });
    }

    /**
     * Setup integration between security components
     */
    setupIntegration() {
        // Connect SecurityMonitor with SecurityWidgets
        window.securityMonitor.securityWidgets = window.securityWidgets;
        
        // Connect SessionManager with SecurityValidation
        window.sessionManager.securityValidation = window.securityValidation;
        
        // Connect ApiSecurity with SecurityMonitor
        window.apiSecurity.securityMonitor = window.securityMonitor;
        
        // Connect SecurityValidation with SessionManager
        window.securityValidation.sessionManager = window.sessionManager;
        
        console.log('[SecurityIntegration] Component cross-references established');
    }

    /**
     * Bind integration event handlers
     */
    bindIntegrationHandlers() {
        // Handle OTAC form interactions
        $(document).on('input', 'input[data-otac="true"], .otac-input', (e) => {
            this.handleOtacInput(e);
        });

        // Handle form submissions with security validation
        $(document).on('submit', 'form[data-security-level]', (e) => {
            this.handleSecureFormSubmit(e);
        });

        // Handle security level changes
        document.addEventListener('securityLevelChanged', (e) => {
            this.handleSecurityLevelChange(e.detail);
        });

        // Handle session warnings
        document.addEventListener('sessionWarning', (e) => {
            this.handleSessionWarning(e.detail);
        });

        // Handle API errors with security implications
        $(document).ajaxError((event, xhr, settings) => {
            this.handleApiSecurityError(xhr, settings);
        });

        console.log('[SecurityIntegration] Integration handlers bound');
    }

    /**
     * Initialize security demonstration features
     */
    initializeSecurityDemonstration() {
        // Add security demo controls to development environments
        if (this.isDevelopmentEnvironment()) {
            this.addSecurityDemoControls();
        }

        // Set up automated security tests
        if (window.location.search.includes('security-test=true')) {
            this.runSecurityTests();
        }
    }

    /**
     * Handle OTAC input with integrated security features
     */
    handleOtacInput(event) {
        const $input = $(event.target);
        const formId = $input.closest('form').attr('id') || 'unknown';
        const currentValue = $input.val();

        // Track OTAC input for security monitoring
        if (window.securityMonitor) {
            window.securityMonitor.trackEvent('otac_input_changed', {
                formId: formId,
                inputLength: currentValue.length,
                isComplete: currentValue.length === 8,
                timestamp: Date.now()
            });
        }

        // Update attempt counter in widgets
        if (window.securityWidgets) {
            // This would be called when an OTAC is submitted and fails
            // For demonstration, we'll simulate this on the 3rd character
            if (currentValue.length === 3) {
                window.securityWidgets.updateOtacAttemptCounter(formId, 1, 5);
            }
        }
    }

    /**
     * Handle secure form submissions
     */
    handleSecureFormSubmit(event) {
        const $form = $(event.target);
        const securityLevel = $form.data('security-level') || 'normal';
        const formType = $form.data('form-type') || 'standard';

        console.log(`[SecurityIntegration] Secure form submitted: ${formType} (${securityLevel})`);

        // Additional security validations based on current security level
        if (window.securityMonitor && window.securityMonitor.securityLevel === 'critical') {
            // In critical security mode, require additional confirmation
            if (!confirm('The system is in high security mode. Are you sure you want to proceed?')) {
                event.preventDefault();
                return false;
            }
        }

        // Track form submission
        if (window.securityMonitor) {
            window.securityMonitor.trackEvent('secure_form_submitted', {
                formType: formType,
                securityLevel: securityLevel,
                currentSystemLevel: window.securityMonitor.securityLevel,
                timestamp: Date.now()
            });
        }

        return true;
    }

    /**
     * Handle security level changes
     */
    handleSecurityLevelChange(detail) {
        console.log(`[SecurityIntegration] Security level changed to: ${detail.level}`);

        // Update all forms based on new security level
        $('form[data-security-level]').each((index, form) => {
            const $form = $(form);
            const currentLevel = $form.data('security-level');
            
            // Upgrade form security level if system level is higher
            if (this.getSecurityLevelPriority(detail.level) > this.getSecurityLevelPriority(currentLevel)) {
                $form.attr('data-security-level', detail.level);
                this.updateFormSecurityVisuals($form, detail.level);
            }
        });

        // Show notification for significant level changes
        if (detail.level === 'critical' || detail.level === 'high') {
            this.showSecurityLevelNotification(detail.level);
        }
    }

    /**
     * Handle session warning events
     */
    handleSessionWarning(detail) {
        console.log('[SecurityIntegration] Session warning received:', detail);

        // Pause any ongoing operations
        this.pauseNonEssentialOperations();

        // Show integrated warning across all security components
        if (window.securityWidgets) {
            window.securityWidgets.showSecurityEvent('session_warning', detail);
        }
    }

    /**
     * Handle API errors with security implications
     */
    handleApiSecurityError(xhr, settings) {
        const status = xhr.status;
        const url = settings.url;

        // Handle specific security-related API errors
        switch (status) {
            case 401: // Unauthorized
                this.handleUnauthorizedError(xhr, settings);
                break;
            case 403: // Forbidden
                this.handleForbiddenError(xhr, settings);
                break;
            case 419: // CSRF Token Mismatch
                this.handleCsrfError(xhr, settings);
                break;
            case 429: // Too Many Requests
                this.handleRateLimitError(xhr, settings);
                break;
        }
    }

    /**
     * Create a secure form programmatically
     */
    createSecureForm(options = {}) {
        const defaultOptions = {
            id: `secureForm_${Date.now()}`,
            action: '#',
            method: 'POST',
            securityLevel: 'normal',
            isOtacForm: false,
            isSensitive: false,
            showIndicators: true
        };

        const config = { ...defaultOptions, ...options };

        // Create form element
        const $form = $(`
            <form id="${config.id}" 
                  action="${config.action}" 
                  method="${config.method}"
                  class="secure-form security-level-${config.securityLevel}"
                  data-security-level="${config.securityLevel}"
                  data-form-type="${config.isOtacForm ? 'otac' : 'standard'}"
                  data-sensitive="${config.isSensitive}">
                
                <!-- CSRF Token -->
                <input type="hidden" name="__RequestVerificationToken" value="${this.getCSRFToken()}">
                
                <!-- Security Metadata -->
                <input type="hidden" name="FormId" value="${config.id}">
                <input type="hidden" name="SecurityLevel" value="${config.securityLevel}">
                <input type="hidden" name="Timestamp" value="${Date.now()}">
                
                <!-- Form Content Placeholder -->
                <div class="secure-form-content">
                    ${config.content || '<p>Form content goes here</p>'}
                </div>
                
                <!-- Submit Button -->
                <div class="form-actions mt-3">
                    <button type="submit" class="btn btn-primary">
                        <i class="fas fa-paper-plane me-1"></i>Submit
                    </button>
                </div>
            </form>
        `);

        // Add security indicators if enabled
        if (config.showIndicators) {
            const indicators = this.createSecurityIndicators(config);
            $form.prepend(indicators);
        }

        // Initialize form security features
        this.initializeFormSecurity($form, config);

        return $form;
    }

    /**
     * Create security indicators for a form
     */
    createSecurityIndicators(config) {
        const levelClass = `security-level-${config.securityLevel}`;
        
        return $(`
            <div class="security-status-header mb-3 ${levelClass}">
                <div class="d-flex align-items-center justify-content-between">
                    <div class="security-status-indicator">
                        <i class="fas fa-shield-alt text-success me-2"></i>
                        <span class="security-status-text">Form Security: Active</span>
                    </div>
                    ${config.isOtacForm ? `
                        <div class="otac-attempt-counter">
                            <small class="text-muted">
                                <i class="fas fa-key me-1"></i>
                                Attempts: <span class="fw-bold">0</span>/5
                            </small>
                        </div>
                    ` : ''}
                </div>
                
                <div class="security-progress mt-2">
                    <div class="progress" style="height: 3px;">
                        <div class="progress-bar bg-success" style="width: 100%"></div>
                    </div>
                </div>
            </div>
        `);
    }

    /**
     * Initialize security features for a form
     */
    initializeFormSecurity($form, config) {
        const formId = config.id;

        // Bind security validation
        if (window.securityValidation) {
            window.securityValidation.initializeForm(formId, config);
        }

        // Set up monitoring
        if (window.securityMonitor) {
            window.securityMonitor.trackEvent('secure_form_created', {
                formId: formId,
                securityLevel: config.securityLevel,
                isOtacForm: config.isOtacForm,
                isSensitive: config.isSensitive
            });
        }

        // Add OTAC-specific features
        if (config.isOtacForm) {
            this.initializeOtacFeatures($form);
        }

        console.log(`[SecurityIntegration] Secure form initialized: ${formId}`);
    }

    /**
     * Initialize OTAC-specific security features
     */
    initializeOtacFeatures($form) {
        const $otacInputs = $form.find('input[data-otac="true"], .otac-input');
        
        $otacInputs.each((index, input) => {
            const $input = $(input);
            
            // Add OTAC-specific styling
            $input.addClass('otac-input')
                  .attr('maxlength', '8')
                  .attr('pattern', '[A-Z0-9]{8}')
                  .attr('placeholder', '12345678');

            // Add character counter
            const counter = $('<small class="form-text text-muted otac-counter">0/8 characters</small>');
            $input.after(counter);

            // Update counter on input
            $input.on('input', function() {
                const length = $(this).val().length;
                counter.text(`${length}/8 characters`);
                
                if (length === 8) {
                    counter.removeClass('text-muted text-danger').addClass('text-success');
                } else if (length > 0) {
                    counter.removeClass('text-muted text-success').addClass('text-primary');
                } else {
                    counter.removeClass('text-success text-primary text-danger').addClass('text-muted');
                }
            });
        });
    }

    /**
     * Utility methods
     */
    getSecurityLevelPriority(level) {
        const priorities = { normal: 0, elevated: 1, high: 2, critical: 3 };
        return priorities[level] || 0;
    }

    updateFormSecurityVisuals($form, level) {
        $form.removeClass('security-level-normal security-level-elevated security-level-high security-level-critical')
             .addClass(`security-level-${level}`);
        
        const $header = $form.prev('.security-status-header');
        if ($header.length) {
            $header.removeClass('security-level-normal security-level-elevated security-level-high security-level-critical')
                   .addClass(`security-level-${level}`);
        }
    }

    showSecurityLevelNotification(level) {
        const messages = {
            high: 'System security level elevated to HIGH. Additional security measures are active.',
            critical: 'CRITICAL security level activated. Some functions may be restricted.'
        };
        
        const message = messages[level];
        if (message && window.securityValidation) {
            window.securityValidation.showToast(message, level === 'critical' ? 'error' : 'warning');
        }
    }

    pauseNonEssentialOperations() {
        // Pause any background operations that are not security-critical
        console.log('[SecurityIntegration] Non-essential operations paused due to security event');
    }

    handleUnauthorizedError(xhr, settings) {
        console.error('[SecurityIntegration] Unauthorized API request:', settings.url);
        
        if (window.sessionManager) {
            window.sessionManager.handleSessionTimeout();
        }
    }

    handleForbiddenError(xhr, settings) {
        console.error('[SecurityIntegration] Forbidden API request:', settings.url);
        
        if (window.securityMonitor) {
            window.securityMonitor.trackEvent('api_access_denied', {
                url: settings.url,
                status: xhr.status,
                timestamp: Date.now()
            });
        }
    }

    handleCsrfError(xhr, settings) {
        console.error('[SecurityIntegration] CSRF error:', settings.url);
        
        // Refresh CSRF token and retry request
        if (window.apiSecurity) {
            window.apiSecurity.refreshCSRFToken().then(() => {
                console.log('[SecurityIntegration] CSRF token refreshed, retrying request');
                $.ajax(settings); // Retry the request
            });
        }
    }

    handleRateLimitError(xhr, settings) {
        console.warn('[SecurityIntegration] Rate limit exceeded:', settings.url);
        
        if (window.securityWidgets) {
            window.securityWidgets.showSecurityEvent('rate_limit_exceeded', {
                url: settings.url,
                retryAfter: xhr.getResponseHeader('Retry-After')
            });
        }
    }

    getCSRFToken() {
        return $('meta[name="csrf-token"]').attr('content') || '';
    }

    isDevelopmentEnvironment() {
        return window.location.hostname === 'localhost' || 
               window.location.hostname.includes('dev');
    }

    /**
     * Development/Testing Methods
     */
    addSecurityDemoControls() {
        const demoControls = $(`
            <div class="security-demo-controls position-fixed" style="bottom: 10px; left: 10px; z-index: 9999;">
                <div class="card" style="width: 250px;">
                    <div class="card-header">
                        <h6 class="mb-0">Security Demo Controls</h6>
                    </div>
                    <div class="card-body">
                        <div class="mb-2">
                            <label class="form-label">Security Level:</label>
                            <select class="form-select form-select-sm" id="securityLevelDemo">
                                <option value="normal">Normal</option>
                                <option value="elevated">Elevated</option>
                                <option value="high">High</option>
                                <option value="critical">Critical</option>
                            </select>
                        </div>
                        <button class="btn btn-sm btn-primary w-100 mb-1" onclick="securityIntegration.simulateOtacAttempt()">
                            Simulate OTAC Attempt
                        </button>
                        <button class="btn btn-sm btn-warning w-100 mb-1" onclick="securityIntegration.simulateSecurityEvent()">
                            Simulate Security Event
                        </button>
                        <button class="btn btn-sm btn-info w-100" onclick="securityIntegration.showSecurityStatus()">
                            Show Security Status
                        </button>
                    </div>
                </div>
            </div>
        `);

        $('body').append(demoControls);

        // Bind demo control handlers
        $('#securityLevelDemo').on('change', function() {
            const newLevel = $(this).val();
            if (window.securityMonitor) {
                window.securityMonitor.escalateSecurityLevel(newLevel);
            }
        });
    }

    simulateOtacAttempt() {
        if (window.securityMonitor) {
            window.securityMonitor.trackEvent('invalid_otac', {
                attempt: Math.floor(Math.random() * 5) + 1,
                timestamp: Date.now()
            });
        }
        
        if (window.securityWidgets) {
            window.securityWidgets.updateOtacAttemptCounter('demo-form', 
                Math.floor(Math.random() * 5) + 1, 5);
        }
    }

    simulateSecurityEvent() {
        const events = ['rapid_clicking', 'rate_limit_exceeded', 'anomaly_detected'];
        const randomEvent = events[Math.floor(Math.random() * events.length)];
        
        if (window.securityMonitor) {
            window.securityMonitor.detectAnomaly(randomEvent, {
                simulated: true,
                timestamp: Date.now()
            });
        }
    }

    showSecurityStatus() {
        const status = {
            securityValidation: window.securityValidation?.getSecurityStatus(),
            securityMonitor: window.securityMonitor?.getSecurityReport(),
            sessionManager: window.sessionManager?.getSessionInfo(),
            apiSecurity: window.apiSecurity?.getSecurityStatus()
        };

        console.log('[SecurityIntegration] Current Security Status:', status);
        alert('Security status logged to console. Press F12 to view.');
    }

    runSecurityTests() {
        console.log('[SecurityIntegration] Running automated security tests...');
        
        // Test security component initialization
        const tests = [
            this.testComponentInitialization(),
            this.testSecurityLevelEscalation(),
            this.testSessionManagement(),
            this.testFormSecurity(),
            this.testApiSecurity()
        ];

        Promise.all(tests).then(results => {
            const passed = results.filter(r => r.passed).length;
            const total = results.length;
            
            console.log(`[SecurityIntegration] Security tests completed: ${passed}/${total} passed`);
            
            results.forEach((result, index) => {
                console.log(`Test ${index + 1}: ${result.name} - ${result.passed ? 'PASSED' : 'FAILED'}`);
                if (!result.passed && result.error) {
                    console.error(`Error: ${result.error}`);
                }
            });
        });
    }

    async testComponentInitialization() {
        try {
            const components = ['securityValidation', 'securityMonitor', 'sessionManager', 'securityWidgets', 'apiSecurity'];
            const missing = components.filter(comp => !window[comp]);
            
            return {
                name: 'Component Initialization',
                passed: missing.length === 0,
                error: missing.length > 0 ? `Missing components: ${missing.join(', ')}` : null
            };
        } catch (error) {
            return { name: 'Component Initialization', passed: false, error: error.message };
        }
    }

    async testSecurityLevelEscalation() {
        try {
            if (!window.securityMonitor) throw new Error('SecurityMonitor not available');
            
            const originalLevel = window.securityMonitor.securityLevel;
            window.securityMonitor.escalateSecurityLevel('elevated');
            const newLevel = window.securityMonitor.securityLevel;
            
            // Reset to original level
            window.securityMonitor.escalateSecurityLevel(originalLevel);
            
            return {
                name: 'Security Level Escalation',
                passed: newLevel === 'elevated',
                error: newLevel !== 'elevated' ? 'Failed to escalate security level' : null
            };
        } catch (error) {
            return { name: 'Security Level Escalation', passed: false, error: error.message };
        }
    }

    async testSessionManagement() {
        try {
            if (!window.sessionManager) throw new Error('SessionManager not available');
            
            const sessionInfo = window.sessionManager.getSessionInfo();
            const hasValidInfo = sessionInfo && sessionInfo.sessionAge !== undefined;
            
            return {
                name: 'Session Management',
                passed: hasValidInfo,
                error: !hasValidInfo ? 'Session info not available' : null
            };
        } catch (error) {
            return { name: 'Session Management', passed: false, error: error.message };
        }
    }

    async testFormSecurity() {
        try {
            const $testForm = this.createSecureForm({
                id: 'test-security-form',
                securityLevel: 'elevated',
                content: '<input type="text" name="test" required>'
            });
            
            const hasSecurityFeatures = $testForm.find('input[name="__RequestVerificationToken"]').length > 0 &&
                                       $testForm.hasClass('secure-form') &&
                                       $testForm.data('security-level') === 'elevated';
            
            $testForm.remove(); // Clean up
            
            return {
                name: 'Form Security',
                passed: hasSecurityFeatures,
                error: !hasSecurityFeatures ? 'Security features not properly applied to form' : null
            };
        } catch (error) {
            return { name: 'Form Security', passed: false, error: error.message };
        }
    }

    async testApiSecurity() {
        try {
            if (!window.apiSecurity) throw new Error('ApiSecurity not available');
            
            const status = window.apiSecurity.getSecurityStatus();
            const hasValidStatus = status && status.rateLimitStatus !== undefined;
            
            return {
                name: 'API Security',
                passed: hasValidStatus,
                error: !hasValidStatus ? 'API security status not available' : null
            };
        } catch (error) {
            return { name: 'API Security', passed: false, error: error.message };
        }
    }

    /**
     * Public API methods
     */
    getIntegrationStatus() {
        return {
            initialized: this.initialized,
            components: {
                securityValidation: !!window.securityValidation,
                securityMonitor: !!window.securityMonitor,
                sessionManager: !!window.sessionManager,
                securityWidgets: !!window.securityWidgets,
                apiSecurity: !!window.apiSecurity
            },
            integrationHandlers: this.integrationHandlers.size
        };
    }

    reinitialize() {
        console.log('[SecurityIntegration] Reinitializing security integration...');
        this.initialized = false;
        this.init();
    }
}

// Initialize security integration when DOM is ready
$(document).ready(() => {
    window.securityIntegration = new SecurityIntegration();
    
    console.log('[SecurityIntegration] Security integration system ready');
});