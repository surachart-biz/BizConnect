using BizConnect.Dal.Models;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for managing branch operations
/// </summary>
public interface IBranchService
{
    /// <summary>
    /// Gets all active branches for dropdowns
    /// </summary>
    /// <returns>List of branches with ID and Name for dropdown population</returns>
    Task<List<(int BranchId, string Name)>> GetActiveBranchesForDropdownAsync();

    /// <summary>
    /// Gets a specific branch by ID
    /// </summary>
    /// <param name="id">Branch ID</param>
    /// <returns>Branch entity or null if not found</returns>
    Task<Branch?> GetBranchByIdAsync(int id);

    /// <summary>
    /// Gets all active branches as entities
    /// </summary>
    /// <returns>Enumerable of active Branch entities</returns>
    Task<IEnumerable<Branch>> GetAllActiveBranchesAsync();

    /// <summary>
    /// Checks if a branch exists and is active
    /// </summary>
    /// <param name="branchId">Branch ID to check</param>
    /// <returns>True if branch exists and is active</returns>
    Task<bool> IsActiveBranchAsync(int branchId);

    /// <summary>
    /// Gets branch name by ID
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <returns>Branch name or null if not found</returns>
    Task<string?> GetBranchNameAsync(int branchId);
}