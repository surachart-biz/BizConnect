/**
 * Admin Tables Module
 * Advanced data table functionality with sorting, filtering, and pagination
 */

class AdminTables extends AdminBaseModule {
    constructor(core) {
        super(core);
        this.tables = new Map();
        this.defaultConfig = {
            pageSize: 10,
            sortable: true,
            filterable: true,
            selectable: false,
            responsive: true,
            showEmpty: true,
            emptyMessage: 'No data available'
        };
    }

    init() {
        super.init();
        
        this.initializeTables();
        this.setupGlobalFilters();
        this.bindEvents();
        
        this.debug('Tables module initialized');
    }

    /**
     * Initialize all tables on the page
     */
    initializeTables() {
        const tables = document.querySelectorAll('.admin-table, .table-modern, .table-enhanced');
        
        tables.forEach((table, index) => {
            const config = this.getTableConfig(table);
            const tableId = table.id || `table-${index}`;
            
            const tableInstance = new AdminTable(table, config, this);
            this.tables.set(tableId, tableInstance);
        });
    }

    /**
     * Get table configuration from data attributes
     */
    getTableConfig(table) {
        const config = { ...this.defaultConfig };
        
        // Read data attributes
        const dataset = table.dataset;
        
        if (dataset.pageSize) config.pageSize = parseInt(dataset.pageSize);
        if (dataset.sortable) config.sortable = dataset.sortable !== 'false';
        if (dataset.filterable) config.filterable = dataset.filterable !== 'false';
        if (dataset.selectable) config.selectable = dataset.selectable === 'true';
        if (dataset.responsive) config.responsive = dataset.responsive !== 'false';
        if (dataset.emptyMessage) config.emptyMessage = dataset.emptyMessage;
        
        return config;
    }

    /**
     * Setup global filters
     */
    setupGlobalFilters() {
        const globalSearch = document.querySelector('.global-table-search');
        if (globalSearch) {
            globalSearch.addEventListener('input', this.debounce((e) => {
                this.applyGlobalFilter(e.target.value);
            }, 300));
        }
    }

    /**
     * Bind events
     */
    bindEvents() {
        // Listen for table refresh events
        this.on('table:refresh', (event) => {
            const { tableId } = event.detail;
            this.refreshTable(tableId);
        });
        
        // Listen for export events
        this.on('table:export', (event) => {
            const { tableId, format } = event.detail;
            this.exportTable(tableId, format);
        });
    }

    /**
     * Apply global filter to all tables
     */
    applyGlobalFilter(searchTerm) {
        this.tables.forEach(table => {
            table.filter(searchTerm);
        });
    }

    /**
     * Refresh a specific table
     */
    async refreshTable(tableId) {
        const table = this.tables.get(tableId);
        if (table) {
            await table.refresh();
        }
    }

    /**
     * Export table data
     */
    exportTable(tableId, format = 'csv') {
        const table = this.tables.get(tableId);
        if (table) {
            table.export(format);
        }
    }

    /**
     * Add a new table programmatically
     */
    addTable(element, config = {}) {
        const tableConfig = { ...this.defaultConfig, ...config };
        const tableId = element.id || `table-${this.tables.size}`;
        
        const tableInstance = new AdminTable(element, tableConfig, this);
        this.tables.set(tableId, tableInstance);
        
        return tableInstance;
    }

    /**
     * Get table instance
     */
    getTable(tableId) {
        return this.tables.get(tableId);
    }

    /**
     * Cleanup
     */
    cleanup() {
        super.cleanup();
        this.tables.forEach(table => table.cleanup());
        this.tables.clear();
    }
}

/**
 * Individual Table Class
 */
class AdminTable {
    constructor(element, config, module) {
        this.element = element;
        this.config = config;
        this.module = module;
        this.tbody = element.querySelector('tbody');
        this.thead = element.querySelector('thead');
        
        this.data = [];
        this.filteredData = [];
        this.currentPage = 1;
        this.sortColumn = null;
        this.sortDirection = 'asc';
        this.selectedRows = new Set();
        
        this.init();
    }

    init() {
        this.extractData();
        this.setupSorting();
        this.setupFiltering();
        this.setupPagination();
        this.setupSelection();
        this.setupResponsive();
        this.render();
    }

    /**
     * Extract data from table
     */
    extractData() {
        const rows = this.tbody.querySelectorAll('tr');
        
        this.data = Array.from(rows).map((row, index) => {
            const cells = row.querySelectorAll('td');
            const rowData = {
                id: row.dataset.id || index,
                element: row,
                cells: Array.from(cells).map(cell => ({
                    text: cell.textContent.trim(),
                    html: cell.innerHTML,
                    element: cell,
                    sortValue: cell.dataset.sort || cell.textContent.trim()
                }))
            };
            return rowData;
        });
        
        this.filteredData = [...this.data];
    }

    /**
     * Setup sorting functionality
     */
    setupSorting() {
        if (!this.config.sortable) return;
        
        const headers = this.thead.querySelectorAll('th[data-sort]');
        
        headers.forEach((header, index) => {
            header.style.cursor = 'pointer';
            header.classList.add('sortable');
            
            // Add sort icon
            if (!header.querySelector('.sort-icon')) {
                const icon = document.createElement('i');
                icon.className = 'fas fa-sort sort-icon ms-2 text-muted';
                header.appendChild(icon);
            }
            
            header.addEventListener('click', () => {
                this.sort(index, header);
            });
        });
    }

    /**
     * Setup filtering functionality
     */
    setupFiltering() {
        if (!this.config.filterable) return;
        
        // Column-specific filters
        const filterRow = this.thead.querySelector('.filter-row');
        if (filterRow) {
            const filterInputs = filterRow.querySelectorAll('input, select');
            
            filterInputs.forEach((input, index) => {
                input.addEventListener('input', this.debounce(() => {
                    this.filterByColumn(index, input.value);
                }, 300));
            });
        }
        
        // Global table filter
        const tableFilter = this.element.parentElement.querySelector('.table-filter');
        if (tableFilter) {
            tableFilter.addEventListener('input', this.debounce((e) => {
                this.filter(e.target.value);
            }, 300));
        }
    }

    /**
     * Setup pagination
     */
    setupPagination() {
        if (this.data.length <= this.config.pageSize) return;
        
        this.createPaginationUI();
    }

    /**
     * Setup row selection
     */
    setupSelection() {
        if (!this.config.selectable) return;
        
        // Add master checkbox
        if (this.thead && !this.thead.querySelector('.master-checkbox')) {
            const headerRow = this.thead.querySelector('tr');
            const th = document.createElement('th');
            th.innerHTML = '<input type="checkbox" class="form-check-input master-checkbox">';
            th.style.width = '40px';
            headerRow.insertBefore(th, headerRow.firstChild);
            
            const masterCheckbox = th.querySelector('.master-checkbox');
            masterCheckbox.addEventListener('change', () => {
                this.selectAll(masterCheckbox.checked);
            });
        }
        
        // Add row checkboxes
        this.data.forEach(row => {
            if (!row.element.querySelector('.row-checkbox')) {
                const td = document.createElement('td');
                td.innerHTML = `<input type="checkbox" class="form-check-input row-checkbox" data-id="${row.id}">`;
                row.element.insertBefore(td, row.element.firstChild);
                
                const checkbox = td.querySelector('.row-checkbox');
                checkbox.addEventListener('change', () => {
                    this.selectRow(row.id, checkbox.checked);
                });
            }
        });
    }

    /**
     * Setup responsive behavior
     */
    setupResponsive() {
        if (!this.config.responsive) return;
        
        // Add responsive wrapper if needed
        if (!this.element.closest('.table-responsive')) {
            const wrapper = document.createElement('div');
            wrapper.className = 'table-responsive';
            this.element.parentNode.insertBefore(wrapper, this.element);
            wrapper.appendChild(this.element);
        }
        
        // Add mobile-friendly attributes
        if (window.innerWidth <= 768) {
            this.addMobileLabels();
        }
        
        window.addEventListener('resize', this.debounce(() => {
            if (window.innerWidth <= 768) {
                this.addMobileLabels();
            } else {
                this.removeMobileLabels();
            }
        }, 250));
    }

    /**
     * Sort table by column
     */
    sort(columnIndex, headerElement) {
        const currentDirection = headerElement.dataset.sort || 'asc';
        const newDirection = currentDirection === 'asc' ? 'desc' : 'asc';
        
        // Clear all sort indicators
        this.thead.querySelectorAll('.sort-icon').forEach(icon => {
            icon.className = 'fas fa-sort sort-icon ms-2 text-muted';
        });
        
        // Update current column indicator
        const icon = headerElement.querySelector('.sort-icon');
        icon.className = `fas fa-sort-${newDirection === 'asc' ? 'up' : 'down'} sort-icon ms-2 text-primary`;
        headerElement.dataset.sort = newDirection;
        
        // Sort data
        this.filteredData.sort((a, b) => {
            const aValue = a.cells[columnIndex]?.sortValue || '';
            const bValue = b.cells[columnIndex]?.sortValue || '';
            
            // Try numeric comparison
            const aNum = parseFloat(aValue);
            const bNum = parseFloat(bValue);
            
            if (!isNaN(aNum) && !isNaN(bNum)) {
                return newDirection === 'asc' ? aNum - bNum : bNum - aNum;
            }
            
            // String comparison
            const comparison = aValue.localeCompare(bValue);
            return newDirection === 'asc' ? comparison : -comparison;
        });
        
        this.sortColumn = columnIndex;
        this.sortDirection = newDirection;
        this.currentPage = 1;
        
        this.render();
        this.module.emit('table:sorted', {
            tableId: this.element.id,
            column: columnIndex,
            direction: newDirection
        });
    }

    /**
     * Filter table data
     */
    filter(searchTerm) {
        if (!searchTerm) {
            this.filteredData = [...this.data];
        } else {
            const term = searchTerm.toLowerCase();
            this.filteredData = this.data.filter(row => {
                return row.cells.some(cell => 
                    cell.text.toLowerCase().includes(term)
                );
            });
        }
        
        this.currentPage = 1;
        this.render();
        this.updatePaginationUI();
        
        this.module.emit('table:filtered', {
            tableId: this.element.id,
            searchTerm,
            resultCount: this.filteredData.length
        });
    }

    /**
     * Filter by specific column
     */
    filterByColumn(columnIndex, value) {
        if (!value) {
            this.filteredData = [...this.data];
        } else {
            const searchValue = value.toLowerCase();
            this.filteredData = this.data.filter(row => {
                const cellValue = row.cells[columnIndex]?.text.toLowerCase() || '';
                return cellValue.includes(searchValue);
            });
        }
        
        this.currentPage = 1;
        this.render();
        this.updatePaginationUI();
    }

    /**
     * Select/deselect all rows
     */
    selectAll(selected) {
        if (selected) {
            this.filteredData.forEach(row => {
                this.selectedRows.add(row.id);
                const checkbox = row.element.querySelector('.row-checkbox');
                if (checkbox) checkbox.checked = true;
                row.element.classList.add('selected');
            });
        } else {
            this.selectedRows.clear();
            this.filteredData.forEach(row => {
                const checkbox = row.element.querySelector('.row-checkbox');
                if (checkbox) checkbox.checked = false;
                row.element.classList.remove('selected');
            });
        }
        
        this.module.emit('table:selection-changed', {
            tableId: this.element.id,
            selectedRows: Array.from(this.selectedRows)
        });
    }

    /**
     * Select/deselect individual row
     */
    selectRow(rowId, selected) {
        if (selected) {
            this.selectedRows.add(rowId);
        } else {
            this.selectedRows.delete(rowId);
        }
        
        const row = this.data.find(r => r.id === rowId);
        if (row) {
            row.element.classList.toggle('selected', selected);
        }
        
        // Update master checkbox
        this.updateMasterCheckbox();
        
        this.module.emit('table:row-selected', {
            tableId: this.element.id,
            rowId,
            selected,
            selectedCount: this.selectedRows.size
        });
    }

    /**
     * Update master checkbox state
     */
    updateMasterCheckbox() {
        const masterCheckbox = this.element.querySelector('.master-checkbox');
        if (!masterCheckbox) return;
        
        const visibleRows = this.getVisibleRows();
        const selectedVisibleCount = visibleRows.filter(row => 
            this.selectedRows.has(row.id)
        ).length;
        
        masterCheckbox.checked = selectedVisibleCount === visibleRows.length && visibleRows.length > 0;
        masterCheckbox.indeterminate = selectedVisibleCount > 0 && selectedVisibleCount < visibleRows.length;
    }

    /**
     * Get currently visible rows
     */
    getVisibleRows() {
        const start = (this.currentPage - 1) * this.config.pageSize;
        const end = start + this.config.pageSize;
        return this.filteredData.slice(start, end);
    }

    /**
     * Create pagination UI
     */
    createPaginationUI() {
        let paginationContainer = this.element.parentElement.querySelector('.table-pagination');
        
        if (!paginationContainer) {
            paginationContainer = document.createElement('div');
            paginationContainer.className = 'table-pagination d-flex justify-content-between align-items-center mt-3';
            this.element.parentElement.appendChild(paginationContainer);
        }
        
        this.paginationContainer = paginationContainer;
        this.updatePaginationUI();
    }

    /**
     * Update pagination UI
     */
    updatePaginationUI() {
        if (!this.paginationContainer) return;
        
        const totalPages = Math.ceil(this.filteredData.length / this.config.pageSize);
        const start = (this.currentPage - 1) * this.config.pageSize + 1;
        const end = Math.min(start + this.config.pageSize - 1, this.filteredData.length);
        
        this.paginationContainer.innerHTML = `
            <div class="pagination-info">
                Showing ${start} to ${end} of ${this.filteredData.length} entries
                ${this.data.length !== this.filteredData.length ? `(filtered from ${this.data.length} total)` : ''}
            </div>
            <nav aria-label="Table pagination">
                <ul class="pagination pagination-sm mb-0">
                    <li class="page-item ${this.currentPage === 1 ? 'disabled' : ''}">
                        <button class="page-link" data-page="prev" ${this.currentPage === 1 ? 'disabled' : ''}>
                            <i class="fas fa-chevron-left"></i>
                        </button>
                    </li>
                    ${this.generatePageNumbers(totalPages)}
                    <li class="page-item ${this.currentPage === totalPages ? 'disabled' : ''}">
                        <button class="page-link" data-page="next" ${this.currentPage === totalPages ? 'disabled' : ''}>
                            <i class="fas fa-chevron-right"></i>
                        </button>
                    </li>
                </ul>
            </nav>
        `;
        
        // Bind pagination events
        this.paginationContainer.addEventListener('click', (e) => {
            if (e.target.closest('.page-link')) {
                e.preventDefault();
                const page = e.target.closest('.page-link').dataset.page;
                this.handlePagination(page);
            }
        });
    }

    /**
     * Generate page numbers for pagination
     */
    generatePageNumbers(totalPages) {
        if (totalPages <= 1) return '';
        
        let pages = [];
        const current = this.currentPage;
        
        // Always show first page
        if (current > 3) {
            pages.push(1);
            if (current > 4) {
                pages.push('...');
            }
        }
        
        // Show pages around current page
        for (let i = Math.max(1, current - 2); i <= Math.min(totalPages, current + 2); i++) {
            pages.push(i);
        }
        
        // Always show last page
        if (current < totalPages - 2) {
            if (current < totalPages - 3) {
                pages.push('...');
            }
            pages.push(totalPages);
        }
        
        return pages.map(page => {
            if (page === '...') {
                return `<li class="page-item disabled"><span class="page-link">...</span></li>`;
            }
            
            return `
                <li class="page-item ${page === current ? 'active' : ''}">
                    <button class="page-link" data-page="${page}">${page}</button>
                </li>
            `;
        }).join('');
    }

    /**
     * Handle pagination clicks
     */
    handlePagination(page) {
        const totalPages = Math.ceil(this.filteredData.length / this.config.pageSize);
        
        switch (page) {
            case 'prev':
                this.currentPage = Math.max(1, this.currentPage - 1);
                break;
            case 'next':
                this.currentPage = Math.min(totalPages, this.currentPage + 1);
                break;
            default:
                this.currentPage = parseInt(page);
                break;
        }
        
        this.render();
        this.updatePaginationUI();
    }

    /**
     * Render table
     */
    render() {
        const visibleRows = this.getVisibleRows();
        
        // Hide all rows
        this.data.forEach(row => {
            row.element.style.display = 'none';
        });
        
        if (visibleRows.length === 0) {
            this.showEmptyState();
        } else {
            this.hideEmptyState();
            // Show visible rows
            visibleRows.forEach(row => {
                row.element.style.display = '';
            });
        }
        
        this.updateMasterCheckbox();
    }

    /**
     * Show empty state
     */
    showEmptyState() {
        let emptyRow = this.tbody.querySelector('.empty-state-row');
        
        if (!emptyRow) {
            emptyRow = document.createElement('tr');
            emptyRow.className = 'empty-state-row';
            
            const colCount = this.thead.querySelectorAll('th').length;
            emptyRow.innerHTML = `
                <td colspan="${colCount}" class="text-center py-5 text-muted">
                    <i class="fas fa-inbox fa-3x mb-3 d-block opacity-25"></i>
                    ${this.config.emptyMessage}
                </td>
            `;
            
            this.tbody.appendChild(emptyRow);
        }
        
        emptyRow.style.display = '';
    }

    /**
     * Hide empty state
     */
    hideEmptyState() {
        const emptyRow = this.tbody.querySelector('.empty-state-row');
        if (emptyRow) {
            emptyRow.style.display = 'none';
        }
    }

    /**
     * Add mobile labels for responsive tables
     */
    addMobileLabels() {
        const headers = Array.from(this.thead.querySelectorAll('th')).map(th => 
            th.textContent.trim()
        );
        
        this.data.forEach(row => {
            const cells = row.element.querySelectorAll('td');
            cells.forEach((cell, index) => {
                if (headers[index]) {
                    cell.setAttribute('data-label', headers[index]);
                }
            });
        });
        
        this.element.classList.add('table-mobile-labels');
    }

    /**
     * Remove mobile labels
     */
    removeMobileLabels() {
        this.data.forEach(row => {
            const cells = row.element.querySelectorAll('td');
            cells.forEach(cell => {
                cell.removeAttribute('data-label');
            });
        });
        
        this.element.classList.remove('table-mobile-labels');
    }

    /**
     * Export table data
     */
    export(format = 'csv') {
        const data = this.filteredData.map(row => 
            row.cells.map(cell => cell.text)
        );
        
        const headers = Array.from(this.thead.querySelectorAll('th')).map(th => 
            th.textContent.trim()
        );
        
        switch (format.toLowerCase()) {
            case 'csv':
                this.exportCSV([headers, ...data]);
                break;
            case 'json':
                this.exportJSON(data, headers);
                break;
            default:
                console.warn('Unsupported export format:', format);
        }
    }

    /**
     * Export as CSV
     */
    exportCSV(data) {
        const csvContent = data.map(row => 
            row.map(cell => `"${cell.replace(/"/g, '""')}"`).join(',')
        ).join('\n');
        
        this.downloadFile(csvContent, 'table-export.csv', 'text/csv');
    }

    /**
     * Export as JSON
     */
    exportJSON(data, headers) {
        const jsonData = data.map(row => {
            const obj = {};
            headers.forEach((header, index) => {
                obj[header] = row[index];
            });
            return obj;
        });
        
        const jsonContent = JSON.stringify(jsonData, null, 2);
        this.downloadFile(jsonContent, 'table-export.json', 'application/json');
    }

    /**
     * Download file
     */
    downloadFile(content, filename, contentType) {
        const blob = new Blob([content], { type: contentType });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        
        window.URL.revokeObjectURL(url);
        document.body.removeChild(a);
    }

    /**
     * Refresh table data
     */
    async refresh() {
        // If there's a data source URL, fetch new data
        const dataSource = this.element.dataset.source;
        if (dataSource) {
            try {
                const response = await fetch(dataSource);
                const html = await response.text();
                
                // Update table content
                this.tbody.innerHTML = html;
                this.extractData();
                this.render();
                
                this.module.emit('table:refreshed', {
                    tableId: this.element.id
                });
            } catch (error) {
                console.error('Failed to refresh table:', error);
            }
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

    /**
     * Cleanup
     */
    cleanup() {
        if (this.paginationContainer) {
            this.paginationContainer.remove();
        }
    }
}

// Export for module registration
window.AdminTables = AdminTables;