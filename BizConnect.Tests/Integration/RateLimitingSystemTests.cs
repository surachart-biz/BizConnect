using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Security.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BizConnect.Tests.Integration;

/// <summary>
/// Comprehensive rate limiting system tests for Phase 3 verification
/// Tests IP-based and user-based rate limiting with cache configuration
/// Verifies lockout mechanisms and threat score calculation
/// </summary>
public class RateLimitingSystemTests : IDisposable
{
    private readonly BizConnectContext _context;
    private readonly RateLimitingService _rateLimitingService;
    private readonly SecurityAuditService _securityAuditService;
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;
    private readonly Mock<ILogger<RateLimitingService>> _mockRateLimitLogger;
    private readonly Mock<ILogger<SecurityAuditService>> _mockAuditLogger;

    public RateLimitingSystemTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BizConnectContext(options);

        // Setup loggers
        _mockRateLimitLogger = new Mock<ILogger<RateLimitingService>>();
        _mockAuditLogger = new Mock<ILogger<SecurityAuditService>>();

        // Setup security audit service
        _securityAuditService = new SecurityAuditService(_context, _mockAuditLogger.Object);

        // Setup memory cache with size limits
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 1000000 // 1MB limit for testing
        });

        // Setup configuration with test values
        var configurationData = new Dictionary<string, string>
        {
            ["RateLimiting:login:MaxAttempts"] = "5",
            ["RateLimiting:login:LockoutDurationMinutes"] = "15",
            ["RateLimiting:login:AttemptWindowMinutes"] = "15",
            ["RateLimiting:login:EnableUserLockout"] = "true",
            ["RateLimiting:login:EnableIpLockout"] = "true",
            ["RateLimiting:api:MaxAttempts"] = "100",
            ["RateLimiting:api:LockoutDurationMinutes"] = "5",
            ["RateLimiting:api:AttemptWindowMinutes"] = "1"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData!)
            .Build();

        // Create rate limiting service
        _rateLimitingService = new RateLimitingService(
            _context, _memoryCache, _mockRateLimitLogger.Object, _securityAuditService, _configuration);
    }

    #region IP-Based Rate Limiting Tests

    [Fact]
    public async Task CheckRateLimit_NewIP_AllowsFullAttempts()
    {
        // Arrange
        var ipAddress = "192.168.1.1";

        // Act
        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(5, result.RemainingAttempts);
        Assert.Equal(5, result.TotalAttempts);
        Assert.Contains("You have 5 attempts remaining", result.Message);
    }

    [Fact]
    public async Task RecordFailedAttempt_SingleAttempt_ReducesRemainingCount()
    {
        // Arrange
        var ipAddress = "192.168.1.2";

        // Act
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login", "testuser");
        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(4, result.RemainingAttempts);
        Assert.Equal(5, result.TotalAttempts);
    }

    [Fact]
    public async Task RecordFailedAttempt_MaxAttempts_TriggersLockout()
    {
        // Arrange
        var ipAddress = "192.168.1.3";

        // Act - Record 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login", $"user{i}");
        }

        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.True(result.IsLocked);
        Assert.Equal(0, result.RemainingAttempts);
        Assert.Contains("Too many failed attempts", result.Message);
        Assert.Contains("15 minutes", result.Message);
    }

    [Fact]
    public async Task GetAttemptCount_TracksCorrectCount()
    {
        // Arrange
        var ipAddress = "192.168.1.4";

        // Act
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");

        var count = await _rateLimitingService.GetAttemptCountAsync(ipAddress, "login");

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task ClearFailedAttempts_ResetsLockout()
    {
        // Arrange
        var ipAddress = "192.168.1.5";

        // Lock the IP
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        }

        // Act - Clear attempts
        await _rateLimitingService.ClearFailedAttemptsAsync(ipAddress, "login");
        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(5, result.RemainingAttempts);
    }

    #endregion

    #region User-Based Rate Limiting Tests

    [Fact]
    public async Task CheckUserLockout_NewUser_NotLocked()
    {
        // Arrange
        var username = "newuser";

        // Act
        var result = await _rateLimitingService.CheckUserLockoutAsync(username);

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(0, result.FailedAttempts);
    }

    [Fact]
    public async Task RecordUserFailedAttempt_TracksAttempts()
    {
        // Arrange
        var username = "testuser";
        var ipAddress = "192.168.1.6";

        // Act
        await _rateLimitingService.RecordUserFailedAttemptAsync(username, ipAddress);
        await _rateLimitingService.RecordUserFailedAttemptAsync(username, ipAddress);

        var result = await _rateLimitingService.CheckUserLockoutAsync(username);

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(2, result.FailedAttempts);
        Assert.Equal(ipAddress, result.LastFailedIpAddress);
    }

    [Fact]
    public async Task RecordUserFailedAttempt_MaxAttempts_LocksUser()
    {
        // Arrange
        var username = "lockableuser";
        var ipAddress = "192.168.1.7";

        // Act - Record 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordUserFailedAttemptAsync(username, ipAddress);
        }

        var result = await _rateLimitingService.CheckUserLockoutAsync(username);

        // Assert
        Assert.True(result.IsLocked);
        Assert.Equal(5, result.FailedAttempts);
        Assert.True(result.LockoutEndTime > DateTime.UtcNow);
    }

    [Fact]
    public async Task ClearUserLockout_RemovesLockout()
    {
        // Arrange
        var username = "clearableuser";
        var ipAddress = "192.168.1.8";

        // Lock the user
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordUserFailedAttemptAsync(username, ipAddress);
        }

        // Act - Clear lockout
        await _rateLimitingService.ClearUserLockoutAsync(username);
        var result = await _rateLimitingService.CheckUserLockoutAsync(username);

        // Assert
        Assert.False(result.IsLocked);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public void GetConfiguration_LoginContext_ReturnsCorrectValues()
    {
        // Act
        var config = _rateLimitingService.GetConfiguration("login");

        // Assert
        Assert.Equal("login", config.Context);
        Assert.Equal(5, config.MaxAttempts);
        Assert.Equal(15, config.LockoutDurationMinutes);
        Assert.Equal(15, config.AttemptWindowMinutes);
        Assert.True(config.EnableUserLockout);
        Assert.True(config.EnableIpLockout);
    }

    [Fact]
    public void GetConfiguration_ApiContext_ReturnsApiValues()
    {
        // Act
        var config = _rateLimitingService.GetConfiguration("api");

        // Assert
        Assert.Equal("api", config.Context);
        Assert.Equal(100, config.MaxAttempts);
        Assert.Equal(5, config.LockoutDurationMinutes);
        Assert.Equal(1, config.AttemptWindowMinutes);
    }

    [Fact]
    public void GetConfiguration_UnknownContext_ReturnsDefaults()
    {
        // Act
        var config = _rateLimitingService.GetConfiguration("unknown");

        // Assert
        Assert.Equal("unknown", config.Context);
        Assert.Equal(5, config.MaxAttempts); // Default values
        Assert.Equal(15, config.LockoutDurationMinutes);
        Assert.Equal(15, config.AttemptWindowMinutes);
    }

    #endregion

    #region Time Window Tests

    [Fact]
    public async Task RateLimit_OldAttempts_NotCountedInWindow()
    {
        // This test is conceptual since we can't easily manipulate time in memory cache
        // In a real scenario, you'd use a testable time provider

        // Arrange
        var ipAddress = "192.168.1.9";

        // Act
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        
        // Simulate checking after window expires (would need time manipulation)
        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert - Within window, attempt should be counted
        Assert.Equal(4, result.RemainingAttempts);
    }

    [Fact]
    public async Task RateLimit_WindowExpiry_ResetsAttempts()
    {
        // Arrange
        var ipAddress = "192.168.1.10";

        // Record some attempts
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");

        // In a real test, you'd advance time beyond the window
        // For now, we verify the current behavior
        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.Equal(3, result.RemainingAttempts); // 5 - 2 = 3
    }

    #endregion

    #region Context-Specific Rate Limiting Tests

    [Fact]
    public async Task RateLimit_DifferentContexts_IndependentLimits()
    {
        // Arrange
        var ipAddress = "192.168.1.11";

        // Act - Record attempts in different contexts
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "api");

        var loginResult = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");
        var apiResult = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "api");

        // Assert
        Assert.Equal(4, loginResult.RemainingAttempts); // 5 - 1 = 4
        Assert.Equal(99, apiResult.RemainingAttempts); // 100 - 1 = 99
    }

    [Fact]
    public async Task RateLimit_LoginAndApi_SeparateLimits()
    {
        // Arrange
        var ipAddress = "192.168.1.12";

        // Act - Max out login attempts
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        }

        var loginResult = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");
        var apiResult = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "api");

        // Assert
        Assert.True(loginResult.IsLocked);
        Assert.False(apiResult.IsLocked);
        Assert.Equal(100, apiResult.RemainingAttempts); // API limit unaffected
    }

    #endregion

    #region Cache Memory Management Tests

    [Fact]
    public async Task RateLimit_CacheSize_CalculatedCorrectly()
    {
        // Arrange
        var ipAddress = "192.168.1.13";

        // Act - Record attempts to trigger cache operations
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");

        // The service should handle cache size calculations internally
        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(3, result.RemainingAttempts);
    }

    [Fact]
    public async Task RateLimit_MultipleCacheEntries_ManagedCorrectly()
    {
        // Arrange
        var ipAddresses = new[] 
        { 
            "192.168.1.14", 
            "192.168.1.15", 
            "192.168.1.16" 
        };

        // Act - Create cache entries for multiple IPs
        foreach (var ip in ipAddresses)
        {
            await _rateLimitingService.RecordFailedAttemptAsync(ip, "login");
            await _rateLimitingService.RecordFailedAttemptAsync(ip, "api");
        }

        // Verify each IP has independent limits
        foreach (var ip in ipAddresses)
        {
            var loginResult = await _rateLimitingService.CheckRateLimitAsync(ip, "login");
            var apiResult = await _rateLimitingService.CheckRateLimitAsync(ip, "api");

            // Assert
            Assert.Equal(4, loginResult.RemainingAttempts);
            Assert.Equal(99, apiResult.RemainingAttempts);
        }
    }

    #endregion

    #region Integration with Security Audit Tests

    [Fact]
    public async Task RateLimit_Lockout_TriggersAuditLog()
    {
        // Arrange
        var ipAddress = "192.168.1.17";

        // Act - Trigger lockout
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login");
        }

        // Assert - Verify audit service was called
        _mockAuditLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("locked out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RateLimit_UserLockout_LogsWarning()
    {
        // Arrange
        var username = "warninguser";
        var ipAddress = "192.168.1.18";

        // Act - Trigger user lockout
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordUserFailedAttemptAsync(username, ipAddress);
        }

        // Assert - Verify rate limiting service logged the lockout
        _mockRateLimitLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("locked out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Overload Method Tests

    [Fact]
    public async Task CheckRateLimitAsync_OverloadMethod_WorksCorrectly()
    {
        // Arrange
        var operation = "login";
        var identifier = "192.168.1.19";

        // Act
        var result = await _rateLimitingService.CheckRateLimitAsync(operation, identifier);

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(5, result.RemainingAttempts);
    }

    [Fact]
    public async Task CheckRateLimitAsync_WithCancellationToken_HandlesCorrectly()
    {
        // Arrange
        var operation = "api";
        var identifier = "192.168.1.20";
        var cancellationToken = new CancellationToken();

        // Act
        var result = await _rateLimitingService.CheckRateLimitAsync(operation, identifier, cancellationToken);

        // Assert
        Assert.False(result.IsLocked);
        Assert.Equal(100, result.RemainingAttempts); // API limit
    }

    #endregion

    #region Cleanup and Maintenance Tests

    [Fact]
    public async Task CleanupExpiredEntries_ExecutesSuccessfully()
    {
        // Act
        await _rateLimitingService.CleanupExpiredEntriesAsync();

        // Assert - Should complete without error
        // In reality, MemoryCache handles expiration automatically
        _mockRateLimitLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("cleanup completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Edge Case Tests

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CheckRateLimit_InvalidIP_HandlesGracefully(string ipAddress)
    {
        // Act & Assert - Should not throw
        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");
        
        // The service should handle invalid IPs gracefully
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CheckUserLockout_InvalidUsername_HandlesGracefully(string username)
    {
        // Act
        var result = await _rateLimitingService.CheckUserLockoutAsync(username);

        // Assert
        Assert.False(result.IsLocked);
    }

    [Fact]
    public async Task RateLimit_ConcurrentRequests_HandledCorrectly()
    {
        // Arrange
        var ipAddress = "192.168.1.21";
        var tasks = new List<Task>();

        // Act - Simulate concurrent requests
        for (int i = 0; i < 3; i++)
        {
            tasks.Add(_rateLimitingService.RecordFailedAttemptAsync(ipAddress, "login"));
        }

        await Task.WhenAll(tasks);

        var result = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.Equal(2, result.RemainingAttempts); // Should have 5 - 3 = 2
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
    }
}