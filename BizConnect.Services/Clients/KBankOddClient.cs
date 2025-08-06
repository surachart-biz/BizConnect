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

            var baseUrl = _configuration["KBankODD:BaseUrl"] ?? throw new InvalidOperationException("KBankODD:BaseUrl not configured");
            var endpoint = "/ws/v1/registerinit";
            var requestUrl = $"{baseUrl.TrimEnd('/')}{endpoint}";

            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Sending request to {Url}: {Content}", requestUrl, jsonContent);

            var response = await _httpClient.PostAsync(requestUrl, content, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Received response from KBank API: Status={StatusCode}, ContentLength={ContentLength}, Content={Content}",
                response.StatusCode, responseContent?.Length ?? 0, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("KBank API returned error status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, responseContent);
                throw new KBankApiException($"KBank API returned {response.StatusCode}: {responseContent}");
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
}

/// <summary>
/// Exception thrown when KBank API operations fail
/// </summary>
public class KBankApiException : Exception
{
    public KBankApiException(string message) : base(message) { }
    public KBankApiException(string message, Exception innerException) : base(message, innerException) { }
}
