/**
 * Admin Layout Module
 * Handles sidebar, topbar, and overall layout functionality
 */

class AdminLayout extends AdminBaseModule {
    constructor(core) {
        super(core);
        this.sidebar = null;
        this.content = null;
        this.topbar = null;
        this.isCollapsed = false;
        this.isMobile = false;
    }

    init() {
        super.init();
        
        this.initializeElements();
        this.setupSidebar();
        this.setupTopbar();
        this.setupResponsiveHandling();
        this.setupKeyboardNavigation();
        this.bindEvents();
        
        this.debug('Layout module initialized');
    }

    /**
     * Initialize DOM elements
     */
    initializeElements() {
        this.sidebar = document.querySelector('.admin-sidebar');
        this.content = document.querySelector('.admin-content');
        this.topbar = document.querySelector('.admin-topbar');
        this.sidebarToggle = document.querySelector('#sidebarToggle, .sidebar-toggle');
        this.mobileToggle = document.querySelector('.sidebar-toggle-mobile');
        
        if (!this.sidebar || !this.content) {
            this.debug('Required layout elements not found');
            return;
        }
    }

    /**
     * Setup sidebar functionality
     */
    setupSidebar() {
        if (!this.sidebar) return;

        // Restore sidebar state
        this.restoreSidebarState();
        
        // Setup navigation highlighting
        this.highlightActiveNavigation();
        
        // Setup navigation interactions
        this.setupNavigationInteractions();
        
        // Setup collapse/expand functionality
        if (this.sidebarToggle) {
            // Remove any existing event listeners to prevent conflicts
            this.sidebarToggle.removeEventListener('click', this._toggleHandler);
            
            // Create bound handler for removal later
            this._toggleHandler = (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.toggleSidebar();
            };
            
            this.sidebarToggle.addEventListener('click', this._toggleHandler);
        }
        
        // Setup overlay click handler for mobile
        this.overlay = document.getElementById('sidebarOverlay');
        if (this.overlay) {
            this._overlayHandler = () => {
                if (this.isMobile && this.sidebar.classList.contains('show')) {
                    this.closeMobileSidebar();
                }
            };
            this.overlay.addEventListener('click', this._overlayHandler);
        }
        
        // Setup escape key handler
        this._escapeHandler = (e) => {
            if (e.key === 'Escape' && this.isMobile && this.sidebar.classList.contains('show')) {
                this.closeMobileSidebar();
            }
        };
        document.addEventListener('keydown', this._escapeHandler);
    }

    /**
     * Setup topbar functionality
     */
    setupTopbar() {
        if (!this.topbar) return;

        // Auto-hide on scroll
        this.setupAutoHide();
        
        // Setup user menu
        this.setupUserMenu();
        
        // Setup notification system
        this.setupNotificationCenter();
    }

    /**
     * Setup responsive handling
     */
    setupResponsiveHandling() {
        // Mobile detection
        this.checkMobileView();
        
        // Mobile sidebar toggle
        if (this.mobileToggle) {
            this.mobileToggle.addEventListener('click', () => {
                this.toggleMobileSidebar();
            });
        }

        // Resize handler
        window.addEventListener('resize', this.debounce(() => {
            this.handleResize();
        }, 250));

        // Outside click handler for mobile
        document.addEventListener('click', (e) => {
            if (this.isMobile && this.sidebar.classList.contains('show')) {
                if (!this.sidebar.contains(e.target) && 
                    !e.target.closest('.sidebar-toggle-mobile')) {
                    this.closeMobileSidebar();
                }
            }
        });
    }

    /**
     * Setup keyboard navigation
     */
    setupKeyboardNavigation() {
        document.addEventListener('keydown', (e) => {
            // Tab navigation within sidebar
            if (e.key === 'Tab' && this.sidebar.contains(document.activeElement)) {
                this.handleTabNavigation(e);
            }
            
            // Arrow key navigation
            if ((e.key === 'ArrowUp' || e.key === 'ArrowDown') && 
                this.sidebar.contains(document.activeElement)) {
                this.handleArrowNavigation(e);
            }
        });
    }

    /**
     * Bind core events
     */
    bindEvents() {
        // Listen for layout events
        this.on('layout:toggle-sidebar', () => this.toggleSidebar());
        this.on('layout:collapse-sidebar', () => this.collapseSidebar());
        this.on('layout:expand-sidebar', () => this.expandSidebar());
        
        // Network status
        this.on('network:offline', () => this.showOfflineIndicator());
        this.on('network:online', () => this.hideOfflineIndicator());
        
        // Theme changes
        this.on('theme:changed', (event) => this.applyTheme(event.detail));
    }

    /**
     * Toggle sidebar (desktop)
     */
    toggleSidebar() {
        if (this.isMobile) {
            this.toggleMobileSidebar();
            return;
        }

        this.isCollapsed = !this.isCollapsed;
        
        if (this.isCollapsed) {
            this.collapseSidebar();
        } else {
            this.expandSidebar();
        }
    }

    /**
     * Collapse sidebar
     */
    collapseSidebar() {
        if (!this.sidebar || this.isMobile) return;

        this.sidebar.classList.add('collapsed');
        this.content.classList.add('sidebar-collapsed');
        this.isCollapsed = true;
        
        // Update toggle icon - keep bars icon for collapsed state
        if (this.sidebarToggle) {
            const icon = this.sidebarToggle.querySelector('i');
            if (icon) {
                icon.className = 'fas fa-bars';
            }
        }
        
        // Save state
        this.saveSidebarState();
        
        // Emit event
        this.emit('sidebar:collapsed');
        
        // Add tooltip to collapsed nav items
        this.addCollapsedTooltips();
    }

    /**
     * Expand sidebar
     */
    expandSidebar() {
        if (!this.sidebar || this.isMobile) return;

        this.sidebar.classList.remove('collapsed');
        this.content.classList.remove('sidebar-collapsed');
        this.isCollapsed = false;
        
        // Update toggle icon - use different icon for expanded state
        if (this.sidebarToggle) {
            const icon = this.sidebarToggle.querySelector('i');
            if (icon) {
                icon.className = 'fas fa-angle-left';
            }
        }
        
        // Save state
        this.saveSidebarState();
        
        // Emit event
        this.emit('sidebar:expanded');
        
        // Remove tooltips
        this.removeCollapsedTooltips();
    }

    /**
     * Toggle mobile sidebar
     */
    toggleMobileSidebar() {
        if (!this.sidebar) return;
        
        const isOpen = this.sidebar.classList.contains('show');
        
        if (isOpen) {
            this.closeMobileSidebar();
        } else {
            this.openMobileSidebar();
        }
    }

    /**
     * Open mobile sidebar
     */
    openMobileSidebar() {
        this.sidebar.classList.add('show');
        if (this.overlay) {
            this.overlay.classList.add('show');
        }
        document.body.style.overflow = 'hidden';
        
        // Focus first nav item for accessibility
        const firstNavLink = this.sidebar.querySelector('.nav-link');
        if (firstNavLink) {
            firstNavLink.focus();
        }
        
        this.emit('sidebar:mobile-opened');
    }

    /**
     * Close mobile sidebar
     */
    closeMobileSidebar() {
        this.sidebar.classList.remove('show');
        if (this.overlay) {
            this.overlay.classList.remove('show');
        }
        document.body.style.overflow = '';
        
        // Return focus to toggle button
        if (this.sidebarToggle) {
            this.sidebarToggle.focus();
        }
        
        this.emit('sidebar:mobile-closed');
    }

    /**
     * Setup navigation interactions
     */
    setupNavigationInteractions() {
        const navLinks = this.sidebar.querySelectorAll('.nav-link');
        
        navLinks.forEach(link => {
            // Hover effects
            link.addEventListener('mouseenter', () => {
                this.showNavTooltip(link);
            });
            
            link.addEventListener('mouseleave', () => {
                this.hideNavTooltip();
            });
            
            // Click handling
            link.addEventListener('click', (e) => {
                this.handleNavClick(e, link);
            });
        });

        // Setup sub-navigation if exists
        this.setupSubNavigation();
    }

    /**
     * Setup sub-navigation (collapsible menu items)
     */
    setupSubNavigation() {
        const expandableItems = this.sidebar.querySelectorAll('.nav-item[data-has-children]');
        
        expandableItems.forEach(item => {
            const toggle = item.querySelector('.nav-link');
            const submenu = item.querySelector('.nav-submenu');
            
            if (toggle && submenu) {
                toggle.addEventListener('click', (e) => {
                    e.preventDefault();
                    this.toggleSubmenu(item, submenu);
                });
            }
        });
    }

    /**
     * Toggle submenu
     */
    toggleSubmenu(item, submenu) {
        const isOpen = item.classList.contains('expanded');
        
        // Close all other submenus
        this.sidebar.querySelectorAll('.nav-item.expanded').forEach(openItem => {
            if (openItem !== item) {
                openItem.classList.remove('expanded');
                const openSubmenu = openItem.querySelector('.nav-submenu');
                if (openSubmenu) {
                    openSubmenu.style.maxHeight = '0';
                }
            }
        });
        
        // Toggle current submenu
        if (isOpen) {
            item.classList.remove('expanded');
            submenu.style.maxHeight = '0';
        } else {
            item.classList.add('expanded');
            submenu.style.maxHeight = submenu.scrollHeight + 'px';
        }
        
        this.emit('sidebar:submenu-toggled', { item, isOpen: !isOpen });
    }

    /**
     * Highlight active navigation
     */
    highlightActiveNavigation() {
        const currentPath = window.location.pathname;
        const navLinks = this.sidebar.querySelectorAll('.nav-link');
        
        navLinks.forEach(link => {
            link.classList.remove('active');
            
            const href = link.getAttribute('href');
            if (href && (currentPath === href || currentPath.startsWith(href + '/'))) {
                link.classList.add('active');
                
                // Expand parent submenu if needed
                const parentSubmenu = link.closest('.nav-submenu');
                if (parentSubmenu) {
                    const parentItem = parentSubmenu.closest('.nav-item');
                    if (parentItem) {
                        parentItem.classList.add('expanded');
                        parentSubmenu.style.maxHeight = parentSubmenu.scrollHeight + 'px';
                    }
                }
            }
        });
    }

    /**
     * Handle navigation click
     */
    handleNavClick(event, link) {
        // Close mobile sidebar on navigation
        if (this.isMobile) {
            this.closeMobileSidebar();
        }
        
        // Remove active class from all links
        this.sidebar.querySelectorAll('.nav-link').forEach(l => l.classList.remove('active'));
        
        // Add active class to clicked link
        link.classList.add('active');
        
        this.emit('navigation:clicked', { link, event });
    }

    /**
     * Setup auto-hide topbar
     */
    setupAutoHide() {
        let lastScrollTop = 0;
        let scrollTimer = null;
        
        const handleScroll = () => {
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
            
            if (scrollTimer) {
                clearTimeout(scrollTimer);
            }
            
            scrollTimer = setTimeout(() => {
                if (scrollTop > lastScrollTop && scrollTop > 100) {
                    // Scrolling down
                    this.topbar.classList.add('hidden');
                } else {
                    // Scrolling up
                    this.topbar.classList.remove('hidden');
                }
                
                lastScrollTop = scrollTop;
            }, 10);
        };
        
        window.addEventListener('scroll', this.throttle(handleScroll, 16));
    }

    /**
     * Setup user menu
     */
    setupUserMenu() {
        const userDropdown = this.topbar.querySelector('#userDropdown');
        if (!userDropdown) return;
        
        // Add click outside handler
        document.addEventListener('click', (e) => {
            if (!userDropdown.contains(e.target)) {
                const dropdown = bootstrap.Dropdown.getInstance(userDropdown);
                if (dropdown && dropdown._isShown()) {
                    dropdown.hide();
                }
            }
        });
    }

    /**
     * Setup notification center
     */
    setupNotificationCenter() {
        const notificationBtn = this.topbar.querySelector('#notificationsDropdown');
        if (!notificationBtn) return;
        
        // Load notifications
        this.loadNotifications();
        
        // Setup notification polling
        this.startNotificationPolling();
    }

    /**
     * Handle resize
     */
    handleResize() {
        const wasMobile = this.isMobile;
        this.checkMobileView();
        
        if (wasMobile && !this.isMobile) {
            // Switched from mobile to desktop
            this.sidebar.classList.remove('show');
            if (this.overlay) {
                this.overlay.classList.remove('show');
            }
            document.body.style.overflow = '';
            this.restoreSidebarState();
        } else if (!wasMobile && this.isMobile) {
            // Switched from desktop to mobile
            this.sidebar.classList.remove('collapsed');
            this.content.classList.remove('sidebar-collapsed');
            // Reset icon
            if (this.sidebarToggle) {
                const icon = this.sidebarToggle.querySelector('i');
                if (icon) icon.className = 'fas fa-bars';
            }
        }
    }

    /**
     * Check if mobile view
     */
    checkMobileView() {
        this.isMobile = window.innerWidth <= 768;
        document.documentElement.setAttribute('data-mobile', this.isMobile);
    }

    /**
     * Tab navigation handler
     */
    handleTabNavigation(event) {
        const focusableElements = this.sidebar.querySelectorAll('.nav-link:not([disabled])');
        const currentIndex = Array.from(focusableElements).indexOf(document.activeElement);
        
        let nextIndex;
        if (event.shiftKey) {
            nextIndex = currentIndex > 0 ? currentIndex - 1 : focusableElements.length - 1;
        } else {
            nextIndex = currentIndex < focusableElements.length - 1 ? currentIndex + 1 : 0;
        }
        
        event.preventDefault();
        focusableElements[nextIndex].focus();
    }

    /**
     * Arrow navigation handler
     */
    handleArrowNavigation(event) {
        const focusableElements = this.sidebar.querySelectorAll('.nav-link:not([disabled])');
        const currentIndex = Array.from(focusableElements).indexOf(document.activeElement);
        
        let nextIndex;
        if (event.key === 'ArrowUp') {
            nextIndex = currentIndex > 0 ? currentIndex - 1 : focusableElements.length - 1;
        } else {
            nextIndex = currentIndex < focusableElements.length - 1 ? currentIndex + 1 : 0;
        }
        
        event.preventDefault();
        focusableElements[nextIndex].focus();
    }

    /**
     * Tooltip management for collapsed sidebar
     */
    addCollapsedTooltips() {
        const navLinks = this.sidebar.querySelectorAll('.nav-link');
        
        navLinks.forEach(link => {
            const text = link.querySelector('span')?.textContent;
            if (text) {
                link.setAttribute('data-bs-toggle', 'tooltip');
                link.setAttribute('data-bs-placement', 'right');
                link.setAttribute('title', text);
                
                // Initialize Bootstrap tooltip
                new bootstrap.Tooltip(link);
            }
        });
    }

    /**
     * Remove collapsed tooltips
     */
    removeCollapsedTooltips() {
        const navLinks = this.sidebar.querySelectorAll('.nav-link[data-bs-toggle="tooltip"]');
        
        navLinks.forEach(link => {
            const tooltip = bootstrap.Tooltip.getInstance(link);
            if (tooltip) {
                tooltip.dispose();
            }
            
            link.removeAttribute('data-bs-toggle');
            link.removeAttribute('data-bs-placement');
            link.removeAttribute('title');
        });
    }

    /**
     * State management
     */
    saveSidebarState() {
        localStorage.setItem('admin-sidebar-collapsed', this.isCollapsed);
    }

    restoreSidebarState() {
        if (this.isMobile) return;
        
        const savedState = localStorage.getItem('admin-sidebar-collapsed');
        if (savedState === 'true') {
            this.collapseSidebar();
        } else {
            this.expandSidebar();
        }
    }

    /**
     * Notification system
     */
    async loadNotifications() {
        try {
            const notifications = await this.core.request('/notifications/recent');
            this.updateNotificationBadge(notifications.unreadCount);
            this.renderNotifications(notifications.items);
        } catch (error) {
            this.debug('Failed to load notifications:', error);
        }
    }

    updateNotificationBadge(count) {
        const badge = this.topbar.querySelector('#notificationBadge');
        if (badge) {
            if (count > 0) {
                badge.textContent = count > 99 ? '99+' : count;
                badge.classList.remove('d-none');
            } else {
                badge.classList.add('d-none');
            }
        }
    }

    startNotificationPolling() {
        setInterval(() => {
            this.loadNotifications();
        }, 60000); // Check every minute
    }

    /**
     * Theme application
     */
    applyTheme(theme) {
        document.documentElement.setAttribute('data-admin-theme', theme);
        this.debug('Theme applied:', theme);
    }

    /**
     * Network status indicators
     */
    showOfflineIndicator() {
        let indicator = this.topbar.querySelector('.offline-indicator');
        if (!indicator) {
            indicator = document.createElement('div');
            indicator.className = 'offline-indicator badge bg-warning';
            indicator.textContent = 'Offline';
            this.topbar.querySelector('.navbar-nav').prepend(indicator);
        }
    }

    hideOfflineIndicator() {
        const indicator = this.topbar.querySelector('.offline-indicator');
        if (indicator) {
            indicator.remove();
        }
    }

    /**
     * Utility methods
     */
    debounce(func, wait) {
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

    throttle(func, limit) {
        let inThrottle;
        return function(...args) {
            if (!inThrottle) {
                func.apply(this, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }

    /**
     * Cleanup
     */
    cleanup() {
        super.cleanup();
        
        // Remove event listeners
        if (this.sidebarToggle && this._toggleHandler) {
            this.sidebarToggle.removeEventListener('click', this._toggleHandler);
        }
        
        if (this.overlay && this._overlayHandler) {
            this.overlay.removeEventListener('click', this._overlayHandler);
        }
        
        if (this._escapeHandler) {
            document.removeEventListener('keydown', this._escapeHandler);
        }
        
        window.removeEventListener('resize', this.handleResize);
        window.removeEventListener('scroll', this.handleScroll);
        
        // Cleanup tooltips
        this.removeCollapsedTooltips();
    }
}

// Export for module registration
window.AdminLayout = AdminLayout;