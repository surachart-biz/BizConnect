// BizConnect Service Worker
// Version: 1.0.0 - Update this version on every release
const CACHE_VERSION = 'bizconnect-v1.0.0';
const CACHE_NAME = `${CACHE_VERSION}-static`;
const RUNTIME_CACHE = `${CACHE_VERSION}-runtime`;

// Assets to cache immediately
const STATIC_ASSETS = [
    '/',
    '/css/site.css',
    '/js/site.js',
    '/js/loading.js',
    '/js/interactions.js',
    '/js/guided-tour.js',
    '/js/home-enhancements.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css',
    '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
    '/lib/jquery/dist/jquery.min.js',
    '/favicon.ico'
];

// Runtime caching patterns
const RUNTIME_PATTERNS = [
    /^https:\/\/fonts\.googleapis\.com/,
    /^https:\/\/fonts\.gstatic\.com/,
    /^https:\/\/cdnjs\.cloudflare\.com/
];

// Install event - cache static assets and skip waiting
self.addEventListener('install', event => {
    console.log(`[SW] Installing version ${CACHE_VERSION}`);
    
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then(cache => {
                console.log('[SW] Caching static assets');
                return cache.addAll(STATIC_ASSETS);
            })
            .then(() => {
                console.log('[SW] Static assets cached, skipping waiting');
                return self.skipWaiting(); // Force immediate activation
            })
            .catch(error => {
                console.error('[SW] Failed to cache static assets:', error);
            })
    );
});

// Activate event - clean old caches and claim clients
self.addEventListener('activate', event => {
    console.log(`[SW] Activating version ${CACHE_VERSION}`);
    
    event.waitUntil(
        Promise.all([
            // Clean up old caches
            caches.keys().then(cacheNames => {
                return Promise.all(
                    cacheNames
                        .filter(cacheName => 
                            cacheName.startsWith('bizconnect-') && 
                            cacheName !== CACHE_NAME && 
                            cacheName !== RUNTIME_CACHE
                        )
                        .map(cacheName => {
                            console.log(`[SW] Deleting old cache: ${cacheName}`);
                            return caches.delete(cacheName);
                        })
                );
            }),
            // Take control of all clients immediately
            self.clients.claim()
        ]).then(() => {
            console.log('[SW] Activation complete, controlling all clients');
        })
    );
});

// Fetch event - serve from cache with network fallback
self.addEventListener('fetch', event => {
    const { request } = event;
    const url = new URL(request.url);
    
    // Skip non-GET requests
    if (request.method !== 'GET') {
        return;
    }
    
    // Skip chrome-extension and other non-http(s) requests
    if (!url.protocol.startsWith('http')) {
        return;
    }
    
    // Handle different types of requests
    if (isStaticAsset(request)) {
        event.respondWith(handleStaticAsset(request));
    } else if (isRuntimeCacheable(request)) {
        event.respondWith(handleRuntimeCache(request));
    } else if (isNavigationRequest(request)) {
        event.respondWith(handleNavigation(request));
    }
});

// Handle static assets (CSS, JS, images)
async function handleStaticAsset(request) {
    try {
        const cache = await caches.open(CACHE_NAME);
        const cachedResponse = await cache.match(request);
        
        if (cachedResponse) {
            console.log(`[SW] Serving from cache: ${request.url}`);
            return cachedResponse;
        }
        
        console.log(`[SW] Fetching and caching: ${request.url}`);
        const networkResponse = await fetch(request);
        
        if (networkResponse.ok) {
            cache.put(request, networkResponse.clone());
        }
        
        return networkResponse;
    } catch (error) {
        console.error(`[SW] Failed to handle static asset: ${request.url}`, error);
        throw error;
    }
}

// Handle runtime cacheable resources (fonts, CDN assets)
async function handleRuntimeCache(request) {
    try {
        const cache = await caches.open(RUNTIME_CACHE);
        const cachedResponse = await cache.match(request);
        
        if (cachedResponse) {
            // Serve from cache and update in background
            fetch(request).then(response => {
                if (response.ok) {
                    cache.put(request, response.clone());
                }
            }).catch(() => {
                // Ignore background update failures
            });
            
            return cachedResponse;
        }
        
        const networkResponse = await fetch(request);
        
        if (networkResponse.ok) {
            cache.put(request, networkResponse.clone());
        }
        
        return networkResponse;
    } catch (error) {
        console.error(`[SW] Failed to handle runtime cache: ${request.url}`, error);
        throw error;
    }
}

// Handle navigation requests
async function handleNavigation(request) {
    try {
        // Always try network first for navigation
        const networkResponse = await fetch(request);
        return networkResponse;
    } catch (error) {
        // Fallback to cached root page if available
        const cache = await caches.open(CACHE_NAME);
        const cachedResponse = await cache.match('/');
        
        if (cachedResponse) {
            console.log('[SW] Serving cached root page for failed navigation');
            return cachedResponse;
        }
        
        throw error;
    }
}

// Helper functions
function isStaticAsset(request) {
    const url = new URL(request.url);
    return url.pathname.match(/\.(css|js|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot)$/);
}

function isRuntimeCacheable(request) {
    return RUNTIME_PATTERNS.some(pattern => pattern.test(request.url));
}

function isNavigationRequest(request) {
    return request.mode === 'navigate' || 
           (request.method === 'GET' && request.headers.get('accept').includes('text/html'));
}

// Handle messages from the client
self.addEventListener('message', event => {
    console.log('[SW] Received message:', event.data);
    
    if (event.data && event.data.type === 'SKIP_WAITING') {
        console.log('[SW] Received SKIP_WAITING message');
        self.skipWaiting();
    }
    
    if (event.data && event.data.type === 'GET_VERSION') {
        event.ports[0].postMessage({ version: CACHE_VERSION });
    }
});

console.log(`[SW] Service Worker ${CACHE_VERSION} loaded`);
