/**
 * BizConnect - Modern UI/UX JavaScript
 * Enhanced user interactions and animations
 */

// Utility Functions
const BizConnect = {
    // Show loading state on buttons
    showButtonLoading: function(button, text = 'Loading...') {
        const originalText = button.innerHTML;
        button.innerHTML = `<span class="spinner me-2"></span>${text}`;
        button.disabled = true;
        button.dataset.originalText = originalText;
    },

    // Hide loading state on buttons
    hideButtonLoading: function(button) {
        if (button.dataset.originalText) {
            button.innerHTML = button.dataset.originalText;
            button.disabled = false;
            delete button.dataset.originalText;
        }
    },

    // Show toast notification
    showToast: function(message, type = 'info', duration = 5000) {
        const toastContainer = document.getElementById('toast-container') || this.createToastContainer();
        const toast = this.createToast(message, type);
        toastContainer.appendChild(toast);

        // Animate in
        setTimeout(() => toast.classList.add('show'), 100);

        // Auto remove
        setTimeout(() => {
            toast.classList.remove('show');
            setTimeout(() => toast.remove(), 300);
        }, duration);
    },

    // Create toast container if it doesn't exist
    createToastContainer: function() {
        const container = document.createElement('div');
        container.id = 'toast-container';
        container.className = 'position-fixed top-0 end-0 p-3';
        container.style.zIndex = '1055';
        document.body.appendChild(container);
        return container;
    },

    // Create individual toast
    createToast: function(message, type) {
        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${type} border-0`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">${message}</div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" onclick="this.closest('.toast').remove()"></button>
            </div>
        `;
        return toast;
    },

    // Smooth scroll to element
    scrollTo: function(element, offset = 0) {
        const targetPosition = element.offsetTop - offset;
        window.scrollTo({
            top: targetPosition,
            behavior: 'smooth'
        });
    },

    // Form validation helper
    validateForm: function(form) {
        let isValid = true;
        const inputs = form.querySelectorAll('input[required], select[required], textarea[required]');

        inputs.forEach(input => {
            if (!input.value.trim()) {
                this.showFieldError(input, 'This field is required');
                isValid = false;
            } else {
                this.clearFieldError(input);
            }
        });

        return isValid;
    },

    // Show field error
    showFieldError: function(field, message) {
        field.classList.add('is-invalid');
        let errorDiv = field.parentNode.querySelector('.invalid-feedback');
        if (!errorDiv) {
            errorDiv = document.createElement('div');
            errorDiv.className = 'invalid-feedback';
            field.parentNode.appendChild(errorDiv);
        }
        errorDiv.textContent = message;
    },

    // Clear field error
    clearFieldError: function(field) {
        field.classList.remove('is-invalid');
        const errorDiv = field.parentNode.querySelector('.invalid-feedback');
        if (errorDiv) {
            errorDiv.remove();
        }
    },

    // Debounce function for search inputs
    debounce: function(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    },

    // Initialize all components
    init: function() {
        this.initFormEnhancements();
        this.initAnimations();
        this.initSearchFunctionality();
        this.initTooltips();
        this.initModalEnhancements();
    },

    // Initialize form enhancements
    initFormEnhancements: function() {
        // Add floating labels effect
        document.querySelectorAll('.form-control').forEach(input => {
            input.addEventListener('focus', function() {
                this.parentNode.classList.add('focused');
            });

            input.addEventListener('blur', function() {
                if (!this.value) {
                    this.parentNode.classList.remove('focused');
                }
            });

            // Check if input has value on load
            if (input.value) {
                input.parentNode.classList.add('focused');
            }
        });

        // Enhanced form submission
        document.querySelectorAll('form').forEach(form => {
            form.addEventListener('submit', function(e) {
                const submitBtn = form.querySelector('button[type="submit"]');
                if (submitBtn && !submitBtn.disabled) {
                    BizConnect.showButtonLoading(submitBtn);
                }
            });
        });
    },

    // Initialize animations
    initAnimations: function() {
        // Intersection Observer for fade-in animations
        const observerOptions = {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        };

        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('fade-in');
                }
            });
        }, observerOptions);

        // Observe all cards and major content blocks
        document.querySelectorAll('.card, .alert, .list-group').forEach(el => {
            observer.observe(el);
        });
    },

    // Initialize search functionality
    initSearchFunctionality: function() {
        const searchInputs = document.querySelectorAll('input[type="search"], .search-input');

        searchInputs.forEach(input => {
            const debouncedSearch = this.debounce((value) => {
                // Implement search logic here
                console.log('Searching for:', value);
            }, 300);

            input.addEventListener('input', function() {
                debouncedSearch(this.value);
            });
        });
    },

    // Initialize tooltips
    initTooltips: function() {
        // Initialize Bootstrap tooltips
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    },

    // Initialize modal enhancements
    initModalEnhancements: function() {
        document.querySelectorAll('.modal').forEach(modal => {
            modal.addEventListener('shown.bs.modal', function() {
                // Focus first input in modal
                const firstInput = modal.querySelector('input, select, textarea');
                if (firstInput) {
                    firstInput.focus();
                }
            });
        });
    },

    // Progressive Web App features
    initPWA: function() {
        // Service worker registration
        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('/sw.js')
                .then(registration => {
                    console.log('SW registered: ', registration);
                })
                .catch(registrationError => {
                    console.log('SW registration failed: ', registrationError);
                });
        }

        // Install prompt
        let deferredPrompt;
        window.addEventListener('beforeinstallprompt', (e) => {
            e.preventDefault();
            deferredPrompt = e;
            this.showInstallPrompt();
        });
    },

    // Show install prompt
    showInstallPrompt: function() {
        const installBanner = document.createElement('div');
        installBanner.className = 'alert alert-info alert-dismissible fade show position-fixed bottom-0 start-0 end-0 m-3';
        installBanner.style.zIndex = '1060';
        installBanner.innerHTML = `
            <div class="d-flex align-items-center">
                <i class="fas fa-mobile-alt me-2"></i>
                <div class="flex-grow-1">
                    <strong>Install BizConnect</strong>
                    <div class="text-sm">Add to your home screen for quick access</div>
                </div>
                <button type="button" class="btn btn-sm btn-primary me-2" id="installBtn">
                    Install
                </button>
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;

        document.body.appendChild(installBanner);

        document.getElementById('installBtn').addEventListener('click', () => {
            if (window.deferredPrompt) {
                window.deferredPrompt.prompt();
                window.deferredPrompt.userChoice.then((choiceResult) => {
                    if (choiceResult.outcome === 'accepted') {
                        console.log('User accepted the install prompt');
                    }
                    window.deferredPrompt = null;
                });
            }
            installBanner.remove();
        });
    },

    // Enhanced keyboard navigation
    initKeyboardNavigation: function() {
        // Escape key to close modals and dropdowns
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                // Close open modals
                const openModal = document.querySelector('.modal.show');
                if (openModal) {
                    const modal = bootstrap.Modal.getInstance(openModal);
                    if (modal) modal.hide();
                }

                // Close open dropdowns
                const openDropdown = document.querySelector('.dropdown-menu.show');
                if (openDropdown) {
                    const dropdown = bootstrap.Dropdown.getInstance(openDropdown.previousElementSibling);
                    if (dropdown) dropdown.hide();
                }
            }
        });

        // Tab navigation improvements
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Tab') {
                document.body.classList.add('keyboard-navigation');
            }
        });

        document.addEventListener('mousedown', function() {
            document.body.classList.remove('keyboard-navigation');
        });
    },

    // Performance monitoring
    initPerformanceMonitoring: function() {
        // Monitor page load performance
        window.addEventListener('load', function() {
            const perfData = performance.getEntriesByType('navigation')[0];
            const loadTime = perfData.loadEventEnd - perfData.loadEventStart;

            if (loadTime > 3000) {
                console.warn('Page load time is slow:', loadTime + 'ms');
            }
        });

        // Monitor long tasks
        if ('PerformanceObserver' in window) {
            const observer = new PerformanceObserver((list) => {
                for (const entry of list.getEntries()) {
                    if (entry.duration > 50) {
                        console.warn('Long task detected:', entry.duration + 'ms');
                    }
                }
            });
            observer.observe({ entryTypes: ['longtask'] });
        }
    },

    // Network status monitoring
    initNetworkMonitoring: function() {
        function updateNetworkStatus() {
            const isOnline = navigator.onLine;
            const statusIndicator = document.getElementById('network-status') || this.createNetworkStatusIndicator();

            if (isOnline) {
                statusIndicator.className = 'badge badge-success position-fixed top-0 end-0 m-3';
                statusIndicator.innerHTML = '<i class="fas fa-wifi me-1"></i>Online';
                setTimeout(() => statusIndicator.style.display = 'none', 3000);
            } else {
                statusIndicator.className = 'badge badge-danger position-fixed top-0 end-0 m-3';
                statusIndicator.innerHTML = '<i class="fas fa-wifi-slash me-1"></i>Offline';
                statusIndicator.style.display = 'block';
            }
        }

        window.addEventListener('online', updateNetworkStatus);
        window.addEventListener('offline', updateNetworkStatus);
    },

    // Create network status indicator
    createNetworkStatusIndicator: function() {
        const indicator = document.createElement('div');
        indicator.id = 'network-status';
        indicator.style.zIndex = '1070';
        indicator.style.display = 'none';
        document.body.appendChild(indicator);
        return indicator;
    },

    // Enhanced error handling
    initErrorHandling: function() {
        window.addEventListener('error', function(e) {
            console.error('JavaScript error:', e.error);
            BizConnect.showToast('An error occurred. Please refresh the page.', 'danger', 5000);
        });

        window.addEventListener('unhandledrejection', function(e) {
            console.error('Unhandled promise rejection:', e.reason);
            BizConnect.showToast('A network error occurred. Please try again.', 'warning', 5000);
        });
    },

    // Theme switching
    initThemeSwitch: function() {
        const savedTheme = localStorage.getItem('theme') || 'light';
        this.setTheme(savedTheme);

        // Create theme toggle button
        const themeToggle = document.createElement('button');
        themeToggle.className = 'btn btn-outline-secondary position-fixed bottom-0 end-0 m-3';
        themeToggle.style.zIndex = '1050';
        themeToggle.innerHTML = '<i class="fas fa-moon"></i>';
        themeToggle.title = 'Toggle dark mode';

        themeToggle.addEventListener('click', () => {
            const currentTheme = document.documentElement.getAttribute('data-theme') || 'light';
            const newTheme = currentTheme === 'light' ? 'dark' : 'light';
            this.setTheme(newTheme);
        });

        document.body.appendChild(themeToggle);
    },

    // Set theme
    setTheme: function(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('theme', theme);

        const themeToggle = document.querySelector('.position-fixed .fas');
        if (themeToggle) {
            themeToggle.className = theme === 'light' ? 'fas fa-moon' : 'fas fa-sun';
        }
    }
};

// Enhanced initialization
document.addEventListener('DOMContentLoaded', function() {
    BizConnect.init();
    BizConnect.initPWA();
    BizConnect.initKeyboardNavigation();
    BizConnect.initPerformanceMonitoring();
    BizConnect.initNetworkMonitoring();
    BizConnect.initErrorHandling();
    BizConnect.initThemeSwitch();
});

// Export for global use
window.BizConnect = BizConnect;
