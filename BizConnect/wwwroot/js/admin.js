/**
 * BizConnect Admin Dashboard JavaScript
 * Advanced admin interface controller with real-time updates and management features
 */

class AdminUI {
    constructor() {
        this.config = {
            updateInterval: 30000, // 30 seconds
            maxRetries: 3,
            apiEndpoint: '/api/admin',
            widgets: new Map(),
            dashboardData: {}
        };
        this.init();
    }

    /**
     * Initialize admin UI system
     */
    init() {
        // Sidebar is now handled by AdminLayout module
        this.setupDashboardWidgets();
        this.setupRealTimeUpdates();
        this.setupDataTables();
        this.setupOtacManagement();
        this.setupExcelOperations();
        this.setupAdminInteractions();
        this.setupSystemMonitoring();
        
        console.log('🔧 Admin UI System Initialized');
    }

    // =================
    // SIDEBAR MANAGEMENT
    // =================
    // Note: Sidebar functionality is now handled by AdminLayout module

    // =================
    // DASHBOARD WIDGETS
    // =================

    setupDashboardWidgets() {
        this.initializeStatWidgets();
        this.initializeChartWidgets();
        this.initializeActivityWidgets();
        this.initializeSystemHealthWidget();
    }

    initializeStatWidgets() {
        // Animate counters
        document.querySelectorAll('.stats-number[data-target]').forEach(counter => {
            const target = parseInt(counter.dataset.target);
            const duration = parseInt(counter.dataset.duration) || 2000;
            
            // Use intersection observer for better performance
            const observer = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        this.animateCounter(counter, 0, target, duration);
                        observer.unobserve(counter);
                    }
                });
            }, { threshold: 0.5 });
            
            observer.observe(counter);
        });

        // Setup refresh buttons for widgets
        document.querySelectorAll('[data-widget-refresh]').forEach(btn => {
            btn.addEventListener('click', () => {
                const widgetId = btn.dataset.widgetRefresh;
                this.refreshWidget(widgetId);
            });
        });
    }

    animateCounter(element, start, end, duration) {
        const startTime = Date.now();
        const range = end - start;

        const updateCounter = () => {
            const elapsed = Date.now() - startTime;
            const progress = Math.min(elapsed / duration, 1);
            
            // Easing function
            const easeOutQuart = 1 - Math.pow(1 - progress, 4);
            const current = Math.round(start + range * easeOutQuart);
            
            element.textContent = current.toLocaleString();
            
            if (progress < 1) {
                requestAnimationFrame(updateCounter);
            }
        };

        requestAnimationFrame(updateCounter);
    }

    initializeChartWidgets() {
        // Initialize Chart.js charts if available
        if (typeof Chart !== 'undefined') {
            this.setupDashboardCharts();
        }
    }

    setupDashboardCharts() {
        // Example: Registration trends chart
        const ctx = document.querySelector('#registrationTrendChart');
        if (ctx) {
            new Chart(ctx, {
                type: 'line',
                data: {
                    labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun'],
                    datasets: [{
                        label: 'ODD Registrations',
                        data: [65, 59, 80, 81, 56, 55],
                        borderColor: 'var(--kbank-green)',
                        backgroundColor: 'rgba(76, 175, 80, 0.1)',
                        tension: 0.4
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            display: false
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            grid: {
                                color: 'rgba(0, 0, 0, 0.05)'
                            }
                        },
                        x: {
                            grid: {
                                display: false
                            }
                        }
                    }
                }
            });
        }
    }

    initializeActivityWidgets() {
        // Setup real-time activity feed
        this.setupActivityFeed();
    }

    setupActivityFeed() {
        const activityContainer = document.querySelector('.recent-activity');
        if (!activityContainer) return;

        // Fetch recent activities
        this.fetchRecentActivities();
    }

    async fetchRecentActivities() {
        try {
            const response = await fetch('/api/admin/activities/recent');
            const activities = await response.json();
            this.updateActivityFeed(activities);
        } catch (error) {
            console.warn('Failed to fetch recent activities:', error);
        }
    }

    updateActivityFeed(activities) {
        const container = document.querySelector('.recent-activity');
        if (!container || !activities?.length) return;

        container.innerHTML = activities.map(activity => `
            <div class="activity-item d-flex align-items-center p-3 mb-3 glass rounded-3">
                <div class="flex-shrink-0 me-3">
                    <div class="status-indicator status-${activity.type?.toLowerCase()}">
                        <div class="status-dot"></div>
                        <span>${activity.status}</span>
                    </div>
                </div>
                <div class="flex-grow-1">
                    <div class="fw-semibold text-dark mb-1">${activity.description}</div>
                    <div class="text-muted small">
                        <i class="fas fa-clock me-1"></i>${this.formatDateTime(activity.timestamp)}
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
            </div>
        `).join('');
    }

    initializeSystemHealthWidget() {
        this.updateSystemHealth();
        
        // Setup periodic health checks
        setInterval(() => {
            this.updateSystemHealth();
        }, 60000); // Every minute
    }

    async updateSystemHealth() {
        try {
            const response = await fetch('/api/admin/system-health');
            const health = await response.json();
            this.displaySystemHealth(health);
        } catch (error) {
            console.warn('System health check failed:', error);
            this.displaySystemHealth({ status: 'error' });
        }
    }

    displaySystemHealth(health) {
        const healthElements = document.querySelectorAll('[data-health-service]');
        
        healthElements.forEach(element => {
            const service = element.dataset.healthService;
            const status = health[service] || { status: 'unknown' };
            
            const statusElement = element.querySelector('.status-indicator');
            if (statusElement) {
                statusElement.className = `status-indicator status-${status.status}`;
                statusElement.textContent = status.status.charAt(0).toUpperCase() + status.status.slice(1);
            }
        });
    }

    // =================
    // REAL-TIME UPDATES
    // =================

    setupRealTimeUpdates() {
        // Start periodic updates
        this.startPeriodicUpdates();
        
        // Setup WebSocket connection if available
        this.setupWebSocketConnection();
    }

    startPeriodicUpdates() {
        setInterval(() => {
            this.updateDashboardStats();
        }, this.config.updateInterval);
    }

    async updateDashboardStats() {
        try {
            const response = await fetch('/api/admin/dashboard/stats');
            const stats = await response.json();
            this.updateStatWidgets(stats);
        } catch (error) {
            console.warn('Failed to update dashboard stats:', error);
        }
    }

    updateStatWidgets(stats) {
        Object.entries(stats).forEach(([key, value]) => {
            const element = document.querySelector(`[data-stat="${key}"]`);
            if (element) {
                const numberElement = element.querySelector('.stats-number');
                if (numberElement) {
                    const currentValue = parseInt(numberElement.textContent) || 0;
                    if (currentValue !== value) {
                        this.animateCounter(numberElement, currentValue, value, 1000);
                    }
                }
            }
        });
    }

    setupWebSocketConnection() {
        // TODO: Implement WebSocket for real-time updates
        // This would be used for instant notifications of new registrations, etc.
    }

    // =================
    // DATA TABLES
    // =================

    setupDataTables() {
        document.querySelectorAll('.admin-table, .table-modern').forEach(table => {
            this.enhanceTable(table);
        });
    }

    enhanceTable(table) {
        // Add sorting functionality
        this.addTableSorting(table);
        
        // Add filtering functionality
        this.addTableFiltering(table);
        
        // Add pagination if needed
        this.addTablePagination(table);
        
        // Add row selection
        this.addRowSelection(table);
    }

    addTableSorting(table) {
        const headers = table.querySelectorAll('th[data-sort]');
        
        headers.forEach(header => {
            header.style.cursor = 'pointer';
            header.innerHTML += ' <i class="fas fa-sort text-muted ms-1"></i>';
            
            header.addEventListener('click', () => {
                this.sortTable(table, header.dataset.sort, header);
            });
        });
    }

    sortTable(table, column, headerElement) {
        const tbody = table.querySelector('tbody');
        const rows = Array.from(tbody.querySelectorAll('tr'));
        
        // Determine sort direction
        const isAscending = headerElement.classList.contains('sort-asc');
        const sortDirection = isAscending ? 'desc' : 'asc';
        
        // Clear all sort classes
        table.querySelectorAll('th').forEach(th => {
            th.classList.remove('sort-asc', 'sort-desc');
            const icon = th.querySelector('i');
            if (icon) {
                icon.className = 'fas fa-sort text-muted ms-1';
            }
        });
        
        // Set current sort class and icon
        headerElement.classList.add(`sort-${sortDirection}`);
        const icon = headerElement.querySelector('i');
        if (icon) {
            icon.className = `fas fa-sort-${sortDirection === 'asc' ? 'up' : 'down'} text-primary ms-1`;
        }
        
        // Sort rows
        const columnIndex = Array.from(headerElement.parentNode.children).indexOf(headerElement);
        
        rows.sort((a, b) => {
            const aValue = a.children[columnIndex]?.textContent.trim() || '';
            const bValue = b.children[columnIndex]?.textContent.trim() || '';
            
            // Try numeric comparison first
            const aNum = parseFloat(aValue);
            const bNum = parseFloat(bValue);
            
            if (!isNaN(aNum) && !isNaN(bNum)) {
                return sortDirection === 'asc' ? aNum - bNum : bNum - aNum;
            }
            
            // Text comparison
            return sortDirection === 'asc' 
                ? aValue.localeCompare(bValue)
                : bValue.localeCompare(aValue);
        });
        
        // Reorder DOM elements
        rows.forEach(row => tbody.appendChild(row));
    }

    addTableFiltering(table) {
        const searchInput = table.closest('.table-container')?.querySelector('.table-search');
        if (!searchInput) return;

        const filterRows = (searchTerm) => {
            const rows = table.querySelectorAll('tbody tr');
            const term = searchTerm.toLowerCase();

            rows.forEach(row => {
                const text = row.textContent.toLowerCase();
                row.style.display = text.includes(term) ? '' : 'none';
            });

            this.updateTableInfo(table);
        };

        searchInput.addEventListener('input', (e) => {
            filterRows(e.target.value);
        });
    }

    addTablePagination(table) {
        // Simple pagination implementation
        const rowsPerPage = 10;
        const rows = table.querySelectorAll('tbody tr');
        
        if (rows.length <= rowsPerPage) return;

        let currentPage = 1;
        const totalPages = Math.ceil(rows.length / rowsPerPage);

        const showPage = (page) => {
            const start = (page - 1) * rowsPerPage;
            const end = start + rowsPerPage;

            rows.forEach((row, index) => {
                row.style.display = (index >= start && index < end) ? '' : 'none';
            });

            currentPage = page;
            this.updatePaginationUI(table, currentPage, totalPages);
        };

        // Create pagination UI
        this.createPaginationUI(table, totalPages, showPage);
        
        // Show first page
        showPage(1);
    }

    addRowSelection(table) {
        // Add master checkbox in header if needed
        const hasCheckboxes = table.querySelector('tbody input[type="checkbox"]');
        if (!hasCheckboxes) return;

        const thead = table.querySelector('thead tr');
        if (thead && !thead.querySelector('input[type="checkbox"]')) {
            const th = document.createElement('th');
            th.innerHTML = '<input type="checkbox" class="form-check-input master-checkbox">';
            thead.insertBefore(th, thead.firstChild);
        }

        const masterCheckbox = table.querySelector('.master-checkbox');
        const rowCheckboxes = table.querySelectorAll('tbody input[type="checkbox"]');

        if (masterCheckbox) {
            masterCheckbox.addEventListener('change', () => {
                rowCheckboxes.forEach(checkbox => {
                    checkbox.checked = masterCheckbox.checked;
                    this.toggleRowSelection(checkbox.closest('tr'), checkbox.checked);
                });
            });
        }

        rowCheckboxes.forEach(checkbox => {
            checkbox.addEventListener('change', () => {
                this.toggleRowSelection(checkbox.closest('tr'), checkbox.checked);
                this.updateMasterCheckbox(table);
            });
        });
    }

    toggleRowSelection(row, selected) {
        row.classList.toggle('selected', selected);
        
        // Dispatch selection event
        const event = new CustomEvent('rowSelectionChanged', {
            detail: { row, selected }
        });
        row.dispatchEvent(event);
    }

    updateMasterCheckbox(table) {
        const masterCheckbox = table.querySelector('.master-checkbox');
        const rowCheckboxes = table.querySelectorAll('tbody input[type="checkbox"]');
        
        if (!masterCheckbox) return;

        const checkedCount = Array.from(rowCheckboxes).filter(cb => cb.checked).length;
        
        masterCheckbox.checked = checkedCount === rowCheckboxes.length;
        masterCheckbox.indeterminate = checkedCount > 0 && checkedCount < rowCheckboxes.length;
    }

    // =================
    // OTAC MANAGEMENT
    // =================

    setupOtacManagement() {
        this.setupOtacGeneration();
        this.setupOtacValidation();
        this.setupOtacBulkOperations();
    }

    setupOtacGeneration() {
        const generateBtn = document.querySelector('[data-action="generate-otac"]');
        if (generateBtn) {
            generateBtn.addEventListener('click', () => this.generateOtacCode());
        }

        // Setup bulk generation
        const bulkGenerateBtn = document.querySelector('[data-action="bulk-generate-otac"]');
        if (bulkGenerateBtn) {
            bulkGenerateBtn.addEventListener('click', () => this.showBulkGenerationModal());
        }
    }

    async generateOtacCode() {
        try {
            this.showLoadingState('Generating OTAC...');
            
            const response = await fetch('/api/admin/otac/generate', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': this.getCSRFToken()
                }
            });

            const result = await response.json();
            
            if (response.ok) {
                this.displayGeneratedOtac(result.otacCode);
                ModernUI.showNotification('success', 'OTAC Generated', `New OTAC code: ${result.otacCode}`);
            } else {
                throw new Error(result.message || 'Failed to generate OTAC');
            }
        } catch (error) {
            console.error('OTAC generation failed:', error);
            ModernUI.showNotification('error', 'Generation Failed', error.message);
        } finally {
            this.hideLoadingState();
        }
    }

    displayGeneratedOtac(otacCode) {
        const modal = document.querySelector('#otacDisplayModal');
        if (modal) {
            const codeElement = modal.querySelector('.otac-code-display');
            if (codeElement) {
                codeElement.textContent = otacCode;
            }
            
            // Show modal
            const bsModal = new bootstrap.Modal(modal);
            bsModal.show();
        }
    }

    setupOtacValidation() {
        // Real-time OTAC validation in forms
        document.querySelectorAll('input[data-otac-validation]').forEach(input => {
            input.addEventListener('input', (e) => {
                this.validateOtacFormat(e.target);
            });
        });
    }

    validateOtacFormat(input) {
        const value = input.value.toUpperCase().replace(/[^A-Z0-9]/g, '');
        input.value = value;

        const isValid = /^[A-Z0-9]{8}$/.test(value);
        
        input.classList.toggle('is-valid', isValid && value.length === 8);
        input.classList.toggle('is-invalid', !isValid && value.length > 0);
    }

    setupOtacBulkOperations() {
        // Setup bulk OTAC operations like export, deactivation, etc.
        const bulkActions = document.querySelectorAll('[data-bulk-action]');
        
        bulkActions.forEach(action => {
            action.addEventListener('click', () => {
                const actionType = action.dataset.bulkAction;
                this.performBulkAction(actionType);
            });
        });
    }

    // =================
    // EXCEL OPERATIONS
    // =================

    setupExcelOperations() {
        this.setupExcelImport();
        this.setupExcelExport();
        this.setupTemplateDownload();
    }

    setupExcelImport() {
        const importBtn = document.querySelector('[data-action="import-excel"]');
        const fileInput = document.querySelector('#excelFileInput');
        
        if (importBtn) {
            importBtn.addEventListener('click', () => {
                fileInput?.click();
            });
        }

        if (fileInput) {
            fileInput.addEventListener('change', (e) => {
                const file = e.target.files[0];
                if (file) {
                    this.processExcelImport(file);
                }
            });
        }
    }

    async processExcelImport(file) {
        const formData = new FormData();
        formData.append('file', file);

        try {
            this.showLoadingState('Processing Excel file...');
            
            const response = await fetch('/api/admin/excel/import', {
                method: 'POST',
                headers: {
                    'X-CSRF-TOKEN': this.getCSRFToken()
                },
                body: formData
            });

            const result = await response.json();
            
            if (response.ok) {
                ModernUI.showNotification('success', 'Import Successful', 
                    `${result.recordsProcessed} records imported successfully`);
                this.refreshCurrentPage();
            } else {
                throw new Error(result.message || 'Import failed');
            }
        } catch (error) {
            console.error('Excel import failed:', error);
            ModernUI.showNotification('error', 'Import Failed', error.message);
        } finally {
            this.hideLoadingState();
        }
    }

    setupExcelExport() {
        document.querySelectorAll('[data-action="export-excel"]').forEach(btn => {
            btn.addEventListener('click', () => {
                const exportType = btn.dataset.exportType || 'all';
                this.exportToExcel(exportType);
            });
        });
    }

    async exportToExcel(type) {
        try {
            this.showLoadingState('Preparing export...');
            
            const response = await fetch(`/api/admin/excel/export/${type}`, {
                method: 'GET',
                headers: {
                    'X-CSRF-TOKEN': this.getCSRFToken()
                }
            });

            if (response.ok) {
                const blob = await response.blob();
                const url = window.URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = `bizconnect-${type}-${new Date().toISOString().split('T')[0]}.xlsx`;
                document.body.appendChild(a);
                a.click();
                window.URL.revokeObjectURL(url);
                document.body.removeChild(a);
                
                ModernUI.showNotification('success', 'Export Successful', 'File downloaded successfully');
            } else {
                throw new Error('Export failed');
            }
        } catch (error) {
            console.error('Excel export failed:', error);
            ModernUI.showNotification('error', 'Export Failed', error.message);
        } finally {
            this.hideLoadingState();
        }
    }

    // =================
    // ADMIN INTERACTIONS
    // =================

    setupAdminInteractions() {
        this.setupQuickActions();
        this.setupBulkOperations();
        this.setupConfirmationDialogs();
        this.setupKeyboardShortcuts();
    }

    setupQuickActions() {
        document.querySelectorAll('[data-quick-action]').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                const action = btn.dataset.quickAction;
                this.performQuickAction(action, btn);
            });
        });
    }

    async performQuickAction(action, button) {
        const actions = {
            'refresh-dashboard': () => this.refreshDashboard(),
            'generate-report': () => this.generateReport(),
            'clear-cache': () => this.clearSystemCache(),
            'backup-data': () => this.initiateDataBackup()
        };

        const actionFunction = actions[action];
        if (actionFunction) {
            try {
                ModernUI.setButtonLoading(button, true);
                await actionFunction();
            } catch (error) {
                ModernUI.showNotification('error', 'Action Failed', error.message);
            } finally {
                ModernUI.setButtonLoading(button, false);
            }
        }
    }

    setupConfirmationDialogs() {
        document.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('[data-confirm-delete]');
            if (deleteBtn) {
                e.preventDefault();
                this.showDeleteConfirmation(deleteBtn);
            }
        });
    }

    showDeleteConfirmation(button) {
        const itemName = button.dataset.itemName || 'this item';
        const message = `Are you sure you want to delete ${itemName}? This action cannot be undone.`;
        
        if (confirm(message)) {
            this.performDelete(button.dataset.deleteUrl || button.href);
        }
    }

    setupKeyboardShortcuts() {
        document.addEventListener('keydown', (e) => {
            // Ctrl+R or Cmd+R: Refresh dashboard
            if ((e.ctrlKey || e.metaKey) && e.key === 'r') {
                e.preventDefault();
                this.refreshDashboard();
            }
            
            // Ctrl+N or Cmd+N: New item (context dependent)
            if ((e.ctrlKey || e.metaKey) && e.key === 'n') {
                e.preventDefault();
                this.handleNewItemShortcut();
            }
        });
    }

    // =================
    // SYSTEM MONITORING
    // =================

    setupSystemMonitoring() {
        this.monitorPerformance();
        this.setupErrorReporting();
        this.trackUserActivity();
    }

    monitorPerformance() {
        // Monitor page load times
        if ('performance' in window) {
            window.addEventListener('load', () => {
                setTimeout(() => {
                    const perfData = performance.getEntriesByType('navigation')[0];
                    const metrics = {
                        loadTime: Math.round(perfData.loadEventEnd - perfData.loadEventStart),
                        domReady: Math.round(perfData.domContentLoadedEventEnd - perfData.domContentLoadedEventStart),
                        firstPaint: Math.round(perfData.responseStart - perfData.fetchStart)
                    };
                    
                    // Send to analytics if needed
                    this.reportPerformanceMetrics(metrics);
                }, 1000);
            });
        }
    }

    setupErrorReporting() {
        window.addEventListener('error', (e) => {
            this.reportError({
                message: e.message,
                filename: e.filename,
                lineno: e.lineno,
                colno: e.colno,
                stack: e.error?.stack
            });
        });

        window.addEventListener('unhandledrejection', (e) => {
            this.reportError({
                message: 'Unhandled Promise Rejection',
                error: e.reason
            });
        });
    }

    // =================
    // UTILITY METHODS
    // =================

    showLoadingState(message = 'Loading...') {
        let overlay = document.querySelector('.admin-loading-overlay');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.className = 'admin-loading-overlay loading-overlay-modern';
            overlay.innerHTML = `
                <div class="text-center">
                    <div class="spinner-modern mb-3"></div>
                    <div class="loading-message">${message}</div>
                </div>
            `;
            document.body.appendChild(overlay);
        } else {
            overlay.querySelector('.loading-message').textContent = message;
            overlay.style.display = 'flex';
        }
    }

    hideLoadingState() {
        const overlay = document.querySelector('.admin-loading-overlay');
        if (overlay) {
            overlay.style.display = 'none';
        }
    }

    getCSRFToken() {
        return document.querySelector('meta[name="csrf-token"]')?.content || 
               document.querySelector('input[name="__RequestVerificationToken"]')?.value;
    }

    formatDateTime(date) {
        return new Date(date).toLocaleString();
    }

    async refreshCurrentPage() {
        window.location.reload();
    }

    async refreshWidget(widgetId) {
        try {
            const response = await fetch(`/api/admin/widgets/${widgetId}/refresh`);
            const html = await response.text();
            
            const widget = document.querySelector(`[data-widget-id="${widgetId}"]`);
            if (widget) {
                widget.innerHTML = html;
                ModernUI.showNotification('success', 'Widget Updated', 'Widget refreshed successfully');
            }
        } catch (error) {
            ModernUI.showNotification('error', 'Refresh Failed', 'Failed to refresh widget');
        }
    }

    reportPerformanceMetrics(metrics) {
        // Send to server or analytics service
        console.log('Performance Metrics:', metrics);
    }

    reportError(error) {
        // Send error to logging service
        console.error('Client Error:', error);
    }
}

// Initialize Admin UI
const adminUI = new AdminUI();

// Export for global use
window.AdminUI = adminUI;
window.adminUI = adminUI;