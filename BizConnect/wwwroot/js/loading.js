/**
 * BizConnect Loading States and Progress Indicators
 * Provides utilities for managing loading states, progress bars, and user feedback
 */

class BizConnectLoading {
    constructor() {
        this.activeLoaders = new Map();
        this.toastContainer = null;
        this.init();
    }

    init() {
        // Create toast container if it doesn't exist
        this.createToastContainer();
        
        // Add global loading styles
        this.addGlobalStyles();
    }

    createToastContainer() {
        if (!document.getElementById('bc-toast-container')) {
            const container = document.createElement('div');
            container.id = 'bc-toast-container';
            container.style.cssText = `
                position: fixed;
                top: 20px;
                right: 20px;
                z-index: 1070;
                pointer-events: none;
            `;
            document.body.appendChild(container);
            this.toastContainer = container;
        }
    }

    addGlobalStyles() {
        if (!document.getElementById('bc-loading-styles')) {
            const style = document.createElement('style');
            style.id = 'bc-loading-styles';
            style.textContent = `
                .bc-loading-cursor { cursor: wait !important; }
                .bc-loading-cursor * { cursor: wait !important; }
            `;
            document.head.appendChild(style);
        }
    }

    // Spinner Methods
    showSpinner(element, options = {}) {
        const config = {
            size: 'default', // sm, default, lg, xl
            type: 'spin', // spin, pulse, dots
            color: 'primary',
            overlay: false,
            ...options
        };

        const spinnerId = this.generateId();
        const spinner = this.createSpinner(config);
        
        if (config.overlay) {
            const overlay = this.createOverlay(spinner, config);
            element.style.position = 'relative';
            element.appendChild(overlay);
            this.activeLoaders.set(spinnerId, overlay);
        } else {
            element.appendChild(spinner);
            this.activeLoaders.set(spinnerId, spinner);
        }

        return spinnerId;
    }

    hideSpinner(spinnerId) {
        const loader = this.activeLoaders.get(spinnerId);
        if (loader && loader.parentNode) {
            loader.remove();
            this.activeLoaders.delete(spinnerId);
        }
    }

    createSpinner(config) {
        const spinner = document.createElement('div');
        
        switch (config.type) {
            case 'pulse':
                spinner.className = `bc-spinner-pulse bc-spinner-${config.size}`;
                break;
            case 'dots':
                spinner.className = 'bc-spinner-dots';
                for (let i = 0; i < 3; i++) {
                    const dot = document.createElement('div');
                    dot.className = 'bc-dot';
                    spinner.appendChild(dot);
                }
                break;
            default:
                spinner.className = `bc-spinner bc-spinner-${config.size}`;
        }

        return spinner;
    }

    createOverlay(content, config) {
        const overlay = document.createElement('div');
        overlay.className = `bc-loading-overlay ${config.dark ? 'bc-loading-overlay-dark' : ''}`;
        
        const loadingContent = document.createElement('div');
        loadingContent.className = 'bc-loading-content';
        loadingContent.appendChild(content);
        
        if (config.text) {
            const text = document.createElement('div');
            text.className = 'bc-loading-text';
            text.textContent = config.text;
            loadingContent.appendChild(text);
        }
        
        overlay.appendChild(loadingContent);
        return overlay;
    }

    // Progress Bar Methods
    createProgressBar(element, options = {}) {
        const config = {
            size: 'default', // sm, default, lg, xl
            value: 0,
            max: 100,
            indeterminate: false,
            striped: true,
            ...options
        };

        const progressId = this.generateId();
        const progressContainer = document.createElement('div');
        progressContainer.className = `bc-progress bc-progress-${config.size}`;
        
        if (config.indeterminate) {
            progressContainer.classList.add('bc-progress-indeterminate');
        }

        const progressBar = document.createElement('div');
        progressBar.className = 'bc-progress-bar';
        progressBar.style.width = `${(config.value / config.max) * 100}%`;
        
        progressContainer.appendChild(progressBar);
        element.appendChild(progressContainer);
        
        this.activeLoaders.set(progressId, { container: progressContainer, bar: progressBar, config });
        return progressId;
    }

    updateProgress(progressId, value) {
        const progress = this.activeLoaders.get(progressId);
        if (progress) {
            const percentage = Math.min(100, Math.max(0, (value / progress.config.max) * 100));
            progress.bar.style.width = `${percentage}%`;
        }
    }

    // Circular Progress Methods
    createCircularProgress(element, options = {}) {
        const config = {
            size: 60,
            strokeWidth: 8,
            value: 0,
            max: 100,
            showText: true,
            ...options
        };

        const progressId = this.generateId();
        const radius = (config.size - config.strokeWidth) / 2;
        const circumference = radius * 2 * Math.PI;
        
        const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('width', config.size);
        svg.setAttribute('height', config.size);
        svg.classList.add('bc-progress-circle');
        
        const bgCircle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        bgCircle.classList.add('bc-progress-circle-bg');
        bgCircle.setAttribute('cx', config.size / 2);
        bgCircle.setAttribute('cy', config.size / 2);
        bgCircle.setAttribute('r', radius);
        bgCircle.setAttribute('stroke-width', config.strokeWidth);
        
        const progressCircle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
        progressCircle.classList.add('bc-progress-circle-bar');
        progressCircle.setAttribute('cx', config.size / 2);
        progressCircle.setAttribute('cy', config.size / 2);
        progressCircle.setAttribute('r', radius);
        progressCircle.setAttribute('stroke-width', config.strokeWidth);
        progressCircle.setAttribute('stroke-dasharray', circumference);
        progressCircle.setAttribute('stroke-dashoffset', circumference);
        
        svg.appendChild(bgCircle);
        svg.appendChild(progressCircle);
        
        const container = document.createElement('div');
        container.style.position = 'relative';
        container.style.display = 'inline-block';
        container.appendChild(svg);
        
        if (config.showText) {
            const text = document.createElement('div');
            text.classList.add('bc-progress-circle-text');
            text.textContent = '0%';
            container.appendChild(text);
        }
        
        element.appendChild(container);
        
        this.activeLoaders.set(progressId, {
            container,
            circle: progressCircle,
            text: config.showText ? text : null,
            circumference,
            config
        });
        
        return progressId;
    }

    updateCircularProgress(progressId, value) {
        const progress = this.activeLoaders.get(progressId);
        if (progress) {
            const percentage = Math.min(100, Math.max(0, (value / progress.config.max) * 100));
            const offset = progress.circumference - (percentage / 100) * progress.circumference;
            
            progress.circle.setAttribute('stroke-dashoffset', offset);
            
            if (progress.text) {
                progress.text.textContent = `${Math.round(percentage)}%`;
            }
        }
    }

    // Skeleton Screen Methods
    createSkeleton(element, type = 'text', options = {}) {
        const skeleton = document.createElement('div');
        skeleton.className = `bc-skeleton bc-skeleton-${type}`;
        
        if (options.size) {
            skeleton.classList.add(`bc-skeleton-${type}-${options.size}`);
        }
        
        if (options.width) {
            skeleton.style.width = options.width;
        }
        
        if (options.height) {
            skeleton.style.height = options.height;
        }
        
        element.appendChild(skeleton);
        return skeleton;
    }

    // Toast Notifications
    showToast(message, options = {}) {
        const config = {
            type: 'info', // success, warning, error, info
            title: '',
            duration: 5000,
            showProgress: true,
            ...options
        };

        const toast = this.createToast(message, config);
        this.toastContainer.appendChild(toast);
        
        // Show toast
        setTimeout(() => toast.classList.add('bc-toast-show'), 100);
        
        // Auto-hide toast
        if (config.duration > 0) {
            this.autoHideToast(toast, config.duration, config.showProgress);
        }
        
        return toast;
    }

    createToast(message, config) {
        const toast = document.createElement('div');
        toast.className = `bc-toast bc-toast-${config.type}`;
        toast.style.pointerEvents = 'auto';
        
        const content = document.createElement('div');
        content.className = 'bc-toast-content';
        
        const icon = document.createElement('div');
        icon.className = 'bc-toast-icon';
        icon.innerHTML = this.getToastIcon(config.type);
        
        const messageDiv = document.createElement('div');
        messageDiv.className = 'bc-toast-message';
        
        if (config.title) {
            const title = document.createElement('div');
            title.className = 'bc-toast-title';
            title.textContent = config.title;
            messageDiv.appendChild(title);
        }
        
        const text = document.createElement('div');
        text.className = 'bc-toast-text';
        text.textContent = message;
        messageDiv.appendChild(text);
        
        content.appendChild(icon);
        content.appendChild(messageDiv);
        toast.appendChild(content);
        
        if (config.showProgress) {
            const progress = document.createElement('div');
            progress.className = 'bc-toast-progress';
            const progressBar = document.createElement('div');
            progressBar.className = 'bc-toast-progress-bar';
            progressBar.style.width = '100%';
            progress.appendChild(progressBar);
            toast.appendChild(progress);
        }
        
        return toast;
    }

    autoHideToast(toast, duration, showProgress) {
        if (showProgress) {
            const progressBar = toast.querySelector('.bc-toast-progress-bar');
            if (progressBar) {
                progressBar.style.transition = `width ${duration}ms linear`;
                progressBar.style.width = '0%';
            }
        }
        
        setTimeout(() => {
            toast.classList.remove('bc-toast-show');
            setTimeout(() => toast.remove(), 300);
        }, duration);
    }

    getToastIcon(type) {
        const icons = {
            success: '<i class="fas fa-check-circle"></i>',
            warning: '<i class="fas fa-exclamation-triangle"></i>',
            error: '<i class="fas fa-times-circle"></i>',
            info: '<i class="fas fa-info-circle"></i>'
        };
        return icons[type] || icons.info;
    }

    // Utility Methods
    generateId() {
        return 'bc-loader-' + Math.random().toString(36).substr(2, 9);
    }

    // Button Loading States
    setButtonLoading(button, loading = true) {
        if (loading) {
            button.classList.add('bc-btn-loading-state');
            button.disabled = true;
        } else {
            button.classList.remove('bc-btn-loading-state');
            button.disabled = false;
        }
    }

    // Page Loading
    showPageLoading(container, text = 'Loading...', subtext = '') {
        const loadingDiv = document.createElement('div');
        loadingDiv.className = 'bc-page-loading';
        loadingDiv.innerHTML = `
            <div class="bc-page-loading-spinner">
                <div class="bc-spinner bc-spinner-xl"></div>
            </div>
            <div class="bc-page-loading-text">${text}</div>
            ${subtext ? `<div class="bc-page-loading-subtext">${subtext}</div>` : ''}
        `;
        
        container.innerHTML = '';
        container.appendChild(loadingDiv);
        return loadingDiv;
    }
}

// Initialize global loading manager
window.BizConnectLoading = new BizConnectLoading();

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = BizConnectLoading;
}
