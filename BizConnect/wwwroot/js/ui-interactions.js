// BizConnect Modern UI Interactions
// Enhanced user interactions with smooth animations and feedback

class UIInteractions {
    constructor() {
        this.initializeComponents();
        this.setupEventListeners();
        this.setupAnimationObserver();
        this.initializeTooltips();
        this.setupFormValidation();
    }

    // Initialize all UI components
    initializeComponents() {
        this.initializeLanguageToggle();
        this.initializeModals();
        this.initializeDropdowns();
        this.initializeProgressBars();
        this.initializeCounters();
        this.initializeCopyButtons();
    }

    // Language toggle functionality
    initializeLanguageToggle() {
        const toggles = document.querySelectorAll('.language-toggle-modern');
        
        toggles.forEach(toggle => {
            const options = toggle.querySelectorAll('.language-option');
            
            options.forEach(option => {
                option.addEventListener('click', (e) => {
                    e.preventDefault();
                    
                    if (!option.classList.contains('active')) {
                        // Remove active from all options
                        options.forEach(opt => opt.classList.remove('active'));
                        
                        // Add active to clicked option
                        option.classList.add('active');
                        
                        // Animate transition
                        this.animateLanguageSwitch(option);
                        
                        // Handle language change
                        const culture = option.dataset.culture;
                        if (culture) {
                            this.changeLanguage(culture);
                        }
                    }
                });
            });
        });
    }

    // Animate language switch
    animateLanguageSwitch(option) {
        option.style.transform = 'scale(1.1)';
        
        setTimeout(() => {
            option.style.transform = 'scale(1)';
        }, 200);
    }

    // Change language with smooth transition
    async changeLanguage(culture) {
        try {
            // Show loading state
            this.showLanguageLoading();
            
            // Send culture change request
            const response = await fetch('/Culture/SetCulture', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': this.getAntiForgeryToken()
                },
                body: JSON.stringify({ culture: culture })
            });
            
            if (response.ok) {
                // Fade out content
                document.body.style.opacity = '0.7';
                document.body.style.transition = 'opacity 0.3s ease';
                
                // Reload page after fade
                setTimeout(() => {
                    window.location.reload();
                }, 300);
            } else {
                this.showToast('error', 'Failed to change language', 'Please try again');
                this.hideLanguageLoading();
            }
        } catch (error) {
            console.error('Language change error:', error);
            this.showToast('error', 'Language Change Error', 'Please try again');
            this.hideLanguageLoading();
        }
    }

    // Show language loading state
    showLanguageLoading() {
        const toggles = document.querySelectorAll('.language-toggle-modern');
        toggles.forEach(toggle => {
            toggle.style.opacity = '0.7';
            toggle.style.pointerEvents = 'none';
        });
    }

    // Hide language loading state
    hideLanguageLoading() {
        const toggles = document.querySelectorAll('.language-toggle-modern');
        toggles.forEach(toggle => {
            toggle.style.opacity = '1';
            toggle.style.pointerEvents = 'auto';
        });
    }

    // Initialize enhanced modals
    initializeModals() {
        const modals = document.querySelectorAll('.modal-modern');
        
        modals.forEach(modal => {
            const bsModal = new bootstrap.Modal(modal);
            
            // Add entrance animation
            modal.addEventListener('show.bs.modal', () => {
                modal.querySelector('.modal-dialog').style.opacity = '0';
                modal.querySelector('.modal-dialog').style.transform = 'scale(0.8) translateY(-20px)';
            });
            
            modal.addEventListener('shown.bs.modal', () => {
                const dialog = modal.querySelector('.modal-dialog');
                dialog.style.transition = 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)';
                dialog.style.opacity = '1';
                dialog.style.transform = 'scale(1) translateY(0)';
            });
            
            // Add exit animation
            modal.addEventListener('hide.bs.modal', (e) => {
                e.preventDefault();
                
                const dialog = modal.querySelector('.modal-dialog');
                dialog.style.transition = 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)';
                dialog.style.opacity = '0';
                dialog.style.transform = 'scale(0.8) translateY(-20px)';
                
                setTimeout(() => {
                    bsModal.hide();
                }, 300);
            });
        });
    }

    // Initialize enhanced dropdowns
    initializeDropdowns() {
        const dropdowns = document.querySelectorAll('.dropdown-modern');
        
        dropdowns.forEach(dropdown => {
            const menu = dropdown.querySelector('.dropdown-menu');
            const toggle = dropdown.querySelector('[data-bs-toggle="dropdown"]');
            
            if (toggle && menu) {
                toggle.addEventListener('click', () => {
                    setTimeout(() => {
                        if (menu.classList.contains('show')) {
                            this.animateDropdownOpen(menu);
                        }
                    }, 10);
                });
                
                // Add hover effects to items
                const items = menu.querySelectorAll('.dropdown-item');
                items.forEach(item => {
                    item.addEventListener('mouseenter', () => {
                        this.animateDropdownItemHover(item, true);
                    });
                    
                    item.addEventListener('mouseleave', () => {
                        this.animateDropdownItemHover(item, false);
                    });
                });
            }
        });
    }

    // Animate dropdown opening
    animateDropdownOpen(menu) {
        menu.style.opacity = '0';
        menu.style.transform = 'translateY(-10px) scale(0.95)';
        
        setTimeout(() => {
            menu.style.transition = 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)';
            menu.style.opacity = '1';
            menu.style.transform = 'translateY(0) scale(1)';
        }, 10);
    }

    // Animate dropdown item hover
    animateDropdownItemHover(item, enter) {
        if (enter) {
            item.style.transform = 'translateX(4px)';
            item.style.background = 'var(--glass-light)';
            item.style.color = 'var(--kbank-green)';
        } else {
            item.style.transform = 'translateX(0)';
            item.style.background = '';
            item.style.color = '';
        }
    }

    // Initialize progress bars with animation
    initializeProgressBars() {
        const progressBars = document.querySelectorAll('.progress-bar-modern');
        
        progressBars.forEach(bar => {
            const value = bar.dataset.value || bar.style.width;
            bar.style.width = '0%';
            
            // Animate to target value
            setTimeout(() => {
                bar.style.transition = 'width 1s ease-out';
                bar.style.width = value;
            }, 200);
        });
    }

    // Initialize animated counters
    initializeCounters() {
        const counters = document.querySelectorAll('.stats-number[data-target]');
        
        counters.forEach(counter => {
            const target = parseInt(counter.dataset.target);
            const duration = parseInt(counter.dataset.duration) || 2000;
            
            this.animateCounter(counter, 0, target, duration);
        });
    }

    // Animate counter from start to end
    animateCounter(element, start, end, duration) {
        const startTimestamp = performance.now();
        
        const step = (timestamp) => {
            const elapsed = timestamp - startTimestamp;
            const progress = Math.min(elapsed / duration, 1);
            
            // Easing function
            const easeOutExpo = progress === 1 ? 1 : 1 - Math.pow(2, -10 * progress);
            const current = Math.floor(start + (end - start) * easeOutExpo);
            
            element.textContent = current.toLocaleString();
            
            if (progress < 1) {
                requestAnimationFrame(step);
            }
        };
        
        requestAnimationFrame(step);
    }

    // Initialize copy buttons
    initializeCopyButtons() {
        const copyButtons = document.querySelectorAll('.otac-copy-btn');
        
        copyButtons.forEach(button => {
            button.addEventListener('click', async (e) => {
                e.preventDefault();
                
                const otacElement = button.closest('.otac-code-modern');
                const codeText = otacElement.querySelector('.otac-text')?.textContent || 
                               otacElement.textContent.replace('📋', '').trim();
                
                try {
                    await navigator.clipboard.writeText(codeText);
                    
                    // Visual feedback
                    this.animateCopySuccess(button);
                    this.showToast('success', 'Copied!', `OTAC code ${codeText} copied to clipboard`);
                    
                } catch (error) {
                    console.error('Copy failed:', error);
                    this.showToast('error', 'Copy Failed', 'Unable to copy to clipboard');
                }
            });
        });
    }

    // Animate successful copy
    animateCopySuccess(button) {
        const originalIcon = button.innerHTML;
        
        button.innerHTML = '<i class="fas fa-check"></i>';
        button.style.background = 'var(--success)';
        button.style.transform = 'scale(1.2)';
        
        setTimeout(() => {
            button.innerHTML = originalIcon;
            button.style.background = '';
            button.style.transform = '';
        }, 1500);
    }

    // Setup global event listeners
    setupEventListeners() {
        // Global click handler for buttons with loading states
        document.addEventListener('click', (e) => {
            const button = e.target.closest('.btn-modern[data-loading]');
            if (button && !button.disabled) {
                this.showButtonLoading(button);
            }
        });

        // Form submission with validation
        document.addEventListener('submit', (e) => {
            const form = e.target.closest('.form-modern');
            if (form) {
                this.handleFormSubmission(form, e);
            }
        });

        // Enhanced hover effects
        this.setupHoverEffects();
        
        // Keyboard navigation enhancements
        this.setupKeyboardNavigation();
    }

    // Show button loading state
    showButtonLoading(button) {
        const originalText = button.innerHTML;
        button.dataset.originalText = originalText;
        
        button.innerHTML = `
            <div class="spinner-modern" style="width: 16px; height: 16px; margin-right: 8px;"></div>
            Loading...
        `;
        button.disabled = true;
        
        // Auto-restore after timeout
        setTimeout(() => {
            this.hideButtonLoading(button);
        }, 5000);
    }

    // Hide button loading state
    hideButtonLoading(button) {
        if (button.dataset.originalText) {
            button.innerHTML = button.dataset.originalText;
            button.disabled = false;
            delete button.dataset.originalText;
        }
    }

    // Setup hover effects for interactive elements
    setupHoverEffects() {
        // Card hover effects
        const cards = document.querySelectorAll('.card-modern, .glass-card');
        cards.forEach(card => {
            card.addEventListener('mouseenter', () => {
                card.style.transform = 'translateY(-4px)';
                card.style.boxShadow = 'var(--shadow-xl)';
            });
            
            card.addEventListener('mouseleave', () => {
                card.style.transform = '';
                card.style.boxShadow = '';
            });
        });

        // Button hover effects
        const buttons = document.querySelectorAll('.btn-modern');
        buttons.forEach(button => {
            button.addEventListener('mouseenter', () => {
                button.style.transform = 'translateY(-2px)';
            });
            
            button.addEventListener('mouseleave', () => {
                button.style.transform = '';
            });
            
            button.addEventListener('mousedown', () => {
                button.style.transform = 'translateY(0)';
            });
            
            button.addEventListener('mouseup', () => {
                button.style.transform = 'translateY(-2px)';
            });
        });
    }

    // Setup keyboard navigation
    setupKeyboardNavigation() {
        // Enhanced focus management
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Tab') {
                this.handleTabNavigation(e);
            }
            
            if (e.key === 'Escape') {
                this.handleEscapeKey(e);
            }
        });
    }

    // Handle tab navigation with visual feedback
    handleTabNavigation(e) {
        setTimeout(() => {
            const focused = document.activeElement;
            if (focused && focused.classList.contains('focus-visible')) {
                focused.scrollIntoView({
                    behavior: 'smooth',
                    block: 'nearest'
                });
            }
        }, 10);
    }

    // Handle escape key
    handleEscapeKey(e) {
        // Close open dropdowns
        const openDropdowns = document.querySelectorAll('.dropdown-menu.show');
        openDropdowns.forEach(dropdown => {
            const toggle = dropdown.parentElement.querySelector('[data-bs-toggle="dropdown"]');
            if (toggle) {
                bootstrap.Dropdown.getInstance(toggle)?.hide();
            }
        });

        // Close open modals
        const openModals = document.querySelectorAll('.modal.show');
        openModals.forEach(modal => {
            bootstrap.Modal.getInstance(modal)?.hide();
        });
    }

    // Setup animation observer for scroll-triggered animations
    setupAnimationObserver() {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const element = entry.target;
                    
                    if (element.classList.contains('fade-in-up')) {
                        element.style.opacity = '1';
                        element.style.transform = 'translateY(0)';
                    }
                    
                    if (element.classList.contains('fade-in-left')) {
                        element.style.opacity = '1';
                        element.style.transform = 'translateX(0)';
                    }
                    
                    if (element.classList.contains('fade-in-right')) {
                        element.style.opacity = '1';
                        element.style.transform = 'translateX(0)';
                    }
                    
                    if (element.classList.contains('scale-in')) {
                        element.style.opacity = '1';
                        element.style.transform = 'scale(1)';
                    }
                }
            });
        }, {
            threshold: 0.1,
            rootMargin: '50px'
        });

        // Observe elements with animation classes
        const animatedElements = document.querySelectorAll(
            '.fade-in-up, .fade-in-left, .fade-in-right, .scale-in'
        );
        
        animatedElements.forEach(element => {
            // Set initial state
            element.style.opacity = '0';
            element.style.transition = 'all 0.6s cubic-bezier(0.4, 0, 0.2, 1)';
            
            if (element.classList.contains('fade-in-up')) {
                element.style.transform = 'translateY(30px)';
            }
            if (element.classList.contains('fade-in-left')) {
                element.style.transform = 'translateX(-30px)';
            }
            if (element.classList.contains('fade-in-right')) {
                element.style.transform = 'translateX(30px)';
            }
            if (element.classList.contains('scale-in')) {
                element.style.transform = 'scale(0.9)';
            }
            
            observer.observe(element);
        });
    }

    // Initialize tooltips
    initializeTooltips() {
        const tooltipTriggerList = [].slice.call(
            document.querySelectorAll('[data-bs-toggle="tooltip"]')
        );
        
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl, {
                customClass: 'tooltip-modern'
            });
        });
    }

    // Setup form validation with visual feedback
    setupFormValidation() {
        const forms = document.querySelectorAll('.form-modern');
        
        forms.forEach(form => {
            const inputs = form.querySelectorAll('.form-control-modern');
            
            inputs.forEach(input => {
                input.addEventListener('blur', () => {
                    this.validateField(input);
                });
                
                input.addEventListener('input', () => {
                    this.clearFieldError(input);
                });
            });
        });
    }

    // Validate individual field
    validateField(field) {
        const value = field.value.trim();
        const required = field.hasAttribute('required');
        const type = field.type;
        
        let isValid = true;
        let message = '';

        // Required validation
        if (required && !value) {
            isValid = false;
            message = 'This field is required';
        }

        // Type-specific validation
        if (value && type === 'email') {
            const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
            if (!emailRegex.test(value)) {
                isValid = false;
                message = 'Please enter a valid email address';
            }
        }

        if (value && type === 'tel') {
            const phoneRegex = /^[\d\s\-\+\(\)]+$/;
            if (!phoneRegex.test(value)) {
                isValid = false;
                message = 'Please enter a valid phone number';
            }
        }

        // Show/hide validation feedback
        if (isValid) {
            this.showFieldSuccess(field);
        } else {
            this.showFieldError(field, message);
        }

        return isValid;
    }

    // Show field success state
    showFieldSuccess(field) {
        field.classList.remove('is-invalid');
        field.classList.add('is-valid');
        field.style.borderColor = 'var(--success)';
        field.style.boxShadow = '0 0 0 3px rgba(0, 200, 83, 0.1)';
        
        this.clearFieldMessage(field);
    }

    // Show field error state
    showFieldError(field, message) {
        field.classList.remove('is-valid');
        field.classList.add('is-invalid');
        field.style.borderColor = 'var(--danger)';
        field.style.boxShadow = '0 0 0 3px rgba(211, 47, 47, 0.1)';
        
        this.showFieldMessage(field, message, 'error');
    }

    // Clear field error state
    clearFieldError(field) {
        field.classList.remove('is-invalid');
        field.style.borderColor = '';
        field.style.boxShadow = '';
        
        this.clearFieldMessage(field);
    }

    // Show field message
    showFieldMessage(field, message, type) {
        let messageEl = field.parentNode.querySelector('.field-message');
        
        if (!messageEl) {
            messageEl = document.createElement('div');
            messageEl.className = `field-message ${type === 'error' ? 'text-danger' : 'text-success'}`;
            field.parentNode.appendChild(messageEl);
        }
        
        messageEl.textContent = message;
        messageEl.style.fontSize = '0.85rem';
        messageEl.style.marginTop = '0.25rem';
        messageEl.style.opacity = '0';
        messageEl.style.transform = 'translateY(-10px)';
        messageEl.style.transition = 'all 0.3s ease';
        
        setTimeout(() => {
            messageEl.style.opacity = '1';
            messageEl.style.transform = 'translateY(0)';
        }, 10);
    }

    // Clear field message
    clearFieldMessage(field) {
        const messageEl = field.parentNode.querySelector('.field-message');
        if (messageEl) {
            messageEl.style.opacity = '0';
            messageEl.style.transform = 'translateY(-10px)';
            
            setTimeout(() => {
                messageEl.remove();
            }, 300);
        }
    }

    // Handle form submission
    handleFormSubmission(form, e) {
        const inputs = form.querySelectorAll('.form-control-modern');
        let isFormValid = true;
        
        inputs.forEach(input => {
            if (!this.validateField(input)) {
                isFormValid = false;
            }
        });
        
        if (!isFormValid) {
            e.preventDefault();
            
            // Scroll to first invalid field
            const firstInvalid = form.querySelector('.is-invalid');
            if (firstInvalid) {
                firstInvalid.scrollIntoView({
                    behavior: 'smooth',
                    block: 'center'
                });
                firstInvalid.focus();
            }
        }
    }

    // Show toast notification
    showToast(type, title, message) {
        const toastContainer = document.getElementById('toast-container') || this.createToastContainer();
        
        const toast = document.createElement('div');
        toast.className = `toast toast-modern toast-${type}`;
        toast.innerHTML = `
            <div class="toast-header">
                <strong class="me-auto">${title}</strong>
                <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body">${message}</div>
        `;
        
        toastContainer.appendChild(toast);
        
        const bsToast = new bootstrap.Toast(toast, {
            autohide: true,
            delay: 5000
        });
        
        bsToast.show();
        
        // Remove element after hiding
        toast.addEventListener('hidden.bs.toast', () => {
            toast.remove();
        });
    }

    // Create toast container if it doesn't exist
    createToastContainer() {
        const container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'toast-container position-fixed top-0 end-0 p-3';
        container.style.zIndex = 'var(--z-toast)';
        
        document.body.appendChild(container);
        return container;
    }

    // Get anti-forgery token
    getAntiForgeryToken() {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const tokenMeta = document.querySelector('meta[name="csrf-token"]');
        
        if (tokenInput) return tokenInput.value;
        if (tokenMeta) return tokenMeta.content;
        
        return '';
    }

    // Utility method to add ripple effect
    addRippleEffect(element, event) {
        const rect = element.getBoundingClientRect();
        const x = event.clientX - rect.left;
        const y = event.clientY - rect.top;
        
        const ripple = document.createElement('span');
        ripple.className = 'ripple';
        ripple.style.left = x + 'px';
        ripple.style.top = y + 'px';
        
        element.appendChild(ripple);
        
        setTimeout(() => {
            ripple.remove();
        }, 600);
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.uiInteractions = new UIInteractions();
});

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = UIInteractions;
}