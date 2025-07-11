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
