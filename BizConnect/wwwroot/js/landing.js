/**
 * BizConnect Landing Page JavaScript
 * Handles OTAC input formatting, form validation, animations, and user interactions
 */

(function() {
    'use strict';

    // Configuration
    const CONFIG = {
        OTAC_MIN_LENGTH: 6,
        OTAC_MAX_LENGTH: 8,
        OTAC_PATTERN: /^[A-Z0-9]+$/,
        ANIMATION_DURATION: 300,
        STATS_ANIMATION_DURATION: 2000,
        DEBOUNCE_DELAY: 300
    };

    // State management
    const state = {
        isFormSubmitting: false,
        statsAnimated: false,
        otacValidated: false
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
         * Animate number counting
         */
        animateNumber: function(element, target, duration = 2000, decimals = 0) {
            const start = 0;
            const increment = target / (duration / 16);
            let current = start;

            const timer = setInterval(() => {
                current += increment;
                if (current >= target) {
                    current = target;
                    clearInterval(timer);
                }
                element.textContent = decimals > 0 ? current.toFixed(decimals) : Math.floor(current);
            }, 16);
        },

        /**
         * Check if element is in viewport
         */
        isInViewport: function(element) {
            const rect = element.getBoundingClientRect();
            return (
                rect.top >= 0 &&
                rect.left >= 0 &&
                rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
                rect.right <= (window.innerWidth || document.documentElement.clientWidth)
            );
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

    // OTAC Input Handler
    const otacHandler = {
        init: function() {
            const otacInput = document.getElementById('OtacCode');
            if (!otacInput) return;

            this.setupEventListeners(otacInput);
            this.setupValidation(otacInput);
        },

        setupEventListeners: function(input) {
            // Format input on keyup
            input.addEventListener('input', (e) => this.formatInput(e.target));
            
            // Validate on blur
            input.addEventListener('blur', (e) => this.validateInput(e.target));
            
            // Handle paste events
            input.addEventListener('paste', (e) => {
                setTimeout(() => this.formatInput(e.target), 10);
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
            this.updateInputState(input, value);
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

            this.updateValidationUI(input, isValid);
            state.otacValidated = isValid;

            return isValid;
        },

        isValidOtac: function(value) {
            return value.length >= CONFIG.OTAC_MIN_LENGTH && 
                   value.length <= CONFIG.OTAC_MAX_LENGTH && 
                   CONFIG.OTAC_PATTERN.test(value);
        },

        updateInputState: function(input, value) {
            // Update character count feedback
            const lengthFeedback = document.querySelector('.otac-length-feedback');
            if (lengthFeedback) {
                lengthFeedback.textContent = `${value.length}/${CONFIG.OTAC_MAX_LENGTH}`;
            }

            // Add visual feedback for completion
            if (value.length >= CONFIG.OTAC_MIN_LENGTH) {
                input.classList.add('is-valid');
                input.classList.remove('is-invalid');
            } else if (value.length > 0) {
                input.classList.add('is-invalid');
                input.classList.remove('is-valid');
            } else {
                input.classList.remove('is-valid', 'is-invalid');
            }
        },

        updateValidationUI: function(input, isValid) {
            const submitBtn = document.getElementById('verifyBtn');
            
            if (isValid) {
                input.classList.add('is-valid');
                input.classList.remove('is-invalid');
                if (submitBtn) submitBtn.disabled = false;
            } else if (input.value.length > 0) {
                input.classList.add('is-invalid');
                input.classList.remove('is-valid');
                if (submitBtn) submitBtn.disabled = true;
            }
        },

        setupValidation: function(input) {
            // Real-time validation with debouncing
            const debouncedValidation = utils.debounce(() => {
                this.validateInput(input);
            }, CONFIG.DEBOUNCE_DELAY);

            input.addEventListener('input', debouncedValidation);
        }
    };

    // Form Handler
    const formHandler = {
        init: function() {
            const form = document.getElementById('otacForm');
            if (!form) return;

            this.setupFormSubmission(form);
            this.setupButtonStates();
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
                utils.showNotification('กรุณากรอกรหัส OTAC ที่ถูกต้อง', 'danger');
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
            // Submit the actual form
            setTimeout(() => {
                form.submit();
            }, 500); // Small delay for better UX
        },

        setupButtonStates: function() {
            const submitBtn = document.getElementById('verifyBtn');
            const otacInput = document.getElementById('OtacCode');

            if (!submitBtn || !otacInput) return;

            // Initial state
            submitBtn.disabled = !otacHandler.isValidOtac(otacInput.value);

            // Update state based on input changes
            otacInput.addEventListener('input', () => {
                if (!state.isFormSubmitting) {
                    submitBtn.disabled = !otacHandler.isValidOtac(otacInput.value);
                }
            });
        }
    };

    // Statistics Animation
    const statsAnimator = {
        init: function() {
            this.setupIntersectionObserver();
        },

        setupIntersectionObserver: function() {
            const statsSection = document.querySelector('.stats-section');
            if (!statsSection) return;

            const observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting && !state.statsAnimated) {
                        this.animateStats();
                        state.statsAnimated = true;
                    }
                });
            }, {
                threshold: 0.3
            });

            observer.observe(statsSection);
        },

        animateStats: function() {
            const statNumbers = document.querySelectorAll('.stat-number[data-target]');
            
            statNumbers.forEach((element, index) => {
                const target = parseFloat(element.getAttribute('data-target'));
                const decimals = target % 1 !== 0 ? 1 : 0;
                
                setTimeout(() => {
                    utils.animateNumber(element, target, CONFIG.STATS_ANIMATION_DURATION, decimals);
                    element.classList.add('fade-in-up');
                }, index * 200);
            });
        }
    };

    // Smooth Scrolling
    const smoothScroller = {
        init: function() {
            this.setupSmoothScrolling();
            this.setupScrollToTop();
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
        },

        setupScrollToTop: function() {
            // Create scroll to top button
            const scrollBtn = document.createElement('button');
            scrollBtn.innerHTML = '<i class="fas fa-arrow-up"></i>';
            scrollBtn.className = 'btn btn-primary rounded-circle position-fixed d-none';
            scrollBtn.style.cssText = 'bottom: 20px; right: 20px; z-index: 999; width: 50px; height: 50px;';
            scrollBtn.setAttribute('aria-label', 'กลับไปด้านบน');
            
            document.body.appendChild(scrollBtn);

            // Show/hide based on scroll position
            window.addEventListener('scroll', () => {
                if (window.pageYOffset > 300) {
                    scrollBtn.classList.remove('d-none');
                } else {
                    scrollBtn.classList.add('d-none');
                }
            });

            // Scroll to top on click
            scrollBtn.addEventListener('click', () => {
                window.scrollTo({
                    top: 0,
                    behavior: 'smooth'
                });
            });
        }
    };

    // Intersection Observer for Animations
    const animationObserver = {
        init: function() {
            this.setupObserver();
        },

        setupObserver: function() {
            const observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        entry.target.classList.add('fade-in-up');
                    }
                });
            }, {
                threshold: 0.1,
                rootMargin: '0px 0px -50px 0px'
            });

            // Observe elements that should animate on scroll
            document.querySelectorAll('.process-step, .accordion-item, .stat-item').forEach(el => {
                observer.observe(el);
            });
        }
    };

    // Process Step Interactions
    const processSteps = {
        init: function() {
            this.setupHoverEffects();
            this.setupClickToScroll();
        },

        setupHoverEffects: function() {
            document.querySelectorAll('.process-step').forEach(step => {
                step.addEventListener('mouseenter', () => {
                    step.style.transform = 'translateY(-10px) scale(1.02)';
                });

                step.addEventListener('mouseleave', () => {
                    step.style.transform = 'translateY(0) scale(1)';
                });
            });
        },

        setupClickToScroll: function() {
            // Clicking on step 2 (OTAC verification) scrolls to form
            const step2 = document.querySelector('.process-step:nth-child(2)');
            if (step2) {
                step2.style.cursor = 'pointer';
                step2.addEventListener('click', () => {
                    document.getElementById('otacForm')?.scrollIntoView({
                        behavior: 'smooth'
                    });
                });
            }
        }
    };

    // FAQ Enhancements
    const faqEnhancements = {
        init: function() {
            this.setupAccordionTracking();
            this.addSearchFunctionality();
        },

        setupAccordionTracking: function() {
            document.querySelectorAll('.accordion-button').forEach(button => {
                button.addEventListener('click', () => {
                    // Track FAQ interactions (can be extended for analytics)
                    const question = button.textContent.trim();
                    console.log('FAQ clicked:', question);
                });
            });
        },

        addSearchFunctionality: function() {
            // Could add FAQ search functionality here in the future
            // This would filter FAQ items based on search terms
        }
    };

    // Accessibility Enhancements
    const accessibilityHelper = {
        init: function() {
            this.setupKeyboardNavigation();
            this.setupAriaLabels();
            this.setupFocusManagement();
        },

        setupKeyboardNavigation: function() {
            // Enhanced keyboard navigation for form
            const otacInput = document.getElementById('OtacCode');
            const submitBtn = document.getElementById('verifyBtn');

            if (otacInput && submitBtn) {
                otacInput.addEventListener('keydown', (e) => {
                    if (e.key === 'Enter' && otacHandler.isValidOtac(otacInput.value)) {
                        e.preventDefault();
                        submitBtn.click();
                    }
                });
            }
        },

        setupAriaLabels: function() {
            // Add appropriate ARIA labels
            const otacInput = document.getElementById('OtacCode');
            if (otacInput) {
                otacInput.setAttribute('aria-describedby', 'otac-help');
                
                // Create help text element if it doesn't exist
                if (!document.getElementById('otac-help')) {
                    const helpText = document.createElement('div');
                    helpText.id = 'otac-help';
                    helpText.className = 'visually-hidden';
                    helpText.textContent = 'กรอกรหัส OTAC 6-8 ตัวอักษร ประกอบด้วยตัวอักษรภาษาอังกฤษตัวใหญ่และตัวเลข';
                    otacInput.parentNode.appendChild(helpText);
                }
            }
        },

        setupFocusManagement: function() {
            // Focus management for better accessibility
            const form = document.getElementById('otacForm');
            if (form) {
                form.addEventListener('submit', () => {
                    // Prevent focus loss during submission
                    const submitBtn = document.getElementById('verifyBtn');
                    if (submitBtn) {
                        submitBtn.setAttribute('aria-busy', 'true');
                    }
                });
            }
        }
    };

    // Error Handling
    const errorHandler = {
        init: function() {
            this.setupGlobalErrorHandling();
        },

        setupGlobalErrorHandling: function() {
            window.addEventListener('error', (e) => {
                console.error('Landing page error:', e.error);
                // Could send error to logging service
            });
        },

        handleFormError: function(error) {
            utils.showNotification('เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง', 'danger');
            
            // Reset form state
            const submitBtn = document.getElementById('verifyBtn');
            if (submitBtn) {
                formHandler.hideLoadingState(submitBtn);
            }
        }
    };

    // Language toggle functionality
    const languageToggle = {
        init: function() {
            const langTH = document.getElementById('langTH');
            const langEN = document.getElementById('langEN');
            
            if (langTH && langEN) {
                langTH.addEventListener('click', () => this.switchLanguage('th'));
                langEN.addEventListener('click', () => this.switchLanguage('en'));
            }
        },

        switchLanguage: function(lang) {
            const langTH = document.getElementById('langTH');
            const langEN = document.getElementById('langEN');
            
            if (langTH && langEN) {
                langTH.className = lang === 'th' ? 'btn btn-secondary btn-sm' : 'btn btn-outline-secondary btn-sm';
                langEN.className = lang === 'en' ? 'btn btn-secondary btn-sm' : 'btn btn-outline-secondary btn-sm';
            }
            
            // Store language preference
            sessionStorage.setItem('language', lang);
            localStorage.setItem('preferredLanguage', lang);
            
            // Show notification
            utils.showNotification(lang === 'th' ? 'เปลี่ยนเป็นภาษาไทยแล้ว' : 'Changed to English', 'success');
        }
    };

    // Main initialization
    function initLandingPage() {
        // Initialize for both landing page and any page that needs these features
        const hasOtacForm = document.getElementById('otacForm');
        const isLandingPage = document.querySelector('.hero-section') || hasOtacForm;

        // Initialize all modules
        otacHandler.init();
        formHandler.init();
        languageToggle.init();
        statsAnimator.init();
        smoothScroller.init();
        animationObserver.init();
        processSteps.init();
        faqEnhancements.init();
        accessibilityHelper.init();
        errorHandler.init();

        console.log('BizConnect Landing Page initialized successfully');
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initLandingPage);
    } else {
        initLandingPage();
    }

    // Expose utility functions globally for debugging
    if (window.location.hostname === 'localhost') {
        window.BizConnectLanding = {
            utils,
            otacHandler,
            formHandler,
            languageToggle,
            state
        };
    }

})();