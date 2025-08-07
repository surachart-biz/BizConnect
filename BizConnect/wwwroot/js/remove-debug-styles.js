/**
 * Remove Debug Styles Script
 * 
 * This script removes the temporary debugging styles once you confirm
 * that the 2-column layout is working correctly.
 * 
 * Usage:
 * 1. Include this script after confirming 2-column layout works
 * 2. Or manually remove the CSS debug section from register.css
 */

document.addEventListener('DOMContentLoaded', function() {
    // Function to remove debug styling
    function removeDebugStyles() {
        console.log('🧹 Removing debug styles for production...');
        
        // Create a style element to override debug styles
        const cleanupStyle = document.createElement('style');
        cleanupStyle.id = 'cleanup-debug-styles';
        cleanupStyle.innerHTML = `
            /* Remove all debugging borders and backgrounds */
            @media (min-width: 768px) {
                .form-section .col-md-6,
                .form-section .col-md-8,
                .form-section .col-md-4 {
                    border: none !important;
                    background: transparent !important;
                }
                
                /* Remove all debug labels */
                .form-section .col-md-6::after,
                .form-section .col-md-8::after,
                .form-section .col-md-4::after,
                .form-section .row.g-4::after {
                    display: none !important;
                    content: none !important;
                }
            }
        `;
        
        document.head.appendChild(cleanupStyle);
        console.log('✅ Debug styles removed successfully');
        
        // Optional: Show confirmation message
        if (window.showNotification) {
            window.showNotification('Debug styles removed - form is now production-ready', 'success');
        }
    }
    
    // Auto-remove debug styles after 10 seconds (for demonstration)
    // Comment this out if you want to manually control when to remove
    /*
    setTimeout(() => {
        console.log('⏰ Auto-removing debug styles after 10 seconds...');
        removeDebugStyles();
    }, 10000);
    */
    
    // Add a button to manually remove debug styles
    const debugControlsContainer = document.createElement('div');
    debugControlsContainer.id = 'debug-controls';
    debugControlsContainer.style.cssText = `
        position: fixed;
        top: 10px;
        left: 10px;
        z-index: 9999;
        background: rgba(40, 167, 69, 0.95);
        color: white;
        padding: 12px 16px;
        border-radius: 8px;
        font-family: 'Sarabun', sans-serif;
        font-size: 12px;
        font-weight: 600;
        box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        border: 2px solid rgba(255,255,255,0.3);
        backdrop-filter: blur(10px);
    `;
    
    debugControlsContainer.innerHTML = `
        <div style="margin-bottom: 8px; font-weight: 700;">🔧 DEBUG MODE ACTIVE</div>
        <div style="margin-bottom: 8px; font-size: 11px;">Verify 2-column layout is working</div>
        <button id="removeDebugBtn" style="
            background: white;
            color: #2e7d32;
            border: none;
            padding: 6px 12px;
            border-radius: 4px;
            font-size: 10px;
            font-weight: 700;
            cursor: pointer;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        ">Remove Debug Styles</button>
    `;
    
    document.body.appendChild(debugControlsContainer);
    
    // Add click event to remove button
    document.getElementById('removeDebugBtn').addEventListener('click', function() {
        removeDebugStyles();
        debugControlsContainer.remove();
    });
    
    // Make functions available globally for manual control
    window.removeDebugStyles = removeDebugStyles;
    window.debugControls = debugControlsContainer;
    
    console.log('🛠️ Debug mode active. Call removeDebugStyles() to clean up when ready.');
});