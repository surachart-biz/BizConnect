using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Results;

namespace BizConnect.Tests.Unit.Services
{
    /// <summary>
    /// Comprehensive unit tests for OtacManagementService focusing on UTC DateTime handling and OTAC lifecycle
    /// </summary>
    public class OtacManagementServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly Mock<IOtacCodeGenerator> _mockOtacCodeGenerator;
        private readonly Mock<ILogger<OtacManagementService>> _mockLogger;
        private readonly Mock<IRepository<KbankOddRegistration>> _mockRepository;
        private readonly Mock<IRepository<User>> _mockUserRepository;
        private readonly OtacManagementService _service;
        private readonly DateTime _fixedUtcTime = new DateTime(2025, 8, 5, 14, 30, 0, DateTimeKind.Utc);

        public OtacManagementServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockOtacCodeGenerator = new Mock<IOtacCodeGenerator>();
            _mockLogger = new Mock<ILogger<OtacManagementService>>();
            _mockRepository = new Mock<IRepository<KbankOddRegistration>>();
            _mockUserRepository = new Mock<IRepository<User>>();

            // Setup DateTime provider to return fixed UTC time
            _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(_fixedUtcTime);

            // Setup repositories
            _mockUnitOfWork.Setup(x => x.KbankOddRegistrations).Returns(_mockRepository.Object);
            _mockUnitOfWork.Setup(x => x.Users).Returns(_mockUserRepository.Object);

            _service = new OtacManagementService(
                _mockUnitOfWork.Object,
                _mockDateTimeProvider.Object,
                _mockOtacCodeGenerator.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task GenerateAsync_WithValidUser_ShouldCreateOtacWithCorrectExpiryTime()
        {
            // Arrange
            var userId = 1;
            var purpose = "Registration";
            var generatedCode = "ABC12345";
            var expectedExpiryTime = _fixedUtcTime.AddMinutes(30);

            var user = new User { Id = userId, Username = "testuser" };
            _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockOtacCodeGenerator.Setup(x => x.GenerateCode()).Returns(generatedCode);

            // Act
            var result = await _service.GenerateAsync(userId, purpose);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(generatedCode, result.Data.Code);
            Assert.Equal(expectedExpiryTime, result.Data.ExpiresAt);
            Assert.Equal(purpose, result.Data.Purpose);
            Assert.Equal(5, result.Data.RemainingAttempts);

            // Verify the registration was created with UTC timestamps
            _mockRepository.Verify(x => x.AddAsync(It.Is<KbankOddRegistration>(r =>
                r.OtacCode == generatedCode &&
                r.OtacExpiresAt == expectedExpiryTime &&
                r.CreatedAt == _fixedUtcTime &&
                r.OtacState == "Generated" &&
                r.AttemptCount == 0 &&
                r.IsLocked == false &&
                r.GeneratedByUserId == userId
            )), Times.Once);

            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task GenerateAsync_WithNonExistentUser_ShouldReturnFailure()
        {
            // Arrange
            var userId = 999;
            _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User)null);

            // Act
            var result = await _service.GenerateAsync(userId);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("User not found", result.ErrorMessage);
            
            // Verify no registration was attempted
            _mockRepository.Verify(x => x.AddAsync(It.IsAny<KbankOddRegistration>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ValidateAsync_WithValidCode_ShouldReturnSuccessAndIncrementAttempts()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var clientIp = "192.168.1.1";
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacExpiresAt = _fixedUtcTime.AddMinutes(10), // Still valid
                OtacState = "Generated",
                AttemptCount = 0,
                IsLocked = false
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.ValidateAsync(otacCode, clientIp);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(normalizedCode, result.Data.Code);
            Assert.Equal(4, result.Data.RemainingAttempts); // 5 - 1 = 4

            // Verify attempt tracking was updated
            Assert.Equal(1, registration.AttemptCount);
            Assert.Equal(_fixedUtcTime, registration.LastAttemptAt);
            Assert.Equal(clientIp, registration.LastAttemptIp);
            Assert.Equal("Validated", registration.OtacState);

            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ValidateAsync_WithExpiredCode_ShouldReturnExpiredFailure()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var clientIp = "192.168.1.1";
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacExpiresAt = _fixedUtcTime.AddMinutes(-10), // Expired 10 minutes ago
                OtacState = "Generated",
                AttemptCount = 0,
                IsLocked = false
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.ValidateAsync(otacCode, clientIp);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("OTAC code has expired", result.ErrorMessage);

            // Verify attempt was still tracked
            Assert.Equal(1, registration.AttemptCount);
            Assert.Equal(_fixedUtcTime, registration.LastAttemptAt);
            Assert.Equal(clientIp, registration.LastAttemptIp);
        }

        [Fact]
        public async Task ValidateAsync_ExceedingMaxAttempts_ShouldLockCode()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var clientIp = "192.168.1.1";
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacExpiresAt = _fixedUtcTime.AddMinutes(10), // Still valid
                OtacState = "Generated",
                AttemptCount = 4, // One more attempt will reach the limit
                IsLocked = false
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.ValidateAsync(otacCode, clientIp);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("OTAC code is locked due to too many failed attempts", result.ErrorMessage);

            // Verify code was locked
            Assert.Equal(5, registration.AttemptCount);
            Assert.True(registration.IsLocked);
            
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ValidateAsync_WithLockedCode_ShouldReturnLockedFailure()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var clientIp = "192.168.1.1";
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacExpiresAt = _fixedUtcTime.AddMinutes(10),
                OtacState = "Generated",
                AttemptCount = 5,
                IsLocked = true // Already locked
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.ValidateAsync(otacCode, clientIp);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("OTAC code is locked due to too many failed attempts", result.ErrorMessage);

            // Verify attempt count was not incremented for locked code
            Assert.Equal(5, registration.AttemptCount);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task ValidateAsync_WithInvalidInput_ShouldReturnFailure(string invalidCode)
        {
            // Act
            var result = await _service.ValidateAsync(invalidCode, "192.168.1.1");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("OTAC code is required", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateAsync_WithInvalidFormat_ShouldReturnFailure()
        {
            // Arrange
            var invalidCode = "123";
            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(invalidCode)).Returns(false);

            // Act
            var result = await _service.ValidateAsync(invalidCode, "192.168.1.1");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Invalid OTAC code format", result.ErrorMessage);
        }

        [Fact]
        public async Task ValidateAsync_WithNonExistentCode_ShouldReturnNotFound()
        {
            // Arrange
            var otacCode = "NOTFOUND";
            var normalizedCode = otacCode.ToUpper();

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration>().AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.ValidateAsync(otacCode, "192.168.1.1");

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("OTAC code not found", result.ErrorMessage);
        }

        [Fact]
        public async Task PurgeExpiredAsync_WithExpiredCodes_ShouldUpdateStatesToExpired()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    OtacCode = "EXPIRED1",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    OtacState = "Generated",
                    Status = string.Empty
                },
                new()
                {
                    Id = 2,
                    OtacCode = "EXPIRED2",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-5),
                    OtacState = "Generated",
                    Status = string.Empty
                }
            };

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.PurgeExpiredAsync();

            // Assert
            Assert.True(result.IsSuccess);

            // Verify all expired registrations were updated
            foreach (var registration in expiredRegistrations)
            {
                Assert.Equal("Expired", registration.OtacState);
                Assert.Equal(_fixedUtcTime, registration.UpdatedAt);
            }

            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task PurgeExpiredAsync_WithNoExpiredCodes_ShouldReturnSuccessWithoutChanges()
        {
            // Arrange
            var activeRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    OtacCode = "ACTIVE1",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(10), // Still valid
                    OtacState = "Generated",
                    Status = string.Empty
                }
            };

            var mockQueryable = new List<KbankOddRegistration>().AsQueryable(); // No expired codes
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.PurgeExpiredAsync();

            // Assert
            Assert.True(result.IsSuccess);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task GetOtacStatisticsAsync_ShouldCalculateCorrectStatistics()
        {
            // Arrange
            var period = TimeSpan.FromHours(24);
            var periodStart = _fixedUtcTime.Subtract(period);
            
            var registrations = new List<KbankOddRegistration>
            {
                new() { Id = 1, CreatedAt = periodStart.AddHours(1), OtacState = "Generated", IsLocked = false, AttemptCount = 1 },
                new() { Id = 2, CreatedAt = periodStart.AddHours(2), OtacState = "Validated", IsLocked = false, AttemptCount = 2 },
                new() { Id = 3, CreatedAt = periodStart.AddHours(3), OtacState = "Expired", IsLocked = false, AttemptCount = 0 },
                new() { Id = 4, CreatedAt = periodStart.AddHours(4), OtacState = "Invalidated", IsLocked = true, AttemptCount = 5 },
                new() { Id = 5, CreatedAt = periodStart.AddHours(5), OtacState = "Used", IsLocked = false, AttemptCount = 1 }
            };

            var mockQueryable = registrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var result = await _service.GetOtacStatisticsAsync(period);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            
            var stats = result.Data;
            Assert.Equal(5, stats.TotalGenerated);
            Assert.Equal(2, stats.TotalValidated); // Validated + Used
            Assert.Equal(1, stats.TotalExpired);
            Assert.Equal(1, stats.TotalLocked);
            Assert.Equal(1, stats.TotalInvalidated);
            Assert.Equal(1.8m, stats.AverageAttempts); // (1+2+0+5+1)/5 = 1.8
            Assert.Equal(period, stats.Period);
            Assert.Equal(periodStart, stats.PeriodStart);
            Assert.Equal(_fixedUtcTime, stats.PeriodEnd);
        }

        [Fact]
        public async Task InvalidateOtacAsync_WithValidCode_ShouldInvalidateAndLock()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacState = "Generated",
                IsLocked = false
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.InvalidateOtacAsync(otacCode);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal("Invalidated", registration.OtacState);
            Assert.True(registration.IsLocked);
            Assert.Equal(_fixedUtcTime, registration.UpdatedAt);
            
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task InvalidateOtacAsync_WithAlreadyInvalidatedCode_ShouldReturnFailure()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacState = "Invalidated", // Already invalidated
                IsLocked = true
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.QueryWithTracking()).Returns(mockQueryable);

            // Act
            var result = await _service.InvalidateOtacAsync(otacCode);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("OTAC code is already invalidated", result.ErrorMessage);
        }

        [Fact]
        public async Task IsValidAsync_WithValidNonExpiredCode_ShouldReturnValid()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacExpiresAt = _fixedUtcTime.AddMinutes(10), // Still valid
                OtacState = "Generated",
                IsLocked = false
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var result = await _service.IsValidAsync(otacCode);

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task IsValidAsync_WithExpiredCode_ShouldReturnInvalid()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacExpiresAt = _fixedUtcTime.AddMinutes(-10), // Expired
                OtacState = "Generated",
                IsLocked = false
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var result = await _service.IsValidAsync(otacCode);

            // Assert
            Assert.False(result.IsValid);
            Assert.Equal("OTAC code has expired", result.ErrorMessage);
        }

        [Fact]
        public async Task GetInfoAsync_WithValidCode_ShouldReturnOtacInfo()
        {
            // Arrange
            var otacCode = "ABC12345";
            var normalizedCode = otacCode.ToUpper();
            var registration = new KbankOddRegistration
            {
                Id = 1,
                OtacCode = normalizedCode,
                OtacExpiresAt = _fixedUtcTime.AddMinutes(15),
                OtacState = "Generated",
                AttemptCount = 2
            };

            _mockOtacCodeGenerator.Setup(x => x.IsValidFormat(otacCode)).Returns(true);
            _mockOtacCodeGenerator.Setup(x => x.NormalizeCode(otacCode)).Returns(normalizedCode);
            
            var mockQueryable = new List<KbankOddRegistration> { registration }.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var result = await _service.GetInfoAsync(otacCode);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(normalizedCode, result.Data.Code);
            Assert.Equal(registration.OtacExpiresAt, result.Data.ExpiresAt);
            Assert.Equal(registration.Id, result.Data.RegistrationId);
            Assert.Equal(3, result.Data.RemainingAttempts); // 5 - 2 = 3
        }
    }
}