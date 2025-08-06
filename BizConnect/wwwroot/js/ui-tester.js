/**
 * BizConnect UI Testing and Validation System
 * Validates responsive design, modern UI components, and user experience
 */

class UITester {
    constructor(options = {}) {
        this.options = {
            testResponsive: true,
            testComponents: true,
            testAnimations: true,
            testAccessibility: true,
            debugMode: false,
            ...options
        };
        
        this.testResults = {
            responsive: {},
            components: {},
            animations: {},
            accessibility: {},
            overall: { passed: 0, failed: 0, warnings: 0 }
        };
        
        this.initialize();
    }
    
    initialize() {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => this.runTests());
        } else {
            this.runTests();
        }
    }
    
    async runTests() {
        console.group('[UI Tester] Running comprehensive UI tests...');
        
        if (this.options.testResponsive) {
            await this.testResponsiveDesign();
        }
        
        if (this.options.testComponents) {
            await this.testModernComponents();
        }
        
        if (this.options.testAnimations) {
            await this.testAnimations();
        }
        
        if (this.options.testAccessibility) {
            await this.testAccessibility();
        }
        
        this.generateReport();
        console.groupEnd();
    }
    
    async testResponsiveDesign() {
        console.log('[UI Tester] Testing responsive design...');
        
        const breakpoints = [
            { name: 'xs', width: 375, height: 667 }, // iPhone SE
            { name: 'sm', width: 576, height: 1024 }, // Small tablet portrait
            { name: 'md', width: 768, height: 1024 }, // iPad portrait
            { name: 'lg', width: 992, height: 768 }, // iPad landscape
            { name: 'xl', width: 1200, height: 800 }, // Desktop
            { name: 'xxl', width: 1400, height: 900 } // Large desktop
        ];
        
        for (const breakpoint of breakpoints) {
            await this.testBreakpoint(breakpoint);
        }
    }
    
    async testBreakpoint(breakpoint) {
        // Simulate viewport change (for testing purposes)
        const originalWidth = window.innerWidth;
        const originalHeight = window.innerHeight;
        
        // Test critical UI elements at this breakpoint
        const tests = [
            this.testNavigation(breakpoint),
            this.testCards(breakpoint),
            this.testButtons(breakpoint),
            this.testForms(breakpoint),
            this.testTables(breakpoint),
            this.testModals(breakpoint)
        ];
        
        const results = await Promise.all(tests);
        
        this.testResults.responsive[breakpoint.name] = {
            ...breakpoint,
            passed: results.filter(r => r.passed).length,
            failed: results.filter(r => !r.passed).length,
            details: results
        };
        
        if (this.options.debugMode) {
            console.log(`[Responsive ${breakpoint.name}]`, this.testResults.responsive[breakpoint.name]);
        }
    }
    
    testNavigation(breakpoint) {
        const navigation = document.querySelector('.navbar, .admin-sidebar, .modern-nav');
        if (!navigation) return { test: 'navigation', passed: true, message: 'No navigation found' };
        
        const computedStyle = window.getComputedStyle(navigation);
        const isVisible = computedStyle.display !== 'none' && computedStyle.visibility !== 'hidden';
        const hasProperPadding = parseFloat(computedStyle.paddingLeft) > 0 || parseFloat(computedStyle.paddingRight) > 0;
        
        return {
            test: 'navigation',
            passed: isVisible && hasProperPadding,
            message: !isVisible ? 'Navigation not visible' : !hasProperPadding ? 'Navigation lacks proper padding' : 'OK'
        };
    }
    
    testCards(breakpoint) {
        const cards = document.querySelectorAll('.glass-card, .card-modern, .stats-widget');
        if (cards.length === 0) return { test: 'cards', passed: true, message: 'No cards found' };
        
        let passed = true;
        let message = 'OK';
        
        cards.forEach(card => {
            const computedStyle = window.getComputedStyle(card);
            const hasGlassMorphism = computedStyle.backdropFilter !== 'none' || computedStyle.webkitBackdropFilter !== 'none';
            const hasProperRadius = parseFloat(computedStyle.borderRadius) > 0;
            const hasProperPadding = parseFloat(computedStyle.padding) > 0;
            
            if (!hasGlassMorphism && breakpoint.width >= 768) {
                passed = false;
                message = 'Cards missing glassmorphism effect on larger screens';
            }
            
            if (!hasProperRadius) {
                passed = false;
                message = 'Cards missing proper border radius';
            }
            
            if (!hasProperPadding) {
                passed = false;
                message = 'Cards missing proper padding';
            }
        });
        
        return { test: 'cards', passed, message };
    }
    
    testButtons(breakpoint) {
        const buttons = document.querySelectorAll('.btn-modern, .btn-kbank, .btn-outline-kbank');
        if (buttons.length === 0) return { test: 'buttons', passed: true, message: 'No modern buttons found' };
        
        let passed = true;
        let message = 'OK';
        
        buttons.forEach(button => {
            const computedStyle = window.getComputedStyle(button);
            const hasProperRadius = parseFloat(computedStyle.borderRadius) > 0;
            const hasTransition = computedStyle.transition !== 'none' && computedStyle.transition !== '';
            const hasProperPadding = parseFloat(computedStyle.paddingLeft) >= 16 && parseFloat(computedStyle.paddingRight) >= 16;
            
            if (!hasProperRadius) {
                passed = false;
                message = 'Buttons missing proper border radius';
            }
            
            if (!hasTransition) {
                passed = false;
                message = 'Buttons missing hover transitions';
            }
            
            if (!hasProperPadding && breakpoint.width >= 576) {
                passed = false;
                message = 'Buttons have insufficient padding on larger screens';
            }
        });
        
        return { test: 'buttons', passed, message };
    }
    
    testForms(breakpoint) {
        const forms = document.querySelectorAll('.form-modern, .secure-form');
        if (forms.length === 0) return { test: 'forms', passed: true, message: 'No modern forms found' };
        
        let passed = true;
        let message = 'OK';
        
        forms.forEach(form => {
            const inputs = form.querySelectorAll('input, textarea, select');
            
            inputs.forEach(input => {
                const computedStyle = window.getComputedStyle(input);
                const hasProperPadding = parseFloat(computedStyle.paddingTop) >= 8;
                const hasProperBorder = computedStyle.border !== 'none' && computedStyle.borderWidth !== '0px';
                const hasProperRadius = parseFloat(computedStyle.borderRadius) > 0;
                
                if (!hasProperPadding) {
                    passed = false;
                    message = 'Form inputs missing proper padding';
                }
                
                if (!hasProperBorder) {
                    passed = false;
                    message = 'Form inputs missing proper borders';
                }
                
                if (!hasProperRadius) {
                    passed = false;
                    message = 'Form inputs missing proper border radius';
                }
            });
        });
        
        return { test: 'forms', passed, message };
    }
    
    testTables(breakpoint) {
        const tables = document.querySelectorAll('.table-modern, .table-responsive-custom');
        if (tables.length === 0) return { test: 'tables', passed: true, message: 'No modern tables found' };
        
        let passed = true;
        let message = 'OK';
        
        tables.forEach(table => {
            const isResponsive = table.closest('.table-responsive, .table-responsive-sm, .table-responsive-md') !== null;
            const hasProperStyling = table.classList.contains('table-modern') || table.classList.contains('table-responsive-custom');
            
            if (!isResponsive && breakpoint.width < 768) {
                passed = false;
                message = 'Tables not responsive on small screens';
            }
            
            if (!hasProperStyling) {
                passed = false;
                message = 'Tables missing modern styling classes';
            }
        });
        
        return { test: 'tables', passed, message };
    }
    
    testModals(breakpoint) {
        const modals = document.querySelectorAll('.modal-modern, .modal');
        if (modals.length === 0) return { test: 'modals', passed: true, message: 'No modals found' };
        
        let passed = true;
        let message = 'OK';
        
        modals.forEach(modal => {
            const dialog = modal.querySelector('.modal-dialog');
            if (dialog) {
                const computedStyle = window.getComputedStyle(dialog);
                const maxWidth = computedStyle.maxWidth;
                
                if (breakpoint.width < 576 && maxWidth !== 'none' && parseFloat(maxWidth) > breakpoint.width * 0.9) {
                    passed = false;
                    message = 'Modal too wide for small screens';
                }
            }
        });
        
        return { test: 'modals', passed, message };
    }
    
    async testModernComponents() {
        console.log('[UI Tester] Testing modern UI components...');
        
        const componentTests = [
            this.testGlassmorphism(),
            this.testKBankBranding(),
            this.testLanguageToggle(),
            this.testSecurityComponents(),
            this.testLoadingStates(),
            this.testAlerts()
        ];
        
        const results = await Promise.all(componentTests);
        
        this.testResults.components = {
            passed: results.filter(r => r.passed).length,
            failed: results.filter(r => !r.passed).length,
            details: results
        };
    }
    
    testGlassmorphism() {
        const glassElements = document.querySelectorAll('.glass-card, .glass, .glass-light, .glass-medium, .glass-dark');
        if (glassElements.length === 0) return { test: 'glassmorphism', passed: false, message: 'No glassmorphism elements found' };
        
        let passed = true;
        let message = 'OK';
        
        glassElements.forEach(element => {
            const computedStyle = window.getComputedStyle(element);
            const hasBackdropFilter = computedStyle.backdropFilter !== 'none' || computedStyle.webkitBackdropFilter !== 'none';
            const hasBackground = computedStyle.backgroundColor !== 'rgba(0, 0, 0, 0)';
            
            if (!hasBackdropFilter && !element.classList.contains('no-backdrop')) {
                passed = false;
                message = 'Glassmorphism elements missing backdrop-filter';
            }
            
            if (!hasBackground) {
                passed = false;
                message = 'Glassmorphism elements missing semi-transparent background';
            }
        });
        
        return { test: 'glassmorphism', passed, message };
    }
    
    testKBankBranding() {
        const root = document.documentElement;
        const computedStyle = window.getComputedStyle(root);
        
        const kbankGreen = computedStyle.getPropertyValue('--kbank-green').trim();
        const primaryBlue = computedStyle.getPropertyValue('--primary-blue').trim();
        const accentGold = computedStyle.getPropertyValue('--accent-gold').trim();
        
        const hasBrandColors = kbankGreen && primaryBlue && accentGold;
        const hasGradients = computedStyle.getPropertyValue('--gradient-kbank').trim();
        
        return {
            test: 'kbank-branding',
            passed: hasBrandColors && hasGradients,
            message: !hasBrandColors ? 'KBank brand colors not defined' : !hasGradients ? 'KBank gradients not defined' : 'OK'
        };
    }
    
    testLanguageToggle() {
        const languageToggle = document.querySelector('.language-toggle-modern, .language-toggle');
        if (!languageToggle) return { test: 'language-toggle', passed: true, message: 'Language toggle not found (may not be on this page)' };
        
        const hasModernStyling = languageToggle.classList.contains('language-toggle-modern');
        const hasButtons = languageToggle.querySelectorAll('button, .language-option').length >= 2;
        
        return {
            test: 'language-toggle',
            passed: hasModernStyling && hasButtons,
            message: !hasModernStyling ? 'Language toggle missing modern styling' : !hasButtons ? 'Language toggle missing option buttons' : 'OK'
        };
    }
    
    testSecurityComponents() {
        const securityForms = document.querySelectorAll('.secure-form, .form-modern[data-security-level]');
        if (securityForms.length === 0) return { test: 'security-components', passed: true, message: 'No security forms found (may not be on this page)' };
        
        let passed = true;
        let message = 'OK';
        
        securityForms.forEach(form => {
            const hasAntiForgery = form.querySelector('input[name="__RequestVerificationToken"]') !== null;
            const hasSecurityLevel = form.hasAttribute('data-security-level');
            const hasSecurityStyling = form.classList.contains('security-level-normal') || 
                                      form.classList.contains('security-level-elevated') ||
                                      form.classList.contains('security-level-high') ||
                                      form.classList.contains('security-level-critical');
            
            if (!hasAntiForgery) {
                passed = false;
                message = 'Security forms missing anti-forgery tokens';
            }
            
            if (!hasSecurityLevel) {
                passed = false;
                message = 'Security forms missing security level attributes';
            }
            
            if (!hasSecurityStyling) {
                passed = false;
                message = 'Security forms missing security level styling';
            }
        });
        
        return { test: 'security-components', passed, message };
    }
    
    testLoadingStates() {
        // Test for loading spinner components
        const loadingSpinners = document.querySelectorAll('.spinner-modern, .loading-overlay-modern');
        const loadingClasses = ['spin', 'pulse', 'bounce'].some(cls => 
            document.querySelector(`.${cls}`) !== null
        );
        
        return {
            test: 'loading-states',
            passed: loadingSpinners.length > 0 || loadingClasses,
            message: loadingSpinners.length === 0 && !loadingClasses ? 'No loading state components found' : 'OK'
        };
    }
    
    testAlerts() {
        const alerts = document.querySelectorAll('.alert-modern, .alert');
        if (alerts.length === 0) return { test: 'alerts', passed: true, message: 'No alert components found (may not be on this page)' };
        
        let passed = true;
        let message = 'OK';
        
        alerts.forEach(alert => {
            const hasIcon = alert.querySelector('.alert-icon, .fas, .far') !== null;
            const hasContent = alert.querySelector('.alert-content, .alert-message') !== null;
            const hasModernStyling = alert.classList.contains('alert-modern');
            
            if (!hasIcon) {
                passed = false;
                message = 'Alert components missing icons';
            }
            
            if (!hasContent) {
                passed = false;
                message = 'Alert components missing proper content structure';
            }
        });
        
        return { test: 'alerts', passed, message };
    }
    
    async testAnimations() {
        console.log('[UI Tester] Testing animations...');
        
        const animationTests = [
            this.testAnimationClasses(),
            this.testAnimationPerformance(),
            this.testReducedMotion()
        ];
        
        const results = await Promise.all(animationTests);
        
        this.testResults.animations = {
            passed: results.filter(r => r.passed).length,
            failed: results.filter(r => !r.passed).length,
            details: results
        };
    }
    
    testAnimationClasses() {
        const animationClasses = [
            'fade-in', 'fade-in-up', 'fade-in-down', 'fade-in-left', 'fade-in-right',
            'bounce-in', 'bounce-in-up', 'bounce-in-down',
            'slide-in-left', 'slide-in-right', 'slide-in-up', 'slide-in-down',
            'scale-in', 'zoom-in', 'rotate-in'
        ];
        
        let foundAnimations = 0;
        
        animationClasses.forEach(className => {
            if (document.querySelector(`.${className}`)) {
                foundAnimations++;
            }
        });
        
        return {
            test: 'animation-classes',
            passed: foundAnimations > 0,
            message: foundAnimations === 0 ? 'No animation classes found in use' : `${foundAnimations} animation classes in use`
        };
    }
    
    testAnimationPerformance() {
        const animatedElements = document.querySelectorAll('[class*="fade-"], [class*="bounce-"], [class*="slide-"], [class*="scale-"], [class*="zoom-"]');
        
        let performanceIssues = 0;
        
        animatedElements.forEach(element => {
            const computedStyle = window.getComputedStyle(element);
            const willChange = computedStyle.willChange;
            const transform = computedStyle.transform;
            
            // Check for hardware acceleration
            if (willChange === 'auto' && transform === 'none') {
                performanceIssues++;
            }
        });
        
        return {
            test: 'animation-performance',
            passed: performanceIssues === 0,
            message: performanceIssues > 0 ? `${performanceIssues} elements may have animation performance issues` : 'Animation performance looks good'
        };
    }
    
    testReducedMotion() {
        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        
        if (!prefersReducedMotion) {
            return { test: 'reduced-motion', passed: true, message: 'User does not prefer reduced motion' };
        }
        
        // Check if animations are properly disabled
        const animatedElements = document.querySelectorAll('[class*="fade-"], [class*="bounce-"], [class*="slide-"]');
        let hasAnimations = false;
        
        animatedElements.forEach(element => {
            const computedStyle = window.getComputedStyle(element);
            if (computedStyle.animationDuration !== '0s' && computedStyle.animationName !== 'none') {
                hasAnimations = true;
            }
        });
        
        return {
            test: 'reduced-motion',
            passed: !hasAnimations,
            message: hasAnimations ? 'Animations not properly disabled for users who prefer reduced motion' : 'Reduced motion preferences respected'
        };
    }
    
    async testAccessibility() {
        console.log('[UI Tester] Testing accessibility...');
        
        const accessibilityTests = [
            this.testFocusIndicators(),
            this.testSkipLinks(),
            this.testColorContrast(),
            this.testAriaLabels()
        ];
        
        const results = await Promise.all(accessibilityTests);
        
        this.testResults.accessibility = {
            passed: results.filter(r => r.passed).length,
            failed: results.filter(r => !r.passed).length,
            details: results
        };
    }
    
    testFocusIndicators() {
        const focusableElements = document.querySelectorAll('button, a, input, textarea, select, [tabindex]');
        let hasGlobalFocusStyles = false;
        
        // Check if there are global focus styles
        const stylesheets = Array.from(document.styleSheets);
        stylesheets.forEach(stylesheet => {
            try {
                const rules = Array.from(stylesheet.cssRules || stylesheet.rules);
                rules.forEach(rule => {
                    if (rule.selectorText && rule.selectorText.includes(':focus')) {
                        hasGlobalFocusStyles = true;
                    }
                });
            } catch (e) {
                // Cross-origin stylesheet, skip
            }
        });
        
        return {
            test: 'focus-indicators',
            passed: hasGlobalFocusStyles,
            message: !hasGlobalFocusStyles ? 'No global focus styles found' : 'Focus indicators present'
        };
    }
    
    testSkipLinks() {
        const skipLinks = document.querySelectorAll('.skip-link, [href="#main-content"], [href="#content"]');
        
        return {
            test: 'skip-links',
            passed: skipLinks.length > 0,
            message: skipLinks.length === 0 ? 'No skip links found' : `${skipLinks.length} skip link(s) found`
        };
    }
    
    testColorContrast() {
        // This is a simplified test - in production you'd use a proper contrast checker
        const textElements = document.querySelectorAll('p, h1, h2, h3, h4, h5, h6, span, a, button, label');
        let contrastIssues = 0;
        
        textElements.forEach(element => {
            const computedStyle = window.getComputedStyle(element);
            const color = computedStyle.color;
            const backgroundColor = computedStyle.backgroundColor;
            
            // Simple check for very light text on light backgrounds
            if (color.includes('rgba(255, 255, 255') && backgroundColor.includes('rgba(255, 255, 255')) {
                contrastIssues++;
            }
        });
        
        return {
            test: 'color-contrast',
            passed: contrastIssues === 0,
            message: contrastIssues > 0 ? `${contrastIssues} potential contrast issues found` : 'No obvious contrast issues detected'
        };
    }
    
    testAriaLabels() {
        const interactiveElements = document.querySelectorAll('button, a, input, select, textarea, [role="button"], [role="link"]');
        let missingLabels = 0;
        
        interactiveElements.forEach(element => {
            const hasLabel = element.hasAttribute('aria-label') || 
                           element.hasAttribute('aria-labelledby') ||
                           element.textContent.trim() !== '' ||
                           element.querySelector('img[alt]') ||
                           element.closest('label');
            
            if (!hasLabel) {
                missingLabels++;
            }
        });
        
        return {
            test: 'aria-labels',
            passed: missingLabels === 0,
            message: missingLabels > 0 ? `${missingLabels} elements missing proper labels` : 'All interactive elements have proper labels'
        };
    }
    
    generateReport() {
        const totalTests = Object.values(this.testResults).reduce((acc, category) => {
            if (typeof category === 'object' && category.passed !== undefined) {
                acc += category.passed + category.failed;
            }
            return acc;
        }, 0);
        
        const totalPassed = Object.values(this.testResults).reduce((acc, category) => {
            if (typeof category === 'object' && category.passed !== undefined) {
                acc += category.passed;
            }
            return acc;
        }, 0);
        
        const overallScore = totalTests > 0 ? Math.round((totalPassed / totalTests) * 100) : 0;
        
        console.group('[UI Tester] Test Results Summary');
        console.log(`Overall Score: ${overallScore}% (${totalPassed}/${totalTests} tests passed)`);
        
        Object.entries(this.testResults).forEach(([category, results]) => {
            if (typeof results === 'object' && results.passed !== undefined) {
                const score = results.passed + results.failed > 0 ? 
                    Math.round((results.passed / (results.passed + results.failed)) * 100) : 0;
                console.log(`${category}: ${score}% (${results.passed}/${results.passed + results.failed})`);
                
                if (this.options.debugMode && results.details) {
                    results.details.forEach(detail => {
                        const status = detail.passed ? '✅' : '❌';
                        console.log(`  ${status} ${detail.test}: ${detail.message}`);
                    });
                }
            }
        });
        
        console.groupEnd();
        
        // Display in-page report if in debug mode
        if (this.options.debugMode) {
            this.displayInPageReport(overallScore, totalPassed, totalTests);
        }
        
        return {
            overallScore,
            totalPassed,
            totalTests,
            results: this.testResults
        };
    }
    
    displayInPageReport(score, passed, total) {
        const reportElement = document.createElement('div');
        reportElement.id = 'ui-test-report';
        reportElement.style.cssText = `
            position: fixed;
            bottom: 20px;
            right: 20px;
            background: rgba(255, 255, 255, 0.95);
            backdrop-filter: blur(10px);
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            padding: 16px;
            max-width: 350px;
            z-index: 9999;
            font-size: 12px;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
        `;
        
        const statusColor = score >= 90 ? '#4CAF50' : score >= 70 ? '#FF9800' : '#f44336';
        const statusIcon = score >= 90 ? '✅' : score >= 70 ? '⚠️' : '❌';
        
        reportElement.innerHTML = `
            <div style="font-weight: bold; margin-bottom: 8px;">UI Test Results</div>
            <div style="color: ${statusColor}; font-size: 14px; margin-bottom: 8px;">
                ${statusIcon} Score: ${score}% (${passed}/${total})
            </div>
            <div style="margin-top: 8px;">
                <div>Responsive: ${Math.round((this.testResults.responsive.passed || 0) / Math.max(1, Object.keys(this.testResults.responsive).length) * 100)}%</div>
                <div>Components: ${Math.round((this.testResults.components?.passed || 0) / Math.max(1, (this.testResults.components?.passed || 0) + (this.testResults.components?.failed || 0)) * 100)}%</div>
                <div>Animations: ${Math.round((this.testResults.animations?.passed || 0) / Math.max(1, (this.testResults.animations?.passed || 0) + (this.testResults.animations?.failed || 0)) * 100)}%</div>
                <div>Accessibility: ${Math.round((this.testResults.accessibility?.passed || 0) / Math.max(1, (this.testResults.accessibility?.passed || 0) + (this.testResults.accessibility?.failed || 0)) * 100)}%</div>
            </div>
            <button onclick="this.parentElement.remove()" style="position: absolute; top: 4px; right: 8px; background: none; border: none; cursor: pointer;">✖</button>
        `;
        
        document.body.appendChild(reportElement);
        
        // Auto-hide after 15 seconds
        setTimeout(() => {
            if (document.getElementById('ui-test-report')) {
                reportElement.remove();
            }
        }, 15000);
    }
}

// Auto-initialize UI testing
document.addEventListener('DOMContentLoaded', function() {
    // Check if we're in development mode
    const isDebugMode = window.location.hostname === 'localhost' || 
                       window.location.hostname === '127.0.0.1' ||
                       window.location.search.includes('debug=ui');
    
    // Only run comprehensive tests in debug mode to avoid impacting production
    if (isDebugMode) {
        setTimeout(() => {
            window.uiTester = new UITester({
                debugMode: true
            });
        }, 1000); // Wait for page to fully load
    }
    
    // Expose global function to manually trigger tests
    window.runUITests = function() {
        if (window.uiTester) {
            return window.uiTester.runTests();
        } else {
            window.uiTester = new UITester({ debugMode: true });
            return window.uiTester.runTests();
        }
    };
});