/**
 * BizConnect Guided Tour System
 * Provides interactive guided tours for new users
 */

class BizConnectGuidedTour {
    constructor() {
        this.currentStep = 0;
        this.steps = [];
        this.isActive = false;
        this.overlay = null;
        this.tooltip = null;
        this.init();
    }

    init() {
        this.createOverlay();
        this.createTooltip();
        this.setupEventListeners();
    }

    createOverlay() {
        this.overlay = document.createElement('div');
        this.overlay.className = 'bc-tour-overlay';
        this.overlay.innerHTML = `
            <div class="bc-tour-backdrop"></div>
            <div class="bc-tour-spotlight"></div>
        `;
        document.body.appendChild(this.overlay);
    }

    createTooltip() {
        this.tooltip = document.createElement('div');
        this.tooltip.className = 'bc-tour-tooltip';
        this.tooltip.innerHTML = `
            <div class="bc-tour-content">
                <div class="bc-tour-header">
                    <h3 class="bc-tour-title"></h3>
                    <button class="bc-tour-close" onclick="guidedTour.endTour()">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                <div class="bc-tour-body">
                    <p class="bc-tour-description"></p>
                </div>
                <div class="bc-tour-footer">
                    <div class="bc-tour-progress">
                        <span class="bc-tour-step-counter"></span>
                    </div>
                    <div class="bc-tour-actions">
                        <button class="btn btn-outline-secondary btn-sm" onclick="guidedTour.skipTour()">
                            Skip Tour
                        </button>
                        <button class="btn btn-outline-secondary btn-sm" onclick="guidedTour.previousStep()" id="tourPrevBtn">
                            <i class="fas fa-arrow-left me-1"></i>Previous
                        </button>
                        <button class="btn btn-primary btn-sm" onclick="guidedTour.nextStep()" id="tourNextBtn">
                            Next <i class="fas fa-arrow-right ms-1"></i>
                        </button>
                    </div>
                </div>
            </div>
            <div class="bc-tour-arrow"></div>
        `;
        document.body.appendChild(this.tooltip);
    }

    setupEventListeners() {
        // Close tour on escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && this.isActive) {
                this.endTour();
            }
        });

        // Prevent interactions with page elements during tour
        this.overlay.addEventListener('click', (e) => {
            if (e.target === this.overlay || e.target.classList.contains('bc-tour-backdrop')) {
                // Allow clicking on highlighted elements
                return;
            }
            e.preventDefault();
            e.stopPropagation();
        });
    }

    // Predefined tours
    startDashboardTour() {
        this.steps = [
            {
                element: '.bc-hero-dashboard',
                title: 'Welcome to Your Dashboard',
                description: 'This is your personalized dashboard where you can see your activity, connections, and opportunities at a glance.',
                position: 'bottom'
            },
            {
                element: '.bc-quick-actions',
                title: 'Quick Actions',
                description: 'Use these quick action cards to perform common tasks like finding connections, viewing jobs, and updating your profile.',
                position: 'top'
            },
            {
                element: '.bc-recent-activity',
                title: 'Recent Activity',
                description: 'Stay up to date with your recent connections, messages, and network activity.',
                position: 'top'
            },
            {
                element: '.navbar-nav',
                title: 'Navigation Menu',
                description: 'Use the navigation menu to access different sections of BizConnect. Everything you need is just a click away.',
                position: 'bottom'
            },
            {
                element: '.bc-user-menu',
                title: 'User Menu',
                description: 'Access your profile settings, preferences, and account options from your user menu.',
                position: 'bottom-left'
            }
        ];

        this.startTour();
    }

    startNetworkingTour() {
        this.steps = [
            {
                element: '.bc-search-section',
                title: 'Find Connections',
                description: 'Use the search functionality to find professionals in your industry or with specific skills.',
                position: 'bottom'
            },
            {
                element: '.bc-filter-options',
                title: 'Filter Results',
                description: 'Narrow down your search results using filters like location, industry, and experience level.',
                position: 'right'
            },
            {
                element: '.bc-connection-cards',
                title: 'Connection Profiles',
                description: 'Browse through professional profiles and learn about potential connections before reaching out.',
                position: 'top'
            },
            {
                element: '.bc-connect-button',
                title: 'Send Connection Requests',
                description: 'Click the connect button to send a personalized connection request to professionals you want to network with.',
                position: 'left'
            }
        ];

        this.startTour();
    }

    startTour() {
        if (this.steps.length === 0) return;

        this.isActive = true;
        this.currentStep = 0;
        this.overlay.style.display = 'block';
        this.tooltip.style.display = 'block';
        
        // Add tour active class to body
        document.body.classList.add('bc-tour-active');
        
        this.showStep(0);
    }

    showStep(stepIndex) {
        if (stepIndex < 0 || stepIndex >= this.steps.length) return;

        const step = this.steps[stepIndex];
        const element = document.querySelector(step.element);

        if (!element) {
            console.warn(`Tour step element not found: ${step.element}`);
            this.nextStep();
            return;
        }

        // Update tooltip content
        this.tooltip.querySelector('.bc-tour-title').textContent = step.title;
        this.tooltip.querySelector('.bc-tour-description').textContent = step.description;
        this.tooltip.querySelector('.bc-tour-step-counter').textContent = 
            `Step ${stepIndex + 1} of ${this.steps.length}`;

        // Update navigation buttons
        const prevBtn = document.getElementById('tourPrevBtn');
        const nextBtn = document.getElementById('tourNextBtn');
        
        prevBtn.style.display = stepIndex > 0 ? 'inline-block' : 'none';
        
        if (stepIndex === this.steps.length - 1) {
            nextBtn.innerHTML = 'Finish <i class="fas fa-check ms-1"></i>';
            nextBtn.onclick = () => this.endTour();
        } else {
            nextBtn.innerHTML = 'Next <i class="fas fa-arrow-right ms-1"></i>';
            nextBtn.onclick = () => this.nextStep();
        }

        // Highlight element
        this.highlightElement(element);
        
        // Position tooltip
        this.positionTooltip(element, step.position || 'bottom');

        // Scroll element into view
        element.scrollIntoView({ 
            behavior: 'smooth', 
            block: 'center',
            inline: 'center'
        });

        this.currentStep = stepIndex;
    }

    highlightElement(element) {
        // Remove previous highlights
        document.querySelectorAll('.bc-tour-highlight').forEach(el => {
            el.classList.remove('bc-tour-highlight');
        });

        // Add highlight to current element
        element.classList.add('bc-tour-highlight');

        // Update spotlight
        const rect = element.getBoundingClientRect();
        const spotlight = this.overlay.querySelector('.bc-tour-spotlight');
        
        spotlight.style.left = (rect.left - 10) + 'px';
        spotlight.style.top = (rect.top - 10) + 'px';
        spotlight.style.width = (rect.width + 20) + 'px';
        spotlight.style.height = (rect.height + 20) + 'px';
    }

    positionTooltip(element, position) {
        const rect = element.getBoundingClientRect();
        const tooltip = this.tooltip;
        const arrow = tooltip.querySelector('.bc-tour-arrow');
        
        // Reset classes
        tooltip.className = 'bc-tour-tooltip';
        arrow.className = 'bc-tour-arrow';

        let left, top;

        switch (position) {
            case 'top':
                left = rect.left + (rect.width / 2) - (tooltip.offsetWidth / 2);
                top = rect.top - tooltip.offsetHeight - 15;
                tooltip.classList.add('bc-tour-tooltip-top');
                arrow.classList.add('bc-tour-arrow-bottom');
                break;
            case 'bottom':
                left = rect.left + (rect.width / 2) - (tooltip.offsetWidth / 2);
                top = rect.bottom + 15;
                tooltip.classList.add('bc-tour-tooltip-bottom');
                arrow.classList.add('bc-tour-arrow-top');
                break;
            case 'left':
                left = rect.left - tooltip.offsetWidth - 15;
                top = rect.top + (rect.height / 2) - (tooltip.offsetHeight / 2);
                tooltip.classList.add('bc-tour-tooltip-left');
                arrow.classList.add('bc-tour-arrow-right');
                break;
            case 'right':
                left = rect.right + 15;
                top = rect.top + (rect.height / 2) - (tooltip.offsetHeight / 2);
                tooltip.classList.add('bc-tour-tooltip-right');
                arrow.classList.add('bc-tour-arrow-left');
                break;
            default:
                left = rect.left + (rect.width / 2) - (tooltip.offsetWidth / 2);
                top = rect.bottom + 15;
                tooltip.classList.add('bc-tour-tooltip-bottom');
                arrow.classList.add('bc-tour-arrow-top');
        }

        // Ensure tooltip stays within viewport
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;

        if (left < 10) left = 10;
        if (left + tooltip.offsetWidth > viewportWidth - 10) {
            left = viewportWidth - tooltip.offsetWidth - 10;
        }
        if (top < 10) top = 10;
        if (top + tooltip.offsetHeight > viewportHeight - 10) {
            top = viewportHeight - tooltip.offsetHeight - 10;
        }

        tooltip.style.left = left + 'px';
        tooltip.style.top = top + 'px';
    }

    nextStep() {
        if (this.currentStep < this.steps.length - 1) {
            this.showStep(this.currentStep + 1);
        } else {
            this.endTour();
        }
    }

    previousStep() {
        if (this.currentStep > 0) {
            this.showStep(this.currentStep - 1);
        }
    }

    skipTour() {
        if (confirm('Are you sure you want to skip the tour? You can always start it again from the help menu.')) {
            this.endTour();
        }
    }

    endTour() {
        this.isActive = false;
        this.overlay.style.display = 'none';
        this.tooltip.style.display = 'none';
        
        // Remove tour classes
        document.body.classList.remove('bc-tour-active');
        document.querySelectorAll('.bc-tour-highlight').forEach(el => {
            el.classList.remove('bc-tour-highlight');
        });

        // Show completion message
        if (window.BizConnectLoading) {
            BizConnectLoading.showToast('Tour completed! You\'re ready to explore BizConnect.', {
                type: 'success',
                title: 'Tour Complete',
                duration: 4000
            });
        }
    }

    // Public API
    static startDashboardTour() {
        if (window.guidedTour) {
            window.guidedTour.startDashboardTour();
        }
    }

    static startNetworkingTour() {
        if (window.guidedTour) {
            window.guidedTour.startNetworkingTour();
        }
    }
}

// Initialize guided tour system
document.addEventListener('DOMContentLoaded', function() {
    window.guidedTour = new BizConnectGuidedTour();
});

// Export for module usage
if (typeof module !== 'undefined' && module.exports) {
    module.exports = BizConnectGuidedTour;
}
