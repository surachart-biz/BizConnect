/**
 * KBank Registration Form JavaScript
 * Extracted and optimized from inline scripts
 * Dependencies: Bootstrap 5, FontAwesome
 */

(function() {
    'use strict';

    // Main initialization
    document.addEventListener('DOMContentLoaded', function() {
        // Clean up any orphaned alert elements from previous page loads
        cleanupOrphanedAlerts();
        initializeRegistrationForm();
    });
    
    // Clean up when page is about to unload
    window.addEventListener('beforeunload', function() {
        cleanupOrphanedAlerts();
    });
    
    // Clean up when page becomes hidden (for mobile/tab switching)
    document.addEventListener('visibilitychange', function() {
        if (document.hidden) {
            cleanupOrphanedAlerts();
        }
    });

    // Function to clean up orphaned alert elements
    function cleanupOrphanedAlerts() {
        // Remove any orphaned alert elements that might be stuck at the top of the page
        const orphanedAlerts = document.querySelectorAll('div.alert.position-fixed, div.alert[style*="position: fixed"], div.alert[style*="top: 20px"], .sr-only[data-announcement="true"]');
        orphanedAlerts.forEach(alert => {
            if (alert.parentNode) {
                alert.remove();
            }
        });
        
        // Also clean up any notification containers that might be empty
        const notificationContainers = document.querySelectorAll('#notificationContainer, .notification-container');
        notificationContainers.forEach(container => {
            if (container && container.children.length === 0) {
                container.remove();
            }
        });
        
        // Clean up any orphaned alert elements with data attributes
        const dataAlerts = document.querySelectorAll('[data-alert-id*="auto-generated"]');
        dataAlerts.forEach(alert => {
            if (alert.parentNode) {
                alert.remove();
            }
        });
    }

    function initializeRegistrationForm() {
        const form = document.getElementById('registerForm');
        const submitBtn = document.getElementById('submitBtn');
        const agreeTerms = document.getElementById('agreeTerms');

        if (!form || !submitBtn || !agreeTerms) {
            console.error('Required form elements not found');
            return;
        }
        
        // Initialize keyboard navigation
        initializeKeyboardNavigation();

        // Form validation configuration - National ID only
        const nationalIdValidator = {
            pattern: /^[0-9]{13}$/,
            message: window.validationMessages?.nationalIdValidation || 'Please enter a valid 13-digit National ID',
            hint: window.validationMessages?.nationalIdHintValidation || 'Thai National ID only - enter 13 digits'
        };

        // Initialize input formatting and validation
        initializeInputs(form);
        
        // Terms checkbox validation handler
        agreeTerms.addEventListener('change', function() {
            validateTermsCheckbox(agreeTerms);
        });
        
        // Form submission handler
        form.addEventListener('submit', function(e) {
            e.preventDefault();
            
            if (validateForm(form, agreeTerms)) {
                submitBtn.disabled = true;
                const originalText = submitBtn.innerHTML;
                const submittingText = window.validationMessages?.submittingData || 'Submitting data...';
                submitBtn.innerHTML = `<span class="loading-spinner me-2"></span>${submittingText}`;
                
                // Submit form after validation
                setTimeout(() => {
                    form.submit();
                }, 500);
            }
        });
    }

    function initializeInputs(form) {
        // Setup for National ID only (no dropdown)
        const idValueInput = document.getElementById('idValueInput');
        const mobileInput = document.getElementById('mobileInput');
        const accountInput = document.getElementById('accountInput');

        // ID Value input formatting (National ID only - 13 digits)
        if (idValueInput) {
            idValueInput.addEventListener('input', function() {
                // Always National ID - only allow 13 digits
                let value = this.value.replace(/[^0-9]/g, '').substring(0, 13);
                this.value = value;
                validateField(this);
            });
        }

        // Mobile number formatting
        if (mobileInput) {
            mobileInput.addEventListener('input', function() {
                let value = this.value.replace(/[^0-9]/g, '');
                
                // Auto-add 0 prefix if not present
                if (value.length > 0 && !value.startsWith('0')) {
                    if (value.startsWith('8') || value.startsWith('9')) {
                        value = '0' + value;
                    }
                }
                
                // Limit length
                value = value.substring(0, 10);
                this.value = value;
                validateField(this);
            });
        }

        // Account number formatting
        if (accountInput) {
            accountInput.addEventListener('input', function() {
                let value = this.value.replace(/[^0-9]/g, '').substring(0, 15);
                this.value = value;
                validateField(this);
            });
        }

        // Ensure all validation errors are hidden initially on page load
        const allErrorElements = form.querySelectorAll('.field-validation-error, .text-danger, .invalid-feedback');
        allErrorElements.forEach(element => {
            element.style.display = 'none';
            element.classList.remove('show');
        });

        // Setup validation only on blur (after user finishes input) and form submission
        const inputs = form.querySelectorAll('input[required], select[required]');
        inputs.forEach(input => {
            // Ensure input doesn't have invalid state initially
            input.classList.remove('is-invalid', 'is-valid');
            
            // Only validate on blur (when user leaves the field)
            input.addEventListener('blur', () => {
                if (input.value.trim() !== '') {
                    validateField(input);
                }
            });
            // Clear validation state when user starts typing
            input.addEventListener('input', () => {
                clearValidationState(input);
                // Also clear any server-side error messages
                clearServerValidationErrors(input);
            });
            // Clear validation errors when user focuses on field
            input.addEventListener('focus', () => {
                clearValidationState(input);
                clearServerValidationErrors(input);
            });
        });
    }

    function validateField(field) {
        const value = field.value.trim();
        const fieldName = field.name || field.id;
        let isValid = true;
        let errorMessage = '';

        // Get validation messages from window object (set by server-side)
        const messages = window.validationMessages || {};

        switch (fieldName) {
            case 'FullName':
                isValid = value.length >= 2 && value.length <= 200;
                errorMessage = messages.fullNameValidation || 'กรุณากรอกชื่อ-นามสกุล / Please enter your full name';
                break;

            case 'IdValue':
                // Always National ID - validate 13 digits
                isValid = /^[0-9]{13}$/.test(value);
                errorMessage = messages.nationalIdValidation || 'กรุณากรอกเลขบัตรประชาชน 13 หลัก / Please enter a valid 13-digit National ID';
                break;

            case 'MobileNo':
                isValid = /^0[89][0-9]{8}$/.test(value);
                errorMessage = messages.mobileValidation || 'กรุณากรอกหมายเลขมือถือที่ถูกต้อง / Please enter a valid mobile number (08XXXXXXXX or 09XXXXXXXX)';
                break;

            case 'AccountNo':
                isValid = /^[0-9]{10,15}$/.test(value);
                errorMessage = messages.accountValidation || 'กรุณากรอกเลขที่บัญชี 10-15 หลัก / Please enter a valid account number (10-15 digits)';
                break;

            case 'BranchId':
                isValid = value !== '';
                errorMessage = messages.branchValidation || 'กรุณาเลือกสาขา / Please select a branch';
                break;
        }

        updateFieldValidation(field, isValid, errorMessage);
        
        // Announce validation change to screen readers
        if (field.value.trim() !== '') {
            announceValidationChange(field, isValid, errorMessage);
        }
        
        return isValid;
    }

    function updateFieldValidation(field, isValid, errorMessage) {
        const feedback = field.parentNode.querySelector('.field-validation-error') || 
                       field.parentNode.querySelector('.invalid-feedback') || 
                       field.parentNode.parentNode.querySelector('.text-danger');
        
        if (isValid) {
            field.classList.remove('is-invalid');
            field.classList.add('is-valid');
            if (feedback) {
                const textElement = feedback.querySelector('.validation-text') || feedback;
                if (textElement !== feedback) {
                    textElement.textContent = '';
                    textElement.style.display = 'none';
                } else {
                    feedback.textContent = '';
                }
                feedback.style.display = 'none';
                feedback.classList.remove('show');
            }
        } else {
            field.classList.remove('is-valid');
            field.classList.add('is-invalid');
            if (feedback) {
                const textElement = feedback.querySelector('.validation-text') || feedback;
                if (textElement !== feedback) {
                    // For elements with .validation-text span inside, show both the container and the text
                    textElement.textContent = errorMessage;
                    textElement.style.display = 'inline'; // Show the text element
                } else {
                    // For direct feedback elements, add icon if needed and set text
                    if (!feedback.innerHTML.includes('fa-exclamation-triangle')) {
                        feedback.innerHTML = '<i class="fas fa-exclamation-triangle me-1" aria-hidden="true"></i><span class="error-text">' + errorMessage + '</span>';
                    } else {
                        const errorSpan = feedback.querySelector('.error-text') || feedback.querySelector('span');
                        if (errorSpan) {
                            errorSpan.textContent = errorMessage;
                        } else {
                            feedback.innerHTML = '<i class="fas fa-exclamation-triangle me-1" aria-hidden="true"></i><span class="error-text">' + errorMessage + '</span>';
                        }
                    }
                }
                feedback.style.display = 'flex';
                feedback.classList.add('show');
            }
        }
    }

    function validateTermsCheckbox(agreeTerms) {
        const termsError = document.getElementById('agreeTermsError');
        
        if (agreeTerms.checked) {
            // Clear error when checked
            agreeTerms.classList.remove('is-invalid');
            if (termsError) {
                termsError.style.display = 'none';
                termsError.classList.remove('show');
            }
        } else {
            // Show error when unchecked
            agreeTerms.classList.add('is-invalid');
            if (termsError) {
                termsError.style.display = 'flex';
                termsError.classList.add('show');
            }
        }
    }

    function clearValidationState(field) {
        field.classList.remove('is-valid', 'is-invalid');
    }

    function clearServerValidationErrors(field) {
        // Clear server-side validation errors
        const feedback = field.parentNode.querySelector('.field-validation-error') ||
                       field.parentNode.querySelector('.text-danger') ||
                       field.parentNode.querySelector('.invalid-feedback');
        if (feedback) {
            feedback.style.display = 'none';
            feedback.classList.remove('show');
            const textElement = feedback.querySelector('.validation-text');
            if (textElement) {
                textElement.textContent = '';
                textElement.style.display = 'none';
            }
        }
    }

    function showServerValidationErrors(field) {
        // Show server-side validation errors if they exist
        const feedback = field.parentNode.querySelector('.field-validation-error') ||
                       field.parentNode.querySelector('.text-danger') ||
                       field.parentNode.querySelector('.invalid-feedback');
        if (feedback) {
            const textElement = feedback.querySelector('.validation-text') || feedback;
            if (textElement.textContent.trim() !== '') {
                if (textElement !== feedback) {
                    textElement.style.display = 'inline';
                }
                feedback.style.display = 'flex';
                feedback.classList.add('show');
            }
        }
    }

    function validateForm(form, agreeTerms) {
        const inputs = form.querySelectorAll('input[required], select[required]');
        let isFormValid = true;

        // First, show all server-side errors
        inputs.forEach(input => {
            showServerValidationErrors(input);
        });

        // Then validate each field, including empty fields
        inputs.forEach(input => {
            // For empty required fields, show appropriate validation message
            if (input.value.trim() === '' && input.hasAttribute('required')) {
                const fieldName = input.name || input.id;
                let errorMessage = '';
                
                // Get validation messages from window object (set by server-side)
                const messages = window.validationMessages || {};
                
                switch (fieldName) {
                    case 'FullName':
                        errorMessage = messages.fullNameValidation || 'กรุณากรอกชื่อ-นามสกุล / Please enter your full name';
                        break;
                    case 'IdValue':
                        errorMessage = messages.nationalIdValidation || 'กรุณากรอกเลขบัตรประชาชน 13 หลัก / Please enter a valid 13-digit National ID';
                        break;
                    case 'MobileNo':
                        errorMessage = messages.mobileValidation || 'กรุณากรอกหมายเลขมือถือ / Please enter your mobile number';
                        break;
                    case 'AccountNo':
                        errorMessage = messages.accountValidation || 'กรุณากรอกเลขที่บัญชี / Please enter your account number';
                        break;
                    case 'BranchId':
                        errorMessage = messages.branchValidation || 'กรุณาเลือกสาขา / Please select a branch';
                        break;
                }
                updateFieldValidation(input, false, errorMessage);
                isFormValid = false;
            } else if (!validateField(input)) {
                isFormValid = false;
            }
        });

        // Check terms agreement
        const termsError = document.getElementById('agreeTermsError');
        if (!agreeTerms.checked) {
            agreeTerms.classList.add('is-invalid');
            if (termsError) {
                termsError.style.display = 'flex';
                termsError.classList.add('show');
            }
            isFormValid = false;
        } else {
            agreeTerms.classList.remove('is-invalid');
            if (termsError) {
                termsError.style.display = 'none';
                termsError.classList.remove('show');
            }
        }

        if (!isFormValid) {
            // Use the multilingual notification banner instead of overriding text
            showNotificationMultilingual();
            
            // Focus on first invalid field
            const firstInvalid = form.querySelector('.is-invalid');
            if (firstInvalid) {
                firstInvalid.focus();
                firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }

        return isFormValid;
    }

    // Modal functions - exposed globally for onclick handlers
    window.acceptTerms = function() {
        const agreeTerms = document.getElementById('agreeTerms');
        if (agreeTerms) {
            agreeTerms.checked = true;
            // Trigger validation clearing when checkbox is programmatically checked
            validateTermsCheckbox(agreeTerms);
        }
        const modal = bootstrap.Modal.getInstance(document.getElementById('termsModal'));
        if (modal) {
            modal.hide();
        }
    };

    window.acceptPrivacy = function() {
        const agreeTerms = document.getElementById('agreeTerms');
        if (agreeTerms) {
            agreeTerms.checked = true;
            // Trigger validation clearing when checkbox is programmatically checked
            validateTermsCheckbox(agreeTerms);
        }
        const modal = bootstrap.Modal.getInstance(document.getElementById('privacyModal'));
        if (modal) {
            modal.hide();
        }
    };

    // Notification banner function with multilingual support
    window.showNotification = function(message, description = null) {
        const banner = document.getElementById('notificationBanner');
        if (!banner) return;
        
        const titleElement = banner.querySelector('.notification-title');
        const descElement = banner.querySelector('.notification-description');
        
        if (titleElement && message) {
            // Use provided message or keep existing multilingual attributes
            titleElement.textContent = message;
        }
        
        if (descElement) {
            if (description) {
                descElement.textContent = description;
                descElement.style.display = 'block';
            } else {
                // Keep the existing multilingual description
                descElement.style.display = 'block';
            }
        }
        
        banner.style.display = 'block';
        banner.setAttribute('aria-hidden', 'false');
        
        // Announce to screen readers
        banner.setAttribute('aria-live', 'polite');
        
        // Auto-hide after 10 seconds
        setTimeout(() => {
            hideNotification();
        }, 10000);
    };
    
    // Show notification with multilingual support
    window.showNotificationMultilingual = function() {
        const banner = document.getElementById('notificationBanner');
        if (!banner) return;
        
        // Don't override the multilingual attributes, just show the banner
        banner.style.display = 'block';
        banner.setAttribute('aria-hidden', 'false');
        banner.setAttribute('aria-live', 'polite');
        
        // Auto-hide after 10 seconds
        setTimeout(() => {
            hideNotification();
        }, 10000);
    };
    
    // Hide notification function
    window.hideNotification = function() {
        const banner = document.getElementById('notificationBanner');
        if (banner) {
            banner.style.display = 'none';
            banner.setAttribute('aria-hidden', 'true');
        }
    };
    
    // Legacy alert helper function for backward compatibility
    window.showAlert = function(type, message) {
        if (type === 'warning' || type === 'danger') {
            showNotification(message);
        } else {
            // Clean up any existing alerts first to prevent accumulation
            const existingAlerts = document.querySelectorAll('div.alert.position-fixed');
            existingAlerts.forEach(alert => alert.remove());
            
            // For success messages, still use the floating alert
            const alertDiv = document.createElement('div');
            alertDiv.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
            alertDiv.style.cssText = 'top: 20px; right: 20px; z-index: 1060; min-width: 300px; max-width: 500px;';
            alertDiv.setAttribute('data-alert-id', 'auto-generated-' + Date.now());
            
            const iconClass = type === 'success' ? 'check-circle' : 'info-circle';
            
            alertDiv.innerHTML = `
                <i class="fas fa-${iconClass} me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
            `;
            
            document.body.appendChild(alertDiv);

            // Enhanced cleanup with multiple fallbacks
            const cleanup = () => {
                if (alertDiv && alertDiv.parentNode) {
                    alertDiv.classList.remove('show');
                    setTimeout(() => {
                        if (alertDiv && alertDiv.parentNode) {
                            alertDiv.remove();
                        }
                    }, 150);
                }
            };
            
            // Auto-remove after 6 seconds
            const autoCleanupTimer = setTimeout(cleanup, 6000);
            
            // Also clean up when user clicks close button
            const closeBtn = alertDiv.querySelector('.btn-close');
            if (closeBtn) {
                closeBtn.addEventListener('click', () => {
                    clearTimeout(autoCleanupTimer);
                    cleanup();
                });
            }
        }
    };

    // Performance optimization: debounce function for input validation
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    // Enhanced input validation with debouncing for performance
    function enhanceInputValidation() {
        const inputs = document.querySelectorAll('input[required], select[required]');
        const debouncedValidate = debounce(validateField, 300);
        
        inputs.forEach(input => {
            input.addEventListener('input', () => {
                clearValidationState(input);
                debouncedValidate(input);
            });
        });
    }

    // Initialize enhanced validation if performance optimization is needed
    // enhanceInputValidation(); // Uncomment if needed
    
    // Keyboard navigation and accessibility enhancements
    function initializeKeyboardNavigation() {
        // Handle escape key to close notification
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                const banner = document.getElementById('notificationBanner');
                if (banner && banner.style.display !== 'none') {
                    hideNotification();
                }
            }
        });
        
        // Improve terms links keyboard accessibility
        const termsLinks = document.querySelectorAll('.terms-link');
        termsLinks.forEach(link => {
            link.addEventListener('keydown', function(e) {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    link.click();
                }
            });
        });
        
        // Enhance checkbox keyboard interaction
        const agreeTerms = document.getElementById('agreeTerms');
        if (agreeTerms) {
            agreeTerms.addEventListener('keydown', function(e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    agreeTerms.checked = !agreeTerms.checked;
                    
                    // Trigger validation update using the dedicated function
                    validateTermsCheckbox(agreeTerms);
                }
            });
        }
    }
    
    // Announce validation changes to screen readers
    function announceValidationChange(field, isValid, message) {
        // Clean up any existing sr-only announcements first
        const existingAnnouncements = document.querySelectorAll('.sr-only[data-announcement="true"]');
        existingAnnouncements.forEach(announcement => {
            if (announcement.parentNode) {
                announcement.remove();
            }
        });
        
        const announcement = document.createElement('div');
        announcement.setAttribute('aria-live', 'polite');
        announcement.setAttribute('aria-atomic', 'true');
        announcement.setAttribute('data-announcement', 'true');
        announcement.className = 'sr-only';
        announcement.textContent = isValid ? 
            `${field.labels[0]?.textContent || field.name} is valid` : 
            `${field.labels[0]?.textContent || field.name}: ${message}`;
        
        document.body.appendChild(announcement);
        
        // Enhanced cleanup with error handling
        setTimeout(() => {
            if (announcement && announcement.parentNode) {
                try {
                    announcement.remove();
                } catch (e) {
                    console.warn('Failed to remove announcement element:', e);
                }
            }
        }, 1000);
    }

})();