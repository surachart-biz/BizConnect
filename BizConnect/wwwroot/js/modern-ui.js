/**
 * BizConnect Modern UI System
 * Complete modern UI controller with glassmorphism, animations, and KBank branding
 * Compatible with Bootstrap 5 and includes Thai/English language support
 */

class ModernUI {
    constructor() {
        this.config = {
            animationDuration: 300,
            debounceDelay: 250,
            language: 'th',
            theme: 'light'
        };
        this.init();
    }

    /**
     * Initialize all modern UI components and systems
     */
    init() {
        this.setupLanguageSystem();
        this.setupAnimationSystem();
        this.setupFormValidation();
        this.setupScrollEffects();
        this.setupModernInteractions();
        this.setupNotifications();
        this.setupComponentEnhancements();
        this.setupPerformanceOptimizations();
        
        // Fire initialization complete event
        this.dispatchEvent('modernUI:ready', { timestamp: Date.now() });
    }

    // =================
    // LANGUAGE SYSTEM
    // =================
    
    setupLanguageSystem() {
        const langToggle = document.querySelector('.language-toggle-modern, .lang-toggle');
        if (!langToggle) return;

        // Load saved language preference
        this.config.language = localStorage.getItem('bizconnect-language') || 'th';
        this.applyLanguage(this.config.language);

        // Setup toggle handlers
        langToggle.addEventListener('click', (e) => {
            if (e.target.classList.contains('language-option') || e.target.classList.contains('lang-btn')) {
                e.preventDefault();
                const selectedLang = e.target.textContent.trim() === 'EN' ? 'en' : 'th';
                this.switchLanguage(selectedLang);
            }
        });
    }

    switchLanguage(language) {
        this.config.language = language;
        localStorage.setItem('bizconnect-language', language);
        this.applyLanguage(language);
        
        // Update toggle UI
        document.querySelectorAll('.language-option, .lang-btn').forEach(btn => {
            btn.classList.remove('active');
            if ((language === 'en' && btn.textContent.includes('EN')) ||
                (language === 'th' && btn.textContent.includes('TH'))) {
                btn.classList.add('active');
            }
        });

        // Fire language change event
        this.dispatchEvent('language:changed', { language });
    }

    applyLanguage(language) {
        // Update document language
        document.documentElement.lang = language;

        // Handle data attribute translations
        document.querySelectorAll('[data-th][data-en]').forEach(el => {
            const text = el.getAttribute(`data-${language}`);
            if (text) {
                el.textContent = text;
            }
        });

        // Update direction for Thai/English (both are LTR)
        document.documentElement.dir = 'ltr';
        
        // Apply language-specific font
        document.body.className = document.body.className.replace(/lang-\w+/, '');
        document.body.classList.add(`lang-${language}`);
    }

    // =================
    // ANIMATION SYSTEM
    // =================

    setupAnimationSystem() {
        // Intersection Observer for scroll-triggered animations
        const observerOptions = {
            threshold: [0.1, 0.25, 0.5],
            rootMargin: '0px 0px -50px 0px'
        };

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    this.triggerScrollAnimation(entry.target, entry.intersectionRatio);
                }
            });
        }, observerOptions);

        // Observe elements for animations
        this.observeElements(observer, [
            '.stats-widget', '.glass-card', '.modern-card', 
            '.service-card', '.feature-item', '.hero-features .feature-item',
            '.process-step', '.faq-item'
        ]);

        // Setup stagger animations
        this.setupStaggerAnimations();
    }

    observeElements(observer, selectors) {
        selectors.forEach(selector => {
            document.querySelectorAll(selector).forEach(el => {
                observer.observe(el);
            });
        });
    }

    triggerScrollAnimation(element, ratio = 1) {
        const delay = Array.from(element.parentNode?.children || []).indexOf(element) * 100;
        
        setTimeout(() => {
            element.classList.add('animate-in');
            element.style.transform = 'translateY(0)';
            element.style.opacity = '1';
        }, delay);
    }

    setupStaggerAnimations() {
        document.querySelectorAll('.stagger-children').forEach(parent => {
            const children = parent.children;
            Array.from(children).forEach((child, index) => {
                child.style.animationDelay = `${index * 0.1}s`;
                child.classList.add('fade-in-up');
            });
        });
    }

    // Counter animation for statistics
    animateCounter(element, start = 0, end, duration = 2000) {
        const startTime = Date.now();
        const range = end - start;

        const updateCounter = () => {
            const elapsed = Date.now() - startTime;
            const progress = Math.min(elapsed / duration, 1);
            
            // Easing function (ease-out cubic)
            const easeOutCubic = 1 - Math.pow(1 - progress, 3);
            const current = Math.round(start + range * easeOutCubic);
            
            element.textContent = this.formatNumber(current);
            
            if (progress < 1) {
                requestAnimationFrame(updateCounter);
            }
        };

        requestAnimationFrame(updateCounter);
    }

    // =================
    // FORM VALIDATION
    // =================

    setupFormValidation() {
        document.querySelectorAll('form').forEach(form => {
            this.enhanceForm(form);
        });
    }

    enhanceForm(form) {
        // Prevent double submission
        form.addEventListener('submit', (e) => {
            if (form.classList.contains('submitting')) {
                e.preventDefault();
                return false;
            }

            if (!this.validateForm(form)) {
                e.preventDefault();
                return false;
            }

            this.setFormSubmitting(form, true);
        });

        // Real-time validation
        form.querySelectorAll('input, select, textarea').forEach(field => {
            field.addEventListener('blur', () => this.validateField(field));
            field.addEventListener('input', this.debounce(() => this.validateField(field), 300));
        });
    }

    validateForm(form) {
        const fields = form.querySelectorAll('input[required], select[required], textarea[required]');
        let isValid = true;

        fields.forEach(field => {
            if (!this.validateField(field)) {
                isValid = false;
            }
        });

        // Custom validations
        if (form.dataset.formType === 'otac-verification') {
            const otacField = form.querySelector('input[name="otacCode"]');
            if (otacField && !this.validateOtacCode(otacField.value)) {
                this.setFieldError(otacField, 'Invalid OTAC code format. Must be 8 characters.');
                isValid = false;
            }
        }

        return isValid;
    }

    validateField(field) {
        const value = field.value.trim();
        const isRequired = field.hasAttribute('required');
        
        // Clear previous errors
        this.clearFieldError(field);

        // Required validation
        if (isRequired && !value) {
            this.setFieldError(field, this.getErrorMessage(field, 'required'));
            return false;
        }

        // Type-specific validation
        if (value) {
            switch (field.type) {
                case 'email':
                    if (!this.isValidEmail(value)) {
                        this.setFieldError(field, this.getErrorMessage(field, 'email'));
                        return false;
                    }
                    break;
                case 'tel':
                    if (!this.isValidPhone(value)) {
                        this.setFieldError(field, this.getErrorMessage(field, 'phone'));
                        return false;
                    }
                    break;
            }

            // Custom validations
            if (field.name === 'nationalId') {
                if (!this.isValidThaiNationalId(value)) {
                    this.setFieldError(field, this.getErrorMessage(field, 'nationalId'));
                    return false;
                }
            }
        }

        // Set success state
        field.classList.add('is-valid');
        return true;
    }

    validateOtacCode(code) {
        return /^[A-Z0-9]{8}$/.test(code);
    }

    isValidEmail(email) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    }

    isValidPhone(phone) {
        const cleaned = phone.replace(/\D/g, '');
        return cleaned.length >= 9 && cleaned.length <= 11;
    }

    isValidThaiNationalId(id) {
        const cleaned = id.replace(/\D/g, '');
        if (cleaned.length !== 13) return false;
        
        // Thai national ID check digit validation
        let sum = 0;
        for (let i = 0; i < 12; i++) {
            sum += parseInt(cleaned[i]) * (13 - i);
        }
        const checkDigit = (11 - (sum % 11)) % 10;
        return checkDigit === parseInt(cleaned[12]);
    }

    setFieldError(field, message) {
        field.classList.remove('is-valid');
        field.classList.add('is-invalid');
        
        let errorDiv = field.parentNode.querySelector('.invalid-feedback');
        if (!errorDiv) {
            errorDiv = document.createElement('div');
            errorDiv.className = 'invalid-feedback';
            field.parentNode.appendChild(errorDiv);
        }
        errorDiv.textContent = message;
    }

    clearFieldError(field) {
        field.classList.remove('is-invalid', 'is-valid');
        const errorDiv = field.parentNode.querySelector('.invalid-feedback');
        if (errorDiv) {
            errorDiv.remove();
        }
    }

    getErrorMessage(field, type) {
        const messages = {
            th: {
                required: 'กรุณากรอกข้อมูลในช่องนี้',
                email: 'กรุณากรอกอีเมลให้ถูกต้อง',
                phone: 'กรุณากรอกเบอร์โทรศัพท์ให้ถูกต้อง',
                nationalId: 'กรุณากรอกเลขบัตรประชาชนให้ถูกต้อง'
            },
            en: {
                required: 'This field is required',
                email: 'Please enter a valid email address',
                phone: 'Please enter a valid phone number',
                nationalId: 'Please enter a valid national ID'
            }
        };

        return field.dataset[`error${type.charAt(0).toUpperCase() + type.slice(1)}`] || 
               messages[this.config.language][type] || 
               messages.en[type];
    }

    setFormSubmitting(form, submitting) {
        if (submitting) {
            form.classList.add('submitting');
            const submitBtn = form.querySelector('button[type="submit"], input[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = true;
                this.setButtonLoading(submitBtn, true);
            }
        } else {
            form.classList.remove('submitting');
            const submitBtn = form.querySelector('button[type="submit"], input[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = false;
                this.setButtonLoading(submitBtn, false);
            }
        }
    }

    // =================
    // SCROLL EFFECTS
    // =================

    setupScrollEffects() {
        let lastScrollTop = 0;
        const header = document.querySelector('.modern-header, .admin-topbar');
        
        if (header) {
            window.addEventListener('scroll', this.throttle(() => {
                const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
                
                // Auto-hide header on scroll down
                if (scrollTop > lastScrollTop && scrollTop > 100) {
                    header.style.transform = 'translateY(-100%)';
                } else {
                    header.style.transform = 'translateY(0)';
                }
                
                lastScrollTop = scrollTop;
                
                // Add scrolled class for styling
                header.classList.toggle('scrolled', scrollTop > 50);
            }, 100));
        }

        // Parallax effects for hero sections
        this.setupParallaxEffects();
    }

    setupParallaxEffects() {
        const parallaxElements = document.querySelectorAll('.parallax-bg');
        
        if (parallaxElements.length === 0) return;

        window.addEventListener('scroll', this.throttle(() => {
            const scrolled = window.pageYOffset;
            
            parallaxElements.forEach(element => {
                const rate = scrolled * -0.5;
                element.style.transform = `translateY(${rate}px)`;
            });
        }, 16)); // 60fps
    }

    // =================
    // MODERN INTERACTIONS
    // =================

    setupModernInteractions() {
        this.setupRippleEffects();
        this.setupHoverEffects();
        this.setupSmoothScrolling();
        this.setupTooltips();
    }

    setupRippleEffects() {
        document.addEventListener('click', (e) => {
            const target = e.target.closest('.btn-modern, .modern-nav-link, .action-btn, .language-option');
            if (!target) return;

            this.createRipple(e, target);
        });
    }

    createRipple(event, element) {
        const ripple = document.createElement('span');
        ripple.className = 'ripple-effect';
        
        const rect = element.getBoundingClientRect();
        const size = Math.max(rect.width, rect.height);
        const x = event.clientX - rect.left - size / 2;
        const y = event.clientY - rect.top - size / 2;
        
        ripple.style.width = ripple.style.height = size + 'px';
        ripple.style.left = x + 'px';
        ripple.style.top = y + 'px';
        
        element.style.position = 'relative';
        element.style.overflow = 'hidden';
        element.appendChild(ripple);
        
        setTimeout(() => ripple.remove(), 600);
    }

    setupHoverEffects() {
        // Enhanced card hover effects
        document.querySelectorAll('.glass-card, .modern-card, .stats-widget').forEach(card => {
            card.addEventListener('mouseenter', () => {
                card.style.transform = 'translateY(-4px) scale(1.02)';
            });
            
            card.addEventListener('mouseleave', () => {
                card.style.transform = 'translateY(0) scale(1)';
            });
        });

        // Magnetic effect for important buttons
        document.querySelectorAll('.btn-kbank, .btn-primary-modern').forEach(btn => {
            btn.addEventListener('mousemove', (e) => {
                const rect = btn.getBoundingClientRect();
                const x = e.clientX - rect.left - rect.width / 2;
                const y = e.clientY - rect.top - rect.height / 2;
                
                btn.style.transform = `translate(${x * 0.1}px, ${y * 0.1}px)`;
            });
            
            btn.addEventListener('mouseleave', () => {
                btn.style.transform = 'translate(0, 0)';
            });
        });
    }

    setupSmoothScrolling() {
        document.addEventListener('click', (e) => {
            const anchor = e.target.closest('a[href^="#"]');
            if (!anchor) return;

            e.preventDefault();
            const target = document.querySelector(anchor.getAttribute('href'));
            if (target) {
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    }

    setupTooltips() {
        // Initialize Bootstrap tooltips
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(tooltipTriggerEl => {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    // =================
    // NOTIFICATION SYSTEM
    // =================

    setupNotifications() {
        this.notificationContainer = this.createNotificationContainer();
    }

    createNotificationContainer() {
        let container = document.querySelector('.notification-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'notification-container';
            container.style.cssText = `
                position: fixed;
                top: 100px;
                right: 20px;
                z-index: 1080;
                max-width: 400px;
                pointer-events: none;
            `;
            document.body.appendChild(container);
        }
        return container;
    }

    showNotification(type = 'info', title, message, duration = 5000) {
        const notification = document.createElement('div');
        notification.className = `toast-modern toast-${type}`;
        notification.style.pointerEvents = 'auto';
        
        const icons = {
            success: 'fa-check-circle',
            error: 'fa-exclamation-circle', 
            warning: 'fa-exclamation-triangle',
            info: 'fa-info-circle'
        };

        notification.innerHTML = `
            <div class="toast-header">
                <i class="fas ${icons[type]} me-2"></i>
                <strong class="me-auto">${title}</strong>
                <button type="button" class="btn-close" data-bs-dismiss="toast"></button>
            </div>
            <div class="toast-body">${message}</div>
        `;

        this.notificationContainer.appendChild(notification);

        // Auto remove
        setTimeout(() => {
            notification.style.opacity = '0';
            notification.style.transform = 'translateX(100%)';
            setTimeout(() => notification.remove(), 300);
        }, duration);

        // Manual close
        notification.querySelector('.btn-close')?.addEventListener('click', () => {
            notification.style.opacity = '0';
            notification.style.transform = 'translateX(100%)';
            setTimeout(() => notification.remove(), 300);
        });

        return notification;
    }

    // =================
    // COMPONENT ENHANCEMENTS
    // =================

    setupComponentEnhancements() {
        this.enhanceProgressBars();
        this.enhanceDropdowns();
        this.enhanceModals();
        this.enhanceTables();
    }

    enhanceProgressBars() {
        const progressBars = document.querySelectorAll('.progress-bar-modern[data-value]');
        
        // Animate progress bars when they become visible
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const bar = entry.target;
                    const value = bar.dataset.value;
                    
                    setTimeout(() => {
                        bar.style.width = value;
                    }, 300);
                    
                    observer.unobserve(bar);
                }
            });
        });

        progressBars.forEach(bar => observer.observe(bar));
    }

    enhanceDropdowns() {
        document.querySelectorAll('.dropdown-modern').forEach(dropdown => {
            const menu = dropdown.querySelector('.dropdown-menu');
            if (menu) {
                menu.addEventListener('show.bs.dropdown', () => {
                    menu.style.animationDelay = '0ms';
                });
            }
        });
    }

    enhanceModals() {
        document.querySelectorAll('.modal-modern').forEach(modal => {
            modal.addEventListener('show.bs.modal', () => {
                modal.querySelector('.modal-dialog')?.classList.add('modal-show');
            });

            modal.addEventListener('hidden.bs.modal', () => {
                modal.querySelector('.modal-dialog')?.classList.remove('modal-show');
            });
        });
    }

    enhanceTables() {
        document.querySelectorAll('.table-modern').forEach(table => {
            this.addTableInteractivity(table);
        });
    }

    addTableInteractivity(table) {
        // Sortable headers
        table.querySelectorAll('th[data-sort]').forEach(header => {
            header.style.cursor = 'pointer';
            header.addEventListener('click', () => {
                this.sortTable(table, header.dataset.sort, header);
            });
        });

        // Row hover effects
        table.querySelectorAll('tbody tr').forEach(row => {
            row.addEventListener('mouseenter', () => {
                row.style.backgroundColor = 'var(--glass-light)';
            });
            
            row.addEventListener('mouseleave', () => {
                row.style.backgroundColor = '';
            });
        });
    }

    // =================
    // PERFORMANCE OPTIMIZATIONS
    // =================

    setupPerformanceOptimizations() {
        // Lazy load images
        this.setupLazyLoading();
        
        // Preload critical resources
        this.preloadCriticalResources();
        
        // Setup performance monitoring
        this.monitorPerformance();
    }

    setupLazyLoading() {
        const images = document.querySelectorAll('img[data-src]');
        
        if ('IntersectionObserver' in window) {
            const imageObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        const img = entry.target;
                        img.src = img.dataset.src;
                        img.removeAttribute('data-src');
                        imageObserver.unobserve(img);
                    }
                });
            });

            images.forEach(img => imageObserver.observe(img));
        } else {
            // Fallback for older browsers
            images.forEach(img => {
                img.src = img.dataset.src;
                img.removeAttribute('data-src');
            });
        }
    }

    preloadCriticalResources() {
        const criticalResources = [
            '/css/modern-ui.css',
            '/css/components.css',
            '/js/admin.js'
        ];

        criticalResources.forEach(resource => {
            const link = document.createElement('link');
            link.rel = 'preload';
            link.href = resource;
            link.as = resource.endsWith('.css') ? 'style' : 'script';
            document.head.appendChild(link);
        });
    }

    monitorPerformance() {
        if ('performance' in window) {
            window.addEventListener('load', () => {
                setTimeout(() => {
                    const perfData = performance.getEntriesByType('navigation')[0];
                    console.log('Page Load Performance:', {
                        loadTime: Math.round(perfData.loadEventEnd - perfData.loadEventStart),
                        domContentLoaded: Math.round(perfData.domContentLoadedEventEnd - perfData.domContentLoadedEventStart),
                        totalTime: Math.round(perfData.loadEventEnd - perfData.fetchStart)
                    });
                }, 1000);
            });
        }
    }

    // =================
    // UTILITY METHODS
    // =================

    setButtonLoading(button, loading) {
        if (loading) {
            button.dataset.originalText = button.innerHTML;
            button.innerHTML = `<span class="spinner-modern me-2"></span>กำลังประมวลผล...`;
            button.disabled = true;
        } else {
            button.innerHTML = button.dataset.originalText || button.innerHTML;
            button.disabled = false;
        }
    }

    formatNumber(number) {
        return new Intl.NumberFormat(this.config.language === 'th' ? 'th-TH' : 'en-US').format(number);
    }

    formatCurrency(amount, currency = 'THB') {
        return new Intl.NumberFormat(this.config.language === 'th' ? 'th-TH' : 'en-US', {
            style: 'currency',
            currency: currency
        }).format(amount);
    }

    formatDate(date, options = {}) {
        const defaultOptions = {
            year: 'numeric',
            month: 'long',
            day: 'numeric'
        };
        
        return new Intl.DateTimeFormat(
            this.config.language === 'th' ? 'th-TH' : 'en-US',
            { ...defaultOptions, ...options }
        ).format(new Date(date));
    }

    debounce(func, wait) {
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

    dispatchEvent(eventName, data = {}) {
        const event = new CustomEvent(eventName, { detail: data });
        window.dispatchEvent(event);
    }

    // =================
    // PUBLIC API METHODS
    // =================

    // Method to refresh specific UI components
    refresh(componentType = 'all') {
        switch (componentType) {
            case 'animations':
                this.setupAnimationSystem();
                break;
            case 'forms':
                this.setupFormValidation();
                break;
            case 'language':
                this.applyLanguage(this.config.language);
                break;
            case 'all':
            default:
                this.init();
                break;
        }
    }

    // Method to update configuration
    updateConfig(newConfig) {
        this.config = { ...this.config, ...newConfig };
        if (newConfig.language) {
            this.switchLanguage(newConfig.language);
        }
    }

    // Method to get current configuration
    getConfig() {
        return { ...this.config };
    }
}

// Initialize Modern UI System
const modernUI = new ModernUI();

// Export for global use
window.ModernUI = modernUI;

// Backward compatibility
window.modernUI = modernUI;

// jQuery plugin for existing code
if (typeof jQuery !== 'undefined') {
    jQuery.fn.modernUI = function(action, ...args) {
        return modernUI[action]?.apply(modernUI, args) || this;
    };
}

// Auto-initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        console.log('✨ BizConnect Modern UI System Initialized');
    });
} else {
    console.log('✨ BizConnect Modern UI System Initialized');
}