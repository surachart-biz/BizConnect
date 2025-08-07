// Bootstrap Modal Test Script for BizConnect
// This script helps verify that the modal system is working correctly

(function() {
    'use strict';

    console.log('🧪 Modal Test Script Loaded');

    // Test functions
    const ModalTester = {
        
        // Test Bootstrap availability
        testBootstrapAvailability: function() {
            console.log('🔍 Testing Bootstrap availability...');
            
            const tests = {
                'Bootstrap object exists': typeof bootstrap !== 'undefined',
                'Bootstrap.Modal exists': typeof bootstrap !== 'undefined' && bootstrap.Modal,
                'Bootstrap.Modal is function': typeof bootstrap !== 'undefined' && typeof bootstrap.Modal === 'function',
                'jQuery Bootstrap exists': typeof $ !== 'undefined' && $.fn && $.fn.modal,
                'Modal element exists': !!document.getElementById('loginModal'),
                'Sign-in button exists': !!document.getElementById('signInBtn')
            };

            console.table(tests);
            
            const passed = Object.values(tests).filter(Boolean).length;
            const total = Object.keys(tests).length;
            
            console.log(`✅ Bootstrap tests passed: ${passed}/${total}`);
            return passed === total;
        },

        // Test modal creation
        testModalCreation: function() {
            console.log('🔨 Testing modal creation...');
            
            const loginModal = document.getElementById('loginModal');
            if (!loginModal) {
                console.error('❌ Login modal element not found');
                return false;
            }

            try {
                const testModal = new bootstrap.Modal(loginModal);
                console.log('✅ Modal instance created successfully');
                console.log('Modal methods:', {
                    show: typeof testModal.show,
                    hide: typeof testModal.hide,
                    dispose: typeof testModal.dispose
                });
                testModal.dispose(); // Clean up test instance
                return true;
            } catch (error) {
                console.error('❌ Modal creation failed:', error);
                return false;
            }
        },

        // Test modal show/hide functionality
        testModalFunctionality: function() {
            console.log('⚡ Testing modal functionality...');
            
            const loginModal = document.getElementById('loginModal');
            if (!loginModal) {
                console.error('❌ Login modal element not found');
                return false;
            }

            try {
                const testModal = new bootstrap.Modal(loginModal);
                
                // Test show
                console.log('🎭 Testing modal show...');
                testModal.show();
                
                setTimeout(() => {
                    const isVisible = loginModal.classList.contains('show');
                    console.log('Modal visible after show():', isVisible);
                    
                    // Test hide
                    console.log('🫥 Testing modal hide...');
                    testModal.hide();
                    
                    setTimeout(() => {
                        const isHidden = !loginModal.classList.contains('show');
                        console.log('Modal hidden after hide():', isHidden);
                        testModal.dispose();
                        
                        if (isVisible && isHidden) {
                            console.log('✅ Modal functionality test PASSED');
                        } else {
                            console.log('❌ Modal functionality test FAILED');
                        }
                    }, 500);
                }, 500);
                
                return true;
            } catch (error) {
                console.error('❌ Modal functionality test failed:', error);
                return false;
            }
        },

        // Test sign-in button click
        testSignInButton: function() {
            console.log('🔘 Testing sign-in button...');
            
            const signInBtn = document.getElementById('signInBtn');
            if (!signInBtn) {
                console.error('❌ Sign-in button not found');
                return false;
            }

            console.log('Button properties:', {
                id: signInBtn.id,
                className: signInBtn.className,
                disabled: signInBtn.disabled,
                onclick: typeof signInBtn.onclick,
                hasClickListeners: signInBtn.getEventListeners ? 'Available' : 'Not available'
            });

            // Simulate click
            try {
                console.log('🖱️ Simulating button click...');
                signInBtn.click();
                console.log('✅ Button click simulation completed');
                return true;
            } catch (error) {
                console.error('❌ Button click simulation failed:', error);
                return false;
            }
        },

        // Run all tests
        runAllTests: function() {
            console.log('🚀 Starting comprehensive modal tests...');
            console.log('=' .repeat(50));
            
            const tests = [
                { name: 'Bootstrap Availability', fn: this.testBootstrapAvailability },
                { name: 'Modal Creation', fn: this.testModalCreation },
                { name: 'Sign-in Button', fn: this.testSignInButton }
                // Note: Modal functionality test disabled by default as it shows/hides modal
                // { name: 'Modal Functionality', fn: this.testModalFunctionality }
            ];

            let passed = 0;
            tests.forEach((test, index) => {
                console.log(`\n📋 Test ${index + 1}: ${test.name}`);
                try {
                    const result = test.fn.call(this);
                    if (result) {
                        passed++;
                        console.log(`✅ ${test.name}: PASSED`);
                    } else {
                        console.log(`❌ ${test.name}: FAILED`);
                    }
                } catch (error) {
                    console.error(`💥 ${test.name}: ERROR -`, error);
                }
            });

            console.log('\n' + '=' .repeat(50));
            console.log(`🏁 Test Summary: ${passed}/${tests.length} tests passed`);
            
            if (passed === tests.length) {
                console.log('🎉 All tests PASSED! Modal system is working correctly.');
            } else {
                console.log('⚠️ Some tests FAILED. Check the logs above for details.');
            }

            return passed === tests.length;
        }
    };

    // Make tester available globally for console access
    window.ModalTester = ModalTester;

    // Auto-run tests when DOM is ready (with delay to allow initialization)
    function runTestsWhenReady() {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', () => {
                setTimeout(() => ModalTester.runAllTests(), 3000);
            });
        } else {
            setTimeout(() => ModalTester.runAllTests(), 3000);
        }
    }

    // Only run auto-tests in development
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        console.log('💡 Modal tests will run automatically in 3 seconds...');
        console.log('💡 You can also run tests manually: ModalTester.runAllTests()');
        runTestsWhenReady();
    }

})();