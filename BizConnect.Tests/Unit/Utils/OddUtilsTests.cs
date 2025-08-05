using BizConnect.Services.Utils;
using Xunit;

namespace BizConnect.Tests.Unit.Utils;

public class OddUtilsTests
{
    [Fact]
    public void BuildAuth_WithValidInputs_ReturnsCorrectHash()
    {
        // Arrange
        var passPhrase = "testpassphrase";
        var param1 = "param1";
        var param2 = "param2";

        // Act
        var result = OddUtils.BuildAuth(passPhrase, param1, param2);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Equal(64, result.Length); // SHA-256 produces 64 character hex string
        Assert.True(result.All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F'))); // Should be uppercase hex
    }

    [Fact]
    public void BuildAuth_WithSameInputs_ReturnsSameHash()
    {
        // Arrange
        var passPhrase = "testpassphrase";
        var param1 = "param1";
        var param2 = "param2";

        // Act
        var result1 = OddUtils.BuildAuth(passPhrase, param1, param2);
        var result2 = OddUtils.BuildAuth(passPhrase, param1, param2);

        // Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void BuildAuth_WithDifferentInputs_ReturnsDifferentHash()
    {
        // Arrange
        var passPhrase = "testpassphrase";
        var param1 = "param1";
        var param2 = "param2";
        var param3 = "param3";

        // Act
        var result1 = OddUtils.BuildAuth(passPhrase, param1, param2);
        var result2 = OddUtils.BuildAuth(passPhrase, param1, param3);

        // Assert
        Assert.NotEqual(result1, result2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildAuth_WithNullOrEmptyPassPhrase_ThrowsArgumentNullException(string passPhrase)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => OddUtils.BuildAuth(passPhrase, "param1"));
    }

    [Fact]
    public void BuildAuth_WithNoParameters_ThrowsArgumentException()
    {
        // Arrange
        var passPhrase = "testpassphrase";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => OddUtils.BuildAuth(passPhrase));
    }

    [Fact]
    public void BuildAuth_WithNullParameters_ThrowsArgumentException()
    {
        // Arrange
        var passPhrase = "testpassphrase";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => OddUtils.BuildAuth(passPhrase, null));
    }

    [Fact]
    public void GenerateExternalReference_ReturnsValidFormat()
    {
        // Act
        var result = OddUtils.GenerateExternalReference();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(24, result.Length); // Updated to expect 24 characters (20 + 4 GUID chars)
        Assert.StartsWith("BIZ", result);
        
        // Verify the datetime part can be parsed (first 17 chars after "BIZ")
        var dateTimePart = result.Substring(3, 17);
        Assert.True(DateTime.TryParseExact(dateTimePart, "yyyyMMddHHmmssfff", null, 
            System.Globalization.DateTimeStyles.None, out var parsedDateTime));
        
        // Verify the GUID part (last 4 chars should be hex)
        var guidPart = result.Substring(20, 4);
        Assert.All(guidPart, c => Assert.True("0123456789abcdefABCDEF".Contains(c)));
        
        // Should be close to current time (within 1 second)
        var timeDiff = Math.Abs((DateTime.Now - parsedDateTime).TotalSeconds);
        Assert.True(timeDiff < 1);
    }

    [Fact]
    public void GenerateExternalReference_GeneratesUniqueValues()
    {
        // Act
        var result1 = OddUtils.GenerateExternalReference();
        Thread.Sleep(1); // Ensure different milliseconds
        var result2 = OddUtils.GenerateExternalReference();

        // Assert
        Assert.NotEqual(result1, result2);
    }

    [Theory]
    [InlineData("BIZ20250713123456789", true)]
    [InlineData("BIZ20250713000000000", true)]
    [InlineData("BIZ20251231235959999", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("ABC20250713123456789", false)]
    [InlineData("BIZ2025071312345678", false)] // Too short
    [InlineData("BIZ202507131234567890", false)] // Too long
    [InlineData("BIZ20250713123456abc", false)] // Invalid datetime
    [InlineData("BIZ20251301123456789", false)] // Invalid month
    [InlineData("BIZ20250732123456789", false)] // Invalid day
    public void IsValidExternalReference_WithVariousInputs_ReturnsExpectedResult(string input, bool expected)
    {
        // Act
        var result = OddUtils.IsValidExternalReference(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
