using BizConnect.Services;
using BizConnect.Services.Interfaces;
using System;
using Xunit;

namespace BizConnect.Tests.Unit.Services
{
    /// <summary>
    /// Unit tests for DateTimeProvider service to ensure proper UTC DateTime handling.
    /// These tests verify that the provider correctly implements the IDateTimeProvider contract.
    /// </summary>
    public class DateTimeProviderTests
    {
        private readonly IDateTimeProvider _dateTimeProvider;

        public DateTimeProviderTests()
        {
            _dateTimeProvider = new DateTimeProvider();
        }

        [Fact]
        public void UtcNow_Should_Return_Current_UTC_DateTime()
        {
            // Arrange
            var beforeCall = DateTime.UtcNow;

            // Act
            var result = _dateTimeProvider.UtcNow;
            var afterCall = DateTime.UtcNow;

            // Assert
            Assert.True(result >= beforeCall, "UtcNow should be after or equal to the time before the call");
            Assert.True(result <= afterCall, "UtcNow should be before or equal to the time after the call");
            Assert.Equal(DateTimeKind.Utc, result.Kind);
        }

        [Fact]
        public void Now_Should_Return_Current_Local_DateTime()
        {
            // Arrange
            var beforeCall = DateTime.Now;

            // Act
            var result = _dateTimeProvider.Now;
            var afterCall = DateTime.Now;

            // Assert
            Assert.True(result >= beforeCall, "Now should be after or equal to the time before the call");
            Assert.True(result <= afterCall, "Now should be before or equal to the time after the call");
            Assert.Equal(DateTimeKind.Local, result.Kind);
        }

        [Fact]
        public void Today_Should_Return_Current_Date_Only()
        {
            // Arrange
            var expectedDate = DateTime.Today;

            // Act
            var result = _dateTimeProvider.Today;

            // Assert
            Assert.Equal(expectedDate.Date, result.Date);
            Assert.Equal(TimeSpan.Zero, result.TimeOfDay);
            Assert.Equal(DateTimeKind.Local, result.Kind);
        }

        [Fact]
        public void UtcNow_Should_Be_Consistent_With_System_DateTime()
        {
            // Act
            var providerUtc = _dateTimeProvider.UtcNow;
            var systemUtc = DateTime.UtcNow;

            // Assert - Allow for small timing differences (less than 1 second)
            var difference = Math.Abs((providerUtc - systemUtc).TotalMilliseconds);
            Assert.True(difference < 1000, $"DateTime difference should be less than 1 second, but was {difference}ms");
        }

        [Fact]
        public void Multiple_Calls_Should_Return_Increasing_Times()
        {
            // Act
            var time1 = _dateTimeProvider.UtcNow;
            System.Threading.Thread.Sleep(1); // Ensure some time passes
            var time2 = _dateTimeProvider.UtcNow;

            // Assert
            Assert.True(time2 >= time1, "Second call should return a time equal to or after the first call");
        }

        [Fact]
        public void Provider_Should_Implement_Interface_Correctly()
        {
            // Assert
            Assert.IsAssignableFrom<IDateTimeProvider>(_dateTimeProvider);
            Assert.NotNull(_dateTimeProvider.UtcNow);
            Assert.NotNull(_dateTimeProvider.Now);
            Assert.NotNull(_dateTimeProvider.Today);
        }
    }
}