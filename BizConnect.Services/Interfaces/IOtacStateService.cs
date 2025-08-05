using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service for managing OTAC state transitions and validation.
/// Enforces business rules including the critical Used state permanence requirement.
/// </summary>
public interface IOtacStateService
{
    /// <summary>
    /// Validates if a state transition is allowed based on business rules.
    /// CRITICAL: Used state has NO valid transitions (permanent).
    /// </summary>
    /// <param name="fromState">Current state</param>
    /// <param name="toState">Desired new state</param>
    /// <returns>True if transition is allowed</returns>
    bool CanTransition(string fromState, string toState);

    /// <summary>
    /// Validates if a state transition is allowed with detailed validation result.
    /// </summary>
    /// <param name="fromState">Current state</param>
    /// <param name="toState">Desired new state</param>
    /// <returns>Validation result with reason</returns>
    StateTransitionValidation ValidateTransition(string fromState, string toState);

    /// <summary>
    /// Gets all valid states that can be transitioned to from the given state.
    /// </summary>
    /// <param name="fromState">Current state</param>
    /// <returns>List of valid target states</returns>
    IEnumerable<string> GetValidTransitions(string fromState);

    /// <summary>
    /// Gets all valid OTAC states supported by the system.
    /// </summary>
    /// <returns>List of all valid states</returns>
    IEnumerable<string> GetAllValidStates();

    /// <summary>
    /// Determines if a state allows purging of the record.
    /// CRITICAL: Used state records must NEVER be purged.
    /// </summary>
    /// <param name="state">State to check</param>
    /// <returns>True if record can be safely purged</returns>
    bool CanPurgeRecord(string state);

    /// <summary>
    /// Gets records that are safe to purge (excludes Used state).
    /// </summary>
    /// <param name="states">States to filter</param>
    /// <returns>States that allow purging</returns>
    IEnumerable<string> GetPurgeableStates(IEnumerable<string> states);

    /// <summary>
    /// Validates that a state is one of the supported values.
    /// </summary>
    /// <param name="state">State to validate</param>
    /// <returns>True if state is valid</returns>
    bool IsValidState(string state);

    /// <summary>
    /// Gets descriptive information about a state including business rules.
    /// </summary>
    /// <param name="state">State to describe</param>
    /// <returns>State information</returns>
    OtacStateInfo GetStateInfo(string state);

    /// <summary>
    /// Gets comprehensive lifecycle statistics for monitoring.
    /// </summary>
    /// <returns>Lifecycle statistics</returns>
    Task<OtacLifecycleStats> GetLifecycleStatsAsync();
}

/// <summary>
/// Result of state transition validation.
/// </summary>
public class StateTransitionValidation
{
    public bool IsValid { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string FromState { get; set; } = string.Empty;
    public string ToState { get; set; } = string.Empty;
}

/// <summary>
/// Information about an OTAC state including business rules.
/// </summary>
public class OtacStateInfo
{
    public string State { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPermanent { get; set; }
    public bool CanBePurged { get; set; }
    public IEnumerable<string> ValidTransitions { get; set; } = new List<string>();
    public string BusinessRule { get; set; } = string.Empty;
}

/// <summary>
/// Comprehensive OTAC lifecycle statistics.
/// </summary>
public class OtacLifecycleStats
{
    public int TotalRecords { get; set; }
    public int GeneratedCount { get; set; }
    public int ValidatedCount { get; set; }
    public int UsedCount { get; set; }
    public int ExpiredCount { get; set; }
    public int InvalidatedCount { get; set; }
    public int PurgedCount { get; set; }
    
    public int ActiveRecords => GeneratedCount + ValidatedCount;
    public int PermanentRecords => UsedCount;
    public int PurgeableRecords => ExpiredCount + InvalidatedCount;
    
    public double UsageConversionRate => TotalRecords > 0 ? (double)UsedCount / TotalRecords * 100 : 0;
    public double ValidationSuccessRate => GeneratedCount > 0 ? (double)ValidatedCount / GeneratedCount * 100 : 0;
}