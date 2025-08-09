/**
 * Admin Core Module
 * Central coordination module for admin interface components
 */

class AdminCore {
    constructor() {
        this.version = '2.0.0';
        this.modules = new Map();
        this.config = {
            debug: false,
            theme: 'modern',
            updateInterval: 30000,
            apiBase: '/api/admin'
        };
        
        this.init();
    }

    /**
     * Initialize admin core system
     */
    init() {
        this.setupEventSystem();
        this.loadConfiguration();
        this.registerModules();
        this.bindGlobalEvents();
        
        this.debug('Admin Core System initialized');
    }

    /**
     * Setup custom event system for inter-module communication
     */
    setupEventSystem() {
        this.eventBus = new EventTarget();
        
        // Global error handler
        window.addEventListener('error', (event) => {
            this.handleError(event.error, 'Global Error');
        });

        // Unhandled promise rejections
        window.addEventListener('unhandledrejection', (event) => {
            this.handleError(event.reason, 'Unhandled Promise Rejection');
        });
    }

    /**
     * Load configuration from local storage and server
     */
    async loadConfiguration() {
        try {
            // Load from localStorage
            const localConfig = localStorage.getItem('admin-config');
            if (localConfig) {
                this.config = { ...this.config, ...JSON.parse(localConfig) };
            }

            // Load from server if available
            const response = await fetch('/api/admin/config');
            if (response.ok) {
                const serverConfig = await response.json();
                this.config = { ...this.config, ...serverConfig };
            }
        } catch (error) {
            this.debug('Failed to load server configuration:', error);
        }
    }

    /**
     * Register all admin modules
     */
    registerModules() {
        // Register core modules
        this.registerModule('layout', AdminLayout);
        this.registerModule('dashboard', AdminDashboard);
        this.registerModule('tables', AdminTables);
        this.registerModule('forms', AdminForms);
        this.registerModule('modals', AdminModals);
        this.registerModule('notifications', AdminNotifications);
        
        this.emit('modules:registered');
    }

    /**
     * Register a module
     */
    registerModule(name, moduleClass) {
        try {
            const instance = new moduleClass(this);
            this.modules.set(name, instance);
            this.debug(`Module '${name}' registered successfully`);
            return instance;
        } catch (error) {
            this.error(`Failed to register module '${name}':`, error);
            return null;
        }
    }

    /**
     * Get a registered module
     */
    getModule(name) {
        return this.modules.get(name);
    }

    /**
     * Bind global events
     */
    bindGlobalEvents() {
        // Page unload cleanup
        window.addEventListener('beforeunload', () => {
            this.cleanup();
        });

        // Focus management
        document.addEventListener('keydown', (e) => {
            this.handleKeyboardShortcuts(e);
        });

        // Network status monitoring
        window.addEventListener('online', () => {
            this.emit('network:online');
            this.showNotification('success', 'Connection Restored', 'Network connection is back online');
        });

        window.addEventListener('offline', () => {
            this.emit('network:offline');
            this.showNotification('warning', 'Connection Lost', 'Network connection is offline');
        });
    }

    /**
     * Handle keyboard shortcuts
     */
    handleKeyboardShortcuts(event) {
        const { ctrlKey, metaKey, altKey, shiftKey, key } = event;
        const isCtrl = ctrlKey || metaKey;

        // Global shortcuts
        if (isCtrl) {
            switch (key) {
                case 'k':
                    event.preventDefault();
                    this.toggleCommandPalette();
                    break;
                case 'b':
                    event.preventDefault();
                    this.toggleSidebar();
                    break;
                case 'r':
                    event.preventDefault();
                    this.refreshCurrentView();
                    break;
                case '/':
                    event.preventDefault();
                    this.focusSearch();
                    break;
            }
        }

        // Escape key
        if (key === 'Escape') {
            this.handleEscape();
        }
    }

    /**
     * Handle escape key press
     */
    handleEscape() {
        // Close modals
        const openModal = document.querySelector('.modal.show');
        if (openModal) {
            const modal = bootstrap.Modal.getInstance(openModal);
            modal?.hide();
            return;
        }

        // Close dropdowns
        const openDropdown = document.querySelector('.dropdown-menu.show');
        if (openDropdown) {
            const dropdown = bootstrap.Dropdown.getInstance(openDropdown.previousElementSibling);
            dropdown?.hide();
            return;
        }

        // Clear search
        const searchInput = document.querySelector('input[type="search"]:focus');
        if (searchInput) {
            searchInput.value = '';
            searchInput.blur();
        }
    }

    /**
     * Event emitter methods
     */
    on(eventName, callback) {
        this.eventBus.addEventListener(eventName, callback);
    }

    off(eventName, callback) {
        this.eventBus.removeEventListener(eventName, callback);
    }

    emit(eventName, data = null) {
        const event = new CustomEvent(eventName, { detail: data });
        this.eventBus.dispatchEvent(event);
        this.debug(`Event emitted: ${eventName}`, data);
    }

    /**
     * API request helper
     */
    async request(endpoint, options = {}) {
        const defaultOptions = {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            }
        };

        // Add CSRF token
        const csrfToken = this.getCSRFToken();
        if (csrfToken) {
            defaultOptions.headers['X-CSRF-TOKEN'] = csrfToken;
        }

        const config = { ...defaultOptions, ...options };
        const url = endpoint.startsWith('http') ? endpoint : `${this.config.apiBase}${endpoint}`;

        try {
            const response = await fetch(url, config);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const contentType = response.headers.get('Content-Type');
            if (contentType && contentType.includes('application/json')) {
                return await response.json();
            }
            
            return await response.text();
        } catch (error) {
            this.handleError(error, `API Request to ${endpoint}`);
            throw error;
        }
    }

    /**
     * Get CSRF token
     */
    getCSRFToken() {
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') ||
               document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    }

    /**
     * Utility methods
     */
    toggleSidebar() {
        this.emit('layout:toggle-sidebar');
    }

    toggleCommandPalette() {
        this.emit('ui:toggle-command-palette');
    }

    refreshCurrentView() {
        this.emit('ui:refresh-view');
    }

    focusSearch() {
        const searchInput = document.querySelector('input[type="search"], .search-input');
        searchInput?.focus();
    }

    /**
     * Notification system
     */
    showNotification(type, title, message, options = {}) {
        const notificationModule = this.getModule('notifications');
        if (notificationModule) {
            return notificationModule.show(type, title, message, options);
        } else {
            // Fallback to browser notification
            console.log(`[${type.toUpperCase()}] ${title}: ${message}`);
        }
    }

    /**
     * Error handling
     */
    handleError(error, context = 'Unknown') {
        this.error(`[${context}]`, error);
        
        // Show user-friendly error
        this.showNotification('error', 'An Error Occurred', 
            'Something went wrong. Please try again or contact support if the issue persists.');

        // Send to error tracking service if available
        this.reportError(error, context);
    }

    reportError(error, context) {
        // Implementation for error reporting service
        // This could send to Sentry, LogRocket, or custom logging endpoint
        this.debug('Error reported:', { error, context });
    }

    /**
     * Logging methods
     */
    debug(...args) {
        if (this.config.debug) {
            console.debug('[AdminCore]', ...args);
        }
    }

    log(...args) {
        console.log('[AdminCore]', ...args);
    }

    warn(...args) {
        console.warn('[AdminCore]', ...args);
    }

    error(...args) {
        console.error('[AdminCore]', ...args);
    }

    /**
     * Configuration management
     */
    setConfig(key, value) {
        if (typeof key === 'object') {
            this.config = { ...this.config, ...key };
        } else {
            this.config[key] = value;
        }
        
        // Save to localStorage
        localStorage.setItem('admin-config', JSON.stringify(this.config));
        
        this.emit('config:changed', { key, value });
    }

    getConfig(key = null) {
        return key ? this.config[key] : this.config;
    }

    /**
     * Theme management
     */
    setTheme(theme) {
        this.setConfig('theme', theme);
        document.documentElement.setAttribute('data-theme', theme);
        this.emit('theme:changed', theme);
    }

    /**
     * Cleanup method
     */
    cleanup() {
        this.modules.forEach((module, name) => {
            if (typeof module.cleanup === 'function') {
                try {
                    module.cleanup();
                    this.debug(`Module '${name}' cleaned up`);
                } catch (error) {
                    this.error(`Failed to cleanup module '${name}':`, error);
                }
            }
        });

        this.emit('core:cleanup');
        this.debug('Admin Core cleanup completed');
    }

    /**
     * Status check
     */
    getStatus() {
        return {
            version: this.version,
            modules: Array.from(this.modules.keys()),
            config: this.config,
            isOnline: navigator.onLine,
            timestamp: Date.now()
        };
    }
}

// Base module class that other modules should extend
class AdminBaseModule {
    constructor(core) {
        this.core = core;
        this.name = this.constructor.name;
        this.initialized = false;
        
        this.init();
    }

    init() {
        this.initialized = true;
        this.debug('Module initialized');
    }

    debug(...args) {
        if (this.core.config.debug) {
            console.debug(`[${this.name}]`, ...args);
        }
    }

    emit(eventName, data) {
        this.core.emit(eventName, data);
    }

    on(eventName, callback) {
        this.core.on(eventName, callback);
    }

    cleanup() {
        this.debug('Module cleanup');
    }
}

// Export classes for use in other modules
window.AdminCore = AdminCore;
window.AdminBaseModule = AdminBaseModule;

// Auto-initialize if not in module mode
if (typeof module === 'undefined') {
    window.adminCore = new AdminCore();
}