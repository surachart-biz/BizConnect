/**
 * Mobile Optimization System for BizConnect
 * Provides touch-friendly interactions, responsive timing, and performance optimizations
 */

class MobileOptimizer {
    constructor() {
        this.isMobile = this.detectMobileDevice();
        this.isTouchDevice = 'ontouchstart' in window;
        this.isIOSDevice = /iPad|iPhone|iPod/.test(navigator.userAgent);
        this.isAndroidDevice = /Android/.test(navigator.userAgent);
        this.touchHandlers = new Map();
        this.gestureHandlers = new Map();
        this.performanceMode = 'auto'; // 'high', 'balanced', 'power-save', 'auto'
        this.lastTouchTime = 0;
        
        this.init();
    }

    init() {
        if (this.isMobile || this.isTouchDevice) {
            this.optimizeForMobile();
            this.setupTouchInteractions();
            this.setupGestureHandling();
            this.optimizePerformance();
            this.setupViewportHandling();
        }
        
        this.setupResponsiveTimings();
        this.monitorPerformance();
    }

    detectMobileDevice() {
        const userAgent = navigator.userAgent.toLowerCase();
        const mobileKeywords = [
            'mobile', 'android', 'iphone', 'ipad', 'ipod', 'blackberry', 
            'windows phone', 'opera mini', 'iemobile', 'wpdesktop'
        ];
        
        return mobileKeywords.some(keyword => userAgent.includes(keyword)) ||
               window.innerWidth <= 768 ||
               (window.screen && window.screen.width <= 768);
    }

    optimizeForMobile() {
        // Add mobile class to body
        document.body.classList.add('mobile-optimized');
        
        // Optimize animation durations for mobile
        const root = document.documentElement;
        root.style.setProperty('--duration-fast-mobile', '0.1s');
        root.style.setProperty('--duration-normal-mobile', '0.2s');
        root.style.setProperty('--duration-slow-mobile', '0.3s');
        
        // Disable complex hover effects
        this.disableHoverEffects();
        
        // Optimize scroll behavior
        this.optimizeScrolling();
        
        // Reduce backdrop filter usage for performance
        this.optimizeBackdropFilters();
        
        console.log('📱 Mobile optimizations applied');
    }

    disableHoverEffects() {
        const style = document.createElement('style');
        style.innerHTML = `
            @media (hover: none) and (pointer: coarse) {
                .hover-lift:hover,
                .hover-scale:hover,
                .hover-glow:hover,
                .card-modern:hover {
                    transform: none !important;
                    box-shadow: inherit !important;
                }
                
                .btn-modern:hover::before {
                    left: -100% !important;
                }
            }
            
            .mobile-optimized .complex-hover {
                display: none;
            }
            
            .mobile-optimized .parallax-element {
                transform: none !important;
            }
        `;
        document.head.appendChild(style);
    }

    setupTouchInteractions() {
        // Enhanced touch feedback for interactive elements
        const touchElements = document.querySelectorAll(`
            .btn, .card, .quick-action-item, .timeline-content,
            .kpi-card, .list-group-item, .dropdown-item, .nav-link
        `);

        touchElements.forEach(element => {
            this.addTouchFeedback(element);
        });

        // Setup touch-specific event handling
        this.setupTouchEvents();
        
        console.log(`🖐️ Touch interactions setup for ${touchElements.length} elements`);
    }

    addTouchFeedback(element) {
        let touchStartTime = 0;
        let touchMoved = false;
        let touchFeedbackTimeout;

        const touchStart = (e) => {
            touchStartTime = Date.now();
            touchMoved = false;
            this.lastTouchTime = touchStartTime;
            
            // Visual feedback
            element.style.transform = 'scale(0.98)';
            element.style.opacity = '0.85';
            element.style.transition = 'all 0.1s ease-out';
            
            // Add touch ripple effect
            this.createTouchRipple(element, e.touches[0]);
            
            // Haptic feedback if available
            if ('vibrate' in navigator) {
                navigator.vibrate(10);
            }
        };

        const touchMove = () => {
            touchMoved = true;
            this.resetTouchFeedback(element);
        };

        const touchEnd = (e) => {
            const touchDuration = Date.now() - touchStartTime;
            
            if (!touchMoved && touchDuration < 500) {
                // Quick tap - enhanced feedback
                this.enhancedTapFeedback(element);
            }
            
            // Reset visual state after delay
            touchFeedbackTimeout = setTimeout(() => {
                this.resetTouchFeedback(element);
            }, 150);
        };

        const touchCancel = () => {
            this.resetTouchFeedback(element);
        };

        // Store handlers for cleanup
        const handlers = { touchStart, touchMove, touchEnd, touchCancel };
        this.touchHandlers.set(element, handlers);

        // Add event listeners with passive option for performance
        element.addEventListener('touchstart', touchStart, { passive: true });
        element.addEventListener('touchmove', touchMove, { passive: true });
        element.addEventListener('touchend', touchEnd, { passive: true });
        element.addEventListener('touchcancel', touchCancel, { passive: true });
    }

    createTouchRipple(element, touch) {
        const rect = element.getBoundingClientRect();
        const size = Math.max(rect.width, rect.height) * 1.2;
        const x = touch.clientX - rect.left - size / 2;
        const y = touch.clientY - rect.top - size / 2;

        const ripple = document.createElement('div');
        ripple.className = 'touch-ripple';
        ripple.style.cssText = `
            position: absolute;
            width: ${size}px;
            height: ${size}px;
            left: ${x}px;
            top: ${y}px;
            background: rgba(255, 255, 255, 0.3);
            border-radius: 50%;
            transform: scale(0);
            animation: touch-ripple-animation 0.6s ease-out;
            pointer-events: none;
            z-index: 100;
        `;

        // Ensure element has relative positioning
        const originalPosition = getComputedStyle(element).position;
        if (originalPosition === 'static') {
            element.style.position = 'relative';
        }
        
        element.style.overflow = 'hidden';
        element.appendChild(ripple);

        setTimeout(() => {
            if (ripple.parentNode) {
                ripple.remove();
            }
        }, 600);
    }

    enhancedTapFeedback(element) {
        element.style.transform = 'scale(1.02)';
        element.style.transition = 'all 0.1s cubic-bezier(0.4, 0, 0.2, 1)';
        
        setTimeout(() => {
            element.style.transform = 'scale(1)';
        }, 100);
    }

    resetTouchFeedback(element) {
        element.style.transform = '';
        element.style.opacity = '';
        element.style.transition = '';
    }

    setupTouchEvents() {
        // Add required CSS for touch ripples
        if (!document.getElementById('touch-ripple-styles')) {
            const style = document.createElement('style');
            style.id = 'touch-ripple-styles';
            style.innerHTML = `
                @keyframes touch-ripple-animation {
                    to {
                        transform: scale(2);
                        opacity: 0;
                    }
                }
                
                .touch-ripple {
                    pointer-events: none !important;
                }
            `;
            document.head.appendChild(style);
        }
    }

    setupGestureHandling() {
        // Swipe gesture detection
        let touchStartX = 0;
        let touchStartY = 0;
        let touchEndX = 0;
        let touchEndY = 0;

        const handleTouchStart = (e) => {
            touchStartX = e.changedTouches[0].screenX;
            touchStartY = e.changedTouches[0].screenY;
        };

        const handleTouchEnd = (e) => {
            touchEndX = e.changedTouches[0].screenX;
            touchEndY = e.changedTouches[0].screenY;
            this.handleSwipe(touchStartX, touchStartY, touchEndX, touchEndY, e.target);
        };

        document.addEventListener('touchstart', handleTouchStart, { passive: true });
        document.addEventListener('touchend', handleTouchEnd, { passive: true });

        // Pinch zoom detection for specific elements
        this.setupPinchZoom();
    }

    handleSwipe(startX, startY, endX, endY, target) {
        const diffX = startX - endX;
        const diffY = startY - endY;
        const minSwipeDistance = 50;
        
        if (Math.abs(diffX) > Math.abs(diffY)) {
            if (Math.abs(diffX) > minSwipeDistance) {
                if (diffX > 0) {
                    this.handleSwipeLeft(target);
                } else {
                    this.handleSwipeRight(target);
                }
            }
        } else {
            if (Math.abs(diffY) > minSwipeDistance) {
                if (diffY > 0) {
                    this.handleSwipeUp(target);
                } else {
                    this.handleSwipeDown(target);
                }
            }
        }
    }

    handleSwipeLeft(target) {
        // Implement swipe left functionality
        const event = new CustomEvent('swipeleft', { detail: { target } });
        target.dispatchEvent(event);
    }

    handleSwipeRight(target) {
        // Implement swipe right functionality
        const event = new CustomEvent('swiperight', { detail: { target } });
        target.dispatchEvent(event);
    }

    handleSwipeUp(target) {
        // Implement swipe up functionality
        const event = new CustomEvent('swipeup', { detail: { target } });
        target.dispatchEvent(event);
    }

    handleSwipeDown(target) {
        // Implement swipe down functionality  
        const event = new CustomEvent('swipedown', { detail: { target } });
        target.dispatchEvent(event);
    }

    setupPinchZoom() {
        // Pinch zoom for specific elements like charts or images
        const zoomableElements = document.querySelectorAll('.zoomable, .chart-container');
        
        zoomableElements.forEach(element => {
            let initialDistance = 0;
            let initialScale = 1;
            
            element.addEventListener('touchstart', (e) => {
                if (e.touches.length === 2) {
                    initialDistance = this.getDistance(e.touches[0], e.touches[1]);
                    initialScale = this.getCurrentScale(element);
                }
            }, { passive: true });
            
            element.addEventListener('touchmove', (e) => {
                if (e.touches.length === 2) {
                    const currentDistance = this.getDistance(e.touches[0], e.touches[1]);
                    const scale = (currentDistance / initialDistance) * initialScale;
                    const clampedScale = Math.min(Math.max(scale, 0.5), 3);
                    
                    element.style.transform = `scale(${clampedScale})`;
                    e.preventDefault();
                }
            });
        });
    }

    getDistance(touch1, touch2) {
        const dx = touch1.clientX - touch2.clientX;
        const dy = touch1.clientY - touch2.clientY;
        return Math.sqrt(dx * dx + dy * dy);
    }

    getCurrentScale(element) {
        const transform = getComputedStyle(element).transform;
        if (transform === 'none') return 1;
        
        const matrix = transform.match(/matrix\(([^)]+)\)/);
        if (!matrix) return 1;
        
        const values = matrix[1].split(',').map(parseFloat);
        return values[0]; // Scale X value
    }

    optimizePerformance() {
        // Detect device capabilities
        const isLowEndDevice = this.detectLowEndDevice();
        
        if (isLowEndDevice) {
            this.performanceMode = 'power-save';
            this.applyPowerSaveOptimizations();
        } else {
            this.performanceMode = 'balanced';
            this.applyBalancedOptimizations();
        }

        // Optimize based on battery level if available
        if ('getBattery' in navigator) {
            navigator.getBattery().then(battery => {
                if (battery.level < 0.2) {
                    this.performanceMode = 'power-save';
                    this.applyPowerSaveOptimizations();
                }
            });
        }
    }

    detectLowEndDevice() {
        // Basic heuristics for low-end device detection
        const cores = navigator.hardwareConcurrency || 2;
        const memory = navigator.deviceMemory || 2;
        const connectionSpeed = navigator.connection?.effectiveType || '4g';
        
        return cores <= 2 || memory <= 2 || ['slow-2g', '2g'].includes(connectionSpeed);
    }

    applyPowerSaveOptimizations() {
        document.body.classList.add('power-save-mode');
        
        const style = document.createElement('style');
        style.innerHTML = `
            .power-save-mode * {
                animation-duration: 0.1s !important;
                transition-duration: 0.1s !important;
            }
            
            .power-save-mode .backdrop-filter,
            .power-save-mode .glass,
            .power-save-mode .glass-card {
                backdrop-filter: none !important;
            }
            
            .power-save-mode .skeleton-shimmer,
            .power-save-mode .loading-dots,
            .power-save-mode .wave-loader {
                animation: none !important;
            }
            
            .power-save-mode .parallax-element {
                transform: none !important;
            }
        `;
        document.head.appendChild(style);
        
        console.log('🔋 Power-save optimizations applied');
    }

    applyBalancedOptimizations() {
        document.body.classList.add('balanced-mode');
        
        // Slightly reduce animation complexity
        const style = document.createElement('style');
        style.innerHTML = `
            .balanced-mode .complex-animation {
                animation-duration: 0.2s !important;
            }
        `;
        document.head.appendChild(style);
        
        console.log('⚖️ Balanced optimizations applied');
    }

    setupViewportHandling() {
        // Handle viewport changes for mobile browsers
        const handleViewportChange = () => {
            // Update viewport height for mobile browsers
            const vh = window.innerHeight * 0.01;
            document.documentElement.style.setProperty('--vh', `${vh}px`);
            
            // Handle keyboard appearance on iOS
            if (this.isIOSDevice) {
                this.handleIOSKeyboard();
            }
        };

        window.addEventListener('resize', handleViewportChange, { passive: true });
        window.addEventListener('orientationchange', () => {
            setTimeout(handleViewportChange, 500);
        }, { passive: true });

        // Initial call
        handleViewportChange();
    }

    handleIOSKeyboard() {
        const inputs = document.querySelectorAll('input, textarea, select');
        
        inputs.forEach(input => {
            input.addEventListener('focus', () => {
                setTimeout(() => {
                    input.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }, 300);
            });
        });
    }

    optimizeScrolling() {
        // Add momentum scrolling for iOS
        if (this.isIOSDevice) {
            document.body.style.webkitOverflowScrolling = 'touch';
        }

        // Optimize scroll event handling
        let ticking = false;
        
        const optimizedScrollHandler = () => {
            if (!ticking) {
                requestAnimationFrame(() => {
                    this.handleOptimizedScroll();
                    ticking = false;
                });
                ticking = true;
            }
        };

        window.addEventListener('scroll', optimizedScrollHandler, { passive: true });
    }

    handleOptimizedScroll() {
        // Only update elements that are visible
        const scrollTop = window.pageYOffset;
        const windowHeight = window.innerHeight;
        
        // Update scroll-dependent elements
        const scrollElements = document.querySelectorAll('[data-scroll-optimize]');
        
        scrollElements.forEach(element => {
            const rect = element.getBoundingClientRect();
            const isVisible = rect.top < windowHeight && rect.bottom > 0;
            
            if (isVisible) {
                element.classList.add('in-viewport');
            } else {
                element.classList.remove('in-viewport');
            }
        });
    }

    optimizeBackdropFilters() {
        if (this.performanceMode === 'power-save') {
            const style = document.createElement('style');
            style.innerHTML = `
                .mobile-optimized .backdrop-filter,
                .mobile-optimized .glass,
                .mobile-optimized .glass-card,
                .modal-content {
                    backdrop-filter: none !important;
                    background: rgba(255, 255, 255, 0.98) !important;
                }
            `;
            document.head.appendChild(style);
        }
    }

    setupResponsiveTimings() {
        // Adjust animation timings based on device capabilities
        const root = document.documentElement;
        
        if (this.isMobile) {
            root.style.setProperty('--duration-fast', '0.15s');
            root.style.setProperty('--duration-normal', '0.25s');
            root.style.setProperty('--duration-slow', '0.4s');
        }
        
        // Reduce timings for low-end devices
        if (this.performanceMode === 'power-save') {
            root.style.setProperty('--duration-fast', '0.1s');
            root.style.setProperty('--duration-normal', '0.15s');
            root.style.setProperty('--duration-slow', '0.2s');
        }
    }

    monitorPerformance() {
        // Monitor frame rate and adjust performance mode
        let frameCount = 0;
        let lastTime = performance.now();
        
        const monitor = () => {
            frameCount++;
            const currentTime = performance.now();
            
            if (currentTime >= lastTime + 1000) {
                const fps = Math.round((frameCount * 1000) / (currentTime - lastTime));
                
                if (fps < 30 && this.performanceMode !== 'power-save') {
                    console.log(`📊 Low FPS detected (${fps}). Switching to power-save mode.`);
                    this.performanceMode = 'power-save';
                    this.applyPowerSaveOptimizations();
                }
                
                frameCount = 0;
                lastTime = currentTime;
            }
            
            requestAnimationFrame(monitor);
        };
        
        // Only monitor performance on mobile devices
        if (this.isMobile) {
            requestAnimationFrame(monitor);
        }
    }

    // Public API methods
    enableHighPerformanceMode() {
        this.performanceMode = 'high';
        document.body.classList.remove('power-save-mode', 'balanced-mode');
        document.body.classList.add('high-performance-mode');
    }

    enablePowerSaveMode() {
        this.performanceMode = 'power-save';
        this.applyPowerSaveOptimizations();
    }

    addSwipeHandler(element, direction, callback) {
        element.addEventListener(`swipe${direction}`, callback);
    }

    removeSwipeHandler(element, direction, callback) {
        element.removeEventListener(`swipe${direction}`, callback);
    }

    destroy() {
        // Clean up touch handlers
        this.touchHandlers.forEach((handlers, element) => {
            element.removeEventListener('touchstart', handlers.touchStart);
            element.removeEventListener('touchmove', handlers.touchMove);
            element.removeEventListener('touchend', handlers.touchEnd);
            element.removeEventListener('touchcancel', handlers.touchCancel);
        });
        
        this.touchHandlers.clear();
        this.gestureHandlers.clear();
    }
}

// Initialize mobile optimizer
document.addEventListener('DOMContentLoaded', () => {
    window.mobileOptimizer = new MobileOptimizer();
    console.log('📱 Mobile Optimizer Initialized');
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = MobileOptimizer;
}