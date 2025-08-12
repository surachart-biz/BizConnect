using BizConnect.Services;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using BizConnect.Services.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BizConnect.Tests.Unit.Services;

/// <summary>
/// Unit tests for Phase 1 pure API methods in KbankOddService
/// </summary>
public class KbankOddServicePhase1Tests
{
    private readonly Mock<IKBankOddClient> _mockKbankClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<IRealtimeNotificationService> _mockRealtimeNotificationService;
    private readonly Mock<ILogger<KbankOddService>> _mockLogger;
    private readonly KbankOddService _service;

    public KbankOddServicePhase1Tests()
    {
        _mockKbankClient = new Mock<IKBankOddClient>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockRealtimeNotificationService = new Mock<IRealtimeNotificationService>();
        _mockLogger = new Mock<ILogger<KbankOddService>>();

        // Setup basic configuration
        _mockConfiguration.Setup(c => c["KBankODD:PassPhrase"]).Returns("test-passphrase");
        _mockConfiguration.Setup(c => c["KBankODD:ExternalSystem"]).Returns("BIZCONNECT");
        _mockConfiguration.Setup(c => c["KBankODD:ServiceName"]).Returns("BizConnect ODD Service");
        _mockConfiguration.Setup(c => c["KBankODD:PGBaseUrl"]).Returns("https://test.kbank.com");
        _mockConfiguration.Setup(c => c["KBankODD:AppBaseUrl"]).Returns("https://localhost:7178");

        // Create service without database context for testing pure API methods
        _service = new KbankOddService(
            null!, // No database context needed for pure API methods
            _mockKbankClient.Object,
            _mockConfiguration.Object,
            _mockDateTimeProvider.Object,
            _mockRealtimeNotificationService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task InitializeRegistrationAsync_Success_ReturnsSuccessResult()
    {
        // Arrange
        var request = new OddRegistrationRequest
        {
            FullName = "Test User",
            MobileNo = "0812345678",
            IdType = "national_id",
            IdValue = "1234567890123",
            AccountNo = "1234567890",
            BranchId = 1
        };

        var expectedResponse = new KBankInitResponse
        {
            RegId = "test-reg-id-123",
            ReturnStatus = "0",
            ReturnCode = "00",
            ReturnMessage = "Success"
        };

        _mockKbankClient
            .Setup(c => c.InitAsync(It.IsAny<KBankInitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.InitializeRegistrationAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("test-reg-id-123", result.RegId);
        Assert.NotEmpty(result.ExternalReference);
        Assert.NotEmpty(result.RedirectUrl);
        Assert.Contains("PGSRegistration.do", result.RedirectUrl);
        Assert.Contains("reg_id=test-reg-id-123", result.RedirectUrl);
    }

    [Fact]
    public async Task InitializeRegistrationAsync_KBankFailure_ReturnsFailureResult()
    {
        // Arrange
        var request = new OddRegistrationRequest
        {
            FullName = "Test User",
            MobileNo = "0812345678",
            IdType = "national_id",
            IdValue = "1234567890123",
            AccountNo = "1234567890",
            BranchId = 1
        };

        var expectedResponse = new KBankInitResponse
        {
            RegId = "",
            ReturnStatus = "1",
            ReturnCode = "E001",
            ReturnMessage = "Invalid request"
        };

        _mockKbankClient
            .Setup(c => c.InitAsync(It.IsAny<KBankInitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _service.InitializeRegistrationAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.RegId);
        Assert.NotEmpty(result.ExternalReference);
        Assert.Null(result.RedirectUrl);
        Assert.Contains("KBank initialization failed", result.ErrorMessage);
    }

    [Fact]
    public async Task InitializeRegistrationAsync_MissingPassPhrase_ReturnsFailureResult()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["KBankODD:PassPhrase"]).Returns((string?)null);
        
        var request = new OddRegistrationRequest
        {
            FullName = "Test User",
            MobileNo = "0812345678",
            IdType = "national_id",
            IdValue = "1234567890123",
            AccountNo = "1234567890",
            BranchId = 1
        };

        // Act
        var result = await _service.InitializeRegistrationAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("PassPhrase not configured", result.ErrorMessage);
        Assert.NotEmpty(result.ExternalReference);
    }

    [Fact]
    public async Task ValidateStatusUpdateAsync_ValidAuthentication_ReturnsSuccess()
    {
        // Arrange
        var passPhrase = "test-passphrase";
        var externalReference = "TEST123456";
        var timestamp = "20250812120000";
        var returnStatus = "0";
        var returnCode = "00";
        
        var expectedAuth = OddUtils.BuildAuth(passPhrase, externalReference, timestamp, returnStatus, returnCode);
        
        var statusUpdate = new StatusUpdateDto
        {
            ExternalReference = externalReference,
            Timestamp = timestamp,
            ReturnStatus = returnStatus,
            ReturnCode = returnCode,
            ReturnMessage = "Success",
            AuthParameter = expectedAuth
        };

        // Act
        var result = await _service.ValidateStatusUpdateAsync(statusUpdate);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(StatusValidationType.Valid, result.ResultType);
        Assert.Equal(externalReference, result.ExternalReference);
        Assert.NotNull(result.StatusUpdate);
        Assert.Equal(statusUpdate, result.StatusUpdate);
    }

    [Fact]
    public async Task ValidateStatusUpdateAsync_InvalidAuthentication_ReturnsFailure()
    {
        // Arrange
        var statusUpdate = new StatusUpdateDto
        {
            ExternalReference = "TEST123456",
            Timestamp = "20250812120000",
            ReturnStatus = "0",
            ReturnCode = "00",
            ReturnMessage = "Success",
            AuthParameter = "invalid-hash"
        };

        // Act
        var result = await _service.ValidateStatusUpdateAsync(statusUpdate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(StatusValidationType.InvalidAuthentication, result.ResultType);
        Assert.Equal("TEST123456", result.ExternalReference);
        Assert.Contains("Invalid authentication hash", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateStatusUpdateAsync_MissingPassPhrase_ReturnsFailure()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["KBankODD:PassPhrase"]).Returns((string?)null);
        
        var statusUpdate = new StatusUpdateDto
        {
            ExternalReference = "TEST123456",
            Timestamp = "20250812120000",
            ReturnStatus = "0",
            ReturnCode = "00",
            ReturnMessage = "Success",
            AuthParameter = "some-hash"
        };

        // Act
        var result = await _service.ValidateStatusUpdateAsync(statusUpdate);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(StatusValidationType.MissingPassPhrase, result.ResultType);
        Assert.Contains("PassPhrase not configured", result.ErrorMessage);
    }
}