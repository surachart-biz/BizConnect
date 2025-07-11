// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// BizConnect Site JavaScript

// Help Modal Function
function showHelpModal() {
    const modalHtml = `
        <div class="modal fade" id="helpModal" tabindex="-1" aria-labelledby="helpModalLabel" aria-hidden="true">
            <div class="modal-dialog modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title" id="helpModalLabel">
                            <i class="fas fa-rocket me-2"></i>Getting Started with BizConnect
                        </h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <div class="row g-4">
                            <div class="col-md-6">
                                <div class="bc-help-card">
                                    <div class="bc-help-icon">
                                        <i class="fas fa-user-circle"></i>
                                    </div>
                                    <h4>Complete Your Profile</h4>
                                    <p>Add your professional information, skills, and experience to make a great first impression.</p>
                                    <a href="#" class="btn btn-outline-primary btn-sm">Update Profile</a>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="bc-help-card">
                                    <div class="bc-help-icon">
                                        <i class="fas fa-search"></i>
                                    </div>
                                    <h4>Find Connections</h4>
                                    <p>Search for professionals in your industry and start building meaningful relationships.</p>
                                    <a href="#" class="btn btn-outline-primary btn-sm">Start Networking</a>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="bc-help-card">
                                    <div class="bc-help-icon">
                                        <i class="fas fa-play"></i>
                                    </div>
                                    <h4>Take a Tour</h4>
                                    <p>Get familiar with BizConnect's features through our interactive guided tours.</p>
                                    <button class="btn btn-outline-primary btn-sm" onclick="BizConnectGuidedTour.startDashboardTour(); $('#helpModal').modal('hide');">Start Tour</button>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="bc-help-card">
                                    <div class="bc-help-icon">
                                        <i class="fas fa-lightbulb"></i>
                                    </div>
                                    <h4>Pro Tips</h4>
                                    <p>Learn best practices for networking and making the most of your BizConnect experience.</p>
                                    <a href="#" class="btn btn-outline-primary btn-sm">View Tips</a>
                                </div>
                            </div>
                        </div>

                        <div class="mt-4 p-3 bg-light rounded">
                            <h5><i class="fas fa-question-circle me-2"></i>Need More Help?</h5>
                            <p class="mb-2">If you have questions or need assistance, we're here to help!</p>
                            <div class="d-flex gap-2">
                                <a href="#" class="btn btn-sm btn-primary">Contact Support</a>
                                <a href="#" class="btn btn-sm btn-outline-secondary">View Documentation</a>
                            </div>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        <button type="button" class="btn btn-primary" onclick="window.location.href='/Onboarding'">
                            <i class="fas fa-redo me-1"></i>Restart Onboarding
                        </button>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Remove existing modal if present
    const existingModal = document.getElementById('helpModal');
    if (existingModal) {
        existingModal.remove();
    }

    // Add modal to body
    document.body.insertAdjacentHTML('beforeend', modalHtml);

    // Show modal
    const modal = new bootstrap.Modal(document.getElementById('helpModal'));
    modal.show();

    // Clean up when modal is hidden
    document.getElementById('helpModal').addEventListener('hidden.bs.modal', function() {
        this.remove();
    });
}

// Initialize onboarding check for new users
document.addEventListener('DOMContentLoaded', function() {
    // Check if user should see onboarding
    checkOnboardingStatus();
});

function checkOnboardingStatus() {
    // In a real application, this would check the user's onboarding status from the server
    // For demo purposes, we'll check localStorage
    const hasCompletedOnboarding = localStorage.getItem('bizconnect_onboarding_completed');
    const isNewUser = localStorage.getItem('bizconnect_new_user');

    if (isNewUser === 'true' && !hasCompletedOnboarding) {
        // Show onboarding prompt after a short delay
        setTimeout(() => {
            showOnboardingPrompt();
        }, 2000);
    }
}

function showOnboardingPrompt() {
    if (window.BizConnectLoading) {
        BizConnectLoading.showToast('Welcome to BizConnect! Would you like to take a quick tour?', {
            type: 'info',
            title: 'Welcome!',
            duration: 8000,
            actions: [
                {
                    text: 'Start Tour',
                    action: () => {
                        window.location.href = '/Onboarding';
                    }
                },
                {
                    text: 'Maybe Later',
                    action: () => {
                        localStorage.setItem('bizconnect_onboarding_prompted', 'true');
                    }
                }
            ]
        });
    }
}

// Mark user as having completed onboarding
function markOnboardingComplete() {
    localStorage.setItem('bizconnect_onboarding_completed', 'true');
    localStorage.removeItem('bizconnect_new_user');
}
