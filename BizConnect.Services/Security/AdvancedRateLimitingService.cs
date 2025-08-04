using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Caching;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Security.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Security;

/// <summary>
/// Advanced rate limiting service with multi-tier rules, adaptive thresholds, and sophisticated protection mechanisms.
/// Implements sliding window, token bucket, and fixed window algorithms with real-time threat assessment.
/// </summary>
public class AdvancedRateLimitingService : IRateLimitingService
{
    private readonly ICacheService _cacheService;
    private readonly ISecurityAuditService _securityAuditService;
    private readonly ILogger<AdvancedRateLimitingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;

    // Cache key prefixes for different rate limiting contexts
    private const string IpRateLimitPrefix = "AdvancedRateLimit:IP";
    private const string UserRateLimitPrefix = "AdvancedRateLimit:User";
    private const string EndpointRateLimitPrefix = "AdvancedRateLimit:Endpoint";
    private const string GlobalRateLimitPrefix = "AdvancedRateLimit:Global";
    private const string TokenBucketPrefix = "AdvancedRateLimit:TokenBucket";
    private const string ThreatScorePrefix = "AdvancedRateLimit:ThreatScore";

    // Advanced configuration
    private readonly AdvancedRateLimitingConfiguration _config;
    private readonly ConcurrentDictionary<string, RateLimitRule> _dynamicRules;
    private readonly Timer _cleanupTimer;

    public AdvancedRateLimitingService(
        ICacheService cacheService,
        ISecurityAuditService securityAuditService,
        ILogger<AdvancedRateLimitingService> logger,
        IConfiguration configuration,
        IDateTimeProvider dateTimeProvider)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _securityAuditService = securityAuditService ?? throw new ArgumentNullException(nameof(securityAuditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));

        _config = LoadConfiguration();
        _dynamicRules = new ConcurrentDictionary<string, RateLimitRule>();

        // Setup cleanup timer
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

        _logger.LogInformation("Advanced rate limiting service initialized with {RuleCount} default rules", 
            _config.Rules.Count);
    }

    #region IRateLimitingService Implementation

    /// <inheritdoc />
    public async Task<RateLimitStatus> CheckRateLimitAsync(string ipAddress, string context = "login")
    {
        var request = new RateLimitRequest
        {
            IpAddress = ipAddress,
            Context = context,
            Timestamp = _dateTimeProvider.UtcNow
        };

        var result = await CheckAdvancedRateLimitAsync(request);
        
        // Convert AdvancedRateLimitResult to RateLimitStatus object
        var status = new RateLimitStatus
        {
            IsLocked = result.IsBlocked
        };
        
        if (result.Checks != null && result.Checks.Any())
        {
            var primaryCheck = result.Checks.FirstOrDefault();
            if (primaryCheck != null && primaryCheck.Rule != null)
            {
                status.TotalAttempts = primaryCheck.CurrentCount;
                status.RemainingAttempts = Math.Max(0, primaryCheck.Rule.MaxRequests - primaryCheck.CurrentCount);
                
                if (result.IsBlocked && primaryCheck.BlockedUntil.HasValue)
                {
                    status.LockoutEndTime = primaryCheck.BlockedUntil.Value;
                    status.TimeUntilUnlock = primaryCheck.BlockedUntil.Value - _dateTimeProvider.UtcNow;
                    status.Message = primaryCheck.Message ?? $"Rate limit exceeded. Locked until {primaryCheck.BlockedUntil.Value:HH:mm:ss}";
                }
                else if (!result.IsBlocked)
                {
                    status.Message = $"Request allowed. {status.RemainingAttempts} attempts remaining.";
                }
            }
        }
        
        // Fallback message if no checks provided details
        if (string.IsNullOrEmpty(status.Message))
        {
            status.Message = result.IsBlocked 
                ? "Rate limit exceeded. Please try again later." 
                : "Request allowed.";
        }
        
        return status;
    }

    /// <inheritdoc />
    public async Task<RateLimitStatus> CheckRateLimitAsync(string operation, string identifier, CancellationToken cancellationToken = default)
    {
        // Delegate to the existing method with parameter mapping (operation = context, identifier = ipAddress)
        return await CheckRateLimitAsync(identifier, operation);
    }

    /// <inheritdoc />
    public async Task RecordFailedAttemptAsync(string ipAddress, string context = "login", string username = null)
    {
        var request = new RateLimitRequest
        {
            IpAddress = ipAddress,
            Context = context,
            Username = username,
            Timestamp = _dateTimeProvider.UtcNow,
            IsFailedAttempt = true
        };

        await RecordAdvancedAttemptAsync(request);
    }

    /// <inheritdoc />
    public async Task ClearFailedAttemptsAsync(string ipAddress, string context = "login")
    {
        await ClearAdvancedRateLimitAsync(ipAddress, context);
    }

    /// <inheritdoc />
    public async Task<int> GetAttemptCountAsync(string ipAddress, string context = "login")
    {
        var attempts = await GetRecentAttemptsAsync(ipAddress, context);
        return attempts.Count;
    }

    /// <inheritdoc />
    public async Task<UserLockoutStatus> CheckUserLockoutAsync(string username)
    {
        var cacheKey = $"{UserRateLimitPrefix}:{username}:lockout";
        var lockoutInfo = await _cacheService.GetAsync<UserLockoutInfo>(cacheKey);

        if (lockoutInfo != null && lockoutInfo.LockoutEnd > _dateTimeProvider.UtcNow)
        {
            return new UserLockoutStatus
            {
                IsLocked = true,
                LockoutEndTime = lockoutInfo.LockoutEnd,
                FailedAttempts = lockoutInfo.FailedAttempts,
                LastFailedIpAddress = lockoutInfo.LastFailedIpAddress,
                LastFailedAttempt = lockoutInfo.LastFailedAttempt
            };
        }

        return new UserLockoutStatus { IsLocked = false };
    }

    /// <inheritdoc />
    public async Task RecordUserFailedAttemptAsync(string username, string ipAddress)
    {
        var request = new RateLimitRequest
        {
            Username = username,
            IpAddress = ipAddress,
            Context = "login",
            Timestamp = _dateTimeProvider.UtcNow,
            IsFailedAttempt = true
        };

        await RecordAdvancedAttemptAsync(request);
    }

    /// <inheritdoc />
    public async Task ClearUserLockoutAsync(string username)
    {
        var cacheKey = $"{UserRateLimitPrefix}:{username}:lockout";
        await _cacheService.RemoveAsync(cacheKey);
        
        _logger.LogInformation("Cleared user lockout for {Username}", username);
    }

    /// <inheritdoc />
    public RateLimitConfiguration GetConfiguration(string context = "login")
    {
        var rule = GetRuleForContext(context);
        return new RateLimitConfiguration
        {
            Context = context,
            MaxAttempts = rule.MaxRequests,
            LockoutDurationMinutes = (int)rule.LockoutDuration.TotalMinutes,
            AttemptWindowMinutes = (int)rule.TimeWindow.TotalMinutes,
            EnableUserLockout = rule.EnableUserLockout,
            EnableIpLockout = rule.EnableIpLockout
        };
    }

    /// <inheritdoc />
    public async Task CleanupExpiredEntriesAsync()
    {
        await Task.Run(() => CleanupExpiredEntries(null));
    }

    #endregion

    #region Advanced Rate Limiting Methods

    /// <summary>
    /// Comprehensive rate limit check using multiple algorithms and threat assessment.
    /// </summary>
    public async Task<AdvancedRateLimitResult> CheckAdvancedRateLimitAsync(RateLimitRequest request)
    {
        try
        {
            var result = new AdvancedRateLimitResult
            {
                Request = request,
                CheckTime = _dateTimeProvider.UtcNow
            };

            // Get applicable rules
            var rules = GetApplicableRules(request);
            var checks = new List<Task<RateLimitCheck>>();

            // Run all rate limit checks in parallel
            foreach (var rule in rules)
            {
                checks.Add(PerformRateLimitCheckAsync(request, rule));
            }

            // Calculate threat score
            var threatScoreTask = CalculateThreatScoreAsync(request);

            // Wait for all checks to complete
            var checkResults = await Task.WhenAll(checks);
            var threatScore = await threatScoreTask;

            // Combine results
            result.Checks = checkResults.ToList();
            result.ThreatScore = threatScore;
            result.IsBlocked = checkResults.Any(c => c.IsBlocked) || threatScore.Score >= _config.ThreatScoreThreshold;

            // Apply adaptive rules based on threat score
            if (threatScore.Score >= _config.AdaptiveThreshold)
            {
                await ApplyAdaptiveRulesAsync(request, threatScore);
            }

            // Log security events
            if (result.IsBlocked || threatScore.Score >= _config.LoggingThreshold)
            {
                await LogSecurityEventAsync(request, result);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in advanced rate limit check for IP {IpAddress}", request.IpAddress);
            
            // Fail-safe: allow request but log the error
            return new AdvancedRateLimitResult
            {
                Request = request,
                CheckTime = _dateTimeProvider.UtcNow,
                IsBlocked = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Records an advanced attempt with comprehensive tracking and analysis.
    /// </summary>
    public async Task RecordAdvancedAttemptAsync(RateLimitRequest request)
    {
        try
        {
            var tasks = new List<Task>();

            // Record IP-based attempts
            tasks.Add(RecordIpAttemptAsync(request));

            // Record user-based attempts if username provided
            if (!string.IsNullOrEmpty(request.Username))
            {
                tasks.Add(RecordUserAttemptAsync(request));
            }

            // Record endpoint-based attempts
            if (!string.IsNullOrEmpty(request.Endpoint))
            {
                tasks.Add(RecordEndpointAttemptAsync(request));
            }

            // Update global statistics
            tasks.Add(UpdateGlobalStatisticsAsync(request));

            // Update threat score if failed attempt
            if (request.IsFailedAttempt)
            {
                tasks.Add(UpdateThreatScoreAsync(request));
            }

            await Task.WhenAll(tasks);

            _logger.LogDebug("Recorded advanced attempt for IP {IpAddress}, Context {Context}, Failed: {IsFailed}",
                request.IpAddress, request.Context, request.IsFailedAttempt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording advanced attempt for IP {IpAddress}", request.IpAddress);
        }
    }

    /// <summary>
    /// Clears advanced rate limit data for a specific IP and context.
    /// </summary>
    public async Task ClearAdvancedRateLimitAsync(string ipAddress, string context)
    {
        try
        {
            var tasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync($"{IpRateLimitPrefix}:{ipAddress}:{context}:*"),
                _cacheService.RemoveByPatternAsync($"{TokenBucketPrefix}:{ipAddress}:{context}:*"),
                _cacheService.RemoveAsync($"{ThreatScorePrefix}:{ipAddress}")
            };

            await Task.WhenAll(tasks);

            _logger.LogInformation("Cleared advanced rate limit data for IP {IpAddress}, Context {Context}",
                ipAddress, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing advanced rate limit data for IP {IpAddress}", ipAddress);
        }
    }

    /// <summary>
    /// Gets comprehensive rate limiting statistics.
    /// </summary>
    public async Task<AdvancedRateLimitingStatistics> GetStatisticsAsync()
    {
        try
        {
            var stats = new AdvancedRateLimitingStatistics
            {
                GeneratedAt = _dateTimeProvider.UtcNow
            };

            // Aggregate statistics from cache
            var globalStatsKey = $"{GlobalRateLimitPrefix}:statistics";
            var globalStats = await _cacheService.GetAsync<GlobalStatistics>(globalStatsKey);
            
            if (globalStats != null)
            {
                stats.TotalRequests = globalStats.TotalRequests;
                stats.BlockedRequests = globalStats.BlockedRequests;
                stats.FailedAttempts = globalStats.FailedAttempts;
                stats.BlockedIpAddresses = globalStats.BlockedIps.Count;
                stats.BlockedUsers = globalStats.BlockedUsers.Count;
            }

            // Get rule effectiveness
            stats.RuleEffectiveness = await CalculateRuleEffectivenessAsync();

            // Get top threat IPs
            stats.TopThreatIps = await GetTopThreatIpsAsync(10);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting advanced rate limiting statistics");
            return new AdvancedRateLimitingStatistics
            {
                GeneratedAt = _dateTimeProvider.UtcNow,
                Error = ex.Message
            };
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<RateLimitCheck> PerformRateLimitCheckAsync(RateLimitRequest request, RateLimitRule rule)
    {
        var check = new RateLimitCheck
        {
            Rule = rule,
            CheckTime = _dateTimeProvider.UtcNow
        };

        try
        {
            switch (rule.Algorithm)
            {
                case RateLimitAlgorithm.SlidingWindow:
                    check = await CheckSlidingWindowAsync(request, rule);
                    break;
                case RateLimitAlgorithm.TokenBucket:
                    check = await CheckTokenBucketAsync(request, rule);
                    break;
                case RateLimitAlgorithm.FixedWindow:
                    check = await CheckFixedWindowAsync(request, rule);
                    break;
                default:
                    check = await CheckSlidingWindowAsync(request, rule);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing rate limit check with rule {RuleName}", rule.Name);
            check.IsBlocked = false; // Fail-safe
            check.Error = ex.Message;
        }

        return check;
    }

    private async Task<RateLimitCheck> CheckSlidingWindowAsync(RateLimitRequest request, RateLimitRule rule)
    {
        var cacheKey = $"{IpRateLimitPrefix}:{request.IpAddress}:{request.Context}:sliding";
        var attempts = await GetRecentAttemptsAsync(request.IpAddress, request.Context, rule.TimeWindow);

        var check = new RateLimitCheck
        {
            Rule = rule,
            CurrentCount = attempts.Count,
            CheckTime = _dateTimeProvider.UtcNow,
            IsBlocked = attempts.Count >= rule.MaxRequests
        };

        if (check.IsBlocked)
        {
            check.BlockedUntil = _dateTimeProvider.UtcNow.Add(rule.LockoutDuration);
            check.Message = $"Rate limit exceeded. {attempts.Count}/{rule.MaxRequests} requests in {rule.TimeWindow.TotalMinutes} minutes.";
        }

        return check;
    }

    private async Task<RateLimitCheck> CheckTokenBucketAsync(RateLimitRequest request, RateLimitRule rule)
    {
        var cacheKey = $"{TokenBucketPrefix}:{request.IpAddress}:{request.Context}";
        var bucket = await _cacheService.GetAsync<TokenBucket>(cacheKey);

        if (bucket == null)
        {
            bucket = new TokenBucket
            {
                Capacity = rule.MaxRequests,
                Tokens = rule.MaxRequests,
                LastRefill = _dateTimeProvider.UtcNow,
                RefillRate = rule.RefillRate ?? rule.MaxRequests / (int)rule.TimeWindow.TotalMinutes
            };
        }

        // Refill tokens based on time elapsed
        var now = _dateTimeProvider.UtcNow;
        var timeSinceRefill = now - bucket.LastRefill;
        var tokensToAdd = (int)(timeSinceRefill.TotalMinutes * bucket.RefillRate);
        
        bucket.Tokens = Math.Min(bucket.Capacity, bucket.Tokens + tokensToAdd);
        bucket.LastRefill = now;

        var check = new RateLimitCheck
        {
            Rule = rule,
            CurrentCount = bucket.Capacity - bucket.Tokens,
            CheckTime = now,
            IsBlocked = bucket.Tokens < 1
        };

        if (!check.IsBlocked)
        {
            bucket.Tokens--;
        }
        else
        {
            check.Message = $"Token bucket empty. Try again in {60 / bucket.RefillRate} seconds.";
        }

        // Update bucket in cache
        await _cacheService.SetAsync(bucket, cacheKey, rule.TimeWindow);

        return check;
    }

    private async Task<RateLimitCheck> CheckFixedWindowAsync(RateLimitRequest request, RateLimitRule rule)
    {
        var now = _dateTimeProvider.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 
            (now.Minute / (int)rule.TimeWindow.TotalMinutes) * (int)rule.TimeWindow.TotalMinutes, 0);
        
        var cacheKey = $"{IpRateLimitPrefix}:{request.IpAddress}:{request.Context}:fixed:{windowStart:yyyyMMddHHmm}";
        var count = await _cacheService.GetAsync<int?>(cacheKey) ?? 0;

        var check = new RateLimitCheck
        {
            Rule = rule,
            CurrentCount = count,
            CheckTime = now,
            IsBlocked = count >= rule.MaxRequests
        };

        if (check.IsBlocked)
        {
            var nextWindow = windowStart.Add(rule.TimeWindow);
            check.BlockedUntil = nextWindow;
            check.Message = $"Fixed window limit exceeded. Try again at {nextWindow:HH:mm}.";
        }

        return check;
    }

    private async Task<ThreatScore> CalculateThreatScoreAsync(RateLimitRequest request)
    {
        var cacheKey = $"{ThreatScorePrefix}:{request.IpAddress}";
        var threatInfo = await _cacheService.GetAsync<ThreatInfo>(cacheKey);

        if (threatInfo == null)
        {
            threatInfo = new ThreatInfo
            {
                IpAddress = request.IpAddress,
                FirstSeen = _dateTimeProvider.UtcNow
            };
        }

        var score = new ThreatScore
        {
            IpAddress = request.IpAddress,
            CalculatedAt = _dateTimeProvider.UtcNow
        };

        // Calculate various threat factors
        score.FailureRateScore = CalculateFailureRateScore(threatInfo);
        score.VelocityScore = CalculateVelocityScore(threatInfo);
        score.PatternScore = await CalculatePatternScoreAsync(request);
        score.ReputationScore = await CalculateReputationScoreAsync(request.IpAddress);
        score.BehaviorScore = CalculateBehaviorScore(threatInfo);

        // Weighted total score
        score.Score = (score.FailureRateScore * 0.3) +
                     (score.VelocityScore * 0.25) +
                     (score.PatternScore * 0.2) +
                     (score.ReputationScore * 0.15) +
                     (score.BehaviorScore * 0.1);

        // Update threat info
        threatInfo.LastSeen = _dateTimeProvider.UtcNow;
        threatInfo.TotalRequests++;
        if (request.IsFailedAttempt)
        {
            threatInfo.FailedAttempts++;
        }

        await _cacheService.SetAsync(threatInfo, cacheKey, TimeSpan.FromHours(24));

        return score;
    }

    private double CalculateFailureRateScore(ThreatInfo threatInfo)
    {
        if (threatInfo.TotalRequests == 0) return 0;
        
        var failureRate = (double)threatInfo.FailedAttempts / threatInfo.TotalRequests;
        return Math.Min(100, failureRate * 100);
    }

    private double CalculateVelocityScore(ThreatInfo threatInfo)
    {
        var timeSpan = _dateTimeProvider.UtcNow - threatInfo.FirstSeen;
        if (timeSpan.TotalMinutes < 1) return 0;
        
        var requestsPerMinute = threatInfo.TotalRequests / timeSpan.TotalMinutes;
        return Math.Min(100, requestsPerMinute * 2); // Adjust multiplier as needed
    }

    private async Task<double> CalculatePatternScoreAsync(RateLimitRequest request)
    {
        // Look for suspicious patterns (rapid fire requests, consistent timing, etc.)
        var attempts = await GetRecentAttemptsAsync(request.IpAddress, request.Context, TimeSpan.FromMinutes(5));
        
        if (attempts.Count < 3) return 0;

        // Check for consistent timing (potential bot behavior)
        var intervals = new List<double>();
        for (int i = 1; i < attempts.Count; i++)
        {
            intervals.Add((attempts[i] - attempts[i - 1]).TotalSeconds);
        }

        var avgInterval = intervals.Average();
        var variance = intervals.Select(x => Math.Pow(x - avgInterval, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        // Low variance in timing suggests automated behavior
        if (stdDev < 2 && avgInterval < 10) // Very consistent, fast requests
        {
            return Math.Min(100, 80 + (attempts.Count * 2));
        }

        return Math.Min(100, attempts.Count * 5);
    }

    private async Task<double> CalculateReputationScoreAsync(string ipAddress)
    {
        // In a real implementation, you would check IP reputation services
        // For now, we'll simulate based on internal blacklists
        await Task.CompletedTask;
        return 0; // Placeholder
    }

    private double CalculateBehaviorScore(ThreatInfo threatInfo)
    {
        var score = 0.0;
        
        // Recent activity burst
        var timeSinceFirstSeen = _dateTimeProvider.UtcNow - threatInfo.FirstSeen;
        if (timeSinceFirstSeen.TotalMinutes < 10 && threatInfo.TotalRequests > 50)
        {
            score += 30;
        }

        // High failure rate
        if (threatInfo.TotalRequests > 10)
        {
            var failureRate = (double)threatInfo.FailedAttempts / threatInfo.TotalRequests;
            if (failureRate > 0.8)
            {
                score += 40;
            }
        }

        return Math.Min(100, score);
    }

    private async Task RecordIpAttemptAsync(RateLimitRequest request)
    {
        var cacheKey = $"{IpRateLimitPrefix}:{request.IpAddress}:{request.Context}:attempts";
        var attempts = await GetRecentAttemptsAsync(request.IpAddress, request.Context);
        
        attempts.Add(request.Timestamp);
        
        var rule = GetRuleForContext(request.Context);
        await _cacheService.SetAsync(attempts, cacheKey, rule.TimeWindow);
    }

    private async Task RecordUserAttemptAsync(RateLimitRequest request)
    {
        if (string.IsNullOrEmpty(request.Username)) return;

        var cacheKey = $"{UserRateLimitPrefix}:{request.Username}:attempts";
        var attempts = await GetRecentAttemptsAsync(request.Username, request.Context);
        
        attempts.Add(request.Timestamp);
        
        var rule = GetRuleForContext(request.Context);
        await _cacheService.SetAsync(attempts, cacheKey, rule.TimeWindow);

        // Check for user lockout
        if (request.IsFailedAttempt && attempts.Count >= rule.MaxRequests)
        {
            var lockoutInfo = new UserLockoutInfo
            {
                Username = request.Username,
                FailedAttempts = attempts.Count,
                LockoutEnd = _dateTimeProvider.UtcNow.Add(rule.LockoutDuration),
                LastFailedIpAddress = request.IpAddress,
                LastFailedAttempt = request.Timestamp
            };

            var lockoutKey = $"{UserRateLimitPrefix}:{request.Username}:lockout";
            await _cacheService.SetAsync(lockoutInfo, lockoutKey, rule.LockoutDuration);

            await _securityAuditService.LogAccountLockoutAsync(request.IpAddress, attempts.Count);
        }
    }

    private async Task RecordEndpointAttemptAsync(RateLimitRequest request)
    {
        if (string.IsNullOrEmpty(request.Endpoint)) return;

        var cacheKey = $"{EndpointRateLimitPrefix}:{request.Endpoint}:attempts";
        var attempts = await _cacheService.GetAsync<List<DateTime>>(cacheKey) ?? new List<DateTime>();
        
        attempts.Add(request.Timestamp);
        
        var rule = GetRuleForContext("endpoint");
        await _cacheService.SetAsync(attempts, cacheKey, rule.TimeWindow);
    }

    private async Task UpdateGlobalStatisticsAsync(RateLimitRequest request)
    {
        var cacheKey = $"{GlobalRateLimitPrefix}:statistics";
        var stats = await _cacheService.GetAsync<GlobalStatistics>(cacheKey) ?? new GlobalStatistics();

        stats.TotalRequests++;
        if (request.IsFailedAttempt)
        {
            stats.FailedAttempts++;
        }

        stats.LastUpdated = _dateTimeProvider.UtcNow;
        
        await _cacheService.SetAsync(stats, cacheKey, TimeSpan.FromHours(24));
    }

    private async Task UpdateThreatScoreAsync(RateLimitRequest request)
    {
        await CalculateThreatScoreAsync(request); // This also updates the cache
    }

    private async Task<List<DateTime>> GetRecentAttemptsAsync(string identifier, string context, TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? GetRuleForContext(context).TimeWindow;
        var cacheKey = $"{IpRateLimitPrefix}:{identifier}:{context}:attempts";
        
        var allAttempts = await _cacheService.GetAsync<List<DateTime>>(cacheKey) ?? new List<DateTime>();
        var cutoff = _dateTimeProvider.UtcNow - window;
        
        return allAttempts.Where(a => a > cutoff).ToList();
    }

    private List<RateLimitRule> GetApplicableRules(RateLimitRequest request)
    {
        var rules = new List<RateLimitRule>();
        
        // Get context-specific rules
        rules.AddRange(_config.Rules.Where(r => r.Context == request.Context || r.Context == "*"));
        
        // Get dynamic rules
        rules.AddRange(_dynamicRules.Values.Where(r => IsRuleApplicable(r, request)));
        
        return rules;
    }

    private RateLimitRule GetRuleForContext(string context)
    {
        return _config.Rules.FirstOrDefault(r => r.Context == context) ?? _config.DefaultRule;
    }

    private bool IsRuleApplicable(RateLimitRule rule, RateLimitRequest request)
    {
        if (rule.Context != "*" && rule.Context != request.Context) return false;
        
        // Add more sophisticated rule matching logic here
        return true;
    }

    private async Task ApplyAdaptiveRulesAsync(RateLimitRequest request, ThreatScore threatScore)
    {
        // Create more restrictive rules for high-threat IPs
        var adaptiveRule = new RateLimitRule
        {
            Name = $"Adaptive-{request.IpAddress}",
            Context = request.Context,
            MaxRequests = Math.Max(1, _config.DefaultRule.MaxRequests / 2),
            TimeWindow = _config.DefaultRule.TimeWindow,
            LockoutDuration = TimeSpan.FromMinutes(_config.DefaultRule.LockoutDuration.TotalMinutes * 2),
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            Priority = 100,
            ExpiresAt = _dateTimeProvider.UtcNow.AddHours(1)
        };

        _dynamicRules.TryAdd($"{request.IpAddress}:{request.Context}", adaptiveRule);
        
        _logger.LogWarning("Applied adaptive rate limiting for IP {IpAddress} with threat score {ThreatScore}",
            request.IpAddress, threatScore.Score);
    }

    private async Task LogSecurityEventAsync(RateLimitRequest request, AdvancedRateLimitResult result)
    {
        if (result.IsBlocked)
        {
            await _securityAuditService.LogAccountLockoutAsync(request.IpAddress, 
                result.Checks.Max(c => c.CurrentCount));
        }

        _logger.LogInformation("Rate limiting event: IP {IpAddress}, Context {Context}, " +
            "Blocked: {IsBlocked}, ThreatScore: {ThreatScore}",
            request.IpAddress, request.Context, result.IsBlocked, result.ThreatScore?.Score ?? 0);
    }

    private async Task<Dictionary<string, double>> CalculateRuleEffectivenessAsync()
    {
        // Calculate how effective each rule is at blocking threats
        await Task.CompletedTask;
        return new Dictionary<string, double>(); // Placeholder
    }

    private async Task<List<ThreatIpInfo>> GetTopThreatIpsAsync(int count)
    {
        // Get top threat IPs from cache
        await Task.CompletedTask;
        return new List<ThreatIpInfo>(); // Placeholder
    }

    private AdvancedRateLimitingConfiguration LoadConfiguration()
    {
        var config = new AdvancedRateLimitingConfiguration();
        
        // Load from appsettings.json
        _configuration.GetSection("AdvancedRateLimiting").Bind(config);
        
        // Set defaults if not configured
        if (!config.Rules.Any())
        {
            config.Rules = GetDefaultRules();
        }

        if (config.DefaultRule == null)
        {
            config.DefaultRule = config.Rules.First();
        }

        return config;
    }

    private List<RateLimitRule> GetDefaultRules()
    {
        return new List<RateLimitRule>
        {
            // OTAC Validation - Multi-tier limits as specified
            new RateLimitRule
            {
                Name = "OTAC_VALIDATE_Tier1",
                Context = "OTAC_VALIDATE",
                MaxRequests = 3,
                TimeWindow = TimeSpan.FromMinutes(1),
                LockoutDuration = TimeSpan.FromMinutes(1),
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                EnableIpLockout = true,
                EnableUserLockout = false,
                Priority = 1
            },
            new RateLimitRule
            {
                Name = "OTAC_VALIDATE_Tier2",
                Context = "OTAC_VALIDATE",
                MaxRequests = 10,
                TimeWindow = TimeSpan.FromMinutes(15),
                LockoutDuration = TimeSpan.FromMinutes(15),
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                EnableIpLockout = true,
                EnableUserLockout = false,
                Priority = 2
            },
            new RateLimitRule
            {
                Name = "OTAC_VALIDATE_Tier3",
                Context = "OTAC_VALIDATE",
                MaxRequests = 50,
                TimeWindow = TimeSpan.FromHours(1),
                LockoutDuration = TimeSpan.FromHours(1),
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                EnableIpLockout = true,
                EnableUserLockout = false,
                Priority = 3
            },
            
            // Login Attempts - Multi-tier limits
            new RateLimitRule
            {
                Name = "LOGIN_ATTEMPTS_User",
                Context = "LOGIN_ATTEMPTS",
                MaxRequests = 5,
                TimeWindow = TimeSpan.FromMinutes(15),
                LockoutDuration = TimeSpan.FromMinutes(15),
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                EnableIpLockout = false,
                EnableUserLockout = true,
                Priority = 1
            },
            new RateLimitRule
            {
                Name = "LOGIN_ATTEMPTS_IP",
                Context = "LOGIN_ATTEMPTS",
                MaxRequests = 20,
                TimeWindow = TimeSpan.FromHours(1),
                LockoutDuration = TimeSpan.FromHours(1),
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                EnableIpLockout = true,
                EnableUserLockout = false,
                Priority = 2
            },
            
            // API Calls - Per authenticated user
            new RateLimitRule
            {
                Name = "API_CALLS",
                Context = "API_CALLS",
                MaxRequests = 100,
                TimeWindow = TimeSpan.FromMinutes(1),
                LockoutDuration = TimeSpan.FromMinutes(5),
                Algorithm = RateLimitAlgorithm.TokenBucket,
                RefillRate = 10,
                EnableIpLockout = false,
                EnableUserLockout = true
            },
            
            // Registration - Per IP
            new RateLimitRule
            {
                Name = "REGISTRATION",
                Context = "REGISTRATION",
                MaxRequests = 5,
                TimeWindow = TimeSpan.FromHours(1),
                LockoutDuration = TimeSpan.FromMinutes(30),
                Algorithm = RateLimitAlgorithm.FixedWindow,
                EnableIpLockout = true,
                EnableUserLockout = false
            },
            
            // Global system-wide limit
            new RateLimitRule
            {
                Name = "GLOBAL_SYSTEM",
                Context = "GLOBAL",
                MaxRequests = 10000,
                TimeWindow = TimeSpan.FromHours(1),
                LockoutDuration = TimeSpan.FromMinutes(10),
                Algorithm = RateLimitAlgorithm.TokenBucket,
                RefillRate = 167, // 10000/60 minutes
                EnableIpLockout = false,
                EnableUserLockout = false,
                Priority = 10
            },
            
            // Legacy login context for backward compatibility
            new RateLimitRule
            {
                Name = "Login_Legacy",
                Context = "login",
                MaxRequests = 5,
                TimeWindow = TimeSpan.FromMinutes(15),
                LockoutDuration = TimeSpan.FromMinutes(15),
                Algorithm = RateLimitAlgorithm.SlidingWindow,
                EnableIpLockout = true,
                EnableUserLockout = true
            }
        };
    }

    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            // Remove expired dynamic rules
            var expiredRules = _dynamicRules.Where(kvp => 
                kvp.Value.ExpiresAt.HasValue && kvp.Value.ExpiresAt < _dateTimeProvider.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredRules)
            {
                _dynamicRules.TryRemove(key, out _);
            }

            if (expiredRules.Any())
            {
                _logger.LogDebug("Cleaned up {Count} expired dynamic rate limiting rules", expiredRules.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during rate limiting cleanup");
        }
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }

    #endregion
}

#region Supporting Data Structures

// RateLimitRequest is now defined in BizConnect.Services.Security.Models.SecurityModels

public class AdvancedRateLimitResult
{
    public RateLimitRequest Request { get; set; } = new();
    public List<RateLimitCheck> Checks { get; set; } = new();
    public ThreatScore? ThreatScore { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime CheckTime { get; set; }
    public string? Error { get; set; }
}

public class RateLimitCheck
{
    public RateLimitRule Rule { get; set; } = new();
    public int CurrentCount { get; set; }
    public bool IsBlocked { get; set; }
    public DateTime? BlockedUntil { get; set; }
    public string? Message { get; set; }
    public DateTime CheckTime { get; set; }
    public string? Error { get; set; }
}

public class RateLimitRule
{
    public string Name { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public int MaxRequests { get; set; }
    public TimeSpan TimeWindow { get; set; }
    public TimeSpan LockoutDuration { get; set; }
    public RateLimitAlgorithm Algorithm { get; set; }
    public int Priority { get; set; }
    public bool EnableIpLockout { get; set; } = true;
    public bool EnableUserLockout { get; set; } = true;
    public int? RefillRate { get; set; } // For token bucket
    public DateTime? ExpiresAt { get; set; } // For dynamic rules
}

public enum RateLimitAlgorithm
{
    SlidingWindow,
    TokenBucket,
    FixedWindow
}

public class ThreatScore
{
    public string IpAddress { get; set; } = string.Empty;
    public double Score { get; set; }
    public double FailureRateScore { get; set; }
    public double VelocityScore { get; set; }
    public double PatternScore { get; set; }
    public double ReputationScore { get; set; }
    public double BehaviorScore { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class TokenBucket
{
    public int Capacity { get; set; }
    public int Tokens { get; set; }
    public DateTime LastRefill { get; set; }
    public int RefillRate { get; set; }
}

public class GlobalStatistics
{
    public int TotalRequests { get; set; }
    public int BlockedRequests { get; set; }
    public int FailedAttempts { get; set; }
    public HashSet<string> BlockedIps { get; set; } = new();
    public HashSet<string> BlockedUsers { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class AdvancedRateLimitingConfiguration
{
    public List<RateLimitRule> Rules { get; set; } = new();
    public RateLimitRule? DefaultRule { get; set; }
    public double ThreatScoreThreshold { get; set; } = 70;
    public double AdaptiveThreshold { get; set; } = 50;
    public double LoggingThreshold { get; set; } = 30;
}

public class AdvancedRateLimitingStatistics
{
    public DateTime GeneratedAt { get; set; }
    public int TotalRequests { get; set; }
    public int BlockedRequests { get; set; }
    public int FailedAttempts { get; set; }
    public int BlockedIpAddresses { get; set; }
    public int BlockedUsers { get; set; }
    public Dictionary<string, double> RuleEffectiveness { get; set; } = new();
    public List<ThreatIpInfo> TopThreatIps { get; set; } = new();
    public string? Error { get; set; }
}

public class ThreatIpInfo
{
    public string IpAddress { get; set; } = string.Empty;
    public double ThreatScore { get; set; }
    public int RequestCount { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime LastActivity { get; set; }
}

#endregion