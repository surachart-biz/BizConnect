// BizConnect Login Modal - Correctness Test Suite
// This file provides testing functions to verify all modal fixes are working

(function() {
    'use strict';

    // Test Suite Configuration
    const TESTS = {
        thaiTextRendering: 'Thai Text Rendering',
        buttonLoadingState: 'Button Loading State',
        demoCredentials: 'Demo Credentials Visibility',
        formValidation: 'Form Validation',
        modalAnimations: 'Modal Animations',
        accessibilityFeatures: 'Accessibility Features'
    };

    class ModalCorrectnessTest {
        constructor() {
            this.testResults = {};
            this.modal = null;
            this.modalInstance = null;
        }

        // Initialize test suite
        init() {
            console.log('🧪 BizConnect Modal Correctness Test Suite v1.0');
            console.log('Available tests:', Object.keys(TESTS).join(', '));
            
            this.modal = document.getElementById('loginModal');
            if (!this.modal) {
                console.error('❌ Login modal not found');
                return false;
            }

            this.modalInstance = window.globalModalInstance || 
                (typeof bootstrap !== 'undefined' ? new bootstrap.Modal(this.modal) : null);
            
            if (!this.modalInstance) {
                console.warn('⚠️ Modal instance not available, some tests may be limited');
            }

            return true;
        }

        // Test 1: Thai Text Rendering
        testThaiTextRendering() {
            const testName = TESTS.thaiTextRendering;
            console.log(`🔍 Testing: ${testName}`);

            const results = {
                passed: 0,
                failed: 0,
                details: []
            };

            // Check username label
            const usernameLabel = document.querySelector('label[for="username"]');
            if (usernameLabel) {
                const text = usernameLabel.textContent.trim();
                const isCorrect = text === 'ชื่อผู้ใช้งาน';
                results[isCorrect ? 'passed' : 'failed']++;
                results.details.push(`Username label: ${text} ${isCorrect ? '✅' : '❌ (should be "ชื่อผู้ใช้งาน")'}`);
            }

            // Check validation message
            const validationMsg = document.querySelector('#username ~ .field-validation-container .validation-message');
            if (validationMsg) {
                const text = validationMsg.textContent.trim();
                const isCorrect = text === 'กรุณากรอกชื่อผู้ใช้งาน';
                results[isCorrect ? 'passed' : 'failed']++;
                results.details.push(`Validation message: ${text} ${isCorrect ? '✅' : '❌ (should be "กรุณากรอกชื่อผู้ใช้งาน")'}`);
            }

            // Check font family application
            const elements = document.querySelectorAll('.floating-label, .validation-message');
            let fontTestPassed = 0;
            elements.forEach(el => {
                const computedStyle = window.getComputedStyle(el);
                const fontFamily = computedStyle.fontFamily;
                if (fontFamily.includes('Sarabun')) {
                    fontTestPassed++;
                }
            });
            
            const allFontsCorrect = fontTestPassed === elements.length;
            results[allFontsCorrect ? 'passed' : 'failed']++;
            results.details.push(`Font family: ${fontTestPassed}/${elements.length} elements use Sarabun ${allFontsCorrect ? '✅' : '❌'}`);

            this.testResults[testName] = results;
            this.logTestResults(testName, results);
            return results;
        }

        // Test 2: Button Loading State
        testButtonLoadingState() {
            const testName = TESTS.buttonLoadingState;
            console.log(`🔍 Testing: ${testName}`);

            const results = {
                passed: 0,
                failed: 0,
                details: []
            };

            const submitBtn = document.getElementById('loginSubmitBtn');
            const btnContent = submitBtn?.querySelector('.btn-content');
            const btnLoading = submitBtn?.querySelector('.btn-loading');

            if (!submitBtn || !btnContent || !btnLoading) {
                results.failed++;
                results.details.push('Button structure incomplete ❌');
                this.testResults[testName] = results;
                return results;
            }

            // Test loading function exists
            if (typeof window.showModalLoading === 'function') {
                results.passed++;
                results.details.push('showModalLoading function exists ✅');
            } else {
                results.failed++;
                results.details.push('showModalLoading function missing ❌');
            }

            // Test initial state
            const initialContentVisible = !btnContent.classList.contains('d-none');
            const initialLoadingHidden = btnLoading.classList.contains('d-none');
            
            if (initialContentVisible && initialLoadingHidden) {
                results.passed++;
                results.details.push('Initial button state correct ✅');
            } else {
                results.failed++;
                results.details.push(`Initial state incorrect: content visible=${initialContentVisible}, loading hidden=${initialLoadingHidden} ❌`);
            }

            this.testResults[testName] = results;
            this.logTestResults(testName, results);
            return results;
        }

        // Test 3: Demo Credentials Visibility
        testDemoCredentials() {
            const testName = TESTS.demoCredentials;
            console.log(`🔍 Testing: ${testName}`);

            const results = {
                passed: 0,
                failed: 0,
                details: []
            };

            const demoSection = document.querySelector('.demo-credentials');
            const demoAccounts = document.querySelectorAll('.demo-account');

            if (!demoSection) {
                results.failed++;
                results.details.push('Demo credentials section not found ❌');
            } else {
                const sectionVisible = window.getComputedStyle(demoSection).display !== 'none';
                if (sectionVisible) {
                    results.passed++;
                    results.details.push('Demo credentials section visible ✅');
                } else {
                    results.failed++;
                    results.details.push('Demo credentials section hidden ❌');
                }
            }

            if (demoAccounts.length === 3) {
                results.passed++;
                results.details.push(`Demo accounts count: ${demoAccounts.length}/3 ✅`);
            } else {
                results.failed++;
                results.details.push(`Demo accounts count: ${demoAccounts.length}/3 ❌`);
            }

            // Test account data attributes
            let accountDataCorrect = 0;
            demoAccounts.forEach((account, index) => {
                const username = account.dataset.username;
                const password = account.dataset.password;
                if (username && password) {
                    accountDataCorrect++;
                }
            });

            if (accountDataCorrect === demoAccounts.length) {
                results.passed++;
                results.details.push('All demo accounts have data attributes ✅');
            } else {
                results.failed++;
                results.details.push(`Demo account data: ${accountDataCorrect}/${demoAccounts.length} complete ❌`);
            }

            this.testResults[testName] = results;
            this.logTestResults(testName, results);
            return results;
        }

        // Test 4: Form Validation
        testFormValidation() {
            const testName = TESTS.formValidation;
            console.log(`🔍 Testing: ${testName}`);

            const results = {
                passed: 0,
                failed: 0,
                details: []
            };

            const usernameField = document.getElementById('username');
            const passwordField = document.getElementById('password');
            const form = document.getElementById('loginForm');

            if (!usernameField || !passwordField || !form) {
                results.failed++;
                results.details.push('Form elements missing ❌');
                this.testResults[testName] = results;
                return results;
            }

            // Test validation function exists
            if (typeof window.validateField === 'function' || typeof validateField === 'function') {
                results.passed++;
                results.details.push('Validation function exists ✅');
            } else {
                results.failed++;
                results.details.push('Validation function missing ❌');
            }

            // Test error display function
            if (typeof window.showFormError === 'function' || typeof showFormError === 'function') {
                results.passed++;
                results.details.push('Error display function exists ✅');
            } else {
                results.failed++;
                results.details.push('Error display function missing ❌');
            }

            // Test validation container structure
            const validationContainers = document.querySelectorAll('.field-validation-container');
            if (validationContainers.length >= 2) {
                results.passed++;
                results.details.push(`Validation containers: ${validationContainers.length} ✅`);
            } else {
                results.failed++;
                results.details.push(`Validation containers: ${validationContainers.length} ❌`);
            }

            this.testResults[testName] = results;
            this.logTestResults(testName, results);
            return results;
        }

        // Test 5: Modal Animations
        testModalAnimations() {
            const testName = TESTS.modalAnimations;
            console.log(`🔍 Testing: ${testName}`);

            const results = {
                passed: 0,
                failed: 0,
                details: []
            };

            // Check CSS animations exist
            const styleSheets = document.styleSheets;
            let animationsFound = 0;
            
            try {
                for (let sheet of styleSheets) {
                    try {
                        const rules = sheet.cssRules || sheet.rules;
                        for (let rule of rules) {
                            if (rule.type === CSSRule.KEYFRAMES_RULE) {
                                if (['fadeInUp', 'spin', 'inputError'].includes(rule.name)) {
                                    animationsFound++;
                                }
                            }
                        }
                    } catch (e) {
                        // Skip cross-origin stylesheets
                    }
                }
            } catch (e) {
                console.warn('Could not check CSS animations:', e.message);
            }

            if (animationsFound > 0) {
                results.passed++;
                results.details.push(`CSS animations found: ${animationsFound} ✅`);
            } else {
                results.failed++;
                results.details.push('CSS animations not detected ❌');
            }

            // Check modal transition classes
            const modalDialog = this.modal?.querySelector('.modal-dialog');
            if (modalDialog) {
                const hasTransitions = window.getComputedStyle(modalDialog).transition !== 'all 0s ease 0s';
                if (hasTransitions) {
                    results.passed++;
                    results.details.push('Modal dialog has transitions ✅');
                } else {
                    results.failed++;
                    results.details.push('Modal dialog missing transitions ❌');
                }
            }

            this.testResults[testName] = results;
            this.logTestResults(testName, results);
            return results;
        }

        // Test 6: Accessibility Features
        testAccessibilityFeatures() {
            const testName = TESTS.accessibilityFeatures;
            console.log(`🔍 Testing: ${testName}`);

            const results = {
                passed: 0,
                failed: 0,
                details: []
            };

            // Check ARIA attributes
            const modalHasAria = this.modal?.getAttribute('aria-labelledby') !== null;
            if (modalHasAria) {
                results.passed++;
                results.details.push('Modal has ARIA attributes ✅');
            } else {
                results.failed++;
                results.details.push('Modal missing ARIA attributes ❌');
            }

            // Check form labels
            const inputs = this.modal?.querySelectorAll('input');
            let labelsCorrect = 0;
            inputs?.forEach(input => {
                const label = document.querySelector(`label[for="${input.id}"]`);
                if (label) labelsCorrect++;
            });

            if (inputs && labelsCorrect === inputs.length) {
                results.passed++;
                results.details.push(`Input labels: ${labelsCorrect}/${inputs.length} ✅`);
            } else {
                results.failed++;
                results.details.push(`Input labels: ${labelsCorrect}/${inputs?.length || 0} ❌`);
            }

            // Check keyboard navigation
            const focusableElements = this.modal?.querySelectorAll('input, button, [tabindex]');
            if (focusableElements && focusableElements.length > 0) {
                results.passed++;
                results.details.push(`Focusable elements: ${focusableElements.length} ✅`);
            } else {
                results.failed++;
                results.details.push('No focusable elements found ❌');
            }

            this.testResults[testName] = results;
            this.logTestResults(testName, results);
            return results;
        }

        // Run all tests
        runAllTests() {
            console.log('🚀 Running BizConnect Modal Correctness Test Suite...\n');
            
            if (!this.init()) {
                console.error('❌ Test suite initialization failed');
                return;
            }

            const testMethods = [
                'testThaiTextRendering',
                'testButtonLoadingState', 
                'testDemoCredentials',
                'testFormValidation',
                'testModalAnimations',
                'testAccessibilityFeatures'
            ];

            testMethods.forEach(method => {
                this[method]();
            });

            this.generateSummaryReport();
        }

        // Log test results
        logTestResults(testName, results) {
            const total = results.passed + results.failed;
            const percentage = total > 0 ? Math.round((results.passed / total) * 100) : 0;
            const status = percentage === 100 ? '✅ PASSED' : 
                          percentage >= 70 ? '⚠️ WARNING' : '❌ FAILED';
            
            console.log(`📊 ${testName}: ${results.passed}/${total} (${percentage}%) ${status}`);
            results.details.forEach(detail => console.log(`   ${detail}`));
            console.log('');
        }

        // Generate summary report
        generateSummaryReport() {
            console.log('📋 MODAL CORRECTNESS TEST SUMMARY');
            console.log('═══════════════════════════════════════');
            
            let totalPassed = 0;
            let totalTests = 0;
            
            Object.keys(this.testResults).forEach(testName => {
                const result = this.testResults[testName];
                const testTotal = result.passed + result.failed;
                const percentage = testTotal > 0 ? Math.round((result.passed / testTotal) * 100) : 0;
                
                totalPassed += result.passed;
                totalTests += testTotal;
                
                console.log(`${testName}: ${percentage}% (${result.passed}/${testTotal})`);
            });
            
            const overallPercentage = totalTests > 0 ? Math.round((totalPassed / totalTests) * 100) : 0;
            const overallStatus = overallPercentage === 100 ? '✅ ALL TESTS PASSED' :
                                 overallPercentage >= 80 ? '⚠️ MOSTLY WORKING' : '❌ NEEDS ATTENTION';
            
            console.log('');
            console.log(`OVERALL SCORE: ${overallPercentage}% (${totalPassed}/${totalTests}) ${overallStatus}`);
            
            if (overallPercentage < 100) {
                console.log('\n🔧 RECOMMENDED ACTIONS:');
                Object.keys(this.testResults).forEach(testName => {
                    const result = this.testResults[testName];
                    if (result.failed > 0) {
                        console.log(`- Fix issues in: ${testName}`);
                        result.details.forEach(detail => {
                            if (detail.includes('❌')) {
                                console.log(`  → ${detail}`);
                            }
                        });
                    }
                });
            }
        }

        // Interactive test functions
        testButtonLoadingLive() {
            console.log('🧪 Testing button loading state interactively...');
            if (typeof window.showModalLoading === 'function') {
                console.log('Showing loading state...');
                window.showModalLoading(true);
                
                setTimeout(() => {
                    console.log('Hiding loading state...');
                    window.showModalLoading(false);
                    console.log('✅ Button loading test completed');
                }, 2000);
            } else {
                console.error('❌ showModalLoading function not available');
            }
        }

        testDemoCredentialsLive() {
            console.log('🧪 Testing demo credentials click functionality...');
            const demoAccounts = document.querySelectorAll('.demo-account');
            if (demoAccounts.length > 0) {
                console.log('Clicking first demo account...');
                demoAccounts[0].click();
                
                setTimeout(() => {
                    const username = document.getElementById('username')?.value;
                    const password = document.getElementById('password')?.value;
                    console.log(`Username filled: "${username}"`);
                    console.log(`Password filled: "${password}"`);
                    console.log('✅ Demo credentials test completed');
                }, 1000);
            } else {
                console.error('❌ No demo accounts found');
            }
        }
    }

    // Create global test instance
    const modalTest = new ModalCorrectnessTest();

    // Export functions for console access
    window.testModal = {
        runAll: () => modalTest.runAllTests(),
        thaiText: () => modalTest.testThaiTextRendering(),
        buttonLoading: () => modalTest.testButtonLoadingState(),
        buttonLoadingLive: () => modalTest.testButtonLoadingLive(),
        demoCredentials: () => modalTest.testDemoCredentials(),
        demoCredentialsLive: () => modalTest.testDemoCredentialsLive(),
        formValidation: () => modalTest.testFormValidation(),
        animations: () => modalTest.testModalAnimations(),
        accessibility: () => modalTest.testAccessibilityFeatures()
    };

    console.log('🧪 Modal Correctness Test Suite loaded');
    console.log('Available commands:');
    console.log('  testModal.runAll() - Run all tests');
    console.log('  testModal.thaiText() - Test Thai text rendering');
    console.log('  testModal.buttonLoading() - Test button states');
    console.log('  testModal.buttonLoadingLive() - Interactive button test');
    console.log('  testModal.demoCredentials() - Test demo accounts');
    console.log('  testModal.demoCredentialsLive() - Interactive demo test');
    console.log('  testModal.formValidation() - Test form validation');
    console.log('  testModal.animations() - Test animations');
    console.log('  testModal.accessibility() - Test accessibility features');

})();