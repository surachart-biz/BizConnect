/**
 * Real-Time Analytics Dashboard
 * Provides live updates, interactive charts, and professional UX
 */
class RealTimeDashboard {
    constructor() {
        this.refreshInterval = 30000; // 30 seconds
        this.charts = {};
        this.refreshTimer = null;
        this.isUpdating = false;
        this.errorCount = 0;
        this.maxErrors = 3;
        
        // Chart.js default configuration
        Chart.defaults.font.family = "'Inter', sans-serif";
        Chart.defaults.color = '#6c757d';
        Chart.defaults.plugins.legend.display = true;
        Chart.defaults.responsive = true;
        Chart.defaults.maintainAspectRatio = false;
    }

    /**
     * Initialize the dashboard with initial data
     */
    async initialize(initialData = null) {
        console.log('Initializing Real-Time Analytics Dashboard');
        
        try {
            // Initialize charts
            this.initializeCharts(initialData);
            
            // Load additional data
            await this.loadAdditionalData();
            
            // Start auto-refresh
            this.startAutoRefresh();
            
            // Setup event listeners
            this.setupEventListeners();
            
            // Show success indicator
            this.showUpdateIndicator('Dashboard initialized successfully', 'success');
            
        } catch (error) {
            console.error('Failed to initialize dashboard:', error);
            this.showUpdateIndicator('Failed to initialize dashboard', 'error');
        }
    }

    /**
     * Initialize all charts with data
     */
    initializeCharts(data) {
        // OTAC Trends Chart (Line Chart)
        this.initializeOtacTrendsChart();
        
        // Status Distribution Chart (Doughnut Chart)
        this.initializeStatusDistributionChart(data);
        
        // Performance Timeline Chart (Multi-line Chart)
        this.initializePerformanceTimelineChart();
    }

    /**
     * Initialize OTAC Trends Line Chart
     */
    initializeOtacTrendsChart() {
        const ctx = document.getElementById('otacTrendsChart');
        if (!ctx) return;

        this.charts.otacTrends = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: 'OTAC Generated',
                        data: [],
                        borderColor: '#667eea',
                        backgroundColor: 'rgba(102, 126, 234, 0.1)',
                        borderWidth: 3,
                        fill: true,
                        tension: 0.4,
                        pointBackgroundColor: '#667eea',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        pointRadius: 6,
                        pointHoverRadius: 8
                    },
                    {
                        label: 'Registrations',
                        data: [],
                        borderColor: '#f5576c',
                        backgroundColor: 'rgba(245, 87, 108, 0.1)',
                        borderWidth: 3,
                        fill: true,
                        tension: 0.4,
                        pointBackgroundColor: '#f5576c',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        pointRadius: 6,
                        pointHoverRadius: 8
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    intersect: false,
                    mode: 'index'
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 20,
                            font: {
                                size: 12,
                                weight: '500'
                            }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        titleColor: '#fff',
                        bodyColor: '#fff',
                        borderColor: '#667eea',
                        borderWidth: 1,
                        cornerRadius: 8,
                        displayColors: true,
                        callbacks: {
                            label: function(context) {
                                return context.dataset.label + ': ' + context.parsed.y.toLocaleString();
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: '#6c757d',
                            font: {
                                size: 11
                            }
                        }
                    },
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        ticks: {
                            color: '#6c757d',
                            font: {
                                size: 11
                            }
                        }
                    }
                }
            }
        });

        // Load initial trend data
        this.loadOtacTrends(7);
    }

    /**
     * Initialize Status Distribution Doughnut Chart
     */
    initializeStatusDistributionChart(data) {
        const ctx = document.getElementById('statusDistributionChart');
        if (!ctx || !data) return;

        const statusData = data.registrations?.statusBreakdown || {
            success: 0, pending: 0, failed: 0, expired: 0
        };

        this.charts.statusDistribution = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Success', 'Pending', 'Failed', 'Expired'],
                datasets: [{
                    data: [
                        statusData.success,
                        statusData.pending,
                        statusData.failed,
                        statusData.expired
                    ],
                    backgroundColor: [
                        '#28a745',
                        '#ffc107',
                        '#dc3545',
                        '#6c757d'
                    ],
                    borderWidth: 0,
                    hoverBorderWidth: 3,
                    hoverBorderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '70%',
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        titleColor: '#fff',
                        bodyColor: '#fff',
                        cornerRadius: 8,
                        callbacks: {
                            label: function(context) {
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = total > 0 ? ((context.parsed / total) * 100).toFixed(1) : 0;
                                return context.label + ': ' + context.parsed.toLocaleString() + ' (' + percentage + '%)';
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Initialize Performance Timeline Chart
     */
    initializePerformanceTimelineChart() {
        const ctx = document.getElementById('performanceTimelineChart');
        if (!ctx) return;

        this.charts.performanceTimeline = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: 'Success Rate (%)',
                        data: [],
                        borderColor: '#28a745',
                        backgroundColor: 'rgba(40, 167, 69, 0.1)',
                        borderWidth: 2,
                        fill: false,
                        tension: 0.4,
                        yAxisID: 'y'
                    },
                    {
                        label: 'Response Time (ms)',
                        data: [],
                        borderColor: '#667eea',
                        backgroundColor: 'rgba(102, 126, 234, 0.1)',
                        borderWidth: 2,
                        fill: false,
                        tension: 0.4,
                        yAxisID: 'y1'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    intersect: false,
                    mode: 'index'
                },
                plugins: {
                    legend: {
                        position: 'top'
                    }
                },
                scales: {
                    x: {
                        grid: {
                            display: false
                        }
                    },
                    y: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        beginAtZero: true,
                        max: 100,
                        ticks: {
                            callback: function(value) {
                                return value + '%';
                            }
                        }
                    },
                    y1: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        beginAtZero: true,
                        grid: {
                            drawOnChartArea: false,
                        },
                        ticks: {
                            callback: function(value) {
                                return value + 'ms';
                            }
                        }
                    }
                }
            }
        });
    }

    /**
     * Start automatic refresh of metrics
     */
    startAutoRefresh() {
        console.log('Starting auto-refresh every', this.refreshInterval / 1000, 'seconds');
        
        this.refreshTimer = setInterval(() => {
            this.refreshMetrics();
        }, this.refreshInterval);
    }

    /**
     * Stop automatic refresh
     */
    stopAutoRefresh() {
        if (this.refreshTimer) {
            clearInterval(this.refreshTimer);
            this.refreshTimer = null;
            console.log('Auto-refresh stopped');
        }
    }

    /**
     * Refresh all dashboard metrics
     */
    async refreshMetrics() {
        if (this.isUpdating) {
            console.log('Update already in progress, skipping...');
            return;
        }

        this.isUpdating = true;
        this.updateConnectionStatus('updating');

        try {
            console.log('Refreshing dashboard metrics...');
            
            const response = await fetch('/Admin/Analytics/GetRealTimeMetrics', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const result = await response.json();
            
            if (result.success && result.data) {
                this.updateMetricCards(result.data);
                this.updateLastUpdatedTime();
                this.showUpdateIndicator('Metrics updated successfully', 'success');
                this.errorCount = 0; // Reset error count on success
            } else {
                throw new Error(result.error || 'Invalid response format');
            }

        } catch (error) {
            console.error('Failed to refresh metrics:', error);
            this.errorCount++;
            
            if (this.errorCount >= this.maxErrors) {
                this.stopAutoRefresh();
                this.showUpdateIndicator('Auto-refresh disabled due to repeated errors', 'error');
                this.updateConnectionStatus('error');
            } else {
                this.showUpdateIndicator(`Update failed (${this.errorCount}/${this.maxErrors})`, 'warning');
                this.updateConnectionStatus('warning');
            }
        } finally {
            this.isUpdating = false;
        }
    }

    /**
     * Update metric cards with new data
     */
    updateMetricCards(data) {
        // Update Active OTAC Count
        this.updateElement('activeOtacCount', data.activeOtacCount);
        
        // Update Registrations Last Hour (if available)
        if (data.registrationsLastHour !== undefined) {
            this.updateElement('registrationsToday', data.registrationsLastHour, '+');
        }
        
        // Update Response Time
        if (data.currentResponseTime !== undefined) {
            this.updateElement('responseTime', Math.round(data.currentResponseTime));
            this.updatePerformanceStatus(data.currentResponseTime);
        }
        
        // Update Security Events
        if (data.recentSecurityEvents !== undefined) {
            this.updateElement('securityEvents', data.recentSecurityEvents);
        }
        
        // Update Cache Hit Rate
        if (data.recentCacheHitRate !== undefined) {
            this.updateElement('cacheHitRate', data.recentCacheHitRate.toFixed(1));
            this.updateProgressBar('cacheHitRateBar', data.recentCacheHitRate);
        }
        
        this.updateConnectionStatus('connected');
    }

    /**
     * Update a single element with animation
     */
    updateElement(elementId, newValue, prefix = '') {
        const element = document.getElementById(elementId);
        if (!element) return;

        const currentValue = element.textContent;
        const displayValue = prefix + newValue.toLocaleString();
        
        if (currentValue !== displayValue) {
            element.style.transform = 'scale(1.05)';
            element.style.color = '#667eea';
            
            setTimeout(() => {
                element.textContent = displayValue;
                element.style.transform = 'scale(1)';
                element.style.color = '';
            }, 150);
        }
    }

    /**
     * Update progress bar
     */
    updateProgressBar(elementId, percentage) {
        const element = document.getElementById(elementId);
        if (element) {
            element.style.width = percentage + '%';
        }
    }

    /**
     * Update performance status text
     */
    updatePerformanceStatus(responseTime) {
        const element = document.getElementById('performanceStatus');
        if (!element) return;

        let status, className;
        if (responseTime < 1000) {
            status = 'Excellent';
            className = 'text-success';
        } else if (responseTime < 2000) {
            status = 'Good';
            className = 'text-warning';
        } else {
            status = 'Needs Attention';
            className = 'text-danger';
        }
        
        element.textContent = status;
        element.className = 'trend-text performance-status ' + className;
    }

    /**
     * Update connection status indicator
     */
    updateConnectionStatus(status) {
        const statusElement = document.getElementById('connectionStatus');
        if (!statusElement) return;

        const iconElement = statusElement.querySelector('i');
        
        statusElement.className = 'badge fs-6';
        iconElement.className = 'fas fa-circle';
        
        switch (status) {
            case 'connected':
                statusElement.classList.add('bg-success');
                iconElement.classList.add('pulse-icon');
                statusElement.innerHTML = '<i class="fas fa-circle pulse-icon"></i> Live';
                break;
            case 'updating':
                statusElement.classList.add('bg-info');
                iconElement.classList.add('fa-spin');
                statusElement.innerHTML = '<i class="fas fa-circle fa-spin"></i> Updating';
                break;
            case 'warning':
                statusElement.classList.add('bg-warning');
                statusElement.innerHTML = '<i class="fas fa-exclamation-triangle"></i> Warning';
                break;
            case 'error':
                statusElement.classList.add('bg-danger');
                statusElement.innerHTML = '<i class="fas fa-times-circle"></i> Error';
                break;
        }
    }

    /**
     * Update last updated timestamp
     */
    updateLastUpdatedTime() {
        const element = document.getElementById('lastUpdated');
        if (element) {
            const now = new Date();
            element.textContent = `Updated: ${now.toLocaleTimeString()}`;
        }
    }

    /**
     * Show update indicator
     */
    showUpdateIndicator(message, type = 'success') {
        const indicator = document.createElement('div');
        indicator.className = `update-indicator ${type}`;
        indicator.textContent = message;
        
        document.body.appendChild(indicator);
        
        // Show indicator
        setTimeout(() => indicator.classList.add('show'), 100);
        
        // Hide and remove indicator
        setTimeout(() => {
            indicator.classList.remove('show');
            setTimeout(() => document.body.removeChild(indicator), 300);
        }, 3000);
    }

    /**
     * Load OTAC trends data
     */
    async loadOtacTrends(days = 7) {
        try {
            const response = await fetch(`/Admin/Analytics/GetOtacTrends?days=${days}`);
            const result = await response.json();
            
            if (result.success && result.data && this.charts.otacTrends) {
                const chart = this.charts.otacTrends;
                chart.data.labels = result.data.labels;
                chart.data.datasets[0].data = result.data.otacTrend;
                chart.data.datasets[1].data = result.data.registrationTrend;
                chart.update('none');
            }
        } catch (error) {
            console.error('Failed to load OTAC trends:', error);
        }
    }

    /**
     * Load additional dashboard data
     */
    async loadAdditionalData() {
        // Load top branches
        this.loadTopBranches();
        
        // Load system alerts
        this.loadSystemAlerts();
        
        // Load performance timeline data
        this.loadPerformanceTimelineData();
    }

    /**
     * Load top branches data
     */
    async loadTopBranches() {
        try {
            const response = await fetch('/Admin/Analytics/GetTopBranches?limit=5');
            const result = await response.json();
            
            if (result.success && result.data) {
                this.renderTopBranches(result.data);
            }
        } catch (error) {
            console.error('Failed to load top branches:', error);
            this.renderTopBranchesError();
        }
    }

    /**
     * Render top branches list
     */
    renderTopBranches(branches) {
        const container = document.getElementById('topBranchesList');
        if (!container) return;

        if (branches.length === 0) {
            container.innerHTML = `
                <div class="no-data">
                    <i class="fas fa-building"></i>
                    <p>No branch data available</p>
                </div>
            `;
            return;
        }

        const html = branches.map(branch => `
            <div class="branch-item">
                <div class="branch-rank">${branch.rankPosition}</div>
                <div class="branch-info">
                    <div class="branch-name">${branch.branchName}</div>
                    <div class="branch-code">${branch.branchCode}</div>
                </div>
                <div class="branch-metrics">
                    <div class="branch-count">${branch.registrationCount}</div>
                    <div class="branch-rate">${branch.successRate.toFixed(1)}%</div>
                </div>
            </div>
        `).join('');

        container.innerHTML = html;
    }

    /**
     * Render top branches error state
     */
    renderTopBranchesError() {
        const container = document.getElementById('topBranchesList');
        if (container) {
            container.innerHTML = `
                <div class="no-data">
                    <i class="fas fa-exclamation-triangle text-warning"></i>
                    <p>Unable to load branch data</p>
                </div>
            `;
        }
    }

    /**
     * Load system alerts
     */
    async loadSystemAlerts() {
        try {
            const response = await fetch('/Admin/Analytics/GetSystemAlerts');
            const result = await response.json();
            
            if (result.success && result.data) {
                this.renderSystemAlerts(result.data);
                this.updateAlertCount(result.data.length);
            }
        } catch (error) {
            console.error('Failed to load system alerts:', error);
            this.renderSystemAlertsError();
        }
    }

    /**
     * Render system alerts
     */
    renderSystemAlerts(alerts) {
        const container = document.getElementById('systemAlerts');
        if (!container) return;

        if (alerts.length === 0) {
            container.innerHTML = `
                <div class="no-data">
                    <i class="fas fa-check-circle text-success"></i>
                    <p>No active alerts. System is running smoothly.</p>
                </div>
            `;
            return;
        }

        const html = alerts.map(alert => {
            const severityClass = this.getSeverityClass(alert.severity);
            const severityIcon = this.getSeverityIcon(alert.severity);
            
            return `
                <div class="alert-item alert-${severityClass}">
                    <div class="alert-icon bg-${severityClass} text-white">
                        <i class="fas ${severityIcon}"></i>
                    </div>
                    <div class="alert-content">
                        <div class="alert-title">${alert.title}</div>
                        <div class="alert-description">${alert.description}</div>
                        <div class="alert-timestamp">${new Date(alert.detectedAt).toLocaleString()}</div>
                    </div>
                    <div class="alert-severity bg-${severityClass} text-white">
                        ${alert.severity}
                    </div>
                </div>
            `;
        }).join('');

        container.innerHTML = html;
    }

    /**
     * Render system alerts error state
     */
    renderSystemAlertsError() {
        const container = document.getElementById('systemAlerts');
        if (container) {
            container.innerHTML = `
                <div class="no-data">
                    <i class="fas fa-exclamation-triangle text-warning"></i>
                    <p>Unable to load system alerts</p>
                </div>
            `;
        }
    }

    /**
     * Update alert count badge
     */
    updateAlertCount(count) {
        const element = document.getElementById('alertCount');
        if (element) {
            element.textContent = count;
            element.className = `badge ${count > 0 ? 'bg-warning' : 'bg-success'}`;
        }
    }

    /**
     * Load performance timeline data
     */
    async loadPerformanceTimelineData() {
        // This would typically load real performance data
        // For now, we'll generate sample data
        if (this.charts.performanceTimeline) {
            const chart = this.charts.performanceTimeline;
            const labels = [];
            const successRates = [];
            const responseTimes = [];
            
            // Generate last 7 days of sample data
            for (let i = 6; i >= 0; i--) {
                const date = new Date();
                date.setDate(date.getDate() - i);
                labels.push(date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }));
                successRates.push(95 + Math.random() * 4); // 95-99%
                responseTimes.push(700 + Math.random() * 300); // 700-1000ms
            }
            
            chart.data.labels = labels;
            chart.data.datasets[0].data = successRates;
            chart.data.datasets[1].data = responseTimes;
            chart.update('none');
        }
    }

    /**
     * Setup event listeners
     */
    setupEventListeners() {
        // Period selection for OTAC trends
        document.querySelectorAll('[data-period]').forEach(button => {
            button.addEventListener('click', (e) => {
                // Update active button
                document.querySelectorAll('[data-period]').forEach(btn => btn.classList.remove('active'));
                e.target.classList.add('active');
                
                // Load trends for selected period
                const days = parseInt(e.target.getAttribute('data-period'));
                this.loadOtacTrends(days);
            });
        });

        // Manual refresh button (if exists)
        const refreshButton = document.getElementById('manualRefresh');
        if (refreshButton) {
            refreshButton.addEventListener('click', () => this.refreshMetrics());
        }

        // Pause/resume auto-refresh on visibility change
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                console.log('Page hidden, pausing auto-refresh');
                this.stopAutoRefresh();
            } else {
                console.log('Page visible, resuming auto-refresh');
                this.startAutoRefresh();
                this.refreshMetrics(); // Immediate refresh when page becomes visible
            }
        });
    }

    /**
     * Get CSS class for alert severity
     */
    getSeverityClass(severity) {
        const severityMap = {
            1: 'info',    // Info
            2: 'warning', // Warning
            3: 'error',   // Error
            4: 'critical' // Critical
        };
        return severityMap[severity] || 'info';
    }

    /**
     * Get icon for alert severity
     */
    getSeverityIcon(severity) {
        const iconMap = {
            1: 'fa-info-circle',         // Info
            2: 'fa-exclamation-triangle', // Warning
            3: 'fa-times-circle',        // Error
            4: 'fa-exclamation-circle'   // Critical
        };
        return iconMap[severity] || 'fa-info-circle';
    }

    /**
     * Cleanup when dashboard is destroyed
     */
    destroy() {
        this.stopAutoRefresh();
        
        // Destroy all charts
        Object.values(this.charts).forEach(chart => {
            if (chart && typeof chart.destroy === 'function') {
                chart.destroy();
            }
        });
        
        this.charts = {};
        console.log('Dashboard destroyed');
    }
}

// Initialize dashboard when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    // Dashboard will be initialized by the View's script section
    console.log('Analytics Dashboard JavaScript loaded');
});

// Export for use in other scripts
window.RealTimeDashboard = RealTimeDashboard;