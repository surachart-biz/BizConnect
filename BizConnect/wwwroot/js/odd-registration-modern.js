/**
 * ODD Registration Modern UI - Enhanced Interactive Features
 * Provides modern, accessible interactions for the ODD Registration interface
 */

(function() {
    'use strict';

    // ==========================================================================
    // Configuration & Constants
    // ==========================================================================
    const CONFIG = {
        SEARCH_DEBOUNCE_DELAY: 500,
        ALERT_AUTO_DISMISS_DELAY: 5000,
        MOBILE_BREAKPOINT: 768,
        ANIMATION_DURATION: 300,
        // Real-time Updates Configuration
        POLLING_INITIAL_INTERVAL: 5000,    // 5 seconds
        POLLING_MAX_INTERVAL: 60000,       // 1 minute
        POLLING_BACKOFF_MULTIPLIER: 1.5,
        VISIBILITY_SLOW_INTERVAL: 30000,   // 30 seconds when page not visible
        ERROR_RETRY_BASE_DELAY: 2000       // 2 seconds base delay for retries
    };

    const SELECTORS = {
        selectAll: '#selectAll',
        rowCheckboxes: '.row-checkbox',
        filterForm: '#filterForm',
        searchInput: '#search',
        statusSelect: '#status',
        pageSizeSelect: '#pageSize',
        tableContainer: '.odd-table-container',
        mobileCards: '.odd-mobile-cards',
        tableResponsive: '.odd-table-responsive',
        loadingOverlay: '.odd-loading-overlay',
        otacCode: '.odd-otac-code'
    };

    // ==========================================================================
    // State Management
    // ==========================================================================
    const state = {
        isLoading: false,
        selectedRows: new Set(),
        searchTimeout: null,
        currentView: 'desktop', // 'desktop' or 'mobile'
        // Real-time Updates State (always enabled)
        pollingInterval: CONFIG.POLLING_INITIAL_INTERVAL,
        pollingTimeoutId: null,
        lastCursor: null,
        consecutiveErrors: 0,
        isPageVisible: true,
        lastUpdateTimestamp: null,
        currentFilters: {
            status: '',
            search: '',
            pageSize: 10
        },
        // Performance optimization cache
        dataCache: new Map(),
        lastDataHash: null,
        batchUpdateQueue: [],
        isProcessingBatch: false
    };

    // ==========================================================================
    // Utility Functions
    // ==========================================================================
    
    /**
     * Debounce function to limit rapid function calls
     */
    function debounce(func, delay) {
        let timeoutId;
        return function (...args) {
            clearTimeout(timeoutId);
            timeoutId = setTimeout(() => func.apply(this, args), delay);
        };
    }

    /**
     * Show modern alert with auto-dismiss
     */
    function showAlert(type, message, options = {}) {
        const alertId = `alert-${Date.now()}`;
        const duration = options.duration || CONFIG.ALERT_AUTO_DISMISS_DELAY;
        const position = options.position || 'top-right';
        
        const alertHtml = `
            <div id="${alertId}" class="alert alert-${type} alert-dismissible fade show odd-alert-modern" 
                 style="position: fixed; ${getAlertPosition(position)}; z-index: 1050; min-width: 320px; max-width: 500px;">
                <div class="d-flex align-items-center">
                    ${getAlertIcon(type)}
                    <div class="flex-grow-1 ms-2">${message}</div>
                    <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', alertHtml);

        // Auto-dismiss
        if (duration > 0) {
            setTimeout(() => {
                const alertElement = document.getElementById(alertId);
                if (alertElement) {
                    alertElement.remove();
                }
            }, duration);
        }

        return alertId;
    }

    function getAlertPosition(position) {
        const positions = {
            'top-right': 'top: 20px; right: 20px;',
            'top-left': 'top: 20px; left: 20px;',
            'bottom-right': 'bottom: 20px; right: 20px;',
            'bottom-left': 'bottom: 20px; left: 20px;',
            'center': 'top: 50%; left: 50%; transform: translate(-50%, -50%);'
        };
        return positions[position] || positions['top-right'];
    }

    function getAlertIcon(type) {
        const icons = {
            success: '<i class="fas fa-check-circle text-success"></i>',
            danger: '<i class="fas fa-exclamation-triangle text-danger"></i>',
            warning: '<i class="fas fa-exclamation-triangle text-warning"></i>',
            info: '<i class="fas fa-info-circle text-info"></i>',
            primary: '<i class="fas fa-info-circle text-primary"></i>'
        };
        return icons[type] || icons['info'];
    }

    /**
     * Show/hide loading overlay
     */
    function setLoadingState(isLoading, message = 'กำลังประมวลผล...') {
        state.isLoading = isLoading;
        
        if (isLoading) {
            showLoadingOverlay(message);
        } else {
            hideLoadingOverlay();
        }
    }

    function showLoadingOverlay(message) {
        const existingOverlay = document.querySelector(SELECTORS.loadingOverlay);
        if (existingOverlay) return;

        const overlay = document.createElement('div');
        overlay.className = 'odd-loading-overlay';
        overlay.innerHTML = `
            <div class="text-center">
                <div class="odd-loading-spinner"></div>
                <div class="odd-loading-text mt-2">${message}</div>
            </div>
        `;

        const container = document.querySelector(SELECTORS.tableContainer);
        if (container) {
            container.style.position = 'relative';
            container.appendChild(overlay);
        }
    }

    function hideLoadingOverlay() {
        const overlay = document.querySelector(SELECTORS.loadingOverlay);
        if (overlay) {
            overlay.remove();
        }
    }

    /**
     * Responsive view management
     */
    function updateResponsiveView() {
        const isMobile = window.innerWidth <= CONFIG.MOBILE_BREAKPOINT;
        const mobileCards = document.querySelector(SELECTORS.mobileCards);
        const tableResponsive = document.querySelector(SELECTORS.tableResponsive);

        if (isMobile) {
            if (tableResponsive) tableResponsive.style.display = 'none';
            if (mobileCards) mobileCards.classList.remove('d-none');
            state.currentView = 'mobile';
        } else {
            if (tableResponsive) tableResponsive.style.display = 'block';
            if (mobileCards) mobileCards.classList.add('d-none');
            state.currentView = 'desktop';
        }
    }

    // ==========================================================================
    // Performance Optimization System
    // ==========================================================================
    
    /**
     * Generate a hash for data comparison to detect changes
     */
    function generateDataHash(data) {
        if (!data || !Array.isArray(data)) return '';
        
        // Create a simple hash based on data content
        const hashString = data.map(item => 
            `${item.id}-${item.status}-${item.updatedAt || item.createdAt}`
        ).join('|');
        
        return btoa(hashString).replace(/[^a-zA-Z0-9]/g, '').substring(0, 16);
    }

    /**
     * Check if data has actually changed to avoid unnecessary updates
     */
    function hasDataChanged(newData) {
        const newHash = generateDataHash(newData);
        const hasChanged = newHash !== state.lastDataHash;
        
        if (hasChanged) {
            state.lastDataHash = newHash;
        }
        
        return hasChanged;
    }

    /**
     * Cache data for performance optimization
     */
    function cacheData(key, data, ttl = 60000) { // 1 minute TTL by default
        const cacheEntry = {
            data: data,
            timestamp: Date.now(),
            ttl: ttl
        };
        
        state.dataCache.set(key, cacheEntry);
        
        // Clean up expired entries periodically
        if (state.dataCache.size > 100) { // Limit cache size
            cleanupCache();
        }
    }

    /**
     * Retrieve cached data if still valid
     */
    function getCachedData(key) {
        const entry = state.dataCache.get(key);
        
        if (!entry) {
            return null;
        }
        
        // Check if entry has expired
        if (Date.now() - entry.timestamp > entry.ttl) {
            state.dataCache.delete(key);
            return null;
        }
        
        return entry.data;
    }

    /**
     * Clean up expired cache entries
     */
    function cleanupCache() {
        const now = Date.now();
        
        for (const [key, entry] of state.dataCache.entries()) {
            if (now - entry.timestamp > entry.ttl) {
                state.dataCache.delete(key);
            }
        }
    }

    /**
     * Add update to batch queue for processing
     */
    function queueUpdate(updateType, data) {
        state.batchUpdateQueue.push({
            type: updateType,
            data: data,
            timestamp: Date.now()
        });
        
        // Process batch if not already processing
        if (!state.isProcessingBatch) {
            processBatchUpdates();
        }
    }

    /**
     * Process batched updates for better performance
     */
    function processBatchUpdates() {
        if (state.batchUpdateQueue.length === 0) {
            return;
        }
        
        state.isProcessingBatch = true;
        
        // Group updates by type for efficient processing
        const groupedUpdates = state.batchUpdateQueue.reduce((groups, update) => {
            const type = update.type;
            if (!groups[type]) {
                groups[type] = [];
            }
            groups[type].push(update);
            return groups;
        }, {});
        
        // Process each group
        requestAnimationFrame(() => {
            try {
                for (const [type, updates] of Object.entries(groupedUpdates)) {
                    switch (type) {
                        case 'newRows':
                            processBatchNewRows(updates);
                            break;
                        case 'updateRows':
                            processBatchRowUpdates(updates);
                            break;
                        case 'removeRows':
                            processBatchRowRemovals(updates);
                            break;
                    }
                }
                
                // Clear processed updates
                state.batchUpdateQueue = [];
            } catch (error) {
                console.error('Error processing batch updates:', error);
            } finally {
                state.isProcessingBatch = false;
            }
        });
    }

    /**
     * Process batch of new rows efficiently
     */
    function processBatchNewRows(updates) {
        const tableBody = document.querySelector('.odd-table tbody');
        if (!tableBody) return;
        
        const fragment = document.createDocumentFragment();
        const itemsToAdd = updates.map(u => u.data).flat();
        
        itemsToAdd.forEach(item => {
            generateRowHtml(item).then(rowHtml => {
                const tempContainer = document.createElement('div');
                tempContainer.innerHTML = rowHtml;
                const newRow = tempContainer.firstElementChild;
                newRow.classList.add('odd-row-new', 'odd-fade-in');
                fragment.appendChild(newRow);
            });
        });
        
        if (fragment.children.length > 0) {
            tableBody.insertBefore(fragment, tableBody.firstChild);
        }
    }

    /**
     * Process batch of row updates efficiently  
     */
    function processBatchRowUpdates(updates) {
        updates.forEach(update => {
            update.data.forEach(item => {
                updateExistingRow(item);
            });
        });
    }

    /**
     * Process batch of row removals efficiently
     */
    function processBatchRowRemovals(updates) {
        const idsToRemove = updates.map(u => u.data).flat();
        
        idsToRemove.forEach(id => {
            const row = findRowById(id);
            if (row) {
                row.classList.add('odd-fade-out');
                setTimeout(() => {
                    if (row.parentNode) {
                        row.remove();
                    }
                }, CONFIG.ANIMATION_DURATION);
            }
        });
    }

    /**
     * Optimize polling interval based on activity and data frequency
     */
    function optimizePollingInterval() {
        const now = Date.now();
        const timeSinceLastUpdate = state.lastUpdateTimestamp ? 
            now - new Date(state.lastUpdateTimestamp).getTime() : Infinity;
        
        // If no updates for a while, slow down polling
        if (timeSinceLastUpdate > 5 * 60 * 1000) { // 5 minutes
            state.pollingInterval = Math.min(
                state.pollingInterval * 1.2,
                CONFIG.POLLING_MAX_INTERVAL
            );
        } else if (timeSinceLastUpdate < 30 * 1000) { // 30 seconds
            // Recent activity, speed up polling
            state.pollingInterval = Math.max(
                CONFIG.POLLING_INITIAL_INTERVAL,
                state.pollingInterval * 0.8
            );
        }
        
        console.log(`Optimized polling interval to ${state.pollingInterval}ms`);
    }

    /**
     * Throttle DOM updates to improve performance
     */
    function throttle(func, limit) {
        let inThrottle;
        return function() {
            const args = arguments;
            const context = this;
            if (!inThrottle) {
                func.apply(context, args);
                inThrottle = true;
                setTimeout(() => inThrottle = false, limit);
            }
        };
    }

    /**
     * Debounced table update to prevent excessive redraws
     */
    const debouncedTableUpdate = debounce((data, stats) => {
        updateTableWithNewData(data, stats);
    }, 100);

    /**
     * Memory optimization: Clear old DOM elements and event listeners
     */
    function cleanupMemory() {
        // Remove old notification elements
        const oldAlerts = document.querySelectorAll('.odd-alert-modern');
        if (oldAlerts.length > 5) { // Keep only last 5 alerts
            for (let i = 0; i < oldAlerts.length - 5; i++) {
                oldAlerts[i].remove();
            }
        }
        
        // Clear cache periodically
        if (state.dataCache.size > 50) {
            cleanupCache();
        }
        
        // Force garbage collection hint (if available)
        if (window.gc && typeof window.gc === 'function') {
            try {
                window.gc();
            } catch (e) {
                // Ignore if gc is not available
            }
        }
    }

    // ==========================================================================
    // Real-time Updates System
    // ==========================================================================
    
    /**
     * Initialize real-time updates system with smart polling
     */
    function initializeRealTimeUpdates() {
        console.log('Initializing real-time updates system...');

        // Setup Page Visibility API for intelligent polling
        initializeVisibilityDetection();
        
        // Initialize current filters from the page
        updateCurrentFilters();
        
        // Do an initial fetch immediately to populate the table
        console.log('Performing initial data fetch...');
        fetchUpdates()
            .then(() => {
                console.log('Initial fetch completed successfully');
                // Start polling after initial fetch
                startSmartPolling();
            })
            .catch(error => {
                console.error('Initial fetch failed:', error);
                // Still start polling even if initial fetch fails
                startSmartPolling();
            });
        
        console.log('Real-time updates system initialized');
    }

    /**
     * Setup Page Visibility API to adjust polling frequency
     */
    function initializeVisibilityDetection() {
        // Handle page visibility changes
        document.addEventListener('visibilitychange', function() {
            state.isPageVisible = !document.hidden;
            
            if (state.isPageVisible) {
                console.log('Page became visible - resuming normal polling');
                // Reset to normal interval when page becomes visible
                state.pollingInterval = CONFIG.POLLING_INITIAL_INTERVAL;
                restartPolling();
            } else {
                console.log('Page hidden - switching to slow polling');
                // Use slower interval when page is hidden
                state.pollingInterval = CONFIG.VISIBILITY_SLOW_INTERVAL;
                restartPolling();
            }
        });

        // Handle window focus/blur as fallback
        window.addEventListener('focus', function() {
            if (!document.hidden) {
                state.isPageVisible = true;
                state.pollingInterval = CONFIG.POLLING_INITIAL_INTERVAL;
                restartPolling();
            }
        });

        window.addEventListener('blur', function() {
            state.isPageVisible = false;
        });
    }

    /**
     * Update current filters from DOM elements
     */
    function updateCurrentFilters() {
        const statusSelect = document.querySelector(SELECTORS.statusSelect);
        const searchInput = document.querySelector(SELECTORS.searchInput);
        const pageSizeSelect = document.querySelector(SELECTORS.pageSizeSelect);

        state.currentFilters = {
            status: statusSelect?.value || '',
            search: searchInput?.value || '',
            pageSize: parseInt(pageSizeSelect?.value || '10', 10)
        };

        console.log('Updated current filters:', state.currentFilters);
    }

    /**
     * Start smart polling with exponential backoff
     */
    function startSmartPolling() {
        if (state.pollingTimeoutId) {
            clearTimeout(state.pollingTimeoutId);
        }

        const pollFunction = async () => {
            try {
                await fetchUpdates();
                
                // Reset error count on success
                state.consecutiveErrors = 0;
                
                // Reset to normal interval if we had errors before
                if (state.pollingInterval > CONFIG.POLLING_INITIAL_INTERVAL && state.isPageVisible) {
                    state.pollingInterval = CONFIG.POLLING_INITIAL_INTERVAL;
                }
                
            } catch (error) {
                handlePollingError(error);
            }
            
            // Schedule next poll
            scheduleNextPoll();
        };

        // Start immediately
        pollFunction();
    }

    /**
     * Handle polling errors with exponential backoff
     */
    function handlePollingError(error) {
        state.consecutiveErrors++;
        console.error(`Polling error (attempt ${state.consecutiveErrors}):`, error);

        // Implement exponential backoff
        const backoffMultiplier = Math.min(Math.pow(CONFIG.POLLING_BACKOFF_MULTIPLIER, state.consecutiveErrors), 8);
        state.pollingInterval = Math.min(
            CONFIG.POLLING_INITIAL_INTERVAL * backoffMultiplier,
            CONFIG.POLLING_MAX_INTERVAL
        );

        // Show user-friendly error after multiple failures
        if (state.consecutiveErrors >= 3) {
            showConnectionStatus(false);
        }

        console.log(`Next poll in ${state.pollingInterval}ms due to errors`);
    }

    /**
     * Schedule the next polling attempt
     */
    function scheduleNextPoll() {
        state.pollingTimeoutId = setTimeout(async () => {
            try {
                await fetchUpdates();
                state.consecutiveErrors = 0;
                    
                    // Adjust interval based on page visibility
                    const baseInterval = state.isPageVisible 
                        ? CONFIG.POLLING_INITIAL_INTERVAL 
                        : CONFIG.VISIBILITY_SLOW_INTERVAL;
                    
                    state.pollingInterval = baseInterval;
                } catch (error) {
                    handlePollingError(error);
                }
                
                scheduleNextPoll();
            }
        }, state.pollingInterval);
    }

    /**
     * Restart polling (when visibility changes or filters update)
     */
    function restartPolling() {
        if (state.pollingTimeoutId) {
            clearTimeout(state.pollingTimeoutId);
            state.pollingTimeoutId = null;
        }
        
        startSmartPolling();
    }

    /**
     * Fetch updates from the server
     */
    async function fetchUpdates() {
        if (state.isLoading) {
            console.log('Already loading, skipping update check');
            return;
        }

        console.log('=== fetchUpdates called ===');
        state.isLoading = true;

        updateCurrentFilters();

        const updateUrl = new URL('/Admin/OddRegistration/GetUpdates', window.location.origin);
        updateUrl.searchParams.append('lastCursor', state.lastCursor || '');
        updateUrl.searchParams.append('pageSize', state.currentFilters.pageSize.toString());
        updateUrl.searchParams.append('status', state.currentFilters.status);
        updateUrl.searchParams.append('search', state.currentFilters.search);

        console.log('DEBUG: Fetch URL:', updateUrl.toString());
        console.log('DEBUG: Request params:', {
            lastCursor: state.lastCursor || '(none)',
            pageSize: state.currentFilters.pageSize,
            status: state.currentFilters.status,
            search: state.currentFilters.search
        });

        const startTime = Date.now();
        const response = await fetch(updateUrl.toString(), {
            method: 'GET',
            headers: {
                'Accept': 'application/json',
                'Cache-Control': 'no-cache',
                'X-Requested-With': 'XMLHttpRequest'
            }
        });
        
        const fetchTime = Date.now() - startTime;
        console.log(`DEBUG: Fetch completed in ${fetchTime}ms, status: ${response.status}`);

        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const data = await response.json();

        console.log('Fetch updates response:', {
            success: data.success,
            hasNewData: data.hasNewData,
            dataLength: data.data?.length,
            totalRecords: data.totalRecords
        });

        if (data.success) {
            // Always update cursor and timestamp
            if (data.nextCursor) {
                state.lastCursor = data.nextCursor;
            }
            if (data.timestamp) {
                state.lastUpdateTimestamp = data.timestamp;
            }

            // Update data if we have new data or this is first load
            if (data.hasNewData && data.data && data.data.length > 0) {
                console.log(`Received ${data.data.length} records`);
                
                // Check if data has actually changed to avoid unnecessary updates
                if (hasDataChanged(data.data)) {
                    console.log('Data has changed, updating table');
                    
                    // Cache the data for performance
                    const cacheKey = `updates-${JSON.stringify(state.currentFilters)}`;
                    cacheData(cacheKey, data.data, 30000); // Cache for 30 seconds
                    
                    // Use debounced update to prevent excessive redraws
                    debouncedTableUpdate(data.data, data.stats);
                    
                    // Optimize polling interval based on activity
                    optimizePollingInterval();
                } else {
                    console.log('No data changes detected, skipping table update');
                }
            } else {
                console.log('No new data to display');
                // Still update stats if available
                if (data.stats) {
                    updateStatsDisplay(data.stats);
                }
            }
            
            // Show connection status as good
            showConnectionStatus(true);
        } else {
            throw new Error(data.message || 'Failed to fetch updates');
        }

        // Clean up memory periodically
        if (Math.random() < 0.1) { // 10% chance to run cleanup
            cleanupMemory();
        }

        // Even if no new data, we successfully connected
        if (state.consecutiveErrors > 0) {
            showConnectionStatus(true);
        }
        
        console.log('=== fetchUpdates completed ===');
    } finally {
        state.isLoading = false;
    }

    /**
     * Show connection status indicator
     */
    function showConnectionStatus(isConnected) {
        const statusIndicator = getOrCreateStatusIndicator();
        
        if (isConnected) {
            statusIndicator.innerHTML = `
                <div class="d-flex align-items-center text-success small">
                    <div class="spinner-grow spinner-grow-sm me-2" style="width: 8px; height: 8px;" role="status"></div>
                    <span>เชื่อมต่อแล้ว</span>
                </div>
            `;
            statusIndicator.classList.remove('text-danger', 'text-warning');
            statusIndicator.classList.add('text-success');
        } else {
            statusIndicator.innerHTML = `
                <div class="d-flex align-items-center text-warning small">
                    <i class="fas fa-exclamation-triangle me-2"></i>
                    <span>การเชื่อมต่อมีปัญหา</span>
                </div>
            `;
            statusIndicator.classList.remove('text-success');
            statusIndicator.classList.add('text-warning');
        }
    }

    /**
     * Get or create the status indicator element
     */
    function getOrCreateStatusIndicator() {
        let indicator = document.getElementById('realTimeStatus');
        if (!indicator) {
            indicator = document.createElement('div');
            indicator.id = 'realTimeStatus';
            indicator.className = 'position-fixed';
            indicator.style.cssText = 'bottom: 20px; right: 20px; z-index: 1040; background: rgba(255,255,255,0.9); padding: 8px 12px; border-radius: 4px; box-shadow: 0 2px 8px rgba(0,0,0,0.1);';
            
            const tableContainer = document.querySelector(SELECTORS.tableContainer);
            if (tableContainer) {
                tableContainer.appendChild(indicator);
            } else {
                document.body.appendChild(indicator);
            }
        }
        return indicator;
    }




    // ==========================================================================
    // Table Update Logic
    // ==========================================================================
    
    /**
     * Update table with new data without page reload
     */
    async function updateTableWithNewData(newData, stats) {
        console.log('updateTableWithNewData called with:', newData?.length, 'items');
        
        if (!newData || newData.length === 0) {
            console.log('No new data to update');
            // Still update stats even if no new data
            if (stats) {
                updateStatsDisplay(stats);
            }
            return;
        }

        const tableBody = document.querySelector('.odd-table tbody');
        const mobileCards = document.querySelector(SELECTORS.mobileCards);
        
        if (!tableBody && !mobileCards) {
            console.log('No table or mobile cards found for updates');
            return;
        }

        console.log('Current view:', state.currentView);
        console.log('Table body found:', !!tableBody);
        console.log('Mobile cards found:', !!mobileCards);

        try {
            if (state.currentView === 'desktop' && tableBody) {
                // Perform incremental updates for better user experience
                await performIncrementalTableUpdate(newData);
            } else if (state.currentView === 'mobile' && mobileCards) {
                await updateMobileCards(newData);
            }

            // Always update statistics display
            if (stats) {
                updateStatsDisplay(stats);
            }

            // Show subtle notification
            showDataUpdateNotification();
            
        } catch (error) {
            console.error('Error updating table with new data:', error);
        }
    }

    /**
     * Perform incremental table update with smart row management
     */
    async function performIncrementalTableUpdate(newData) {
        console.log('Performing incremental table update with', newData.length, 'items');
        
        if (!newData || newData.length === 0) {
            console.log('No data for incremental update');
            return;
        }

        // Get existing row IDs
        const existingRowIds = getExistingRowIds();
        console.log('Existing row IDs:', Array.from(existingRowIds));

        // Categorize data into new, updated, and existing
        const newItems = [];
        const updatedItems = [];
        
        for (const item of newData) {
            if (existingRowIds.has(item.id)) {
                updatedItems.push(item);
            } else {
                newItems.push(item);
            }
        }

        console.log('Categorized data:', {
            newItemsCount: newItems.length,
            updatedItemsCount: updatedItems.length
        });

        // Process updates sequentially for better performance
        if (newItems.length > 0) {
            await insertNewTableRows(newItems);
        }
        
        if (updatedItems.length > 0) {
            await updateExistingRows(updatedItems);
        }
        
        console.log('Incremental table update completed');
    }

    /**
     * Refresh table content with new data (replaces current rows)
     */
    async function refreshTableContent(newData) {
        console.log('refreshTableContent called with', newData.length, 'items');
        
        const tableBody = document.querySelector('.odd-table tbody');
        if (!tableBody) {
            console.error('Table body not found for refresh');
            return;
        }

        try {
            // Create document fragment for better performance
            const fragment = document.createDocumentFragment();
            
            // Generate rows for all items
            for (const item of newData) {
                const rowHtml = await generateRowHtml(item);
                const tempContainer = document.createElement('div');
                tempContainer.innerHTML = rowHtml;
                const newRow = tempContainer.firstElementChild;
                
                // Add fade-in animation for new rows
                newRow.classList.add('odd-fade-in');
                fragment.appendChild(newRow);
            }
            
            // Clear existing content and add new rows
            tableBody.innerHTML = '';
            tableBody.appendChild(fragment);
            
            // Trigger fade-in animations
            requestAnimationFrame(() => {
                const rows = tableBody.querySelectorAll('tr');
                rows.forEach((row, index) => {
                    setTimeout(() => {
                        row.classList.add('odd-row-inserted');
                    }, index * 50); // Stagger the animation
                });
            });
            
            console.log('Table refreshed with', newData.length, 'rows');
            
        } catch (error) {
            console.error('Error refreshing table content:', error);
        }
    }

    /**
     * Show subtle data update notification
     */
    function showDataUpdateNotification() {
        const indicator = getOrCreateStatusIndicator();
        
        // Flash the indicator briefly
        indicator.style.transition = 'opacity 0.3s ease';
        indicator.style.opacity = '0.5';
        
        setTimeout(() => {
            indicator.style.opacity = '1';
        }, 300);
        
        // Also show a subtle toast notification for new data
        showNewDataIndicator();
    }

    /**
     * Show visual indicator for new data updates
     */
    function showNewDataIndicator() {
        // Remove any existing indicators
        const existingIndicator = document.querySelector('.odd-new-data-indicator');
        if (existingIndicator) {
            existingIndicator.remove();
        }

        // Create new data indicator
        const indicator = document.createElement('div');
        indicator.className = 'odd-new-data-indicator';
        indicator.innerHTML = `
            <div class="d-flex align-items-center">
                <i class="fas fa-sync-alt fa-spin me-2"></i>
                <span>ข้อมูลใหม่ถูกอัปเดตแล้ว</span>
            </div>
        `;
        
        // Style the indicator
        Object.assign(indicator.style, {
            position: 'fixed',
            top: '80px',
            right: '20px',
            backgroundColor: '#28a745',
            color: 'white',
            padding: '8px 16px',
            borderRadius: '4px',
            fontSize: '14px',
            zIndex: '1060',
            boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
            transform: 'translateX(100%)',
            transition: 'transform 0.3s ease',
            opacity: '0'
        });

        document.body.appendChild(indicator);

        // Animate in
        requestAnimationFrame(() => {
            indicator.style.transform = 'translateX(0)';
            indicator.style.opacity = '1';
        });

        // Auto-hide after 3 seconds
        setTimeout(() => {
            indicator.style.transform = 'translateX(100%)';
            indicator.style.opacity = '0';
            setTimeout(() => {
                if (indicator.parentNode) {
                    indicator.remove();
                }
            }, 300);
        }, 3000);
    }

    /**
     * Get existing row IDs from the current table
     */
    function getExistingRowIds() {
        const existingIds = new Set();
        const rows = document.querySelectorAll('.odd-table tbody tr');
        
        rows.forEach(row => {
            const checkbox = row.querySelector('.row-checkbox');
            if (checkbox && checkbox.value) {
                existingIds.add(parseInt(checkbox.value, 10));
            }
        });

        return existingIds;
    }

    /**
     * Update an existing row with new data
     */
    async function updateExistingRow(item) {
        const row = findRowById(item.id);
        if (!row) {
            console.log(`Row with ID ${item.id} not found for update`);
            return;
        }

        // Add update highlight class
        row.classList.add('odd-row-updating');

        try {
            // Generate new row HTML
            const newRowHtml = await generateRowHtml(item);
            const tempContainer = document.createElement('div');
            tempContainer.innerHTML = newRowHtml;
            const newRow = tempContainer.firstElementChild;

            // Copy classes and attributes
            newRow.className = row.className;
            newRow.classList.add('odd-row-updated');

            // Replace the row content
            row.innerHTML = newRow.innerHTML;

            // Animate the update
            setTimeout(() => {
                row.classList.remove('odd-row-updating', 'odd-row-updated');
            }, 2000);

        } catch (error) {
            console.error('Error updating row:', error);
            row.classList.remove('odd-row-updating');
        }
    }

    /**
     * Insert a new row at the top of the table
     */
    async function insertNewRow(item) {
        const tableBody = document.querySelector('.odd-table tbody');
        if (!tableBody) return;

        try {
            // Generate row HTML
            const rowHtml = await generateRowHtml(item);
            
            // Create temporary element
            const tempContainer = document.createElement('div');
            tempContainer.innerHTML = rowHtml;
            const newRow = tempContainer.firstElementChild;
            
            // Add new row animation classes
            newRow.classList.add('odd-row-new', 'odd-fade-in');
            
            // Insert at the beginning
            tableBody.insertBefore(newRow, tableBody.firstChild);
            
            // Trigger animation
            requestAnimationFrame(() => {
                newRow.classList.add('odd-row-inserted');
            });

            // Remove the row limit to maintain page size
            const rows = tableBody.querySelectorAll('tr');
            const pageSize = state.currentFilters.pageSize;
            
            if (rows.length > pageSize) {
                // Remove excess rows from the bottom with fade out
                for (let i = pageSize; i < rows.length; i++) {
                    const excessRow = rows[i];
                    excessRow.classList.add('odd-fade-out');
                    
                    setTimeout(() => {
                        if (excessRow.parentNode) {
                            excessRow.remove();
                        }
                    }, CONFIG.ANIMATION_DURATION);
                }
            }

            // Clean up animation classes after animation
            setTimeout(() => {
                newRow.classList.remove('odd-row-new', 'odd-fade-in', 'odd-row-inserted');
            }, CONFIG.ANIMATION_DURATION + 100);

        } catch (error) {
            console.error('Error inserting new row:', error);
        }
    }

    /**
     * Insert new table rows for new items
     */
    async function insertNewTableRows(newItems) {
        if (!newItems || newItems.length === 0) return;
        
        const tableBody = document.querySelector('.odd-table tbody');
        if (!tableBody) return;
        
        console.log('Inserting', newItems.length, 'new rows');
        
        const fragment = document.createDocumentFragment();
        
        // Process items sequentially to maintain order
        for (const item of newItems) {
            try {
                const rowHtml = await generateRowHtml(item);
                const tempContainer = document.createElement('div');
                tempContainer.innerHTML = rowHtml;
                const newRow = tempContainer.firstElementChild;
                
                if (newRow) {
                    newRow.classList.add('odd-row-new', 'odd-fade-in');
                    newRow.setAttribute('data-id', item.id);
                    fragment.appendChild(newRow);
                }
            } catch (error) {
                console.error('Error generating row HTML for item:', item.id, error);
            }
        }
        
        if (fragment.children.length > 0) {
            tableBody.insertBefore(fragment, tableBody.firstChild);
            
            // Animate new rows with staggered effect
            const newRows = tableBody.querySelectorAll('.odd-row-new');
            newRows.forEach((row, index) => {
                setTimeout(() => {
                    row.classList.add('odd-row-inserted');
                }, index * 100); // Slightly longer delay for better visual effect
            });
            
            // Clean up animation classes
            setTimeout(() => {
                newRows.forEach(row => {
                    row.classList.remove('odd-row-new', 'odd-fade-in', 'odd-row-inserted');
                });
            }, CONFIG.ANIMATION_DURATION + (newRows.length * 100));
            
            // Remove excess rows to maintain page size
            removeOldRows(state.currentFilters.pageSize);
        }
    }

    /**
     * Update existing rows with modified data
     */
    async function updateExistingRows(updatedItems) {
        if (!updatedItems || updatedItems.length === 0) return;
        
        console.log('Updating', updatedItems.length, 'existing rows');
        
        // Process updates sequentially to avoid DOM conflicts
        for (const item of updatedItems) {
            try {
                await updateExistingRow(item);
            } catch (error) {
                console.error('Error updating row for item:', item.id, error);
            }
        }
    }

    /**
     * Remove old rows to maintain pagination size
     */
    function removeOldRows(pageSize) {
        const tableBody = document.querySelector('.odd-table tbody');
        if (!tableBody) return;
        
        const rows = tableBody.querySelectorAll('tr');
        
        if (rows.length <= pageSize) return;
        
        console.log('Removing', rows.length - pageSize, 'excess rows');
        
        // Remove excess rows from the bottom with fade-out animation
        for (let i = pageSize; i < rows.length; i++) {
            const row = rows[i];
            row.classList.add('odd-row-removing');
            
            // Apply fade-out styles
            Object.assign(row.style, {
                transition: 'opacity 0.3s ease, transform 0.3s ease',
                opacity: '0',
                transform: 'translateX(20px)'
            });
            
            setTimeout(() => {
                if (row.parentNode) {
                    row.remove();
                }
            }, CONFIG.ANIMATION_DURATION);
        }
    }

    /**
     * Generate HTML for a table row
     */
    async function generateRowHtml(item) {
        // This is a simplified version - you might want to create a more robust template system
        const currentCulture = document.documentElement.lang || 'en';
        const isThaiCulture = currentCulture.startsWith('th');
        
        // Helper function to format date
        const formatDate = (dateStr) => {
            if (!dateStr) return '';
            const date = new Date(dateStr);
            return date.toLocaleString(isThaiCulture ? 'th-TH' : 'en-US');
        };

        // Helper function to format relative time
        const formatRelativeTime = (dateStr) => {
            if (!dateStr) return '';
            const date = new Date(dateStr);
            const now = new Date();
            const diffMs = now - date;
            const diffMins = Math.floor(diffMs / 60000);
            
            if (diffMins < 1) return 'เมื่อกี้นี้';
            if (diffMins < 60) return `${diffMins} นาทีที่แล้ว`;
            const diffHours = Math.floor(diffMins / 60);
            if (diffHours < 24) return `${diffHours} ชั่วโมงที่แล้ว`;
            const diffDays = Math.floor(diffHours / 24);
            return `${diffDays} วันที่แล้ว`;
        };

        // Generate status badge
        const getStatusBadge = (status) => {
            const statusClasses = {
                'Pending': 'odd-badge odd-badge-pending',
                'Completed': 'odd-badge odd-badge-success',
                'Success': 'odd-badge odd-badge-success',
                'Failed': 'odd-badge odd-badge-failed',
                'Fail': 'odd-badge odd-badge-failed'
            };
            
            const statusLabels = {
                'Pending': 'รอดำเนินการ',
                'Completed': 'สำเร็จ',
                'Success': 'สำเร็จ',
                'Failed': 'ล้มเหลว',
                'Fail': 'ล้มเหลว'
            };

            const cssClass = statusClasses[status] || 'odd-badge odd-badge-secondary';
            const label = statusLabels[status] || status;
            
            return `<span class="${cssClass}">${label}</span>`;
        };

        // Generate OTAC state badge
        const getOtacStateBadge = (otacState) => {
            if (!otacState) return '';
            
            const stateClasses = {
                'Generated': 'odd-badge odd-badge-success',
                'Used': 'odd-badge odd-badge-info',
                'Expired': 'odd-badge odd-badge-warning'
            };
            
            const stateLabels = {
                'Generated': 'สร้างแล้ว',
                'Used': 'ใช้แล้ว',
                'Expired': 'หมดอายุ'
            };

            const cssClass = stateClasses[otacState] || 'odd-badge odd-badge-secondary';
            const label = stateLabels[otacState] || otacState;
            
            return `<span class="${cssClass}">${label}</span>`;
        };

        return `
            <tr>
                <td class="odd-cell-checkbox">
                    <input type="checkbox" class="form-check-input row-checkbox" 
                           value="${item.id}" aria-label="Select registration ${item.id}">
                </td>

                <!-- External Reference -->
                <td class="odd-cell-external-ref">
                    ${item.externalReference ? 
                        `<div class="odd-info-primary">${item.externalReference}</div>` : 
                        '<div class="text-muted">-</div>'
                    }
                </td>

                <!-- Customer Info Block -->
                <td class="odd-cell-customer">
                    <div class="odd-info-block ${isThaiCulture ? 'odd-thai-font' : ''}">
                        ${item.fullName ? 
                            `<div class="odd-info-primary">
                                <i class="fas fa-user me-1 text-secondary"></i>${item.fullName}
                            </div>` : ''
                        }
                        
                        ${item.mobileNo ? 
                            `<div class="odd-info-detail">
                                <i class="fas fa-phone me-1 text-success"></i>
                                <span>${item.mobileNo}</span>
                            </div>` : ''
                        }

                        ${item.accountNo ? 
                            `<div class="odd-info-detail">
                                <i class="fas fa-university me-1 text-info"></i>
                                <span class="text-muted">${item.accountNo}</span>
                            </div>` : ''
                        }
                    </div>
                </td>

                <!-- Branch Information -->
                <td class="odd-cell-branch">
                    ${item.branch ? 
                        `<div class="odd-info-block ${isThaiCulture ? 'odd-thai-font' : ''}">
                            <div class="odd-info-primary">
                                ${isThaiCulture && item.branch.nameTh ? item.branch.nameTh : item.branch.nameEn}
                            </div>
                            ${item.branch.code ? 
                                `<div class="odd-info-detail">
                                    <span class="odd-badge bg-secondary">${item.branch.code}</span>
                                </div>` : ''
                            }
                        </div>` :
                        '<span class="text-muted">ไม่มีสาขาที่กำหนด</span>'
                    }
                </td>

                <!-- OTAC Details -->
                <td class="odd-cell-otac">
                    <div class="odd-info-block ${isThaiCulture ? 'odd-thai-font' : ''}">
                        ${item.otacCode ? 
                            `<div class="mb-2">
                                <code class="odd-otac-code" title="คลิกเพื่อคัดลอก">${item.otacCode}</code>
                            </div>` : ''
                        }

                        ${item.otacState ? 
                            `<div class="mb-2">
                                ${getOtacStateBadge(item.otacState)}
                            </div>` : ''
                        }

                        ${item.otacExpiresAt ? 
                            `<div class="odd-timeline-item">
                                <i class="fas fa-clock odd-timeline-icon text-warning"></i>
                                <div class="odd-timeline-content">
                                    <div class="odd-timeline-label">หมดอายุ</div>
                                    <div class="odd-timeline-value">${formatDate(item.otacExpiresAt)}</div>
                                    <div class="odd-timeline-time">${formatRelativeTime(item.otacExpiresAt)}</div>
                                </div>
                            </div>` : ''
                        }
                    </div>
                </td>

                <!-- Registration Status -->
                <td class="odd-cell-status">
                    <div class="odd-info-block ${isThaiCulture ? 'odd-thai-font' : ''}">
                        <div class="mb-2">
                            ${getStatusBadge(item.status)}
                        </div>

                        ${item.returnCode ? 
                            `<div class="odd-timeline-item">
                                <i class="fas fa-code odd-timeline-icon text-info"></i>
                                <div class="odd-timeline-content">
                                    <div class="odd-timeline-label">รหัส</div>
                                    <div class="odd-timeline-value font-monospace">${item.returnCode}</div>
                                </div>
                            </div>` : ''
                        }

                        ${item.regId ? 
                            `<div class="odd-timeline-item">
                                <i class="fas fa-id-badge odd-timeline-icon text-primary"></i>
                                <div class="odd-timeline-content">
                                    <div class="odd-timeline-label">Registration ID</div>
                                    <div class="odd-timeline-value font-monospace text-primary">${item.regId}</div>
                                </div>
                            </div>` : ''
                        }

                        ${item.espaId ? 
                            `<div class="odd-timeline-item">
                                <i class="fas fa-fingerprint odd-timeline-icon text-success"></i>
                                <div class="odd-timeline-content">
                                    <div class="odd-timeline-label">ESPA ID</div>
                                    <div class="odd-timeline-value font-monospace text-success">${item.espaId}</div>
                                </div>
                            </div>` : ''
                        }
                    </div>
                </td>

                <!-- Tracking Info -->
                <td class="odd-cell-tracking">
                    <div class="odd-info-block ${isThaiCulture ? 'odd-thai-font' : ''}">
                        ${item.generatedByUser ? 
                            `<div class="odd-user-info mb-2">
                                <div class="odd-user-avatar">
                                    ${(item.generatedByUser.firstName?.[0] || '') + (item.generatedByUser.lastName?.[0] || '')}
                                </div>
                                <div class="odd-user-details">
                                    <div class="odd-user-name">${item.generatedByUser.firstName || ''} ${item.generatedByUser.lastName || ''}</div>
                                    <div class="odd-user-role">${item.generatedByUser.role || ''}</div>
                                </div>
                            </div>` : ''
                        }

                        ${item.attemptCount > 0 ? 
                            `<div class="odd-timeline-item">
                                <i class="fas fa-redo odd-timeline-icon text-warning"></i>
                                <div class="odd-timeline-content">
                                    <div class="odd-timeline-label">ความพยายาม</div>
                                    <div class="odd-timeline-value">
                                        <span class="odd-badge odd-badge-warning">${item.attemptCount}</span>
                                    </div>
                                </div>
                            </div>` : ''
                        }

                        <div class="odd-timeline-item">
                            <i class="fas fa-plus-circle odd-timeline-icon text-success"></i>
                            <div class="odd-timeline-content">
                                <div class="odd-timeline-label">สร้างเมื่อ</div>
                                <div class="odd-timeline-value">${formatDate(item.createdAt)}</div>
                                <div class="odd-timeline-time">${formatRelativeTime(item.createdAt)}</div>
                            </div>
                        </div>

                        ${item.updatedAt ? 
                            `<div class="odd-timeline-item">
                                <i class="fas fa-edit odd-timeline-icon text-info"></i>
                                <div class="odd-timeline-content">
                                    <div class="odd-timeline-label">อัปเดตเมื่อ</div>
                                    <div class="odd-timeline-value">${formatDate(item.updatedAt)}</div>
                                    <div class="odd-timeline-time">${formatRelativeTime(item.updatedAt)}</div>
                                </div>
                            </div>` : ''
                        }
                    </div>
                </td>
            </tr>
        `;
    }

    /**
     * Find a row by its ID
     */
    function findRowById(id) {
        const checkbox = document.querySelector(`.row-checkbox[value="${id}"]`);
        return checkbox ? checkbox.closest('tr') : null;
    }

    /**
     * Update mobile cards view
     */
    async function updateMobileCards(newData) {
        const mobileCardsContainer = document.querySelector(SELECTORS.mobileCards);
        if (!mobileCardsContainer) {
            console.log('Mobile cards container not found');
            return;
        }

        console.log('Updating mobile cards with', newData.length, 'items');

        try {
            // Get existing card IDs
            const existingCardIds = new Set();
            const existingCards = mobileCardsContainer.querySelectorAll('.odd-mobile-card');
            existingCards.forEach(card => {
                const checkbox = card.querySelector('.row-checkbox');
                if (checkbox && checkbox.value) {
                    existingCardIds.add(parseInt(checkbox.value, 10));
                }
            });

            // Categorize data
            const newItems = [];
            const updatedItems = [];
            
            for (const item of newData) {
                if (existingCardIds.has(item.id)) {
                    updatedItems.push(item);
                } else {
                    newItems.push(item);
                }
            }

            console.log('Mobile cards - New:', newItems.length, 'Updated:', updatedItems.length);

            // Insert new cards at the top
            if (newItems.length > 0) {
                await insertNewMobileCards(newItems);
            }

            // Update existing cards
            if (updatedItems.length > 0) {
                await updateExistingMobileCards(updatedItems);
            }

            // Remove excess cards to maintain page size
            removexcessMobileCards(state.currentFilters.pageSize);

            console.log('Mobile cards update completed');
        } catch (error) {
            console.error('Error updating mobile cards:', error);
        }
    }

    /**
     * Insert new mobile cards for new items
     */
    async function insertNewMobileCards(newItems) {
        const mobileCardsContainer = document.querySelector(SELECTORS.mobileCards);
        if (!mobileCardsContainer || !newItems.length) return;

        const fragment = document.createDocumentFragment();

        for (const item of newItems) {
            try {
                const cardHtml = await generateMobileCardHtml(item);
                const tempContainer = document.createElement('div');
                tempContainer.innerHTML = cardHtml;
                const newCard = tempContainer.firstElementChild;
                
                if (newCard) {
                    newCard.classList.add('odd-card-new', 'odd-fade-in');
                    newCard.setAttribute('data-id', item.id);
                    fragment.appendChild(newCard);
                }
            } catch (error) {
                console.error('Error generating mobile card HTML for item:', item.id, error);
            }
        }

        if (fragment.children.length > 0) {
            mobileCardsContainer.insertBefore(fragment, mobileCardsContainer.firstChild);
            
            // Animate new cards
            const newCards = mobileCardsContainer.querySelectorAll('.odd-card-new');
            newCards.forEach((card, index) => {
                setTimeout(() => {
                    card.classList.add('odd-card-inserted');
                }, index * 100);
            });

            // Clean up animation classes
            setTimeout(() => {
                newCards.forEach(card => {
                    card.classList.remove('odd-card-new', 'odd-fade-in', 'odd-card-inserted');
                });
            }, CONFIG.ANIMATION_DURATION + (newCards.length * 100));
        }
    }

    /**
     * Update existing mobile cards
     */
    async function updateExistingMobileCards(updatedItems) {
        for (const item of updatedItems) {
            try {
                const existingCard = document.querySelector(`[data-id="${item.id}"]`);
                if (existingCard) {
                    const newCardHtml = await generateMobileCardHtml(item);
                    const tempContainer = document.createElement('div');
                    tempContainer.innerHTML = newCardHtml;
                    const newCard = tempContainer.firstElementChild;

                    if (newCard) {
                        // Copy classes and add update animation
                        newCard.className = existingCard.className;
                        newCard.classList.add('odd-card-updated');
                        
                        existingCard.innerHTML = newCard.innerHTML;
                        
                        // Clean up animation
                        setTimeout(() => {
                            existingCard.classList.remove('odd-card-updated');
                        }, 1000);
                    }
                }
            } catch (error) {
                console.error('Error updating mobile card for item:', item.id, error);
            }
        }
    }

    /**
     * Remove excess mobile cards to maintain page size
     */
    function removexcessMobileCards(pageSize) {
        const mobileCardsContainer = document.querySelector(SELECTORS.mobileCards);
        if (!mobileCardsContainer) return;

        const cards = mobileCardsContainer.querySelectorAll('.odd-mobile-card');
        if (cards.length <= pageSize) return;

        for (let i = pageSize; i < cards.length; i++) {
            const card = cards[i];
            card.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
            card.style.opacity = '0';
            card.style.transform = 'translateY(20px)';
            
            setTimeout(() => {
                if (card.parentNode) {
                    card.remove();
                }
            }, CONFIG.ANIMATION_DURATION);
        }
    }

    /**
     * Generate HTML for a mobile card
     */
    async function generateMobileCardHtml(item) {
        const currentCulture = document.documentElement.lang || 'en';
        const isThaiCulture = currentCulture.startsWith('th');
        
        const formatDate = (dateStr) => {
            if (!dateStr) return '';
            const date = new Date(dateStr);
            return date.toLocaleString(isThaiCulture ? 'th-TH' : 'en-US');
        };

        const formatRelativeTime = (dateStr) => {
            if (!dateStr) return '';
            const date = new Date(dateStr);
            const now = new Date();
            const diffMs = now - date;
            const diffMins = Math.floor(diffMs / 60000);
            
            if (diffMins < 1) return 'เมื่อกี้นี้';
            if (diffMins < 60) return `${diffMins} นาทีที่แล้ว`;
            const diffHours = Math.floor(diffMins / 60);
            if (diffHours < 24) return `${diffHours} ชั่วโมงที่แล้ว`;
            const diffDays = Math.floor(diffHours / 24);
            return `${diffDays} วันที่แล้ว`;
        };

        const getStatusBadge = (status) => {
            const statusClasses = {
                'Pending': 'odd-badge odd-badge-pending',
                'Completed': 'odd-badge odd-badge-success',
                'Success': 'odd-badge odd-badge-success',
                'Failed': 'odd-badge odd-badge-failed',
                'Fail': 'odd-badge odd-badge-failed'
            };
            
            const statusLabels = {
                'Pending': 'รอดำเนินการ',
                'Completed': 'สำเร็จ',
                'Success': 'สำเร็จ',
                'Failed': 'ล้มเหลว',
                'Fail': 'ล้มเหลว'
            };

            const cssClass = statusClasses[status] || 'odd-badge odd-badge-secondary';
            const label = statusLabels[status] || status;
            
            return `<span class="${cssClass}">${label}</span>`;
        };

        return `
            <div class="odd-mobile-card odd-fade-in">
                <div class="odd-mobile-card-header">
                    <div class="d-flex justify-content-between align-items-center">
                        <div class="d-flex align-items-center">
                            <input type="checkbox" class="form-check-input row-checkbox me-2" 
                                   value="${item.id}" aria-label="Select registration ${item.id}">
                            ${item.externalReference ? 
                                `<strong class="text-primary">${item.externalReference}</strong>` : 
                                `<strong class="text-muted">#${item.id}</strong>`
                            }
                        </div>
                        <div>
                            ${getStatusBadge(item.status)}
                        </div>
                    </div>
                </div>

                <div class="odd-mobile-card-body">
                    ${item.fullName ? 
                        `<div class="odd-mobile-field">
                            <div class="odd-mobile-label">
                                <i class="fas fa-user me-1"></i>ลูกค้า
                            </div>
                            <div class="odd-mobile-value">
                                <div class="fw-semibold">${item.fullName}</div>
                                ${item.mobileNo ? `<div class="small text-muted">${item.mobileNo}</div>` : ''}
                            </div>
                        </div>` : ''
                    }

                    ${item.otacCode ? 
                        `<div class="odd-mobile-field">
                            <div class="odd-mobile-label">
                                <i class="fas fa-key me-1"></i>OTAC
                            </div>
                            <div class="odd-mobile-value">
                                <code class="odd-otac-code">${item.otacCode}</code>
                            </div>
                        </div>` : ''
                    }

                    ${item.branch ? 
                        `<div class="odd-mobile-field">
                            <div class="odd-mobile-label">
                                <i class="fas fa-building me-1"></i>สาขา
                            </div>
                            <div class="odd-mobile-value">
                                ${item.branch.nameEn || item.branch.nameTh || ''}
                            </div>
                        </div>` : ''
                    }

                    <div class="odd-mobile-field">
                        <div class="odd-mobile-label">
                            <i class="fas fa-calendar me-1"></i>สร้างเมื่อ
                        </div>
                        <div class="odd-mobile-value">
                            <div class="small">${formatDate(item.createdAt)}</div>
                            <div class="small text-muted">${formatRelativeTime(item.createdAt)}</div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    /**
     * Update statistics display in the header
     */
    function updateStatsDisplay(stats) {
        if (!stats) {
            console.log('No stats provided for display update');
            return;
        }

        console.log('Updating stats display with:', stats);

        try {
            // Update the statistics badges in the table header
            const pendingBadge = document.querySelector('.odd-badge-pending');
            const successBadge = document.querySelector('.odd-badge-success');
            const failedBadge = document.querySelector('.odd-badge-failed');

            if (pendingBadge) {
                const newText = pendingBadge.textContent.replace(/\d+/, stats.pending || 0);
                pendingBadge.textContent = newText;
                // Add subtle animation to show update
                pendingBadge.style.transition = 'all 0.3s ease';
                pendingBadge.style.transform = 'scale(1.1)';
                setTimeout(() => {
                    pendingBadge.style.transform = 'scale(1)';
                }, 200);
            }

            if (successBadge) {
                const newText = successBadge.textContent.replace(/\d+/, stats.completed || stats.success || 0);
                successBadge.textContent = newText;
                // Add subtle animation to show update
                successBadge.style.transition = 'all 0.3s ease';
                successBadge.style.transform = 'scale(1.1)';
                setTimeout(() => {
                    successBadge.style.transform = 'scale(1)';
                }, 200);
            }

            if (failedBadge) {
                const newText = failedBadge.textContent.replace(/\d+/, stats.failed || 0);
                failedBadge.textContent = newText;
                // Add subtle animation to show update
                failedBadge.style.transition = 'all 0.3s ease';
                failedBadge.style.transform = 'scale(1.1)';
                setTimeout(() => {
                    failedBadge.style.transform = 'scale(1)';
                }, 200);
            }

            console.log('Successfully updated stats display');
        } catch (error) {
            console.error('Error updating stats display:', error);
        }
    }

    /**
     * Show notification for new data
     */
    function showNewDataNotification(count) {
        const message = count === 1 ? 
            'มีข้อมูลใหม่ 1 รายการ' : 
            `มีข้อมูลใหม่ ${count} รายการ`;
        
        showAlert('info', message, { 
            duration: 3000,
            position: 'top-right'
        });
    }

    // ==========================================================================
    // Selection Management
    // ==========================================================================
    
    function initializeSelectionHandlers() {
        const selectAll = document.querySelector(SELECTORS.selectAll);
        const rowCheckboxes = document.querySelectorAll(SELECTORS.rowCheckboxes);

        if (!selectAll || !rowCheckboxes.length) return;

        // Select All functionality
        selectAll.addEventListener('change', function() {
            const isChecked = this.checked;
            state.selectedRows.clear();
            
            rowCheckboxes.forEach(checkbox => {
                checkbox.checked = isChecked;
                if (isChecked) {
                    state.selectedRows.add(checkbox.value);
                }
            });

            updateSelectionUI();
        });

        // Individual checkbox handling
        rowCheckboxes.forEach(checkbox => {
            checkbox.addEventListener('change', function() {
                if (this.checked) {
                    state.selectedRows.add(this.value);
                } else {
                    state.selectedRows.delete(this.value);
                }

                updateSelectAllState();
                updateSelectionUI();
            });
        });
    }

    function updateSelectAllState() {
        const selectAll = document.querySelector(SELECTORS.selectAll);
        const rowCheckboxes = document.querySelectorAll(SELECTORS.rowCheckboxes);
        
        if (!selectAll || !rowCheckboxes.length) return;

        const checkedCount = state.selectedRows.size;
        const totalCount = rowCheckboxes.length;

        selectAll.indeterminate = checkedCount > 0 && checkedCount < totalCount;
        selectAll.checked = checkedCount === totalCount && totalCount > 0;
    }

    function updateSelectionUI() {
        // Update action buttons visibility/state based on selection
        const hasSelection = state.selectedRows.size > 0;
        
        // Enable/disable bulk action buttons
        const bulkButtons = document.querySelectorAll('[onclick^=\"bulkAction\"]');
        bulkButtons.forEach(button => {
            button.disabled = !hasSelection;
            button.classList.toggle('btn-outline-secondary', !hasSelection);
            button.classList.toggle('btn-primary', hasSelection);
        });
    }

    // ==========================================================================
    // Filter & Search Management
    // ==========================================================================
    
    function initializeFilterHandlers() {
        const filterForm = document.querySelector(SELECTORS.filterForm);
        const searchInput = document.querySelector(SELECTORS.searchInput);
        const statusSelect = document.querySelector(SELECTORS.statusSelect);
        const pageSizeSelect = document.querySelector(SELECTORS.pageSizeSelect);

        if (!filterForm) return;

        // Enhanced search with debouncing
        if (searchInput) {
            const debouncedSubmit = debounce(() => {
                if (!state.isLoading) {
                    filterForm.submit();
                }
            }, CONFIG.SEARCH_DEBOUNCE_DELAY);

            searchInput.addEventListener('input', debouncedSubmit);

            // Enter key support
            searchInput.addEventListener('keypress', function(e) {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    clearTimeout(state.searchTimeout);
                    filterForm.submit();
                }
            });

            // Escape key to clear
            searchInput.addEventListener('keydown', function(e) {
                if (e.key === 'Escape' && this.value) {
                    this.value = '';
                    debouncedSubmit();
                }
            });
        }

        // Auto-submit on status/page size change
        if (statusSelect) {
            statusSelect.addEventListener('change', () => {
                filterForm.submit();
            });
        }

        if (pageSizeSelect) {
            pageSizeSelect.addEventListener('change', () => {
                filterForm.submit();
            });
        }

        // Form submit handler with loading state
        filterForm.addEventListener('submit', function() {
            setLoadingState(true, 'กำลังโหลดข้อมูล...');
        });
    }

    // ==========================================================================
    // OTAC Management
    // ==========================================================================
    
    function initializeOtacHandlers() {
        // Copy to clipboard for OTAC codes
        document.addEventListener('click', function(e) {
            if (e.target.matches(SELECTORS.otacCode) || e.target.closest(SELECTORS.otacCode)) {
                const codeElement = e.target.matches(SELECTORS.otacCode) ? e.target : e.target.closest(SELECTORS.otacCode);
                copyToClipboard(codeElement.textContent.trim());
            }
        });
    }

    /**
     * Modern clipboard API with enhanced feedback
     */
    async function copyToClipboardModern(text) {
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch (err) {
                console.warn('Modern clipboard API failed, falling back:', err);
                return fallbackCopyToClipboard(text);
            }
        } else {
            return fallbackCopyToClipboard(text);
        }
    }
    
    /**
     * Show copy success toast notification
     */
    function showCopyToast(message, type = 'success') {
        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${type} border-0 position-fixed`;
        toast.style.cssText = 'top: 20px; right: 20px; z-index: 1055;';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');
        
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fas fa-${type === 'success' ? 'check-circle' : 'exclamation-triangle'} me-2"></i>
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;
        
        document.body.appendChild(toast);
        
        // Initialize and show toast
        if (typeof bootstrap !== 'undefined' && bootstrap.Toast) {
            const bsToast = new bootstrap.Toast(toast, { delay: 3000 });
            bsToast.show();
            
            // Clean up after hide
            toast.addEventListener('hidden.bs.toast', () => {
                toast.remove();
            });
        } else {
            // Fallback without Bootstrap
            setTimeout(() => {
                toast.style.opacity = '0';
                setTimeout(() => toast.remove(), 300);
            }, 3000);
        }
    }

    /**
     * Enhanced copy to clipboard with visual feedback
     */
    function copyToClipboard(text, element = null) {
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(() => {
                showAlert('success', 'คัดลอกรหัสเรียบร้อยแล้ว!', { duration: 2000 });
                if (element) showCopyFeedback(element);
            }).catch(() => {
                fallbackCopyToClipboard(text, element);
            });
        } else {
            fallbackCopyToClipboard(text, element);
        }
    }

    function fallbackCopyToClipboard(text, element = null) {
        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.style.cssText = 'position:fixed;top:-999px;left:-999px;opacity:0;';
        document.body.appendChild(textArea);
        textArea.select();
        textArea.setSelectionRange(0, 99999);

        try {
            document.execCommand('copy');
            showAlert('success', 'คัดลอกรหัสเรียบร้อยแล้ว!', { duration: 2000 });
            if (element) showCopyFeedback(element);
        } catch (err) {
            showAlert('warning', 'ไม่สามารถคัดลอกอัตโนมัติได้ กรุณาคัดลอกด้วยตนเอง', { duration: 3000 });
        }

        document.body.removeChild(textArea);
    }

    function showCopyFeedback(element) {
        const originalClass = element.className;
        element.style.backgroundColor = 'var(--odd-success-light)';
        element.style.transform = 'scale(1.05)';
        
        setTimeout(() => {
            element.style.backgroundColor = '';
            element.style.transform = '';
        }, 300);
    }

    // ==========================================================================
    // Action Handlers
    // ==========================================================================
    
    /**
     * Generate OTAC immediately with Priority 2 Progressive Loading
     */
    window.generateOtacImmediately = function() {
        console.log('generateOtacImmediately called with Priority 2 features');
        
        const generateButton = document.getElementById('generateOtacButton');
        
        // Prevent multiple clicks
        if (state.isLoading || (generateButton && generateButton.disabled)) {
            console.log('Already generating OTAC, ignoring click');
            return;
        }
        
        // Store original button text
        if (generateButton && !generateButton.dataset.originalText) {
            generateButton.dataset.originalText = generateButton.textContent.trim();
        }
        
        // Start Priority 2 Progressive Loading
        showProgressiveLoading();
        
        const token = document.querySelector('input[name=\"__RequestVerificationToken\"]')?.value;
        const generateUrl = '/Admin/Otac/GenerateForKBankOdd';

        console.log('Token found:', !!token);
        console.log('Generate URL:', generateUrl);

        // Actual API call with progressive updates
        performOtacGeneration(generateUrl, token);
    };

    /**
     * Priority 2: Progressive Loading with Steps
     */
    function showProgressiveLoading() {
        const generateButton = document.getElementById('generateOtacButton');
        
        // Disable button immediately
        if (generateButton) {
            generateButton.disabled = true;
            generateButton.classList.add('btn-loading');
        }
        
        // Create progress modal
        const progressModalHtml = `
            <div class="modal fade" id="otacProgressModal" tabindex="-1" aria-labelledby="otacProgressLabel" aria-hidden="true" data-bs-backdrop="static" data-bs-keyboard="false">
                <div class="modal-dialog modal-lg modal-dialog-centered">
                    <div class="modal-content">
                        <div class="modal-header bg-primary text-white">
                            <h5 class="modal-title" id="otacProgressLabel">
                                <i class="fas fa-cog fa-spin me-2"></i>กำลังสร้าง OTAC Code
                            </h5>
                        </div>
                        <div class="modal-body p-4">
                            <!-- Progress Steps -->
                            <div class="row mb-4">
                                <div class="col-12">
                                    <div class="progress progress-lg mb-3" style="height: 12px;">
                                        <div id="otacProgressBar" class="progress-bar progress-bar-striped progress-bar-animated bg-success" 
                                             role="progressbar" style="width: 0%" aria-valuenow="0" aria-valuemin="0" aria-valuemax="100">
                                        </div>
                                    </div>
                                    <div class="d-flex justify-content-between small text-muted">
                                        <span>0%</span>
                                        <span>33%</span>
                                        <span>66%</span>
                                        <span>100%</span>
                                    </div>
                                </div>
                            </div>

                            <!-- Step Messages -->
                            <div class="card border-light">
                                <div class="card-body">
                                    <div id="currentStepMessage" class="h6 text-primary mb-2">
                                        <i class="fas fa-hourglass-start me-2"></i>เตรียมการสร้าง OTAC Code...
                                    </div>
                                    <div id="stepDetails" class="small text-muted">
                                        กำลังเตรียมการเชื่อมต่อกับระบบความปลอดภัย
                                    </div>
                                    <div id="timeEstimate" class="small text-info mt-2">
                                        <i class="fas fa-clock me-1"></i>ประมาณการเวลา: 8-15 วินาที
                                    </div>
                                </div>
                            </div>

                            <!-- Security Guidelines (Priority 2) -->
                            <div class="alert alert-warning border-0 mt-3">
                                <h6 class="alert-heading">
                                    <i class="fas fa-shield-alt me-2"></i>แนวปฏิบัติด้านความปลอดภัย
                                </h6>
                                <ul class="mb-0 small">
                                    <li>OTAC Code จะหมดอายุภายใน 30 นาที</li>
                                    <li>ใช้ได้เพียงครั้งเดียว ห้ามแชร์กับผู้อื่น</li>
                                    <li>เก็บรักษารหัสให้ปลอดภัยจนกว่าจะใช้งานเสร็จ</li>
                                    <li>หากสูญหาย สามารถสร้างใหม่ได้ทันที</li>
                                </ul>
                            </div>

                            <!-- Connection Status -->
                            <div id="connectionStatus" class="d-flex align-items-center mt-3">
                                <div class="spinner-border spinner-border-sm text-success me-2" role="status" aria-hidden="true"></div>
                                <span class="small text-success">การเชื่อมต่อ: ปกติ</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal if present
        const existingModal = document.getElementById('otacProgressModal');
        if (existingModal) existingModal.remove();

        // Add modal to body
        document.body.insertAdjacentHTML('beforeend', progressModalHtml);

        // Show modal
        const modal = new bootstrap.Modal(document.getElementById('otacProgressModal'));
        modal.show();

        // Start progressive updates
        startProgressUpdates();
    }

    /**
     * Priority 2: Progressive Step Updates
     */
    function startProgressUpdates() {
        const progressBar = document.getElementById('otacProgressBar');
        const stepMessage = document.getElementById('currentStepMessage');
        const stepDetails = document.getElementById('stepDetails');
        const timeEstimate = document.getElementById('timeEstimate');
        
        const steps = [
            {
                progress: 0,
                message: '<i class="fas fa-hourglass-start me-2"></i>เตรียมการสร้าง OTAC Code...',
                details: 'กำลังเตรียมการเชื่อมต่อกับระบบความปลอดภัย',
                time: 'ประมาณการเวลา: 8-15 วินาที'
            },
            {
                progress: 33,
                message: '<i class="fas fa-shield-alt me-2 text-info"></i>ตรวจสอบความปลอดภัย...',
                details: 'กำลังยืนยันสิทธิ์และตรวจสอบข้อมูลผู้ใช้',
                time: 'เหลือประมาณ: 10-12 วินาที'
            },
            {
                progress: 66,
                message: '<i class="fas fa-key me-2 text-warning"></i>สร้างรหัสยืนยัน...',
                details: 'กำลังสร้าง OTAC Code แบบสุ่มด้วยอัลกอริทึมความปลอดภัยสูง',
                time: 'เหลือประมาณ: 3-5 วินาที'
            },
            {
                progress: 100,
                message: '<i class="fas fa-check-circle me-2 text-success"></i>สร้าง OTAC สำเร็จ!',
                details: 'OTAC Code พร้อมใช้งานแล้ว กำลังเปิดหน้าต่างผลลัพธ์...',
                time: 'เสร็จสิ้น'
            }
        ];

        let currentStep = 0;
        
        const updateStep = () => {
            if (currentStep < steps.length) {
                const step = steps[currentStep];
                
                // Update progress bar with animation
                progressBar.style.width = step.progress + '%';
                progressBar.setAttribute('aria-valuenow', step.progress);
                
                // Update messages
                stepMessage.innerHTML = step.message;
                stepDetails.textContent = step.details;
                timeEstimate.innerHTML = `<i class="fas fa-clock me-1"></i>${step.time}`;
                
                currentStep++;
            }
        };

        // Initial step
        updateStep();
        
        // Progressive updates
        setTimeout(() => updateStep(), 2000);  // 33% at 2s
        setTimeout(() => updateStep(), 5000);  // 66% at 5s
        
        // Start actual API call at 1 second
        setTimeout(() => {
            performActualApiCall();
        }, 1000);
    }

    /**
     * Priority 2: Enhanced API Call with Error Handling
     */
    function performOtacGeneration(generateUrl, token) {
        // This is called from startProgressUpdates after delay
    }

    function performActualApiCall() {
        const token = document.querySelector('input[name=\"__RequestVerificationToken\"]')?.value;
        const generateUrl = '/Admin/Otac/GenerateForKBankOdd';

        fetch(generateUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token || ''
            },
            body: `__RequestVerificationToken=${encodeURIComponent(token || '')}`
        })
        .then(response => {
            console.log('Response status:', response.status);
            
            // Update connection status
            updateConnectionStatus(response.ok);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            return response.json();
        })
        .then(data => {
            console.log('Response data:', data);
            
            // Complete progress to 100%
            setTimeout(() => {
                const progressBar = document.getElementById('otacProgressBar');
                const stepMessage = document.getElementById('currentStepMessage');
                const stepDetails = document.getElementById('stepDetails');
                
                if (progressBar) {
                    progressBar.style.width = '100%';
                    progressBar.setAttribute('aria-valuenow', '100');
                }
                
                if (stepMessage) {
                    stepMessage.innerHTML = '<i class="fas fa-check-circle me-2 text-success"></i>สร้าง OTAC สำเร็จ!';
                }
                
                if (stepDetails) {
                    stepDetails.textContent = 'OTAC Code พร้อมใช้งานแล้ว กำลังเปิดหน้าต่างผลลัพธ์...';
                }
                
                if (data.success) {
                    // Close progress modal and show results
                    setTimeout(() => {
                        const progressModal = bootstrap.Modal.getInstance(document.getElementById('otacProgressModal'));
                        if (progressModal) {
                            progressModal.hide();
                        }
                        
                        setTimeout(() => {
                            showEnhancedOtacResultsModal(data);
                            restoreGenerateButton();
                        }, 500);
                    }, 1500);
                } else {
                    showEnhancedError(data.message || 'เกิดข้อผิดพลาดในการสร้าง OTAC Code', 1);
                }
            }, 1000);
        })
        .catch(error => {
            console.error('OTAC generation error:', error);
            updateConnectionStatus(false);
            showEnhancedError(`เกิดข้อผิดพลาดในการเชื่อมต่อ: ${error.message}`, 1);
        });
    }

    /**
     * Priority 2: Connection Status Updates
     */
    function updateConnectionStatus(isConnected) {
        const connectionStatus = document.getElementById('connectionStatus');
        if (!connectionStatus) return;

        if (isConnected) {
            connectionStatus.innerHTML = `
                <div class="spinner-border spinner-border-sm text-success me-2" role="status" aria-hidden="true"></div>
                <span class="small text-success">การเชื่อมต่อ: ปกติ</span>
            `;
        } else {
            connectionStatus.innerHTML = `
                <i class="fas fa-exclamation-triangle text-warning me-2"></i>
                <span class="small text-warning">การเชื่อมต่อ: มีปัญหา</span>
            `;
        }
    }

    /**
     * Priority 2: Enhanced Error Display with Retry
     */
    function showEnhancedError(message, attemptCount = 1) {
        const progressBar = document.getElementById('otacProgressBar');
        const stepMessage = document.getElementById('currentStepMessage');
        const stepDetails = document.getElementById('stepDetails');
        const timeEstimate = document.getElementById('timeEstimate');
        
        // Update progress modal to show error state
        if (progressBar) {
            progressBar.classList.remove('bg-success', 'progress-bar-animated');
            progressBar.classList.add('bg-danger');
            progressBar.style.width = '100%';
        }
        
        if (stepMessage) {
            stepMessage.innerHTML = '<i class="fas fa-exclamation-triangle me-2 text-danger"></i>เกิดข้อผิดพลาด';
        }
        
        if (stepDetails) {
            stepDetails.textContent = message;
        }
        
        if (timeEstimate) {
            timeEstimate.innerHTML = `<i class="fas fa-redo me-1"></i>ความพยายามครั้งที่: ${attemptCount}`;
        }

        // Add retry button after delay
        setTimeout(() => {
            const modalBody = document.querySelector('#otacProgressModal .modal-body');
            if (modalBody && !modalBody.querySelector('.retry-section')) {
                const retrySection = document.createElement('div');
                retrySection.className = 'retry-section text-center mt-3';
                retrySection.innerHTML = `
                    <div class="alert alert-danger border-0 mb-3">
                        <h6 class="alert-heading">
                            <i class="fas fa-exclamation-triangle me-2"></i>ข้อผิดพลาด (ครั้งที่ ${attemptCount})
                        </h6>
                        <p class="mb-2">${message}</p>
                        <small class="text-muted">
                            กรุณาลองใหม่อีกครั้ง หรือติดต่อผู้ดูแลระบบหากปัญหายังคงอยู่
                        </small>
                    </div>
                    <div class="d-flex gap-2 justify-content-center">
                        <button type="button" class="btn btn-outline-primary" onclick="retryOtacGeneration(${attemptCount + 1})">
                            <i class="fas fa-redo me-1"></i>ลองใหม่ (${attemptCount + 1})
                        </button>
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                            <i class="fas fa-times me-1"></i>ปิด
                        </button>
                    </div>
                `;
                modalBody.appendChild(retrySection);
            }
        }, 1000);
    }

    /**
     * Priority 2: Retry Function with Attempt Counter
     */
    window.retryOtacGeneration = function(attemptCount) {
        console.log(`Retrying OTAC generation, attempt: ${attemptCount}`);
        
        // Reset progress modal
        const progressBar = document.getElementById('otacProgressBar');
        const stepMessage = document.getElementById('currentStepMessage');
        const stepDetails = document.getElementById('stepDetails');
        const retrySection = document.querySelector('.retry-section');
        
        if (retrySection) {
            retrySection.remove();
        }
        
        if (progressBar) {
            progressBar.classList.remove('bg-danger');
            progressBar.classList.add('bg-success', 'progress-bar-animated');
            progressBar.style.width = '0%';
        }
        
        if (stepMessage) {
            stepMessage.innerHTML = '<i class="fas fa-redo me-2"></i>ลองใหม่... (ครั้งที่ ' + attemptCount + ')';
        }
        
        if (stepDetails) {
            stepDetails.textContent = 'กำลังเตรียมการเชื่อมต่อใหม่กับระบบความปลอดภัย';
        }
        
        // Store attempt count for error handling
        window.currentAttemptCount = attemptCount;
        
        // Restart the process
        startProgressUpdates();
    };

    /**
     * Restore generate button to original state
     */
    function restoreGenerateButton() {
        const generateButton = document.getElementById('generateOtacButton');
        if (generateButton) {
            generateButton.disabled = false;
            generateButton.innerHTML = `
                <i class="fas fa-key me-1"></i>${generateButton.dataset.originalText || 'เพิ่มข้อมูล'}
            `;
            generateButton.classList.remove('btn-loading', 'btn-success', 'btn-info');
            generateButton.classList.add('btn-success');
        }
    }

    /**
     * Priority 2: Enhanced OTAC Results Modal with Expiry Warnings
     */
    function showEnhancedOtacResultsModal(data) {
        const expiryDate = new Date(data.expiresAt);
        const now = new Date();
        const timeLeft = expiryDate - now;
        const minutesLeft = Math.floor(timeLeft / 60000);
        
        // Calculate expiry warning level
        let expiryWarningClass = 'success';
        let expiryWarningIcon = 'clock';
        let expiryWarningText = 'ปกติ';
        
        if (minutesLeft <= 2) {
            expiryWarningClass = 'danger';
            expiryWarningIcon = 'exclamation-triangle';
            expiryWarningText = 'ใกล้หมดอายุ!';
        } else if (minutesLeft <= 5) {
            expiryWarningClass = 'warning';
            expiryWarningIcon = 'hourglass-half';
            expiryWarningText = 'แนะนำให้ใช้เร็ว';
        }
        
        const expiryTimeLocal = expiryDate.toLocaleString('th-TH', {
            year: 'numeric',
            month: 'short',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });

        const modalHtml = `
            <div class="modal fade" id="otacResultsModal" tabindex="-1" aria-labelledby="otacResultsModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header bg-gradient" style="background: linear-gradient(135deg, #28a745, #20c997);">
                            <h5 class="modal-title text-white" id="otacResultsModalLabel">
                                <i class="fas fa-check-circle me-2"></i>OTAC สร้างสำเร็จ!
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body text-center p-4">
                            <!-- Expiry Warning Alert (Priority 2) -->
                            <div class="alert alert-${expiryWarningClass} border-0 mb-4">
                                <div class="d-flex align-items-center justify-content-center">
                                    <i class="fas fa-${expiryWarningIcon} fa-2x me-3"></i>
                                    <div class="text-start">
                                        <h6 class="alert-heading mb-1">สถานะการหมดอายุ: ${expiryWarningText}</h6>
                                        <p class="mb-0">เหลือเวลา: <strong>${minutesLeft} นาที</strong> | หมดอายุ: ${expiryTimeLocal}</p>
                                        ${minutesLeft <= 5 ? '<small class="text-muted">⚠️ แนะนำให้ใช้ OTAC Code ทันทีเพื่อหลีกเลี่ยงการหมดอายุ</small>' : ''}
                                    </div>
                                </div>
                            </div>

                            <div class="odd-card mb-4">
                                <div class="odd-card-body">
                                    <div class="mb-4">
                                        <i class="fas fa-key fa-4x text-success mb-3"></i>
                                        <h4 class="text-success mb-2">OTAC Code พร้อมใช้งาน</h4>
                                    </div>

                                    <div class="code-display p-4 bg-light rounded border-success border-2 mb-4">
                                        <label class="form-label small text-muted mb-2">รหัสยืนยัน (ใช้ได้เพียงครั้งเดียว)</label>
                                        <div class="input-group justify-content-center mb-2">
                                            <input type="text" id="resultGeneratedCode" 
                                                   class="form-control form-control-lg font-monospace fw-bold text-center border-success"
                                                   value="${data.code}" readonly 
                                                   style="font-size: clamp(1.2rem, 4vw, 1.8rem); letter-spacing: 0.3em; max-width: 100%; background: #f8fff9;">
                                            <button class="btn btn-success btn-lg" type="button" 
                                                    onclick="copyOtacCode('resultGeneratedCode')" title="คัดลอก"
                                                    style="min-width: 80px; min-height: 48px;">
                                                <i class="fas fa-copy"></i>
                                                <span class="d-none d-sm-inline ms-1">คัดลอก</span>
                                            </button>
                                        </div>
                                        
                                        <!-- Mobile-friendly instruction -->
                                        <div class="d-md-none small text-primary text-center mb-2">
                                            <i class="fas fa-hand-pointer me-1"></i>แตะปุ่มคัดลอกด้านขวา
                                        </div>

                                        <div class="mt-3">
                                            <div id="resultCountdown" class="fw-semibold"></div>
                                        </div>
                                    </div>

                                    <!-- Enhanced Security Warnings (Priority 2) -->
                                    <div class="row g-3 mb-3">
                                        <div class="col-md-6">
                                            <div class="alert alert-info border-0 mb-0 h-100">
                                                <h6 class="alert-heading">
                                                    <i class="fas fa-shield-alt me-2"></i>ความปลอดภัย
                                                </h6>
                                                <ul class="mb-0 small">
                                                    <li>ใช้ได้เพียงครั้งเดียวเท่านั้น</li>
                                                    <li>ห้ามแชร์กับผู้อื่นโดยเด็ดขาด</li>
                                                    <li>เก็บรักษาในที่ปลอดภัย</li>
                                                </ul>
                                            </div>
                                        </div>
                                        <div class="col-md-6">
                                            <div class="alert alert-warning border-0 mb-0 h-100">
                                                <h6 class="alert-heading">
                                                    <i class="fas fa-exclamation-triangle me-2"></i>ข้อควรระวัง
                                                </h6>
                                                <ul class="mb-0 small">
                                                    <li>หมดอายุภายใน 30 นาที</li>
                                                    <li>ระบบจะล็อกหากใช้ผิด 3 ครั้ง</li>
                                                    <li>กรอกข้อมูลให้ถูกต้องครั้งแรก</li>
                                                </ul>
                                            </div>
                                        </div>
                                    </div>

                                    <!-- Critical Warning for Near Expiry -->
                                    ${minutesLeft <= 5 ? `
                                    <div class="alert alert-danger border-0 mb-3">
                                        <div class="d-flex align-items-center">
                                            <i class="fas fa-exclamation-triangle fa-2x text-danger me-3"></i>
                                            <div>
                                                <h6 class="alert-heading mb-1">🚨 เตือน: ใกล้หมดอายุ!</h6>
                                                <p class="mb-0">
                                                    OTAC Code นี้จะหมดอายุในอีก <strong class="text-danger">${minutesLeft} นาที</strong>
                                                    กรุณานำไปใช้ทันทีเพื่อหลีกเลี่ยงการต้องสร้างใหม่
                                                </p>
                                            </div>
                                        </div>
                                    </div>
                                    ` : ''}
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer justify-content-center gap-2 flex-wrap">
                            <button type="button" class="btn btn-success btn-lg" onclick="copyOtacCode('resultGeneratedCode')"
                                    style="min-height: 48px; min-width: 140px;">
                                <i class="fas fa-copy me-1"></i>คัดลอกรหัส
                            </button>
                            <button type="button" class="btn btn-outline-primary" onclick="refreshTable()">
                                <i class="fas fa-sync-alt me-1"></i>
                                <span class="d-none d-sm-inline">รีเฟรชตาราง</span>
                                <span class="d-sm-none">รีเฟรช</span>
                            </button>
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="fas fa-times me-1"></i>ปิด
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal
        const existingModal = document.getElementById('otacResultsModal');
        if (existingModal) existingModal.remove();

        // Add new modal
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Initialize and show modal
        const modal = new bootstrap.Modal(document.getElementById('otacResultsModal'));
        modal.show();

        // Start enhanced countdown with warnings
        startEnhancedOtacCountdown(data.expiresAt);

        // Cleanup on close - ADD AUTO REFRESH TABLE
        const modalElement = document.getElementById('otacResultsModal');
        const handleModalClose = function(event) {
            // Auto-refresh table when modal closes
            console.log('Enhanced OTAC Results Modal closed - refreshing table automatically');
            
            // Show visual feedback for table refresh
            if (window.visualFeedback) {
                window.visualFeedback.showToast('info', 'กำลังอัปเดต', 'ตารางกำลังรีเฟรชข้อมูลใหม่...', { duration: 2000 });
            }
            
            // PRIORITY: Force immediate table refresh after OTAC creation
            console.log('DEBUG: About to refresh table after OTAC creation');
            
            // Create a dedicated immediate refresh function for post-OTAC
            const immediateRefresh = async () => {
                console.log('DEBUG: Starting immediate post-OTAC refresh');
                
                try {
                    // Strategy 1: Direct API call with fresh data
                    console.log('DEBUG: Using immediate fetchUpdates bypass');
                        
                        // Reset state to force fresh data
                        state.lastCursor = null;
                        state.isLoading = false;
                        
                        // Direct call to fetch with fresh params
                        await fetchUpdates();
                        console.log('DEBUG: Immediate fetchUpdates successful');
                        
                        // Show success feedback
                        if (window.visualFeedback) {
                            window.visualFeedback.showToast('success', 'อัปเดตสำเร็จ', 'ตารางแสดงข้อมูล OTAC ใหม่แล้ว', { duration: 3000 });
                        }
                        return true;
                    }
                    
                    // Strategy 2: Use refreshTable function
                    if (typeof window.refreshTable === 'function') {
                        console.log('DEBUG: Using window.refreshTable() as backup');
                        window.refreshTable();
                        return true;
                    }
                    
                    // Strategy 3: Page reload as last resort
                    console.log('DEBUG: Using page reload as last resort');
                    setTimeout(() => {
                        window.location.reload();
                    }, 1000);
                    return true;
                    
                } catch (error) {
                    console.error('DEBUG: All refresh strategies failed:', error);
                    
                    // Ultimate fallback
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                    return false;
                }
            };
            
            // Execute immediate refresh
            immediateRefresh().then(success => {
                if (success) {
                    console.log('DEBUG: ✅ Post-OTAC table refresh completed successfully');
                } else {
                    console.warn('DEBUG: ⚠️  Post-OTAC table refresh had issues but fallback initiated');
                }
            }).catch(error => {
                console.error('DEBUG: ❌ Post-OTAC table refresh completely failed:', error);
                
                // Emergency page reload
                setTimeout(() => {
                    window.location.reload();
                }, 2000);
            });
            
            // Accelerate polling after OTAC creation for faster updates (Enhanced version)
            setTimeout(() => {
                state.pollingInterval = Math.max(1000, CONFIG.POLLING_INITIAL_INTERVAL * 0.2); // Speed up to 1-2 seconds
                console.log('Enhanced: Accelerated polling to', state.pollingInterval, 'ms after OTAC creation');
                restartPolling();
                
                // Reset to normal speed after 30 seconds
                setTimeout(() => {
                    state.pollingInterval = CONFIG.POLLING_INITIAL_INTERVAL;
                    console.log('Enhanced: Polling speed reset to normal:', state.pollingInterval, 'ms');
                    restartPolling();
                }, 30000);
            }, 100);
            
            // Remove event listener to prevent memory leaks
            modalElement.removeEventListener('hidden.bs.modal', handleModalClose);
            
            // Remove modal from DOM after delay to allow animations
            setTimeout(() => {
                if (modalElement && modalElement.parentNode) {
                    modalElement.remove();
                }
            }, 500);
        };
        
        modalElement.addEventListener('hidden.bs.modal', handleModalClose);
    }

    /**
     * Show OTAC result in modern mobile-responsive modal (Legacy)
     */
    function showOtacResultsModal(data) {
        const expiryDate = new Date(data.expiresAt);
        const expiryTimeLocal = expiryDate.toLocaleString('th-TH', {
            year: 'numeric',
            month: 'short',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });

        const modalHtml = `
            <div class="modal fade" id="otacResultsModal" tabindex="-1" aria-labelledby="otacResultsModalLabel" aria-hidden="true">
                <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
                    <div class="modal-content">
                        <div class="modal-header bg-gradient" style="background: linear-gradient(135deg, var(--odd-success), var(--odd-success-dark));">
                            <h5 class="modal-title text-white" id="otacResultsModalLabel">
                                <i class="fas fa-check-circle me-2"></i>OTAC สร้างสำเร็จ!
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                        </div>
                        <div class="modal-body text-center p-4">
                            <div class="odd-card mb-4">
                                <div class="odd-card-body">
                                    <div class="mb-4">
                                        <i class="fas fa-key fa-4x text-success mb-3"></i>
                                        <h4 class="text-success mb-2">OTAC Code พร้อมใช้งาน</h4>
                                    </div>

                                    <div class="code-display p-4 bg-light rounded border mb-4">
                                        <label class="form-label small text-muted mb-2">รหัสยืนยัน</label>
                                        <div class="input-group justify-content-center mb-2">
                                            <input type="text" id="resultGeneratedCode" 
                                                   class="form-control form-control-lg font-monospace fw-bold text-center border-success"
                                                   value="${data.code}" readonly 
                                                   style="font-size: clamp(1.2rem, 4vw, 1.8rem); letter-spacing: 0.3em; max-width: 100%; background: var(--odd-surface);">
                                            <button class="btn btn-outline-success btn-lg" type="button" 
                                                    onclick="copyOtacCode('resultGeneratedCode')" title="คัดลอก"
                                                    style="min-width: 60px; min-height: 48px;">
                                                <i class="fas fa-copy"></i>
                                            </button>
                                        </div>
                                        <!-- Mobile-friendly tap instruction -->
                                        <div class="d-md-none small text-muted text-center mb-2">
                                            <i class="fas fa-hand-pointer me-1"></i>แตะปุ่มคัดลอกเพื่อใช้งาน
                                        </div>

                                        <div class="mt-3">
                                            <small class="text-muted">
                                                <i class="fas fa-clock me-1"></i>หมดอายุ: ${expiryTimeLocal}
                                            </small>
                                            <div id="resultCountdown" class="small text-warning mt-1 fw-semibold"></div>
                                        </div>
                                    </div>

                                    <div class="alert alert-info border-0 mb-0">
                                        <i class="fas fa-shield-alt me-2"></i>
                                        รหัสนี้ใช้ได้เพียงครั้งเดียว กรุณาเก็บรักษาให้ปลอดภัย
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div class="modal-footer justify-content-center gap-2 flex-wrap">
                            <button type="button" class="btn btn-success btn-lg" onclick="copyOtacCode('resultGeneratedCode')"
                                    style="min-height: 48px; min-width: 120px;">
                                <i class="fas fa-copy me-1"></i>คัดลอก
                            </button>
                            <button type="button" class="btn btn-outline-primary" onclick="refreshTable()">
                                <i class="fas fa-sync-alt me-1"></i><span class="d-none d-sm-inline">รีเฟรชตาราง</span><span class="d-sm-none">รีเฟรช</span>
                            </button>
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="fas fa-times me-1"></i>ปิด
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal
        const existingModal = document.getElementById('otacResultsModal');
        if (existingModal) existingModal.remove();

        // Add new modal
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Initialize and show modal
        const modal = new bootstrap.Modal(document.getElementById('otacResultsModal'));
        modal.show();

        // Start countdown
        startOtacCountdown(data.expiresAt);

        // Cleanup on close - ADD AUTO REFRESH TABLE  
        const modalElement = document.getElementById('otacResultsModal');
        const handleModalClose = function(event) {
            // Auto-refresh table when modal closes (Legacy version)
            console.log('Legacy OTAC Results Modal closed - refreshing table automatically');
            
            // Show visual feedback for table refresh
            if (window.visualFeedback) {
                window.visualFeedback.showToast('info', 'กำลังอัปเดต', 'ตารางกำลังรีเฟรชข้อมูลใหม่...', { duration: 2000 });
            }
            
            // PRIORITY: Force immediate table refresh after OTAC creation (Legacy)
            console.log('DEBUG: About to refresh table after OTAC creation (Legacy)');
            
            // Create a dedicated immediate refresh function for post-OTAC (Legacy)
            const immediateRefresh = async () => {
                console.log('DEBUG: Starting immediate post-OTAC refresh (Legacy)');
                
                try {
                    // Strategy 1: Direct API call with fresh data
                    console.log('DEBUG: Using immediate fetchUpdates bypass (Legacy)');
                        
                        // Reset state to force fresh data
                        state.lastCursor = null;
                        state.isLoading = false;
                        
                        // Direct call to fetch with fresh params
                        await fetchUpdates();
                        console.log('DEBUG: Immediate fetchUpdates successful (Legacy)');
                        
                        // Show success feedback
                        if (window.visualFeedback) {
                            window.visualFeedback.showToast('success', 'อัปเดตสำเร็จ', 'ตารางแสดงข้อมูล OTAC ใหม่แล้ว (Legacy)', { duration: 3000 });
                        }
                        return true;
                    }
                    
                    // Strategy 2: Use refreshTable function
                    if (typeof window.refreshTable === 'function') {
                        console.log('DEBUG: Using window.refreshTable() as backup (Legacy)');
                        window.refreshTable();
                        return true;
                    }
                    
                    // Strategy 3: Page reload as last resort
                    console.log('DEBUG: Using page reload as last resort (Legacy)');
                    setTimeout(() => {
                        window.location.reload();
                    }, 1000);
                    return true;
                    
                } catch (error) {
                    console.error('DEBUG: All refresh strategies failed (Legacy):', error);
                    
                    // Ultimate fallback
                    setTimeout(() => {
                        window.location.reload();
                    }, 1500);
                    return false;
                }
            };
            
            // Execute immediate refresh
            immediateRefresh().then(success => {
                if (success) {
                    console.log('DEBUG: ✅ Post-OTAC table refresh completed successfully (Legacy)');
                } else {
                    console.warn('DEBUG: ⚠️  Post-OTAC table refresh had issues but fallback initiated (Legacy)');
                }
            }).catch(error => {
                console.error('DEBUG: ❌ Post-OTAC table refresh completely failed (Legacy):', error);
                
                // Emergency page reload
                setTimeout(() => {
                    window.location.reload();
                }, 2000);
            });
            
            // Accelerate polling after OTAC creation for faster updates (Legacy version)
            setTimeout(() => {
                state.pollingInterval = Math.max(1000, CONFIG.POLLING_INITIAL_INTERVAL * 0.2); // Speed up to 1-2 seconds
                console.log('Legacy: Accelerated polling to', state.pollingInterval, 'ms after OTAC creation');
                restartPolling();
                
                // Reset to normal speed after 30 seconds
                setTimeout(() => {
                    state.pollingInterval = CONFIG.POLLING_INITIAL_INTERVAL;
                    console.log('Legacy: Polling speed reset to normal:', state.pollingInterval, 'ms');
                    restartPolling();
                }, 30000);
            }, 100);
            
            // Remove event listener to prevent memory leaks
            modalElement.removeEventListener('hidden.bs.modal', handleModalClose);
            
            // Remove modal from DOM after delay to allow animations
            setTimeout(() => {
                if (modalElement && modalElement.parentNode) {
                    modalElement.remove();
                }
            }, 500);
        };
        
        modalElement.addEventListener('hidden.bs.modal', handleModalClose);
    }

    /**
     * Copy OTAC code with enhanced feedback and modern API
     */
    window.copyOtacCode = function(elementId) {
        const element = document.getElementById(elementId);
        if (!element) return;

        const textToCopy = element.value || element.textContent;
        
        // Use modern Clipboard API with fallback
        copyToClipboardModern(textToCopy).then(() => {
            // Enhanced visual feedback for button
            const button = element.nextElementSibling || document.querySelector(`[onclick*="${elementId}"]`);
            if (button) {
                const originalContent = button.innerHTML;
                const originalClasses = button.className;
                
                button.innerHTML = '<i class=\"fas fa-check\"></i><span class=\"d-none d-sm-inline ms-1\">คัดลอกแล้ว!</span>';
                button.classList.add('btn-success', 'copied');
                button.classList.remove('btn-outline-success');
                button.disabled = true;
                
                // Haptic feedback on mobile
                if (navigator.vibrate) {
                    navigator.vibrate(50);
                }

                setTimeout(() => {
                    button.innerHTML = originalContent;
                    button.className = originalClasses;
                    button.disabled = false;
                }, 2500);
            }
            
            // Show toast notification
            showCopyToast('คัดลอกรหัส OTAC เรียบร้อยแล้ว!');
        }).catch(() => {
            showCopyToast('ไม่สามารถคัดลอกอัตโนมัติได้ กรุณาคัดลอกด้วยตนเอง', 'warning');
        });
    };

    /**
     * Priority 2: Enhanced OTAC Countdown with Dynamic Warnings
     */
    function startEnhancedOtacCountdown(expiresAt) {
        const expiryDate = new Date(expiresAt);
        const countdownElement = document.getElementById('resultCountdown');
        
        if (!countdownElement) return;

        let warningShown = false;
        let urgentWarningShown = false;

        const updateCountdown = () => {
            const now = new Date();
            const timeLeft = expiryDate - now;

            if (timeLeft <= 0) {
                countdownElement.innerHTML = `
                    <div class="alert alert-danger p-2 mb-0">
                        <i class="fas fa-times-circle me-1"></i>
                        <strong>หมดอายุแล้ว</strong> - กรุณาสร้าง OTAC ใหม่
                    </div>
                `;
                
                // Show expired modal overlay
                showExpiredOverlay();
                return;
            }

            const minutes = Math.floor(timeLeft / 60000);
            const seconds = Math.floor((timeLeft % 60000) / 1000);

            let alertClass = 'alert-success';
            let icon = 'fas fa-clock';
            let statusText = 'ปกติ';
            let extraWarning = '';
            
            if (minutes < 1) {
                alertClass = 'alert-danger';
                icon = 'fas fa-exclamation-triangle';
                statusText = 'ใกล้หมดอายุมาก!';
                extraWarning = '<br><small class="text-danger">⚠️ เหลือเวลาน้อยมาก กรุณาใช้ทันที</small>';
                
                // Show urgent warning once
                if (!urgentWarningShown) {
                    showUrgentExpiryWarning(minutes, seconds);
                    urgentWarningShown = true;
                }
            } else if (minutes < 2) {
                alertClass = 'alert-danger';
                icon = 'fas fa-exclamation-triangle';
                statusText = 'ใกล้หมดอายุ!';
                extraWarning = '<br><small>🚨 กรุณาใช้ OTAC Code ทันที</small>';
            } else if (minutes < 5) {
                alertClass = 'alert-warning';
                icon = 'fas fa-hourglass-half';
                statusText = 'แนะนำให้ใช้เร็ว';
                extraWarning = '<br><small>⏰ แนะนำให้ใช้เร็วๆ เพื่อความปลอดภัย</small>';
                
                // Show warning once at 5 minutes
                if (!warningShown && minutes === 4) {
                    showExpiryWarning(minutes);
                    warningShown = true;
                }
            }

            countdownElement.innerHTML = `
                <div class="alert ${alertClass} p-2 mb-0">
                    <i class="${icon} me-1"></i>
                    <strong>เหลือ ${minutes} นาที ${seconds} วินาที</strong> (${statusText})${extraWarning}
                </div>
            `;
        };

        updateCountdown();
        const interval = setInterval(updateCountdown, 1000);

        // Clear interval when modal closes
        document.getElementById('otacResultsModal')?.addEventListener('hidden.bs.modal', () => {
            clearInterval(interval);
        });
    }

    /**
     * Priority 2: Show Expiry Warning Toast
     */
    function showExpiryWarning(minutes) {
        showAlert('warning', `🕐 OTAC Code จะหมดอายุในอีก ${minutes} นาที กรุณาใช้เร็วๆ`, { 
            duration: 8000, 
            position: 'top-right' 
        });
        
        // Vibrate on mobile if supported
        if (navigator.vibrate) {
            navigator.vibrate([200, 100, 200]);
        }
    }

    /**
     * Priority 2: Show Urgent Expiry Warning
     */
    function showUrgentExpiryWarning(minutes, seconds) {
        showAlert('danger', `🚨 เตือนด่วน! OTAC Code จะหมดอายุในอีก ${minutes} นาที ${seconds} วินาที กรุณาใช้ทันที!`, { 
            duration: 0, // Don't auto-dismiss
            position: 'center' 
        });
        
        // Urgent vibration pattern
        if (navigator.vibrate) {
            navigator.vibrate([300, 100, 300, 100, 300]);
        }
    }

    /**
     * Priority 2: Show Expired Overlay
     */
    function showExpiredOverlay() {
        const modal = document.getElementById('otacResultsModal');
        if (!modal || modal.querySelector('.expired-overlay')) return;

        const overlay = document.createElement('div');
        overlay.className = 'expired-overlay';
        overlay.style.cssText = `
            position: absolute;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(220, 53, 69, 0.9);
            color: white;
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 1060;
            backdrop-filter: blur(3px);
        `;
        
        overlay.innerHTML = `
            <div class="text-center">
                <i class="fas fa-times-circle fa-4x mb-3"></i>
                <h4 class="mb-3">OTAC Code หมดอายุแล้ว</h4>
                <p class="mb-4">กรุณาปิดหน้าต่างนี้และสร้าง OTAC Code ใหม่</p>
                <button class="btn btn-light" onclick="closeExpiredModal()">
                    <i class="fas fa-plus me-1"></i>สร้าง OTAC ใหม่
                </button>
            </div>
        `;
        
        modal.querySelector('.modal-content').style.position = 'relative';
        modal.querySelector('.modal-content').appendChild(overlay);
    }

    /**
     * Priority 2: Close Expired Modal and Generate New OTAC
     */
    window.closeExpiredModal = function() {
        const modal = bootstrap.Modal.getInstance(document.getElementById('otacResultsModal'));
        if (modal) {
            modal.hide();
        }
        
        // Auto-generate new OTAC after a short delay
        setTimeout(() => {
            generateOtacImmediately();
        }, 500);
    };

    /**
     * OTAC countdown timer (Legacy)
     */
    function startOtacCountdown(expiresAt) {
        const expiryDate = new Date(expiresAt);
        const countdownElement = document.getElementById('resultCountdown');
        
        if (!countdownElement) return;

        const updateCountdown = () => {
            const now = new Date();
            const timeLeft = expiryDate - now;

            if (timeLeft <= 0) {
                countdownElement.innerHTML = '<span class=\"text-danger\"><i class=\"fas fa-exclamation-triangle me-1\"></i>หมดอายุแล้ว</span>';
                return;
            }

            const minutes = Math.floor(timeLeft / 60000);
            const seconds = Math.floor((timeLeft % 60000) / 1000);

            let cssClass = 'text-success';
            let icon = 'fas fa-clock';
            
            if (minutes < 2) {
                cssClass = 'text-danger';
                icon = 'fas fa-exclamation-triangle';
            } else if (minutes < 5) {
                cssClass = 'text-warning';
                icon = 'fas fa-hourglass-half';
            }

            countdownElement.innerHTML = `<span class=\"${cssClass}\"><i class=\"${icon} me-1\"></i>เหลือ ${minutes} นาที ${seconds} วินาที</span>`;
        };

        updateCountdown();
        const interval = setInterval(updateCountdown, 1000);

        // Clear interval when modal closes
        document.getElementById('otacResultsModal')?.addEventListener('hidden.bs.modal', () => {
            clearInterval(interval);
        });
    }

    // ==========================================================================
    // Export & Bulk Actions
    // ==========================================================================
    
    /**
     * Export data functionality
     */
    window.exportData = function(format) {
        setLoadingState(true, `กำลังเตรียมไฟล์ ${format.toUpperCase()}...`);
        
        const exportUrl = `/Admin/OddRegistration/Export?format=${format}`;
        
        fetch(exportUrl, {
            method: 'GET',
            headers: {
                'Accept': 'application/json'
            }
        })
        .then(response => {
            setLoadingState(false);
            
            if (response.ok) {
                // If it's a file download, create download link
                if (response.headers.get('content-disposition')) {
                    const url = window.URL.createObjectURL(response.blob());
                    const a = document.createElement('a');
                    a.style.display = 'none';
                    a.href = url;
                    a.download = `odd-registrations.${format}`;
                    document.body.appendChild(a);
                    a.click();
                    window.URL.revokeObjectURL(url);
                    
                    showAlert('success', `ดาวน์โหลดไฟล์ ${format.toUpperCase()} เรียบร้อยแล้ว`);
                } else {
                    return response.json();
                }
            } else {
                throw new Error('Export failed');
            }
        })
        .then(data => {
            if (data) {
                showAlert('info', data.message || `กำลังเตรียมไฟล์ ${format.toUpperCase()}`);
            }
        })
        .catch(error => {
            setLoadingState(false);
            console.error('Export error:', error);
            showAlert('warning', 'ฟีเจอร์ Export กำลังพัฒนา กรุณารอการอัปเดต');
        });
    };

    /**
     * Bulk actions
     */
    window.bulkAction = function(action) {
        if (state.selectedRows.size === 0) {
            showAlert('warning', 'กรุณาเลือกรายการที่ต้องการดำเนินการ');
            return;
        }

        const actionLabels = {
            approve: 'อนุมัติ',
            reject: 'ปฏิเสธ',
            delete: 'ลบ'
        };

        const actionLabel = actionLabels[action] || action;
        const count = state.selectedRows.size;

        if (!confirm(`คุณต้องการ${actionLabel}รายการที่เลือก ${count} รายการหรือไม่?`)) {
            return;
        }

        setLoadingState(true, `กำลัง${actionLabel}รายการที่เลือก...`);

        // Simulate API call (implement actual endpoint)
        setTimeout(() => {
            setLoadingState(false);
            showAlert('info', `ฟีเจอร์ ${actionLabel} หลายรายการกำลังพัฒนา สำหรับ ${count} รายการ`);
        }, 2000);
    };

    /**
     * Refresh table
     */
    window.refreshTable = function() {
        console.log('DEBUG: refreshTable called - real-time updates always enabled');
        
        // Always show loading state
        setLoadingState(true, 'กำลังรีเฟรชข้อมูล...');
        
        // Always use real-time refresh strategy (no toggle)
        console.log('DEBUG: Using real-time refresh strategy');
            
            // Reset cursor to get latest data
            state.lastCursor = null;
            state.consecutiveErrors = 0;
            state.pollingInterval = CONFIG.POLLING_INITIAL_INTERVAL;
            
            // Fetch updates immediately with timeout
            Promise.race([
                fetchUpdates(),
                new Promise((_, reject) => 
                    setTimeout(() => reject(new Error('Fetch timeout')), 10000)
                )
            ])
            .then(() => {
                console.log('DEBUG: Real-time refresh successful');
                setLoadingState(false);
                showAlert('success', 'รีเฟรชข้อมูลเรียบร้อยแล้ว', { duration: 2000 });
                
                // Restart normal polling
                restartPolling();
            })
            .catch(error => {
                console.error('DEBUG: Real-time refresh failed:', error);
                console.log('DEBUG: Falling back to page reload');
                
                setLoadingState(false);
                showAlert('warning', 'กำลังรีโหลดหน้าเว็บเพื่อรีเฟรชข้อมูล...', { duration: 2000 });
                
                // Immediate fallback to page reload
                setTimeout(() => {
                    location.reload();
                }, 1000);
            });
        
        return true; // Return success for testing
    };

    // ==========================================================================
    // Keyboard Navigation & Accessibility
    // ==========================================================================
    
    function initializeKeyboardNavigation() {
        document.addEventListener('keydown', function(e) {
            // Ctrl/Cmd + F to focus search
            if ((e.ctrlKey || e.metaKey) && e.key === 'f') {
                e.preventDefault();
                const searchInput = document.querySelector(SELECTORS.searchInput);
                if (searchInput) {
                    searchInput.focus();
                    searchInput.select();
                }
            }

            // Ctrl/Cmd + R to refresh (override default)
            if ((e.ctrlKey || e.metaKey) && e.key === 'r') {
                e.preventDefault();
                refreshTable();
            }

            // Ctrl/Cmd + A to select all (in table context)
            if ((e.ctrlKey || e.metaKey) && e.key === 'a' && 
                e.target.closest(SELECTORS.tableContainer)) {
                e.preventDefault();
                const selectAll = document.querySelector(SELECTORS.selectAll);
                if (selectAll) {
                    selectAll.checked = true;
                    selectAll.dispatchEvent(new Event('change'));
                }
            }
        });

        // Enhanced focus management for table rows
        const actionButtons = document.querySelectorAll('.odd-action-buttons .btn, .btn');
        actionButtons.forEach(button => {
            button.addEventListener('focus', function() {
                const row = this.closest('tr');
                if (row) {
                    row.classList.add('odd-table-row-focused');
                }
            });

            button.addEventListener('blur', function() {
                const row = this.closest('tr');
                if (row) {
                    row.classList.remove('odd-table-row-focused');
                }
            });
        });
    }

    // ==========================================================================
    // Initialization
    // ==========================================================================
    
    function initialize() {
        // Wait for DOM to be fully loaded
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', initialize);
            return;
        }

        // Initialize all components
        initializeSelectionHandlers();
        initializeFilterHandlers();
        initializeOtacHandlers();
        initializeKeyboardNavigation();
        updateResponsiveView();

        // Initialize real-time updates system
        initializeRealTimeUpdates();

        // Handle window resize for responsive behavior
        let resizeTimeout;
        window.addEventListener('resize', function() {
            clearTimeout(resizeTimeout);
            resizeTimeout = setTimeout(updateResponsiveView, 250);
        });

        // Initialize tooltips if Bootstrap is available
        if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
            const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle=\"tooltip\"]'));
            tooltipTriggerList.map(function (tooltipTriggerEl) {
                return new bootstrap.Tooltip(tooltipTriggerEl);
            });
        }

        // Add event listeners to restart polling when filters change
        const filterForm = document.querySelector(SELECTORS.filterForm);
        if (filterForm) {
            filterForm.addEventListener('submit', function() {
                // Reset cursor and restart polling when filters change
                state.lastCursor = null;
                setTimeout(() => {
                    restartPolling();
                }, 1000); // Allow page to process filter first
            });
        }

        // Clean up on page unload
        window.addEventListener('beforeunload', function() {
            if (state.pollingTimeoutId) {
                clearTimeout(state.pollingTimeoutId);
                state.pollingTimeoutId = null;
            }
        });

        console.log('ODD Registration Modern UI with Real-time Updates initialized successfully');
    }

    // Auto-initialize
    initialize();

    // Ensure function is available globally
    if (typeof window.generateOtacImmediately !== 'function') {
        console.error('generateOtacImmediately not found on window object, re-assigning');
        window.generateOtacImmediately = function() {
            setLoadingState(true, 'กำลังสร้าง OTAC Code...');
            showAlert('info', 'กำลังสร้าง OTAC Code...', { duration: 2000 });
            
            const token = document.querySelector('input[name=\"__RequestVerificationToken\"]')?.value;
            const generateUrl = '/Admin/Otac/GenerateForKBankOdd';

            fetch(generateUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token || ''
                },
                body: `__RequestVerificationToken=${encodeURIComponent(token || '')}`
            })
            .then(response => {
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                return response.json();
            })
            .then(data => {
                setLoadingState(false);
                if (data.success) {
                    showOtacResultsModal(data);
                } else {
                    showAlert('danger', data.message || 'เกิดข้อผิดพลาดในการสร้าง OTAC Code');
                }
            })
            .catch(error => {
                setLoadingState(false);
                console.error('OTAC generation error:', error);
                showAlert('danger', `เกิดข้อผิดพลาดในการเชื่อมต่อ: ${error.message}`);
            });
        };
    }

    console.log('ODD Registration Modern UI: generateOtacImmediately function availability:', typeof window.generateOtacImmediately);

})();