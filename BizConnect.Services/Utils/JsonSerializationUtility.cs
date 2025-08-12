using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Utils;

/// <summary>
/// Utility service for robust JSON serialization and deserialization with fallback strategies
/// Specifically designed to handle KBank API response variations and other external API integrations
/// </summary>
public class JsonSerializationUtility
{
    private readonly ILogger<JsonSerializationUtility> _logger;
    
    /// <summary>
    /// Primary JSON serializer options using snake_case naming policy
    /// </summary>
    public static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString // Handle string numbers from APIs
    };

    /// <summary>
    /// Fallback JSON serializer options using camelCase naming policy
    /// </summary>
    public static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    /// <summary>
    /// Strict JSON serializer options for cases where exact property names are required
    /// </summary>
    public static readonly JsonSerializerOptions StrictOptions = new()
    {
        PropertyNamingPolicy = null, // Use exact property names
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public JsonSerializationUtility(ILogger<JsonSerializationUtility> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to deserialize JSON with multiple fallback strategies
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="json">The JSON string to deserialize</param>
    /// <param name="context">Context information for logging (e.g., "KBank API response", "External Reference: 12345")</param>
    /// <returns>Deserialized object or null if all strategies fail</returns>
    public T? TryDeserializeWithFallback<T>(string json, string context = "Unknown") where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            _logger.LogWarning("Cannot deserialize null or empty JSON string. Context: {Context}", context);
            return null;
        }

        var strategies = new[]
        {
            ("SnakeCase", SnakeCaseOptions),
            ("CamelCase", CamelCaseOptions), 
            ("Strict", StrictOptions)
        };

        foreach (var (strategyName, options) in strategies)
        {
            try
            {
                var result = JsonSerializer.Deserialize<T>(json, options);
                if (result != null)
                {
                    if (strategyName != "SnakeCase")
                    {
                        _logger.LogWarning("JSON deserialization succeeded with {Strategy} fallback strategy. Context: {Context}", 
                            strategyName, context);
                    }
                    return result;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "JSON deserialization with {Strategy} strategy failed. Context: {Context}", 
                    strategyName, context);
            }
        }

        _logger.LogError("All JSON deserialization strategies failed. Context: {Context}, JSON Length: {Length}, JSON Preview: {Preview}", 
            context, json.Length, TruncateForLog(json, 200));
        
        return null;
    }

    /// <summary>
    /// Validates if a JSON string is well-formed
    /// </summary>
    /// <param name="json">The JSON string to validate</param>
    /// <returns>True if the JSON is valid, false otherwise</returns>
    public bool IsValidJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Serializes an object to JSON with specified options
    /// </summary>
    /// <typeparam name="T">The type to serialize</typeparam>
    /// <param name="obj">The object to serialize</param>
    /// <param name="options">JSON serializer options to use</param>
    /// <returns>JSON string representation</returns>
    public string Serialize<T>(T obj, JsonSerializerOptions? options = null)
    {
        try
        {
            return JsonSerializer.Serialize(obj, options ?? SnakeCaseOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize object of type {Type}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Analyzes JSON structure and provides insights for troubleshooting
    /// </summary>
    /// <param name="json">The JSON string to analyze</param>
    /// <param name="context">Context information for logging</param>
    /// <returns>Analysis results</returns>
    public JsonAnalysis AnalyzeJson(string json, string context = "Unknown")
    {
        var analysis = new JsonAnalysis
        {
            Context = context,
            Length = json?.Length ?? 0,
            IsValid = false,
            PropertyCount = 0,
            Properties = new List<string>()
        };

        if (string.IsNullOrEmpty(json))
        {
            analysis.ErrorMessage = "JSON is null or empty";
            return analysis;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            analysis.IsValid = true;
            
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                analysis.Properties.AddRange(doc.RootElement.EnumerateObject().Select(p => p.Name));
                analysis.PropertyCount = analysis.Properties.Count;
            }
            
            analysis.RootType = doc.RootElement.ValueKind.ToString();
        }
        catch (JsonException ex)
        {
            analysis.ErrorMessage = ex.Message;
            _logger.LogWarning("JSON analysis failed for context: {Context}. Error: {Error}", context, ex.Message);
        }

        return analysis;
    }

    /// <summary>
    /// Truncates a string for safe logging
    /// </summary>
    private static string TruncateForLog(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
            return input;
        
        return input.Substring(0, maxLength) + "... (truncated)";
    }
}

/// <summary>
/// Results of JSON structure analysis
/// </summary>
public class JsonAnalysis
{
    public string Context { get; set; } = string.Empty;
    public int Length { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RootType { get; set; }
    public int PropertyCount { get; set; }
    public List<string> Properties { get; set; } = new();
}