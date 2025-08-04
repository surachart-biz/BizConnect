using BizConnect.Dal.Models;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizConnect.Services;

/// <summary>
/// Service for managing branch operations
/// </summary>
public class BranchService : IBranchService
{
    private readonly BizConnectContext _context;
    private readonly ILogger<BranchService> _logger;

    public BranchService(BizConnectContext context, ILogger<BranchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active branches for dropdowns
    /// </summary>
    /// <returns>List of branches with ID and Name for dropdown population</returns>
    public async Task<List<(int BranchId, string Name)>> GetActiveBranchesForDropdownAsync()
    {
        try
        {
            _logger.LogInformation("Fetching active branches for dropdown");

            var branches = await _context.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .Select(b => new { b.BranchId, b.Name })
                .ToListAsync();

            var result = branches.Select(b => (b.BranchId, b.Name)).ToList();
            _logger.LogInformation("Successfully fetched {Count} active branches", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch active branches for dropdown");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific branch by ID
    /// </summary>
    /// <param name="id">Branch ID</param>
    /// <returns>Branch entity or null if not found</returns>
    public async Task<Branch?> GetBranchByIdAsync(int id)
    {
        try
        {
            _logger.LogInformation("Fetching branch with ID: {BranchId}", id);

            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
            {
                _logger.LogWarning("Branch not found with ID: {BranchId}", id);
            }
            else
            {
                _logger.LogInformation("Successfully fetched branch: {BranchName} (ID: {BranchId})", branch.Name, id);
            }

            return branch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch branch with ID: {BranchId}", id);
            throw;
        }
    }

    /// <summary>
    /// Gets all active branches as entities
    /// </summary>
    /// <returns>Enumerable of active Branch entities</returns>
    public async Task<IEnumerable<Branch>> GetAllActiveBranchesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching all active branches");

            var branches = await _context.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.Name)
                .ToListAsync();

            _logger.LogInformation("Successfully fetched {Count} active branches", branches.Count);
            return branches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch all active branches");
            throw;
        }
    }

    /// <summary>
    /// Checks if a branch exists and is active
    /// </summary>
    /// <param name="branchId">Branch ID to check</param>
    /// <returns>True if branch exists and is active</returns>
    public async Task<bool> IsActiveBranchAsync(int branchId)
    {
        try
        {
            _logger.LogInformation("Checking if branch is active: {BranchId}", branchId);

            var isActive = await _context.Branches
                .AnyAsync(b => b.BranchId == branchId && b.IsActive);

            _logger.LogInformation("Branch {BranchId} active status: {IsActive}", branchId, isActive);
            return isActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check branch active status: {BranchId}", branchId);
            throw;
        }
    }

    /// <summary>
    /// Gets branch name by ID
    /// </summary>
    /// <param name="branchId">Branch ID</param>
    /// <returns>Branch name or null if not found</returns>
    public async Task<string?> GetBranchNameAsync(int branchId)
    {
        try
        {
            _logger.LogInformation("Fetching branch name for ID: {BranchId}", branchId);

            var name = await _context.Branches
                .Where(b => b.BranchId == branchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            if (name == null)
            {
                _logger.LogWarning("Branch name not found for ID: {BranchId}", branchId);
            }
            else
            {
                _logger.LogInformation("Successfully fetched branch name: {BranchName} for ID: {BranchId}", name, branchId);
            }

            return name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch branch name for ID: {BranchId}", branchId);
            throw;
        }
    }

    /// <summary>
    /// Gets all branches (both active and inactive) for analytics
    /// </summary>
    /// <returns>Enumerable of all Branch entities</returns>
    public async Task<IEnumerable<Branch>> GetAllBranchesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching all branches");

            var branches = await _context.Branches
                .OrderBy(b => b.Name)
                .ToListAsync();

            _logger.LogInformation("Successfully fetched {Count} branches", branches.Count);
            return branches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch all branches");
            throw;
        }
    }

    /// <summary>
    /// Gets all active branches (Result pattern compatible method)
    /// </summary>
    /// <returns>Result with list of active branches</returns>
    public async Task<Result<IEnumerable<Branch>>> GetActiveBranchesAsync()
    {
        try
        {
            _logger.LogInformation("Fetching active branches with Result pattern");

            var branches = await GetAllActiveBranchesAsync();
            
            return Result<IEnumerable<Branch>>.Success(branches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch active branches");
            return Result<IEnumerable<Branch>>.Failure($"Failed to fetch active branches: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets branch performance data
    /// </summary>
    /// <param name="days">Number of days to analyze</param>
    /// <returns>Result with branch performance metrics</returns>
    public async Task<Result<List<BranchPerformance>>> GetBranchPerformanceAsync(int days = 30)
    {
        try
        {
            if (days <= 0 || days > 365) days = 30;

            _logger.LogInformation("Calculating branch performance for {Days} days", days);

            var startDate = DateTime.UtcNow.AddDays(-days).Date;
            var endDate = DateTime.UtcNow.Date;

            // Get all branches with their registration stats
            var branchPerformanceData = await _context.Branches
                .Select(b => new BranchPerformance
                {
                    BranchId = b.BranchId,
                    BranchName = b.Name,
                    TotalRegistrations = b.KbankOddRegistrations
                        .Count(r => r.CreatedAt.Date >= startDate && r.CreatedAt.Date <= endDate),
                    SuccessfulRegistrations = b.KbankOddRegistrations
                        .Count(r => r.CreatedAt.Date >= startDate && r.CreatedAt.Date <= endDate && r.Status == "Success"),
                    FailedRegistrations = b.KbankOddRegistrations
                        .Count(r => r.CreatedAt.Date >= startDate && r.CreatedAt.Date <= endDate && r.Status == "Fail"),
                    PendingRegistrations = b.KbankOddRegistrations
                        .Count(r => r.CreatedAt.Date >= startDate && r.CreatedAt.Date <= endDate && r.Status == "Pending"),
                    LastRegistrationAt = b.KbankOddRegistrations
                        .Where(r => r.CreatedAt.Date >= startDate && r.CreatedAt.Date <= endDate)
                        .OrderByDescending(r => r.CreatedAt)
                        .Select(r => r.CreatedAt)
                        .FirstOrDefault(),
                    PeriodStart = startDate,
                    PeriodEnd = endDate
                })
                .Where(bp => bp.TotalRegistrations > 0) // Only include branches with registrations
                .OrderByDescending(bp => bp.TotalRegistrations)
                .ToListAsync();

            // Calculate average processing times separately (complex query)
            foreach (var branchPerf in branchPerformanceData)
            {
                var processingTimes = await _context.KbankOddRegistrations
                    .Where(r => r.BranchId == branchPerf.BranchId &&
                               r.CreatedAt.Date >= startDate && r.CreatedAt.Date <= endDate &&
                               r.Status == "Success" && r.UpdatedAt.HasValue)
                    .Select(r => new { r.CreatedAt, r.UpdatedAt })
                    .ToListAsync();

                if (processingTimes.Any())
                {
                    var avgMinutes = processingTimes
                        .Select(pt => (pt.UpdatedAt!.Value - pt.CreatedAt).TotalMinutes)
                        .Where(minutes => minutes > 0)
                        .DefaultIfEmpty(0)
                        .Average();

                    branchPerf.AverageProcessingTimeMinutes = Math.Round((decimal)avgMinutes, 2);
                }
            }

            _logger.LogInformation("Calculated performance for {Count} branches with registrations", branchPerformanceData.Count);

            return Result<List<BranchPerformance>>.Success(branchPerformanceData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate branch performance for {Days} days", days);
            return Result<List<BranchPerformance>>.Failure($"Failed to calculate branch performance: {ex.Message}");
        }
    }
}