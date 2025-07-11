// Service Worker Registration and Update Management
// BizConnect PWA Support

class ServiceWorkerManager {
    constructor() {
        this.registration = null;
        this.isUpdateAvailable = false;
        this.refreshing = false;
        
        // Bind methods to preserve context
        this.onUpdateFound = this.onUpdateFound.bind(this);
        this.onStateChange = this.onStateChange.bind(this);
        this.onControllerChange = this.onControllerChange.bind(this);
        
        this.init();
    }
    
    async init() {
        if (!('serviceWorker' in navigator)) {
            console.log('[SW Manager] Service Worker not supported');
            return;
        }
        
        try {
            await this.registerServiceWorker();
            this.setupUpdateListeners();
        } catch (error) {
            console.error('[SW Manager] Failed to initialize:', error);
        }
    }
    
    async registerServiceWorker() {
        try {
            console.log('[SW Manager] Registering Service Worker...');
            
            this.registration = await navigator.serviceWorker.register('/service-worker.js', {
                scope: '/'
            });
            
            console.log('[SW Manager] Service Worker registered successfully');
            
            // Check for updates immediately
            this.registration.addEventListener('updatefound', this.onUpdateFound);
            
            // Check for updates periodically (every 30 minutes)
            setInterval(() => {
                this.checkForUpdates();
            }, 30 * 60 * 1000);
            
            // Check for updates when page becomes visible
            document.addEventListener('visibilitychange', () => {
                if (!document.hidden) {
                    this.checkForUpdates();
                }
            });
            
        } catch (error) {
            console.error('[SW Manager] Service Worker registration failed:', error);
            throw error;
        }
    }
    
    setupUpdateListeners() {
        // Listen for controller changes (new SW taking control)
        navigator.serviceWorker.addEventListener('controllerchange', this.onControllerChange);
        
        // Listen for messages from the service worker
        navigator.serviceWorker.addEventListener('message', event => {
            console.log('[SW Manager] Received message from SW:', event.data);
        });
    }
    
    onUpdateFound() {
        console.log('[SW Manager] Update found, installing new Service Worker...');
        
        const newWorker = this.registration.installing;
        if (!newWorker) return;
        
        newWorker.addEventListener('statechange', this.onStateChange);
    }
    
    onStateChange(event) {
        const worker = event.target;
        console.log(`[SW Manager] Service Worker state changed to: ${worker.state}`);
        
        if (worker.state === 'installed') {
            if (navigator.serviceWorker.controller) {
                // New update available
                console.log('[SW Manager] New update available');
                this.isUpdateAvailable = true;
                this.showUpdateNotification();
            } else {
                // First time installation
                console.log('[SW Manager] Service Worker installed for the first time');
                this.showInstallNotification();
            }
        }
    }
    
    onControllerChange() {
        console.log('[SW Manager] Controller changed, reloading page...');
        
        if (this.refreshing) return;
        this.refreshing = true;
        
        // Small delay to ensure the new SW is fully active
        setTimeout(() => {
            window.location.reload();
        }, 100);
    }
    
    async checkForUpdates() {
        if (!this.registration) return;
        
        try {
            console.log('[SW Manager] Checking for updates...');
            await this.registration.update();
        } catch (error) {
            console.error('[SW Manager] Failed to check for updates:', error);
        }
    }
    
    async skipWaiting() {
        if (!this.registration || !this.registration.waiting) {
            console.log('[SW Manager] No waiting Service Worker to skip');
            return;
        }
        
        console.log('[SW Manager] Sending SKIP_WAITING message to Service Worker');
        this.registration.waiting.postMessage({ type: 'SKIP_WAITING' });
    }
    
    showUpdateNotification() {
        // Check if we have the BizConnect loading system available
        if (window.BizConnectLoading) {
            window.BizConnectLoading.showToast(
                'A new version of BizConnect is available. Click "Update" to get the latest features and improvements.',
                {
                    type: 'info',
                    title: 'Update Available',
                    duration: 0, // Don't auto-dismiss
                    actions: [
                        {
                            text: 'Update Now',
                            action: () => {
                                this.skipWaiting();
                            }
                        },
                        {
                            text: 'Later',
                            action: () => {
                                // Just dismiss the notification
                            }
                        }
                    ]
                }
            );
        } else {
            // Fallback to browser notification
            if (confirm('A new version of BizConnect is available. Would you like to update now?')) {
                this.skipWaiting();
            }
        }
    }
    
    showInstallNotification() {
        console.log('[SW Manager] BizConnect is now available offline!');
        
        if (window.BizConnectLoading) {
            window.BizConnectLoading.showToast(
                'BizConnect is now available offline! You can use the app even without an internet connection.',
                {
                    type: 'success',
                    title: 'Offline Ready',
                    duration: 5000
                }
            );
        }
    }
    
    async getServiceWorkerVersion() {
        if (!navigator.serviceWorker.controller) {
            return null;
        }
        
        return new Promise((resolve) => {
            const messageChannel = new MessageChannel();
            messageChannel.port1.onmessage = (event) => {
                resolve(event.data.version);
            };
            
            navigator.serviceWorker.controller.postMessage(
                { type: 'GET_VERSION' },
                [messageChannel.port2]
            );
        });
    }
    
    // Public method to manually trigger update check
    async forceUpdate() {
        console.log('[SW Manager] Force update requested');
        await this.checkForUpdates();
    }
    
    // Public method to get registration status
    getStatus() {
        return {
            isSupported: 'serviceWorker' in navigator,
            isRegistered: !!this.registration,
            isUpdateAvailable: this.isUpdateAvailable,
            controller: !!navigator.serviceWorker.controller
        };
    }
}

// Initialize Service Worker Manager when DOM is ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.swManager = new ServiceWorkerManager();
    });
} else {
    window.swManager = new ServiceWorkerManager();
}

// Export for global access
window.ServiceWorkerManager = ServiceWorkerManager;
