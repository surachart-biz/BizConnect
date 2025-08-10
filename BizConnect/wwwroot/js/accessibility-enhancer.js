/**
 * Accessibility Enhancement System for BizConnect
 * Provides comprehensive accessibility support including reduced motion,
 * keyboard navigation, screen reader support, and focus management
 */

class AccessibilityEnhancer {
    constructor() {
        this.prefersReducedMotion = this.detectReducedMotionPreference();
        this.isHighContrast = this.detectHighContrastPreference();
        this.isScreenReaderActive = this.detectScreenReader();
        this.focusableElements = [];
        this.trapStack = [];
        this.announcements = [];
        this.keyboardShortcuts = new Map();
        
        this.init();
    }

    init() {
        this.setupMotionPreferences();
        this.setupKeyboardNavigation();
        this.setupFocusManagement();
        this.setupScreenReaderSupport();
        this.setupHighContrastSupport();
        this.setupKeyboardShortcuts();
        this.setupARIAEnhancements();
        this.monitorAccessibilityChanges();
        
        console.log('♿ Accessibility enhancements initialized');
    }

    detectReducedMotionPreference() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    detectHighContrastPreference() {
        return window.matchMedia('(prefers-contrast: high)').matches ||
               window.matchMedia('(forced-colors: active)').matches;
    }

    detectScreenReader() {
        // Multiple heuristics to detect screen reader usage
        const hasScreenReader = !!(
            navigator.userAgent.match(/JAWS|NVDA|SARA|Dragon|ZoomText|MAGic|Supernova|Cobra/i) ||
            window.speechSynthesis ||
            navigator.userAgent.match(/VoiceOver/i) ||
            document.getElementById('nvda-announcer')
        );
        
        return hasScreenReader;
    }

    setupMotionPreferences() {
        if (this.prefersReducedMotion) {
            this.disableMotionAnimations();
        }

        // Listen for preference changes
        const motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
        motionQuery.addEventListener('change', (e) => {
            this.prefersReducedMotion = e.matches;
            if (e.matches) {
                this.disableMotionAnimations();
            } else {
                this.enableMotionAnimations();
            }
        });
    }

    disableMotionAnimations() {
        document.body.classList.add('reduced-motion');
        
        const style = document.createElement('style');
        style.id = 'reduced-motion-styles';
        style.innerHTML = `
            .reduced-motion *,
            .reduced-motion *::before,
            .reduced-motion *::after {
                animation-duration: 0.01ms !important;
                animation-iteration-count: 1 !important;
                transition-duration: 0.01ms !important;
                scroll-behavior: auto !important;
            }
            
            .reduced-motion .fade-in,
            .reduced-motion .fade-in-up,
            .reduced-motion .slide-in-right,
            .reduced-motion .scale-in,
            .reduced-motion .bounce,
            .reduced-motion .shake,
            .reduced-motion .pulse {
                animation: none !important;
                opacity: 1 !important;
                transform: none !important;
            }
            
            .reduced-motion .loading-dots,
            .reduced-motion .wave-loader,
            .reduced-motion .skeleton-shimmer,
            .reduced-motion .kbank-spinner {
                animation: none !important;
            }
            
            .reduced-motion .parallax-element {
                transform: none !important;
            }
        `;
        document.head.appendChild(style);
        
        console.log('🚫 Motion animations disabled for accessibility');
    }

    enableMotionAnimations() {
        document.body.classList.remove('reduced-motion');
        const existingStyle = document.getElementById('reduced-motion-styles');
        if (existingStyle) {
            existingStyle.remove();
        }
        
        console.log('✅ Motion animations enabled');
    }

    setupKeyboardNavigation() {
        this.updateFocusableElements();
        this.setupFocusVisibility();
        this.setupTabNavigation();
        this.setupArrowKeyNavigation();
        this.setupEscapeKeyHandling();
        
        // Update focusable elements when DOM changes
        const observer = new MutationObserver(() => {
            this.updateFocusableElements();
        });
        
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['tabindex', 'disabled', 'aria-hidden']
        });
    }

    updateFocusableElements() {
        const selectors = [
            'a[href]',
            'button:not([disabled])',
            'input:not([disabled])',
            'textarea:not([disabled])',
            'select:not([disabled])',
            '[tabindex]:not([tabindex="-1"])',
            '[role="button"]:not([disabled])',
            '[role="link"]:not([disabled])',
            '[role="menuitem"]:not([disabled])',
            '[role="tab"]:not([disabled])'
        ].join(',');
        
        this.focusableElements = Array.from(document.querySelectorAll(selectors))
            .filter(el => this.isElementVisible(el) && !this.isElementDisabled(el));
    }

    isElementVisible(element) {
        const style = getComputedStyle(element);
        return style.display !== 'none' && 
               style.visibility !== 'hidden' && 
               style.opacity !== '0' &&
               element.offsetWidth > 0 && 
               element.offsetHeight > 0;
    }

    isElementDisabled(element) {
        return element.disabled || 
               element.getAttribute('aria-disabled') === 'true' ||
               element.getAttribute('aria-hidden') === 'true';
    }

    setupFocusVisibility() {
        let hadKeyboardEvent = false;
        let isMouseDown = false;
        
        const handleKeyDown = (e) => {
            if (e.key === 'Tab' || e.key === 'Enter' || e.key === ' ') {
                hadKeyboardEvent = true;
            }
        };
        
        const handleMouseDown = () => {
            isMouseDown = true;
            hadKeyboardEvent = false;
        };
        
        const handleMouseUp = () => {
            isMouseDown = false;
        };
        
        const handleFocus = (e) => {
            if (hadKeyboardEvent || !isMouseDown) {
                e.target.classList.add('keyboard-focused');
                this.announceToScreenReader(`${this.getElementDescription(e.target)} focused`);
            }
        };
        
        const handleBlur = (e) => {
            e.target.classList.remove('keyboard-focused');
        };
        
        document.addEventListener('keydown', handleKeyDown);
        document.addEventListener('mousedown', handleMouseDown);
        document.addEventListener('mouseup', handleMouseUp);
        document.addEventListener('focus', handleFocus, true);
        document.addEventListener('blur', handleBlur, true);
        
        // Add focus visibility styles
        this.addFocusStyles();
    }

    addFocusStyles() {
        const style = document.createElement('style');
        style.id = 'focus-visibility-styles';
        style.innerHTML = `
            .keyboard-focused {
                outline: 3px solid var(--kbank-primary) !important;
                outline-offset: 2px !important;
                border-radius: 4px !important;
            }
            
            .keyboard-focused.btn {
                box-shadow: 0 0 0 3px var(--kbank-primary) !important;
                outline: none !important;
            }
            
            .keyboard-focused.card,
            .keyboard-focused.kpi-card {
                box-shadow: 0 0 0 3px var(--kbank-primary), 0 8px 32px rgba(0, 0, 0, 0.1) !important;
            }
            
            /* High contrast focus indicators */
            @media (prefers-contrast: high) {
                .keyboard-focused {
                    outline: 4px solid ButtonText !important;
                    outline-offset: 2px !important;
                }
            }
            
            @media (forced-colors: active) {
                .keyboard-focused {
                    outline: 3px solid Highlight !important;
                    outline-offset: 2px !important;
                }
            }
        `;
        document.head.appendChild(style);
    }

    setupTabNavigation() {
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Tab') {
                this.handleTabNavigation(e);
            }
        });
    }

    handleTabNavigation(e) {
        const activeElement = document.activeElement;
        const currentIndex = this.focusableElements.indexOf(activeElement);
        
        if (e.shiftKey) {
            // Shift + Tab (backward navigation)
            if (currentIndex <= 0) {
                e.preventDefault();
                this.focusableElements[this.focusableElements.length - 1]?.focus();
            }
        } else {
            // Tab (forward navigation)
            if (currentIndex >= this.focusableElements.length - 1) {
                e.preventDefault();
                this.focusableElements[0]?.focus();
            }
        }
    }

    setupArrowKeyNavigation() {
        // Arrow key navigation for specific components
        document.addEventListener('keydown', (e) => {
            const target = e.target;
            
            // Navigation for card grids
            if (target.closest('.kpi-card, .quick-action-item')) {
                this.handleGridNavigation(e, target);
            }
            
            // Navigation for timeline items
            if (target.closest('.timeline-item')) {
                this.handleTimelineNavigation(e, target);
            }
            
            // Navigation for dropdown menus
            if (target.closest('.dropdown-menu')) {
                this.handleDropdownNavigation(e, target);
            }
        });
    }

    handleGridNavigation(e, target) {
        const container = target.closest('.row, .grid, .quick-actions-grid');
        if (!container) return;
        
        const items = Array.from(container.querySelectorAll('.kpi-card, .quick-action-item'));
        const currentIndex = items.indexOf(target.closest('.kpi-card, .quick-action-item'));
        
        let nextIndex = currentIndex;
        
        switch (e.key) {
            case 'ArrowRight':
                nextIndex = Math.min(currentIndex + 1, items.length - 1);
                break;
            case 'ArrowLeft':
                nextIndex = Math.max(currentIndex - 1, 0);
                break;
            case 'ArrowDown':
                nextIndex = Math.min(currentIndex + 2, items.length - 1);
                break;
            case 'ArrowUp':
                nextIndex = Math.max(currentIndex - 2, 0);
                break;
        }
        
        if (nextIndex !== currentIndex) {
            e.preventDefault();
            const nextElement = items[nextIndex]?.querySelector('a, button') || items[nextIndex];
            nextElement?.focus();
        }
    }

    handleTimelineNavigation(e, target) {
        const container = target.closest('.activity-timeline');
        if (!container) return;
        
        const items = Array.from(container.querySelectorAll('.timeline-item'));
        const currentIndex = items.indexOf(target.closest('.timeline-item'));
        
        let nextIndex = currentIndex;
        
        switch (e.key) {
            case 'ArrowDown':
                nextIndex = Math.min(currentIndex + 1, items.length - 1);
                break;
            case 'ArrowUp':
                nextIndex = Math.max(currentIndex - 1, 0);
                break;
        }
        
        if (nextIndex !== currentIndex) {
            e.preventDefault();
            const nextElement = items[nextIndex]?.querySelector('button, a') || items[nextIndex];
            nextElement?.focus();
        }
    }

    handleDropdownNavigation(e, target) {
        const dropdown = target.closest('.dropdown-menu');
        const items = Array.from(dropdown.querySelectorAll('.dropdown-item:not(.disabled)'));
        const currentIndex = items.indexOf(target);
        
        let nextIndex = currentIndex;
        
        switch (e.key) {
            case 'ArrowDown':
                nextIndex = (currentIndex + 1) % items.length;
                break;
            case 'ArrowUp':
                nextIndex = currentIndex <= 0 ? items.length - 1 : currentIndex - 1;
                break;
            case 'Home':
                nextIndex = 0;
                break;
            case 'End':
                nextIndex = items.length - 1;
                break;
        }
        
        if (nextIndex !== currentIndex) {
            e.preventDefault();
            items[nextIndex]?.focus();
        }
    }

    setupEscapeKeyHandling() {
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                this.handleEscapeKey(e);
            }
        });
    }

    handleEscapeKey(e) {
        // Close modals
        const openModal = document.querySelector('.modal.show');
        if (openModal) {
            const modalInstance = bootstrap.Modal.getInstance(openModal);
            modalInstance?.hide();
            return;
        }
        
        // Close dropdowns
        const openDropdown = document.querySelector('.dropdown-menu.show');
        if (openDropdown) {
            const dropdown = bootstrap.Dropdown.getInstance(
                openDropdown.previousElementSibling
            );
            dropdown?.hide();
            return;
        }
        
        // Release focus trap
        if (this.trapStack.length > 0) {
            this.releaseFocusTrap();
        }
    }

    setupFocusManagement() {
        this.setupFocusTrapping();
        this.setupInitialFocus();
    }

    setupFocusTrapping() {
        // Automatically trap focus in modals
        document.addEventListener('shown.bs.modal', (e) => {
            this.trapFocus(e.target);
        });
        
        document.addEventListener('hidden.bs.modal', (e) => {
            this.releaseFocusTrap();
        });
    }

    trapFocus(container) {
        const focusableElements = container.querySelectorAll(
            'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );
        
        if (focusableElements.length === 0) return;
        
        const firstElement = focusableElements[0];
        const lastElement = focusableElements[focusableElements.length - 1];
        
        const trapHandler = (e) => {
            if (e.key === 'Tab') {
                if (e.shiftKey) {
                    if (document.activeElement === firstElement) {
                        e.preventDefault();
                        lastElement.focus();
                    }
                } else {
                    if (document.activeElement === lastElement) {
                        e.preventDefault();
                        firstElement.focus();
                    }
                }
            }
        };
        
        container.addEventListener('keydown', trapHandler);
        this.trapStack.push({ container, handler: trapHandler, firstElement });
        
        // Focus first element
        setTimeout(() => firstElement.focus(), 100);
    }

    releaseFocusTrap() {
        const trap = this.trapStack.pop();
        if (trap) {
            trap.container.removeEventListener('keydown', trap.handler);
        }
    }

    setupInitialFocus() {
        // Set initial focus on page load
        window.addEventListener('load', () => {
            const skipLink = document.getElementById('skip-to-content');
            if (skipLink) {
                skipLink.focus();
            } else {
                const firstHeading = document.querySelector('h1, h2, h3');
                if (firstHeading) {
                    firstHeading.setAttribute('tabindex', '-1');
                    firstHeading.focus();
                }
            }
        });
    }

    setupScreenReaderSupport() {
        this.createLiveRegion();
        this.enhanceFormLabels();
        this.addScreenReaderOnlyContent();
        this.setupStatusAnnouncements();
    }

    createLiveRegion() {
        // Create ARIA live region for announcements
        if (!document.getElementById('aria-live-region')) {
            const liveRegion = document.createElement('div');
            liveRegion.id = 'aria-live-region';
            liveRegion.setAttribute('aria-live', 'polite');
            liveRegion.setAttribute('aria-atomic', 'true');
            liveRegion.style.cssText = `
                position: absolute;
                left: -10000px;
                width: 1px;
                height: 1px;
                overflow: hidden;
            `;
            document.body.appendChild(liveRegion);
        }
    }

    announceToScreenReader(message, priority = 'polite') {
        const liveRegion = document.getElementById('aria-live-region');
        if (!liveRegion) return;
        
        liveRegion.setAttribute('aria-live', priority);
        liveRegion.textContent = message;
        
        // Clear after announcement
        setTimeout(() => {
            liveRegion.textContent = '';
        }, 1000);
    }

    enhanceFormLabels() {
        // Enhance form accessibility
        const inputs = document.querySelectorAll('input, textarea, select');
        
        inputs.forEach(input => {
            // Add descriptions for screen readers
            if (input.placeholder && !input.getAttribute('aria-describedby')) {
                const descId = `desc-${input.id || Math.random().toString(36).substr(2, 9)}`;
                const description = document.createElement('span');
                description.id = descId;
                description.className = 'sr-only';
                description.textContent = input.placeholder;
                input.parentNode.appendChild(description);
                input.setAttribute('aria-describedby', descId);
            }
            
            // Add required field announcements
            if (input.required && !input.getAttribute('aria-required')) {
                input.setAttribute('aria-required', 'true');
            }
        });
    }

    addScreenReaderOnlyContent() {
        // Add CSS for screen reader only content
        const style = document.createElement('style');
        style.innerHTML = `
            .sr-only {
                position: absolute;
                width: 1px;
                height: 1px;
                padding: 0;
                margin: -1px;
                overflow: hidden;
                clip: rect(0, 0, 0, 0);
                white-space: nowrap;
                border: 0;
            }
            
            .sr-only-focusable:active,
            .sr-only-focusable:focus {
                position: static;
                width: auto;
                height: auto;
                padding: inherit;
                margin: inherit;
                overflow: visible;
                clip: auto;
                white-space: normal;
            }
        `;
        document.head.appendChild(style);
    }

    setupStatusAnnouncements() {
        // Announce status changes
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.type === 'childList') {
                    // Announce new content
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === Node.ELEMENT_NODE) {
                            this.announceNewContent(node);
                        }
                    });
                }
                
                if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
                    // Announce status changes
                    this.announceStatusChange(mutation.target);
                }
            });
        });
        
        observer.observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['class', 'aria-live', 'aria-label']
        });
    }

    announceNewContent(element) {
        // Announce important new content
        if (element.matches('.toast, .alert, .notification')) {
            const message = element.textContent.trim();
            if (message) {
                this.announceToScreenReader(message, 'assertive');
            }
        }
        
        if (element.matches('.timeline-item')) {
            this.announceToScreenReader('New activity added', 'polite');
        }
    }

    announceStatusChange(element) {
        // Announce status badge changes
        if (element.matches('.badge, .status-badge, .timeline-status-badge')) {
            const status = element.textContent.trim();
            if (status) {
                this.announceToScreenReader(`Status changed to ${status}`, 'polite');
            }
        }
    }

    setupHighContrastSupport() {
        if (this.isHighContrast) {
            this.applyHighContrastStyles();
        }
        
        // Listen for contrast preference changes
        const contrastQuery = window.matchMedia('(prefers-contrast: high)');
        contrastQuery.addEventListener('change', (e) => {
            this.isHighContrast = e.matches;
            if (e.matches) {
                this.applyHighContrastStyles();
            } else {
                this.removeHighContrastStyles();
            }
        });
    }

    applyHighContrastStyles() {
        document.body.classList.add('high-contrast');
        
        const style = document.createElement('style');
        style.id = 'high-contrast-styles';
        style.innerHTML = `
            .high-contrast {
                --shadow-sm: none;
                --shadow-md: none;
                --shadow-lg: none;
                --shadow-xl: none;
                --shadow-2xl: none;
            }
            
            .high-contrast .card,
            .high-contrast .kpi-card,
            .high-contrast .modal-content {
                border: 2px solid ButtonText !important;
                box-shadow: none !important;
            }
            
            .high-contrast .btn {
                border: 2px solid ButtonText !important;
            }
            
            .high-contrast .timeline-status {
                border: 3px solid ButtonText !important;
            }
            
            .high-contrast .progress-bar,
            .high-contrast .kpi-progress-bar {
                background: Highlight !important;
            }
        `;
        document.head.appendChild(style);
    }

    removeHighContrastStyles() {
        document.body.classList.remove('high-contrast');
        const style = document.getElementById('high-contrast-styles');
        if (style) {
            style.remove();
        }
    }

    setupKeyboardShortcuts() {
        // Define keyboard shortcuts
        this.keyboardShortcuts.set('alt+h', () => this.goToHome());
        this.keyboardShortcuts.set('alt+d', () => this.goToDashboard());
        this.keyboardShortcuts.set('alt+s', () => this.focusSearch());
        this.keyboardShortcuts.set('alt+m', () => this.toggleMainMenu());
        this.keyboardShortcuts.set('?', () => this.showKeyboardShortcuts());
        
        // Listen for keyboard shortcuts
        document.addEventListener('keydown', (e) => {
            const shortcut = this.getShortcutString(e);
            const action = this.keyboardShortcuts.get(shortcut);
            
            if (action && !this.isInputFocused()) {
                e.preventDefault();
                action();
            }
        });
        
        // Add skip link
        this.addSkipLink();
    }

    getShortcutString(e) {
        const parts = [];
        if (e.ctrlKey) parts.push('ctrl');
        if (e.altKey) parts.push('alt');
        if (e.shiftKey) parts.push('shift');
        if (e.metaKey) parts.push('meta');
        parts.push(e.key.toLowerCase());
        return parts.join('+');
    }

    isInputFocused() {
        const activeElement = document.activeElement;
        return activeElement && (
            activeElement.tagName === 'INPUT' ||
            activeElement.tagName === 'TEXTAREA' ||
            activeElement.tagName === 'SELECT' ||
            activeElement.isContentEditable
        );
    }

    addSkipLink() {
        if (document.getElementById('skip-to-content')) return;
        
        const skipLink = document.createElement('a');
        skipLink.id = 'skip-to-content';
        skipLink.href = '#main-content';
        skipLink.textContent = 'Skip to main content';
        skipLink.className = 'sr-only sr-only-focusable';
        skipLink.style.cssText = `
            position: absolute;
            top: -40px;
            left: 6px;
            background: var(--kbank-primary);
            color: white;
            padding: 8px 12px;
            text-decoration: none;
            border-radius: 4px;
            z-index: 10000;
            transition: top 0.2s;
        `;
        
        skipLink.addEventListener('focus', () => {
            skipLink.style.top = '6px';
        });
        
        skipLink.addEventListener('blur', () => {
            skipLink.style.top = '-40px';
        });
        
        document.body.insertBefore(skipLink, document.body.firstChild);
    }

    setupARIAEnhancements() {
        // Enhance existing elements with ARIA attributes
        this.enhanceButtons();
        this.enhanceCards();
        this.enhanceModals();
        this.enhanceNavigation();
    }

    enhanceButtons() {
        const buttons = document.querySelectorAll('button:not([aria-label]):not([aria-labelledby])');
        buttons.forEach(button => {
            if (!button.textContent.trim() && button.querySelector('i')) {
                const icon = button.querySelector('i');
                const iconClass = Array.from(icon.classList).find(cls => cls.startsWith('fa-'));
                if (iconClass) {
                    const label = this.getIconLabel(iconClass);
                    button.setAttribute('aria-label', label);
                }
            }
        });
    }

    enhanceCards() {
        const cards = document.querySelectorAll('.card, .kpi-card');
        cards.forEach(card => {
            if (!card.getAttribute('role')) {
                card.setAttribute('role', 'region');
            }
            
            const heading = card.querySelector('h1, h2, h3, h4, h5, h6');
            if (heading && !card.getAttribute('aria-labelledby')) {
                if (!heading.id) {
                    heading.id = `heading-${Math.random().toString(36).substr(2, 9)}`;
                }
                card.setAttribute('aria-labelledby', heading.id);
            }
        });
    }

    enhanceModals() {
        const modals = document.querySelectorAll('.modal');
        modals.forEach(modal => {
            if (!modal.getAttribute('role')) {
                modal.setAttribute('role', 'dialog');
            }
            
            if (!modal.getAttribute('aria-modal')) {
                modal.setAttribute('aria-modal', 'true');
            }
            
            const title = modal.querySelector('.modal-title');
            if (title && !modal.getAttribute('aria-labelledby')) {
                if (!title.id) {
                    title.id = `modal-title-${Math.random().toString(36).substr(2, 9)}`;
                }
                modal.setAttribute('aria-labelledby', title.id);
            }
        });
    }

    enhanceNavigation() {
        const navs = document.querySelectorAll('nav:not([aria-label]):not([aria-labelledby])');
        navs.forEach(nav => {
            nav.setAttribute('aria-label', 'Navigation');
        });
    }

    getIconLabel(iconClass) {
        const iconLabels = {
            'fa-home': 'Home',
            'fa-user': 'User',
            'fa-cog': 'Settings',
            'fa-search': 'Search',
            'fa-plus': 'Add',
            'fa-minus': 'Remove',
            'fa-edit': 'Edit',
            'fa-trash': 'Delete',
            'fa-download': 'Download',
            'fa-upload': 'Upload',
            'fa-refresh': 'Refresh',
            'fa-sync': 'Sync',
            'fa-times': 'Close',
            'fa-check': 'Confirm',
            'fa-info': 'Information',
            'fa-warning': 'Warning',
            'fa-error': 'Error'
        };
        
        return iconLabels[iconClass] || 'Button';
    }

    getElementDescription(element) {
        return element.getAttribute('aria-label') ||
               element.textContent.trim() ||
               element.getAttribute('title') ||
               element.tagName.toLowerCase();
    }

    monitorAccessibilityChanges() {
        // Monitor for accessibility preference changes
        const queries = [
            { query: '(prefers-reduced-motion: reduce)', handler: this.setupMotionPreferences.bind(this) },
            { query: '(prefers-contrast: high)', handler: this.setupHighContrastSupport.bind(this) }
        ];
        
        queries.forEach(({ query, handler }) => {
            const mediaQuery = window.matchMedia(query);
            mediaQuery.addEventListener('change', handler);
        });
    }

    // Public API methods
    announce(message, priority = 'polite') {
        this.announceToScreenReader(message, priority);
    }

    focusElement(selector) {
        const element = document.querySelector(selector);
        if (element) {
            element.focus();
            return true;
        }
        return false;
    }

    addKeyboardShortcut(shortcut, action) {
        this.keyboardShortcuts.set(shortcut, action);
    }

    removeKeyboardShortcut(shortcut) {
        this.keyboardShortcuts.delete(shortcut);
    }

    // Shortcut actions
    goToHome() {
        window.location.href = '/';
    }

    goToDashboard() {
        const dashboardLink = document.querySelector('a[href*="Dashboard"], a[href="/Admin"]');
        if (dashboardLink) {
            dashboardLink.click();
        }
    }

    focusSearch() {
        const searchInput = document.querySelector('input[type="search"], input[placeholder*="search" i]');
        if (searchInput) {
            searchInput.focus();
        }
    }

    toggleMainMenu() {
        const menuToggle = document.querySelector('.navbar-toggler, [aria-expanded]');
        if (menuToggle) {
            menuToggle.click();
        }
    }

    showKeyboardShortcuts() {
        const shortcuts = [
            'Alt+H: Go to home',
            'Alt+D: Go to dashboard',  
            'Alt+S: Focus search',
            'Alt+M: Toggle main menu',
            'Tab: Next element',
            'Shift+Tab: Previous element',
            'Escape: Close dialog/menu',
            '?: Show this help'
        ];
        
        this.announce(`Keyboard shortcuts: ${shortcuts.join(', ')}`, 'assertive');
    }

    destroy() {
        // Cleanup
        const liveRegion = document.getElementById('aria-live-region');
        if (liveRegion) {
            liveRegion.remove();
        }
        
        const skipLink = document.getElementById('skip-to-content');
        if (skipLink) {
            skipLink.remove();
        }
        
        // Remove added styles
        const stylesToRemove = [
            'reduced-motion-styles',
            'focus-visibility-styles',
            'high-contrast-styles'
        ];
        
        stylesToRemove.forEach(id => {
            const style = document.getElementById(id);
            if (style) {
                style.remove();
            }
        });
    }
}

// Initialize accessibility enhancer
document.addEventListener('DOMContentLoaded', () => {
    window.accessibilityEnhancer = new AccessibilityEnhancer();
    console.log('♿ Accessibility Enhancer Initialized');
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = AccessibilityEnhancer;
}