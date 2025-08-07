// Enhanced Authentication Management System for BizConnect

(function() {
    'use strict';

    // Configuration
    const CONFIG = {
        SESSION_TIMEOUT: 30 * 60 * 1000, // 30 minutes in milliseconds
        DEMO_REDIRECT_DELAY: 1500,
        LOGOUT_REDIRECT_DELAY: 1000
    };

    // Authentication Manager Class
    class BizConnectAuth {
        constructor() {
            this.currentUser = null;
            this.sessionTimer = null;
            this.languageManager = null;
            this.initialized = false;
            
            // CRITICAL: Force reset authentication state immediately
            this.forceResetState();
            
            // Bind methods to maintain context
            this.handleLogin = this.handleLogin.bind(this);
            this.handleSignOut = this.handleSignOut.bind(this);
            this.checkAuthStatus = this.checkAuthStatus.bind(this);
        }

        // Force reset authentication state to prevent dropdown showing
        forceResetState() {
            console.log('🚨 FORCE RESET: Clearing all authentication state');
            this.currentUser = null;
            sessionStorage.removeItem('authUser');
            localStorage.removeItem('authUser');
            
            // Force UI to unauthenticated state immediately
            this.forceUIToUnauthenticated();
        }

        // Force UI to show unauthenticated state
        forceUIToUnauthenticated() {
            console.log('🔄 FORCE UI: Setting unauthenticated state');
            
            const signInContainer = document.getElementById('signInContainer');
            const userDropdownContainer = document.getElementById('userDropdownContainer');
            
            if (signInContainer) {
                signInContainer.classList.remove('d-none', 'hide-signin');
                signInContainer.style.display = 'flex';
                console.log('✅ Sign-in container forced visible');
            }
            
            if (userDropdownContainer) {
                userDropdownContainer.classList.add('d-none');
                userDropdownContainer.classList.remove('show-user');
                userDropdownContainer.style.display = 'none';
                console.log('✅ User dropdown container forced hidden');
            }
        }

        init() {
            console.log('🔐 Initializing BizConnect Authentication System...');
            
            if (this.initialized) {
                console.log('⚠️ Auth already initialized, skipping...');
                return;
            }
            
            // Get language manager if available
            this.languageManager = window.BizConnectLanguage;
            
            // CRITICAL: Force reset state again to ensure clean start
            this.forceResetState();
            
            // Setup event listeners
            this.setupEventListeners();
            
            // Setup session monitoring (but don't restore session yet)
            this.setupSessionMonitoring();
            
            // Mark as initialized
            this.initialized = true;
            
            // Force UI update after DOM is ready - multiple attempts to ensure state
            setTimeout(() => {
                console.log('🔄 Final auth state verification...');
                this.verifyAuthenticationState();
            }, 100);
            
            setTimeout(() => {
                console.log('🔄 Secondary auth state verification...');
                this.verifyAuthenticationState();
            }, 500);
            
            console.log('✅ BizConnect Authentication System initialized successfully');
        }

        // Verify and enforce authentication state
        verifyAuthenticationState() {
            console.log('🔍 Verifying authentication state...');
            
            const signInContainer = document.getElementById('signInContainer');
            const userDropdownContainer = document.getElementById('userDropdownContainer');
            
            if (!this.currentUser) {
                console.log('🔒 User not authenticated - enforcing unauthenticated UI state');
                
                if (signInContainer) {
                    signInContainer.classList.remove('d-none', 'hide-signin');
                    signInContainer.style.display = 'flex';
                }
                
                if (userDropdownContainer) {
                    userDropdownContainer.classList.add('d-none');
                    userDropdownContainer.classList.remove('show-user');
                    userDropdownContainer.style.display = 'none';
                }
                
                console.log('✅ Unauthenticated state enforced');
            } else {
                console.log('🔓 User authenticated - showing authenticated UI state');
                this.updateAuthUI(this.currentUser);
            }
        }

        setupEventListeners() {
            // IMPORTANT: Do NOT add event listeners to sign-in button here
            // The sign-in button modal functionality is handled in Index.cshtml
            
            // Login form
            const loginForm = document.getElementById('loginForm');
            if (loginForm) {
                loginForm.addEventListener('submit', this.handleLogin);
            }
            
            // Sign out
            const signOutBtn = document.getElementById('signOutBtn');
            if (signOutBtn) {
                signOutBtn.addEventListener('click', this.handleSignOut);
            }
            
            // Admin dashboard link
            const adminDashboardLink = document.getElementById('adminDashboardLink');
            if (adminDashboardLink) {
                adminDashboardLink.addEventListener('click', (e) => {
                    e.preventDefault();
                    
                    // Check if user has admin privileges
                    if (this.currentUser && (this.currentUser.role === 'Admin' || this.currentUser.role === 'Employee')) {
                        window.location.href = '/Admin/Dashboard';
                    } else {
                        this.showNotification(
                            this.t('insufficientPrivileges', 'You do not have sufficient privileges to access the admin dashboard'), 
                            'warning'
                        );
                    }
                });
            }

            // Setup modal event listeners (but NOT sign-in button)
            this.setupModalListeners();
        }

        setupModalListeners() {
            const loginModal = document.getElementById('loginModal');
            if (loginModal) {
                // Clear form when modal opens
                loginModal.addEventListener('show.bs.modal', () => {
                    this.clearLoginForm();
                    this.hideLoginError();
                });

                // Clear form when modal closes
                loginModal.addEventListener('hidden.bs.modal', () => {
                    this.clearLoginForm();
                    this.hideLoginError();
                });
            }
        }

        setupSessionMonitoring() {
            // Clear existing timer
            if (this.sessionTimer) {
                clearTimeout(this.sessionTimer);
            }

            // Set up session timeout if user is logged in
            if (this.currentUser) {
                this.sessionTimer = setTimeout(() => {
                    this.handleSessionTimeout();
                }, CONFIG.SESSION_TIMEOUT);
            }
        }

        // Language helper method
        t(key, fallback = '') {
            if (this.languageManager && typeof this.languageManager.t === 'function') {
                return this.languageManager.t(key) || fallback;
            }
            return fallback;
        }

        // Session timeout handler
        handleSessionTimeout() {
            const message = this.t('sessionExpired', 'Your session has expired. Please login again.');
            this.showNotification(message, 'warning');
            
            // Clear session data
            this.currentUser = null;
            sessionStorage.removeItem('authUser');
            
            // Update UI
            this.updateAuthUI(null);
            
            // Show login modal
            setTimeout(() => {
                const loginModal = document.getElementById('loginModal');
                if (loginModal) {
                    const modal = new bootstrap.Modal(loginModal);
                    modal.show();
                }
            }, 2000);
        }

        // Form management methods
        clearLoginForm() {
            // Try new modal reset function first
            if (typeof window.resetModalState === 'function') {
                window.resetModalState();
                return;
            }
            
            // Fallback to original implementation
            const usernameField = document.getElementById('username');
            const passwordField = document.getElementById('password');
            
            if (usernameField) {
                usernameField.value = '';
                usernameField.classList.remove('is-valid', 'is-invalid', 'error');
            }
            
            if (passwordField) {
                passwordField.value = '';
                passwordField.classList.remove('is-valid', 'is-invalid', 'error');
                passwordField.type = 'password'; // Reset password visibility
            }
            
            // Reset password toggle icon for new modal
            const toggleIcon = document.getElementById('passwordToggleIcon');
            if (toggleIcon) {
                toggleIcon.className = 'fas fa-eye';
            }
        }

        hideLoginError() {
            const errorAlert = document.getElementById('loginErrorAlert');
            if (errorAlert) {
                errorAlert.classList.add('d-none');
            }
        }

        showLoginError(message) {
            // Try new modal structure first
            if (typeof window.showFormError === 'function') {
                window.showFormError(message);
                return;
            }
            
            // Fallback to original implementation
            const errorAlert = document.getElementById('loginErrorAlert');
            const errorMessage = document.getElementById('loginErrorMessage');
            
            if (errorAlert && errorMessage) {
                errorMessage.textContent = message;
                errorAlert.classList.remove('d-none');
                
                // Hide info alert when showing error (new modal structure)
                const infoAlert = document.getElementById('authInfoAlert');
                if (infoAlert) {
                    infoAlert.style.display = 'none';
                }
                
                // Hide after 5 seconds
                setTimeout(() => {
                    errorAlert.classList.add('d-none');
                    // Restore info alert
                    if (infoAlert) {
                        infoAlert.style.display = 'flex';
                    }
                }, 5000);
            }
        }

        // Enhanced login handler with validation
        async handleLogin(e) {
            e.preventDefault();
            
            // Hide any existing error
            this.hideLoginError();
            
            const username = document.getElementById('username').value.trim();
            const password = document.getElementById('password').value;
            
            // Validation
            if (!username || !password) {
                const message = this.t('fillAllFields', 'Please fill in all fields');
                this.showLoginError(message);
                return;
            }
            
            // Show loading state
            this.showLoading(true);
            
            try {
                // Try actual API call first
                const response = await fetch('/Account/Login', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Requested-With': 'XMLHttpRequest'
                    },
                    body: JSON.stringify({
                        username: username,
                        password: password
                    })
                });
                
                const result = await response.json();
                
                if (response.ok && result.success) {
                    this.handleLoginSuccess(result, username);
                } else {
                    // Fallback to demo credentials
                    this.handleDemoLogin(username, password);
                }
            } catch (error) {
                console.error('Login API error:', error);
                // Fallback to demo credentials for development
                this.handleDemoLogin(username, password);
            }
            
            this.showLoading(false);
        }

        // Handle successful login from API
        handleLoginSuccess(result, username) {
            const userData = {
                username: result.username || username,
                role: result.role || 'User',
                loginTime: new Date().toISOString()
            };
            
            this.currentUser = userData;
            sessionStorage.setItem('authUser', JSON.stringify(userData));
            
            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('loginModal'));
            if (modal) {
                modal.hide();
            }
            
            // Update UI
            this.updateAuthUI(userData);
            
            // Show success message
            const message = `${this.t('loginSuccess', 'Login successful!')} ${this.t('welcome', 'Welcome')} ${userData.username}`;
            this.showNotification(message, 'success');
            
            // Setup session monitoring
            this.setupSessionMonitoring();
            
            // Redirect if needed
            if (result.redirectUrl) {
                setTimeout(() => {
                    window.location.href = result.redirectUrl;
                }, CONFIG.DEMO_REDIRECT_DELAY);
            }
        }

        // Handle demo login credentials
        handleDemoLogin(username, password) {
            const credentials = {
                'admin': { password: 'admin123', role: 'Admin' },
                'employee': { password: 'emp123', role: 'Employee' },
                'test': { password: 'test123', role: 'User' }
            };
            
            if (credentials[username] && credentials[username].password === password) {
                const userData = {
                    username: username,
                    role: credentials[username].role,
                    loginTime: new Date().toISOString(),
                    isDemo: true
                };
                
                this.currentUser = userData;
                sessionStorage.setItem('authUser', JSON.stringify(userData));
                
                // Hide modal
                const modal = bootstrap.Modal.getInstance(document.getElementById('loginModal'));
                if (modal) {
                    modal.hide();
                }
                
                // Update UI
                this.updateAuthUI(userData);
                
                // Show success message
                const message = `${this.t('loginSuccess', 'Login successful!')} ${this.t('welcome', 'Welcome')} ${username}`;
                this.showNotification(message, 'success');
                
                // Setup session monitoring
                this.setupSessionMonitoring();
                
                // Redirect admin users to dashboard
                if (userData.role === 'Admin') {
                    setTimeout(() => {
                        window.location.href = '/Admin/Dashboard';
                    }, CONFIG.DEMO_REDIRECT_DELAY);
                }
            } else {
                const message = this.t('loginError', 'Invalid username or password');
                this.showLoginError(message);
            }
        }

        // Handle sign out
        async handleSignOut() {
            try {
                // Call logout endpoint
                const response = await fetch('/Account/Logout', {
                    method: 'POST',
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });
                
                if (response.ok) {
                    this.performLogout();
                } else {
                    // Fallback - still logout locally
                    this.performLogout();
                }
            } catch (error) {
                console.error('Logout error:', error);
                // Fallback - clear session and update UI
                this.performLogout();
            }
        }

        // Perform the actual logout process
        performLogout() {
            // Clear session timer
            if (this.sessionTimer) {
                clearTimeout(this.sessionTimer);
                this.sessionTimer = null;
            }

            // Clear user data
            this.currentUser = null;
            sessionStorage.removeItem('authUser');
            
            // Update UI
            this.updateAuthUI(null);
            
            // Show notification
            const message = this.t('logoutSuccess', 'Logged out successfully');
            this.showNotification(message, 'info');
            
            // Redirect to home
            setTimeout(() => {
                window.location.href = '/';
            }, CONFIG.LOGOUT_REDIRECT_DELAY);
        }

        // Check authentication status on page load
        checkAuthStatus() {
            console.log('🔍 Checking authentication status...');
            
            // CRITICAL: Always start with unauthenticated state for security
            this.forceResetState();
            
            const authUser = sessionStorage.getItem('authUser');
            if (authUser && authUser !== 'null') {
                try {
                    const userData = JSON.parse(authUser);
                    
                    // Validate the user data before accepting it
                    if (userData && userData.username && userData.loginTime) {
                        const loginTime = new Date(userData.loginTime);
                        const now = new Date();
                        const timeDiff = now.getTime() - loginTime.getTime();
                        
                        // Check if session is still valid (30 minutes)
                        if (timeDiff < CONFIG.SESSION_TIMEOUT) {
                            this.currentUser = userData;
                            this.updateAuthUI(userData);
                            this.setupSessionMonitoring();
                            console.log('✅ Authentication status: Logged in as', userData.username);
                            return;
                        } else {
                            console.log('⚠️ Session expired, clearing auth data');
                            this.forceResetState();
                        }
                    } else {
                        console.log('⚠️ Invalid user data, clearing auth data');
                        this.forceResetState();
                    }
                } catch (error) {
                    console.error('❌ Error parsing auth data:', error);
                    this.forceResetState();
                }
            }
            
            // If we reach here, user is not logged in
            console.log('🔒 Authentication status: Not logged in');
            this.currentUser = null;
            this.updateAuthUI(null);
        }

        // Update UI based on authentication state
        updateAuthUI(userData) {
            const signInContainer = document.getElementById('signInContainer');
            const userDropdownContainer = document.getElementById('userDropdownContainer');
            const userName = document.getElementById('userName');
            const adminLink = document.getElementById('adminDashboardLink');
            
            console.log('🔄 Updating auth UI for user:', userData);
            
            if (userData && userData.username) {
                // User is logged in - show user dropdown, hide sign-in button
                console.log('🔓 Showing authenticated UI state');
                
                if (signInContainer) {
                    signInContainer.classList.add('d-none', 'hide-signin');
                    signInContainer.style.display = 'none';
                    console.log('✅ Sign-in container hidden');
                }
                
                if (userDropdownContainer) {
                    userDropdownContainer.classList.remove('d-none');
                    userDropdownContainer.classList.add('show-user');
                    userDropdownContainer.style.display = 'flex';
                    console.log('✅ User dropdown container shown');
                }
                
                if (userName) {
                    userName.textContent = userData.username;
                    console.log('✅ Username set to:', userData.username);
                }
                
                // Update admin dashboard link visibility based on role
                if (adminLink) {
                    const adminListItem = adminLink.parentElement;
                    if (userData.role === 'Admin' || userData.role === 'Employee') {
                        adminListItem.style.display = 'block';
                        console.log('✅ Admin link shown for role:', userData.role);
                    } else {
                        adminListItem.style.display = 'none';
                        console.log('ℹ️ Admin link hidden for role:', userData.role);
                    }
                }
            } else {
                // User is not logged in - show sign-in button, hide user dropdown
                console.log('🔒 Showing unauthenticated UI state');
                
                if (signInContainer) {
                    signInContainer.classList.remove('d-none', 'hide-signin');
                    signInContainer.style.display = 'flex';
                    console.log('✅ Sign-in container shown');
                }
                
                if (userDropdownContainer) {
                    userDropdownContainer.classList.add('d-none');
                    userDropdownContainer.classList.remove('show-user');
                    userDropdownContainer.style.display = 'none';
                    console.log('✅ User dropdown container hidden');
                }
                
                // Clear username
                if (userName) {
                    userName.textContent = 'ผู้ใช้งาน';
                }
            }
            
            console.log('✅ Auth UI update completed');
        }

        // Show/hide loading state on login button
        showLoading(show) {
            // Try new modal loading function first
            if (typeof window.showModalLoading === 'function') {
                window.showModalLoading(show);
                return;
            }
            
            // Fallback to original implementation
            const btn = document.getElementById('loginSubmitBtn');
            if (!btn) return;
            
            // Try new modal structure first
            let text = btn.querySelector('.btn-content');
            let spinner = btn.querySelector('.btn-loading');
            
            // Fallback to old structure
            if (!text) text = btn.querySelector('.login-text');
            if (!spinner) spinner = btn.querySelector('.login-spinner');
            
            if (show) {
                if (text) text.classList.add('d-none');
                if (spinner) spinner.classList.remove('d-none');
                btn.disabled = true;
                btn.style.cursor = 'not-allowed';
            } else {
                if (text) text.classList.remove('d-none');
                if (spinner) spinner.classList.add('d-none');
                btn.disabled = false;
                btn.style.cursor = 'pointer';
            }
        }

        // Show notification message
        showNotification(message, type = 'info') {
            // Try to use global showNotification if available (from _Layout.cshtml)
            if (typeof window.showNotification === 'function') {
                window.showNotification(message, type);
                return;
            }

            // Fallback implementation
            let container = document.getElementById('notificationContainer');
            if (!container) {
                container = document.createElement('div');
                container.id = 'notificationContainer';
                container.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 9999;';
                document.body.appendChild(container);
            }
            
            const notification = document.createElement('div');
            
            const colors = {
                success: 'alert-success',
                error: 'alert-danger',
                warning: 'alert-warning',
                info: 'alert-info'
            };
            
            const icons = {
                success: 'fa-check-circle',
                error: 'fa-exclamation-circle',
                warning: 'fa-exclamation-triangle',
                info: 'fa-info-circle'
            };
            
            notification.className = `alert ${colors[type]} alert-dismissible fade show shadow-sm`;
            notification.style.minWidth = '300px';
            notification.innerHTML = `
                <i class="fas ${icons[type]} me-2"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            `;
            
            container.appendChild(notification);
            
            // Auto remove after 5 seconds
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.classList.remove('show');
                    setTimeout(() => notification.remove(), 150);
                }
            }, 5000);
        }

        // Public API methods
        isLoggedIn() {
            return this.currentUser !== null && sessionStorage.getItem('authUser') !== null;
        }

        getCurrentUser() {
            return this.currentUser;
        }

        getUserRole() {
            return this.currentUser ? this.currentUser.role : null;
        }

        hasRole(requiredRole) {
            if (!this.currentUser) return false;
            
            const roleHierarchy = {
                'Admin': 3,
                'Employee': 2,
                'User': 1
            };
            
            const currentRoleLevel = roleHierarchy[this.currentUser.role] || 0;
            const requiredRoleLevel = roleHierarchy[requiredRole] || 0;
            
            return currentRoleLevel >= requiredRoleLevel;
        }
    }

    // Create global instance
    const authManager = new BizConnectAuth();

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            authManager.init();
        });
    } else {
        authManager.init();
    }

    // Debug and verification tools
    const debugTools = {
        verifyAuthState: function() {
            console.log('🔍 DEBUG: Authentication State Verification');
            console.log('Current User:', authManager.currentUser);
            console.log('Session Storage:', sessionStorage.getItem('authUser'));
            console.log('Is Logged In:', authManager.isLoggedIn());
            
            const signInContainer = document.getElementById('signInContainer');
            const userDropdownContainer = document.getElementById('userDropdownContainer');
            
            console.log('Sign-in Container:', {
                element: signInContainer,
                classList: signInContainer?.classList.toString(),
                display: signInContainer?.style.display,
                visible: signInContainer && !signInContainer.classList.contains('d-none') && signInContainer.style.display !== 'none'
            });
            
            console.log('User Dropdown Container:', {
                element: userDropdownContainer,
                classList: userDropdownContainer?.classList.toString(),
                display: userDropdownContainer?.style.display,
                visible: userDropdownContainer && !userDropdownContainer.classList.contains('d-none') && userDropdownContainer.style.display !== 'none'
            });
        },

        forceResetToUnauthenticated: function() {
            console.log('🚨 DEBUG: Force reset to unauthenticated state');
            authManager.forceResetState();
            authManager.verifyAuthenticationState();
        },

        simulateLogin: function(username = 'test', role = 'User') {
            console.log('🧪 DEBUG: Simulating login for:', username);
            const userData = {
                username: username,
                role: role,
                loginTime: new Date().toISOString(),
                isDemo: true
            };
            authManager.currentUser = userData;
            sessionStorage.setItem('authUser', JSON.stringify(userData));
            authManager.updateAuthUI(userData);
        }
    };

    // Export for use in other scripts
    window.authManager = {
        checkAuthStatus: authManager.checkAuthStatus.bind(authManager),
        updateAuthUI: authManager.updateAuthUI.bind(authManager),
        showNotification: authManager.showNotification.bind(authManager),
        handleSignOut: authManager.handleSignOut.bind(authManager),
        isLoggedIn: authManager.isLoggedIn.bind(authManager),
        getCurrentUser: authManager.getCurrentUser.bind(authManager),
        getUserRole: authManager.getUserRole.bind(authManager),
        hasRole: authManager.hasRole.bind(authManager),
        
        // Debug tools (only available in development)
        debug: window.location.hostname === 'localhost' ? debugTools : {}
    };

    // Global debug functions for console access
    if (window.location.hostname === 'localhost') {
        window.debugAuth = debugTools.verifyAuthState;
        window.debugForceReset = debugTools.forceResetToUnauthenticated;
        window.debugSimulateLogin = debugTools.simulateLogin;
        
        console.log('🧪 DEBUG: Authentication debug tools available:');
        console.log('  - debugAuth() - Verify current authentication state');
        console.log('  - debugForceReset() - Force reset to unauthenticated');
        console.log('  - debugSimulateLogin(username, role) - Simulate login');
    }

})();