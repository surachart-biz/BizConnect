using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using BizConnect.Services.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services;

/// <summary>
/// Service implementation for KBank Online Direct Debit operations
/// </summary>
public class KbankOddService : IKbankOddService
{
    private readonly BizConnectContext _context;
    private readonly IKBankOddClient _kbankClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KbankOddService> _logger;

    public KbankOddService(
        BizConnectContext context,
        IKBankOddClient kbankClient,
        IConfiguration configuration,
        ILogger<KbankOddService> logger)
    {
        _context = context;
        _kbankClient = kbankClient;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> StartRegistrationRedirectUrlAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting KBank ODD registration process");

            // Generate external reference
            var externalReference = OddUtils.GenerateExternalReference();
            _logger.LogDebug("Generated external reference: {ExternalReference}", externalReference);

            // Get configuration values
            var passPhrase = _configuration["KBankODD:PassPhrase"] 
                ?? throw new InvalidOperationException("KBankODD:PassPhrase not configured");
            var externalSystem = _configuration["KBankODD:ExternalSystem"] 
                ?? throw new InvalidOperationException("KBankODD:ExternalSystem not configured");
            var serviceName = _configuration["KBankODD:ServiceName"] 
                ?? throw new InvalidOperationException("KBankODD:ServiceName not configured");
            var pgBaseUrl = _configuration["KBankODD:PGBaseUrl"] 
                ?? throw new InvalidOperationException("KBankODD:PGBaseUrl not configured");

            // Build authentication hash
            var authParameter = OddUtils.BuildAuth(passPhrase, externalSystem, externalReference);

            // Create initialization request
            var initRequest = new KBankInitRequest
            {
                TransactionType = "0600",
                Encoding = "UTF8",
                ExternalSystem = externalSystem,
                ExternalReference = externalReference,
                ServiceName = serviceName,
                AuthParameter = authParameter
            };

            // Call KBank API
            var initResponse = await _kbankClient.InitAsync(initRequest, cancellationToken);

            // Check if initialization was successful
            if (initResponse.ReturnStatus != "0" || string.IsNullOrEmpty(initResponse.RegId))
            {
                _logger.LogError("KBank initialization failed: Status={Status}, Code={Code}, Message={Message}",
                    initResponse.ReturnStatus, initResponse.ReturnCode, initResponse.ReturnMessage);
                throw new InvalidOperationException($"KBank initialization failed: {initResponse.ReturnMessage}");
            }

            // Save registration record with Pending status
            var registration = new KbankOddRegistration
            {
                ExternalReference = externalReference,
                RegId = initResponse.RegId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.KbankOddRegistrations.Add(registration);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("KBank ODD registration record created: ExternalReference={ExternalReference}, RegId={RegId}",
                externalReference, initResponse.RegId);

            // Build redirect URL
            var redirectUrl = $"{pgBaseUrl.TrimEnd('/')}/PGSRegistration.do?reg_id={initResponse.RegId}&langLocale=th_TH";
            
            _logger.LogInformation("KBank ODD registration redirect URL generated: {RedirectUrl}", redirectUrl);
            
            return redirectUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start KBank ODD registration process");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> StartRegistrationAsync(OddRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting KBank ODD registration process with user contact information");

            // Generate external reference
            var externalReference = OddUtils.GenerateExternalReference();
            _logger.LogDebug("Generated external reference: {ExternalReference}", externalReference);

            // Get configuration values
            var passPhrase = _configuration["KBankODD:PassPhrase"];
            if (string.IsNullOrEmpty(passPhrase))
            {
                throw new InvalidOperationException("KBankODD:PassPhrase not configured");
            }

            var externalSystem = _configuration["KBankODD:ExternalSystem"] ?? "BIZCONNECT";
            var serviceName = _configuration["KBankODD:ServiceName"] ?? "BizConnect ODD Service";
            var pgBaseUrl = _configuration["KBankODD:PGBaseUrl"] ?? throw new InvalidOperationException("KBankODD:PGBaseUrl not configured");
            var appBaseUrl = _configuration["KBankODD:AppBaseUrl"] ?? "https://localhost:7178"; // Application base URL

            // Build authentication hash
            var authParameter = OddUtils.BuildAuth(passPhrase, externalSystem, externalReference);

            // Create initialization request with contact information
            var initRequest = new KBankInitRequest
            {
                TransactionType = "0600",
                Encoding = "UTF8",
                ExternalSystem = externalSystem,
                ExternalReference = externalReference,
                ServiceName = serviceName,
                UserEmail = request.Email,
                UserMobileNo = request.MobileNo,
                Id = request.IdValue,
                CallbackUrl = $"{appBaseUrl.TrimEnd('/')}/kbank/odd/callback?ref={externalReference}",
                AuthParameter = authParameter
            };

            // Call KBank API
            var initResponse = await _kbankClient.InitAsync(initRequest, cancellationToken);

            // Check if initialization was successful
            if (initResponse.ReturnStatus != "0" || string.IsNullOrEmpty(initResponse.RegId))
            {
                _logger.LogError("KBank initialization failed: Status={Status}, Code={Code}, Message={Message}",
                    initResponse.ReturnStatus, initResponse.ReturnCode, initResponse.ReturnMessage);
                throw new InvalidOperationException($"KBank initialization failed: {initResponse.ReturnMessage}");
            }

            // Save registration record with Pending status and contact information
            var registration = new KbankOddRegistration
            {
                ExternalReference = externalReference,
                RegId = initResponse.RegId,
                Status = "Pending",
                Email = request.Email,
                MobileNo = request.MobileNo,
                IdType = request.IdType,
                IdValue = request.IdValue,
                CreatedAt = DateTime.UtcNow
            };

            _context.KbankOddRegistrations.Add(registration);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("KBank ODD registration record created with contact info: ExternalReference={ExternalReference}, RegId={RegId}, Email={Email}",
                externalReference, initResponse.RegId, request.Email);

            // Build redirect URL
            var redirectUrl = $"{pgBaseUrl.TrimEnd('/')}/PGSRegistration.do?reg_id={initResponse.RegId}&langLocale=th_TH";

            _logger.LogInformation("KBank ODD registration redirect URL generated: {RedirectUrl}", redirectUrl);

            return redirectUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start KBank ODD registration process with contact information");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<StatusProcessResult> ProcessStatusUpdateAsync(StatusUpdateDto dto, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing KBank ODD status update for external reference: {ExternalReference}", 
                dto.ExternalReference);

            // Get pass phrase from configuration
            var passPhrase = _configuration["KBankODD:PassPhrase"];
            if (string.IsNullOrEmpty(passPhrase))
            {
                _logger.LogError("KBankODD:PassPhrase not configured");
                return StatusProcessResult.Unauthorized;
            }

            // Validate authentication hash
            var expectedAuth = OddUtils.BuildAuth(passPhrase, dto.ExternalReference, dto.Timestamp, 
                dto.ReturnStatus, dto.ReturnCode);
            
            if (dto.AuthParameter != expectedAuth)
            {
                _logger.LogWarning("Invalid authentication hash for external reference: {ExternalReference}. Expected: {Expected}, Received: {Received}",
                    dto.ExternalReference, expectedAuth, dto.AuthParameter);
                return StatusProcessResult.Unauthorized;
            }

            // Find existing registration record
            var registration = await _context.KbankOddRegistrations
                .FirstOrDefaultAsync(r => r.ExternalReference == dto.ExternalReference, cancellationToken);

            if (registration == null)
            {
                _logger.LogWarning("Registration record not found for external reference: {ExternalReference}", 
                    dto.ExternalReference);
                return StatusProcessResult.NotFound;
            }

            // Update registration record
            registration.EspaId = dto.EspaId;
            registration.Status = dto.ReturnStatus == "0" ? "Success" : "Fail";
            registration.ReturnCode = dto.ReturnCode;
            registration.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("KBank ODD registration status updated: ExternalReference={ExternalReference}, Status={Status}, EspaId={EspaId}",
                dto.ExternalReference, registration.Status, dto.EspaId);

            return dto.ReturnStatus == "0" ? StatusProcessResult.Success : StatusProcessResult.Fail;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process KBank ODD status update for external reference: {ExternalReference}", 
                dto.ExternalReference);
            throw;
        }
    }
}
