using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Services.Caching;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Security.Models;
using BizConnect.Services.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Security;

/// <summary>
/// Enhanced security audit service with structured logging, compliance reporting,
/// and comprehensive audit trail management.
/// </summary>
public class EnhancedSecurityAuditService : IEnhancedSecurityAuditService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<EnhancedSecurityAuditService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;
    
    // Cache key prefixes
    private const string AuditEntryPrefix = "SecurityAudit:Entry";
    private const string AuditIndexPrefix = "SecurityAudit:Index";
    private const string ComplianceReportPrefix = "SecurityAudit:ComplianceReport";
    private const string AuditStatisticsPrefix = "SecurityAudit:Statistics";
    private const string DashboardDataPrefix = "SecurityAudit:Dashboard";
    
    // Configuration
    private readonly SecurityAuditConfiguration _config;
    private readonly Timer _cleanupTimer;
    private readonly Timer _reportGenerationTimer;
    
    public EnhancedSecurityAuditService(
        ICacheService cacheService,
        ILogger<EnhancedSecurityAuditService> logger,
        IConfiguration configuration,
        IDateTimeProvider dateTimeProvider)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        
        _config = LoadConfiguration();
        
        // Initialize timers
        _cleanupTimer = new Timer(PerformCleanup, null, 
            TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        _reportGenerationTimer = new Timer(GenerateScheduledReports, null, 
            TimeSpan.FromHours(24), TimeSpan.FromHours(24));
            
        _logger.LogInformation("Enhanced security audit service initialized with {RetentionDays} days retention",
            _config.RetentionDays);
    }
    
    #region IEnhancedSecurityAuditService Implementation
    
    public async Task LogSecurityEventAsync(string category, string action, object details)
    {
        try
        {
            var auditEntry = new SecurityAuditEntry
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = _dateTimeProvider.UtcNow,
                Category = category,
                Action = action,
                Details = JsonSerializer.Serialize(details),
                Severity = DetermineSeverity(category, action),
                IpAddress = ExtractIpAddress(details),
                Username = ExtractUsername(details),
                UserAgent = ExtractUserAgent(details),
                SessionId = ExtractSessionId(details),
                CorrelationId = ExtractCorrelationId(details)
            };
            
            // Store the audit entry
            await StoreAuditEntryAsync(auditEntry);
            
            // Update indices for efficient querying
            await UpdateAuditIndicesAsync(auditEntry);
            
            // Check for compliance violations
            await CheckComplianceViolationsAsync(auditEntry);
            
            // Log to structured logger
            LogStructuredEvent(auditEntry);
            
            _logger.LogDebug("Security audit entry created: {Category}.{Action} for {Username} from {IpAddress}",
                category, action, auditEntry.Username ?? "Anonymous", auditEntry.IpAddress ?? "Unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging security event: {Category}.{Action}", category, action);
        }
    }
    
    public async Task<List<SecurityAuditEntry>> GetAuditTrailAsync(AuditFilter filter)
    {
        try
        {
            var entries = await RetrieveAuditEntriesAsync(filter);
            
            // Apply additional filtering
            if (!string.IsNullOrEmpty(filter.Username))
            {
                entries = entries.Where(e => e.Username?.Contains(filter.Username, StringComparison.OrdinalIgnoreCase) == true).ToList();
            }
            
            if (!string.IsNullOrEmpty(filter.IpAddress))
            {
                entries = entries.Where(e => e.IpAddress == filter.IpAddress).ToList();
            }
            
            if (filter.MinSeverity.HasValue)
            {
                entries = entries.Where(e => e.Severity >= filter.MinSeverity.Value).ToList();
            }
            
            // Sort and paginate
            entries = entries.OrderByDescending(e => e.Timestamp).ToList();
            
            if (filter.Skip > 0)
            {
                entries = entries.Skip(filter.Skip).ToList();
            }
            
            if (filter.Take > 0)
            {
                entries = entries.Take(filter.Take).ToList();
            }
            
            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit trail");
            return new List<SecurityAuditEntry>();
        }
    }
    
    public async Task<SecurityDashboardData> GetSecurityDashboardDataAsync()
    {
        try
        {
            var cacheKey = $"{DashboardDataPrefix}:current";
            var cachedData = await _cacheService.GetAsync<SecurityDashboardData>(cacheKey);
            
            if (cachedData != null && cachedData.GeneratedAt > _dateTimeProvider.UtcNow.AddMinutes(-5))
            {
                return cachedData;
            }
            
            var dashboardData = await GenerateDashboardDataAsync();
            await _cacheService.SetAsync(dashboardData, cacheKey, TimeSpan.FromMinutes(5));
            
            return dashboardData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating security dashboard data");
            return new SecurityDashboardData { GeneratedAt = _dateTimeProvider.UtcNow };
        }
    }
    
    public async Task PurgeOldAuditEntriesAsync(int retentionDays)
    {
        try
        {
            var cutoffDate = _dateTimeProvider.UtcNow.AddDays(-retentionDays);
            var purgedCount = 0;
            
            // Get entries older than retention period
            var filter = new AuditFilter
            {
                EndDate = cutoffDate,
                Take = 1000 // Process in batches
            };
            
            List<SecurityAuditEntry> entriesToPurge;
            do
            {
                entriesToPurge = await GetAuditTrailAsync(filter);
                
                foreach (var entry in entriesToPurge)
                {
                    var entryKey = $"{AuditEntryPrefix}:{entry.Id}";
                    await _cacheService.RemoveAsync(entryKey);
                    purgedCount++;
                }
                
            } while (entriesToPurge.Count == filter.Take);
            
            // Update statistics
            await UpdatePurgeStatisticsAsync(purgedCount, cutoffDate);
            
            _logger.LogInformation("Purged {Count} audit entries older than {CutoffDate}", 
                purgedCount, cutoffDate.ToString("yyyy-MM-dd"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging old audit entries");
        }
    }
    
    public async Task GenerateComplianceReportAsync(DateTime from, DateTime to)
    {
        try
        {
            var reportId = Guid.NewGuid().ToString();
            var report = new ComplianceReport
            {
                Id = reportId,
                GeneratedAt = _dateTimeProvider.UtcNow,
                PeriodFrom = from,
                PeriodTo = to,
                Status = ComplianceReportStatus.Generating
            };
            
            // Store initial report
            var reportKey = $"{ComplianceReportPrefix}:{reportId}";
            await _cacheService.SetAsync(report, reportKey, TimeSpan.FromDays(30));
            
            // Generate report asynchronously
            _ = Task.Run(async () => await GenerateComplianceReportInternalAsync(report));
            
            _logger.LogInformation("Started compliance report generation for period {From} to {To}", 
                from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating compliance report generation");
        }
    }
    
    // Critical missing methods implementation
    public async Task<Result<SecurityDashboardData>> GetSecurityDashboardAsync(TimeSpan period, CancellationToken cancellationToken = default)
    {
        try
        {
            var endTime = _dateTimeProvider.UtcNow;
            var startTime = endTime.Subtract(period);
            
            var cacheKey = $"{DashboardDataPrefix}:period:{period.TotalHours}h";
            var cachedResult = await _cacheService.GetAsync<SecurityDashboardData>(cacheKey);
            
            if (cachedResult != null && cachedResult.GeneratedAt > endTime.AddMinutes(-2))
            {
                return Result<SecurityDashboardData>.Success(cachedResult);
            }
            
            var filter = new AuditFilter
            {
                StartDate = startTime,
                EndDate = endTime,
                Take = 10000
            };
            
            var auditEntries = await GetAuditTrailAsync(filter);
            
            var dashboardData = new SecurityDashboardData
            {
                GeneratedAt = endTime,
                TimeRange = period,
                TotalSecurityEvents = auditEntries.Count,
                ThreatCount = auditEntries.Count(e => e.Severity >= AuditSeverity.High),
                BlockedIPs = auditEntries
                    .Where(e => e.Category == "IpBlock" || e.Action.Contains("Block"))
                    .Select(e => e.IpAddress)
                    .Where(ip => !string.IsNullOrEmpty(ip))
                    .Distinct()
                    .Count(),
                CurrentThreatLevel = DetermineThreatLevel(auditEntries),
                RecentAlerts = GenerateRecentAlerts(auditEntries.Take(50).ToList()),
                LastUpdated = endTime,
                Metrics = GenerateSecurityMetrics(auditEntries),
                ActiveAlerts = GenerateActiveAlerts(auditEntries),
                TopThreatIps = GenerateTopThreatIps(auditEntries),
                RecentEvents = auditEntries.Take(100).Select(MapToEventSummary).ToList(),
                EventTypeCounts = auditEntries.GroupBy(e => e.Category).ToDictionary(g => g.Key, g => g.Count()),
                ThreatTrendData = GenerateThreatTrendData(auditEntries),
                ActivePatterns = new List<SuspiciousPattern>(),
                SecurityLevel = new SecurityLevelStatus
                {
                    CurrentLevel = DetermineThreatLevel(auditEntries),
                    LevelSetAt = endTime,
                    Reason = "Automated assessment based on recent security events"
                },
                RecentResponses = new List<ResponseAction>()
            };
            
            await _cacheService.SetAsync(dashboardData, cacheKey, TimeSpan.FromMinutes(2));
            
            return Result<SecurityDashboardData>.Success(dashboardData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating security dashboard for period {Period}", period);
            return Result<SecurityDashboardData>.Failure($"Failed to generate security dashboard: {ex.Message}");
        }
    }
    
    public async Task LogUserLockoutAsync(string userId, string reason, TimeSpan duration)
    {
        try
        {
            var lockoutDetails = new
            {
                UserId = userId,
                Reason = reason,
                Duration = duration.ToString(),
                LockoutEnd = _dateTimeProvider.UtcNow.Add(duration),
                IsAutomatic = true
            };
            
            await LogSecurityEventAsync("UserLockout", "LockoutTriggered", lockoutDetails);
            
            _logger.LogWarning("User {UserId} locked out for {Duration} due to: {Reason}", 
                userId, duration, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging user lockout for user {UserId}", userId);
        }
    }
    
    public async Task LogIpBlockAsync(string ipAddress, string reason, TimeSpan duration)
    {
        try
        {
            var blockDetails = new
            {
                IpAddress = ipAddress,
                Reason = reason,
                Duration = duration.ToString(),
                BlockEnd = _dateTimeProvider.UtcNow.Add(duration),
                IsAutomatic = true,
                BlockType = duration > TimeSpan.FromHours(24) ? "Extended" : "Temporary"
            };
            
            await LogSecurityEventAsync("IpBlock", "BlockTriggered", blockDetails);
            
            _logger.LogWarning("IP address {IpAddress} blocked for {Duration} due to: {Reason}", 
                ipAddress, duration, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging IP block for {IpAddress}", ipAddress);
        }
    }
    
    #endregion
    
    #region Specialized Audit Methods
    
    public async Task LogLoginAttemptAsync(string ipAddress, string username, bool success, string details = "")
    {
        await LogSecurityEventAsync("Authentication", success ? "LoginSuccess" : "LoginFailure", new
        {
            IpAddress = ipAddress,
            Username = username,
            Success = success,
            Details = details,
            Timestamp = _dateTimeProvider.UtcNow
        });
    }
    
    public async Task LogAccountLockoutAsync(string identifier, int attemptCount)
    {
        await LogSecurityEventAsync("Security", "AccountLockout", new
        {
            Identifier = identifier,
            AttemptCount = attemptCount,
            Timestamp = _dateTimeProvider.UtcNow,
            Severity = "High"
        });
    }
    
    public async Task LogIpBlockAsync(string ipAddress, string reason)
    {
        await LogSecurityEventAsync("Security", "IpBlock", new
        {
            IpAddress = ipAddress,
            Reason = reason,
            Timestamp = _dateTimeProvider.UtcNow,
            Severity = "High"
        });
    }
    
    public async Task LogOtacGenerationAsync(string phoneNumber, string ipAddress)
    {
        await LogSecurityEventAsync("OTAC", "OtacGeneration", new
        {
            PhoneNumber = MaskPhoneNumber(phoneNumber),
            IpAddress = ipAddress,
            Timestamp = _dateTimeProvider.UtcNow
        });
    }
    
    public async Task LogOtacValidationAsync(string phoneNumber, string ipAddress, bool success, int attemptCount = 1)
    {
        await LogSecurityEventAsync("OTAC", success ? "OtacValidationSuccess" : "OtacValidationFailure", new
        {
            PhoneNumber = MaskPhoneNumber(phoneNumber),
            IpAddress = ipAddress,
            Success = success,
            AttemptCount = attemptCount,
            Timestamp = _dateTimeProvider.UtcNow,
            Severity = success ? "Info" : "Medium"
        });
    }
    
    public async Task LogThreatResponseAsync(string ipAddress, string threatLevel, int actionCount)
    {
        await LogSecurityEventAsync("ThreatResponse", "AutomatedResponse", new
        {
            IpAddress = ipAddress,
            ThreatLevel = threatLevel,
            ActionCount = actionCount,
            Timestamp = _dateTimeProvider.UtcNow,
            Severity = "High"
        });
    }
    
    public async Task LogDataAccessAsync(string username, string resource, string action, bool authorized)
    {
        await LogSecurityEventAsync("DataAccess", authorized ? "AuthorizedAccess" : "UnauthorizedAccess", new
        {
            Username = username,
            Resource = resource,
            Action = action,
            Authorized = authorized,
            Timestamp = _dateTimeProvider.UtcNow,
            Severity = authorized ? "Info" : "High"
        });
    }
    
    public async Task LogConfigurationChangeAsync(string username, string setting, string oldValue, string newValue)
    {
        await LogSecurityEventAsync("Configuration", "SettingChange", new
        {
            Username = username,
            Setting = setting,
            OldValue = MaskSensitiveValue(setting, oldValue),
            NewValue = MaskSensitiveValue(setting, newValue),
            Timestamp = _dateTimeProvider.UtcNow,
            Severity = "Medium"
        });
    }
    
    #endregion
    
    #region Private Helper Methods
    
    private async Task StoreAuditEntryAsync(SecurityAuditEntry auditEntry)
    {
        var entryKey = $"{AuditEntryPrefix}:{auditEntry.Id}";
        var retention = TimeSpan.FromDays(_config.RetentionDays);
        await _cacheService.SetAsync(auditEntry, entryKey, retention);
    }
    
    private async Task UpdateAuditIndicesAsync(SecurityAuditEntry auditEntry)
    {
        var tasks = new List<Task>();
        
        // Time-based index
        var timeKey = $"{AuditIndexPrefix}:ByTime:{auditEntry.Timestamp:yyyyMMddHH}";
        tasks.Add(AddToIndexAsync(timeKey, auditEntry.Id));
        
        // Category index
        var categoryKey = $"{AuditIndexPrefix}:ByCategory:{auditEntry.Category}";
        tasks.Add(AddToIndexAsync(categoryKey, auditEntry.Id));
        
        // User index
        if (!string.IsNullOrEmpty(auditEntry.Username))
        {
            var userKey = $"{AuditIndexPrefix}:ByUser:{auditEntry.Username}";
            tasks.Add(AddToIndexAsync(userKey, auditEntry.Id));
        }
        
        // IP index
        if (!string.IsNullOrEmpty(auditEntry.IpAddress))
        {
            var ipKey = $"{AuditIndexPrefix}:ByIp:{auditEntry.IpAddress}";
            tasks.Add(AddToIndexAsync(ipKey, auditEntry.Id));
        }
        
        // Severity index
        var severityKey = $"{AuditIndexPrefix}:BySeverity:{auditEntry.Severity}";
        tasks.Add(AddToIndexAsync(severityKey, auditEntry.Id));
        
        await Task.WhenAll(tasks);
    }
    
    private async Task AddToIndexAsync(string indexKey, string entryId)
    {
        var index = await _cacheService.GetAsync<List<string>>(indexKey) ?? new List<string>();
        index.Add(entryId);
        
        // Keep indices manageable by limiting size
        if (index.Count > 10000)
        {
            index = index.Skip(1000).ToList(); // Remove oldest 1000 entries
        }
        
        await _cacheService.SetAsync(index, indexKey, TimeSpan.FromDays(_config.RetentionDays));
    }
    
    private async Task<List<SecurityAuditEntry>> RetrieveAuditEntriesAsync(AuditFilter filter)
    {
        var entries = new List<SecurityAuditEntry>();
        var entryIds = new HashSet<string>();
        
        // Get entries based on filter criteria
        if (filter.StartDate.HasValue || filter.EndDate.HasValue)
        {
            var startDate = filter.StartDate ?? _dateTimeProvider.UtcNow.AddDays(-7);
            var endDate = filter.EndDate ?? _dateTimeProvider.UtcNow;
            
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddHours(1))
            {
                var timeKey = $"{AuditIndexPrefix}:ByTime:{date:yyyyMMddHH}";
                var timeEntries = await _cacheService.GetAsync<List<string>>(timeKey);
                if (timeEntries != null)
                {
                    foreach (var entryId in timeEntries)
                    {
                        entryIds.Add(entryId);
                    }
                }
            }
        }
        
        if (!string.IsNullOrEmpty(filter.Category))
        {
            var categoryKey = $"{AuditIndexPrefix}:ByCategory:{filter.Category}";
            var categoryEntries = await _cacheService.GetAsync<List<string>>(categoryKey);
            if (categoryEntries != null)
            {
                if (entryIds.Any())
                {
                    entryIds.IntersectWith(categoryEntries);
                }
                else
                {
                    foreach (var entryId in categoryEntries)
                    {
                        entryIds.Add(entryId);
                    }
                }
            }
        }
        
        // If no specific filters, get recent entries
        if (!entryIds.Any())
        {
            var recentTimeKey = $"{AuditIndexPrefix}:ByTime:{_dateTimeProvider.UtcNow:yyyyMMddHH}";
            var recentEntries = await _cacheService.GetAsync<List<string>>(recentTimeKey);
            if (recentEntries != null)
            {
                foreach (var entryId in recentEntries)
                {
                    entryIds.Add(entryId);
                }
            }
        }
        
        // Retrieve actual entries
        foreach (var entryId in entryIds)
        {
            var entryKey = $"{AuditEntryPrefix}:{entryId}";
            var entry = await _cacheService.GetAsync<SecurityAuditEntry>(entryKey);
            if (entry != null)
            {
                entries.Add(entry);
            }
        }
        
        return entries;
    }
    
    private async Task CheckComplianceViolationsAsync(SecurityAuditEntry auditEntry)
    {
        // Check for compliance violations
        var violations = new List<string>();
        
        // Example: Multiple failed login attempts
        if (auditEntry.Category == "Authentication" && auditEntry.Action == "LoginFailure")
        {
            var recentFailures = await GetRecentFailedLogins(auditEntry.IpAddress, TimeSpan.FromMinutes(15));
            if (recentFailures >= 5)
            {
                violations.Add("Excessive failed login attempts detected");
            }
        }
        
        // Example: Off-hours administrative activity
        if (auditEntry.Category == "Configuration" || auditEntry.Category == "DataAccess")
        {
            var hour = auditEntry.Timestamp.Hour;
            if (hour < 6 || hour > 22) // Outside business hours
            {
                violations.Add("Administrative activity outside business hours");
            }
        }
        
        // Log violations
        foreach (var violation in violations)
        {
            await LogSecurityEventAsync("Compliance", "ViolationDetected", new
            {
                Violation = violation,
                OriginalEntry = auditEntry.Id,
                Timestamp = _dateTimeProvider.UtcNow
            });
        }
    }
    
    private async Task<int> GetRecentFailedLogins(string? ipAddress, TimeSpan timeWindow)
    {
        if (string.IsNullOrEmpty(ipAddress)) return 0;
        
        var cutoffTime = _dateTimeProvider.UtcNow - timeWindow;
        var filter = new AuditFilter
        {
            Category = "Authentication",
            IpAddress = ipAddress,
            StartDate = cutoffTime
        };
        
        var entries = await GetAuditTrailAsync(filter);
        return entries.Count(e => e.Action == "LoginFailure");
    }
    
    private void LogStructuredEvent(SecurityAuditEntry auditEntry)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["AuditId"] = auditEntry.Id,
            ["Category"] = auditEntry.Category,
            ["Action"] = auditEntry.Action,
            ["IpAddress"] = auditEntry.IpAddress ?? "Unknown",
            ["Username"] = auditEntry.Username ?? "Anonymous",
            ["Severity"] = auditEntry.Severity.ToString()
        });
        
        var logLevel = auditEntry.Severity switch
        {
            AuditSeverity.Critical => LogLevel.Critical,
            AuditSeverity.High => LogLevel.Error,
            AuditSeverity.Medium => LogLevel.Warning,
            AuditSeverity.Low => LogLevel.Information,
            _ => LogLevel.Information
        };
        
        _logger.Log(logLevel, "Security audit: {Category}.{Action} - {Details}",
            auditEntry.Category, auditEntry.Action, auditEntry.Details);
    }
    
    private async Task<SecurityDashboardData> GenerateDashboardDataAsync()
    {
        var dashboardData = new SecurityDashboardData
        {
            GeneratedAt = _dateTimeProvider.UtcNow
        };
        
        // Get recent entries for analysis
        var filter = new AuditFilter
        {
            StartDate = _dateTimeProvider.UtcNow.AddDays(-1),
            Take = 10000
        };
        
        var recentEntries = await GetAuditTrailAsync(filter);
        
        // Calculate metrics
        dashboardData.TotalSecurityEvents = recentEntries.Count;
        dashboardData.ThreatCount = recentEntries.Count(e => e.Severity >= AuditSeverity.Medium);
        dashboardData.BlockedIPs = recentEntries.Where(e => e.Action == "IpBlock").Select(e => e.IpAddress).Where(ip => !string.IsNullOrEmpty(ip)).Distinct().Count();
        dashboardData.ComplianceViolations = recentEntries.Where(e => e.Category == "Compliance")
            .Select(e => new ComplianceViolation
            {
                ViolationType = e.Action,
                Description = e.Details ?? "Compliance violation detected",
                Severity = MapAuditSeverityToThreatSeverity(e.Severity),
                DetectedAt = e.Timestamp,
                IpAddress = e.IpAddress,
                Username = e.Username,
                IsResolved = false
            })
            .ToList();
        
        // Category breakdown
        dashboardData.EventsByCategory = recentEntries.GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Count());
        
        // Severity breakdown
        dashboardData.EventsBySeverity = recentEntries.GroupBy(e => e.Severity)
            .ToDictionary(g => MapAuditSeverityToThreatSeverity(g.Key), g => g.Count());
        
        // Hourly trend
        dashboardData.HourlyTrend = recentEntries.GroupBy(e => e.Timestamp.Hour)
            .ToDictionary(g => g.Key, g => g.Count());
        
        // Top IPs by activity
        dashboardData.TopActiveIps = recentEntries.Where(e => !string.IsNullOrEmpty(e.IpAddress))
            .GroupBy(e => e.IpAddress)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new TopActiveIp
            {
                IpAddress = g.Key!,
                RequestCount = g.Count(),
                ThreatCount = g.Count(e => e.Severity >= AuditSeverity.High),
                LastActivity = g.Max(e => e.Timestamp),
                EventTypes = g.Select(e => e.Category).Distinct().ToList(),
                HighestThreatLevel = MapAuditSeverityToThreatSeverity(g.Max(e => e.Severity)),
                IsBlocked = g.Any(e => e.Action.Contains("Block"))
            })
            .ToList();
        
        // Set additional dashboard properties
        dashboardData.CurrentThreatLevel = DetermineThreatLevel(recentEntries);
        dashboardData.LastUpdated = _dateTimeProvider.UtcNow;
        dashboardData.EventTypeCounts = dashboardData.EventsByCategory;
        dashboardData.BlockedIpAddresses = recentEntries.Where(e => e.Action == "IpBlock")
            .Select(e => e.IpAddress)
            .Where(ip => !string.IsNullOrEmpty(ip))
            .Distinct()
            .ToList();
        
        return dashboardData;
    }
    
    private async Task GenerateComplianceReportInternalAsync(ComplianceReport report)
    {
        try
        {
            report.Status = ComplianceReportStatus.Generating;
            
            // Get audit entries for the specified period
            var filter = new AuditFilter
            {
                StartDate = report.PeriodFrom,
                EndDate = report.PeriodTo,
                Take = 100000 // Large batch for comprehensive report
            };
            
            var entries = await GetAuditTrailAsync(filter);
            
            // Generate report sections
            report.Summary = GenerateReportSummary(entries);
            report.SecurityEvents = GenerateSecurityEventsSection(entries);
            report.ComplianceViolations = GenerateComplianceViolationsSection(entries);
            report.AccessPatterns = GenerateAccessPatternsSection(entries);
            report.Recommendations = GenerateRecommendationsSection(entries);
            
            report.Status = ComplianceReportStatus.Completed;
            report.CompletedAt = _dateTimeProvider.UtcNow;
            
            // Store completed report
            var reportKey = $"{ComplianceReportPrefix}:{report.Id}";
            await _cacheService.SetAsync(report, reportKey, TimeSpan.FromDays(30));
            
            _logger.LogInformation("Compliance report {ReportId} generated successfully", report.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report {ReportId}", report.Id);
            report.Status = ComplianceReportStatus.Failed;
            report.ErrorMessage = ex.Message;
        }
    }
    
    private ReportSummary GenerateReportSummary(List<SecurityAuditEntry> entries)
    {
        return new ReportSummary
        {
            TotalEvents = entries.Count,
            SecurityEvents = entries.Count(e => e.Severity >= AuditSeverity.Medium),
            CriticalEvents = entries.Count(e => e.Severity == AuditSeverity.Critical),
            ComplianceViolations = entries.Count(e => e.Category == "Compliance"),
            UniqueUsers = entries.Where(e => !string.IsNullOrEmpty(e.Username)).Select(e => e.Username).Distinct().Count(),
            UniqueIpAddresses = entries.Where(e => !string.IsNullOrEmpty(e.IpAddress)).Select(e => e.IpAddress).Distinct().Count()
        };
    }
    
    private List<SecurityEventSummary> GenerateSecurityEventsSection(List<SecurityAuditEntry> entries)
    {
        return entries.Where(e => e.Severity >= AuditSeverity.Medium)
            .OrderByDescending(e => e.Timestamp)
            .Take(100)
            .Select(e => new SecurityEventSummary
            {
                Id = e.Id,
                Timestamp = e.Timestamp,
                Category = e.Category,
                Action = e.Action,
                EventType = e.Category,
                Severity = MapAuditSeverityToThreatSeverity(e.Severity),
                IpAddress = e.IpAddress ?? "",
                Username = e.Username ?? "",
                Description = TruncateDetails(e.Details, 200),
                Details = TruncateDetails(e.Details, 200),
                IsResolved = false
            })
            .ToList();
    }
    
    private List<ComplianceViolationSummary> GenerateComplianceViolationsSection(List<SecurityAuditEntry> entries)
    {
        return entries.Where(e => e.Category == "Compliance")
            .GroupBy(e => e.Action)
            .Select(g => new ComplianceViolationSummary
            {
                ViolationType = g.Key,
                Count = g.Count(),
                LastOccurrence = g.Max(e => e.Timestamp),
                AffectedUsers = g.Where(e => !string.IsNullOrEmpty(e.Username)).Select(e => e.Username).Distinct().Count(),
                AffectedIps = g.Where(e => !string.IsNullOrEmpty(e.IpAddress)).Select(e => e.IpAddress).Distinct().Count()
            })
            .OrderByDescending(v => v.Count)
            .ToList();
    }
    
    private List<AccessPatternSummary> GenerateAccessPatternsSection(List<SecurityAuditEntry> entries)
    {
        return entries.Where(e => e.Category == "DataAccess")
            .GroupBy(e => e.Username)
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .Select(g => new AccessPatternSummary
            {
                Username = g.Key!,
                TotalAccesses = g.Count(),
                UnauthorizedAttempts = g.Count(e => e.Action == "UnauthorizedAccess"),
                ResourcesAccessed = g.Where(e => e.Details != null).Select(e => ExtractResourceFromDetails(e.Details!)).Distinct().Count(),
                LastAccess = g.Max(e => e.Timestamp),
                OffHoursAccesses = g.Count(e => e.Timestamp.Hour < 6 || e.Timestamp.Hour > 22)
            })
            .OrderByDescending(p => p.UnauthorizedAttempts)
            .ThenByDescending(p => p.TotalAccesses)
            .Take(50)
            .ToList();
    }
    
    private List<string> GenerateRecommendationsSection(List<SecurityAuditEntry> entries)
    {
        var recommendations = new List<string>();
        
        var failedLogins = entries.Count(e => e.Category == "Authentication" && e.Action == "LoginFailure");
        var totalLogins = entries.Count(e => e.Category == "Authentication");
        
        if (totalLogins > 0 && (double)failedLogins / totalLogins > 0.1)
        {
            recommendations.Add("High failure rate detected in login attempts. Consider implementing additional authentication measures.");
        }
        
        var offHoursActivity = entries.Count(e => e.Timestamp.Hour < 6 || e.Timestamp.Hour > 22);
        if (offHoursActivity > entries.Count * 0.2)
        {
            recommendations.Add("Significant off-hours activity detected. Review access patterns and consider implementing time-based access controls.");
        }
        
        var complianceViolations = entries.Count(e => e.Category == "Compliance");
        if (complianceViolations > 0)
        {
            recommendations.Add($"Detected {complianceViolations} compliance violations. Review and address these issues promptly.");
        }
        
        if (!recommendations.Any())
        {
            recommendations.Add("No immediate security concerns identified in the audit period.");
        }
        
        return recommendations;
    }
    
    // Utility methods
    private AuditSeverity DetermineSeverity(string category, string action)
    {
        return (category, action) switch
        {
            ("Security", "AccountLockout") => AuditSeverity.High,
            ("Security", "IpBlock") => AuditSeverity.High,
            ("ThreatResponse", _) => AuditSeverity.High,
            ("Authentication", "LoginFailure") => AuditSeverity.Medium,
            ("Compliance", _) => AuditSeverity.High,
            ("DataAccess", "UnauthorizedAccess") => AuditSeverity.High,
            ("Configuration", _) => AuditSeverity.Medium,
            _ => AuditSeverity.Low
        };
    }
    
    private string? ExtractIpAddress(object details)
    {
        if (details is JsonElement json && json.TryGetProperty("IpAddress", out var ipProp))
        {
            return ipProp.GetString();
        }
        return ExtractPropertyFromObject(details, "IpAddress");
    }
    
    private string? ExtractUsername(object details)
    {
        if (details is JsonElement json && json.TryGetProperty("Username", out var userProp))
        {
            return userProp.GetString();
        }
        return ExtractPropertyFromObject(details, "Username");
    }
    
    private string? ExtractUserAgent(object details)
    {
        return ExtractPropertyFromObject(details, "UserAgent");
    }
    
    private string? ExtractSessionId(object details)
    {
        return ExtractPropertyFromObject(details, "SessionId");
    }
    
    private string? ExtractCorrelationId(object details)
    {
        return ExtractPropertyFromObject(details, "CorrelationId");
    }
    
    private string? ExtractPropertyFromObject(object obj, string propertyName)
    {
        var json = JsonSerializer.Serialize(obj);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString();
        }
        return null;
    }
    
    private string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4) return phoneNumber;
        return phoneNumber.Substring(0, 3) + "****" + phoneNumber.Substring(phoneNumber.Length - 2);
    }
    
    private string MaskSensitiveValue(string setting, string value)
    {
        var sensitiveKeys = new[] { "password", "secret", "key", "token", "passphrase" };
        if (sensitiveKeys.Any(key => setting.Contains(key, StringComparison.OrdinalIgnoreCase)))
        {
            return "[MASKED]";
        }
        return value;
    }
    
    private string TruncateDetails(string? details, int maxLength)
    {
        if (string.IsNullOrEmpty(details)) return "";
        return details.Length <= maxLength ? details : details.Substring(0, maxLength) + "...";
    }
    
    private string ExtractResourceFromDetails(string details)
    {
        try
        {
            using var doc = JsonDocument.Parse(details);
            if (doc.RootElement.TryGetProperty("Resource", out var prop))
            {
                return prop.GetString() ?? "Unknown";
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return "Unknown";
    }
    
    private async Task UpdatePurgeStatisticsAsync(int purgedCount, DateTime cutoffDate)
    {
        var stats = new PurgeStatistics
        {
            PurgedCount = purgedCount,
            PurgeDate = _dateTimeProvider.UtcNow,
            CutoffDate = cutoffDate
        };
        
        var statsKey = $"{AuditStatisticsPrefix}:Purge:{_dateTimeProvider.UtcNow:yyyyMMdd}";
        await _cacheService.SetAsync(stats, statsKey, TimeSpan.FromDays(30));
    }
    
    private SecurityAuditConfiguration LoadConfiguration()
    {
        var config = new SecurityAuditConfiguration();
        _configuration.GetSection("SecurityAudit").Bind(config);
        
        // Set defaults
        if (config.RetentionDays == 0) config.RetentionDays = 90;
        if (config.BatchSize == 0) config.BatchSize = 1000;
        if (!config.EnableStructuredLogging.HasValue) config.EnableStructuredLogging = true;
        
        return config;
    }
    
    private void PerformCleanup(object? state)
    {
        Task.Run(async () =>
        {
            try
            {
                await PurgeOldAuditEntriesAsync(_config.RetentionDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audit cleanup");
            }
        });
    }
    
    private void GenerateScheduledReports(object? state)
    {
        Task.Run(async () =>
        {
            try
            {
                // Generate daily compliance report
                var yesterday = _dateTimeProvider.UtcNow.AddDays(-1).Date;
                await GenerateComplianceReportAsync(yesterday, yesterday.AddDays(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating scheduled reports");
            }
        });
    }
    
    #region Helper Methods for Dashboard Data Generation
    
    private ThreatLevel DetermineThreatLevel(List<SecurityAuditEntry> auditEntries)
    {
        if (!auditEntries.Any()) return ThreatLevel.Low;
        
        var recentEntries = auditEntries.Where(e => e.Timestamp > _dateTimeProvider.UtcNow.AddHours(-1)).ToList();
        var criticalCount = recentEntries.Count(e => e.Severity == AuditSeverity.Critical);
        var highCount = recentEntries.Count(e => e.Severity == AuditSeverity.High);
        
        if (criticalCount > 10) return ThreatLevel.Critical;
        if (criticalCount > 5 || highCount > 20) return ThreatLevel.High;
        if (criticalCount > 0 || highCount > 10) return ThreatLevel.Medium;
        
        return ThreatLevel.Low;
    }
    
    private List<SecurityAlert> GenerateRecentAlerts(List<SecurityAuditEntry> auditEntries)
    {
        return auditEntries
            .Where(e => e.Severity >= AuditSeverity.High)
            .Take(10)
            .Select(e => new SecurityAlert
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"{e.Category}: {e.Action}",
                Description = e.Details ?? "Security event detected",
                Severity = MapAuditSeverityToThreatSeverity(e.Severity),
                CreatedAt = e.Timestamp,
                Status = SecurityAlertStatus.New,
                IpAddress = e.IpAddress ?? "Unknown",
                Username = e.Username,
                Priority = MapSeverityToPriority(e.Severity)
            })
            .ToList();
    }
    
    private SecurityMetrics GenerateSecurityMetrics(List<SecurityAuditEntry> auditEntries)
    {
        return new SecurityMetrics
        {
            TotalEvents = auditEntries.Count,
            ThreatEvents = auditEntries.Count(e => e.Severity >= AuditSeverity.High),
            BlockedAttempts = auditEntries.Count(e => e.Action.Contains("Block") || e.Action.Contains("Deny")),
            ActiveAlerts = auditEntries.Count(e => e.Severity == AuditSeverity.Critical && e.Timestamp > _dateTimeProvider.UtcNow.AddHours(-24)),
            WatchlistedIps = auditEntries.Select(e => e.IpAddress).Where(ip => !string.IsNullOrEmpty(ip)).Distinct().Count(),
            ThreatDetectionRate = CalculateThreatDetectionRate(auditEntries),
            FalsePositiveRate = 0.05, // Placeholder value
            AutomatedResponses = auditEntries.Count(e => e.Category == "AutomatedResponse"),
            AverageResponseTime = 30.0, // Placeholder value in seconds
            ComplianceViolations = auditEntries.Count(e => e.Category == "Compliance"),
            HealthScore = new SecurityHealthScore
            {
                OverallScore = CalculateOverallHealthScore(auditEntries),
                ThreatDetectionScore = 0.85,
                ResponseEffectivenessScore = 0.80,
                ComplianceScore = 0.95,
                SystemResilienceScore = 0.88,
                CalculatedAt = _dateTimeProvider.UtcNow
            }
        };
    }
    
    private List<SecurityAlert> GenerateActiveAlerts(List<SecurityAuditEntry> auditEntries)
    {
        return auditEntries
            .Where(e => e.Severity == AuditSeverity.Critical && e.Timestamp > _dateTimeProvider.UtcNow.AddHours(-24))
            .Take(20)
            .Select(e => new SecurityAlert
            {
                Id = Guid.NewGuid().ToString(),
                Title = $"Critical Alert: {e.Category}",
                Description = e.Details ?? "Critical security event",
                Severity = ThreatSeverity.Critical,
                CreatedAt = e.Timestamp,
                Status = SecurityAlertStatus.New,
                IpAddress = e.IpAddress ?? "Unknown",
                Username = e.Username,
                Priority = AlertPriority.Urgent
            })
            .ToList();
    }
    
    private List<TopThreatIp> GenerateTopThreatIps(List<SecurityAuditEntry> auditEntries)
    {
        return auditEntries
            .Where(e => !string.IsNullOrEmpty(e.IpAddress) && e.Severity >= AuditSeverity.Medium)
            .GroupBy(e => e.IpAddress)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new TopThreatIp
            {
                IpAddress = g.Key!,
                ThreatScore = CalculateIpThreatScore(g.ToList()),
                EventCount = g.Count(),
                FailedAttempts = g.Count(e => e.Action.Contains("Fail") || e.Action.Contains("Deny")),
                LastActivity = g.Max(e => e.Timestamp),
                ThreatTypes = g.Select(e => e.Category).Distinct().ToList(),
                HighestSeverity = MapAuditSeverityToThreatSeverity(g.Max(e => e.Severity)),
                IsBlocked = g.Any(e => e.Action.Contains("Block")),
                IsWatchlisted = true
            })
            .ToList();
    }
    
    private Models.SecurityEventSummary MapToEventSummary(SecurityAuditEntry entry)
    {
        return new Models.SecurityEventSummary
        {
            Id = entry.Id,
            Timestamp = entry.Timestamp,
            EventType = entry.Category,
            IpAddress = entry.IpAddress ?? "Unknown",
            Severity = MapAuditSeverityToThreatSeverity(entry.Severity),
            Description = entry.Details ?? "",
            Username = entry.Username,
            IsResolved = false,
            Category = entry.Category
        };
    }
    
    private Dictionary<string, int> GenerateThreatTrendData(List<SecurityAuditEntry> auditEntries)
    {
        var result = new Dictionary<string, int>();
        var now = _dateTimeProvider.UtcNow;
        
        for (int i = 0; i < 7; i++)
        {
            var day = now.AddDays(-i).ToString("yyyy-MM-dd");
            var dayStart = now.AddDays(-i).Date;
            var dayEnd = dayStart.AddDays(1);
            
            var count = auditEntries.Count(e => e.Timestamp >= dayStart && e.Timestamp < dayEnd && e.Severity >= AuditSeverity.High);
            result[day] = count;
        }
        
        return result;
    }
    
    private double CalculateThreatDetectionRate(List<SecurityAuditEntry> auditEntries)
    {
        if (!auditEntries.Any()) return 0.0;
        var totalEvents = auditEntries.Count;
        var threatEvents = auditEntries.Count(e => e.Severity >= AuditSeverity.High);
        return totalEvents > 0 ? (double)threatEvents / totalEvents : 0.0;
    }
    
    private double CalculateOverallHealthScore(List<SecurityAuditEntry> auditEntries)
    {
        if (!auditEntries.Any()) return 1.0;
        
        var recentCritical = auditEntries.Count(e => e.Severity == AuditSeverity.Critical && e.Timestamp > _dateTimeProvider.UtcNow.AddHours(-24));
        var recentHigh = auditEntries.Count(e => e.Severity == AuditSeverity.High && e.Timestamp > _dateTimeProvider.UtcNow.AddHours(-24));
        
        var score = 1.0;
        score -= recentCritical * 0.1;
        score -= recentHigh * 0.05;
        
        return Math.Max(0.0, Math.Min(1.0, score));
    }
    
    private double CalculateIpThreatScore(List<SecurityAuditEntry> entries)
    {
        if (!entries.Any()) return 0.0;
        
        var score = 0.0;
        score += entries.Count(e => e.Severity == AuditSeverity.Critical) * 0.4;
        score += entries.Count(e => e.Severity == AuditSeverity.High) * 0.3;
        score += entries.Count(e => e.Severity == AuditSeverity.Medium) * 0.2;
        score += entries.Count(e => e.Action.Contains("Fail")) * 0.1;
        
        return Math.Min(1.0, score / entries.Count);
    }
    
    private ThreatSeverity MapAuditSeverityToThreatSeverity(AuditSeverity severity)
    {
        return severity switch
        {
            AuditSeverity.Low => ThreatSeverity.Low,
            AuditSeverity.Medium => ThreatSeverity.Medium,
            AuditSeverity.High => ThreatSeverity.High,
            AuditSeverity.Critical => ThreatSeverity.Critical,
            _ => ThreatSeverity.Low
        };
    }
    
    private AlertPriority MapSeverityToPriority(AuditSeverity severity)
    {
        return severity switch
        {
            AuditSeverity.Critical => AlertPriority.Urgent,
            AuditSeverity.High => AlertPriority.High,
            AuditSeverity.Medium => AlertPriority.Medium,
            _ => AlertPriority.Low
        };
    }
    
    #endregion
    
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _reportGenerationTimer?.Dispose();
    }
    
    #endregion
}

#region Supporting Interfaces and Classes

public interface IEnhancedSecurityAuditService
{
    Task LogSecurityEventAsync(string category, string action, object details);
    Task<List<SecurityAuditEntry>> GetAuditTrailAsync(AuditFilter filter);
    Task<SecurityDashboardData> GetSecurityDashboardDataAsync();
    Task PurgeOldAuditEntriesAsync(int retentionDays);
    Task GenerateComplianceReportAsync(DateTime from, DateTime to);
    
    // Critical missing methods for security monitoring
    Task<Result<SecurityDashboardData>> GetSecurityDashboardAsync(TimeSpan period, CancellationToken cancellationToken = default);
    Task LogUserLockoutAsync(string userId, string reason, TimeSpan duration);
    Task LogIpBlockAsync(string ipAddress, string reason, TimeSpan duration);
}

public class SecurityAuditEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public AuditSeverity Severity { get; set; }
    public string? IpAddress { get; set; }
    public string? Username { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string? CorrelationId { get; set; }
}

public class AuditFilter
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Category { get; set; }
    public string? Action { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public AuditSeverity? MinSeverity { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 100;
}

public class ComplianceReport
{
    public string Id { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public ComplianceReportStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public ReportSummary? Summary { get; set; }
    public List<SecurityEventSummary>? SecurityEvents { get; set; }
    public List<ComplianceViolationSummary>? ComplianceViolations { get; set; }
    public List<AccessPatternSummary>? AccessPatterns { get; set; }
    public List<string>? Recommendations { get; set; }
}

public class ReportSummary
{
    public int TotalEvents { get; set; }
    public int SecurityEvents { get; set; }
    public int CriticalEvents { get; set; }
    public int ComplianceViolations { get; set; }
    public int UniqueUsers { get; set; }
    public int UniqueIpAddresses { get; set; }
}

public class ComplianceViolationSummary
{
    public string ViolationType { get; set; } = string.Empty;
    public int Count { get; set; }
    public DateTime LastOccurrence { get; set; }
    public int AffectedUsers { get; set; }
    public int AffectedIps { get; set; }
}

public class AccessPatternSummary
{
    public string Username { get; set; } = string.Empty;
    public int TotalAccesses { get; set; }
    public int UnauthorizedAttempts { get; set; }
    public int ResourcesAccessed { get; set; }
    public DateTime LastAccess { get; set; }
    public int OffHoursAccesses { get; set; }
}

public class PurgeStatistics
{
    public int PurgedCount { get; set; }
    public DateTime PurgeDate { get; set; }
    public DateTime CutoffDate { get; set; }
}

public class SecurityAuditConfiguration
{
    public int RetentionDays { get; set; } = 90;
    public int BatchSize { get; set; } = 1000;
    public bool? EnableStructuredLogging { get; set; } = true;
    public bool EnableComplianceReporting { get; set; } = true;
    public bool EnableRealTimeAlerts { get; set; } = true;
}

public enum AuditSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ComplianceReportStatus
{
    Pending,
    Generating,
    Completed,
    Failed
}

#endregion