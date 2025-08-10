/**
 * BizConnect ODD Registrations Enhanced UI JavaScript
 * Provides interactive features for the ODD registrations management interface
 */

document.addEventListener('DOMContentLoaded', function() {
    initializeEnhancedTable();
    initializeFilters();
    initializeTooltips();
    initializeKeyboardNavigation();
    animateTableRows();
});

/**
 * Initialize enhanced table functionality
 */
function initializeEnhancedTable() {
    // Enhanced select all functionality
    const selectAllCheckbox = document.getElementById('selectAll');
    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener('change', function() {
            const checkboxes = document.querySelectorAll('.row-checkbox');
            checkboxes.forEach(checkbox => {
                checkbox.checked = this.checked;
                updateRowSelection(checkbox.closest('tr'), this.checked);
            });
            updateBulkActionsVisibility();
        });
    }

    // Individual checkbox handling with visual feedback
    document.querySelectorAll('.row-checkbox').forEach(checkbox => {
        checkbox.addEventListener('change', function() {
            const row = this.closest('tr');
            updateRowSelection(row, this.checked);
            updateSelectAllState();
            updateBulkActionsVisibility();
        });
    });

    // Enhanced row hover effects
    document.querySelectorAll('.table tbody tr').forEach(row => {
        row.addEventListener('mouseenter', function() {
            this.style.transform = 'translateY(-2px)';
            this.style.boxShadow = '0 4px 12px rgba(37, 99, 235, 0.15)';
        });

        row.addEventListener('mouseleave', function() {
            if (!this.classList.contains('selected')) {
                this.style.transform = '';
                this.style.boxShadow = '';
            }
        });
    });
}

/**
 * Update row selection visual state
 */
function updateRowSelection(row, isSelected) {
    if (isSelected) {
        row.classList.add('selected');
        row.style.backgroundColor = 'rgba(37, 99, 235, 0.05)';
        row.style.borderLeft = '4px solid #2563eb';
        row.style.transform = 'translateY(-1px)';
        row.style.boxShadow = '0 2px 8px rgba(37, 99, 235, 0.1)';
    } else {
        row.classList.remove('selected');
        row.style.backgroundColor = '';
        row.style.borderLeft = '';
        row.style.transform = '';
        row.style.boxShadow = '';
    }
}

/**
 * Update select all checkbox state
 */
function updateSelectAllState() {
    const selectAll = document.getElementById('selectAll');
    const allCheckboxes = document.querySelectorAll('.row-checkbox');
    const checkedCheckboxes = document.querySelectorAll('.row-checkbox:checked');

    if (selectAll) {
        selectAll.indeterminate = checkedCheckboxes.length > 0 && checkedCheckboxes.length < allCheckboxes.length;
        selectAll.checked = checkedCheckboxes.length === allCheckboxes.length && allCheckboxes.length > 0;
    }
}

/**
 * Update bulk actions visibility
 */
function updateBulkActionsVisibility() {
    const checkedCount = document.querySelectorAll('.row-checkbox:checked').length;
    // This would control bulk action buttons visibility
    // Implementation depends on specific bulk action UI elements
}

/**
 * Initialize enhanced filters
 */
function initializeFilters() {
    const statusFilter = document.getElementById('status');
    const pageSizeFilter = document.getElementById('pageSize');
    const searchInput = document.getElementById('search');

    // Auto-submit on filter changes with loading indicator
    [statusFilter, pageSizeFilter].forEach(filter => {
        if (filter) {
            filter.addEventListener('change', function() {
                showLoadingState();
                document.getElementById('filterForm').submit();
            });
        }
    });

    // Enhanced search with debouncing
    if (searchInput) {
        let searchTimeout;
        searchInput.addEventListener('input', function() {
            clearTimeout(searchTimeout);
            const searchBtn = document.querySelector('#filterForm button[type="submit"]');
            
            // Show that search is being prepared
            if (searchBtn) {
                searchBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Searching...';
                searchBtn.disabled = true;
            }

            searchTimeout = setTimeout(() => {
                if (searchBtn) {
                    searchBtn.innerHTML = '<i class="fas fa-search me-1"></i>Filter';
                    searchBtn.disabled = false;
                }
            }, 500);
        });

        // Enter key handling
        searchInput.addEventListener('keypress', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                showLoadingState();
                document.getElementById('filterForm').submit();
            }
        });
    }
}

/**
 * Show loading state overlay
 */
function showLoadingState() {
    const dataTable = document.querySelector('.data-table');
    if (dataTable && !dataTable.querySelector('.loading-overlay')) {
        const loadingOverlay = document.createElement('div');
        loadingOverlay.className = 'loading-overlay';
        loadingOverlay.innerHTML = `
            <div class="loading-spinner"></div>
        `;
        dataTable.style.position = 'relative';
        dataTable.appendChild(loadingOverlay);
    }
}

/**
 * Initialize enhanced tooltips
 */
function initializeTooltips() {
    // Initialize Bootstrap tooltips
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function (tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });

    // Add custom tooltips for OTAC codes
    document.querySelectorAll('.otac-code').forEach(otacElement => {
        if (!otacElement.hasAttribute('title')) {
            otacElement.setAttribute('title', 'Click to copy OTAC code');
            otacElement.style.cursor = 'pointer';
            
            otacElement.addEventListener('click', function() {
                copyToClipboard(this.textContent);
                showToast('success', 'OTAC code copied to clipboard!');
            });
        }
    });

    // Add tooltips for status badges
    document.querySelectorAll('.badge').forEach(badge => {
        const text = badge.textContent.trim();
        if (text.includes('Generated')) {
            badge.setAttribute('title', 'OTAC code has been generated and is ready for use');
        } else if (text.includes('Validated')) {
            badge.setAttribute('title', 'OTAC code has been validated successfully');
        } else if (text.includes('Used')) {
            badge.setAttribute('title', 'OTAC code has been used for registration');
        } else if (text.includes('Expired')) {
            badge.setAttribute('title', 'OTAC code has expired and cannot be used');
        } else if (text.includes('Locked')) {
            badge.setAttribute('title', 'OTAC code is locked due to multiple failed attempts');
        }
    });
}

/**
 * Initialize enhanced keyboard navigation
 */
function initializeKeyboardNavigation() {
    document.addEventListener('keydown', function(e) {
        // Global keyboard shortcuts
        if (e.ctrlKey || e.metaKey) {
            switch(e.key) {
                case 'f':
                    e.preventDefault();
                    document.getElementById('search')?.focus();
                    break;
                case 'a':
                    if (e.shiftKey) {
                        e.preventDefault();
                        document.getElementById('selectAll')?.click();
                    }
                    break;
                case 'r':
                    e.preventDefault();
                    refreshTable();
                    break;
            }
        }

        // Escape key to clear search
        if (e.key === 'Escape') {
            const searchInput = document.getElementById('search');
            if (searchInput && document.activeElement === searchInput) {
                searchInput.value = '';
                showLoadingState();
                document.getElementById('filterForm').submit();
            }
        }
    });

    // Enhanced focus management for table rows
    document.querySelectorAll('.action-buttons .btn').forEach(button => {
        button.addEventListener('focus', function() {
            this.closest('tr').classList.add('table-row-focused');
        });

        button.addEventListener('blur', function() {
            this.closest('tr').classList.remove('table-row-focused');
        });
    });
}

/**
 * Animate table rows on load
 */
function animateTableRows() {
    const rows = document.querySelectorAll('.table tbody tr');
    rows.forEach((row, index) => {
        row.style.opacity = '0';
        row.style.transform = 'translateY(20px)';
        
        setTimeout(() => {
            row.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
            row.style.opacity = '1';
            row.style.transform = 'translateY(0)';
        }, index * 50);
    });
}

/**
 * Enhanced status update function
 */
function updateStatus(id, status) {
    const confirmMessage = `Are you sure you want to ${status.toLowerCase()} this registration?`;
    
    if (!confirm(confirmMessage)) {
        return;
    }

    // Show loading state on the specific row
    const row = document.querySelector(`input[value="${id}"]`)?.closest('tr');
    if (row) {
        row.style.opacity = '0.6';
        row.style.pointerEvents = 'none';
    }

    fetch(window.updateStatusUrl || '/Admin/OddRegistration/UpdateStatus', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        },
        body: `id=${id}&status=${status}`
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showToast('success', data.message || 'Status updated successfully');
            setTimeout(() => location.reload(), 1000);
        } else {
            showToast('danger', data.message || 'Failed to update status');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showToast('danger', 'An error occurred while updating the status.');
    })
    .finally(() => {
        if (row) {
            row.style.opacity = '1';
            row.style.pointerEvents = 'auto';
        }
    });
}

/**
 * Enhanced delete function
 */
function deleteRegistration(id) {
    const confirmMessage = 'Are you sure you want to delete this registration? This action cannot be undone.';
    
    if (!confirm(confirmMessage)) {
        return;
    }

    const row = document.querySelector(`input[value="${id}"]`)?.closest('tr');
    if (row) {
        row.style.opacity = '0.6';
        row.style.pointerEvents = 'none';
    }

    fetch(`${window.deleteUrl || '/Admin/OddRegistration/Delete'}/${id}`, {
        method: 'DELETE',
        headers: {
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        }
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showToast('success', data.message || 'Registration deleted successfully');
            
            // Animate row removal
            if (row) {
                row.style.transform = 'translateX(-100%)';
                setTimeout(() => {
                    row.remove();
                    updateSelectAllState();
                }, 300);
            }
        } else {
            showToast('danger', data.message || 'Failed to delete registration');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showToast('danger', 'An error occurred while deleting the registration.');
    })
    .finally(() => {
        if (row) {
            row.style.opacity = '1';
            row.style.pointerEvents = 'auto';
        }
    });
}

/**
 * Enhanced export function
 */
function exportData(format) {
    const button = event.target.closest('button');
    const originalContent = button.innerHTML;
    
    button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Exporting...';
    button.disabled = true;

    fetch(`${window.exportUrl || '/Admin/OddRegistration/Export'}?format=${format}`)
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            showToast('success', data.message || 'Export completed successfully');
        } else {
            showToast('info', data.message || 'Export functionality is being implemented');
        }
    })
    .catch(error => {
        console.error('Error:', error);
        showToast('danger', 'Export functionality is being implemented.');
    })
    .finally(() => {
        button.innerHTML = originalContent;
        button.disabled = false;
    });
}

/**
 * Enhanced OTAC generation
 */
function generateOtacImmediately() {
    console.log('generateOtacImmediately called (legacy version)');
    showToast('info', 'กำลังสร้าง OTAC Code...');

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
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
        if (data.success) {
            showOtacResultsModal(data);
        } else {
            showToast('danger', data.message || 'เกิดข้อผิดพลาดในการสร้าง OTAC Code');
        }
    })
    .catch(error => {
        console.error('OTAC generation error:', error);
        showToast('danger', `เกิดข้อผิดพลาดในการเชื่อมต่อ: ${error.message}`);
    });
}

/**
 * Enhanced OTAC results modal
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
                    <div class="modal-header bg-success text-white">
                        <h5 class="modal-title" id="otacResultsModalLabel">
                            <i class="fas fa-check-circle me-2"></i>🎯 OTAC สร้างสำเร็จ!
                        </h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body text-center">
                        <div class="alert alert-success border-0 shadow-sm">
                            <div class="mb-4">
                                <i class="fas fa-check-circle fa-4x text-success mb-3"></i>
                                <h4 class="text-success mb-2">OTAC Code พร้อมใช้งาน</h4>
                            </div>

                            <div class="code-display p-4 bg-white rounded border mb-4">
                                <label class="form-label small text-muted mb-2">รหัสยืนยัน</label>
                                <div class="input-group justify-content-center">
                                    <input type="text" id="resultGeneratedCode" class="form-control form-control-lg font-monospace fw-bold text-center"
                                           value="${data.code}" readonly style="font-size: 2rem; letter-spacing: 0.5em; background: #f8f9fa; max-width: 300px;">
                                    <button class="btn btn-outline-success" type="button" onclick="copyToClipboard('resultGeneratedCode')" title="คัดลอก">
                                        <i class="fas fa-copy"></i>
                                    </button>
                                </div>

                                <div class="mt-3">
                                    <small class="text-muted">หมดอายุ: ${expiryTimeLocal}</small>
                                    <div id="resultCountdown" class="small text-warning mt-1"></div>
                                </div>
                            </div>

                            <div class="text-muted">
                                <small><i class="fas fa-shield-alt me-1"></i>รหัสนี้ใช้ได้เพียงครั้งเดียว กรุณาเก็บรักษาให้ปลอดภัย</small>
                            </div>
                        </div>
                    </div>
                    <div class="modal-footer justify-content-center">
                        <button type="button" class="btn btn-success" onclick="copyToClipboard('resultGeneratedCode')">
                            <i class="fas fa-copy me-1"></i>คัดลอก
                        </button>
                        <button type="button" class="btn btn-outline-secondary" onclick="refreshTable()">
                            <i class="fas fa-sync-alt me-1"></i>รีเฟรช
                        </button>
                        <button type="button" class="btn btn-primary" data-bs-dismiss="modal">
                            <i class="fas fa-times me-1"></i>ปิด
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Remove existing modal if present
    const existingModal = document.getElementById('otacResultsModal');
    if (existingModal) {
        existingModal.remove();
    }

    // Add modal to body
    document.body.insertAdjacentHTML('beforeend', modalHtml);

    // Initialize and show modal
    const modal = new bootstrap.Modal(document.getElementById('otacResultsModal'));
    modal.show();

    // Start countdown
    startResultCountdown(data.expiresAt);

    // Clean up on modal close
    document.getElementById('otacResultsModal').addEventListener('hidden.bs.modal', function () {
        this.remove();
    });
}

/**
 * Copy to clipboard utility
 */
function copyToClipboard(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.select();
        element.setSelectionRange(0, 99999);

        try {
            document.execCommand('copy');
            showToast('success', 'คัดลอกรหัสเรียบร้อยแล้ว!');

            // Visual feedback
            const button = element.nextElementSibling;
            if (button) {
                const originalContent = button.innerHTML;
                button.innerHTML = '<i class="fas fa-check"></i>';
                button.classList.add('btn-success');
                button.classList.remove('btn-outline-success', 'btn-outline-secondary');

                setTimeout(() => {
                    button.innerHTML = originalContent;
                    button.classList.remove('btn-success');
                    button.classList.add('btn-outline-success');
                }, 2000);
            }
        } catch (err) {
            showToast('warning', 'ไม่สามารถคัดลอกอัตโนมัติได้ กรุณาคัดลอกด้วยตนเอง');
        }
    }
}

/**
 * Enhanced toast notification system
 */
function showToast(type, message) {
    const toastContainer = document.getElementById('toast-container') || createToastContainer();
    
    const toastId = 'toast-' + Date.now();
    const toastHtml = `
        <div id="${toastId}" class="toast show" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="toast-header bg-${type} text-white">
                <i class="fas fa-${getToastIcon(type)} me-2"></i>
                <strong class="me-auto">${getToastTitle(type)}</strong>
                <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
            </div>
            <div class="toast-body">
                ${message}
            </div>
        </div>
    `;
    
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement, { delay: 5000 });
    toast.show();
    
    // Remove toast element after it's hidden
    toastElement.addEventListener('hidden.bs.toast', function() {
        this.remove();
    });
}

function createToastContainer() {
    const container = document.createElement('div');
    container.id = 'toast-container';
    container.className = 'toast-container position-fixed top-0 end-0 p-3';
    container.style.zIndex = '1055';
    document.body.appendChild(container);
    return container;
}

function getToastIcon(type) {
    const icons = {
        'success': 'check-circle',
        'danger': 'exclamation-triangle',
        'warning': 'exclamation-circle',
        'info': 'info-circle'
    };
    return icons[type] || 'info-circle';
}

function getToastTitle(type) {
    const titles = {
        'success': 'Success',
        'danger': 'Error',
        'warning': 'Warning',
        'info': 'Information'
    };
    return titles[type] || 'Notification';
}

/**
 * Countdown timer for OTAC expiration
 */
let resultCountdownInterval;
function startResultCountdown(expiresAt) {
    const expiryDate = new Date(expiresAt);
    const countdownElement = document.getElementById('resultCountdown');

    if (!countdownElement) return;

    if (resultCountdownInterval) {
        clearInterval(resultCountdownInterval);
    }

    resultCountdownInterval = setInterval(() => {
        const now = new Date();
        const timeLeft = expiryDate - now;

        if (timeLeft <= 0) {
            countdownElement.innerHTML = '<span class="text-danger"><i class="fas fa-exclamation-triangle me-1"></i>หมดอายุแล้ว</span>';
            clearInterval(resultCountdownInterval);
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

        countdownElement.innerHTML = `<span class="${cssClass}"><i class="${icon} me-1"></i>เหลือ ${minutes} นาที ${seconds} วินาที</span>';
    }, 1000);
}

/**
 * Refresh table function
 */
function refreshTable() {
    showLoadingState();
    location.reload();
}

/**
 * Bulk actions handler
 */
function bulkAction(action) {
    const checkedBoxes = document.querySelectorAll('.row-checkbox:checked');
    if (checkedBoxes.length === 0) {
        showToast('warning', 'Please select at least one registration.');
        return;
    }

    const ids = Array.from(checkedBoxes).map(cb => cb.value);
    showToast('info', `Bulk ${action} functionality will be implemented for ${ids.length} registrations.`);
}

// Export functions for global access
window.updateStatus = updateStatus;
window.deleteRegistration = deleteRegistration;
window.exportData = exportData;
window.generateOtacImmediately = generateOtacImmediately;
window.refreshTable = refreshTable;
window.bulkAction = bulkAction;
window.copyToClipboard = copyToClipboard;