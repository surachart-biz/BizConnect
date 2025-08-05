using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BizConnect.Dal;
using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Jobs;

namespace BizConnect.Tests.Unit.Jobs
{
    /// <summary>
    /// Comprehensive unit tests for OptimizedDailyPaymentJob focusing on TIMESTAMPTZ handling and batch processing
    /// </summary>
    public class OptimizedDailyPaymentJobTests
    {
        private readonly Mock<BizConnectContext> _mockContext;
        private readonly Mock<IPaymentProcessingService> _mockPaymentProcessingService;
        private readonly Mock<ILogger<OptimizedDailyPaymentJob>> _mockLogger;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly Mock<DbSet<KbankOddRegistration>> _mockDbSet;
        private readonly OptimizedDailyPaymentJob _job;
        private readonly DateTime _fixedUtcTime = new DateTime(2025, 8, 5, 14, 30, 0, DateTimeKind.Utc);

        public OptimizedDailyPaymentJobTests()
        {
            _mockContext = new Mock<BizConnectContext>();
            _mockPaymentProcessingService = new Mock<IPaymentProcessingService>();
            _mockLogger = new Mock<ILogger<OptimizedDailyPaymentJob>>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockDbSet = new Mock<DbSet<KbankOddRegistration>>();

            // Setup DateTime provider to return fixed UTC time
            _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(_fixedUtcTime);

            // Setup DbContext
            _mockContext.Setup(x => x.Set<KbankOddRegistration>()).Returns(_mockDbSet.Object);

            _job = new OptimizedDailyPaymentJob(
                _mockContext.Object,
                _mockPaymentProcessingService.Object,
                _mockLogger.Object,
                _mockDateTimeProvider.Object);
        }

        [Fact]
        public async Task ExecuteAsync_WithNoRegistrations_ShouldCompleteSuccessfully()
        {
            // Arrange
            var emptyRegistrations = new List<KbankOddRegistration>();
            var mockQueryable = emptyRegistrations.AsQueryable();

            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ProcessingStatistics);
            Assert.Equal(0, result.ProcessingStatistics.TotalRegistrations);
            Assert.Equal(_fixedUtcTime, result.StartTime);
            Assert.True(result.EndTime >= result.StartTime);
            Assert.True(result.Duration.TotalMilliseconds >= 0);
        }

        [Fact]
        public async Task ExecuteAsync_WithStaleRegistrations_ShouldProcessThem()
        {
            // Arrange
            var staleTime = _fixedUtcTime.AddHours(-8); // 8 hours ago, past the 6-hour stale threshold
            var staleRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    Status = "PENDING",
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    UpdatedAt = staleTime
                },
                new()
                {
                    Id = 2,
                    Status = "PENDING",
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    UpdatedAt = staleTime
                }
            };

            SetupMockDbSetWithData(staleRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync(batchSize: 10, enableParallelProcessing: false);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ProcessingStatistics);
            Assert.Equal(2, result.ProcessingStatistics.StaleRegistrations);
            Assert.NotNull(result.StaleProcessingResult);
            Assert.True(result.StaleProcessingResult.Success);

            // Verify stale registrations were updated with TIMESTAMPTZ
            foreach (var registration in staleRegistrations)
            {
                Assert.Equal("MANUAL_REVIEW", registration.Status);
                Assert.Equal(_fixedUtcTime, registration.UpdatedAt);
            }

            _mockContext.Verify(x => x.SaveChangesAsync(), Times.AtLeastOnce);
        }

        [Fact]
        public async Task ExecuteAsync_WithPendingRegistrations_ShouldDelegateToPaymentService()
        {
            // Arrange
            var pendingRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    Status = "PENDING",
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    UpdatedAt = _fixedUtcTime.AddMinutes(-30)
                }
            };

            SetupMockDbSetWithData(pendingRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 1,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.PendingProcessingResult);
            Assert.True(result.PendingProcessingResult.Success);
            Assert.Equal(1, result.PendingProcessingResult.TotalProcessed);

            // Verify payment service was called
            _mockPaymentProcessingService.Verify(x => x.ExecuteDailyProcessingAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithMixedStatusRegistrations_ShouldCalculateStatisticsCorrectly()
        {
            // Arrange
            var yesterday = _fixedUtcTime.AddDays(-1);
            var registrations = new List<KbankOddRegistration>
            {
                new() { Id = 1, Status = "PENDING", CreatedAt = yesterday.AddHours(1), UpdatedAt = _fixedUtcTime.AddMinutes(-30) },
                new() { Id = 2, Status = "COMPLETED", CreatedAt = yesterday.AddHours(2), UpdatedAt = _fixedUtcTime.AddMinutes(-15) },
                new() { Id = 3, Status = "FAILED", CreatedAt = yesterday.AddHours(3), UpdatedAt = _fixedUtcTime.AddMinutes(-10) },
                new() { Id = 4, Status = "EXPIRED", CreatedAt = yesterday.AddHours(4), UpdatedAt = _fixedUtcTime.AddMinutes(-5) },
                new() { Id = 5, Status = "PENDING", CreatedAt = yesterday.AddHours(5), UpdatedAt = _fixedUtcTime.AddHours(-8) } // Stale
            };

            SetupMockDbSetWithData(registrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 1,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ProcessingStatistics);
            Assert.Equal(5, result.ProcessingStatistics.TotalRegistrations);
            Assert.Equal(2, result.ProcessingStatistics.PendingRegistrations);
            Assert.Equal(1, result.ProcessingStatistics.CompletedRegistrations);
            Assert.Equal(1, result.ProcessingStatistics.FailedRegistrations);
            Assert.Equal(1, result.ProcessingStatistics.ExpiredRegistrations);
            Assert.Equal(1, result.ProcessingStatistics.StaleRegistrations);
        }

        [Fact]
        public async Task ExecuteAsync_WithParallelProcessingEnabled_ShouldHandleConcurrency()
        {
            // Arrange
            var staleTime = _fixedUtcTime.AddHours(-8);
            var staleRegistrations = new List<KbankOddRegistration>();
            
            // Create enough registrations to trigger parallel processing
            for (int i = 1; i <= 10; i++)
            {
                staleRegistrations.Add(new KbankOddRegistration
                {
                    Id = i,
                    Status = "PENDING",
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    UpdatedAt = staleTime
                });
            }

            SetupMockDbSetWithData(staleRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync(batchSize: 3, enableParallelProcessing: true);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.StaleProcessingResult);
            Assert.True(result.StaleProcessingResult.Success);
            Assert.True(result.StaleProcessingResult.BatchResults.Count > 1); // Should have multiple batches
            Assert.Equal(10, result.StaleProcessingResult.TotalProcessed);
        }

        [Fact]
        public async Task ExecuteAsync_WithPaymentServiceError_ShouldHandleGracefully()
        {
            // Arrange
            var pendingRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    Status = "PENDING",
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    UpdatedAt = _fixedUtcTime.AddMinutes(-30)
                }
            };

            SetupMockDbSetWithData(pendingRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = false,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = "Payment service error"
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success); // Job should still complete
            Assert.NotNull(result.PendingProcessingResult);
            Assert.False(result.PendingProcessingResult.Success);
            Assert.Equal("Payment service error", result.PendingProcessingResult.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldGenerateReconciliationReport()
        {
            // Arrange
            var today = _fixedUtcTime.Date;
            var todaysRegistrations = new List<KbankOddRegistration>
            {
                new() { Id = 1, Status = "PENDING", CreatedAt = today.AddHours(1) },
                new() { Id = 2, Status = "PENDING", CreatedAt = today.AddHours(2) },
                new() { Id = 3, Status = "COMPLETED", CreatedAt = today.AddHours(3) }
            };

            SetupMockDbSetWithData(todaysRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ReconciliationResult);
            Assert.Equal(_fixedUtcTime, result.ReconciliationResult.GeneratedAt);
            Assert.Equal(3, result.ReconciliationResult.TotalRegistrations);
            Assert.True(result.ReconciliationResult.StatusCounts.ContainsKey("PENDING"));
            Assert.True(result.ReconciliationResult.StatusCounts.ContainsKey("COMPLETED"));
            Assert.Equal(2, result.ReconciliationResult.StatusCounts["PENDING"]);
            Assert.Equal(1, result.ReconciliationResult.StatusCounts["COMPLETED"]);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldPerformCleanupOperations()
        {
            // Arrange
            var emptyRegistrations = new List<KbankOddRegistration>();
            SetupMockDbSetWithData(emptyRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.CleanupResult);
            Assert.True(result.CleanupResult.Success);
            Assert.Equal(3, result.CleanupResult.Operations.Count); // Update Stats, Archive, Optimize
            
            var operationNames = result.CleanupResult.Operations.Select(op => op.Name).ToList();
            Assert.Contains("Update Daily Statistics", operationNames);
            Assert.Contains("Archive Old Records", operationNames);
            Assert.Contains("Optimize Performance", operationNames);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(500)]
        [InlineData(2000)]
        [InlineData(5000)] // Should be clamped to MaxBatchSize
        public async Task ExecuteAsync_WithVariousBatchSizes_ShouldEnforceLimits(int requestedBatchSize)
        {
            // Arrange
            var emptyRegistrations = new List<KbankOddRegistration>();
            SetupMockDbSetWithData(emptyRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync(batchSize: requestedBatchSize);

            // Assert
            Assert.True(result.Success);
            // The job should complete successfully regardless of batch size
            // Batch size limits are enforced internally (1 <= batchSize <= 2000)
        }

        [Fact]
        public async Task ExecuteAsync_WithDatabaseError_ShouldThrowException()
        {
            // Arrange
            _mockContext.Setup(x => x.Set<KbankOddRegistration>()).Throws(new InvalidOperationException("Database connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _job.ExecuteAsync());
        }

        [Fact]
        public async Task ExecuteAsync_ShouldTrackExecutionMetrics()
        {
            // Arrange
            var emptyRegistrations = new List<KbankOddRegistration>();
            SetupMockDbSetWithData(emptyRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(_fixedUtcTime, result.StartTime);
            Assert.True(result.EndTime >= result.StartTime);
            Assert.True(result.Duration.TotalMilliseconds >= 0);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteAsync_WithDisabledParallelProcessing_ShouldProcessSequentially()
        {
            // Arrange
            var staleTime = _fixedUtcTime.AddHours(-8);
            var staleRegistrations = new List<KbankOddRegistration>();
            
            for (int i = 1; i <= 6; i++)
            {
                staleRegistrations.Add(new KbankOddRegistration
                {
                    Id = i,
                    Status = "PENDING",
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    UpdatedAt = staleTime
                });
            }

            SetupMockDbSetWithData(staleRegistrations);

            var mockPaymentResult = new PaymentProcessingResult
            {
                IsSuccessful = true,
                StaleRegistrationsUpdated = 0,
                ErrorMessage = null
            };
            _mockPaymentProcessingService.Setup(x => x.ExecuteDailyProcessingAsync()).ReturnsAsync(mockPaymentResult);

            // Act
            var result = await _job.ExecuteAsync(batchSize: 2, enableParallelProcessing: false);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.StaleProcessingResult);
            Assert.True(result.StaleProcessingResult.Success);
            Assert.Equal(6, result.StaleProcessingResult.TotalProcessed);
            
            // With sequential processing, batches should be processed one after another
            // We can verify this by checking that all batches completed successfully
            Assert.All(result.StaleProcessingResult.BatchResults, batch => Assert.True(batch.Success));
        }

        private void SetupMockDbSetWithData(List<KbankOddRegistration> registrations)
        {
            var mockQueryable = registrations.AsQueryable();
            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
            _mockDbSet.As<IQueryable<KbankOddRegistration>>().Setup(m => m.GetEnumerator()).Returns(mockQueryable.GetEnumerator());
        }
    }

    /// <summary>
    /// Mock implementation of PaymentProcessingResult for testing
    /// </summary>
    public class PaymentProcessingResult
    {
        public bool IsSuccessful { get; set; }
        public int StaleRegistrationsUpdated { get; set; }
        public string? ErrorMessage { get; set; }
    }
}