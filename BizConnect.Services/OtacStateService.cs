using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services;

/// <summary>
/// Service for managing OTAC state transitions and validation.
/// Enforces critical business rules including Used state permanence.
/// </summary>
public class OtacStateService : IOtacStateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OtacStateService> _logger;

    // OTAC State Constants
    public const string GENERATED = "Generated";
    public const string VALIDATED = "Validated";
    public const string USED = "Used";
    public const string EXPIRED = "Expired";
    public const string INVALIDATED = "Invalidated";
    public const string PURGED = "Purged";

    // Valid state transitions mapping
    private static readonly Dictionary<string, HashSet<string>> ValidTransitions = new()
    {
        [GENERATED] = new HashSet<string> { VALIDATED, EXPIRED, INVALIDATED },
        [VALIDATED] = new HashSet<string> { USED, EXPIRED, INVALIDATED },
        [USED] = new HashSet<string>(), // CRITICAL: Used state is PERMANENT - no transitions allowed
        [EXPIRED] = new HashSet<string> { PURGED },
        [INVALIDATED] = new HashSet<string> { PURGED },
        [PURGED] = new HashSet<string>() // Terminal state
    };

    // States that allow purging (Used state is excluded for permanence)
    private static readonly HashSet<string> PurgeableStates = new() { EXPIRED, INVALIDATED };

    // All valid states
    private static readonly HashSet<string> AllValidStates = new() 
    { 
        GENERATED, VALIDATED, USED, EXPIRED, INVALIDATED, PURGED 
    };

    public OtacStateService(
        IUnitOfWork unitOfWork,
        ILogger<OtacStateService> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates if a state transition is allowed based on business rules.
    /// CRITICAL: Used state has NO valid transitions (permanent).
    /// </summary>
    public bool CanTransition(string fromState, string toState)
    {
        if (string.IsNullOrEmpty(fromState) || string.IsNullOrEmpty(toState))
            return false;

        if (!IsValidState(fromState) || !IsValidState(toState))
            return false;

        // CRITICAL BUSINESS RULE: Used state is permanent
        if (fromState == USED)
        {
            _logger.LogWarning("VIOLATION: Attempt to transition from permanent Used state for state {FromState} -> {ToState}", 
                fromState, toState);
            return false;
        }

        return ValidTransitions.ContainsKey(fromState) && 
               ValidTransitions[fromState].Contains(toState);
    }

    /// <summary>
    /// Validates if a state transition is allowed with detailed validation result.
    /// </summary>
    public StateTransitionValidation ValidateTransition(string fromState, string toState)
    {
        var validation = new StateTransitionValidation
        {
            FromState = fromState ?? string.Empty,
            ToState = toState ?? string.Empty
        };

        if (string.IsNullOrEmpty(fromState))
        {
            validation.Reason = "From state cannot be null or empty";
            return validation;
        }

        if (string.IsNullOrEmpty(toState))
        {
            validation.Reason = "To state cannot be null or empty";
            return validation;
        }

        if (!IsValidState(fromState))
        {
            validation.Reason = $"Invalid from state: '{fromState}'. Valid states: {string.Join(", ", AllValidStates)}";
            return validation;
        }

        if (!IsValidState(toState))
        {
            validation.Reason = $"Invalid to state: '{toState}'. Valid states: {string.Join(", ", AllValidStates)}";
            return validation;
        }

        // CRITICAL BUSINESS RULE: Used state is permanent
        if (fromState == USED)
        {
            validation.Reason = "CRITICAL: Used state is PERMANENT and cannot be changed. Required for daily payment processing.";
            _logger.LogWarning("VIOLATION: Attempt to transition from permanent Used state: {FromState} -> {ToState}", 
                fromState, toState);
            return validation;
        }

        if (!ValidTransitions.ContainsKey(fromState))
        {
            validation.Reason = $"No valid transitions defined for state '{fromState}'";
            return validation;
        }

        if (!ValidTransitions[fromState].Contains(toState))
        {
            var validTargets = string.Join(", ", ValidTransitions[fromState]);
            validation.Reason = $"Invalid transition from '{fromState}' to '{toState}'. Valid transitions: {validTargets}";
            return validation;
        }

        validation.IsValid = true;
        validation.Reason = $"Valid transition: {fromState} -> {toState}";
        return validation;
    }

    /// <summary>
    /// Gets all valid states that can be transitioned to from the given state.
    /// </summary>
    public IEnumerable<string> GetValidTransitions(string fromState)
    {
        if (string.IsNullOrEmpty(fromState) || !ValidTransitions.ContainsKey(fromState))
            return Enumerable.Empty<string>();

        return ValidTransitions[fromState];
    }

    /// <summary>
    /// Gets all valid OTAC states supported by the system.
    /// </summary>
    public IEnumerable<string> GetAllValidStates()
    {
        return AllValidStates;
    }

    /// <summary>
    /// Determines if a state allows purging of the record.
    /// CRITICAL: Used state records must NEVER be purged.
    /// </summary>
    public bool CanPurgeRecord(string state)
    {
        if (string.IsNullOrEmpty(state))
            return false;

        // CRITICAL: Used state records are permanent and required for daily payments
        if (state == USED)
        {
            _logger.LogDebug("PROTECTION: Used state record cannot be purged - required for daily payments");
            return false;
        }

        return PurgeableStates.Contains(state);
    }

    /// <summary>
    /// Gets records that are safe to purge (excludes Used state).
    /// </summary>
    public IEnumerable<string> GetPurgeableStates(IEnumerable<string> states)
    {
        return states?.Where(CanPurgeRecord) ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// Validates that a state is one of the supported values.
    /// </summary>
    public bool IsValidState(string state)
    {
        return !string.IsNullOrEmpty(state) && AllValidStates.Contains(state);
    }

    /// <summary>
    /// Gets descriptive information about a state including business rules.
    /// </summary>
    public OtacStateInfo GetStateInfo(string state)
    {
        if (!IsValidState(state))
        {
            return new OtacStateInfo
            {
                State = state ?? string.Empty,
                Description = "Invalid state",
                BusinessRule = "This state is not recognized by the system"
            };
        }

        return state switch
        {
            GENERATED => new OtacStateInfo
            {
                State = GENERATED,
                Description = "OTAC code has been generated and is awaiting validation",
                IsPermanent = false,
                CanBePurged = false,
                ValidTransitions = GetValidTransitions(GENERATED),
                BusinessRule = "Initial state when OTAC is created. Can transition to Validated, Expired, or Invalidated."
            },
            VALIDATED => new OtacStateInfo
            {
                State = VALIDATED,
                Description = "OTAC code has been validated and is ready for use",
                IsPermanent = false,
                CanBePurged = false,
                ValidTransitions = GetValidTransitions(VALIDATED),
                BusinessRule = "Code has been validated by user. Can transition to Used, Expired, or Invalidated."
            },
            USED => new OtacStateInfo
            {
                State = USED,
                Description = "OTAC code has been used for registration and is PERMANENT",
                IsPermanent = true,
                CanBePurged = false,
                ValidTransitions = GetValidTransitions(USED),
                BusinessRule = "CRITICAL: PERMANENT state required for daily payment processing. NEVER purge or modify."
            },
            EXPIRED => new OtacStateInfo
            {
                State = EXPIRED,
                Description = "OTAC code has expired and can be purged",
                IsPermanent = false,
                CanBePurged = true,
                ValidTransitions = GetValidTransitions(EXPIRED),
                BusinessRule = "Code expired before being used. Can be purged after grace period."
            },
            INVALIDATED => new OtacStateInfo
            {
                State = INVALIDATED,
                Description = "OTAC code was invalidated due to validation failure",
                IsPermanent = false,
                CanBePurged = true,
                ValidTransitions = GetValidTransitions(INVALIDATED),
                BusinessRule = "Code invalidated due to failed validation attempts. Can be purged."
            },
            PURGED => new OtacStateInfo
            {
                State = PURGED,
                Description = "OTAC code record has been archived/purged",
                IsPermanent = false,
                CanBePurged = false,
                ValidTransitions = GetValidTransitions(PURGED),
                BusinessRule = "Terminal state. Record has been archived and cleaned up."
            },
            _ => new OtacStateInfo
            {
                State = state,
                Description = "Unknown state",
                BusinessRule = "State not recognized"
            }
        };
    }

    /// <summary>
    /// Gets comprehensive lifecycle statistics for monitoring.
    /// </summary>
    public async Task<OtacLifecycleStats> GetLifecycleStatsAsync()
    {
        try
        {
            var repository = _unitOfWork.GetRepository<KbankOddRegistration>();
            
            // Get all records grouped by state
            var stateStats = await repository.Query()
                .GroupBy(r => r.OtacState)
                .Select(g => new { State = g.Key, Count = g.Count() })
                .ToListAsync();

            var stats = new OtacLifecycleStats();

            foreach (var stateStat in stateStats)
            {
                stats.TotalRecords += stateStat.Count;
                
                switch (stateStat.State)
                {
                    case GENERATED:
                        stats.GeneratedCount = stateStat.Count;
                        break;
                    case VALIDATED:
                        stats.ValidatedCount = stateStat.Count;
                        break;
                    case USED:
                        stats.UsedCount = stateStat.Count;
                        break;
                    case EXPIRED:
                        stats.ExpiredCount = stateStat.Count;
                        break;
                    case INVALIDATED:
                        stats.InvalidatedCount = stateStat.Count;
                        break;
                    case PURGED:
                        stats.PurgedCount = stateStat.Count;
                        break;
                    default:
                        _logger.LogWarning("Unknown OTAC state found in database: {State} with {Count} records", 
                            stateStat.State, stateStat.Count);
                        break;
                }
            }

            _logger.LogDebug("OTAC Lifecycle Stats: Total={Total}, Used={Used} (permanent), " +
                           "Active={Active}, Purgeable={Purgeable}, Conversion Rate={ConversionRate:F2}%",
                stats.TotalRecords, stats.PermanentRecords, stats.ActiveRecords, 
                stats.PurgeableRecords, stats.UsageConversionRate);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OTAC lifecycle statistics");
            return new OtacLifecycleStats();
        }
    }
}