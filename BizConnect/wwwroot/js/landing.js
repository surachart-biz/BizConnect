/**
 * BizConnect Landing Page JavaScript
 * Handles OTAC input formatting, blur-based validation, multilingual support, and user interactions
 * Updated: No progress tracking, blur-based validation only
 */

(function() {
    'use strict';

    // Configuration
    const CONFIG = {
        OTAC_MIN_LENGTH: 6,
        OTAC_MAX_LENGTH: 8,
        OTAC_PATTERN: /^[A-Z0-9]+$/,
        ANIMATION_DURATION: 300,
        DEBOUNCE_DELAY: 300
    };

    // State management
    const state = {
        isFormSubmitting: false,
        currentLanguage: 'th'
    };

    // Utility functions
    const utils = {
        /**
         * Debounce function to limit function calls
         */
        debounce: function(func, delay) {
            let timeoutId;
            return function (...args) {
                clearTimeout(timeoutId);
                timeoutId = setTimeout(() => func.apply(this, args), delay);
            };
        },

        /**
         * Show notification message
         */
        showNotification: function(message, type = 'info') {
            // Create notification element
            const notification = document.createElement('div');
            notification.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
            notification.style.cssText = 'top: 20px; right: 20px; z-index: 9999; max-width: 300px;';
            notification.innerHTML = `
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;

            document.body.appendChild(notification);

            // Auto-remove after 5 seconds
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 5000);
        }
    };

    // Enhanced Language System with Real-Time Switching
    const BizConnectLanguage = {
        currentLanguage: 'th',
        
        init: function() {
            // Load saved language preference
            const savedLang = localStorage.getItem('preferredLanguage') || 'th';
            this.switchLanguage(savedLang, false);
            
            // Bind language toggle events
            document.getElementById('langTH')?.addEventListener('click', (e) => {
                e.preventDefault();
                this.switchLanguage('th');
            });
            
            document.getElementById('langEN')?.addEventListener('click', (e) => {
                e.preventDefault();
                this.switchLanguage('en');
            });
        },

        switchLanguage: function(lang, showNotification = true) {
            this.currentLanguage = lang;
            state.currentLanguage = lang;
            
            // Update toggle buttons
            document.querySelectorAll('.lang-btn').forEach(btn => btn.classList.remove('active'));
            document.getElementById('lang' + lang.toUpperCase())?.classList.add('active');
            
            // Update all elements with bilingual data attributes
            this.updateElementsText(lang);
            
            // Update form placeholders
            this.updatePlaceholders(lang);
            
            // Store language preference
            localStorage.setItem('preferredLanguage', lang);
            sessionStorage.setItem('language', lang);
            
            if (showNotification) {
                const message = lang === 'th' ? 'เปลี่ยนเป็นภาษาไทยแล้ว' : 'Changed to English';
                this.showNotification(message, 'success');
            }
        },

        updateElementsText: function(lang) {
            const elements = document.querySelectorAll('[data-text-th][data-text-en]');
            elements.forEach(element => {
                const text = lang === 'th' ? element.getAttribute('data-text-th') : element.getAttribute('data-text-en');
                if (text) {
                    if (element.tagName === 'INPUT' && (element.type === 'button' || element.type === 'submit')) {
                        element.value = text;
                    } else {
                        element.textContent = text;
                    }
                }
            });
        },

        updatePlaceholders: function(lang) {
            const elements = document.querySelectorAll('[data-placeholder-th][data-placeholder-en]');
            elements.forEach(element => {
                const placeholder = lang === 'th' ? element.getAttribute('data-placeholder-th') : element.getAttribute('data-placeholder-en');
                if (placeholder && element.placeholder !== undefined) {
                    element.placeholder = placeholder;
                }
            });
        },

        getCurrentLanguage: function() {
            return this.currentLanguage;
        },

        t: function(key) {
            const translations = {
                'th': {
                    'validOtacRequired': 'กรุณากรอกรหัส OTAC ที่ถูกต้อง',
                    'languageChanged': 'เปลี่ยนเป็นภาษาไทยแล้ว',
                    'processingRequest': 'กำลังดำเนินการ กรุณารอสักครู่...',
                    'enterUsername': 'กรุณากรอกชื่อผู้ใช้งาน',
                    'enterPassword': 'กรุณากรอกรหัสผ่าน'
                },
                'en': {
                    'validOtacRequired': 'Please enter a valid OTAC code',
                    'languageChanged': 'Changed to English',
                    'processingRequest': 'Processing your request, please wait...',
                    'enterUsername': 'Please enter username',
                    'enterPassword': 'Please enter password'
                }
            };
            return translations[this.currentLanguage][key] || key;
        },

        showNotification: function(message, type = 'info') {
            const notification = document.createElement('div');
            notification.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
            notification.style.cssText = 'top: 20px; right: 20px; z-index: 9999; max-width: 300px;';
            notification.innerHTML = `
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;
            
            document.body.appendChild(notification);
            
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 5000);
        }
    };

    // Enhanced OTAC Handler with Blur-Based Validation
    const otacHandler = {
        init: function() {
            const otacInput = document.getElementById('OtacCode');
            if (!otacInput) return;
            
            this.setupEventListeners(otacInput);
        },

        setupEventListeners: function(input) {
            let hasUserInteracted = false;
            
            // Format input on keyup
            input.addEventListener('input', (e) => {
                this.formatInput(e.target);
                // Clear validation state on input (neutral state)
                if (hasUserInteracted) {
                    e.target.classList.remove('is-valid', 'is-invalid');
                }
            });
            
            // Validate only on blur after user interaction
            input.addEventListener('blur', (e) => {
                hasUserInteracted = true;
                this.validateInput(e.target);
            });
            
            // Clear validation state on focus
            input.addEventListener('focus', (e) => {
                e.target.classList.remove('is-valid', 'is-invalid');
            });
            
            // Handle paste events
            input.addEventListener('paste', (e) => {
                setTimeout(() => {
                    this.formatInput(e.target);
                    if (hasUserInteracted) {
                        e.target.classList.remove('is-valid', 'is-invalid');
                    }
                }, 10);
            });

            // Prevent invalid characters
            input.addEventListener('keypress', (e) => this.filterKeypress(e));
        },

        formatInput: function(input) {
            // Convert to uppercase and remove invalid characters
            let value = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
            
            // Limit length
            if (value.length > CONFIG.OTAC_MAX_LENGTH) {
                value = value.substring(0, CONFIG.OTAC_MAX_LENGTH);
            }

            input.value = value;
        },

        filterKeypress: function(e) {
            const char = String.fromCharCode(e.which);
            if (!CONFIG.OTAC_PATTERN.test(char) && !['Backspace', 'Delete', 'Tab', 'Enter'].includes(e.key)) {
                e.preventDefault();
            }
        },

        validateInput: function(input) {
            const value = input.value.trim();
            const isValid = this.isValidOtac(value);

            // Apply validation styling only after blur
            if (isValid) {
                input.classList.remove('is-invalid');
                input.classList.add('is-valid');
            } else if (value.length > 0) {
                input.classList.remove('is-valid');
                input.classList.add('is-invalid');
            } else {
                input.classList.remove('is-valid', 'is-invalid');
            }

            return isValid;
        },

        isValidOtac: function(value) {
            return value.length >= CONFIG.OTAC_MIN_LENGTH && 
                   value.length <= CONFIG.OTAC_MAX_LENGTH && 
                   CONFIG.OTAC_PATTERN.test(value);
        }
    };

    // Enhanced Form Handler with Proper Validation
    const formHandler = {
        init: function() {
            const form = document.getElementById('otacForm');
            if (!form) return;

            this.setupFormSubmission(form);
        },

        setupFormSubmission: function(form) {
            form.addEventListener('submit', (e) => this.handleSubmit(e));
        },

        handleSubmit: function(e) {
            e.preventDefault();
            
            if (state.isFormSubmitting) return;

            const form = e.target;
            const otacInput = form.querySelector('#OtacCode');
            const submitBtn = form.querySelector('#verifyBtn');

            // Validate OTAC before submission
            if (!otacHandler.validateInput(otacInput)) {
                const message = BizConnectLanguage.t('validOtacRequired');
                BizConnectLanguage.showNotification(message, 'danger');
                otacInput.focus();
                return;
            }

            this.showLoadingState(submitBtn);
            this.submitForm(form);
        },

        showLoadingState: function(button) {
            if (!button) return;

            state.isFormSubmitting = true;
            button.disabled = true;
            
            const btnText = button.querySelector('.btn-text');
            const btnSpinner = button.querySelector('.btn-spinner');
            
            if (btnText) btnText.classList.add('d-none');
            if (btnSpinner) btnSpinner.classList.remove('d-none');
        },

        hideLoadingState: function(button) {
            if (!button) return;

            state.isFormSubmitting = false;
            button.disabled = false;
            
            const btnText = button.querySelector('.btn-text');
            const btnSpinner = button.querySelector('.btn-spinner');
            
            if (btnText) btnText.classList.remove('d-none');
            if (btnSpinner) btnSpinner.classList.add('d-none');
        },

        submitForm: function(form) {
            // Show processing notification
            const message = BizConnectLanguage.t('processingRequest');
            BizConnectLanguage.showNotification(message, 'info');
            
            // Submit the actual form
            setTimeout(() => {
                form.submit();
            }, 500); // Small delay for better UX
        }
    };

    // Smooth Scrolling
    const smoothScroller = {
        init: function() {
            this.setupSmoothScrolling();
        },

        setupSmoothScrolling: function() {
            document.querySelectorAll('a[href^="#"]').forEach(anchor => {
                anchor.addEventListener('click', (e) => {
                    e.preventDefault();
                    const target = document.querySelector(anchor.getAttribute('href'));
                    if (target) {
                        target.scrollIntoView({
                            behavior: 'smooth',
                            block: 'start'
                        });
                    }
                });
            });
        }
    };

    // Authentication Integration
    const authIntegration = {
        init: function() {
            console.log('🔄 Landing: Initializing authentication integration...');
            
            // CRITICAL: Force unauthenticated state first
            this.forceUnauthenticatedState();
            
            // Setup password visibility toggle
            this.setupPasswordToggle();
            
            // Setup login form validation
            this.setupLoginValidation();
            
            // NOTE: Do NOT add sign-in button event handlers here
            // The sign-in button modal functionality is handled in Index.cshtml
            
            // Delay auth check to let auth.js initialize first
            setTimeout(() => {
                this.checkAuthStatus();
            }, 250);
        },

        // Force UI to unauthenticated state immediately
        forceUnauthenticatedState: function() {
            console.log('🚨 Landing: Forcing unauthenticated state...');
            
            const signInContainer = document.getElementById('signInContainer');
            const userDropdownContainer = document.getElementById('userDropdownContainer');
            
            if (signInContainer) {
                signInContainer.classList.remove('d-none', 'hide-signin');
                signInContainer.style.display = 'flex';
                console.log('✅ Landing: Sign-in container forced visible');
            }
            
            if (userDropdownContainer) {
                userDropdownContainer.classList.add('d-none');
                userDropdownContainer.classList.remove('show-user');
                userDropdownContainer.style.display = 'none';
                console.log('✅ Landing: User dropdown container forced hidden');
            }
        },

        checkAuthStatus: function() {
            console.log('🔍 Landing: Checking authentication status...');
            
            // First ensure we start with unauthenticated state
            this.forceUnauthenticatedState();
            
            // Check if auth.js has loaded and has auth status
            if (window.authManager && typeof window.authManager.isLoggedIn === 'function') {
                const isLoggedIn = window.authManager.isLoggedIn();
                console.log('🔍 Landing: Auth manager status check - logged in:', isLoggedIn);
                
                if (isLoggedIn) {
                    const userData = window.authManager.getCurrentUser();
                    console.log('🔓 Landing: Auth manager detected logged-in user:', userData);
                    if (userData && userData.username) {
                        this.updateUIForLoggedInUser(userData);
                    } else {
                        console.log('⚠️ Landing: Invalid user data, forcing unauthenticated');
                        this.updateUI(null);
                    }
                } else {
                    console.log('🔒 Landing: User not logged in');
                    this.updateUI(null);
                }
            } else {
                // Ensure UI shows sign-in by default when no auth manager
                console.log('🔒 Landing: No auth manager available, showing sign-in state');
                this.updateUI(null);
            }
        },

        updateUI: function(userData) {
            console.log('🔄 Landing: Updating UI state for user:', userData);
            
            const signInContainer = document.getElementById('signInContainer');
            const userDropdownContainer = document.getElementById('userDropdownContainer');
            const userName = document.getElementById('userName');

            if (userData && userData.username) {
                console.log('🔓 Landing: Showing authenticated UI');
                
                // Show user dropdown container, hide sign in container
                if (signInContainer) {
                    signInContainer.classList.add('d-none', 'hide-signin');
                    signInContainer.style.display = 'none';
                }
                if (userDropdownContainer) {
                    userDropdownContainer.classList.remove('d-none');
                    userDropdownContainer.classList.add('show-user');
                    userDropdownContainer.style.display = 'flex';
                }
                if (userName) {
                    userName.textContent = userData.username;
                }
            } else {
                console.log('🔒 Landing: Showing unauthenticated UI');
                
                // Show sign in container, hide user dropdown container
                if (signInContainer) {
                    signInContainer.classList.remove('d-none', 'hide-signin');
                    signInContainer.style.display = 'flex';
                }
                if (userDropdownContainer) {
                    userDropdownContainer.classList.add('d-none');
                    userDropdownContainer.classList.remove('show-user');
                    userDropdownContainer.style.display = 'none';
                }
            }
        },

        setupPasswordToggle: function() {
            const toggleBtn = document.getElementById('togglePassword');
            const passwordInput = document.getElementById('password');
            const toggleIcon = document.getElementById('passwordToggleIcon');

            if (toggleBtn && passwordInput && toggleIcon) {
                toggleBtn.addEventListener('click', () => {
                    const isPassword = passwordInput.type === 'password';
                    passwordInput.type = isPassword ? 'text' : 'password';
                    toggleIcon.className = isPassword ? 'fas fa-eye-slash' : 'fas fa-eye';
                });
            }
        },

        setupLoginValidation: function() {
            const loginForm = document.getElementById('loginForm');
            if (loginForm) {
                // Add real-time validation
                const usernameField = document.getElementById('username');
                const passwordField = document.getElementById('password');

                if (usernameField) {
                    usernameField.addEventListener('blur', () => {
                        this.validateField(usernameField, 'username');
                    });
                }

                if (passwordField) {
                    passwordField.addEventListener('blur', () => {
                        this.validateField(passwordField, 'password');
                    });
                }
            }
        },

        validateField: function(field, type) {
            const isValid = field.value.trim().length > 0;
            const errorMessage = type === 'username' 
                ? BizConnectLanguage.t('enterUsername')
                : BizConnectLanguage.t('enterPassword');

            if (isValid) {
                field.classList.remove('is-invalid');
                field.classList.add('is-valid');
            } else if (field.value.length > 0) {
                field.classList.remove('is-valid');
                field.classList.add('is-invalid');
                // Update the invalid feedback text
                const feedback = field.parentElement.querySelector('.invalid-feedback span');
                if (feedback) {
                    feedback.textContent = errorMessage;
                }
            }
        },

        updateUIForLoggedInUser: function(userData) {
            console.log('🔓 Landing: Updating UI for logged-in user:', userData);
            
            const signInContainer = document.getElementById('signInContainer');
            const userDropdownContainer = document.getElementById('userDropdownContainer');
            const userName = document.getElementById('userName');

            if (signInContainer && userDropdownContainer && userName && userData.username) {
                signInContainer.classList.add('d-none', 'hide-signin');
                signInContainer.style.display = 'none';
                
                userDropdownContainer.classList.remove('d-none');
                userDropdownContainer.classList.add('show-user');
                userDropdownContainer.style.display = 'flex';
                
                userName.textContent = userData.username;
                
                console.log('✅ Landing: Updated UI for authenticated user');
            } else {
                console.log('⚠️ Landing: Missing elements or invalid user data, forcing unauthenticated');
                this.updateUI(null);
            }
        },

        showLoginError: function(message) {
            const errorAlert = document.getElementById('loginErrorAlert');
            const errorMessage = document.getElementById('loginErrorMessage');
            
            if (errorAlert && errorMessage) {
                errorMessage.textContent = message;
                errorAlert.classList.remove('d-none');
                
                // Hide after 5 seconds
                setTimeout(() => {
                    errorAlert.classList.add('d-none');
                }, 5000);
            }
        }
    };

    // Main initialization
    function initLandingPage() {
        // Initialize all modules
        BizConnectLanguage.init();
        otacHandler.init();
        formHandler.init();
        smoothScroller.init();
        authIntegration.init();
        
        console.log('BizConnect Landing Page initialized successfully');
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initLandingPage);
    } else {
        initLandingPage();
    }

    // Make BizConnectLanguage globally available
    window.BizConnectLanguage = BizConnectLanguage;

    // Expose utility functions globally for debugging
    if (window.location.hostname === 'localhost') {
        window.BizConnectLanding = {
            utils,
            otacHandler,
            formHandler,
            BizConnectLanguage,
            authIntegration,
            state
        };
    }

})();