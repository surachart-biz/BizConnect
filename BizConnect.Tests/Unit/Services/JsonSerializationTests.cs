using System.Text.Json;
using BizConnect.Services.Models.KBank;
using BizConnect.Services.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BizConnect.Tests.Unit.Services;

/// <summary>
/// Unit tests for JSON serialization utility and KBank model deserialization
/// Tests various scenarios including different naming conventions and malformed JSON
/// </summary>
public class JsonSerializationTests
{
    private readonly JsonSerializationUtility _jsonUtility;
    private readonly Mock<ILogger<JsonSerializationUtility>> _mockLogger;

    public JsonSerializationTests()
    {
        _mockLogger = new Mock<ILogger<JsonSerializationUtility>>();
        _jsonUtility = new JsonSerializationUtility(_mockLogger.Object);
    }

    [Theory]
    [InlineData("snake_case JSON", """{"reg_id": "REG123", "return_status": "0", "return_code": "SUCCESS", "return_message": "Registration successful"}""")]
    [InlineData("camelCase JSON", """{"regId": "REG123", "returnStatus": "0", "returnCode": "SUCCESS", "returnMessage": "Registration successful"}""")]
    [InlineData("PascalCase JSON", """{"RegId": "REG123", "ReturnStatus": "0", "ReturnCode": "SUCCESS", "ReturnMessage": "Registration successful"}""")]
    [InlineData("Mixed case JSON", """{"reg_id": "REG123", "returnStatus": "0", "ReturnCode": "SUCCESS", "return_message": "Registration successful"}""")]
    public void TryDeserializeWithFallback_ShouldHandleVariousNamingConventions(string testName, string json)
    {
        // Act
        var result = _jsonUtility.TryDeserializeWithFallback<KBankInitResponse>(json, testName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REG123", result.RegId);
        Assert.Equal("0", result.ReturnStatus);
        Assert.Equal("SUCCESS", result.ReturnCode);
        Assert.Equal("Registration successful", result.ReturnMessage);
    }

    [Fact]
    public void TryDeserializeWithFallback_ShouldHandleNullOrEmptyJson()
    {
        // Act & Assert
        Assert.Null(_jsonUtility.TryDeserializeWithFallback<KBankInitResponse>(null, "null test"));
        Assert.Null(_jsonUtility.TryDeserializeWithFallback<KBankInitResponse>("", "empty test"));
        Assert.Null(_jsonUtility.TryDeserializeWithFallback<KBankInitResponse>("   ", "whitespace test"));
    }

    [Fact]
    public void TryDeserializeWithFallback_ShouldHandleMalformedJson()
    {
        // Arrange
        var malformedJson = """{"reg_id": "REG123", "return_status": "0","""; // Missing closing brace

        // Act
        var result = _jsonUtility.TryDeserializeWithFallback<KBankInitResponse>(malformedJson, "malformed test");

        // Assert
        Assert.Null(result);
        
        // Verify error logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("All JSON deserialization strategies failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TryDeserializeWithFallback_ShouldHandleJsonWithTrailingCommas()
    {
        // Arrange
        var jsonWithTrailingCommas = """{"reg_id": "REG123", "return_status": "0", "return_code": "SUCCESS", "return_message": "Registration successful",}""";

        // Act
        var result = _jsonUtility.TryDeserializeWithFallback<KBankInitResponse>(jsonWithTrailingCommas, "trailing comma test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REG123", result.RegId);
    }

    [Fact]
    public void TryDeserializeWithFallback_ShouldHandleJsonWithComments()
    {
        // Arrange
        var jsonWithComments = """
        {
            // Registration ID from KBank
            "reg_id": "REG123",
            "return_status": "0", // Success status
            "return_code": "SUCCESS",
            "return_message": "Registration successful"
        }
        """;

        // Act
        var result = _jsonUtility.TryDeserializeWithFallback<KBankInitResponse>(jsonWithComments, "comments test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REG123", result.RegId);
    }

    [Fact]
    public void TryDeserializeWithFallback_ShouldHandleStringNumbers()
    {
        // Arrange - KBank might return numeric values as strings
        var jsonWithStringNumbers = """{"reg_id": "REG123", "return_status": "0", "return_code": "200", "return_message": "Registration successful"}""";

        // Act
        var result = _jsonUtility.TryDeserializeWithFallback<KBankInitResponse>(jsonWithStringNumbers, "string numbers test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REG123", result.RegId);
        Assert.Equal("0", result.ReturnStatus);
        Assert.Equal("200", result.ReturnCode);
    }

    [Fact]
    public void IsValidJson_ShouldCorrectlyIdentifyValidAndInvalidJson()
    {
        // Valid JSON cases
        Assert.True(_jsonUtility.IsValidJson("""{"key": "value"}"""));
        Assert.True(_jsonUtility.IsValidJson("""[]"""));
        Assert.True(_jsonUtility.IsValidJson(""""string""""));
        Assert.True(_jsonUtility.IsValidJson("""123"""));
        Assert.True(_jsonUtility.IsValidJson("""true"""));
        Assert.True(_jsonUtility.IsValidJson("""null"""));

        // Invalid JSON cases
        Assert.False(_jsonUtility.IsValidJson(""));
        Assert.False(_jsonUtility.IsValidJson(null));
        Assert.False(_jsonUtility.IsValidJson("""{"key": "value",}""")); // Trailing comma not allowed in strict mode
        Assert.False(_jsonUtility.IsValidJson("""{"key": value}""")); // Unquoted value
        Assert.False(_jsonUtility.IsValidJson("""{"key": "value""")); // Missing closing brace
    }

    [Fact]
    public void AnalyzeJson_ShouldProvideCorrectAnalysis()
    {
        // Arrange
        var json = """{"reg_id": "REG123", "return_status": "0", "return_code": "SUCCESS", "return_message": "Registration successful"}""";

        // Act
        var analysis = _jsonUtility.AnalyzeJson(json, "test analysis");

        // Assert
        Assert.Equal("test analysis", analysis.Context);
        Assert.True(analysis.IsValid);
        Assert.Equal(4, analysis.PropertyCount);
        Assert.Contains("reg_id", analysis.Properties);
        Assert.Contains("return_status", analysis.Properties);
        Assert.Contains("return_code", analysis.Properties);
        Assert.Contains("return_message", analysis.Properties);
        Assert.Equal("Object", analysis.RootType);
        Assert.Null(analysis.ErrorMessage);
    }

    [Fact]
    public void AnalyzeJson_ShouldHandleMalformedJson()
    {
        // Arrange
        var malformedJson = """{"key": "value",""";

        // Act
        var analysis = _jsonUtility.AnalyzeJson(malformedJson, "malformed test");

        // Assert
        Assert.Equal("malformed test", analysis.Context);
        Assert.False(analysis.IsValid);
        Assert.Equal(0, analysis.PropertyCount);
        Assert.Empty(analysis.Properties);
        Assert.NotNull(analysis.ErrorMessage);
    }

    [Fact]
    public void Serialize_ShouldProduceCorrectSnakeCaseJson()
    {
        // Arrange
        var response = new KBankInitResponse
        {
            RegId = "REG123",
            ReturnStatus = "0",
            ReturnCode = "SUCCESS",
            ReturnMessage = "Registration successful"
        };

        // Act
        var json = _jsonUtility.Serialize(response);

        // Assert
        Assert.Contains("reg_id", json);
        Assert.Contains("return_status", json);
        Assert.Contains("return_code", json);
        Assert.Contains("return_message", json);
        Assert.Contains("REG123", json);
    }

    [Theory]
    [InlineData("""{"extra_field": "value", "reg_id": "REG123", "return_status": "0", "return_code": "SUCCESS", "return_message": "Registration successful"}""")]
    [InlineData("""{"reg_id": "REG123", "return_status": "0", "return_code": "SUCCESS", "return_message": "Registration successful", "another_field": null}""")]
    public void TryDeserializeWithFallback_ShouldIgnoreUnknownProperties(string jsonWithExtraFields)
    {
        // Act
        var result = _jsonUtility.TryDeserializeWithFallback<KBankInitResponse>(jsonWithExtraFields, "extra fields test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("REG123", result.RegId);
        Assert.Equal("0", result.ReturnStatus);
        Assert.Equal("SUCCESS", result.ReturnCode);
        Assert.Equal("Registration successful", result.ReturnMessage);
    }

    [Fact]
    public void StaticJsonOptions_ShouldBeConfiguredCorrectly()
    {
        // Test SnakeCaseOptions
        Assert.NotNull(JsonSerializationUtility.SnakeCaseOptions.PropertyNamingPolicy);
        Assert.True(JsonSerializationUtility.SnakeCaseOptions.PropertyNameCaseInsensitive);
        Assert.True(JsonSerializationUtility.SnakeCaseOptions.AllowTrailingCommas);

        // Test CamelCaseOptions
        Assert.NotNull(JsonSerializationUtility.CamelCaseOptions.PropertyNamingPolicy);
        Assert.True(JsonSerializationUtility.CamelCaseOptions.PropertyNameCaseInsensitive);
        Assert.True(JsonSerializationUtility.CamelCaseOptions.AllowTrailingCommas);

        // Test StrictOptions
        Assert.Null(JsonSerializationUtility.StrictOptions.PropertyNamingPolicy);
        Assert.False(JsonSerializationUtility.StrictOptions.PropertyNameCaseInsensitive);
        Assert.False(JsonSerializationUtility.StrictOptions.AllowTrailingCommas);
    }
}