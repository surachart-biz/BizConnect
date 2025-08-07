/**
 * Enhanced KBank Registration Form
 * Advanced UX and Validation Improvements
 * @version 2.0
 * @author BizConnect Team
 */

(function() {
    'use strict';

    // Enhanced notification system
    function showEnhancedNotification(message, type = 'info', options = {}) {
        const defaults = {
            duration: 5000,
            showCloseButton: true,
            position: 'top-right',
            animated: true
        };
        
        const config = { ...defaults, ...options };
        
        // Create notification element
        const notification = document.createElement('div');
        notification.className = `bc-notification bc-notification-${type} ${config.animated ? 'bc-notification-animated' : ''}`;
        
        // Icon mapping
        const icons = {
            success: 'fas fa-check-circle',
            error: 'fas fa-exclamation-triangle',
            warning: 'fas fa-exclamation-circle',
            info: 'fas fa-info-circle'
        };
        
        notification.innerHTML = `
            <div class="bc-notification-content">
                <i class="${icons[type]} bc-notification-icon"></i>
                <span class="bc-notification-message">${message}</span>
                ${config.showCloseButton ? '<button class="bc-notification-close" type="button" aria-label="Close"><i class="fas fa-times"></i></button>' : ''}
            </div>
            <div class="bc-notification-progress" style="animation-duration: ${config.duration}ms;"></div>
        `;
        
        // Position container
        let container = document.querySelector(`.bc-notification-container.${config.position}`);
        if (!container) {
            container = document.createElement('div');
            container.className = `bc-notification-container ${config.position}`;
            document.body.appendChild(container);
        }
        
        container.appendChild(notification);
        
        // Auto dismiss
        if (config.duration > 0) {
            setTimeout(() => {
                dismissNotification(notification);
            }, config.duration);
        }
        
        // Close button handler
        const closeBtn = notification.querySelector('.bc-notification-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', () => dismissNotification(notification));
        }
        
        // Animate in
        setTimeout(() => {
            notification.classList.add('bc-notification-show');
        }, 100);
        
        return notification;
    }
    
    function dismissNotification(notification) {
        notification.classList.add('bc-notification-hide');
        setTimeout(() => {
            if (notification.parentNode) {
                notification.parentNode.removeChild(notification);
            }
        }, 300);
    }
    
    // Enhanced field validation with better UX
    function createFieldValidator() {
        const validators = {
            nationalId: {
                pattern: /^[0-9]{13}$/,
                checksum: function(id) {
                    if (id.length !== 13) return false;
                    
                    let sum = 0;
                    for (let i = 0; i < 12; i++) {
                        sum += parseInt(id.charAt(i)) * (13 - i);
                    }
                    
                    const remainder = sum % 11;
                    const checkDigit = (11 - remainder) % 10;
                    
                    return checkDigit === parseInt(id.charAt(12));
                },
                validate: function(value) {
                    if (!this.pattern.test(value)) {
                        return { valid: false, message: 'กรุณากรอกเลขบัตรประชาชน 13 หลัก / Please enter 13-digit National ID' };
                    }
                    if (!this.checksum(value)) {
                        return { valid: false, message: 'เลขบัตรประชาชนไม่ถูกต้อง / Invalid National ID checksum' };
                    }
                    return { valid: true, message: 'เลขบัตรประชาชนถูกต้อง / Valid National ID' };
                }
            },
            
            mobileNumber: {
                patterns: {
                    thai: /^(08|09|06|07)\d{8}$/,
                    international: /^\+66[6-9]\d{8}$/
                },
                validate: function(value) {
                    const cleanValue = value.replace(/[\s\-]/g, '');
                    
                    if (this.patterns.thai.test(cleanValue) || this.patterns.international.test(cleanValue)) {
                        return { valid: true, message: 'เบอร์โทรศัพท์ถูกต้อง / Valid mobile number' };
                    }
                    
                    return { 
                        valid: false, 
                        message: 'รูปแบบเบอร์โทรไม่ถูกต้อง (08xxxxxxxx หรือ +66xxxxxxxx) / Invalid mobile format'
                    };
                }
            },
            
            accountNumber: {
                pattern: /^\d{10,15}$/,
                validate: function(value) {
                    if (!this.pattern.test(value)) {
                        return { 
                            valid: false, 
                            message: 'เลขที่บัญชีต้องเป็นตัวเลข 10-15 หลัก / Account number must be 10-15 digits'
                        };
                    }
                    return { valid: true, message: 'เลขที่บัญชีถูกต้อง / Valid account number' };
                }
            },
            
            fullName: {
                pattern: /^[a-zA-Zก-๙\s]{2,}$/,
                validate: function(value) {
                    const trimmedValue = value.trim();
                    
                    if (trimmedValue.length < 2) {
                        return { valid: false, message: 'กรุณากรอกชื่อ-นามสกุล / Please enter your full name' };
                    }
                    
                    if (!this.pattern.test(trimmedValue)) {
                        return { 
                            valid: false, 
                            message: 'ชื่อ-นามสกุลประกอบด้วยตัวอักษรเท่านั้น / Name should contain only letters'
                        };
                    }
                    
                    return { valid: true, message: 'ชื่อ-นามสกุลถูกต้อง / Valid name format' };
                }
            }
        };
        
        return validators;
    }
    
    // Enhanced form initialization
    function initializeEnhancedForm() {
        const form = document.getElementById('oddRegistrationForm');
        if (!form) return;
        
        const validators = createFieldValidator();
        const validFields = new Set();
        const requiredFields = ['FullName', 'MobileNo', 'IdValue', 'AccountNo', 'BranchId'];
        
        // Language switching functionality
        function initLanguageSupport() {
            const currentLang = localStorage.getItem('preferredLanguage') || 'th';
            switchLanguage(currentLang);
            
            // Language toggle event listeners
            document.getElementById('langTH')?.addEventListener('click', () => switchLanguage('th'));
            document.getElementById('langEN')?.addEventListener('click', () => switchLanguage('en'));
        }
        
        function switchLanguage(lang) {
            // Update language buttons
            document.querySelectorAll('.lang-btn').forEach(btn => btn.classList.remove('active'));
            const targetBtn = document.getElementById('lang' + lang.toUpperCase());
            if (targetBtn) targetBtn.classList.add('active');
            
            // Update all elements with multilingual data attributes
            document.querySelectorAll('[data-th][data-en]').forEach(element => {
                if (lang === 'th') {
                    element.textContent = element.getAttribute('data-th');
                } else {
                    element.textContent = element.getAttribute('data-en');
                }
            });
            
            // Update placeholders
            document.querySelectorAll('[data-placeholder-th][data-placeholder-en]').forEach(element => {
                if (lang === 'th') {
                    element.placeholder = element.getAttribute('data-placeholder-th');
                } else {
                    element.placeholder = element.getAttribute('data-placeholder-en');
                }
            });
            
            // Store preference
            localStorage.setItem('preferredLanguage', lang);
        }
        
        // Enhanced field validation with real-time feedback
        function setupFieldValidation() {
            // National ID with enhanced formatting
            const nationalIdField = document.querySelector('input[name="IdValue"]');
            if (nationalIdField) {
                let nationalIdTimeout;
                
                nationalIdField.addEventListener('input', function(e) {
                    let value = e.target.value.replace(/[^\d]/g, '');
                    
                    // Format display (without affecting actual value)
                    if (value.length > 1) {
                        const formatted = value.replace(/(\d{1})(\d{4})?(\d{5})?(\d{2})?(\d{1})?/, 
                            (match, p1, p2, p3, p4, p5) => {
                                let result = p1;
                                if (p2) result += '-' + p2;
                                if (p3) result += '-' + p3;
                                if (p4) result += '-' + p4;
                                if (p5) result += '-' + p5;
                                return result;
                            });
                        
                        // Show formatted version in a tooltip or helper
                        this.setAttribute('title', `รูปแบบ: ${formatted}`);
                    }
                    
                    e.target.value = value;
                    
                    // Debounced validation
                    // Only validate after user has interacted (blur event)
                    // clearTimeout(nationalIdTimeout);
                    // nationalIdTimeout = setTimeout(() => {
                    //     validateField(e.target, 'nationalId', validators.nationalId);
                    // }, 300);
                });
                
                nationalIdField.addEventListener('blur', function(e) {
                    validateField(e.target, 'IdValue', validators.nationalId);
                });
                
                nationalIdField.addEventListener('focus', function(e) {
                    // Clear validation state on focus
                    e.target.classList.remove('is-valid', 'is-invalid');
                });
            }
            
            // Mobile number with smart formatting
            const mobileField = document.querySelector('input[name="MobileNo"]');
            if (mobileField) {
                let mobileTimeout;
                
                mobileField.addEventListener('input', function(e) {
                    let value = e.target.value.replace(/[^\d+\-]/g, '');
                    e.target.value = value;
                    
                    // Smart formatting suggestions
                    if (value.startsWith('0') && value.length === 10) {
                        this.setAttribute('title', `รูปแบบ: ${value.replace(/(\d{2})(\d{4})(\d{4})/, '$1-$2-$3')}`);
                    }
                    
                    // Only validate after user has interacted (blur event)
                    // clearTimeout(mobileTimeout);
                    // mobileTimeout = setTimeout(() => {
                    //     validateField(e.target, 'MobileNo', validators.mobileNumber);
                    // }, 400);
                });
                
                mobileField.addEventListener('blur', function(e) {
                    validateField(e.target, 'MobileNo', validators.mobileNumber);
                });
                
                mobileField.addEventListener('focus', function(e) {
                    // Clear validation state on focus
                    e.target.classList.remove('is-valid', 'is-invalid');
                });
            }
            
            // Account number
            const accountField = document.querySelector('input[name="AccountNo"]');
            if (accountField) {
                let accountTimeout;
                
                accountField.addEventListener('input', function(e) {
                    let value = e.target.value.replace(/[^\d]/g, '');
                    e.target.value = value;
                    
                    // Only validate after user has interacted (blur event)
                    // clearTimeout(accountTimeout);
                    // accountTimeout = setTimeout(() => {
                    //     validateField(e.target, 'AccountNo', validators.accountNumber);
                    // }, 300);
                });
                
                accountField.addEventListener('blur', function(e) {
                    validateField(e.target, 'AccountNo', validators.accountNumber);
                });
                
                accountField.addEventListener('focus', function(e) {
                    // Clear validation state on focus
                    e.target.classList.remove('is-valid', 'is-invalid');
                });
            }
            
            // Full name
            const nameField = document.querySelector('input[name="FullName"]');
            if (nameField) {
                let nameTimeout;
                
                nameField.addEventListener('input', function(e) {
                    // Only validate after user has interacted (blur event)
                    // clearTimeout(nameTimeout);
                    // nameTimeout = setTimeout(() => {
                    //     validateField(e.target, 'FullName', validators.fullName);
                    // }, 500);
                });
                
                nameField.addEventListener('blur', function(e) {
                    validateField(e.target, 'FullName', validators.fullName);
                });
                
                nameField.addEventListener('focus', function(e) {
                    // Clear validation state on focus
                    e.target.classList.remove('is-valid', 'is-invalid');
                });
            }
            
            // Branch selection
            const branchField = document.querySelector('select[name="BranchId"]');
            if (branchField) {
                branchField.addEventListener('change', function(e) {
                    validateField(e.target, 'BranchId', {
                        validate: (value) => {
                            if (!value || value === '') {
                                return { valid: false, message: 'กรุณาเลือกสาขา / Please select a branch' };
                            }
                            return { valid: true, message: 'เลือกสาขาแล้ว / Branch selected' };
                        }
                    });
                });
            }
        }
        
        function validateField(field, fieldName, validator) {
            const result = validator.validate(field.value);
            const fieldGroup = field.closest('.form-group');
            
            // Remove existing validation feedback
            field.classList.remove('is-valid', 'is-invalid');
            const existingFeedback = fieldGroup.querySelector('.bc-field-feedback');
            if (existingFeedback) {
                existingFeedback.remove();
            }
            
            if (!field.value.trim()) {
                validFields.delete(fieldName);
                return;
            }
            
            // Create feedback element
            const feedback = document.createElement('div');
            feedback.className = 'bc-field-feedback';
            
            if (result.valid) {
                field.classList.add('is-valid');
                feedback.className += ' bc-field-feedback-success';
                feedback.innerHTML = `<i class="fas fa-check-circle"></i> ${result.message}`;
                validFields.add(fieldName);
                
                // Success animation
                field.style.transform = 'scale(1.02)';
                setTimeout(() => field.style.transform = '', 200);
            } else {
                field.classList.add('is-invalid');
                feedback.className += ' bc-field-feedback-error';
                feedback.innerHTML = `<i class="fas fa-exclamation-triangle"></i> ${result.message}`;
                validFields.delete(fieldName);
                
                // Error shake animation
                field.style.animation = 'inputShake 0.5s ease-in-out';
                setTimeout(() => field.style.animation = '', 500);
            }
            
            fieldGroup.appendChild(feedback);
        }
        
        // Enhanced form submission
        function setupFormSubmission() {
            const submitBtn = document.getElementById('submitBtn');
            
            form.addEventListener('submit', function(e) {
                e.preventDefault();
                
                // Final validation
                const allFieldsValid = validFields.size >= requiredFields.length - 1; // Allow some flexibility
                
                if (!allFieldsValid) {
                    showEnhancedNotification(
                        'กรุณากรอกข้อมูลให้ครบถ้วนและถูกต้อง / Please complete all required fields correctly',
                        'error',
                        { duration: 6000 }
                    );
                    
                    // Focus first invalid field
                    const firstInvalidField = form.querySelector('.is-invalid');
                    if (firstInvalidField) {
                        firstInvalidField.focus();
                        firstInvalidField.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                    
                    // Reset button state if validation fails
                    resetSubmissionLoading(submitBtn);
                    return;
                }
                
                // Show enhanced loading state
                showSubmissionLoading(submitBtn);
                
                // No progress tracking needed
                
                showEnhancedNotification(
                    'กำลังดำเนินการ กรุณารอสักครู่... / Processing your request, please wait...',
                    'info',
                    { duration: 0, showCloseButton: false }
                );
                
                // Simulate processing time with progress
                setTimeout(() => {
                    form.submit();
                }, 2000);
            });
        }
        
        function showSubmissionLoading(button) {
            const btnContent = button.querySelector('.btn-content');
            const btnLoading = button.querySelector('.btn-loading');
            
            if (btnContent && btnLoading) {
                btnContent.classList.add('d-none');
                btnLoading.classList.remove('d-none');
                button.disabled = true;
            } else {
                // Fallback if structure is different
                button.disabled = true;
                button.innerHTML = `
                    <div class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></div>
                    <span>กำลังดำเนินการ... / Processing...</span>
                `;
            }
        }
        
        function resetSubmissionLoading(button) {
            const btnContent = button.querySelector('.btn-content');
            const btnLoading = button.querySelector('.btn-loading');
            
            if (btnContent && btnLoading) {
                btnContent.classList.remove('d-none');
                btnLoading.classList.add('d-none');
            }
            
            button.disabled = false;
        }
        
        // Initialize all components
        initLanguageSupport();
        setupFieldValidation();
        setupFormSubmission();
        
        // Show welcome message
        setTimeout(() => {
            const currentLang = localStorage.getItem('preferredLanguage') || 'th';
            const welcomeMessage = currentLang === 'th' ? 
                'ยินดีต้อนรับสู่ระบบลงทะเบียน KBank ODD' : 
                'Welcome to KBank ODD Registration';
            showEnhancedNotification(welcomeMessage, 'info', { duration: 4000 });
        }, 1000);
    }
    
    // Add notification styles
    function addNotificationStyles() {
        if (document.getElementById('bc-notification-styles')) return;
        
        const styles = document.createElement('style');
        styles.id = 'bc-notification-styles';
        styles.textContent = `
            .bc-notification-container {
                position: fixed;
                z-index: 9999;
                pointer-events: none;
            }
            
            .bc-notification-container.top-right {
                top: 1rem;
                right: 1rem;
            }
            
            .bc-notification {
                background: white;
                border-radius: 12px;
                box-shadow: 0 10px 40px rgba(0, 0, 0, 0.15);
                margin-bottom: 1rem;
                max-width: 400px;
                pointer-events: auto;
                position: relative;
                overflow: hidden;
                opacity: 0;
                transform: translateX(100%);
                transition: all 0.4s cubic-bezier(0.4, 0, 0.2, 1);
            }
            
            .bc-notification.bc-notification-show {
                opacity: 1;
                transform: translateX(0);
            }
            
            .bc-notification.bc-notification-hide {
                opacity: 0;
                transform: translateX(100%);
            }
            
            .bc-notification-content {
                display: flex;
                align-items: center;
                padding: 1rem 1.25rem;
                gap: 0.75rem;
            }
            
            .bc-notification-icon {
                font-size: 1.25rem;
                flex-shrink: 0;
            }
            
            .bc-notification-message {
                flex: 1;
                font-weight: 500;
                font-size: 0.9rem;
                line-height: 1.4;
            }
            
            .bc-notification-close {
                background: none;
                border: none;
                color: inherit;
                cursor: pointer;
                padding: 0.25rem;
                border-radius: 4px;
                opacity: 0.7;
                transition: opacity 0.2s;
                flex-shrink: 0;
            }
            
            .bc-notification-close:hover {
                opacity: 1;
            }
            
            .bc-notification-progress {
                position: absolute;
                bottom: 0;
                left: 0;
                height: 3px;
                background: rgba(255, 255, 255, 0.3);
                animation: notificationProgress linear forwards;
            }
            
            .bc-notification-success {
                background: linear-gradient(135deg, #E8F5E8 0%, #C8E6C9 100%);
                color: #2E7D32;
                border-left: 4px solid #4CAF50;
            }
            
            .bc-notification-error {
                background: linear-gradient(135deg, #FFEBEE 0%, #FFCDD2 100%);
                color: #C62828;
                border-left: 4px solid #F44336;
            }
            
            .bc-notification-warning {
                background: linear-gradient(135deg, #FFF8E1 0%, #FFECB3 100%);
                color: #F57C00;
                border-left: 4px solid #FF9800;
            }
            
            .bc-notification-info {
                background: linear-gradient(135deg, #E3F2FD 0%, #BBDEFB 100%);
                color: #1976D2;
                border-left: 4px solid #2196F3;
            }
            
            .bc-field-feedback {
                display: flex;
                align-items: center;
                gap: 0.5rem;
                margin-top: 0.5rem;
                font-size: 0.875rem;
                font-weight: 500;
                animation: slideInUp 0.3s ease-out;
            }
            
            .bc-field-feedback-success {
                color: #2E7D32;
            }
            
            .bc-field-feedback-error {
                color: #C62828;
            }
            
            @keyframes notificationProgress {
                from { width: 100%; }
                to { width: 0%; }
            }
            
            @keyframes slideInUp {
                from {
                    opacity: 0;
                    transform: translateY(10px);
                }
                to {
                    opacity: 1;
                    transform: translateY(0);
                }
            }
            
            @media (max-width: 480px) {
                .bc-notification-container.top-right {
                    top: 0.5rem;
                    right: 0.5rem;
                    left: 0.5rem;
                }
                
                .bc-notification {
                    max-width: none;
                    transform: translateY(-100%);
                }
                
                .bc-notification.bc-notification-show {
                    transform: translateY(0);
                }
                
                .bc-notification.bc-notification-hide {
                    transform: translateY(-100%);
                }
            }
        `;
        
        document.head.appendChild(styles);
    }
    
    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() {
            addNotificationStyles();
            initializeEnhancedForm();
        });
    } else {
        addNotificationStyles();
        initializeEnhancedForm();
    }
    
})();