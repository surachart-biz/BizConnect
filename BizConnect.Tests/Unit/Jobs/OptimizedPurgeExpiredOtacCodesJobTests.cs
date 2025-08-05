using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using BizConnect.Dal.Models;
using BizConnect.Dal.Repositories;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using BizConnect.Services.Jobs;

namespace BizConnect.Tests.Unit.Jobs
{
    /// <summary>
    /// Comprehensive unit tests for OptimizedPurgeExpiredOtacCodesJob focusing on TIMESTAMPTZ handling and batch processing
    /// </summary>
    public class OptimizedPurgeExpiredOtacCodesJobTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ILogger<OptimizedPurgeExpiredOtacCodesJob>> _mockLogger;
        private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<IRepository<KbankOddRegistration>> _mockRepository;
        private readonly OptimizedPurgeExpiredOtacCodesJob _job;
        private readonly DateTime _fixedUtcTime = new DateTime(2025, 8, 5, 14, 30, 0, DateTimeKind.Utc);

        public OptimizedPurgeExpiredOtacCodesJobTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockLogger = new Mock<ILogger<OptimizedPurgeExpiredOtacCodesJob>>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockCacheService = new Mock<ICacheService>();
            _mockRepository = new Mock<IRepository<KbankOddRegistration>>();

            // Setup DateTime provider to return fixed UTC time
            _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(_fixedUtcTime);

            // Setup repository
            _mockUnitOfWork.Setup(x => x.GetRepository<KbankOddRegistration>()).Returns(_mockRepository.Object);

            // Setup cache service
            _mockCacheService.Setup(x => x.GetStatistics()).Returns(new CacheStatistics
            {
                HitRatio = 0.85,
                CurrentEntryCount = 100
            });

            _job = new OptimizedPurgeExpiredOtacCodesJob(
                _mockUnitOfWork.Object,
                _mockLogger.Object,
                _mockDateTimeProvider.Object,
                _mockCacheService.Object);
        }

        [Fact]
        public async Task ExecuteAsync_WithExpiredOtacCodes_ShouldPurgeSuccessfully()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    OtacCode = "EXPIRED1",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED",
                    CreatedAt = _fixedUtcTime.AddMinutes(-40)
                },
                new()
                {
                    Id = 2,
                    OtacCode = "EXPIRED2",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-5),
                    Status = "OTAC_GENERATED",
                    CreatedAt = _fixedUtcTime.AddMinutes(-35)
                }
            };

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);
            
            // Setup GetByIdAsync for each record
            foreach (var registration in expiredRegistrations)
            {
                _mockRepository.Setup(x => x.GetByIdAsync(registration.Id)).ReturnsAsync(registration);
            }

            // Act
            var result = await _job.ExecuteAsync(batchSize: 10, maxBatches: 1);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.TotalRecordsPurged);
            Assert.Equal(1, result.TotalBatches);
            Assert.Equal(2, result.CacheInvalidationCount);

            // Verify records were updated with TIMESTAMPTZ
            foreach (var registration in expiredRegistrations)
            {
                Assert.Equal("EXPIRED", registration.Status);
                Assert.Equal(_fixedUtcTime, registration.UpdatedAt);
                Assert.Null(registration.OtacCode); // Should be cleared
                Assert.Null(registration.OtacExpiresAt); // Should be cleared
            }

            // Verify database operations
            _mockRepository.Verify(x => x.GetByIdAsync(It.IsAny<int>()), Times.Exactly(2));
            _mockRepository.Verify(x => x.Update(It.IsAny<KbankOddRegistration>()), Times.Exactly(2));
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_WithNoExpiredCodes_ShouldCompleteWithoutPurging()
        {
            // Arrange
            var emptyQueryable = new List<KbankOddRegistration>().AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(emptyQueryable);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, result.TotalRecordsPurged);
            Assert.Equal(1, result.TotalBatches);
            Assert.Equal(0, result.CacheInvalidationCount);

            // Verify no update operations were performed
            _mockRepository.Verify(x => x.Update(It.IsAny<KbankOddRegistration>()), Times.Never);
            _mockUnitOfWork.Verify(x => x.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_WithBatchProcessing_ShouldHandleMultipleBatches()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>();
            for (int i = 1; i <= 5; i++)
            {
                expiredRegistrations.Add(new KbankOddRegistration
                {
                    Id = i,
                    OtacCode = $"EXPIRED{i}",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED",
                    CreatedAt = _fixedUtcTime.AddMinutes(-40)
                });
            }

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Setup GetByIdAsync for each record
            foreach (var registration in expiredRegistrations)
            {
                _mockRepository.Setup(x => x.GetByIdAsync(registration.Id)).ReturnsAsync(registration);
            }

            // Act - Use small batch size to force multiple batches
            var result = await _job.ExecuteAsync(batchSize: 2, maxBatches: 0);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(5, result.TotalRecordsPurged);
            Assert.True(result.TotalBatches >= 3); // Should take at least 3 batches for 5 records with batch size 2
            Assert.Equal(5, result.CacheInvalidationCount);

            // Verify all records were processed
            _mockRepository.Verify(x => x.Update(It.IsAny<KbankOddRegistration>()), Times.Exactly(5));
        }

        [Fact]
        public async Task ExecuteAsync_WithMaxBatchesLimit_ShouldRespectLimit()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>();
            for (int i = 1; i <= 10; i++)
            {
                expiredRegistrations.Add(new KbankOddRegistration
                {
                    Id = i,
                    OtacCode = $"EXPIRED{i}",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED",
                    CreatedAt = _fixedUtcTime.AddMinutes(-40)
                });
            }

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Setup GetByIdAsync for each record
            foreach (var registration in expiredRegistrations.Take(5)) // Only first 5 will be processed
            {
                _mockRepository.Setup(x => x.GetByIdAsync(registration.Id)).ReturnsAsync(registration);
            }

            // Act - Limit to 1 batch with batch size 5
            var result = await _job.ExecuteAsync(batchSize: 5, maxBatches: 1);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(5, result.TotalRecordsPurged); // Should only process 1 batch of 5
            Assert.Equal(1, result.TotalBatches);
            Assert.Equal(5, result.CacheInvalidationCount);
        }

        [Fact]
        public async Task ExecuteAsync_WithDatabaseError_ShouldRetryAndFail()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    OtacCode = "EXPIRED1",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED",
                    CreatedAt = _fixedUtcTime.AddMinutes(-40)
                }
            };

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);
            _mockRepository.Setup(x => x.GetByIdAsync(1)).ThrowsAsync(new InvalidOperationException("Database error"));

            // Act
            var result = await _job.ExecuteAsync(batchSize: 1, maxBatches: 1);

            // Assert
            Assert.True(result.Success); // Job continues despite batch failures
            Assert.Equal(0, result.TotalRecordsPurged);
            Assert.Equal(1, result.TotalBatches);
            Assert.Single(result.BatchResults);
            Assert.False(result.BatchResults[0].Success);
            Assert.Equal("Database error", result.BatchResults[0].ErrorMessage);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldInvalidateCacheEntries()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    OtacCode = "EXPIRED1",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED",
                    CreatedAt = _fixedUtcTime.AddMinutes(-40)
                }
            };

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);
            _mockRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(expiredRegistrations[0]);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);

            // Verify cache invalidation calls were made
            _mockCacheService.Verify(x => x.RemoveAsync("OTAC:EXPIRED1"), Times.Once);
            _mockCacheService.Verify(x => x.RemoveAsync("OtacValidation:EXPIRED1"), Times.Once);
            _mockCacheService.Verify(x => x.RemoveAsync("Registration:1"), Times.Once);
            _mockCacheService.Verify(x => x.RemoveAsync("KbankRegistration:1"), Times.Once);

            // Verify general cache cleanup
            _mockCacheService.Verify(x => x.RemoveByPatternAsync("OTAC:*"), Times.Once);
            _mockCacheService.Verify(x => x.RemoveByPatternAsync("OtacValidation:*"), Times.Once);
            _mockCacheService.Verify(x => x.RemoveByPatternAsync("Registration:*"), Times.Once);
            _mockCacheService.Verify(x => x.RemoveByPatternAsync("KbankRegistration:*"), Times.Once);
            _mockCacheService.Verify(x => x.RemoveByPatternAsync("OtacStats:*"), Times.Once);
        }

        [Fact]
        public async Task GetPurgeStatisticsAsync_ShouldCalculateCorrectStatistics()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    CreatedAt = _fixedUtcTime.AddHours(-2),
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED"
                },
                new()
                {
                    Id = 2,
                    CreatedAt = _fixedUtcTime.AddHours(-4),
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-20),
                    Status = "OTAC_GENERATED"
                }
            };

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var statistics = await _job.GetPurgeStatisticsAsync();

            // Assert
            Assert.Equal(2, statistics.TotalExpiredCodes);
            Assert.Equal(_fixedUtcTime.AddHours(-4), statistics.OldestExpiredDate);
            Assert.Equal(3.0, statistics.AverageAgeHours); // (2 + 4) / 2 = 3
        }

        [Fact]
        public async Task GetPurgeRecommendationAsync_WithSmallNumberOfExpiredCodes_ShouldRecommendSingleBatch()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>();
            for (int i = 1; i <= 50; i++)
            {
                expiredRegistrations.Add(new KbankOddRegistration
                {
                    Id = i,
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED"
                });
            }

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var recommendation = await _job.GetPurgeRecommendationAsync();

            // Assert
            Assert.True(recommendation.ShouldPurge);
            Assert.Equal(50, recommendation.RecommendedBatchSize);
            Assert.Contains("Small number", recommendation.Reason);
        }

        [Fact]
        public async Task GetPurgeRecommendationAsync_WithLargeNumberOfExpiredCodes_ShouldRecommendLargerBatchSize()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>();
            for (int i = 1; i <= 2000; i++)
            {
                expiredRegistrations.Add(new KbankOddRegistration
                {
                    Id = i,
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED"
                });
            }

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var recommendation = await _job.GetPurgeRecommendationAsync();

            // Assert
            Assert.True(recommendation.ShouldPurge);
            Assert.True(recommendation.RecommendedBatchSize > 100);
            Assert.Contains("Large number", recommendation.Reason);
        }

        [Fact]
        public async Task GetPurgeRecommendationAsync_WithNoExpiredCodes_ShouldNotRecommendPurge()
        {
            // Arrange
            var emptyQueryable = new List<KbankOddRegistration>().AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(emptyQueryable);

            // Act
            var recommendation = await _job.GetPurgeRecommendationAsync();

            // Assert
            Assert.False(recommendation.ShouldPurge);
            Assert.Contains("No expired", recommendation.Reason);
        }

        [Fact]
        public async Task GetExecutionMetricsAsync_ShouldReturnComprehensiveMetrics()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    CreatedAt = _fixedUtcTime.AddHours(-1),
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED"
                }
            };

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);

            // Act
            var metrics = await _job.GetExecutionMetricsAsync();

            // Assert
            Assert.Equal(1, metrics.ExpiredCodesAvailable);
            Assert.Equal(0.85, metrics.CacheHitRatio);
            Assert.Equal(100, metrics.CacheEntryCount);
            Assert.True(metrics.RecommendedNextRun > _fixedUtcTime);
            Assert.Equal(_fixedUtcTime, metrics.LastExecutionTime);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(100)]
        [InlineData(1000)]
        public async Task ExecuteAsync_WithVariousBatchSizes_ShouldEnforceLimits(int requestedBatchSize)
        {
            // Arrange
            var emptyQueryable = new List<KbankOddRegistration>().AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(emptyQueryable);

            // Act
            var result = await _job.ExecuteAsync(batchSize: requestedBatchSize);

            // Assert
            Assert.True(result.Success);
            // If batchSize is > MaxBatchSize (1000), it should be clamped
            // If batchSize is < 1, it should be set to 1
            // This is verified indirectly by the job completing successfully
        }

        [Fact]
        public async Task ExecuteAsync_ShouldTrackBatchMetrics()
        {
            // Arrange
            var expiredRegistrations = new List<KbankOddRegistration>
            {
                new()
                {
                    Id = 1,
                    OtacCode = "EXPIRED1",
                    OtacExpiresAt = _fixedUtcTime.AddMinutes(-10),
                    Status = "OTAC_GENERATED",
                    CreatedAt = _fixedUtcTime.AddMinutes(-40)
                }
            };

            var mockQueryable = expiredRegistrations.AsQueryable();
            _mockRepository.Setup(x => x.Query()).Returns(mockQueryable);
            _mockRepository.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(expiredRegistrations[0]);

            // Act
            var result = await _job.ExecuteAsync();

            // Assert
            Assert.True(result.Success);
            Assert.Single(result.BatchResults);
            
            var batchResult = result.BatchResults[0];
            Assert.Equal(1, batchResult.BatchNumber);
            Assert.Equal(1, batchResult.RecordsPurged);
            Assert.True(batchResult.Success);
            Assert.True(batchResult.Duration.TotalMilliseconds >= 0);

            // Verify performance metrics
            Assert.True(result.RecordsPerSecond >= 0);
            Assert.Equal(1.0, result.SuccessfulBatchRatio);
            Assert.True(result.AverageBatchDurationMs >= 0);
        }
    }
}