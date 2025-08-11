/**
 * KBank Registration Form JavaScript
 * Extracted and optimized from inline scripts
 * Dependencies: Bootstrap 5, FontAwesome
 */

(function() {
    'use strict';

    // Main initialization
    document.addEventListener('DOMContentLoaded', function() {
        initializeRegistrationForm();
    });

    function initializeRegistrationForm() {
        const form = document.getElementById('registerForm');
        const submitBtn = document.getElementById('submitBtn');
        const agreeTerms = document.getElementById('agreeTerms');

        if (!form || !submitBtn || !agreeTerms) {
            console.error('Required form elements not found');
            return;
        }

        // Form validation configuration - National ID only
        const nationalIdValidator = {
            pattern: /^[0-9]{13}$/,
            message: window.validationMessages?.nationalIdValidation || 'Please enter a valid 13-digit National ID',
            hint: window.validationMessages?.nationalIdHintValidation || 'Thai National ID only - enter 13 digits'
        };

        // Initialize input formatting and validation
        initializeInputs(form);
        
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

        // Real-time validation for all inputs
        const inputs = form.querySelectorAll('input[required], select[required]');
        inputs.forEach(input => {
            input.addEventListener('blur', () => validateField(input));
            input.addEventListener('input', () => clearValidationState(input));
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
                errorMessage = messages.fullNameValidation || 'Please enter a valid full name';
                break;

            case 'IdValue':
                // Always National ID - validate 13 digits
                isValid = /^[0-9]{13}$/.test(value);
                errorMessage = messages.nationalIdValidation || 'Please enter a valid 13-digit National ID';
                break;

            case 'MobileNo':
                isValid = /^0[89][0-9]{8}$/.test(value);
                errorMessage = messages.mobileValidation || 'Please enter a valid mobile number (08XXXXXXXX or 09XXXXXXXX)';
                break;

            case 'AccountNo':
                isValid = /^[0-9]{10,15}$/.test(value);
                errorMessage = messages.accountValidation || 'Please enter a valid account number (10-15 digits)';
                break;

            case 'BranchId':
                isValid = value !== '';
                errorMessage = messages.branchValidation || 'Please select a branch';
                break;
        }

        updateFieldValidation(field, isValid, errorMessage);
        return isValid;
    }

    function updateFieldValidation(field, isValid, errorMessage) {
        const feedback = field.parentNode.querySelector('.invalid-feedback') || 
                       field.parentNode.parentNode.querySelector('.text-danger');
        
        if (isValid) {
            field.classList.remove('is-invalid');
            field.classList.add('is-valid');
            if (feedback) feedback.textContent = '';
        } else {
            field.classList.remove('is-valid');
            field.classList.add('is-invalid');
            if (feedback) feedback.textContent = errorMessage;
        }
    }

    function clearValidationState(field) {
        field.classList.remove('is-valid', 'is-invalid');
    }

    function validateForm(form, agreeTerms) {
        const inputs = form.querySelectorAll('input[required], select[required]');
        let isFormValid = true;

        inputs.forEach(input => {
            if (!validateField(input)) {
                isFormValid = false;
            }
        });

        // Check terms agreement
        if (!agreeTerms.checked) {
            agreeTerms.classList.add('is-invalid');
            isFormValid = false;
        } else {
            agreeTerms.classList.remove('is-invalid');
        }

        if (!isFormValid) {
            const warningMessage = window.validationMessages?.checkFormData || 'Please check your form data';
            showAlert('warning', warningMessage);
            
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
        }
        const modal = bootstrap.Modal.getInstance(document.getElementById('privacyModal'));
        if (modal) {
            modal.hide();
        }
    };

    // Alert helper function - exposed globally for server-side use
    window.showAlert = function(type, message) {
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
        alertDiv.style.cssText = 'top: 20px; right: 20px; z-index: 1060; min-width: 300px; max-width: 500px;';
        
        const iconClass = type === 'success' ? 'check-circle' : 
                         type === 'danger' ? 'exclamation-triangle' : 
                         'info-circle';
        
        alertDiv.innerHTML = `
            <i class="fas fa-${iconClass} me-2"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
        `;
        
        document.body.appendChild(alertDiv);

        // Auto-remove after 6 seconds
        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.classList.remove('show');
                setTimeout(() => alertDiv.remove(), 150);
            }
        }, 6000);
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

})();