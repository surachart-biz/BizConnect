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
        ANIMATION_DURATION: 300
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
        currentView: 'desktop' // 'desktop' or 'mobile'
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
     * Generate OTAC immediately
     */
    window.generateOtacImmediately = function() {
        console.log('generateOtacImmediately called');
        
        // Prevent multiple clicks
        if (state.isLoading) {
            console.log('Already generating OTAC, ignoring click');
            return;
        }
        
        setLoadingState(true, 'กำลังสร้าง OTAC Code...');
        showAlert('info', 'กำลังสร้าง OTAC Code...', { duration: 2000 });
        
        const token = document.querySelector('input[name=\"__RequestVerificationToken\"]')?.value;
        const generateUrl = '/Admin/Otac/GenerateForKBankOdd';

        console.log('Token found:', !!token);
        console.log('Generate URL:', generateUrl);

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
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            return response.json();
        })
        .then(data => {
            console.log('Response data:', data);
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

    /**
     * Show OTAC result in modern modal
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
                <div class="modal-dialog modal-lg">
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
                                        <div class="input-group justify-content-center">
                                            <input type="text" id="resultGeneratedCode" 
                                                   class="form-control form-control-lg font-monospace fw-bold text-center border-success"
                                                   value="${data.code}" readonly 
                                                   style="font-size: 1.8rem; letter-spacing: 0.3em; max-width: 280px; background: var(--odd-surface);">
                                            <button class="btn btn-outline-success" type="button" 
                                                    onclick="copyOtacCode('resultGeneratedCode')" title="คัดลอก">
                                                <i class="fas fa-copy"></i>
                                            </button>
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
                        <div class="modal-footer justify-content-center gap-2">
                            <button type="button" class="btn btn-success" onclick="copyOtacCode('resultGeneratedCode')">
                                <i class="fas fa-copy me-1"></i>คัดลอก
                            </button>
                            <button type="button" class="btn btn-outline-primary" onclick="refreshTable()">
                                <i class="fas fa-sync-alt me-1"></i>รีเฟรชตาราง
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

        // Cleanup on close
        document.getElementById('otacResultsModal').addEventListener('hidden.bs.modal', function() {
            this.remove();
        });
    }

    /**
     * Copy OTAC code with enhanced feedback
     */
    window.copyOtacCode = function(elementId) {
        const element = document.getElementById(elementId);
        if (!element) return;

        copyToClipboard(element.value, element);

        // Visual feedback for button
        const button = element.nextElementSibling;
        if (button) {
            const originalContent = button.innerHTML;
            button.innerHTML = '<i class=\"fas fa-check\"></i>';
            button.classList.add('btn-success');
            button.classList.remove('btn-outline-success');

            setTimeout(() => {
                button.innerHTML = originalContent;
                button.classList.remove('btn-success');
                button.classList.add('btn-outline-success');
            }, 2000);
        }
    };

    /**
     * OTAC countdown timer
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
        setLoadingState(true, 'กำลังรีเฟรชข้อมูล...');
        location.reload();
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

        console.log('ODD Registration Modern UI initialized successfully');
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