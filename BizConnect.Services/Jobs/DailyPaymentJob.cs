using BizConnect.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Jobs;

/// <summary>
/// Background job service for processing daily payment operations
/// Runs daily at 2:00 AM to process payment-related tasks
/// </summary>
public class DailyPaymentJob
{
    private readonly IPaymentProcessingService _paymentProcessingService;
    private readonly ILogger<DailyPaymentJob> _logger;

    public DailyPaymentJob(
        IPaymentProcessingService paymentProcessingService, 
        ILogger<DailyPaymentJob> logger)
    {
        _paymentProcessingService = paymentProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Executes daily payment processing tasks
    /// This includes reconciliation, reporting, and cleanup operations
    /// </summary>
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting daily payment job execution");

        try
        {
            var result = await _paymentProcessingService.ExecuteDailyProcessingAsync();

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Daily payment job completed successfully in {Duration}ms. " +
                    "Report: {TotalRegistrations} total, Stale Updated: {StaleUpdated}, " +
                    "Success Rate: {SuccessRate:F2}%",
                    result.Duration.TotalMilliseconds,
                    result.ReconciliationReport.TotalRegistrations,
                    result.StaleRegistrationsUpdated,
                    result.Statistics.SuccessRate);
            }
            else
            {
                _logger.LogError("Daily payment job completed with errors after {Duration}ms: {ErrorMessage}",
                    result.Duration.TotalMilliseconds, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily payment job failed with exception");
            throw; // Re-throw to let Hangfire handle retry logic
        }
    }

}