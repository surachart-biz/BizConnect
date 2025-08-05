using BizConnect.Dal.Models;
using BizConnect.Services.Models.Results;

namespace BizConnect.Services.Interfaces;

/// <summary>
/// Service interface for managing branch operations
/// </summary>
public interface IBranchService
{
    /// <summary>
    /// Gets all active branches for dropdowns
    /// </summary>
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>List of branches with ID and Name for dropdown population</returns>
    Task<List<(int BranchId, string Name)>> GetActiveBranchesForDropdownAsync(string language = "en");

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
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>Branch name or null if not found</returns>
    Task<string?> GetBranchNameAsync(int branchId, string language = "en");

    /// <summary>
    /// Gets all branches (both active and inactive) for analytics
    /// </summary>
    /// <returns>Enumerable of all Branch entities</returns>
    Task<IEnumerable<Branch>> GetAllBranchesAsync();

    /// <summary>
    /// Gets all active branches (Result pattern compatible method)
    /// </summary>
    /// <returns>Result with list of active branches</returns>
    Task<Result<IEnumerable<Branch>>> GetActiveBranchesAsync();

    /// <summary>
    /// Gets branch performance data
    /// </summary>
    /// <param name="days">Number of days to analyze</param>
    /// <param name="language">Language code ('th' for Thai, 'en' for English). Defaults to 'en'</param>
    /// <returns>Result with branch performance metrics</returns>
    Task<Result<List<BranchPerformance>>> GetBranchPerformanceAsync(int days = 30, string language = "en");
}