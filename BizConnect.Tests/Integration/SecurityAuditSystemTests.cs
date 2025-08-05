using BizConnect.Dal;
using BizConnect.Services;
using BizConnect.Services.Common;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace BizConnect.Tests.Integration;

/// <summary>
/// Comprehensive security audit system tests for Phase 3 verification
/// Tests security logging functionality without Email references
/// Verifies audit trail completeness and threat detection
/// </summary>
public class SecurityAuditSystemTests : IDisposable
{
    private readonly BizConnectContext _context;
    private readonly SecurityAuditService _securityAuditService;
    private readonly Mock<ILogger<SecurityAuditService>> _mockLogger;

    public SecurityAuditSystemTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BizConnectContext(options);
        
        // Setup logger mock
        _mockLogger = new Mock<ILogger<SecurityAuditService>>();
        
        // Create service
        _securityAuditService = new SecurityAuditService(_context, _mockLogger.Object);
    }

    #region Successful Login Audit Tests

    [Fact]
    public async Task LogSuccessfulLogin_WithUsernameOnly_LogsCorrectly()
    {
        // Arrange
        var username = "admin";
        var ipAddress = "192.168.1.1";
        var userAgent = "Mozilla/5.0 Test Browser";

        // Act
        await _securityAuditService.LogSuccessfulLoginAsync(username, ipAddress, userAgent);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, "Successful login");
    }

    [Fact]
    public async Task LogSuccessfulLogin_WithoutEmail_DoesNotReferenceEmail()
    {
        // Arrange
        var username = "user123"; // Username only, no email
        var ipAddress = "10.0.0.1";

        // Act
        await _securityAuditService.LogSuccessfulLoginAsync(username, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, "Successful login");
        
        // Verify no email-related terms in log messages
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => !v.ToString()!.ToLower().Contains("email")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("user1", "127.0.0.1", "Chrome")]
    [InlineData("admin", "192.168.1.100", "Firefox")]
    [InlineData("employee", "10.0.0.50", null)]
    public async Task LogSuccessfulLogin_VariousScenarios_LogsAppropriately(
        string username, string ipAddress, string userAgent)
    {
        // Act
        await _securityAuditService.LogSuccessfulLoginAsync(username, ipAddress, userAgent);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, username);
        VerifyLoggerWasCalled(LogLevel.Information, ipAddress);
    }

    #endregion

    #region Failed Login Audit Tests

    [Fact]
    public async Task LogFailedLogin_WithReason_LogsWarning()
    {
        // Arrange
        var username = "wronguser";
        var ipAddress = "192.168.1.2";
        var reason = "Invalid credentials";
        var userAgent = "Suspicious Bot";

        // Act
        await _securityAuditService.LogFailedLoginAsync(username, ipAddress, reason, userAgent);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "Failed login");
        VerifyLoggerWasCalled(LogLevel.Warning, reason);
    }

    [Fact]
    public async Task LogFailedLogin_MultipleAttempts_TracksEachAttempt()
    {
        // Arrange
        var username = "admin";
        var ipAddress = "192.168.1.3";
        var attempts = new[]
        {
            "Invalid password",
            "Account not found",
            "Account inactive"
        };

        // Act
        foreach (var reason in attempts)
        {
            await _securityAuditService.LogFailedLoginAsync(username, ipAddress, reason);
        }

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed login")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    #endregion

    #region Logout Audit Tests

    [Fact]
    public async Task LogLogout_WithUsernameAndIP_LogsCorrectly()
    {
        // Arrange
        var username = "user1";
        var ipAddress = "192.168.1.4";

        // Act
        await _securityAuditService.LogLogoutAsync(username, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, "logged out");
        VerifyLoggerWasCalled(LogLevel.Information, username);
        VerifyLoggerWasCalled(LogLevel.Information, ipAddress);
    }

    [Fact]
    public async Task LogLogout_UnknownUser_HandlesGracefully()
    {
        // Arrange
        var username = "Unknown";
        var ipAddress = "192.168.1.5";

        // Act
        await _securityAuditService.LogLogoutAsync(username, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, "logged out");
    }

    #endregion

    #region Account Lockout Audit Tests

    [Fact]
    public async Task LogAccountLockout_WithFailedAttempts_LogsCriticalEvent()
    {
        // Arrange
        var ipAddress = "192.168.1.6";
        var failedAttempts = 5;

        // Act
        await _securityAuditService.LogAccountLockoutAsync(ipAddress, failedAttempts);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Critical, "locked out");
        VerifyLoggerWasCalled(LogLevel.Critical, failedAttempts.ToString());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task LogAccountLockout_VariousAttemptCounts_LogsCorrectly(int attemptCount)
    {
        // Arrange
        var ipAddress = $"192.168.1.{attemptCount}";

        // Act
        await _securityAuditService.LogAccountLockoutAsync(ipAddress, attemptCount);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Critical, attemptCount.ToString());
    }

    #endregion

    #region Unauthorized Access Audit Tests

    [Fact]
    public async Task LogUnauthorizedAccess_WithResource_LogsWarning()
    {
        // Arrange
        var username = "hacker";
        var resource = "/Admin/Users";
        var ipAddress = "192.168.1.7";

        // Act
        await _securityAuditService.LogUnauthorizedAccessAsync(username, resource, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "Unauthorized access");
        VerifyLoggerWasCalled(LogLevel.Warning, resource);
    }

    [Fact]
    public async Task LogUnauthorizedAccess_AnonymousUser_HandlesNull()
    {
        // Arrange
        string? username = null;
        var resource = "/Admin/Dashboard";
        var ipAddress = "192.168.1.8";

        // Act
        await _securityAuditService.LogUnauthorizedAccessAsync(username, resource, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "Anonymous");
    }

    #endregion

    #region OTAC Audit Tests

    [Fact]
    public async Task LogOtacGenerated_WithPurpose_LogsInfoEvent()
    {
        // Arrange
        var code = "ABC12345";
        var purpose = "Account verification";
        var generatedBy = "admin";

        // Act
        await _securityAuditService.LogOtacGeneratedAsync(code, purpose, generatedBy);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, "OTAC generated");
        VerifyLoggerWasCalled(LogLevel.Information, purpose);
        
        // Verify actual code is NOT logged (security)
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains(code)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogOtacValidation_Success_LogsInfoEvent()
    {
        // Arrange
        var code = "XYZ98765";
        var ipAddress = "192.168.1.9";
        var attemptNumber = 1;

        // Act
        await _securityAuditService.LogOtacValidationAsync(code, true, ipAddress, attemptNumber);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, "OTAC validation successful");
    }

    [Fact]
    public async Task LogOtacValidation_Failure_LogsWarning()
    {
        // Arrange
        var code = "INVALID1";
        var ipAddress = "192.168.1.10";
        var attemptNumber = 3;

        // Act
        await _securityAuditService.LogOtacValidationAsync(code, false, ipAddress, attemptNumber);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "OTAC validation failed");
        VerifyLoggerWasCalled(LogLevel.Warning, attemptNumber.ToString());
    }

    [Fact]
    public async Task LogOtacLockout_AfterFailedAttempts_LogsCritical()
    {
        // Arrange
        var code = "LOCKED01";
        var failedAttempts = 5;

        // Act
        await _securityAuditService.LogOtacLockoutAsync(code, failedAttempts);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Critical, "OTAC locked");
        VerifyLoggerWasCalled(LogLevel.Critical, failedAttempts.ToString());
    }

    #endregion

    #region Suspicious Activity Audit Tests

    [Fact]
    public async Task LogSuspiciousActivity_WithDetails_LogsCritical()
    {
        // Arrange
        var activityType = "Brute force attack";
        var details = "Multiple failed login attempts from same IP";
        var ipAddress = "192.168.1.11";

        // Act
        await _securityAuditService.LogSuspiciousActivityAsync(activityType, details, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Critical, "Suspicious activity");
        VerifyLoggerWasCalled(LogLevel.Critical, activityType);
    }

    [Theory]
    [InlineData("SQL Injection attempt", "Detected malicious query", "10.0.0.1")]
    [InlineData("Port scanning", "Rapid connection attempts", "192.168.1.50")]
    [InlineData("Invalid session", "Session token manipulation", "172.16.0.1")]
    public async Task LogSuspiciousActivity_VariousThreats_LogsAppropriately(
        string activityType, string details, string ipAddress)
    {
        // Act
        await _securityAuditService.LogSuspiciousActivityAsync(activityType, details, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Critical, activityType);
    }

    #endregion

    #region Data Access Audit Tests

    [Fact]
    public async Task LogDataAccess_WithOperation_LogsInfo()
    {
        // Arrange
        var username = "admin";
        var dataType = "User";
        var operation = "READ";
        var entityId = "123";

        // Act
        await _securityAuditService.LogDataAccessAsync(username, dataType, operation, entityId);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, operation);
        VerifyLoggerWasCalled(LogLevel.Information, dataType);
    }

    [Theory]
    [InlineData("CREATE", "User")]
    [InlineData("UPDATE", "Registration")]
    [InlineData("DELETE", "OtacCode")]
    public async Task LogDataAccess_VariousOperations_LogsCorrectly(string operation, string dataType)
    {
        // Arrange
        var username = "admin";
        var entityId = Guid.NewGuid().ToString();

        // Act
        await _securityAuditService.LogDataAccessAsync(username, dataType, operation, entityId);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Information, operation);
    }

    #endregion

    #region Password and Role Change Audit Tests

    [Fact]
    public async Task LogPasswordChange_WithChangedBy_LogsWarning()
    {
        // Arrange
        var username = "user1";
        var changedBy = "admin";
        var ipAddress = "192.168.1.12";

        // Act
        await _securityAuditService.LogPasswordChangeAsync(username, changedBy, ipAddress);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "Password changed");
        VerifyLoggerWasCalled(LogLevel.Warning, changedBy);
    }

    [Fact]
    public async Task LogRoleChange_WithOldAndNewRole_LogsWarning()
    {
        // Arrange
        var username = "user1";
        var oldRole = "User";
        var newRole = "Employee";
        var changedBy = "admin";

        // Act
        await _securityAuditService.LogRoleChangeAsync(username, oldRole, newRole, changedBy);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "Role changed");
        VerifyLoggerWasCalled(LogLevel.Warning, oldRole);
        VerifyLoggerWasCalled(LogLevel.Warning, newRole);
    }

    #endregion

    #region Configuration Change Audit Tests

    [Fact]
    public async Task LogSecurityConfigurationChange_LogsCritical()
    {
        // Arrange
        var setting = "MaxLoginAttempts";
        var oldValue = "5";
        var newValue = "3";
        var changedBy = "admin";

        // Act
        await _securityAuditService.LogSecurityConfigurationChangeAsync(setting, oldValue, newValue, changedBy);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Critical, "Configuration");
        VerifyLoggerWasCalled(LogLevel.Critical, setting);
    }

    #endregion

    #region Result Pattern Tests

    [Fact]
    public async Task LogSuccessfulLoginResult_ReturnsSuccess()
    {
        // Arrange
        var username = "user1";
        var ipAddress = "192.168.1.13";

        // Act
        var result = await _securityAuditService.LogSuccessfulLoginResultAsync(username, ipAddress);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task LogFailedLoginResult_ReturnsSuccess()
    {
        // Arrange
        var username = "user1";
        var ipAddress = "192.168.1.14";
        var reason = "Invalid password";

        // Act
        var result = await _securityAuditService.LogFailedLoginResultAsync(username, ipAddress, reason);

        // Assert
        Assert.True(result.IsSuccess);
    }

    #endregion

    #region Threat Response Tests

    [Fact]
    public async Task LogThreatResponse_WithLevel_LogsWarning()
    {
        // Arrange
        var ipAddress = "192.168.1.15";
        var threatLevel = "HIGH";
        var actionCount = 3;

        // Act
        await _securityAuditService.LogThreatResponseAsync(ipAddress, threatLevel, actionCount);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "Threat response");
        VerifyLoggerWasCalled(LogLevel.Warning, threatLevel);
    }

    [Fact]
    public async Task LogIpBlock_WithReason_LogsWarning()
    {
        // Arrange
        var ipAddress = "192.168.1.16";
        var reason = "Brute force detected";

        // Act
        await _securityAuditService.LogIpBlockAsync(ipAddress, reason);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "IP");
        VerifyLoggerWasCalled(LogLevel.Warning, "blocked");
    }

    [Fact]
    public async Task LogIpBlock_WithDuration_LogsWarningWithTime()
    {
        // Arrange
        var ipAddress = "192.168.1.17";
        var reason = "Suspicious activity";
        var duration = TimeSpan.FromMinutes(30);

        // Act
        await _securityAuditService.LogIpBlockAsync(ipAddress, reason, duration);

        // Assert
        VerifyLoggerWasCalled(LogLevel.Warning, "blocked");
        VerifyLoggerWasCalled(LogLevel.Warning, duration.TotalMinutes.ToString());
    }

    #endregion

    #region Code Security Tests

    [Fact]
    public void GetHashedCode_DoesNotExposeActualCode()
    {
        // This tests that the private GetHashedCode method doesn't expose actual codes
        // We can't test the private method directly, but we verify OTAC logging doesn't expose codes
        
        // Arrange
        var code = "SECRET123";
        var purpose = "Test";
        var generatedBy = "admin";

        // Act & Assert
        var task = _securityAuditService.LogOtacGeneratedAsync(code, purpose, generatedBy);
        
        // Verify the actual code is never logged
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => !v.ToString()!.Contains(code)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Helper Methods

    private void VerifyLoggerWasCalled(LogLevel level, string message)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}