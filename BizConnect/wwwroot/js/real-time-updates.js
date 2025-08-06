/**
 * BizConnect Real-Time Updates System
 * Manages live data updates, notifications, and system monitoring
 */

class RealTimeUpdates {
    constructor() {
        this.config = {
            updateInterval: 30000, // 30 seconds
            healthCheckInterval: 60000, // 1 minute
            maxRetries: 3,
            retryDelay: 5000,
            endpoints: {
                stats: '/api/dashboard/stats',
                health: '/api/system/health',
                activities: '/api/dashboard/activities',
                notifications: '/api/notifications/recent'
            }
        };
        
        this.state = {
            isConnected: false,
            retryCount: 0,
            lastUpdate: null,
            activeUpdates: new Set()
        };
        
        this.init();
    }

    /**
     * Initialize real-time updates system
     */
    init() {
        this.setupPeriodicUpdates();
        this.setupSystemHealthMonitoring();
        this.setupNotificationSystem();
        this.setupConnectionMonitoring();
        this.setupVisibilityHandling();
        
        console.log('⚡ Real-Time Updates System Initialized');
    }

    // =================
    // PERIODIC UPDATES
    // =================

    setupPeriodicUpdates() {
        // Start dashboard statistics updates
        if (this.isDashboardPage()) {
            this.startDashboardUpdates();
        }
        
        // Start activity feed updates
        if (this.hasActivityFeed()) {
            this.startActivityUpdates();
        }
        
        // Start notification updates for authenticated users
        if (this.isUserAuthenticated()) {
            this.startNotificationUpdates();
        }
    }

    startDashboardUpdates() {
        const updateStats = async () => {
            if (!document.hidden && this.state.isConnected) {
                await this.updateDashboardStats();
            }
        };

        // Initial update
        updateStats();
        
        // Set up interval
        const intervalId = setInterval(updateStats, this.config.updateInterval);
        this.state.activeUpdates.add(intervalId);
    }

    async updateDashboardStats() {
        try {
            const response = await fetch(this.config.endpoints.stats, {
                method: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const stats = await response.json();
            this.processDashboardStats(stats);
            this.updateConnectionState(true);
            
        } catch (error) {
            console.warn('Dashboard stats update failed:', error);
            this.handleUpdateError('stats', error);
        }
    }

    processDashboardStats(stats) {
        // Update stat counters with smooth animations
        Object.entries(stats).forEach(([key, value]) => {
            this.updateStatWidget(key, value);
        });

        // Update progress bars
        this.updateProgressBars(stats);
        
        // Update trend indicators
        this.updateTrendIndicators(stats);
        
        // Update timestamp
        this.updateLastUpdatedTime();
        
        this.state.lastUpdate = Date.now();
    }

    updateStatWidget(statKey, newValue) {
        const selectors = [
            `[data-stat="${statKey}"]`,
            `#stat-${this.kebabCase(statKey)}`,
            `[data-stat-key="${statKey}"]`
        ];

        for (const selector of selectors) {
            const element = document.querySelector(selector);
            if (element) {
                const numberElement = element.querySelector('.stats-number, .stat-value, .counter');
                if (numberElement) {
                    this.animateStatChange(numberElement, newValue);
                }
                break;
            }
        }
    }

    animateStatChange(element, newValue) {
        const currentValue = parseInt(element.textContent.replace(/[^\d]/g, '')) || 0;
        
        if (currentValue === newValue) return;

        // Add change indicator
        this.showChangeIndicator(element, newValue > currentValue);
        
        // Animate to new value
        this.animateCounter(element, currentValue, newValue, 1500);
    }

    showChangeIndicator(element, isIncrease) {
        const indicator = element.parentNode.querySelector('.change-indicator');
        if (indicator) indicator.remove();

        const changeEl = document.createElement('span');
        changeEl.className = `change-indicator ${isIncrease ? 'increase' : 'decrease'}`;
        changeEl.innerHTML = `<i class="fas fa-arrow-${isIncrease ? 'up' : 'down'}"></i>`;
        
        element.parentNode.appendChild(changeEl);
        
        // Remove after animation
        setTimeout(() => changeEl.remove(), 2000);
    }

    animateCounter(element, start, end, duration = 1500) {
        const startTime = Date.now();
        const range = end - start;

        const updateCounter = () => {
            const elapsed = Date.now() - startTime;
            const progress = Math.min(elapsed / duration, 1);
            
            // Smooth easing function
            const easeOutCubic = 1 - Math.pow(1 - progress, 3);
            const current = Math.round(start + range * easeOutCubic);
            
            element.textContent = current.toLocaleString();
            
            if (progress < 1) {
                requestAnimationFrame(updateCounter);
            }
        };

        requestAnimationFrame(updateCounter);
    }

    updateProgressBars(stats) {
        document.querySelectorAll('.progress-bar-modern[data-stat]').forEach(bar => {
            const statKey = bar.dataset.stat;
            const value = stats[statKey];
            
            if (value !== undefined) {
                bar.style.width = `${value}%`;
                
                // Update text if present
                const textElement = bar.parentNode.nextElementSibling?.querySelector('.progress-text');
                if (textElement) {
                    textElement.textContent = `${value}%`;
                }
            }
        });
    }

    updateTrendIndicators(stats) {
        document.querySelectorAll('[data-trend]').forEach(indicator => {
            const trendKey = indicator.dataset.trend;
            const trendValue = stats[`${trendKey}_trend`];
            
            if (trendValue !== undefined) {
                indicator.className = `trend-indicator ${trendValue > 0 ? 'positive' : trendValue < 0 ? 'negative' : 'neutral'}`;
                indicator.innerHTML = `
                    <i class="fas fa-arrow-${trendValue > 0 ? 'up' : trendValue < 0 ? 'down' : 'right'}"></i>
                    ${Math.abs(trendValue)}%
                `;
            }
        });
    }

    // =================
    // ACTIVITY UPDATES
    // =================

    startActivityUpdates() {
        const updateActivities = async () => {
            if (!document.hidden) {
                await this.updateActivityFeed();
            }
        };

        // Initial update
        updateActivities();
        
        // Set up interval (less frequent than stats)
        const intervalId = setInterval(updateActivities, this.config.updateInterval * 2);
        this.state.activeUpdates.add(intervalId);
    }

    async updateActivityFeed() {
        try {
            const response = await fetch(this.config.endpoints.activities);
            const activities = await response.json();
            
            this.processActivityUpdate(activities);
            
        } catch (error) {
            console.warn('Activity feed update failed:', error);
            this.handleUpdateError('activities', error);
        }
    }

    processActivityUpdate(activities) {
        const activityContainer = document.querySelector('.activity-feed, .recent-activity');
        if (!activityContainer) return;

        // Check for new activities
        const existingIds = Array.from(activityContainer.querySelectorAll('[data-activity-id]'))
            .map(el => el.dataset.activityId);
        
        const newActivities = activities.filter(activity => 
            !existingIds.includes(activity.id?.toString())
        );

        // Add new activities with animation
        newActivities.forEach((activity, index) => {
            setTimeout(() => {
                this.addActivityItem(activity, activityContainer);
            }, index * 200);
        });

        // Update existing activities
        this.updateExistingActivities(activities, activityContainer);
    }

    addActivityItem(activity, container) {
        const activityEl = this.createActivityElement(activity);
        
        // Add with animation
        activityEl.style.opacity = '0';
        activityEl.style.transform = 'translateY(-20px)';
        container.insertBefore(activityEl, container.firstChild);
        
        // Animate in
        requestAnimationFrame(() => {
            activityEl.style.transition = 'all 0.3s ease';
            activityEl.style.opacity = '1';
            activityEl.style.transform = 'translateY(0)';
        });

        // Remove old activities to prevent overflow
        const maxActivities = 20;
        const allActivities = container.querySelectorAll('.activity-item');
        if (allActivities.length > maxActivities) {
            allActivities[allActivities.length - 1].remove();
        }
    }

    createActivityElement(activity) {
        const div = document.createElement('div');
        div.className = 'activity-item d-flex align-items-center p-3 mb-3 glass rounded-3';
        div.dataset.activityId = activity.id;
        
        div.innerHTML = `
            <div class="flex-shrink-0 me-3">
                <div class="status-indicator status-${activity.type?.toLowerCase()}">
                    <div class="status-dot"></div>
                    <span>${activity.status}</span>
                </div>
            </div>
            <div class="flex-grow-1">
                <div class="fw-semibold text-dark mb-1">${activity.description}</div>
                <div class="text-muted small">
                    <i class="fas fa-clock me-1"></i>${this.formatRelativeTime(activity.timestamp)}
                    ${activity.user ? `<span class="ms-3"><i class="fas fa-user me-1"></i>${activity.user}</span>` : ''}
                </div>
            </div>
            <div class="flex-shrink-0">
                <div class="action-group">
                    <a href="#" class="action-btn action-btn-view" title="View Details">
                        <i class="fas fa-eye"></i>
                    </a>
                </div>
            </div>
        `;
        
        return div;
    }

    // =================
    // SYSTEM HEALTH MONITORING
    // =================

    setupSystemHealthMonitoring() {
        const checkHealth = async () => {
            if (!document.hidden) {
                await this.checkSystemHealth();
            }
        };

        // Initial check
        checkHealth();
        
        // Regular health checks
        const intervalId = setInterval(checkHealth, this.config.healthCheckInterval);
        this.state.activeUpdates.add(intervalId);
    }

    async checkSystemHealth() {
        try {
            const response = await fetch(this.config.endpoints.health);
            const health = await response.json();
            
            this.processHealthUpdate(health);
            this.updateConnectionState(true);
            
        } catch (error) {
            console.warn('System health check failed:', error);
            this.updateConnectionState(false);
            this.handleUpdateError('health', error);
        }
    }

    processHealthUpdate(health) {
        // Update system status indicators
        document.querySelectorAll('[data-health-component]').forEach(component => {
            const componentName = component.dataset.healthComponent;
            const status = health.components?.[componentName] || health[componentName];
            
            if (status) {
                this.updateHealthIndicator(component, status);
            }
        });

        // Update overall health status
        const overallStatus = health.status || 'unknown';
        this.updateOverallHealthStatus(overallStatus);
    }

    updateHealthIndicator(element, status) {
        const statusEl = element.querySelector('.health-status, .status-indicator');
        if (statusEl) {
            statusEl.className = `health-status status-${status.status?.toLowerCase() || 'unknown'}`;
            statusEl.textContent = status.status || 'Unknown';
        }

        // Update progress bar if present
        const progressBar = element.querySelector('.progress-bar');
        if (progressBar && status.metrics?.utilization !== undefined) {
            progressBar.style.width = `${status.metrics.utilization}%`;
        }
    }

    updateOverallHealthStatus(status) {
        const indicators = document.querySelectorAll('.system-health-indicator');
        indicators.forEach(indicator => {
            indicator.className = `system-health-indicator status-${status.toLowerCase()}`;
            indicator.textContent = status.charAt(0).toUpperCase() + status.slice(1);
        });
    }

    // =================
    // NOTIFICATION SYSTEM
    // =================

    setupNotificationSystem() {
        if (!this.isUserAuthenticated()) return;

        this.startNotificationUpdates();
        this.setupNotificationDisplay();
    }

    startNotificationUpdates() {
        const updateNotifications = async () => {
            if (!document.hidden) {
                await this.checkNotifications();
            }
        };

        // Check immediately
        updateNotifications();
        
        // Regular checks
        const intervalId = setInterval(updateNotifications, this.config.updateInterval);
        this.state.activeUpdates.add(intervalId);
    }

    async checkNotifications() {
        try {
            const response = await fetch(this.config.endpoints.notifications);
            const notifications = await response.json();
            
            this.processNotifications(notifications);
            
        } catch (error) {
            console.warn('Notification check failed:', error);
            this.handleUpdateError('notifications', error);
        }
    }

    processNotifications(notifications) {
        // Update notification badge
        const badge = document.querySelector('.notification-badge, #notificationBadge');
        if (badge) {
            const unreadCount = notifications.filter(n => !n.read).length;
            
            if (unreadCount > 0) {
                badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
                badge.classList.remove('d-none');
            } else {
                badge.classList.add('d-none');
            }
        }

        // Show new notifications as toasts
        const lastCheck = this.getLastNotificationCheck();
        const newNotifications = notifications.filter(n => 
            new Date(n.created_at) > lastCheck && !n.read
        );

        newNotifications.forEach((notification, index) => {
            setTimeout(() => {
                this.showNotificationToast(notification);
            }, index * 500);
        });

        this.setLastNotificationCheck(Date.now());
    }

    showNotificationToast(notification) {
        if (window.ModernUI) {
            ModernUI.showNotification(
                notification.type || 'info',
                notification.title || 'New Notification',
                notification.message,
                8000
            );
        }
    }

    // =================
    // CONNECTION MONITORING
    // =================

    setupConnectionMonitoring() {
        // Monitor online/offline status
        window.addEventListener('online', () => {
            this.updateConnectionState(true);
            this.resumeUpdates();
        });

        window.addEventListener('offline', () => {
            this.updateConnectionState(false);
            this.pauseUpdates();
        });

        // Initial connection state
        this.updateConnectionState(navigator.onLine);
    }

    updateConnectionState(isConnected) {
        const wasConnected = this.state.isConnected;
        this.state.isConnected = isConnected;

        // Update UI indicators
        const indicators = document.querySelectorAll('.connection-status');
        indicators.forEach(indicator => {
            indicator.className = `connection-status ${isConnected ? 'connected' : 'disconnected'}`;
            indicator.textContent = isConnected ? 'Connected' : 'Disconnected';
        });

        // Show connection change notification
        if (wasConnected !== isConnected) {
            if (isConnected) {
                this.showConnectionRestored();
                this.state.retryCount = 0;
            } else {
                this.showConnectionLost();
            }
        }
    }

    showConnectionRestored() {
        if (window.ModernUI) {
            ModernUI.showNotification('success', 'Connection Restored', 'Real-time updates resumed', 3000);
        }
    }

    showConnectionLost() {
        if (window.ModernUI) {
            ModernUI.showNotification('warning', 'Connection Lost', 'Updates paused until connection is restored', 5000);
        }
    }

    pauseUpdates() {
        this.state.activeUpdates.forEach(intervalId => {
            clearInterval(intervalId);
        });
        this.state.activeUpdates.clear();
    }

    resumeUpdates() {
        this.setupPeriodicUpdates();
    }

    // =================
    // VISIBILITY HANDLING
    // =================

    setupVisibilityHandling() {
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                // Page is hidden - reduce update frequency
                this.reduceUpdateFrequency();
            } else {
                // Page is visible - restore normal updates
                this.restoreUpdateFrequency();
                // Immediate update when becoming visible
                this.performImmediateUpdate();
            }
        });
    }

    reduceUpdateFrequency() {
        // Implementation would modify intervals to be less frequent
        console.log('Reducing update frequency for background tab');
    }

    restoreUpdateFrequency() {
        // Implementation would restore normal intervals
        console.log('Restoring normal update frequency');
    }

    async performImmediateUpdate() {
        if (this.state.isConnected) {
            try {
                await Promise.all([
                    this.updateDashboardStats(),
                    this.updateActivityFeed(),
                    this.checkNotifications()
                ]);
            } catch (error) {
                console.warn('Immediate update failed:', error);
            }
        }
    }

    // =================
    // ERROR HANDLING
    // =================

    handleUpdateError(updateType, error) {
        this.state.retryCount++;
        
        if (this.state.retryCount > this.config.maxRetries) {
            console.error(`Max retries exceeded for ${updateType} updates:`, error);
            this.updateConnectionState(false);
            return;
        }

        // Retry after delay
        setTimeout(() => {
            switch (updateType) {
                case 'stats':
                    this.updateDashboardStats();
                    break;
                case 'activities':
                    this.updateActivityFeed();
                    break;
                case 'health':
                    this.checkSystemHealth();
                    break;
                case 'notifications':
                    this.checkNotifications();
                    break;
            }
        }, this.config.retryDelay);
    }

    // =================
    // UTILITY METHODS
    // =================

    isDashboardPage() {
        return window.location.pathname.includes('/Admin') || 
               document.querySelector('.admin-dashboard') !== null;
    }

    hasActivityFeed() {
        return document.querySelector('.activity-feed, .recent-activity') !== null;
    }

    isUserAuthenticated() {
        return document.querySelector('meta[name="user-authenticated"]')?.content === 'true';
    }

    updateLastUpdatedTime() {
        const timeElements = document.querySelectorAll('.last-updated');
        const now = new Date().toLocaleTimeString();
        
        timeElements.forEach(el => {
            el.textContent = `Last updated: ${now}`;
        });
    }

    formatRelativeTime(timestamp) {
        const date = new Date(timestamp);
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMins / 60);
        const diffDays = Math.floor(diffHours / 24);

        if (diffMins < 1) return 'Just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 7) return `${diffDays}d ago`;
        
        return date.toLocaleDateString();
    }

    kebabCase(str) {
        return str.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase();
    }

    getLastNotificationCheck() {
        const stored = localStorage.getItem('lastNotificationCheck');
        return stored ? new Date(parseInt(stored)) : new Date(0);
    }

    setLastNotificationCheck(timestamp) {
        localStorage.setItem('lastNotificationCheck', timestamp.toString());
    }

    // =================
    // PUBLIC API
    // =================

    // Method to manually trigger updates
    async forceUpdate(type = 'all') {
        const updates = {
            stats: () => this.updateDashboardStats(),
            activities: () => this.updateActivityFeed(),
            health: () => this.checkSystemHealth(),
            notifications: () => this.checkNotifications(),
            all: () => this.performImmediateUpdate()
        };

        const updateFunction = updates[type];
        if (updateFunction) {
            await updateFunction();
        }
    }

    // Method to pause/resume updates
    toggleUpdates(pause = null) {
        const shouldPause = pause !== null ? pause : this.state.activeUpdates.size > 0;
        
        if (shouldPause) {
            this.pauseUpdates();
        } else {
            this.resumeUpdates();
        }
    }

    // Method to get current state
    getState() {
        return { ...this.state };
    }

    // Method to update configuration
    updateConfig(newConfig) {
        this.config = { ...this.config, ...newConfig };
    }
}

// Initialize Real-Time Updates System
const realTimeUpdates = new RealTimeUpdates();

// Export for global use
window.RealTimeUpdates = realTimeUpdates;
window.realTimeUpdates = realTimeUpdates;