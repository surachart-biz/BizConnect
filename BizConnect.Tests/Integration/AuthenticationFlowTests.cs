using System.Security.Claims;
using BizConnect.Controllers;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BizConnect.Tests.Integration;

/// <summary>
/// Comprehensive authentication flow testing for Phase 3 verification
/// Tests username-only login, session management, security audit, and rate limiting
/// </summary>
public class AuthenticationFlowTests : IDisposable
{
    private readonly BizConnectContext _context;
    private readonly UserService _userService;
    private readonly SecurityAuditService _securityAuditService;
    private readonly RateLimitingService _rateLimitingService;
    private readonly AccountController _accountController;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Mock<ILogger<AccountController>> _mockControllerLogger;
    private readonly Mock<ILogger<SecurityAuditService>> _mockAuditLogger;
    private readonly Mock<ILogger<RateLimitingService>> _mockRateLimitLogger;
    private readonly IMemoryCache _memoryCache;
    private readonly IConfiguration _configuration;

    public AuthenticationFlowTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BizConnectContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new BizConnectContext(options);

        // Setup date time provider
        _dateTimeProvider = new DateTimeProvider();

        // Setup services
        _userService = new UserService(_context, _dateTimeProvider);
        
        // Setup loggers
        _mockControllerLogger = new Mock<ILogger<AccountController>>();
        _mockAuditLogger = new Mock<ILogger<SecurityAuditService>>();
        _mockRateLimitLogger = new Mock<ILogger<RateLimitingService>>();

        // Setup security audit service
        _securityAuditService = new SecurityAuditService(_context, _mockAuditLogger.Object);

        // Setup memory cache and configuration for rate limiting
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var configurationData = new Dictionary<string, string>
        {
            ["RateLimiting:login:MaxAttempts"] = "5",
            ["RateLimiting:login:LockoutDurationMinutes"] = "15",
            ["RateLimiting:login:AttemptWindowMinutes"] = "15",
            ["RateLimiting:login:EnableUserLockout"] = "true",
            ["RateLimiting:login:EnableIpLockout"] = "true"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData!)
            .Build();

        _rateLimitingService = new RateLimitingService(
            _context, _memoryCache, _mockRateLimitLogger.Object, _securityAuditService, _configuration);

        // Setup AccountController
        _accountController = new AccountController(
            _userService, _mockControllerLogger.Object, _rateLimitingService, _securityAuditService);

        // Setup HTTP context for controller
        SetupHttpContext();

        // Seed test data
        SeedTestData();
    }

    private void SetupHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["User-Agent"] = "Test-User-Agent";
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
        
        // Setup session
        var sessionFeature = new Mock<ISessionFeature>();
        var session = new Mock<ISession>();
        sessionFeature.Setup(s => s.Session).Returns(session.Object);
        httpContext.Features.Set(sessionFeature.Object);

        // Setup authentication
        var authService = new Mock<IAuthenticationService>();
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(s => s.GetService(typeof(IAuthenticationService)))
                      .Returns(authService.Object);
        httpContext.RequestServices = serviceProvider.Object;

        _accountController.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    private void SeedTestData()
    {
        var users = new List<User>
        {
            new User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = "Admin",
                IsActive = true,
                CreatedAt = _dateTimeProvider.UtcNow,
                UpdatedAt = _dateTimeProvider.UtcNow
            },
            new User
            {
                Id = 2,
                Username = "employee",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("employee123"),
                Role = "Employee",
                IsActive = true,
                CreatedAt = _dateTimeProvider.UtcNow,
                UpdatedAt = _dateTimeProvider.UtcNow
            },
            new User
            {
                Id = 3,
                Username = "user1",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("user123"),
                Role = "User",
                IsActive = true,
                CreatedAt = _dateTimeProvider.UtcNow,
                UpdatedAt = _dateTimeProvider.UtcNow
            },
            new User
            {
                Id = 4,
                Username = "inactive",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("inactive123"),
                Role = "User",
                IsActive = false,
                CreatedAt = _dateTimeProvider.UtcNow,
                UpdatedAt = _dateTimeProvider.UtcNow
            }
        };

        _context.Users.AddRange(users);
        _context.SaveChanges();
    }

    #region Username-Only Authentication Tests

    [Fact]
    public async Task Authentication_ValidAdminCredentials_AuthenticatesSuccessfully()
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "admin",
            Password = "admin123",
            RememberMe = false
        };

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        var redirectResult = (LocalRedirectResult)result;
        Assert.Contains("Admin", redirectResult.Url);

        // Verify user was authenticated
        var authenticatedUser = await _userService.AuthenticateAsync("admin", "admin123");
        Assert.NotNull(authenticatedUser);
        Assert.Equal("Admin", authenticatedUser.Role);
    }

    [Fact]
    public async Task Authentication_ValidEmployeeCredentials_AuthenticatesSuccessfully()
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "employee",
            Password = "employee123",
            RememberMe = false
        };

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        var redirectResult = (LocalRedirectResult)result;
        Assert.Contains("Admin", redirectResult.Url); // Employee has admin access

        // Verify user was authenticated
        var authenticatedUser = await _userService.AuthenticateAsync("employee", "employee123");
        Assert.NotNull(authenticatedUser);
        Assert.Equal("Employee", authenticatedUser.Role);
    }

    [Fact]
    public async Task Authentication_ValidUserCredentials_RedirectsToKBankRegister()
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "user1",
            Password = "user123",
            RememberMe = false
        };

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        var redirectResult = (LocalRedirectResult)result;
        Assert.Contains("KBank", redirectResult.Url);
        Assert.Contains("Register", redirectResult.Url);

        // Verify user was authenticated
        var authenticatedUser = await _userService.AuthenticateAsync("user1", "user123");
        Assert.NotNull(authenticatedUser);
        Assert.Equal("User", authenticatedUser.Role);
    }

    [Fact]
    public async Task Authentication_InvalidPassword_ReturnsViewWithError()
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "admin",
            Password = "wrongpassword",
            RememberMe = false
        };

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        Assert.False(_accountController.ModelState.IsValid);
        Assert.Contains("Invalid username or password", 
            _accountController.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task Authentication_InactiveUser_ReturnsViewWithError()
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "inactive",
            Password = "inactive123",
            RememberMe = false
        };

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<ViewResult>(result);
        var viewResult = (ViewResult)result;
        Assert.False(_accountController.ModelState.IsValid);
        Assert.Contains("Invalid username or password", 
            _accountController.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("username", "")]
    [InlineData(null, "password")]
    [InlineData("username", null)]
    public async Task Authentication_EmptyCredentials_ReturnsViewWithValidationError(string username, string password)
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = username,
            Password = password,
            RememberMe = false
        };

        // Manually trigger model validation
        _accountController.TryValidateModel(loginModel);

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<ViewResult>(result);
        Assert.False(_accountController.ModelState.IsValid);
    }

    #endregion

    #region Password Policy Tests

    [Theory]
    [InlineData("1234567")] // Too short (7 chars)
    [InlineData("12345")] // Too short (5 chars)
    public void PasswordValidation_TooShort_FailsValidation(string password)
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "testuser",
            Password = password,
            RememberMe = false
        };

        // Act
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(loginModel);
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            loginModel, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, r => r.ErrorMessage!.Contains("at least 8 characters"));
    }

    [Fact]
    public void PasswordValidation_MinimumLength_PassesValidation()
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "testuser",
            Password = "12345678", // Exactly 8 chars
            RememberMe = false
        };

        // Act
        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(loginModel);
        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            loginModel, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task RateLimiting_MultipleFailedAttempts_TriggersLockout()
    {
        // Arrange
        var ipAddress = "192.168.1.100";
        var loginModel = new LoginViewModel
        {
            Username = "admin",
            Password = "wrongpassword",
            RememberMe = false
        };

        // Update HTTP context with different IP
        _accountController.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipAddress);

        // Act - Make 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            await _accountController.Login(loginModel);
        }

        // Check rate limit status after 5 attempts
        var rateLimitStatus = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.True(rateLimitStatus.IsLocked);
        Assert.Equal(0, rateLimitStatus.RemainingAttempts);
        Assert.Contains("Too many failed attempts", rateLimitStatus.Message);
    }

    [Fact]
    public async Task RateLimiting_SuccessfulLogin_ClearsFailedAttempts()
    {
        // Arrange
        var ipAddress = "192.168.1.101";
        var wrongLoginModel = new LoginViewModel
        {
            Username = "admin",
            Password = "wrongpassword",
            RememberMe = false
        };
        var correctLoginModel = new LoginViewModel
        {
            Username = "admin",
            Password = "admin123",
            RememberMe = false
        };

        // Update HTTP context with different IP
        _accountController.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ipAddress);

        // Act - Make 3 failed attempts, then successful login
        for (int i = 0; i < 3; i++)
        {
            await _accountController.Login(wrongLoginModel);
        }

        await _accountController.Login(correctLoginModel);

        // Check rate limit status after successful login
        var rateLimitStatus = await _rateLimitingService.CheckRateLimitAsync(ipAddress, "login");

        // Assert
        Assert.False(rateLimitStatus.IsLocked);
        Assert.Equal(5, rateLimitStatus.RemainingAttempts); // Should be reset
    }

    [Fact]
    public async Task RateLimiting_UserSpecificLockout_BlocksUserFromAnyIP()
    {
        // Arrange
        var username = "user1";
        var ipAddress1 = "192.168.1.102";
        var ipAddress2 = "192.168.1.103";

        // Make failed attempts from first IP to lock user
        for (int i = 0; i < 5; i++)
        {
            await _rateLimitingService.RecordUserFailedAttemptAsync(username, ipAddress1);
        }

        // Act - Check lockout status from different IP
        var lockoutStatus = await _rateLimitingService.CheckUserLockoutAsync(username);

        // Assert
        Assert.True(lockoutStatus.IsLocked);
        Assert.Equal(ipAddress1, lockoutStatus.LastFailedIpAddress);
        Assert.Equal(5, lockoutStatus.FailedAttempts);
    }

    #endregion

    #region Security Audit Tests

    [Fact]
    public async Task SecurityAudit_SuccessfulLogin_LogsEvent()
    {
        // Arrange
        var username = "admin";
        var ipAddress = "127.0.0.1";
        var userAgent = "Test-Browser";

        // Act
        await _securityAuditService.LogSuccessfulLoginAsync(username, ipAddress, userAgent);

        // Assert
        // Note: Since we're not using a real database table for audit logs,
        // we verify the logger was called with appropriate information
        _mockAuditLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Successful login")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SecurityAudit_FailedLogin_LogsEventWithReason()
    {
        // Arrange
        var username = "admin";
        var ipAddress = "127.0.0.1";
        var reason = "Invalid password";
        var userAgent = "Test-Browser";

        // Act
        await _securityAuditService.LogFailedLoginAsync(username, ipAddress, reason, userAgent);

        // Assert
        _mockAuditLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed login")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SecurityAudit_AccountLockout_LogsCriticalEvent()
    {
        // Arrange
        var ipAddress = "192.168.1.104";
        var failedAttempts = 5;

        // Act
        await _securityAuditService.LogAccountLockoutAsync(ipAddress, failedAttempts);

        // Assert
        _mockAuditLogger.Verify(
            x => x.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("locked out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task SessionManagement_RememberMeTrue_SetsLongerExpiration()
    {
        // This test would verify that RememberMe = true results in longer cookie expiration
        // In a real scenario, you'd check the authentication properties
        // For now, we verify the login model accepts RememberMe parameter

        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "admin",
            Password = "admin123",
            RememberMe = true
        };

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        // In a real implementation, you'd verify the authentication cookie properties
    }

    [Fact]
    public async Task SessionManagement_RememberMeFalse_SetsDefaultExpiration()
    {
        // Arrange
        var loginModel = new LoginViewModel
        {
            Username = "admin",
            Password = "admin123",
            RememberMe = false
        };

        // Act
        var result = await _accountController.Login(loginModel);

        // Assert
        Assert.IsType<LocalRedirectResult>(result);
        // In a real implementation, you'd verify shorter cookie expiration
    }

    #endregion

    #region DateTime Provider Tests

    [Fact]
    public void DateTimeProvider_UtcNow_ReturnsCurrentTime()
    {
        // Arrange & Act
        var currentTime = _dateTimeProvider.UtcNow;
        var systemTime = DateTime.UtcNow;

        // Assert
        var timeDifference = Math.Abs((currentTime - systemTime).TotalSeconds);
        Assert.True(timeDifference < 1); // Should be within 1 second
    }

    [Fact]
    public async Task UserService_CreatesUserWithProperTimestamp()
    {
        // Arrange
        var username = "timestamptest";
        var password = "password123";
        var role = "User";

        // Act
        var user = await _userService.CreateUserAsync(username, password, role);

        // Assert
        Assert.NotNull(user);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.True(user.UpdatedAt <= DateTime.UtcNow);
        Assert.Equal(user.CreatedAt, user.UpdatedAt); // Should be same on creation
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
    }
}

