/**
 * Visual Feedback System for BizConnect
 * Provides advanced visual feedback animations including success states,
 * error handling, copy feedback, and real-time update highlighting
 */

class VisualFeedbackSystem {
    constructor() {
        this.activeAnimations = new Set();
        this.notificationQueue = [];
        this.isProcessingQueue = false;
        this.init();
    }

    init() {
        this.injectStyles();
        this.setupGlobalEventListeners();
        this.initializeToastContainer();
    }

    injectStyles() {
        if (document.getElementById('visual-feedback-styles')) return;

        const styles = `
            <style id="visual-feedback-styles">
                /* Success Animations */
                .success-animation {
                    position: fixed;
                    top: 50%;
                    left: 50%;
                    transform: translate(-50%, -50%);
                    z-index: 9999;
                    pointer-events: none;
                }

                .success-checkmark {
                    width: 80px;
                    height: 80px;
                    border-radius: 50%;
                    display: block;
                    stroke-width: 3;
                    stroke: var(--success);
                    stroke-miterlimit: 10;
                    background: white;
                    box-shadow: 0 0 0 4px var(--success);
                    animation: success-scale 0.3s ease-in-out 0.9s both;
                    margin: 0 auto;
                }

                .success-checkmark-circle {
                    stroke-dasharray: 166;
                    stroke-dashoffset: 166;
                    stroke-width: 3;
                    stroke-miterlimit: 10;
                    stroke: var(--success);
                    fill: white;
                    animation: success-stroke 0.6s cubic-bezier(0.65, 0, 0.45, 1) forwards;
                }

                .success-checkmark-check {
                    transform-origin: 50% 50%;
                    stroke-dasharray: 48;
                    stroke-dashoffset: 48;
                    animation: success-stroke 0.3s cubic-bezier(0.65, 0, 0.45, 1) 0.8s forwards;
                }

                /* Error Shake Animations */
                .error-shake-element {
                    animation: error-shake-advanced 0.6s cubic-bezier(0.36, 0.07, 0.19, 0.97);
                }

                @keyframes error-shake-advanced {
                    0% { transform: translate3d(0, 0, 0) rotate(0deg); }
                    10%, 90% { transform: translate3d(-2px, 0, 0) rotate(-0.5deg); }
                    20%, 80% { transform: translate3d(4px, 0, 0) rotate(0.5deg); }
                    30%, 50%, 70% { transform: translate3d(-6px, 0, 0) rotate(-1deg); }
                    40%, 60% { transform: translate3d(6px, 0, 0) rotate(1deg); }
                    100% { transform: translate3d(0, 0, 0) rotate(0deg); }
                }

                /* Copy Feedback */
                .copy-feedback {
                    position: relative;
                    overflow: hidden;
                }

                .copy-feedback::before {
                    content: 'Copied!';
                    position: absolute;
                    top: 50%;
                    left: 50%;
                    transform: translate(-50%, -50%);
                    background: var(--success);
                    color: white;
                    padding: 4px 12px;
                    border-radius: 20px;
                    font-size: 12px;
                    font-weight: 600;
                    white-space: nowrap;
                    opacity: 0;
                    animation: copy-show 2s ease-out;
                }

                @keyframes copy-show {
                    0% { opacity: 0; transform: translate(-50%, -50%) scale(0.8); }
                    20% { opacity: 1; transform: translate(-50%, -50%) scale(1); }
                    80% { opacity: 1; transform: translate(-50%, -50%) scale(1); }
                    100% { opacity: 0; transform: translate(-50%, -50%) scale(0.9); }
                }

                /* Toast Notifications */
                .toast-container-modern {
                    position: fixed;
                    top: 20px;
                    right: 20px;
                    z-index: 10000;
                    display: flex;
                    flex-direction: column;
                    gap: 12px;
                    max-width: 400px;
                }

                .toast-modern {
                    background: white;
                    border-radius: 12px;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
                    backdrop-filter: blur(20px);
                    border: 1px solid rgba(255, 255, 255, 0.2);
                    padding: 16px 20px;
                    display: flex;
                    align-items: flex-start;
                    gap: 12px;
                    transform: translateX(100%);
                    opacity: 0;
                    animation: toast-slide-in 0.3s cubic-bezier(0.34, 1.56, 0.64, 1) forwards;
                    position: relative;
                    overflow: hidden;
                }

                .toast-modern.toast-success {
                    border-left: 4px solid var(--success);
                }

                .toast-modern.toast-error {
                    border-left: 4px solid var(--danger);
                }

                .toast-modern.toast-warning {
                    border-left: 4px solid var(--warning);
                }

                .toast-modern.toast-info {
                    border-left: 4px solid var(--info);
                }

                .toast-modern.hiding {
                    animation: toast-slide-out 0.3s ease-in forwards;
                }

                .toast-icon {
                    width: 20px;
                    height: 20px;
                    border-radius: 50%;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    font-size: 12px;
                    color: white;
                    flex-shrink: 0;
                    margin-top: 2px;
                }

                .toast-success .toast-icon {
                    background: var(--success);
                }

                .toast-error .toast-icon {
                    background: var(--danger);
                }

                .toast-warning .toast-icon {
                    background: var(--warning);
                }

                .toast-info .toast-icon {
                    background: var(--info);
                }

                .toast-content {
                    flex: 1;
                    min-width: 0;
                }

                .toast-title {
                    font-weight: 600;
                    font-size: 14px;
                    color: var(--gray-900);
                    margin-bottom: 2px;
                    line-height: 1.3;
                }

                .toast-message {
                    font-size: 13px;
                    color: var(--gray-600);
                    line-height: 1.4;
                    margin: 0;
                }

                .toast-close {
                    background: none;
                    border: none;
                    color: var(--gray-500);
                    cursor: pointer;
                    padding: 0;
                    width: 20px;
                    height: 20px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    border-radius: 4px;
                    transition: all 0.2s ease;
                    flex-shrink: 0;
                }

                .toast-close:hover {
                    background: var(--gray-100);
                    color: var(--gray-700);
                }

                .toast-progress {
                    position: absolute;
                    bottom: 0;
                    left: 0;
                    height: 2px;
                    background: currentColor;
                    opacity: 0.3;
                    animation: toast-progress 4s linear forwards;
                }

                /* Real-time Update Highlighting */
                .update-highlight {
                    animation: update-glow 3s ease-out;
                    position: relative;
                }

                .update-highlight::before {
                    content: '';
                    position: absolute;
                    top: -2px;
                    left: -2px;
                    right: -2px;
                    bottom: -2px;
                    background: linear-gradient(45deg, var(--success), var(--info));
                    border-radius: inherit;
                    z-index: -1;
                    opacity: 0;
                    animation: update-border 3s ease-out;
                }

                @keyframes update-glow {
                    0% { background-color: rgba(40, 167, 69, 0.1); }
                    50% { background-color: rgba(40, 167, 69, 0.05); }
                    100% { background-color: transparent; }
                }

                @keyframes update-border {
                    0%, 10% { opacity: 0.8; }
                    90%, 100% { opacity: 0; }
                }

                /* Number Counter Updates */
                .counter-update {
                    display: inline-block;
                    animation: counter-bounce 0.6s cubic-bezier(0.68, -0.55, 0.265, 1.55);
                }

                @keyframes counter-bounce {
                    0% { transform: scale(1); }
                    50% { transform: scale(1.2); color: var(--success); }
                    100% { transform: scale(1); }
                }

                /* Status Badge Transitions */
                .status-change-animation {
                    animation: status-pulse 0.8s ease-out;
                }

                @keyframes status-pulse {
                    0% { transform: scale(1); }
                    25% { transform: scale(1.05); box-shadow: 0 0 0 4px rgba(40, 167, 69, 0.3); }
                    50% { transform: scale(1.1); box-shadow: 0 0 0 8px rgba(40, 167, 69, 0.2); }
                    75% { transform: scale(1.05); box-shadow: 0 0 0 4px rgba(40, 167, 69, 0.1); }
                    100% { transform: scale(1); box-shadow: none; }
                }

                /* Form Validation Feedback */
                .field-success {
                    animation: field-success-glow 2s ease-out;
                }

                .field-error {
                    animation: field-error-shake 0.6s ease-out;
                }

                @keyframes field-success-glow {
                    0% { box-shadow: 0 0 0 0 rgba(40, 167, 69, 0.7); }
                    70% { box-shadow: 0 0 0 10px rgba(40, 167, 69, 0); }
                    100% { box-shadow: 0 0 0 0 rgba(40, 167, 69, 0); }
                }

                @keyframes field-error-shake {
                    0%, 100% { transform: translateX(0); box-shadow: 0 0 0 0 rgba(220, 53, 69, 0.7); }
                    10%, 30%, 50%, 70%, 90% { transform: translateX(-3px); box-shadow: 0 0 0 5px rgba(220, 53, 69, 0.3); }
                    20%, 40%, 60%, 80% { transform: translateX(3px); box-shadow: 0 0 0 5px rgba(220, 53, 69, 0.3); }
                }

                /* Keyframe Definitions */
                @keyframes success-stroke {
                    100% { stroke-dashoffset: 0; }
                }

                @keyframes success-scale {
                    0%, 100% { transform: scale(1); }
                    50% { transform: scale(1.1); }
                }

                @keyframes toast-slide-in {
                    to {
                        transform: translateX(0);
                        opacity: 1;
                    }
                }

                @keyframes toast-slide-out {
                    to {
                        transform: translateX(100%);
                        opacity: 0;
                    }
                }

                @keyframes toast-progress {
                    from { width: 100%; }
                    to { width: 0%; }
                }

                /* Mobile Responsive */
                @media (max-width: 768px) {
                    .toast-container-modern {
                        left: 20px;
                        right: 20px;
                        top: 20px;
                        max-width: none;
                    }

                    .toast-modern {
                        padding: 12px 16px;
                        gap: 10px;
                    }

                    .toast-title {
                        font-size: 13px;
                    }

                    .toast-message {
                        font-size: 12px;
                    }

                    .success-checkmark {
                        width: 60px;
                        height: 60px;
                    }
                }
            </style>
        `;

        document.head.insertAdjacentHTML('beforeend', styles);
    }

    initializeToastContainer() {
        if (document.querySelector('.toast-container-modern')) return;

        const container = document.createElement('div');
        container.className = 'toast-container-modern';
        container.id = 'toast-container-modern';
        document.body.appendChild(container);
    }

    setupGlobalEventListeners() {
        // Listen for copy events
        document.addEventListener('copy', (e) => {
            const activeElement = document.activeElement;
            if (activeElement && (activeElement.tagName === 'INPUT' || activeElement.tagName === 'TEXTAREA')) {
                this.showCopyFeedback(activeElement);
            }
        });

        // Listen for form submissions
        document.addEventListener('submit', (e) => {
            const form = e.target;
            const submitBtn = form.querySelector('[type="submit"]');
            if (submitBtn) {
                this.setButtonLoading(submitBtn, true);
            }
        });

        // Listen for successful AJAX requests
        document.addEventListener('ajaxSuccess', (e) => {
            this.showSuccessAnimation();
        });

        // Listen for failed AJAX requests
        document.addEventListener('ajaxError', (e) => {
            this.showErrorAnimation(e.target);
        });
    }

    // Success Animations
    showSuccessAnimation(options = {}) {
        const {
            title = 'Success!',
            message = 'Operation completed successfully',
            duration = 2000,
            showCheckmark = true
        } = options;

        if (showCheckmark) {
            this.displaySuccessCheckmark();
        }

        this.showToast('success', title, message, { duration });
    }

    displaySuccessCheckmark() {
        const checkmarkHtml = `
            <div class="success-animation">
                <svg class="success-checkmark" viewBox="0 0 52 52">
                    <circle class="success-checkmark-circle" cx="26" cy="26" r="25" fill="white"/>
                    <path class="success-checkmark-check" fill="none" d="m14.1 27.2l7.1 7.2 16.7-16.8"/>
                </svg>
            </div>
        `;

        const wrapper = document.createElement('div');
        wrapper.innerHTML = checkmarkHtml;
        const animation = wrapper.firstElementChild;
        
        document.body.appendChild(animation);
        this.activeAnimations.add(animation);

        setTimeout(() => {
            if (animation.parentNode) {
                animation.remove();
                this.activeAnimations.delete(animation);
            }
        }, 2000);
    }

    // Error Animations
    showErrorAnimation(element = null, options = {}) {
        const {
            title = 'Error',
            message = 'Something went wrong. Please try again.',
            duration = 4000,
            shake = true
        } = options;

        if (element && shake) {
            this.shakeElement(element);
        }

        this.showToast('error', title, message, { duration });
    }

    shakeElement(element) {
        element.classList.remove('error-shake-element');
        // Force reflow
        element.offsetHeight;
        element.classList.add('error-shake-element');
        
        setTimeout(() => {
            element.classList.remove('error-shake-element');
        }, 600);
    }

    // Copy Feedback
    showCopyFeedback(element) {
        element.classList.remove('copy-feedback');
        // Force reflow
        element.offsetHeight;
        element.classList.add('copy-feedback');
        
        setTimeout(() => {
            element.classList.remove('copy-feedback');
        }, 2000);
    }

    // Toast Notifications
    showToast(type, title, message, options = {}) {
        const {
            duration = 4000,
            closable = true,
            showProgress = true
        } = options;

        const toast = this.createToastElement(type, title, message, { closable, showProgress });
        const container = document.getElementById('toast-container-modern');
        
        container.appendChild(toast);

        // Auto-remove toast
        if (duration > 0) {
            setTimeout(() => {
                this.removeToast(toast);
            }, duration);
        }

        return toast;
    }

    createToastElement(type, title, message, { closable, showProgress }) {
        const iconMap = {
            success: 'fas fa-check',
            error: 'fas fa-times',
            warning: 'fas fa-exclamation',
            info: 'fas fa-info'
        };

        const toast = document.createElement('div');
        toast.className = `toast-modern toast-${type}`;
        
        toast.innerHTML = `
            <div class="toast-icon">
                <i class="${iconMap[type]}"></i>
            </div>
            <div class="toast-content">
                <div class="toast-title">${title}</div>
                <div class="toast-message">${message}</div>
            </div>
            ${closable ? '<button class="toast-close" aria-label="Close"><i class="fas fa-times"></i></button>' : ''}
            ${showProgress ? '<div class="toast-progress"></div>' : ''}
        `;

        if (closable) {
            const closeBtn = toast.querySelector('.toast-close');
            closeBtn.addEventListener('click', () => this.removeToast(toast));
        }

        return toast;
    }

    removeToast(toast) {
        toast.classList.add('hiding');
        setTimeout(() => {
            if (toast.parentNode) {
                toast.remove();
            }
        }, 300);
    }

    // Real-time Update Highlighting
    highlightUpdate(element, options = {}) {
        const { type = 'default', duration = 3000 } = options;
        
        element.classList.remove('update-highlight');
        // Force reflow
        element.offsetHeight;
        element.classList.add('update-highlight');
        
        setTimeout(() => {
            element.classList.remove('update-highlight');
        }, duration);
    }

    // Counter Animations
    animateCounterUpdate(element, newValue, options = {}) {
        const { duration = 600, easing = 'cubic-bezier(0.68, -0.55, 0.265, 1.55)' } = options;
        
        element.classList.add('counter-update');
        element.textContent = newValue;
        
        setTimeout(() => {
            element.classList.remove('counter-update');
        }, duration);
    }

    // Status Change Animations
    animateStatusChange(element, newStatus, options = {}) {
        const { highlight = true } = options;
        
        element.textContent = newStatus;
        
        if (highlight) {
            element.classList.add('status-change-animation');
            setTimeout(() => {
                element.classList.remove('status-change-animation');
            }, 800);
        }
    }

    // Form Field Feedback
    showFieldSuccess(field) {
        field.classList.remove('field-success', 'field-error');
        // Force reflow
        field.offsetHeight;
        field.classList.add('field-success');
        
        setTimeout(() => {
            field.classList.remove('field-success');
        }, 2000);
    }

    showFieldError(field) {
        field.classList.remove('field-success', 'field-error');
        // Force reflow
        field.offsetHeight;
        field.classList.add('field-error');
        
        setTimeout(() => {
            field.classList.remove('field-error');
        }, 600);
    }

    // Button Loading States
    setButtonLoading(button, isLoading = true) {
        if (isLoading) {
            button.classList.add('btn-loading');
            button.disabled = true;
            button.dataset.originalText = button.textContent;
        } else {
            button.classList.remove('btn-loading');
            button.disabled = false;
            if (button.dataset.originalText) {
                button.textContent = button.dataset.originalText;
                delete button.dataset.originalText;
            }
        }
    }

    // Utility Methods
    clearAllAnimations() {
        this.activeAnimations.forEach(animation => {
            if (animation.parentNode) {
                animation.remove();
            }
        });
        this.activeAnimations.clear();
    }

    // Mobile Touch Feedback
    addTouchFeedback(element) {
        let touchTimeout;
        
        element.addEventListener('touchstart', () => {
            element.style.transform = 'scale(0.98)';
            element.style.opacity = '0.8';
            element.style.transition = 'all 0.1s ease';
        }, { passive: true });
        
        element.addEventListener('touchend', () => {
            clearTimeout(touchTimeout);
            touchTimeout = setTimeout(() => {
                element.style.transform = '';
                element.style.opacity = '';
            }, 100);
        }, { passive: true });
        
        element.addEventListener('touchcancel', () => {
            element.style.transform = '';
            element.style.opacity = '';
        }, { passive: true });
    }

    // Destroy instance
    destroy() {
        this.clearAllAnimations();
        const container = document.getElementById('toast-container-modern');
        if (container) {
            container.remove();
        }
        const styles = document.getElementById('visual-feedback-styles');
        if (styles) {
            styles.remove();
        }
    }
}

// Initialize global instance
document.addEventListener('DOMContentLoaded', () => {
    window.visualFeedback = new VisualFeedbackSystem();
    console.log('✨ Visual Feedback System Initialized');
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = VisualFeedbackSystem;
}