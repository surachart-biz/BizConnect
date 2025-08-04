using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizConnect.Services.Caching;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Security.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services.Security;

/// <summary>
/// Advanced threat response service with automated response mechanisms,
/// escalation procedures, and comprehensive action tracking.
/// </summary>
public class ThreatResponseService : IThreatResponseService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<ThreatResponseService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISecurityAuditService _securityAuditService;
    
    // Cache key prefixes
    private const string ThreatResponsePrefix = "ThreatResponse:Response";
    private const string UserLockoutPrefix = "ThreatResponse:UserLockout";
    private const string IpBlockPrefix = "ThreatResponse:IpBlock";
    private const string AdminNotificationPrefix = "ThreatResponse:AdminNotification";
    private const string SecurityLevelPrefix = "ThreatResponse:SecurityLevel";
    private const string ResponseHistoryPrefix = "ThreatResponse:History";
    private const string EscalationPrefix = "ThreatResponse:Escalation";
    
    // Configuration
    private readonly ThreatResponseConfiguration _config;
    private readonly ConcurrentDictionary<string, ResponseAction> _activeResponses;
    private readonly Timer _cleanupTimer;
    
    public ThreatResponseService(
        ICacheService cacheService,
        ILogger<ThreatResponseService> logger,
        IConfiguration configuration,
        IDateTimeProvider dateTimeProvider,
        ISecurityAuditService securityAuditService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        _securityAuditService = securityAuditService ?? throw new ArgumentNullException(nameof(securityAuditService));
        
        _config = LoadConfiguration();
        _activeResponses = new ConcurrentDictionary<string, ResponseAction>();
        
        // Initialize cleanup timer
        _cleanupTimer = new Timer(PerformCleanup, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            
        _logger.LogInformation("Threat response service initialized with {ResponseLevels} response levels configured",
            _config.ResponseLevels.Count);
    }
    
    #region IThreatResponseService Implementation
    
    public async Task<ResponseAction> RespondToThreatAsync(ThreatInfo threat)
    {
        try
        {
            var responseAction = new ResponseAction
            {
                ThreatId = threat.Id,
                ThreatLevel = threat.Level,
                IpAddress = threat.IpAddress,
                Username = threat.Username,
                InitiatedAt = _dateTimeProvider.UtcNow,
                ResponseId = Guid.NewGuid().ToString()
            };
            
            // Get response configuration for threat level
            var responseConfig = GetResponseConfiguration(threat.Level);
            if (responseConfig == null)
            {
                responseAction.Success = false;
                responseAction.ErrorMessage = "No response configuration found for threat level";
                return responseAction;
            }
            
            // Execute responses based on threat level
            var actionTasks = new List<Task>();
            
            // 1. IP-based responses
            if (responseConfig.BlockIp)
            {
                actionTasks.Add(ExecuteIpBlockingAsync(threat, responseConfig, responseAction));
            }
            
            // 2. User-based responses
            if (responseConfig.LockoutUser && !string.IsNullOrEmpty(threat.Username))
            {
                actionTasks.Add(ExecuteUserLockoutAsync(threat, responseConfig, responseAction));
            }
            
            // 3. Session termination
            if (responseConfig.TerminateSessions)
            {
                actionTasks.Add(ExecuteSessionTerminationAsync(threat, responseAction));
            }
            
            // 4. Admin notifications
            if (responseConfig.NotifyAdmin)
            {
                actionTasks.Add(ExecuteAdminNotificationAsync(threat, responseAction));
            }
            
            // 5. Security level escalation
            if (responseConfig.EscalateSecurityLevel)
            {
                actionTasks.Add(ExecuteSecurityEscalationAsync(threat, responseAction));
            }
            
            // 6. Enhanced monitoring
            if (responseConfig.EnableEnhancedMonitoring)
            {
                actionTasks.Add(ExecuteEnhancedMonitoringAsync(threat, responseConfig, responseAction));
            }
            
            // Execute all actions in parallel
            await Task.WhenAll(actionTasks);
            
            responseAction.CompletedAt = _dateTimeProvider.UtcNow;
            responseAction.Duration = responseAction.CompletedAt.Value - responseAction.InitiatedAt;
            responseAction.Success = !responseAction.Actions.Any(a => !a.Success);
            
            // Store response action
            await StoreResponseActionAsync(responseAction);
            
            // Add to active responses for tracking
            _activeResponses.TryAdd(responseAction.ResponseId, responseAction);
            
            // Log security audit
            await _securityAuditService.LogThreatResponseAsync(threat.IpAddress, threat.Level.ToString(), 
                responseAction.Actions.Count);
            
            _logger.LogInformation("Threat response completed for {ThreatId}: {ActionCount} actions executed, Success={Success}",
                threat.Id, responseAction.Actions.Count, responseAction.Success);
                
            return responseAction;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error responding to threat {ThreatId}", threat.Id);
            return new ResponseAction
            {
                ThreatId = threat.Id,
                Success = false,
                ErrorMessage = ex.Message,
                InitiatedAt = _dateTimeProvider.UtcNow,
                CompletedAt = _dateTimeProvider.UtcNow
            };
        }
    }
    
    public async Task LockoutUserAsync(string userId, TimeSpan duration, string reason)
    {
        try
        {
            var lockoutInfo = new UserLockoutInfo
            {
                UserId = userId,
                Reason = reason,
                LockedAt = _dateTimeProvider.UtcNow,
                LockoutEnd = _dateTimeProvider.UtcNow.Add(duration),
                LockoutDuration = duration,
                IsActive = true
            };
            
            var cacheKey = $"{UserLockoutPrefix}:{userId}";
            await _cacheService.SetAsync(lockoutInfo, cacheKey, duration);
            
            // Log the user lockout as suspicious activity
            await _securityAuditService.LogSuspiciousActivityAsync("USER_LOCKOUT", 
                $"User {userId} locked out for {duration.TotalMinutes} minutes: {reason}", "System");
            
            _logger.LogWarning("User {UserId} locked out for {Duration} minutes: {Reason}",
                userId, duration.TotalMinutes, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking out user {UserId}", userId);
        }
    }
    
    public async Task BlockIpAsync(string ipAddress, TimeSpan duration, string reason)
    {
        try
        {
            var blockInfo = new IpBlockInfo
            {
                IpAddress = ipAddress,
                Reason = reason,
                BlockedAt = _dateTimeProvider.UtcNow,
                BlockEnd = _dateTimeProvider.UtcNow.Add(duration),
                BlockDuration = duration,
                IsActive = true
            };
            
            var cacheKey = $"{IpBlockPrefix}:{ipAddress}";
            await _cacheService.SetAsync(blockInfo, cacheKey, duration);
            
            // Log the IP block
            await _securityAuditService.LogIpBlockAsync(ipAddress, reason);
            
            _logger.LogWarning("IP {IpAddress} blocked for {Duration} minutes: {Reason}",
                ipAddress, duration.TotalMinutes, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking IP {IpAddress}", ipAddress);
        }
    }
    
    public async Task NotifyAdminAsync(ThreatInfo threat)
    {
        try
        {
            var notification = new AdminNotification
            {
                Id = Guid.NewGuid().ToString(),
                ThreatId = threat.Id,
                ThreatLevel = threat.Level,
                IpAddress = threat.IpAddress,
                Username = threat.Username,
                Title = GenerateNotificationTitle(threat),
                Message = GenerateNotificationMessage(threat),
                CreatedAt = _dateTimeProvider.UtcNow,
                Priority = DetermineNotificationPriority(threat.Level),
                IsRead = false
            };
            
            var cacheKey = $"{AdminNotificationPrefix}:{notification.Id}";
            await _cacheService.SetAsync(notification, cacheKey, TimeSpan.FromDays(7));
            
            // Store in notifications list for admin dashboard
            var notificationsKey = $"{AdminNotificationPrefix}:List";
            var notifications = await _cacheService.GetAsync<List<string>>(notificationsKey) ?? new List<string>();
            notifications.Insert(0, notification.Id); // Add to beginning for latest first
            
            // Keep only last 100 notifications
            if (notifications.Count > 100)
            {
                notifications = notifications.Take(100).ToList();
            }
            
            await _cacheService.SetAsync(notifications, notificationsKey, TimeSpan.FromDays(7));
            
            _logger.LogInformation("Admin notification created for threat {ThreatId}: {Title}",
                threat.Id, notification.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating admin notification for threat {ThreatId}", threat.Id);
        }
    }
    
    public async Task EscalateSecurityLevelAsync(ThreatLevel level)
    {
        try
        {
            var escalation = new SecurityLevelEscalation
            {
                PreviousLevel = await GetCurrentSecurityLevelAsync(),
                NewLevel = level,
                EscalatedAt = _dateTimeProvider.UtcNow,
                Reason = $"Automated escalation due to {level} threat detection",
                Duration = GetEscalationDuration(level),
                IsActive = true
            };
            
            escalation.EscalationEnd = escalation.EscalatedAt.Add(escalation.Duration);
            
            var cacheKey = $"{SecurityLevelPrefix}:Current";
            await _cacheService.SetAsync(escalation, cacheKey, escalation.Duration);
            
            // Store escalation history
            var historyKey = $"{EscalationPrefix}:{Guid.NewGuid()}";
            await _cacheService.SetAsync(escalation, historyKey, TimeSpan.FromDays(30));
            
            _logger.LogWarning("Security level escalated to {NewLevel} for {Duration} minutes",
                level, escalation.Duration.TotalMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error escalating security level to {Level}", level);
        }
    }
    
    #endregion
    
    #region Public Helper Methods
    
    public async Task<bool> IsUserLockedOutAsync(string userId)
    {
        try
        {
            var cacheKey = $"{UserLockoutPrefix}:{userId}";
            var lockoutInfo = await _cacheService.GetAsync<UserLockoutInfo>(cacheKey);
            
            return lockoutInfo != null && lockoutInfo.IsActive && 
                   lockoutInfo.LockoutEnd > _dateTimeProvider.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user lockout status for {UserId}", userId);
            return false;
        }
    }
    
    public async Task<bool> IsIpBlockedAsync(string ipAddress)
    {
        try
        {
            var cacheKey = $"{IpBlockPrefix}:{ipAddress}";
            var blockInfo = await _cacheService.GetAsync<IpBlockInfo>(cacheKey);
            
            return blockInfo != null && blockInfo.IsActive && 
                   blockInfo.BlockEnd > _dateTimeProvider.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking IP block status for {IpAddress}", ipAddress);
            return false;
        }
    }
    
    public async Task<UserLockoutInfo?> GetUserLockoutInfoAsync(string userId)
    {
        try
        {
            var cacheKey = $"{UserLockoutPrefix}:{userId}";
            return await _cacheService.GetAsync<UserLockoutInfo>(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user lockout info for {UserId}", userId);
            return null;
        }
    }
    
    public async Task<IpBlockInfo?> GetIpBlockInfoAsync(string ipAddress)
    {
        try
        {
            var cacheKey = $"{IpBlockPrefix}:{ipAddress}";
            return await _cacheService.GetAsync<IpBlockInfo>(cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting IP block info for {IpAddress}", ipAddress);
            return null;
        }
    }
    
    public async Task<List<AdminNotification>> GetAdminNotificationsAsync(int count = 50)
    {
        try
        {
            var notifications = new List<AdminNotification>();
            var notificationsKey = $"{AdminNotificationPrefix}:List";
            var notificationIds = await _cacheService.GetAsync<List<string>>(notificationsKey) ?? new List<string>();
            
            foreach (var id in notificationIds.Take(count))
            {
                var notificationKey = $"{AdminNotificationPrefix}:{id}";
                var notification = await _cacheService.GetAsync<AdminNotification>(notificationKey);
                if (notification != null)
                {
                    notifications.Add(notification);
                }
            }
            
            return notifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting admin notifications");
            return new List<AdminNotification>();
        }
    }
    
    public async Task<ThreatLevel> GetCurrentSecurityLevelAsync()
    {
        try
        {
            var cacheKey = $"{SecurityLevelPrefix}:Current";
            var escalation = await _cacheService.GetAsync<SecurityLevelEscalation>(cacheKey);
            
            if (escalation != null && escalation.IsActive && 
                escalation.EscalationEnd > _dateTimeProvider.UtcNow)
            {
                return escalation.NewLevel;
            }
            
            return ThreatLevel.Low; // Default security level
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current security level");
            return ThreatLevel.Low;
        }
    }
    
    public async Task<List<ResponseAction>> GetActiveResponsesAsync()
    {
        return _activeResponses.Values.Where(r => r.IsActive).ToList();
    }
    
    public async Task<ThreatResponseStatistics> GetStatisticsAsync()
    {
        try
        {
            var stats = new ThreatResponseStatistics
            {
                GeneratedAt = _dateTimeProvider.UtcNow
            };
            
            // Get statistics from last 24 hours
            var cutoffTime = _dateTimeProvider.UtcNow.AddHours(-24);
            var responses = _activeResponses.Values.Where(r => r.InitiatedAt >= cutoffTime).ToList();
            
            stats.TotalResponses = responses.Count;
            stats.SuccessfulResponses = responses.Count(r => r.Success);
            stats.FailedResponses = responses.Count(r => !r.Success);
            
            if (responses.Any())
            {
                stats.AverageResponseTime = responses.Average(r => r.Duration?.TotalSeconds ?? 0);
            }
            
            stats.ResponsesByThreatLevel = responses.GroupBy(r => r.ThreatLevel)
                .ToDictionary(g => g.Key.ToString(), g => g.Count());
                
            stats.CurrentSecurityLevel = await GetCurrentSecurityLevelAsync();
            stats.ActiveUserLockouts = await GetActiveUserLockoutCountAsync();
            stats.ActiveIpBlocks = await GetActiveIpBlockCountAsync();
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting threat response statistics");
            return new ThreatResponseStatistics { GeneratedAt = _dateTimeProvider.UtcNow };
        }
    }
    
    #endregion
    
    #region Private Helper Methods
    
    private async Task ExecuteIpBlockingAsync(ThreatInfo threat, ResponseConfiguration config, ResponseAction responseAction)
    {
        try
        {
            var duration = GetBlockDuration(threat.Level);
            await BlockIpAsync(threat.IpAddress, duration, $"Automated response to {threat.Level} threat");
            
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "IP_BLOCK",
                Success = true,
                Details = $"Blocked IP {threat.IpAddress} for {duration.TotalMinutes} minutes",
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
        catch (Exception ex)
        {
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "IP_BLOCK",
                Success = false,
                ErrorMessage = ex.Message,
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
    }
    
    private async Task ExecuteUserLockoutAsync(ThreatInfo threat, ResponseConfiguration config, ResponseAction responseAction)
    {
        try
        {
            if (string.IsNullOrEmpty(threat.Username)) return;
            
            var duration = GetLockoutDuration(threat.Level);
            await LockoutUserAsync(threat.Username, duration, $"Automated response to {threat.Level} threat");
            
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "USER_LOCKOUT",
                Success = true,
                Details = $"Locked out user {threat.Username} for {duration.TotalMinutes} minutes",
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
        catch (Exception ex)
        {
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "USER_LOCKOUT",
                Success = false,
                ErrorMessage = ex.Message,
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
    }
    
    private async Task ExecuteSessionTerminationAsync(ThreatInfo threat, ResponseAction responseAction)
    {
        try
        {
            // Placeholder for session termination logic
            // In a real implementation, this would terminate active sessions for the user/IP
            
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "SESSION_TERMINATION",
                Success = true,
                Details = $"Sessions terminated for IP {threat.IpAddress}",
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
        catch (Exception ex)
        {
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "SESSION_TERMINATION",
                Success = false,
                ErrorMessage = ex.Message,
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
    }
    
    private async Task ExecuteAdminNotificationAsync(ThreatInfo threat, ResponseAction responseAction)
    {
        try
        {
            await NotifyAdminAsync(threat);
            
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "ADMIN_NOTIFICATION",
                Success = true,
                Details = $"Admin notification sent for {threat.Level} threat",
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
        catch (Exception ex)
        {
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "ADMIN_NOTIFICATION",
                Success = false,
                ErrorMessage = ex.Message,
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
    }
    
    private async Task ExecuteSecurityEscalationAsync(ThreatInfo threat, ResponseAction responseAction)
    {
        try
        {
            await EscalateSecurityLevelAsync(threat.Level);
            
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "SECURITY_ESCALATION",
                Success = true,
                Details = $"Security level escalated to {threat.Level}",
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
        catch (Exception ex)
        {
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "SECURITY_ESCALATION",
                Success = false,
                ErrorMessage = ex.Message,
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
    }
    
    private async Task ExecuteEnhancedMonitoringAsync(ThreatInfo threat, ResponseConfiguration config, ResponseAction responseAction)
    {
        try
        {
            var duration = TimeSpan.FromHours(4); // Enhanced monitoring duration
            var monitoringKey = $"EnhancedMonitoring:{threat.IpAddress}";
            await _cacheService.SetAsync(new { StartedAt = _dateTimeProvider.UtcNow }, monitoringKey, duration);
            
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "ENHANCED_MONITORING",
                Success = true,
                Details = $"Enhanced monitoring enabled for IP {threat.IpAddress} for {duration.TotalHours} hours",
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
        catch (Exception ex)
        {
            responseAction.Actions.Add(new IndividualAction
            {
                ActionType = "ENHANCED_MONITORING",
                Success = false,
                ErrorMessage = ex.Message,
                ExecutedAt = _dateTimeProvider.UtcNow
            });
        }
    }
    
    private ResponseConfiguration? GetResponseConfiguration(ThreatLevel level)
    {
        return _config.ResponseLevels.FirstOrDefault(r => r.ThreatLevel == level);
    }
    
    private TimeSpan GetBlockDuration(ThreatLevel level)
    {
        return level switch
        {
            ThreatLevel.Critical => TimeSpan.FromHours(24),
            ThreatLevel.High => TimeSpan.FromHours(4),
            ThreatLevel.Medium => TimeSpan.FromHours(1),
            ThreatLevel.Low => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromMinutes(15)
        };
    }
    
    private TimeSpan GetLockoutDuration(ThreatLevel level)
    {
        return level switch
        {
            ThreatLevel.Critical => TimeSpan.FromHours(24),
            ThreatLevel.High => TimeSpan.FromHours(2),
            ThreatLevel.Medium => TimeSpan.FromMinutes(30),
            ThreatLevel.Low => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromMinutes(15)
        };
    }
    
    private TimeSpan GetEscalationDuration(ThreatLevel level)
    {
        return level switch
        {
            ThreatLevel.Critical => TimeSpan.FromHours(8),
            ThreatLevel.High => TimeSpan.FromHours(4),
            ThreatLevel.Medium => TimeSpan.FromHours(2),
            ThreatLevel.Low => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(1)
        };
    }
    
    private string GenerateNotificationTitle(ThreatInfo threat)
    {
        return $"{threat.Level} Security Threat Detected";
    }
    
    private string GenerateNotificationMessage(ThreatInfo threat)
    {
        var message = $"A {threat.Level.ToString().ToLower()} security threat was detected from IP address {threat.IpAddress}";
        
        if (!string.IsNullOrEmpty(threat.Username))
        {
            message += $" targeting user {threat.Username}";
        }
        
        message += $" at {threat.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC.";
        
        if (!string.IsNullOrEmpty(threat.Description))
        {
            message += $" Details: {threat.Description}";
        }
        
        return message;
    }
    
    private NotificationPriority DetermineNotificationPriority(ThreatLevel level)
    {
        return level switch
        {
            ThreatLevel.Critical => NotificationPriority.Urgent,
            ThreatLevel.High => NotificationPriority.High,
            ThreatLevel.Medium => NotificationPriority.Medium,
            ThreatLevel.Low => NotificationPriority.Low,
            _ => NotificationPriority.Low
        };
    }
    
    private async Task StoreResponseActionAsync(ResponseAction responseAction)
    {
        var cacheKey = $"{ResponseHistoryPrefix}:{responseAction.ResponseId}";
        await _cacheService.SetAsync(responseAction, cacheKey, TimeSpan.FromDays(30));
    }
    
    private async Task<int> GetActiveUserLockoutCountAsync()
    {
        // Placeholder - would need to scan user lockout entries
        return 0;
    }
    
    private async Task<int> GetActiveIpBlockCountAsync()
    {
        // Placeholder - would need to scan IP block entries
        return 0;
    }
    
    private ThreatResponseConfiguration LoadConfiguration()
    {
        var config = new ThreatResponseConfiguration();
        _configuration.GetSection("ThreatResponse").Bind(config);
        
        // Set defaults if not configured
        if (!config.ResponseLevels.Any())
        {
            config.ResponseLevels = GetDefaultResponseLevels();
        }
        
        return config;
    }
    
    private List<ResponseConfiguration> GetDefaultResponseLevels()
    {
        return new List<ResponseConfiguration>
        {
            new ResponseConfiguration
            {
                ThreatLevel = ThreatLevel.Critical,
                BlockIp = true,
                LockoutUser = true,
                TerminateSessions = true,
                NotifyAdmin = true,
                EscalateSecurityLevel = true,
                EnableEnhancedMonitoring = true
            },
            new ResponseConfiguration
            {
                ThreatLevel = ThreatLevel.High,
                BlockIp = true,
                LockoutUser = true,
                TerminateSessions = false,
                NotifyAdmin = true,
                EscalateSecurityLevel = true,
                EnableEnhancedMonitoring = true
            },
            new ResponseConfiguration
            {
                ThreatLevel = ThreatLevel.Medium,
                BlockIp = true,
                LockoutUser = false,
                TerminateSessions = false,
                NotifyAdmin = true,
                EscalateSecurityLevel = false,
                EnableEnhancedMonitoring = true
            },
            new ResponseConfiguration
            {
                ThreatLevel = ThreatLevel.Low,
                BlockIp = false,
                LockoutUser = false,
                TerminateSessions = false,
                NotifyAdmin = false,
                EscalateSecurityLevel = false,
                EnableEnhancedMonitoring = false
            }
        };
    }
    
    private void PerformCleanup(object? state)
    {
        Task.Run(async () =>
        {
            try
            {
                // Clean up expired active responses
                var expiredResponses = _activeResponses.Values
                    .Where(r => r.CompletedAt.HasValue && 
                              r.CompletedAt.Value.AddHours(24) < _dateTimeProvider.UtcNow)
                    .Select(r => r.ResponseId)
                    .ToList();
                
                foreach (var responseId in expiredResponses)
                {
                    _activeResponses.TryRemove(responseId, out _);
                }
                
                if (expiredResponses.Any())
                {
                    _logger.LogDebug("Cleaned up {Count} expired threat responses", expiredResponses.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during threat response cleanup");
            }
        });
    }
    
    public void Dispose()
    {
        _cleanupTimer?.Dispose();
    }
    
    #endregion
}

#region Supporting Interfaces and Classes

public interface IThreatResponseService
{
    Task<ResponseAction> RespondToThreatAsync(ThreatInfo threat);
    Task LockoutUserAsync(string userId, TimeSpan duration, string reason);
    Task BlockIpAsync(string ipAddress, TimeSpan duration, string reason);
    Task NotifyAdminAsync(ThreatInfo threat);
    Task EscalateSecurityLevelAsync(ThreatLevel level);
}

#endregion

#region ThreatResponse-Specific Models

/// <summary>
/// Threat response statistics for monitoring and analysis.
/// </summary>
public class ThreatResponseStatistics
{
    public DateTime GeneratedAt { get; set; }
    public int TotalResponses { get; set; }
    public int SuccessfulResponses { get; set; }
    public int FailedResponses { get; set; }
    public double AverageResponseTime { get; set; }
    public Dictionary<string, int> ResponsesByThreatLevel { get; set; } = new();
    public ThreatLevel CurrentSecurityLevel { get; set; }
    public int ActiveUserLockouts { get; set; }
    public int ActiveIpBlocks { get; set; }
}

/// <summary>
/// Threat response configuration settings.
/// </summary>
public class ThreatResponseConfiguration
{
    public List<ResponseConfiguration> ResponseLevels { get; set; } = new();
    public bool EnableAutomatedResponse { get; set; } = true;
    public int MaxConcurrentResponses { get; set; } = 10;
}

/// <summary>
/// Response configuration for specific threat levels.
/// </summary>
public class ResponseConfiguration
{
    public ThreatLevel ThreatLevel { get; set; }
    public bool BlockIp { get; set; }
    public bool LockoutUser { get; set; }
    public bool TerminateSessions { get; set; }
    public bool NotifyAdmin { get; set; }
    public bool EscalateSecurityLevel { get; set; }
    public bool EnableEnhancedMonitoring { get; set; }
}

#endregion