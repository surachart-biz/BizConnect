/**
 * BizConnect Interactive Elements & Animations
 * Provides enhanced user interactions, smooth transitions, and engaging animations
 */

class BizConnectInteractions {
    constructor() {
        this.observers = new Map();
        this.animationQueue = [];
        this.isAnimating = false;
        this.init();
    }

    init() {
        this.setupIntersectionObserver();
        this.setupClickEffects();
        this.setupHoverEffects();
        this.setupFormInteractions();
        this.setupScrollAnimations();
        this.setupParallaxEffects();
        this.setupCounterAnimations();
        this.setupMicroInteractions();
        this.setupAdvancedAnimations();
        this.setupMobileOptimizations();
    }

    // Intersection Observer for scroll animations
    setupIntersectionObserver() {
        if ('IntersectionObserver' in window) {
            const observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        this.animateElement(entry.target);
                    }
                });
            }, {
                threshold: 0.1,
                rootMargin: '0px 0px -50px 0px'
            });

            // Observe elements with animation classes
            const animatedElements = document.querySelectorAll(
                '.bc-fade-in, .bc-slide-in-left, .bc-slide-in-right, .bc-slide-in-up, .bc-slide-in-down'
            );
            
            animatedElements.forEach(el => {
                el.style.opacity = '0';
                el.style.transform = this.getInitialTransform(el);
                observer.observe(el);
            });

            this.observers.set('intersection', observer);
        }
    }

    getInitialTransform(element) {
        if (element.classList.contains('bc-slide-in-left')) return 'translateX(-20px)';
        if (element.classList.contains('bc-slide-in-right')) return 'translateX(20px)';
        if (element.classList.contains('bc-slide-in-up')) return 'translateY(20px)';
        if (element.classList.contains('bc-slide-in-down')) return 'translateY(-20px)';
        return 'translateY(10px)';
    }

    animateElement(element) {
        element.style.transition = 'all 0.6s ease-out';
        element.style.opacity = '1';
        element.style.transform = 'translate(0, 0)';
    }

    // Click effects and ripples
    setupClickEffects() {
        document.addEventListener('click', (e) => {
            const target = e.target.closest('.bc-ripple, .btn');
            if (target) {
                this.createRippleEffect(target, e);
            }

            // Add bounce effect to buttons
            if (target && target.classList.contains('btn')) {
                this.addBounceEffect(target);
            }
        });
    }

    createRippleEffect(element, event) {
        const rect = element.getBoundingClientRect();
        const size = Math.max(rect.width, rect.height);
        const x = event.clientX - rect.left - size / 2;
        const y = event.clientY - rect.top - size / 2;

        const ripple = document.createElement('div');
        ripple.style.cssText = `
            position: absolute;
            width: ${size}px;
            height: ${size}px;
            left: ${x}px;
            top: ${y}px;
            background: rgba(255, 255, 255, 0.3);
            border-radius: 50%;
            transform: scale(0);
            animation: ripple-animation 0.6s ease-out;
            pointer-events: none;
            z-index: 1000;
        `;

        element.style.position = 'relative';
        element.style.overflow = 'hidden';
        element.appendChild(ripple);

        setTimeout(() => {
            if (ripple.parentNode) {
                ripple.remove();
            }
        }, 600);
    }

    addBounceEffect(element) {
        element.classList.remove('bc-bounce');
        setTimeout(() => {
            element.classList.add('bc-bounce');
        }, 10);
    }

    // Enhanced hover effects
    setupHoverEffects() {
        // Magnetic effect for buttons
        const magneticElements = document.querySelectorAll('.btn-primary, .btn-secondary');
        
        magneticElements.forEach(element => {
            element.addEventListener('mousemove', (e) => {
                const rect = element.getBoundingClientRect();
                const x = e.clientX - rect.left - rect.width / 2;
                const y = e.clientY - rect.top - rect.height / 2;
                
                element.style.transform = `translate(${x * 0.1}px, ${y * 0.1}px) translateY(-2px)`;
            });

            element.addEventListener('mouseleave', () => {
                element.style.transform = '';
            });
        });

        // Tilt effect for cards
        const tiltElements = document.querySelectorAll('.card, .bc-tilt');
        
        tiltElements.forEach(element => {
            element.addEventListener('mousemove', (e) => {
                if (!element.classList.contains('bc-tilt')) return;
                
                const rect = element.getBoundingClientRect();
                const x = e.clientX - rect.left;
                const y = e.clientY - rect.top;
                
                const centerX = rect.width / 2;
                const centerY = rect.height / 2;
                
                const rotateX = (y - centerY) / 10;
                const rotateY = (centerX - x) / 10;
                
                element.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale(1.02)`;
            });

            element.addEventListener('mouseleave', () => {
                if (element.classList.contains('bc-tilt')) {
                    element.style.transform = '';
                }
            });
        });
    }

    // Form interaction enhancements
    setupFormInteractions() {
        // Floating labels
        const formGroups = document.querySelectorAll('.bc-form-group');
        
        formGroups.forEach(group => {
            const input = group.querySelector('.bc-form-control, .bc-form-select');
            const label = group.querySelector('.bc-form-label');
            
            if (input && label) {
                input.addEventListener('focus', () => {
                    label.style.transform = 'translateY(-20px) scale(0.85)';
                    label.style.color = 'var(--bc-primary)';
                });

                input.addEventListener('blur', () => {
                    if (!input.value) {
                        label.style.transform = '';
                        label.style.color = '';
                    }
                });
            }
        });

        // Form validation animations
        const forms = document.querySelectorAll('form');
        
        forms.forEach(form => {
            form.addEventListener('submit', (e) => {
                const invalidFields = form.querySelectorAll(':invalid');
                
                invalidFields.forEach(field => {
                    field.classList.add('bc-shake');
                    setTimeout(() => {
                        field.classList.remove('bc-shake');
                    }, 500);
                });
            });
        });
    }

    // Scroll-based animations
    setupScrollAnimations() {
        let ticking = false;

        window.addEventListener('scroll', () => {
            if (!ticking) {
                requestAnimationFrame(() => {
                    this.updateScrollAnimations();
                    ticking = false;
                });
                ticking = true;
            }
        });
    }

    updateScrollAnimations() {
        const scrolled = window.pageYOffset;
        const rate = scrolled * -0.5;

        // Parallax backgrounds
        const parallaxElements = document.querySelectorAll('.bc-parallax');
        parallaxElements.forEach(element => {
            element.style.transform = `translateY(${rate}px)`;
        });

        // Fade elements based on scroll
        const fadeElements = document.querySelectorAll('.bc-scroll-fade');
        fadeElements.forEach(element => {
            const elementTop = element.offsetTop;
            const elementHeight = element.offsetHeight;
            const windowHeight = window.innerHeight;
            
            const opacity = Math.max(0, Math.min(1, 
                (windowHeight - (elementTop - scrolled)) / (windowHeight + elementHeight)
            ));
            
            element.style.opacity = opacity;
        });
    }

    // Parallax effects
    setupParallaxEffects() {
        const parallaxElements = document.querySelectorAll('.bc-parallax');
        
        parallaxElements.forEach(element => {
            element.style.willChange = 'transform';
        });
    }

    // Counter animations
    setupCounterAnimations() {
        const counters = document.querySelectorAll('.bc-counter');
        
        if ('IntersectionObserver' in window) {
            const counterObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        this.animateCounter(entry.target);
                        counterObserver.unobserve(entry.target);
                    }
                });
            });

            counters.forEach(counter => {
                counterObserver.observe(counter);
            });
        }
    }

    animateCounter(element) {
        const target = parseInt(element.dataset.target || element.textContent);
        const duration = parseInt(element.dataset.duration || 2000);
        const start = 0;
        const increment = target / (duration / 16);
        let current = start;

        const timer = setInterval(() => {
            current += increment;
            element.textContent = Math.floor(current);

            if (current >= target) {
                element.textContent = target;
                clearInterval(timer);
            }
        }, 16);
    }

    // Utility methods
    addAnimation(element, animationClass, duration = 1000) {
        return new Promise((resolve) => {
            element.classList.add(animationClass);
            
            setTimeout(() => {
                element.classList.remove(animationClass);
                resolve();
            }, duration);
        });
    }

    staggerAnimation(elements, animationClass, delay = 100) {
        elements.forEach((element, index) => {
            setTimeout(() => {
                this.addAnimation(element, animationClass);
            }, index * delay);
        });
    }

    // Page transition effects
    pageTransition(callback) {
        document.body.style.opacity = '0';
        document.body.style.transform = 'scale(0.98)';
        document.body.style.transition = 'all 0.3s ease-out';
        
        setTimeout(() => {
            if (callback) callback();
            
            document.body.style.opacity = '1';
            document.body.style.transform = 'scale(1)';
            
            setTimeout(() => {
                document.body.style.transition = '';
            }, 300);
        }, 300);
    }

    // Advanced Micro-interactions
    setupMicroInteractions() {
        // Enhanced button hover effects
        const modernButtons = document.querySelectorAll('.btn-modern, .quick-action-item');
        modernButtons.forEach(button => {
            button.addEventListener('mouseenter', (e) => {
                this.createHoverEffect(e.target);
            });
            
            button.addEventListener('mouseleave', (e) => {
                this.removeHoverEffect(e.target);
            });
        });

        // Card magnetic attraction
        const cards = document.querySelectorAll('.kpi-card, .card-modern, .activity-card');
        cards.forEach(card => {
            card.addEventListener('mousemove', (e) => {
                this.handleCardMagneticEffect(e, card);
            });
            
            card.addEventListener('mouseleave', (e) => {
                this.resetCardPosition(card);
            });
        });

        // Input field focus animations
        const inputs = document.querySelectorAll('.form-control, .form-select');
        inputs.forEach(input => {
            input.addEventListener('focus', (e) => {
                this.animateInputFocus(e.target);
            });
            
            input.addEventListener('blur', (e) => {
                this.animateInputBlur(e.target);
            });
        });
    }

    setupAdvancedAnimations() {
        // Page transition effects
        this.setupPageTransitions();
        
        // Modal animation enhancements
        this.setupModalAnimations();
        
        // Table row animations
        this.setupTableAnimations();
        
        // Real-time update highlighting
        this.setupUpdateHighlighting();
    }

    setupMobileOptimizations() {
        // Detect mobile devices
        this.isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
        this.isTouchDevice = 'ontouchstart' in window;
        
        if (this.isMobile || this.isTouchDevice) {
            // Optimize animation timing for mobile
            document.documentElement.style.setProperty('--duration-fast', '0.1s');
            document.documentElement.style.setProperty('--duration-normal', '0.2s');
            document.documentElement.style.setProperty('--duration-slow', '0.3s');
            
            // Add touch-specific interactions
            this.setupTouchInteractions();
            
            // Disable complex hover effects on mobile
            document.body.classList.add('mobile-device');
        }
    }

    createHoverEffect(element) {
        element.style.transform = 'translateY(-2px) scale(1.02)';
        element.style.boxShadow = '0 8px 25px rgba(0,0,0,0.15)';
        element.style.transition = 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)';
    }

    removeHoverEffect(element) {
        element.style.transform = '';
        element.style.boxShadow = '';
    }

    handleCardMagneticEffect(event, card) {
        if (this.isMobile) return; // Skip on mobile for performance
        
        const rect = card.getBoundingClientRect();
        const x = event.clientX - rect.left;
        const y = event.clientY - rect.top;
        
        const centerX = rect.width / 2;
        const centerY = rect.height / 2;
        
        const deltaX = (x - centerX) / centerX;
        const deltaY = (y - centerY) / centerY;
        
        const tiltX = deltaY * 5; // Reduced tilt for subtlety
        const tiltY = deltaX * -5;
        
        card.style.transform = `perspective(1000px) rotateX(${tiltX}deg) rotateY(${tiltY}deg) translateY(-4px) scale(1.02)`;
        card.style.transition = 'transform 0.1s ease-out';
    }

    resetCardPosition(card) {
        card.style.transform = '';
        card.style.transition = 'transform 0.4s cubic-bezier(0.4, 0, 0.2, 1)';
    }

    animateInputFocus(input) {
        const parent = input.closest('.form-group, .form-group-modern');
        if (parent) {
            parent.classList.add('focused');
        }
        
        input.style.transform = 'scale(1.01)';
        input.style.boxShadow = '0 0 0 3px rgba(0, 102, 204, 0.1)';
        input.style.borderColor = 'var(--kbank-primary)';
    }

    animateInputBlur(input) {
        const parent = input.closest('.form-group, .form-group-modern');
        if (parent && !input.value) {
            parent.classList.remove('focused');
        }
        
        input.style.transform = '';
        input.style.boxShadow = '';
        input.style.borderColor = '';
    }

    setupPageTransitions() {
        // Intercept navigation for smooth transitions
        const links = document.querySelectorAll('a[href]:not([href^="#"]):not([href^="javascript:"]):not([data-bs-toggle])');
        links.forEach(link => {
            link.addEventListener('click', (e) => {
                if (e.ctrlKey || e.metaKey || e.shiftKey || link.target === '_blank') return;
                
                const href = link.href;
                if (href && href !== window.location.href) {
                    e.preventDefault();
                    this.performPageTransition(href);
                }
            });
        });
    }

    performPageTransition(url) {
        document.body.style.opacity = '0.8';
        document.body.style.transform = 'scale(0.98)';
        document.body.style.transition = 'all 0.3s ease-out';
        
        setTimeout(() => {
            window.location.href = url;
        }, 300);
    }

    setupModalAnimations() {
        // Enhanced modal show/hide animations
        document.addEventListener('show.bs.modal', (e) => {
            const modal = e.target;
            const modalDialog = modal.querySelector('.modal-dialog');
            
            if (modalDialog) {
                modalDialog.style.transform = 'scale(0.8) translateY(-50px)';
                modalDialog.style.opacity = '0';
                modalDialog.style.transition = 'all 0.3s cubic-bezier(0.34, 1.56, 0.64, 1)';
                
                setTimeout(() => {
                    modalDialog.style.transform = 'scale(1) translateY(0)';
                    modalDialog.style.opacity = '1';
                }, 10);
            }
        });
        
        document.addEventListener('hide.bs.modal', (e) => {
            const modal = e.target;
            const modalDialog = modal.querySelector('.modal-dialog');
            
            if (modalDialog) {
                modalDialog.style.transform = 'scale(0.9) translateY(20px)';
                modalDialog.style.opacity = '0';
                modalDialog.style.transition = 'all 0.2s ease-in';
            }
        });
    }

    setupTableAnimations() {
        const tableRows = document.querySelectorAll('tbody tr');
        tableRows.forEach((row, index) => {
            row.style.animationDelay = `${index * 0.05}s`;
            row.classList.add('fade-in-up');
            
            // Hover effects for table rows
            row.addEventListener('mouseenter', () => {
                if (!this.isMobile) {
                    row.style.transform = 'translateX(4px)';
                    row.style.boxShadow = '0 2px 8px rgba(0,0,0,0.1)';
                }
            });
            
            row.addEventListener('mouseleave', () => {
                row.style.transform = '';
                row.style.boxShadow = '';
            });
        });
    }

    setupUpdateHighlighting() {
        // Highlight newly updated content
        this.highlightNewContent = (selector) => {
            const elements = document.querySelectorAll(selector);
            elements.forEach(element => {
                element.classList.add('highlight-new');
                setTimeout(() => {
                    element.classList.remove('highlight-new');
                    element.classList.add('highlight-fade');
                }, 2000);
                setTimeout(() => {
                    element.classList.remove('highlight-fade');
                }, 3000);
            });
        };
    }

    setupTouchInteractions() {
        // Touch-specific interactions for mobile
        const touchElements = document.querySelectorAll('.btn, .card, .quick-action-item');
        
        touchElements.forEach(element => {
            element.addEventListener('touchstart', (e) => {
                element.style.transform = 'scale(0.98)';
                element.style.transition = 'transform 0.1s ease-out';
            }, { passive: true });
            
            element.addEventListener('touchend', (e) => {
                setTimeout(() => {
                    element.style.transform = '';
                }, 100);
            }, { passive: true });
        });
    }

    // Success animation helper
    showSuccessAnimation(element) {
        const checkmark = document.createElement('div');
        checkmark.innerHTML = `
            <svg class="success-checkmark" viewBox="0 0 52 52">
                <circle class="success-checkmark-circle" cx="26" cy="26" r="25" fill="none"/>
                <path class="success-checkmark-check" fill="none" d="m14.1 27.2l7.1 7.2 16.7-16.8"/>
            </svg>
        `;
        checkmark.style.position = 'fixed';
        checkmark.style.top = '50%';
        checkmark.style.left = '50%';
        checkmark.style.transform = 'translate(-50%, -50%)';
        checkmark.style.zIndex = '9999';
        checkmark.style.pointerEvents = 'none';
        
        document.body.appendChild(checkmark);
        
        setTimeout(() => {
            checkmark.remove();
        }, 2000);
    }

    // Error shake animation helper
    showErrorAnimation(element) {
        element.classList.add('error-shake');
        setTimeout(() => {
            element.classList.remove('error-shake');
        }, 600);
    }

    // Copy feedback animation
    showCopyFeedback(element) {
        const originalText = element.textContent;
        element.classList.add('copy-success');
        element.textContent = 'Copied!';
        
        setTimeout(() => {
            element.classList.remove('copy-success');
            element.textContent = originalText;
        }, 1500);
    }

    // Loading state management
    setLoadingState(element, isLoading = true) {
        if (isLoading) {
            element.classList.add('loading-state');
            element.disabled = true;
            const icon = element.querySelector('i');
            if (icon) {
                icon.classList.add('fa-spin');
            }
        } else {
            element.classList.remove('loading-state');
            element.disabled = false;
            const icon = element.querySelector('i');
            if (icon) {
                icon.classList.remove('fa-spin');
            }
        }
    }

    // Progress bar animation
    animateProgressBar(progressBar, targetValue, duration = 1000) {
        const startValue = 0;
        const startTime = performance.now();
        
        const animate = (currentTime) => {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);
            const currentValue = startValue + (targetValue - startValue) * this.easeOutCubic(progress);
            
            progressBar.style.width = `${currentValue}%`;
            
            if (progress < 1) {
                requestAnimationFrame(animate);
            }
        };
        
        requestAnimationFrame(animate);
    }

    easeOutCubic(t) {
        return 1 - Math.pow(1 - t, 3);
    }

    // Stagger animation helper
    staggerElements(elements, animationClass, delay = 100) {
        elements.forEach((element, index) => {
            setTimeout(() => {
                element.classList.add(animationClass);
            }, index * delay);
        });
    }

    // Cleanup
    destroy() {
        this.observers.forEach(observer => {
            observer.disconnect();
        });
        this.observers.clear();
    }
}

// CSS for ripple animation
const rippleCSS = `
@keyframes ripple-animation {
    to {
        transform: scale(2);
        opacity: 0;
    }
}
`;

// Add CSS to document
if (!document.getElementById('bc-ripple-styles')) {
    const style = document.createElement('style');
    style.id = 'bc-ripple-styles';
    style.textContent = rippleCSS;
    document.head.appendChild(style);
}

// Initialize interactions when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.BizConnectInteractions = new BizConnectInteractions();
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = BizConnectInteractions;
}
