using BizConnect.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Jobs;

/// <summary>
/// Background job to automatically purge expired OTAC codes
/// Runs every 5 minutes to clean up expired codes
/// </summary>
public class PurgeExpiredOtacCodesJob
{
    private readonly IOddRegistrationService _oddRegistrationService;
    private readonly ILogger<PurgeExpiredOtacCodesJob> _logger;

    public PurgeExpiredOtacCodesJob(
        IOddRegistrationService oddRegistrationService, 
        ILogger<PurgeExpiredOtacCodesJob> logger)
    {
        _oddRegistrationService = oddRegistrationService;
        _logger = logger;
    }

    /// <summary>
    /// Executes the OTAC purge job
    /// </summary>
    public async Task ExecuteAsync()
    {
        try
        {
            _logger.LogInformation("Starting expired OTAC purge job");

            // Purge expired OTAC registrations (now consolidated in one table)
            var purgedCount = await _oddRegistrationService.PurgeExpiredOtacCodesAsync();

            if (purgedCount > 0)
            {
                _logger.LogInformation("Purge job completed. Purged {PurgedCount} expired OTAC registrations", purgedCount);
            }
            else
            {
                _logger.LogDebug("Purge job completed. No expired OTAC registrations found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during OTAC purge job execution");
            throw; // Re-throw to mark job as failed in Hangfire
        }
    }
}