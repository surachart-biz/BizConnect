/**
 * BizConnect Performance Monitoring System
 * Monitors loading times, UI performance, and user experience metrics
 * Ensures compliance with < 2 second loading time requirement
 */

class PerformanceMonitor {
    constructor(options = {}) {
        this.options = {
            trackingEnabled: true,
            maxLoadTime: 2000, // 2 seconds requirement
            reportingEndpoint: '/api/performance/report',
            debugMode: false,
            ...options
        };
        
        this.metrics = {
            pageLoadTime: 0,
            domContentLoadedTime: 0,
            firstContentfulPaint: 0,
            largestContentfulPaint: 0,
            cumulativeLayoutShift: 0,
            firstInputDelay: 0,
            cssLoadTime: 0,
            jsLoadTime: 0,
            imageLoadTime: 0,
            animationPerformance: {},
            responsiveBreakpoints: {}
        };
        
        this.warnings = [];
        this.initialize();
    }
    
    initialize() {
        if (!this.options.trackingEnabled) return;
        
        // Wait for DOM to be ready
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.startTracking());
        } else {
            this.startTracking();
        }
        
        // Track page visibility changes
        document.addEventListener('visibilitychange', () => this.handleVisibilityChange());
        
        // Track window resize for responsive performance
        window.addEventListener('resize', debounce(() => this.trackResponsivePerformance(), 250));
    }
    
    startTracking() {
        this.trackPageLoadMetrics();
        this.trackResourceLoadTimes();
        this.trackWebVitals();
        this.trackAnimationPerformance();
        this.trackResponsivePerformance();
        this.validateLoadTime();
        
        if (this.options.debugMode) {
            this.displayPerformanceReport();
        }
    }
    
    trackPageLoadMetrics() {
        if (!window.performance) return;
        
        const navigation = performance.getEntriesByType('navigation')[0];
        if (navigation) {
            this.metrics.pageLoadTime = navigation.loadEventEnd - navigation.loadEventStart;
            this.metrics.domContentLoadedTime = navigation.domContentLoadedEventEnd - navigation.domContentLoadedEventStart;
            
            if (this.options.debugMode) {
                console.log('[Performance] Page Load Time:', this.metrics.pageLoadTime, 'ms');
                console.log('[Performance] DOM Content Loaded:', this.metrics.domContentLoadedTime, 'ms');
            }
        }
    }
    
    trackResourceLoadTimes() {
        if (!window.performance) return;
        
        const resources = performance.getEntriesByType('resource');
        
        // Track CSS load times
        const cssResources = resources.filter(resource => 
            resource.name.includes('.css') || 
            resource.name.includes('modern-ui') ||
            resource.name.includes('components') ||
            resource.name.includes('responsive') ||
            resource.name.includes('animations')
        );
        
        this.metrics.cssLoadTime = cssResources.reduce((total, resource) => 
            total + (resource.responseEnd - resource.requestStart), 0) / cssResources.length || 0;
        
        // Track JS load times
        const jsResources = resources.filter(resource => 
            resource.name.includes('.js') && 
            (resource.name.includes('ui-interactions') || 
             resource.name.includes('performance-monitor'))
        );
        
        this.metrics.jsLoadTime = jsResources.reduce((total, resource) => 
            total + (resource.responseEnd - resource.requestStart), 0) / jsResources.length || 0;
        
        // Track image load times
        const imageResources = resources.filter(resource => 
            resource.name.match(/\.(jpg|jpeg|png|gif|svg|webp)$/i)
        );
        
        this.metrics.imageLoadTime = imageResources.reduce((total, resource) => 
            total + (resource.responseEnd - resource.requestStart), 0) / imageResources.length || 0;
        
        if (this.options.debugMode) {
            console.log('[Performance] CSS Load Time:', this.metrics.cssLoadTime, 'ms');
            console.log('[Performance] JS Load Time:', this.metrics.jsLoadTime, 'ms');
            console.log('[Performance] Image Load Time:', this.metrics.imageLoadTime, 'ms');
        }
    }
    
    trackWebVitals() {
        // First Contentful Paint
        if (window.PerformanceObserver) {
            try {
                new PerformanceObserver((list) => {
                    for (const entry of list.getEntries()) {
                        if (entry.name === 'first-contentful-paint') {
                            this.metrics.firstContentfulPaint = entry.startTime;
                        }
                    }
                }).observe({entryTypes: ['paint']});
                
                // Largest Contentful Paint
                new PerformanceObserver((list) => {
                    const entries = list.getEntries();
                    const lastEntry = entries[entries.length - 1];
                    this.metrics.largestContentfulPaint = lastEntry.startTime;
                }).observe({entryTypes: ['largest-contentful-paint']});
                
                // Cumulative Layout Shift
                new PerformanceObserver((list) => {
                    for (const entry of list.getEntries()) {
                        if (!entry.hadRecentInput) {
                            this.metrics.cumulativeLayoutShift += entry.value;
                        }
                    }
                }).observe({entryTypes: ['layout-shift']});
                
            } catch (e) {
                if (this.options.debugMode) {
                    console.warn('[Performance] Web Vitals tracking not fully supported:', e);
                }
            }
        }
    }
    
    trackAnimationPerformance() {
        const animatedElements = document.querySelectorAll('[class*="fade-"], [class*="bounce-"], [class*="slide-"], [class*="scale-"], [class*="zoom-"]');
        
        let totalAnimationTime = 0;
        let animationCount = 0;
        
        animatedElements.forEach(element => {
            const startTime = performance.now();
            
            element.addEventListener('animationend', () => {
                const endTime = performance.now();
                const duration = endTime - startTime;
                totalAnimationTime += duration;
                animationCount++;
                
                if (duration > 1000) { // Animations over 1 second
                    this.warnings.push(`Long animation detected on ${element.className}: ${duration}ms`);
                }
            }, { once: true });
        });
        
        // Store animation performance metrics
        setTimeout(() => {
            this.metrics.animationPerformance = {
                averageDuration: animationCount > 0 ? totalAnimationTime / animationCount : 0,
                totalAnimations: animationCount,
                totalTime: totalAnimationTime
            };
        }, 3000); // Allow time for animations to complete
    }
    
    trackResponsivePerformance() {
        const breakpoints = {
            'xs': window.matchMedia('(max-width: 575.98px)'),
            'sm': window.matchMedia('(min-width: 576px) and (max-width: 767.98px)'),
            'md': window.matchMedia('(min-width: 768px) and (max-width: 991.98px)'),
            'lg': window.matchMedia('(min-width: 992px) and (max-width: 1199.98px)'),
            'xl': window.matchMedia('(min-width: 1200px) and (max-width: 1399.98px)'),
            'xxl': window.matchMedia('(min-width: 1400px)')
        };
        
        Object.entries(breakpoints).forEach(([name, mediaQuery]) => {
            if (mediaQuery.matches) {
                const startTime = performance.now();
                
                // Force a layout recalculation
                document.body.offsetHeight;
                
                const endTime = performance.now();
                this.metrics.responsiveBreakpoints[name] = {
                    active: true,
                    layoutTime: endTime - startTime
                };
            }
        });
        
        if (this.options.debugMode) {
            console.log('[Performance] Responsive Performance:', this.metrics.responsiveBreakpoints);
        }
    }
    
    validateLoadTime() {
        const totalLoadTime = this.metrics.pageLoadTime + this.metrics.cssLoadTime + this.metrics.jsLoadTime;
        
        if (totalLoadTime > this.options.maxLoadTime) {
            this.warnings.push(`Total load time (${totalLoadTime}ms) exceeds requirement (${this.options.maxLoadTime}ms)`);
            
            if (this.options.debugMode) {
                console.warn(`[Performance] Load time requirement failed: ${totalLoadTime}ms > ${this.options.maxLoadTime}ms`);
            }
        } else {
            if (this.options.debugMode) {
                console.log(`[Performance] Load time requirement met: ${totalLoadTime}ms ≤ ${this.options.maxLoadTime}ms`);
            }
        }
        
        // Check individual component load times
        if (this.metrics.cssLoadTime > 500) {
            this.warnings.push(`CSS load time is high: ${this.metrics.cssLoadTime}ms`);
        }
        
        if (this.metrics.jsLoadTime > 300) {
            this.warnings.push(`JavaScript load time is high: ${this.metrics.jsLoadTime}ms`);
        }
        
        if (this.metrics.imageLoadTime > 1000) {
            this.warnings.push(`Image load time is high: ${this.metrics.imageLoadTime}ms`);
        }
    }
    
    handleVisibilityChange() {
        if (document.hidden) {
            this.pauseTracking();
        } else {
            this.resumeTracking();
        }
    }
    
    pauseTracking() {
        this.trackingPaused = true;
    }
    
    resumeTracking() {
        this.trackingPaused = false;
        this.trackResponsivePerformance();
    }
    
    generateReport() {
        return {
            timestamp: new Date().toISOString(),
            userAgent: navigator.userAgent,
            viewport: {
                width: window.innerWidth,
                height: window.innerHeight
            },
            metrics: this.metrics,
            warnings: this.warnings,
            passed: this.warnings.length === 0,
            recommendations: this.generateRecommendations()
        };
    }
    
    generateRecommendations() {
        const recommendations = [];
        
        if (this.metrics.cssLoadTime > 300) {
            recommendations.push('Consider optimizing CSS delivery or reducing CSS bundle size');
        }
        
        if (this.metrics.jsLoadTime > 200) {
            recommendations.push('Consider code splitting or lazy loading for JavaScript');
        }
        
        if (this.metrics.imageLoadTime > 800) {
            recommendations.push('Consider image optimization or lazy loading');
        }
        
        if (this.metrics.firstContentfulPaint > 1500) {
            recommendations.push('Consider optimizing critical rendering path');
        }
        
        if (this.metrics.cumulativeLayoutShift > 0.1) {
            recommendations.push('Consider fixing layout shift issues');
        }
        
        if (this.metrics.animationPerformance.averageDuration > 800) {
            recommendations.push('Consider optimizing animation performance');
        }
        
        return recommendations;
    }
    
    displayPerformanceReport() {
        const report = this.generateReport();
        
        console.group('[Performance Monitor] Report');
        console.log('Overall Status:', report.passed ? '✅ PASSED' : '❌ FAILED');
        console.log('Load Time Check:', (this.metrics.pageLoadTime + this.metrics.cssLoadTime + this.metrics.jsLoadTime) <= this.options.maxLoadTime ? '✅ PASSED' : '❌ FAILED');
        console.table({
            'Page Load Time (ms)': this.metrics.pageLoadTime,
            'CSS Load Time (ms)': this.metrics.cssLoadTime,
            'JS Load Time (ms)': this.metrics.jsLoadTime,
            'Image Load Time (ms)': this.metrics.imageLoadTime,
            'First Contentful Paint (ms)': this.metrics.firstContentfulPaint,
            'Largest Contentful Paint (ms)': this.metrics.largestContentfulPaint
        });
        
        if (this.warnings.length > 0) {
            console.warn('Performance Warnings:');
            this.warnings.forEach(warning => console.warn('⚠️', warning));
        }
        
        if (report.recommendations.length > 0) {
            console.info('Performance Recommendations:');
            report.recommendations.forEach(rec => console.info('💡', rec));
        }
        
        console.groupEnd();
        
        // Display in-page notification for development
        this.displayInPageReport(report);
    }
    
    displayInPageReport(report) {
        // Only show in development mode
        if (!this.options.debugMode) return;
        
        const reportElement = document.createElement('div');
        reportElement.id = 'performance-report';
        reportElement.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: rgba(255, 255, 255, 0.95);
            backdrop-filter: blur(10px);
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 16px;
            max-width: 400px;
            z-index: 9999;
            font-size: 12px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
        `;
        
        const status = report.passed ? 
            '<div style="color: #4CAF50;">✅ Performance: PASSED</div>' :
            '<div style="color: #f44336;">❌ Performance: FAILED</div>';
        
        const loadTime = this.metrics.pageLoadTime + this.metrics.cssLoadTime + this.metrics.jsLoadTime;
        const loadTimeStatus = loadTime <= this.options.maxLoadTime ?
            `<div style="color: #4CAF50;">✅ Load Time: ${loadTime}ms ≤ 2000ms</div>` :
            `<div style="color: #f44336;">❌ Load Time: ${loadTime}ms > 2000ms</div>`;
        
        reportElement.innerHTML = `
            <div style="font-weight: bold; margin-bottom: 8px;">BizConnect Performance Report</div>
            ${status}
            ${loadTimeStatus}
            <div style="margin-top: 8px;">
                <div>CSS: ${Math.round(this.metrics.cssLoadTime)}ms</div>
                <div>JS: ${Math.round(this.metrics.jsLoadTime)}ms</div>
                <div>FCP: ${Math.round(this.metrics.firstContentfulPaint)}ms</div>
            </div>
            <button onclick="this.parentElement.remove()" style="position: absolute; top: 4px; right: 8px; background: none; border: none; cursor: pointer;">✖</button>
        `;
        
        document.body.appendChild(reportElement);
        
        // Auto-hide after 10 seconds
        setTimeout(() => {
            if (document.getElementById('performance-report')) {
                reportElement.remove();
            }
        }, 10000);
    }
    
    sendReport() {
        if (!this.options.reportingEndpoint) return;
        
        const report = this.generateReport();
        
        fetch(this.options.reportingEndpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify(report)
        }).catch(error => {
            if (this.options.debugMode) {
                console.warn('[Performance] Failed to send report:', error);
            }
        });
    }
}

// Utility functions
function debounce(func, wait) {
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

// Auto-initialize performance monitoring
document.addEventListener('DOMContentLoaded', function() {
    // Check if we're in development mode
    const isDebugMode = window.location.hostname === 'localhost' || 
                       window.location.hostname === '127.0.0.1' ||
                       window.location.search.includes('debug=performance');
    
    // Initialize performance monitor
    window.performanceMonitor = new PerformanceMonitor({
        debugMode: isDebugMode,
        trackingEnabled: true
    });
    
    // Expose global function to manually trigger report
    window.showPerformanceReport = function() {
        window.performanceMonitor.displayPerformanceReport();
    };
});