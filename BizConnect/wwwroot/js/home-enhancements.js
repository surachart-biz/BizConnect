/**
 * BizConnect Home Page Enhancements
 * Interactive animations and dynamic content for the home page
 */

document.addEventListener('DOMContentLoaded', function() {
    // Initialize all home page enhancements
    initGreeting();
    initCounterAnimations();
    initIntersectionObserver();
    initSmoothScrolling();
    initNetworkAnimation();
});

/**
 * Dynamic greeting based on time of day
 */
function initGreeting() {
    const greetingElement = document.getElementById('greeting-text');
    if (!greetingElement) return;

    const hour = new Date().getHours();
    let greeting = 'Good morning';
    let icon = 'fas fa-sun';

    if (hour >= 12 && hour < 17) {
        greeting = 'Good afternoon';
        icon = 'fas fa-sun';
    } else if (hour >= 17 || hour < 6) {
        greeting = 'Good evening';
        icon = 'fas fa-moon';
    }

    greetingElement.textContent = greeting;
    const iconElement = greetingElement.previousElementSibling;
    if (iconElement) {
        iconElement.className = `${icon} text-warning me-2`;
    }
}

/**
 * Animated counter for statistics
 */
function initCounterAnimations() {
    const counters = document.querySelectorAll('.bc-stat-number[data-count]');
    
    counters.forEach(counter => {
        const target = parseInt(counter.getAttribute('data-count'));
        const duration = 2000; // 2 seconds
        const increment = target / (duration / 16); // 60fps
        let current = 0;

        const updateCounter = () => {
            current += increment;
            if (current < target) {
                counter.textContent = Math.floor(current);
                requestAnimationFrame(updateCounter);
            } else {
                counter.textContent = target;
            }
        };

        // Start animation when element is visible
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    updateCounter();
                    observer.unobserve(entry.target);
                }
            });
        });

        observer.observe(counter);
    });
}

/**
 * Intersection Observer for scroll animations
 */
function initIntersectionObserver() {
    const animatedElements = document.querySelectorAll('.bc-slide-in-up, .bc-slide-in-left, .bc-slide-in-right, .bc-fade-in');
    
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.style.opacity = '1';
                entry.target.style.transform = 'translateY(0) translateX(0)';
                observer.unobserve(entry.target);
            }
        });
    }, {
        threshold: 0.1,
        rootMargin: '0px 0px -50px 0px'
    });

    animatedElements.forEach(element => {
        // Set initial state
        element.style.opacity = '0';
        if (element.classList.contains('bc-slide-in-up')) {
            element.style.transform = 'translateY(30px)';
        } else if (element.classList.contains('bc-slide-in-left')) {
            element.style.transform = 'translateX(-30px)';
        } else if (element.classList.contains('bc-slide-in-right')) {
            element.style.transform = 'translateX(30px)';
        }
        
        element.style.transition = 'opacity 0.6s ease, transform 0.6s ease';
        observer.observe(element);
    });
}

/**
 * Smooth scrolling for anchor links
 */
function initSmoothScrolling() {
    const smoothScrollLinks = document.querySelectorAll('.bc-smooth-scroll');
    
    smoothScrollLinks.forEach(link => {
        link.addEventListener('click', function(e) {
            const href = this.getAttribute('href');
            if (href.startsWith('#')) {
                e.preventDefault();
                const target = document.querySelector(href);
                if (target) {
                    target.scrollIntoView({
                        behavior: 'smooth',
                        block: 'start'
                    });
                }
            }
        });
    });
}

/**
 * Enhanced network visualization animation
 */
function initNetworkAnimation() {
    const networkContainer = document.querySelector('.bc-network-container');
    if (!networkContainer) return;

    const nodes = networkContainer.querySelectorAll('.bc-network-node');
    const connections = networkContainer.querySelectorAll('.bc-network-connection');

    // Add hover effects to nodes
    nodes.forEach((node, index) => {
        node.addEventListener('mouseenter', function() {
            // Highlight connected nodes
            connections.forEach(connection => {
                connection.style.opacity = '1';
                connection.style.transform += ' scale(1.2)';
            });
        });

        node.addEventListener('mouseleave', function() {
            // Reset connections
            connections.forEach(connection => {
                connection.style.opacity = '';
                connection.style.transform = connection.style.transform.replace(' scale(1.2)', '');
            });
        });
    });

    // Add click animation
    nodes.forEach(node => {
        node.addEventListener('click', function() {
            this.style.transform += ' scale(1.3)';
            setTimeout(() => {
                this.style.transform = this.style.transform.replace(' scale(1.3)', '');
            }, 200);
        });
    });
}

/**
 * Enhanced ripple effect for buttons
 */
function addRippleEffect(element, event) {
    const ripple = document.createElement('span');
    const rect = element.getBoundingClientRect();
    const size = Math.max(rect.width, rect.height);
    const x = event.clientX - rect.left - size / 2;
    const y = event.clientY - rect.top - size / 2;
    
    ripple.style.width = ripple.style.height = size + 'px';
    ripple.style.left = x + 'px';
    ripple.style.top = y + 'px';
    ripple.classList.add('bc-ripple-effect');
    
    element.appendChild(ripple);
    
    setTimeout(() => {
        ripple.remove();
    }, 600);
}

// Add ripple effect to buttons with bc-ripple class
document.addEventListener('click', function(e) {
    if (e.target.closest('.bc-ripple')) {
        const button = e.target.closest('.bc-ripple');
        addRippleEffect(button, e);
    }
});

/**
 * Progress bar animation
 */
function animateProgressBar() {
    const progressFill = document.querySelector('.bc-progress-fill');
    if (!progressFill) return;

    const targetWidth = progressFill.style.width;
    progressFill.style.width = '0%';
    
    setTimeout(() => {
        progressFill.style.width = targetWidth;
    }, 500);
}

// Initialize progress bar animation when visible
const progressBar = document.querySelector('.bc-progress-bar');
if (progressBar) {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                animateProgressBar();
                observer.unobserve(entry.target);
            }
        });
    });
    observer.observe(progressBar);
}

/**
 * Add loading states to action buttons
 */
document.querySelectorAll('.bc-action-link').forEach(link => {
    link.addEventListener('click', function(e) {
        if (this.getAttribute('onclick') === 'return false;') {
            e.preventDefault();
            
            const originalText = this.innerHTML;
            this.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Loading...';
            this.style.pointerEvents = 'none';
            
            setTimeout(() => {
                this.innerHTML = originalText;
                this.style.pointerEvents = '';
            }, 1500);
        }
    });
});

/**
 * Suggestion card interactions
 */
document.querySelectorAll('.bc-suggestion-item .btn-primary').forEach(button => {
    button.addEventListener('click', function() {
        const originalText = this.innerHTML;
        this.innerHTML = '<i class="fas fa-check me-1"></i>Sent';
        this.classList.remove('btn-primary');
        this.classList.add('btn-success');
        this.disabled = true;
        
        // Show success message
        const suggestionItem = this.closest('.bc-suggestion-item');
        const successMessage = document.createElement('div');
        successMessage.className = 'alert alert-success alert-sm mt-2';
        successMessage.innerHTML = '<i class="fas fa-check-circle me-1"></i>Connection request sent!';
        suggestionItem.appendChild(successMessage);
        
        setTimeout(() => {
            successMessage.remove();
        }, 3000);
    });
});
