using System.Text;
using System.Text.Json;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.KBank;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Clients;

/// <summary>
/// HTTP client for KBank Online Direct Debit API communication
/// </summary>
public class KBankOddClient : IKBankOddClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KBankOddClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public KBankOddClient(HttpClient httpClient, IConfiguration configuration, ILogger<KBankOddClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Calls KBank's registration initialization endpoint
    /// </summary>
    /// <param name="request">Initialization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Initialization response</returns>
    /// <exception cref="KBankApiException">Thrown when API call fails</exception>
    public async Task<KBankInitResponse> InitAsync(KBankInitRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calling KBank ODD initialization API for external reference: {ExternalReference}", 
                request.ExternalReference);

            // Validate required authentication parameter
            if (string.IsNullOrEmpty(request.AuthParameter))
            {
                var error = "AuthParameter is required but not provided";
                _logger.LogError(error);
                throw new KBankApiException(error, "ValidationError");
            }

            // Validate AuthParameter format (SHA-256 hash should be 64 hex characters)
            if (request.AuthParameter.Length != 64 || !IsValidHexString(request.AuthParameter))
            {
                var error = $"AuthParameter has invalid format. Expected 64-character hex string, got: {request.AuthParameter.Length} characters";
                _logger.LogError(error);
                throw new KBankApiException(error, "ValidationError");
            }

            var baseUrl = _configuration["KBankODD:BaseUrl"] ?? throw new InvalidOperationException("KBankODD:BaseUrl not configured");
            var endpoint = "/ws/v1/registerinit";
            var requestUrl = $"{baseUrl.TrimEnd('/')}{endpoint}";

            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending request to {Url} with ExternalReference: {ExternalReference}", 
                requestUrl, request.ExternalReference);

            var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Received response from KBank API: Status={StatusCode}, ContentLength={ContentLength}, Content={Content}",
                response.StatusCode, responseContent?.Length ?? 0, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = GetErrorDetail(response.StatusCode, responseContent);
                _logger.LogError("KBank API returned error status: {StatusCode}, ErrorType: {ErrorType}, Message: {Message}", 
                    response.StatusCode, errorDetail.Type, errorDetail.Message);
                throw new KBankApiException($"KBank API error ({errorDetail.Type}): {errorDetail.Message}", errorDetail.Type);
            }

            var initResponse = JsonSerializer.Deserialize<KBankInitResponse>(responseContent, _jsonOptions);
            
            if (initResponse == null)
            {
                _logger.LogError("Failed to deserialize KBank API response: {Content}", responseContent);
                throw new KBankApiException("Failed to deserialize KBank API response");
            }

            _logger.LogInformation("KBank ODD initialization completed for external reference: {ExternalReference}, RegId: {RegId}, Status: {Status}", 
                request.ExternalReference, initResponse.RegId, initResponse.ReturnStatus);

            return initResponse;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when calling KBank API for external reference: {ExternalReference}", 
                request.ExternalReference);
            throw new KBankApiException("Failed to communicate with KBank API", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Request timeout when calling KBank API for external reference: {ExternalReference}", 
                request.ExternalReference);
            throw new KBankApiException("Request to KBank API timed out", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize/deserialize JSON when calling KBank API for external reference: {ExternalReference}", 
                request.ExternalReference);
            throw new KBankApiException("JSON processing error when communicating with KBank API", ex);
        }
    }

    /// <summary>
    /// Tests connectivity to KBank API for health monitoring
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if KBank API is accessible</returns>
    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing connectivity to KBank API");

            var baseUrl = _configuration["KBankODD:BaseUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("KBank API base URL not configured");
                return false;
            }

            // Test with a simple HEAD request to check if the API is reachable
            var testUrl = $"{baseUrl.TrimEnd('/')}/health"; // Assuming there's a health endpoint
            
            using var request = new HttpRequestMessage(HttpMethod.Head, testUrl);
            request.Headers.Add("User-Agent", "BizConnect-HealthCheck/1.0");
            
            // Use short timeout for connectivity test
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            
            var response = await _httpClient.SendAsync(request, combinedCts.Token);
            
            var isConnected = response.StatusCode != System.Net.HttpStatusCode.RequestTimeout &&
                             response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable;
            
            _logger.LogDebug("KBank API connectivity test result: {IsConnected} (Status: {StatusCode})", 
                isConnected, response.StatusCode);
            
            return isConnected;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "KBank API connectivity test failed due to HTTP error");
            return false;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "KBank API connectivity test timed out");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during KBank API connectivity test");
            return false;
        }
    }

    /// <summary>
    /// Validates if a string contains only hexadecimal characters
    /// </summary>
    /// <param name="input">Input string to validate</param>
    /// <returns>True if input is valid hexadecimal</returns>
    private static bool IsValidHexString(string input)
    {
        return input.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }

    /// <summary>
    /// Maps HTTP status codes and response content to structured error details
    /// </summary>
    /// <param name="statusCode">HTTP status code from KBank API</param>
    /// <param name="responseContent">Response content from KBank API</param>
    /// <returns>Structured error details</returns>
    private static (string Type, string Message) GetErrorDetail(System.Net.HttpStatusCode statusCode, string? responseContent)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.BadRequest => ("ValidationError", "Invalid request data or parameters"),
            System.Net.HttpStatusCode.Unauthorized => ("AuthenticationError", "Authentication failed or invalid credentials"),
            System.Net.HttpStatusCode.Forbidden => ("AuthorizationError", "Access denied or insufficient permissions"),
            System.Net.HttpStatusCode.NotFound => ("NotFound", "Requested resource not found"),
            System.Net.HttpStatusCode.TooManyRequests => ("RateLimitError", "Request rate limit exceeded"),
            System.Net.HttpStatusCode.InternalServerError => ("ServerError", "KBank internal server error"),
            System.Net.HttpStatusCode.BadGateway => ("GatewayError", "KBank gateway error"),
            System.Net.HttpStatusCode.ServiceUnavailable => ("ServiceUnavailable", "KBank service temporarily unavailable"),
            System.Net.HttpStatusCode.GatewayTimeout => ("TimeoutError", "KBank gateway timeout"),
            _ => ("UnknownError", $"HTTP {(int)statusCode}: {responseContent}")
        };
    }
}

/// <summary>
/// Exception thrown when KBank API operations fail with enhanced error categorization
/// </summary>
public class KBankApiException : Exception
{
    /// <summary>
    /// Gets the error type category (e.g., ValidationError, AuthenticationError, etc.)
    /// </summary>
    public string ErrorType { get; }

    /// <summary>
    /// Initializes a new instance with basic message
    /// </summary>
    /// <param name="message">Error message</param>
    public KBankApiException(string message) : base(message) 
    {
        ErrorType = "UnknownError";
    }

    /// <summary>
    /// Initializes a new instance with message and error type
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="errorType">Error type category</param>
    public KBankApiException(string message, string errorType) : base(message) 
    {
        ErrorType = errorType;
    }

    /// <summary>
    /// Initializes a new instance with message and inner exception
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="innerException">Inner exception</param>
    public KBankApiException(string message, Exception innerException) : base(message, innerException) 
    {
        ErrorType = "UnknownError";
    }

    /// <summary>
    /// Indicates if the error is potentially recoverable through retry
    /// </summary>
    public bool IsRetryable => ErrorType is "TimeoutError" or "ServiceUnavailable" or "GatewayError" or "ServerError";
}
