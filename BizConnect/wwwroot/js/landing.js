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
                const message = languageToggle.t('validOtacRequired') || 'กรุณากรอกรหัส OTAC ที่ถูกต้อง';
                utils.showNotification(message, 'danger');
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
            // Create scroll to top button with proper styling and positioning
            const scrollBtn = document.createElement('button');
            scrollBtn.innerHTML = '<i class="fas fa-arrow-up"></i>';
            scrollBtn.className = 'scroll-to-top-btn rounded-circle position-fixed d-none';
            scrollBtn.id = 'scrollToTopBtn';
            scrollBtn.style.cssText = `
                bottom: 20px; 
                right: 20px; 
                z-index: 1000; 
                width: 50px; 
                height: 50px;
                background: linear-gradient(135deg, #4CAF50, #388E3C);
                color: white;
                border: none;
                box-shadow: 0 4px 20px rgba(76, 175, 80, 0.3);
                transition: all 0.3s cubic-bezier(0.4, 0.0, 0.2, 1);
                font-size: 1.2rem;
                cursor: pointer;
            `;
            scrollBtn.setAttribute('aria-label', 'กลับไปด้านบน');
            scrollBtn.setAttribute('title', 'กลับไปด้านบน');
            
            // Add hover effects
            scrollBtn.addEventListener('mouseenter', () => {
                scrollBtn.style.transform = 'translateY(-3px) scale(1.1)';
                scrollBtn.style.boxShadow = '0 6px 25px rgba(76, 175, 80, 0.4)';
            });
            
            scrollBtn.addEventListener('mouseleave', () => {
                scrollBtn.style.transform = 'translateY(0) scale(1)';
                scrollBtn.style.boxShadow = '0 4px 20px rgba(76, 175, 80, 0.3)';
            });
            
            document.body.appendChild(scrollBtn);

            // Show/hide with smooth transitions based on scroll position
            let isVisible = false;
            window.addEventListener('scroll', utils.debounce(() => {
                const shouldShow = window.pageYOffset > 300;
                
                if (shouldShow && !isVisible) {
                    scrollBtn.classList.remove('d-none');
                    scrollBtn.style.opacity = '0';
                    scrollBtn.style.transform = 'translateY(10px) scale(0.8)';
                    
                    // Animate in
                    requestAnimationFrame(() => {
                        scrollBtn.style.transition = 'all 0.3s cubic-bezier(0.4, 0.0, 0.2, 1)';
                        scrollBtn.style.opacity = '1';
                        scrollBtn.style.transform = 'translateY(0) scale(1)';
                    });
                    isVisible = true;
                    
                } else if (!shouldShow && isVisible) {
                    scrollBtn.style.transition = 'all 0.3s cubic-bezier(0.4, 0.0, 0.2, 1)';
                    scrollBtn.style.opacity = '0';
                    scrollBtn.style.transform = 'translateY(10px) scale(0.8)';
                    
                    setTimeout(() => {
                        if (!isVisible) return; // Check if still should be hidden
                        scrollBtn.classList.add('d-none');
                    }, 300);
                    isVisible = false;
                }
            }, 100));

            // Scroll to top with smooth animation and click feedback
            scrollBtn.addEventListener('click', (e) => {
                // Click animation
                scrollBtn.style.transform = 'translateY(2px) scale(0.95)';
                
                setTimeout(() => {
                    scrollBtn.style.transform = 'translateY(0) scale(1)';
                }, 150);
                
                // Smooth scroll to top
                window.scrollTo({
                    top: 0,
                    behavior: 'smooth'
                });
                
                // Hide immediately after click
                scrollBtn.style.opacity = '0.7';
                setTimeout(() => {
                    scrollBtn.style.opacity = '1';
                }, 300);
                
                e.preventDefault();
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
            const message = languageToggle.getCurrentLanguage() === 'th' 
                ? 'เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง' 
                : 'An error occurred. Please try again.';
            utils.showNotification(message, 'danger');
            
            // Reset form state
            const submitBtn = document.getElementById('verifyBtn');
            if (submitBtn) {
                formHandler.hideLoadingState(submitBtn);
            }
        }
    };

    // Enhanced Language toggle functionality with UI translation
    const languageToggle = {
        currentLanguage: 'th',
        translations: {
            'th': {
                // Header elements
                'signin': 'เข้าสู่ระบบ',
                'signout': 'ออกจากระบบ',
                'adminDashboard': 'แผงควบคุมผู้ดูแล',
                'profile': 'จัดการโปรไฟล์',
                
                // Hero section
                'heroTitle': 'ลงทะเบียนหักบัญชีอัตโนมัติ',
                'heroSubtitle': 'สะดวก รวดเร็ว และปลอดภัย ด้วยระบบ OTAC จาก KBank สำหรับการลงทะเบียนบริการหักบัญชีอัตโนมัติ',
                'secureBadge': 'ระบบปลอดภัย รับรองโดย KBank',
                
                // OTAC Form
                'otacLabel': 'รหัส OTAC',
                'otacPlaceholder': 'ABC12345',
                'verifyButton': 'ยืนยันและดำเนินการต่อ',
                'verifyingText': 'กำลังตรวจสอบ...',
                
                // Login Modal
                'staffLogin': 'เข้าสู่ระบบเจ้าหน้าที่',
                'staffOnly': 'สำหรับเจ้าหน้าที่และผู้ดูแลระบบเท่านั้น',
                'username': 'ชื่อผู้ใช้งาน',
                'password': 'รหัสผ่าน',
                'signingIn': 'กำลังตรวจสอบ...',
                'demoAccounts': 'บัญชีทดสอบ:',
                'enterUsername': 'กรุณากรอกชื่อผู้ใช้งาน',
                'enterPassword': 'กรุณากรอกรหัสผ่าน',
                
                // Notifications
                'languageChanged': 'เปลี่ยนเป็นภาษาไทยแล้ว',
                'loginSuccess': 'เข้าสู่ระบบสำเร็จ!',
                'loginError': 'ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง',
                'logoutSuccess': 'ออกจากระบบเรียบร้อยแล้ว',
                'fillAllFields': 'กรุณากรอกข้อมูลให้ครบถ้วน',
                'validOtacRequired': 'กรุณากรอกรหัส OTAC ที่ถูกต้อง'
            },
            'en': {
                // Header elements
                'signin': 'Sign In',
                'signout': 'Sign Out',
                'adminDashboard': 'Admin Dashboard',
                'profile': 'Manage Profile',
                
                // Hero section
                'heroTitle': 'Automatic Direct Debit Registration',
                'heroSubtitle': 'Convenient, Fast, and Secure with KBank OTAC system for automatic direct debit service registration',
                'secureBadge': 'Secure System Certified by KBank',
                
                // OTAC Form
                'otacLabel': 'OTAC Code',
                'otacPlaceholder': 'ABC12345',
                'verifyButton': 'Verify and Continue',
                'verifyingText': 'Verifying...',
                
                // Login Modal
                'staffLogin': 'Staff Login',
                'staffOnly': 'For staff and system administrators only',
                'username': 'Username',
                'password': 'Password',
                'signingIn': 'Signing in...',
                'demoAccounts': 'Demo Accounts:',
                'enterUsername': 'Please enter username',
                'enterPassword': 'Please enter password',
                
                // Notifications
                'languageChanged': 'Changed to English',
                'loginSuccess': 'Login successful!',
                'loginError': 'Invalid username or password',
                'logoutSuccess': 'Logged out successfully',
                'fillAllFields': 'Please fill in all fields',
                'validOtacRequired': 'Please enter a valid OTAC code'
            }
        },

        init: function() {
            const langTH = document.getElementById('langTH');
            const langEN = document.getElementById('langEN');
            
            if (langTH && langEN) {
                langTH.addEventListener('click', (e) => {
                    e.preventDefault();
                    this.switchLanguage('th');
                });
                langEN.addEventListener('click', (e) => {
                    e.preventDefault();
                    this.switchLanguage('en');
                });
            }

            // Load saved language preference
            const savedLang = localStorage.getItem('preferredLanguage') || 'th';
            this.switchLanguage(savedLang, false);
        },

        switchLanguage: function(lang, showNotification = true) {
            this.currentLanguage = lang;
            
            // Update language toggle buttons
            const langTH = document.getElementById('langTH');
            const langEN = document.getElementById('langEN');
            
            if (langTH && langEN) {
                langTH.className = lang === 'th' ? 'lang-btn active' : 'lang-btn';
                langEN.className = lang === 'en' ? 'lang-btn active' : 'lang-btn';
            }
            
            // Update all elements with data-text attributes
            this.updateElementsText(lang);
            
            // Store language preference
            sessionStorage.setItem('language', lang);
            localStorage.setItem('preferredLanguage', lang);
            
            // Show notification
            if (showNotification) {
                utils.showNotification(this.translations[lang].languageChanged, 'success');
            }
        },

        updateElementsText: function(lang) {
            const elements = document.querySelectorAll('[data-text-th][data-text-en]');
            
            elements.forEach(element => {
                const text = lang === 'th' ? element.dataset.textTh : element.dataset.textEn;
                if (text) {
                    // Handle different element types
                    if (element.tagName === 'INPUT') {
                        if (element.type === 'text' || element.type === 'password') {
                            element.placeholder = text;
                        } else {
                            element.value = text;
                        }
                    } else if (element.tagName === 'A' && element.classList.contains('btn')) {
                        // Handle buttons with spans
                        const span = element.querySelector('span');
                        if (span) {
                            span.textContent = text;
                        } else {
                            // Fallback - update full text but preserve icons
                            const icon = element.querySelector('i');
                            if (icon) {
                                element.innerHTML = '';
                                element.appendChild(icon);
                                element.appendChild(document.createTextNode(' ' + text));
                            } else {
                                element.textContent = text;
                            }
                        }
                    } else if (element.classList.contains('signin-text') || element.classList.contains('menu-text')) {
                        // Handle specific text spans within buttons/menus
                        element.textContent = text;
                    } else {
                        // Handle regular text elements
                        element.textContent = text;
                    }
                }
            });

            // Update specific dynamic content
            this.updateDynamicContent(lang);
        },

        updateDynamicContent: function(lang) {
            const translations = this.translations[lang];
            
            // Update OTAC form elements that might not have data attributes
            const otacLabel = document.querySelector('.input-label');
            if (otacLabel) {
                otacLabel.textContent = translations.otacLabel;
            }

            const verifyBtn = document.getElementById('verifyBtn');
            if (verifyBtn) {
                const btnText = verifyBtn.querySelector('.btn-text');
                if (btnText) {
                    btnText.innerHTML = `<i class="fas fa-arrow-right"></i> ${translations.verifyButton}`;
                }
                
                const spinnerText = verifyBtn.querySelector('.btn-spinner');
                if (spinnerText) {
                    spinnerText.innerHTML = `<span class="spinner-border spinner-border-sm me-2" role="status"></span> ${translations.verifyingText}`;
                }
            }
        },

        getCurrentLanguage: function() {
            return this.currentLanguage;
        },

        t: function(key) {
            return this.translations[this.currentLanguage][key] || key;
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
        authIntegration.init();
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
                ? languageToggle.t('enterUsername')
                : languageToggle.t('enterPassword');

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

    // Expose utility functions globally for debugging
    if (window.location.hostname === 'localhost') {
        window.BizConnectLanding = {
            utils,
            otacHandler,
            formHandler,
            languageToggle,
            authIntegration,
            state
        };
    }

    // Make language toggle available globally for auth.js integration
    window.BizConnectLanguage = languageToggle;

})();