using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BizConnect.Tests.Unit.Services;

public class KbankOddServiceTests : IDisposable
{
    private readonly BizConnectContext _context;
    private readonly Mock<IKBankOddClient> _mockKbankClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<KbankOddService>> _mockLogger;
    private readonly KbankOddService _service;

    public KbankOddServiceTests()
    {
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new BizConnectContext(options);

        _mockKbankClient = new Mock<IKBankOddClient>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<KbankOddService>>();

        // Setup default configuration values
        _mockConfiguration.Setup(c => c["KBankODD:PassPhrase"]).Returns("testpassphrase");
        _mockConfiguration.Setup(c => c["KBankODD:ExternalSystem"]).Returns("BIZCONNECT");
        _mockConfiguration.Setup(c => c["KBankODD:ServiceName"]).Returns("Test Service");
        _mockConfiguration.Setup(c => c["KBankODD:PGBaseUrl"]).Returns("https://test.kasikornbank.com");

        _service = new KbankOddService(_context, _mockKbankClient.Object, _mockConfiguration.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StartRegistrationRedirectUrlAsync_WithValidConfiguration_ReturnsRedirectUrl()
    {
        // Arrange
        var mockResponse = new KBankInitResponse
        {
            RegId = "TEST123456",
            ReturnStatus = "0",
            ReturnCode = "0000",
            ReturnMessage = "Success"
        };

        _mockKbankClient.Setup(c => c.InitAsync(It.IsAny<KBankInitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _service.StartRegistrationRedirectUrlAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("PGSRegistration.do", result);
        Assert.Contains("reg_id=TEST123456", result);
        Assert.Contains("langLocale=th_TH", result);

        // Verify database record was created
        var registration = await _context.KbankOddRegistrations.FirstOrDefaultAsync();
        Assert.NotNull(registration);
        Assert.Equal("TEST123456", registration.RegId);
        Assert.Equal("Pending", registration.Status);
        Assert.True(registration.ExternalReference.StartsWith("BIZ"));
    }

    [Fact]
    public async Task StartRegistrationRedirectUrlAsync_WithFailedKBankResponse_ThrowsException()
    {
        // Arrange
        var mockResponse = new KBankInitResponse
        {
            RegId = null,
            ReturnStatus = "1",
            ReturnCode = "9999",
            ReturnMessage = "System Error"
        };

        _mockKbankClient.Setup(c => c.InitAsync(It.IsAny<KBankInitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartRegistrationRedirectUrlAsync());
        
        Assert.Contains("KBank initialization failed", exception.Message);
    }

    [Theory]
    [InlineData("KBankODD:PassPhrase")]
    [InlineData("KBankODD:ExternalSystem")]
    [InlineData("KBankODD:ServiceName")]
    [InlineData("KBankODD:PGBaseUrl")]
    public async Task StartRegistrationRedirectUrlAsync_WithMissingConfiguration_ThrowsException(string configKey)
    {
        // Arrange
        _mockConfiguration.Setup(c => c[configKey]).Returns((string)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartRegistrationRedirectUrlAsync());
    }

    [Fact]
    public async Task ProcessStatusUpdateAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var externalReference = "BIZ20250713123456789";
        var registration = new KbankOddRegistration
        {
            ExternalReference = externalReference,
            RegId = "TEST123456",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.KbankOddRegistrations.Add(registration);
        await _context.SaveChangesAsync();

        var dto = new StatusUpdateDto
        {
            ExternalReference = externalReference,
            EspaId = "ESPA123456",
            Timestamp = "20250713123456",
            ReturnStatus = "0",
            ReturnCode = "0000",
            ReturnMessage = "Success",
            AuthParameter = "VALID_HASH" // This would be calculated in real scenario
        };

        // Mock the auth calculation to return the expected hash
        var expectedAuth = BizConnect.Services.Utils.OddUtils.BuildAuth("testpassphrase", 
            dto.ExternalReference, dto.Timestamp, dto.ReturnStatus, dto.ReturnCode);
        dto.AuthParameter = expectedAuth;

        // Act
        var result = await _service.ProcessStatusUpdateAsync(dto);

        // Assert
        Assert.Equal(StatusProcessResult.Success, result);

        // Verify database was updated
        var updatedRegistration = await _context.KbankOddRegistrations
            .FirstOrDefaultAsync(r => r.ExternalReference == externalReference);
        Assert.NotNull(updatedRegistration);
        Assert.Equal("Success", updatedRegistration.Status);
        Assert.Equal("ESPA123456", updatedRegistration.EspaId);
        Assert.Equal("0000", updatedRegistration.ReturnCode);
        Assert.NotNull(updatedRegistration.UpdatedAt);
    }

    [Fact]
    public async Task ProcessStatusUpdateAsync_WithFailedStatus_ReturnsFail()
    {
        // Arrange
        var externalReference = "BIZ20250713123456789";
        var registration = new KbankOddRegistration
        {
            ExternalReference = externalReference,
            RegId = "TEST123456",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        _context.KbankOddRegistrations.Add(registration);
        await _context.SaveChangesAsync();

        var dto = new StatusUpdateDto
        {
            ExternalReference = externalReference,
            Timestamp = "20250713123456",
            ReturnStatus = "1",
            ReturnCode = "9999",
            ReturnMessage = "Failed",
            AuthParameter = "VALID_HASH"
        };

        var expectedAuth = BizConnect.Services.Utils.OddUtils.BuildAuth("testpassphrase", 
            dto.ExternalReference, dto.Timestamp, dto.ReturnStatus, dto.ReturnCode);
        dto.AuthParameter = expectedAuth;

        // Act
        var result = await _service.ProcessStatusUpdateAsync(dto);

        // Assert
        Assert.Equal(StatusProcessResult.Fail, result);

        var updatedRegistration = await _context.KbankOddRegistrations
            .FirstOrDefaultAsync(r => r.ExternalReference == externalReference);
        Assert.Equal("Fail", updatedRegistration.Status);
    }

    [Fact]
    public async Task ProcessStatusUpdateAsync_WithInvalidAuth_ReturnsUnauthorized()
    {
        // Arrange
        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            Timestamp = "20250713123456",
            ReturnStatus = "0",
            ReturnCode = "0000",
            ReturnMessage = "Success",
            AuthParameter = "INVALID_HASH"
        };

        // Act
        var result = await _service.ProcessStatusUpdateAsync(dto);

        // Assert
        Assert.Equal(StatusProcessResult.Unauthorized, result);
    }

    [Fact]
    public async Task ProcessStatusUpdateAsync_WithNonExistentRecord_ReturnsNotFound()
    {
        // Arrange
        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            Timestamp = "20250713123456",
            ReturnStatus = "0",
            ReturnCode = "0000",
            ReturnMessage = "Success",
            AuthParameter = "VALID_HASH"
        };

        var expectedAuth = BizConnect.Services.Utils.OddUtils.BuildAuth("testpassphrase", 
            dto.ExternalReference, dto.Timestamp, dto.ReturnStatus, dto.ReturnCode);
        dto.AuthParameter = expectedAuth;

        // Act
        var result = await _service.ProcessStatusUpdateAsync(dto);

        // Assert
        Assert.Equal(StatusProcessResult.NotFound, result);
    }

    [Fact]
    public async Task ProcessStatusUpdateAsync_WithMissingPassPhrase_ReturnsUnauthorized()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["KBankODD:PassPhrase"]).Returns((string)null);

        var dto = new StatusUpdateDto
        {
            ExternalReference = "BIZ20250713123456789",
            AuthParameter = "SOME_HASH"
        };

        // Act
        var result = await _service.ProcessStatusUpdateAsync(dto);

        // Assert
        Assert.Equal(StatusProcessResult.Unauthorized, result);
    }

    [Fact]
    public async Task StartRegistrationAsync_WithValidRequest_ReturnsRedirectUrl()
    {
        // Arrange
        var request = new OddRegistrationRequest
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        var mockResponse = new KBankInitResponse
        {
            RegId = "TEST123456",
            ReturnStatus = "0",
            ReturnCode = "0000",
            ReturnMessage = "Success"
        };

        _mockKbankClient.Setup(c => c.InitAsync(It.IsAny<KBankInitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _service.StartRegistrationAsync(request);

        // Assert
        Assert.True(result.StartsWith("https://test.kasikornbank.com/PGSRegistration.do?reg_id=TEST123456"));

        // Verify database record was created with contact information
        var registration = await _context.KbankOddRegistrations.FirstOrDefaultAsync();
        Assert.NotNull(registration);
        Assert.Equal("TEST123456", registration.RegId);
        Assert.Equal("Pending", registration.Status);
        Assert.Equal("test@example.com", registration.Email);
        Assert.Equal("0812345678", registration.MobileNo);
        Assert.Equal("National ID", registration.IdType);
        Assert.Equal("1234567890123", registration.IdValue);
    }

    [Fact]
    public async Task StartRegistrationAsync_WithValidRequest_CallsKBankWithContactInfo()
    {
        // Arrange
        var request = new OddRegistrationRequest
        {
            Email = "user@test.com",
            MobileNo = "+66812345678",
            IdType = "Passport",
            IdValue = "AB1234567"
        };

        var mockResponse = new KBankInitResponse
        {
            RegId = "TEST789",
            ReturnStatus = "0",
            ReturnCode = "0000",
            ReturnMessage = "Success"
        };

        _mockKbankClient.Setup(c => c.InitAsync(It.IsAny<KBankInitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        await _service.StartRegistrationAsync(request);

        // Assert
        _mockKbankClient.Verify(c => c.InitAsync(It.Is<KBankInitRequest>(req =>
            req.UserEmail == "user@test.com" &&
            req.UserMobileNo == "+66812345678" &&
            req.Id == "AB1234567"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartRegistrationAsync_WithKBankFailure_ThrowsException()
    {
        // Arrange
        var request = new OddRegistrationRequest
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        var mockResponse = new KBankInitResponse
        {
            RegId = null,
            ReturnStatus = "1",
            ReturnCode = "9999",
            ReturnMessage = "System Error"
        };

        _mockKbankClient.Setup(c => c.InitAsync(It.IsAny<KBankInitRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartRegistrationAsync(request));

        Assert.Contains("KBank initialization failed", exception.Message);
        Assert.Contains("System Error", exception.Message);
    }

    [Fact]
    public async Task StartRegistrationAsync_WithMissingPassPhrase_ThrowsException()
    {
        // Arrange
        var request = new OddRegistrationRequest
        {
            Email = "test@example.com",
            MobileNo = "0812345678",
            IdType = "National ID",
            IdValue = "1234567890123"
        };

        _mockConfiguration.Setup(c => c["KBankODD:PassPhrase"]).Returns((string?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.StartRegistrationAsync(request));

        Assert.Contains("KBankODD:PassPhrase not configured", exception.Message);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
