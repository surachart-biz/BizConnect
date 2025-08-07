// Authentication Management System for BizConnect

document.addEventListener('DOMContentLoaded', function() {
    // Check if user is already logged in
    checkAuthStatus();
    
    // Language toggle
    const langTH = document.getElementById('langTH');
    const langEN = document.getElementById('langEN');
    
    if (langTH && langEN) {
        langTH.addEventListener('click', () => switchLanguage('th'));
        langEN.addEventListener('click', () => switchLanguage('en'));
    }
    
    // Login form
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', handleLogin);
    }
    
    // Sign out
    const signOutBtn = document.getElementById('signOutBtn');
    if (signOutBtn) {
        signOutBtn.addEventListener('click', handleSignOut);
    }
    
    // Admin dashboard link
    const adminDashboardLink = document.getElementById('adminDashboardLink');
    if (adminDashboardLink) {
        adminDashboardLink.addEventListener('click', function(e) {
            e.preventDefault();
            // In production, this would redirect to the actual admin dashboard
            window.location.href = '/Admin/Dashboard';
        });
    }
});

function switchLanguage(lang) {
    // Update active button
    const langTH = document.getElementById('langTH');
    const langEN = document.getElementById('langEN');
    
    if (langTH && langEN) {
        langTH.className = lang === 'th' ? 'btn btn-secondary btn-sm' : 'btn btn-outline-secondary btn-sm';
        langEN.className = lang === 'en' ? 'btn btn-secondary btn-sm' : 'btn btn-outline-secondary btn-sm';
    }
    
    // Store language preference
    sessionStorage.setItem('language', lang);
    localStorage.setItem('preferredLanguage', lang);
    
    // Show notification
    showNotification(lang === 'th' ? 'เปลี่ยนเป็นภาษาไทยแล้ว' : 'Changed to English', 'success');
    
    // In real app, this would trigger interface language change
    updateUILanguage(lang);
}

function updateUILanguage(lang) {
    // This function would update all UI text based on language
    // For now, it's a placeholder for future implementation
    const translations = {
        'th': {
            'signIn': 'เข้าสู่ระบบ',
            'signOut': 'ออกจากระบบ',
            'username': 'ชื่อผู้ใช้งาน',
            'password': 'รหัสผ่าน',
            'adminDashboard': 'แผงควบคุมผู้ดูแล',
            'profile': 'จัดการโปรไฟล์'
        },
        'en': {
            'signIn': 'Sign In',
            'signOut': 'Sign Out',
            'username': 'Username',
            'password': 'Password',
            'adminDashboard': 'Admin Dashboard',
            'profile': 'Manage Profile'
        }
    };
}

async function handleLogin(e) {
    e.preventDefault();
    
    const username = document.getElementById('username').value.trim();
    const password = document.getElementById('password').value;
    
    if (!username || !password) {
        showNotification('กรุณากรอกข้อมูลให้ครบถ้วน', 'warning');
        return;
    }
    
    // Show loading
    showLoading(true);
    
    try {
        // Make actual API call to login endpoint
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
            // Store user data
            const userData = {
                username: result.username || username,
                role: result.role || 'User',
                loginTime: new Date().toISOString()
            };
            
            sessionStorage.setItem('authUser', JSON.stringify(userData));
            
            // Hide modal
            const modal = bootstrap.Modal.getInstance(document.getElementById('loginModal'));
            if (modal) {
                modal.hide();
            }
            
            // Update UI
            updateAuthUI(userData);
            
            showNotification(`เข้าสู่ระบบสำเร็จ! ยินดีต้อนรับ ${userData.username}`, 'success');
            
            // Redirect if needed
            if (result.redirectUrl) {
                setTimeout(() => {
                    window.location.href = result.redirectUrl;
                }, 1500);
            }
        } else {
            // For demo purposes, check hardcoded credentials
            handleDemoLogin(username, password);
        }
    } catch (error) {
        console.error('Login error:', error);
        // Fallback to demo login for testing
        handleDemoLogin(username, password);
    }
    
    showLoading(false);
}

function handleDemoLogin(username, password) {
    // Demo credentials for testing
    const credentials = {
        'admin': { password: 'admin123', role: 'Admin' },
        'employee': { password: 'emp123', role: 'Employee' },
        'test': { password: 'test123', role: 'User' }
    };
    
    if (credentials[username] && credentials[username].password === password) {
        // Success
        const userData = {
            username: username,
            role: credentials[username].role,
            loginTime: new Date().toISOString()
        };
        
        sessionStorage.setItem('authUser', JSON.stringify(userData));
        
        // Hide modal
        const modal = bootstrap.Modal.getInstance(document.getElementById('loginModal'));
        if (modal) {
            modal.hide();
        }
        
        // Update UI
        updateAuthUI(userData);
        
        showNotification(`เข้าสู่ระบบสำเร็จ! ยินดีต้อนรับ ${username}`, 'success');
        
        // Redirect admin users to dashboard
        if (userData.role === 'Admin') {
            setTimeout(() => {
                window.location.href = '/Admin/Dashboard';
            }, 1500);
        }
    } else {
        showNotification('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง', 'error');
    }
}

async function handleSignOut() {
    try {
        // Call logout endpoint
        const response = await fetch('/Account/Logout', {
            method: 'POST',
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        });
        
        if (response.ok) {
            sessionStorage.removeItem('authUser');
            updateAuthUI(null);
            showNotification('ออกจากระบบเรียบร้อยแล้ว', 'info');
            
            // Redirect to home
            setTimeout(() => {
                window.location.href = '/';
            }, 1000);
        }
    } catch (error) {
        console.error('Logout error:', error);
        // Fallback - clear session and update UI
        sessionStorage.removeItem('authUser');
        updateAuthUI(null);
        showNotification('ออกจากระบบเรียบร้อยแล้ว', 'info');
    }
}

function checkAuthStatus() {
    const authUser = sessionStorage.getItem('authUser');
    if (authUser) {
        try {
            const userData = JSON.parse(authUser);
            updateAuthUI(userData);
        } catch (error) {
            console.error('Error parsing auth data:', error);
            sessionStorage.removeItem('authUser');
        }
    }
    
    // Check and apply saved language preference
    const savedLang = localStorage.getItem('preferredLanguage') || 'th';
    switchLanguage(savedLang);
}

function updateAuthUI(userData) {
    const signInBtn = document.getElementById('signInBtn');
    const userMenu = document.getElementById('userMenu');
    const userName = document.getElementById('userName');
    const adminLink = document.getElementById('adminDashboardLink');
    
    if (userData) {
        // Show user menu, hide sign in button
        if (signInBtn) signInBtn.classList.add('d-none');
        if (userMenu) userMenu.classList.remove('d-none');
        if (userName) userName.textContent = userData.username;
        
        // Update admin dashboard link visibility based on role
        if (adminLink) {
            if (userData.role === 'Admin' || userData.role === 'Employee') {
                adminLink.parentElement.style.display = 'block';
            } else {
                adminLink.parentElement.style.display = 'none';
            }
        }
    } else {
        // Show sign in button, hide user menu
        if (signInBtn) signInBtn.classList.remove('d-none');
        if (userMenu) userMenu.classList.add('d-none');
    }
}

function showLoading(show) {
    const btn = document.getElementById('loginSubmitBtn');
    if (!btn) return;
    
    const text = btn.querySelector('.login-text');
    const spinner = btn.querySelector('.login-spinner');
    
    if (show) {
        if (text) text.classList.add('d-none');
        if (spinner) spinner.classList.remove('d-none');
        btn.disabled = true;
    } else {
        if (text) text.classList.remove('d-none');
        if (spinner) spinner.classList.add('d-none');
        btn.disabled = false;
    }
}

function showNotification(message, type = 'info') {
    const container = document.getElementById('notificationContainer');
    if (!container) return;
    
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

// Export functions for use in other scripts
window.authManager = {
    checkAuthStatus,
    updateAuthUI,
    showNotification,
    handleSignOut,
    isLoggedIn: () => sessionStorage.getItem('authUser') !== null,
    getCurrentUser: () => {
        const authUser = sessionStorage.getItem('authUser');
        return authUser ? JSON.parse(authUser) : null;
    }
};