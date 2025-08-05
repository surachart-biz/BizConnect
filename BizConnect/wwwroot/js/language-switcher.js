/**
 * BizConnect Language Switcher
 * Handles client-side language switching functionality
 */

class LanguageSwitcher {
    constructor() {
        this.currentLanguage = this.getCurrentLanguage();
        this.init();
    }

    init() {
        this.bindEvents();
        this.updateLanguageDisplay();
    }

    getCurrentLanguage() {
        // Get current language from cookie
        const cookies = document.cookie.split(';');
        for (let cookie of cookies) {
            const [name, value] = cookie.trim().split('=');
            if (name === '.AspNetCore.Culture') {
                const cultureValue = decodeURIComponent(value);
                const match = cultureValue.match(/c=([^|]+)/);
                if (match) {
                    return match[1].startsWith('th') ? 'th' : 'en';
                }
            }
        }
        return 'en'; // Default to English
    }

    bindEvents() {
        // Handle language toggle button clicks
        document.addEventListener('click', (e) => {
            if (e.target.closest('[data-culture]')) {
                const cultureButton = e.target.closest('[data-culture]');
                const culture = cultureButton.getAttribute('data-culture');
                this.switchLanguage(culture);
            }
        });

        // Handle form submissions for language switching
        document.addEventListener('submit', (e) => {
            if (e.target.closest('form[action*="SetCulture"]')) {
                this.showLoadingState(e.target);
            }
        });
    }

    switchLanguage(culture) {
        // Store preference in localStorage
        localStorage.setItem('preferredCulture', culture);
        
        // Show loading state
        this.showLanguageChangeLoading();
        
        // Submit form programmatically if available
        const form = document.querySelector(`form input[value="${culture}"]`)?.closest('form');
        if (form) {
            form.submit();
        }
    }

    showLoadingState(form) {
        const button = form.querySelector('button[type="submit"]');
        if (button) {
            const originalText = button.innerHTML;
            button.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Changing...';
            button.disabled = true;
        }
    }

    showLanguageChangeLoading() {
        // Show a subtle loading indicator
        const loadingToast = this.createLoadingToast();
        document.body.appendChild(loadingToast);
        
        // Auto-remove after 3 seconds if page doesn't reload
        setTimeout(() => {
            if (loadingToast.parentNode) {
                loadingToast.remove();
            }
        }, 3000);
    }

    createLoadingToast() {
        const toast = document.createElement('div');
        toast.className = 'toast show position-fixed top-0 end-0 m-3';
        toast.style.zIndex = '1060';
        toast.innerHTML = `
            <div class="toast-header bg-primary text-white">
                <i class="fas fa-language me-2"></i>
                <strong class="me-auto">Language</strong>
                <small>Now</small>
            </div>
            <div class="toast-body">
                <div class="d-flex align-items-center">
                    <div class="spinner-border spinner-border-sm me-2" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <span>Switching language...</span>
                </div>
            </div>
        `;
        return toast;
    }

    updateLanguageDisplay() {
        // Update any dynamic language-dependent content
        const languageElements = document.querySelectorAll('[data-lang-key]');
        languageElements.forEach(element => {
            const key = element.getAttribute('data-lang-key');
            const text = this.getTranslation(key);
            if (text) {
                element.textContent = text;
            }
        });
    }

    getTranslation(key) {
        // Basic translation map for common UI elements
        const translations = {
            'en': {
                'loading': 'Loading...',
                'error': 'Error',
                'success': 'Success',
                'cancel': 'Cancel',
                'confirm': 'Confirm',
                'save': 'Save',
                'delete': 'Delete',
                'edit': 'Edit',
                'view': 'View',
                'close': 'Close'
            },
            'th': {
                'loading': 'กำลังโหลด...',
                'error': 'เกิดข้อผิดพลาด',
                'success': 'สำเร็จ',
                'cancel': 'ยกเลิก',
                'confirm': 'ยืนยัน',
                'save': 'บันทึก',
                'delete': 'ลบ',
                'edit': 'แก้ไข',
                'view': 'ดู',
                'close': 'ปิด'
            }
        };

        return translations[this.currentLanguage]?.[key] || null;
    }

    // Utility method to format dates based on current language/culture
    formatDate(date, options = {}) {
        const dateObj = date instanceof Date ? date : new Date(date);
        
        if (this.currentLanguage === 'th') {
            // Thai Buddhist calendar format
            const thaiYear = dateObj.getFullYear() + 543;
            const thaiOptions = {
                year: 'numeric',
                month: 'long',
                day: 'numeric',
                ...options
            };
            
            // Use Thai locale if available
            try {
                return new Intl.DateTimeFormat('th-TH-u-ca-buddhist', thaiOptions).format(dateObj);
            } catch (e) {
                // Fallback to manual formatting
                const months = [
                    'มกราคม', 'กุมภาพันธ์', 'มีนาคม', 'เมษายน', 'พฤษภาคม', 'มิถุนายน',
                    'กรกฎาคม', 'สิงหาคม', 'กันยายน', 'ตุลาคม', 'พฤศจิกายน', 'ธันวาคม'
                ];
                return `${dateObj.getDate()} ${months[dateObj.getMonth()]} ${thaiYear}`;
            }
        } else {
            // English format
            const englishOptions = {
                year: 'numeric',
                month: 'long',
                day: 'numeric',
                ...options
            };
            return new Intl.DateTimeFormat('en-US', englishOptions).format(dateObj);
        }
    }

    // Method to update all dates on the page
    updateDateDisplays() {
        const dateElements = document.querySelectorAll('[data-date]');
        dateElements.forEach(element => {
            const dateValue = element.getAttribute('data-date');
            if (dateValue) {
                const formattedDate = this.formatDate(dateValue);
                element.textContent = formattedDate;
            }
        });
    }
}

// Initialize language switcher when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    window.languageSwitcher = new LanguageSwitcher();
});

// Export for use in other scripts
if (typeof module !== 'undefined' && module.exports) {
    module.exports = LanguageSwitcher;
}