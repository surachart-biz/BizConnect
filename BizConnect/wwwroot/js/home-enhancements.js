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

    // Initialize new interactive elements
    initInteractiveStats();
    initInteractiveCards();
    initInteractiveTimeline();
    initEnhancedNetwork();
    initProgressBars();
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
    nodes.forEach((node) => {
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

/**
 * NEW INTERACTIVE FUNCTIONS
 */

/**
 * Interactive Stats with Click Actions
 */
function initInteractiveStats() {
    const interactiveStats = document.querySelectorAll('.bc-stat-interactive');

    interactiveStats.forEach(stat => {
        stat.addEventListener('click', function() {
            const statType = this.dataset.stat || this.dataset.tooltip;

            // Add click animation
            this.style.transform = 'scale(0.95)';
            setTimeout(() => {
                this.style.transform = '';
            }, 150);

            // Trigger specific action based on stat type
            if (statType && statType.includes('network')) {
                showNetworkModal();
            } else if (statType && statType.includes('opportunities')) {
                showOpportunitiesModal();
            }

            // Add ripple effect
            addRippleEffect(this, e);
        });

        // Add keyboard support
        stat.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                this.click();
            }
        });
    });
}

/**
 * Interactive Action Cards
 */
function initInteractiveCards() {
    const interactiveCards = document.querySelectorAll('.bc-interactive-card');

    interactiveCards.forEach(card => {
        // Initialize progress bars on hover
        card.addEventListener('mouseenter', function() {
            const progressBar = this.querySelector('.bc-progress-fill-animated');
            if (progressBar) {
                const progress = progressBar.dataset.progress || '0';
                progressBar.style.width = progress + '%';
            }

            // Start counter animation
            const counter = this.querySelector('.bc-counter');
            if (counter && !counter.dataset.animated) {
                animateCounter(counter);
                counter.dataset.animated = 'true';
            }
        });

        // Add click interaction
        card.addEventListener('click', function(e) {
            if (!e.target.closest('a, button')) {
                const link = this.querySelector('.bc-action-link');
                if (link) {
                    link.click();
                }
            }
        });
    });
}

/**
 * Interactive Timeline
 */
function initInteractiveTimeline() {
    const timelineItems = document.querySelectorAll('.bc-activity-interactive');

    timelineItems.forEach(item => {
        // Add click to expand details
        item.addEventListener('click', function() {
            const details = this.querySelector('.bc-activity-details');
            if (details) {
                const isExpanded = details.style.maxHeight && details.style.maxHeight !== '0px';

                if (isExpanded) {
                    details.style.maxHeight = '0px';
                    details.style.opacity = '0';
                } else {
                    details.style.maxHeight = details.scrollHeight + 'px';
                    details.style.opacity = '1';
                }
            }
        });

        // Add hover sound effect
        item.addEventListener('mouseenter', function() {
            playInteractionSound(400, 50);
        });
    });

    // Initialize relative time updates
    updateRelativeTimes();
    setInterval(updateRelativeTimes, 60000); // Update every minute
}

/**
 * Enhanced Network Visualization
 */
function initEnhancedNetwork() {
    const networkContainer = document.querySelector('.bc-interactive-network');
    if (!networkContainer) return;

    const nodes = networkContainer.querySelectorAll('.bc-node-interactive');

    nodes.forEach(node => {
        // Add click interaction
        node.addEventListener('click', function() {
            const nodeType = this.classList.contains('bc-node-center') ? 'profile' :
                           this.classList.contains('bc-node-1') ? 'jobs' :
                           this.classList.contains('bc-node-2') ? 'network' :
                           this.classList.contains('bc-node-3') ? 'events' : 'growth';

            // Animate click
            this.style.transform = 'scale(1.3)';
            setTimeout(() => {
                this.style.transform = '';
            }, 300);

            // Show relevant content
            showNodeContent(nodeType);

            // Play sound
            playInteractionSound(600 + Math.random() * 200, 100);
        });

        // Add hover effects
        node.addEventListener('mouseenter', function() {
            // Highlight connected paths
            const connections = networkContainer.querySelectorAll('.bc-connection-interactive');
            connections.forEach(connection => {
                connection.style.opacity = '1';
                connection.style.transform = 'scale(1.1)';
            });
        });

        node.addEventListener('mouseleave', function() {
            // Reset connections
            const connections = networkContainer.querySelectorAll('.bc-connection-interactive');
            connections.forEach(connection => {
                connection.style.opacity = '';
                connection.style.transform = '';
            });
        });
    });
}

/**
 * Progress Bar Animations
 */
function initProgressBars() {
    const progressBars = document.querySelectorAll('.bc-progress-fill-animated');

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const progressBar = entry.target;
                const progress = progressBar.dataset.progress || '0';

                setTimeout(() => {
                    progressBar.style.width = progress + '%';
                }, 200);

                observer.unobserve(progressBar);
            }
        });
    });

    progressBars.forEach(bar => {
        bar.style.width = '0%';
        observer.observe(bar);
    });
}

/**
 * HELPER FUNCTIONS
 */

/**
 * Animate counter with easing
 */
function animateCounter(element) {
    const target = parseInt(element.dataset.target) || 0;
    const duration = 1000;
    let startTime = null;

    const easeOutQuart = (t) => 1 - (--t) * t * t * t;

    const animate = (timestamp) => {
        if (!startTime) startTime = timestamp;
        const progress = Math.min((timestamp - startTime) / duration, 1);
        const easedProgress = easeOutQuart(progress);
        const current = Math.floor(easedProgress * target);

        element.textContent = current;

        if (progress < 1) {
            requestAnimationFrame(animate);
        } else {
            element.textContent = target;
        }
    };

    requestAnimationFrame(animate);
}

/**
 * Show network modal (placeholder)
 */
function showNetworkModal() {
    // Create a simple modal or redirect
    console.log('Opening network view...');
    // In a real app, this would open a modal or navigate to network page
}

/**
 * Show opportunities modal (placeholder)
 */
function showOpportunitiesModal() {
    console.log('Opening opportunities view...');
    // In a real app, this would open a modal or navigate to jobs page
}

/**
 * Show node content based on type
 */
function showNodeContent(nodeType) {
    const messages = {
        profile: 'Your profile is 85% complete. Add more skills to improve visibility!',
        jobs: 'You have 8 job opportunities waiting. 3 are perfect matches!',
        network: 'Your network has grown by 12% this month. 5 new connection requests!',
        events: '3 networking events this week. RSVP to "Tech Leaders Meetup"?',
        growth: 'Your career score increased by 15 points. You\'re in the top 10%!'
    };

    // Show toast notification
    showToast(messages[nodeType] || 'Feature coming soon!');
}

/**
 * Show toast notification
 */
function showToast(message) {
    const toast = document.createElement('div');
    toast.className = 'bc-toast';
    toast.innerHTML = `
        <div class="bc-toast-content">
            <i class="fas fa-info-circle me-2"></i>
            ${message}
        </div>
    `;

    // Add toast styles
    toast.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        background: var(--bc-white);
        border: 1px solid var(--bc-gray-200);
        border-radius: var(--bc-radius-lg);
        padding: var(--bc-spacing-3) var(--bc-spacing-4);
        box-shadow: var(--bc-shadow-xl);
        z-index: 9999;
        max-width: 300px;
        transform: translateX(100%);
        transition: transform 0.3s ease;
    `;

    document.body.appendChild(toast);

    // Animate in
    setTimeout(() => {
        toast.style.transform = 'translateX(0)';
    }, 100);

    // Remove after delay
    setTimeout(() => {
        toast.style.transform = 'translateX(100%)';
        setTimeout(() => {
            if (toast.parentNode) {
                toast.remove();
            }
        }, 300);
    }, 4000);
}

/**
 * Update relative times
 */
function updateRelativeTimes() {
    const timeElements = document.querySelectorAll('.bc-relative-time');

    timeElements.forEach(element => {
        const timestamp = parseInt(element.dataset.timestamp);
        if (timestamp) {
            const now = new Date();
            const past = new Date(now.getTime() - (timestamp * 60 * 60 * 1000)); // hours ago
            const diff = now - past;

            const hours = Math.floor(diff / (1000 * 60 * 60));
            const days = Math.floor(hours / 24);

            let relativeTime;
            if (days > 0) {
                relativeTime = `${days} day${days > 1 ? 's' : ''} ago`;
            } else if (hours > 0) {
                relativeTime = `${hours} hour${hours > 1 ? 's' : ''} ago`;
            } else {
                relativeTime = 'Just now';
            }

            element.textContent = relativeTime;
        }
    });
}

/**
 * Play interaction sound
 */
function playInteractionSound(frequency = 800, duration = 100) {
    // Simple sound feedback (only if audio context is available)
    if (typeof AudioContext !== 'undefined') {
        try {
            const audioContext = new AudioContext();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);

            oscillator.frequency.setValueAtTime(frequency, audioContext.currentTime);
            oscillator.type = 'sine';

            gainNode.gain.setValueAtTime(0, audioContext.currentTime);
            gainNode.gain.linearRampToValueAtTime(0.005, audioContext.currentTime + 0.01);
            gainNode.gain.exponentialRampToValueAtTime(0.001, audioContext.currentTime + duration / 1000);

            oscillator.start(audioContext.currentTime);
            oscillator.stop(audioContext.currentTime + duration / 1000);
        } catch (e) {
            // Audio not supported, fail silently
        }
    }
}
