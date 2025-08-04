using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using BizConnect.Dal.Models;
using BizConnect.Dal.UnitOfWork;
using BizConnect.Services.Interfaces;
using BizConnect.Services.Models.Responses;
using BizConnect.Services.Models.Results;

namespace BizConnect.Services
{
    /// <summary>
    /// Service for querying KBank ODD registration data
    /// </summary>
    public class RegistrationQueryService : IRegistrationQueryService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<RegistrationQueryService> _logger;

        public RegistrationQueryService(
            IUnitOfWork unitOfWork,
            IDateTimeProvider dateTimeProvider,
            ILogger<RegistrationQueryService> logger)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves a paginated list of registrations with optional filtering
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="status">Optional status filter</param>
        /// <param name="search">Optional search term for full name, mobile, or account number</param>
        /// <returns>Result containing paged registration data</returns>
        public async Task<Result<Models.Responses.PagedResult<KbankOddRegistration>>> GetPagedAsync(int page = 1, int pageSize = 20, string? status = null, string? search = null)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 1000) pageSize = 20;

                _logger.LogDebug("Retrieving paged registrations: page={Page}, pageSize={PageSize}, status={Status}, search={Search}", 
                    page, pageSize, status, search);

                var baseQuery = _unitOfWork.KbankOddRegistrations.Query();

                // Apply status filter
                if (!string.IsNullOrWhiteSpace(status))
                {
                    baseQuery = baseQuery.Where(r => r.Status == status);
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim().ToLower();
                    baseQuery = baseQuery.Where(r => 
                        (r.FullName != null && r.FullName.ToLower().Contains(searchTerm)) ||
                        (r.MobileNo != null && r.MobileNo.Contains(searchTerm)) ||
                        (r.AccountNo != null && r.AccountNo.Contains(searchTerm)) ||
                        r.ExternalReference.ToLower().Contains(searchTerm) ||
                        (r.RegId != null && r.RegId.ToLower().Contains(searchTerm)));
                }

                // Get total count for pagination
                var totalCount = await baseQuery.CountAsync();

                // Apply pagination and includes
                var items = await baseQuery
                    .Include(r => r.Branch)
                    .Include(r => r.GeneratedByUser)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var pagedResult = Models.Responses.PagedResult<KbankOddRegistration>.Create(items, page, pageSize, totalCount);

                _logger.LogDebug("Retrieved {ItemCount} registrations out of {TotalCount} total", 
                    items.Count, totalCount);

                return Result<Models.Responses.PagedResult<KbankOddRegistration>>.Success(pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged registrations");
                return Result<Models.Responses.PagedResult<KbankOddRegistration>>.Failure($"Failed to retrieve registrations: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a single registration by its ID
        /// </summary>
        /// <param name="id">Registration ID</param>
        /// <returns>Result containing the registration or failure if not found</returns>
        public async Task<Result<KbankOddRegistration>> GetByIdAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return Result<KbankOddRegistration>.Failure("Invalid registration ID");
                }

                var registration = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .Where(r => r.Id == id)
                    .Include(r => r.Branch)
                    .Include(r => r.GeneratedByUser)
                    .FirstOrDefaultAsync();

                if (registration == null)
                {
                    _logger.LogWarning("Registration with ID {Id} not found", id);
                    return Result<KbankOddRegistration>.Failure($"Registration with ID {id} not found");
                }

                return Result<KbankOddRegistration>.Success(registration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registration by ID {Id}", id);
                return Result<KbankOddRegistration>.Failure($"Failed to retrieve registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves the most recent registrations
        /// </summary>
        /// <param name="count">Number of recent registrations to retrieve</param>
        /// <returns>Result containing list of recent registrations</returns>
        public async Task<Result<IEnumerable<KbankOddRegistration>>> GetRecentAsync(int count = 10)
        {
            try
            {
                if (count <= 0 || count > 100) count = 10;

                var registrations = await _unitOfWork.KbankOddRegistrations
                    .Query()
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(count)
                    .Include(r => r.Branch)
                    .Include(r => r.GeneratedByUser)
                    .ToListAsync();

                _logger.LogDebug("Retrieved {Count} recent registrations", registrations.Count);

                return Result<IEnumerable<KbankOddRegistration>>.Success(registrations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent registrations");
                return Result<IEnumerable<KbankOddRegistration>>.Failure($"Failed to retrieve recent registrations: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves all registrations for data export purposes
        /// </summary>
        /// <param name="status">Optional status filter</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>Result containing all matching registrations</returns>
        public async Task<Result<IEnumerable<KbankOddRegistration>>> GetForExportAsync(string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                _logger.LogInformation("Retrieving registrations for export: status={Status}, fromDate={FromDate}, toDate={ToDate}", 
                    status, fromDate, toDate);

                var baseQuery = _unitOfWork.KbankOddRegistrations.Query();

                // Apply status filter
                if (!string.IsNullOrWhiteSpace(status))
                {
                    baseQuery = baseQuery.Where(r => r.Status == status);
                }

                // Apply date range filters
                if (fromDate.HasValue)
                {
                    baseQuery = baseQuery.Where(r => r.CreatedAt >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    baseQuery = baseQuery.Where(r => r.CreatedAt <= endOfDay);
                }

                var registrations = await baseQuery
                    .Include(r => r.Branch)
                    .Include(r => r.GeneratedByUser)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} registrations for export", registrations.Count);

                return Result<IEnumerable<KbankOddRegistration>>.Success(registrations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving registrations for export");
                return Result<IEnumerable<KbankOddRegistration>>.Failure($"Failed to retrieve registrations for export: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates comprehensive statistics for registrations
        /// </summary>
        /// <param name="fromDate">Optional start date for statistics calculation</param>
        /// <param name="toDate">Optional end date for statistics calculation</param>
        /// <returns>Result containing registration statistics</returns>
        public async Task<Result<RegistrationStatistics>> GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var now = _dateTimeProvider.UtcNow;

                // Set default date range if not provided
                if (!fromDate.HasValue)
                {
                    fromDate = now.AddDays(-30); // Last 30 days
                }

                if (!toDate.HasValue)
                {
                    toDate = now;
                }

                _logger.LogDebug("Generating statistics from {FromDate} to {ToDate}", fromDate, toDate);

                var baseQuery = _unitOfWork.KbankOddRegistrations
                    .Query();

                var dateRangeQuery = baseQuery
                    .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate);

                // Calculate basic counts
                var totalRegistrations = await dateRangeQuery.CountAsync();
                var successfulRegistrations = await dateRangeQuery.CountAsync(r => r.Status == "Success");
                var failedRegistrations = await dateRangeQuery.CountAsync(r => r.Status == "Fail");
                var pendingRegistrations = await dateRangeQuery.CountAsync(r => r.Status == "Pending");

                // Calculate time-based counts
                var today = now.Date;
                var weekStart = today.AddDays(-(int)today.DayOfWeek);
                var monthStart = new DateTime(today.Year, today.Month, 1);

                var todayRegistrations = await baseQuery.CountAsync(r => r.CreatedAt.Date == today);
                var weekRegistrations = await baseQuery.CountAsync(r => r.CreatedAt >= weekStart);
                var monthRegistrations = await baseQuery.CountAsync(r => r.CreatedAt >= monthStart);

                // Status counts
                var statusCounts = await dateRangeQuery
                    .Where(r => !string.IsNullOrEmpty(r.Status))
                    .GroupBy(r => r.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Status, x => x.Count);

                // Branch counts
                var branchCounts = await dateRangeQuery
                    .Include(r => r.Branch)
                    .Where(r => r.Branch != null)
                    .GroupBy(r => r.Branch!.Name)
                    .Select(g => new { BranchName = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.BranchName, x => x.Count);

                // Daily counts for the last 30 days
                var last30Days = now.AddDays(-30);
                var dailyCountsData = await baseQuery
                    .Where(r => r.CreatedAt >= last30Days)
                    .GroupBy(r => r.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync();

                var dailyCounts = new Dictionary<DateTime, int>();
                for (var date = last30Days.Date; date <= now.Date; date = date.AddDays(1))
                {
                    var dayCount = dailyCountsData.FirstOrDefault(d => d.Date == date)?.Count ?? 0;
                    dailyCounts[date] = dayCount;
                }

                // Calculate average processing time
                double? averageProcessingTime = null;
                var completedRegistrations = await dateRangeQuery
                    .Where(r => r.Status == "Success" && r.UpdatedAt.HasValue)
                    .Select(r => new { r.CreatedAt, r.UpdatedAt })
                    .ToListAsync();

                if (completedRegistrations.Any())
                {
                    var processingTimes = completedRegistrations
                        .Select(r => (r.UpdatedAt!.Value - r.CreatedAt).TotalMinutes)
                        .Where(minutes => minutes > 0);

                    if (processingTimes.Any())
                    {
                        averageProcessingTime = Math.Round(processingTimes.Average(), 2);
                    }
                }

                // Get last registration timestamp
                var lastRegistrationAt = await baseQuery
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => r.CreatedAt)
                    .FirstOrDefaultAsync();

                var statistics = new RegistrationStatistics
                {
                    TotalRegistrations = totalRegistrations,
                    SuccessfulRegistrations = successfulRegistrations,
                    FailedRegistrations = failedRegistrations,
                    PendingRegistrations = pendingRegistrations,
                    TodayRegistrations = todayRegistrations,
                    WeekRegistrations = weekRegistrations,
                    MonthRegistrations = monthRegistrations,
                    StatusCounts = statusCounts,
                    BranchCounts = branchCounts,
                    DailyCounts = dailyCounts,
                    AverageProcessingTimeMinutes = averageProcessingTime,
                    LastRegistrationAt = lastRegistrationAt == default ? null : lastRegistrationAt,
                    FromDate = fromDate,
                    ToDate = toDate,
                    GeneratedAt = now
                };

                _logger.LogInformation("Generated statistics: {TotalRegistrations} total, {SuccessfulRegistrations} successful, {FailedRegistrations} failed", 
                    totalRegistrations, successfulRegistrations, failedRegistrations);

                return Result<RegistrationStatistics>.Success(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating registration statistics");
                return Result<RegistrationStatistics>.Failure($"Failed to generate statistics: {ex.Message}");
            }
        }

        /// <summary>
        /// Searches registrations with advanced filtering (API compatible method)
        /// </summary>
        /// <param name="page">Page number</param>
        /// <param name="pageSize">Items per page</param>
        /// <param name="status">Status filter</param>
        /// <param name="search">Search term</param>
        /// <param name="fromDate">Start date filter</param>
        /// <param name="toDate">End date filter</param>
        /// <returns>Result with paged search results</returns>
        public async Task<Result<PagedResult<KbankOddRegistration>>> SearchRegistrationsAsync(
            int page = 1, 
            int pageSize = 20, 
            string? status = null, 
            string? search = null, 
            DateTime? fromDate = null, 
            DateTime? toDate = null)
        {
            try
            {
                // Validate parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 1000) pageSize = 20;

                _logger.LogDebug("Searching registrations: page={Page}, pageSize={PageSize}, status={Status}, search={Search}, fromDate={FromDate}, toDate={ToDate}", 
                    page, pageSize, status, search, fromDate, toDate);

                var baseQuery = _unitOfWork.KbankOddRegistrations.Query();

                // Apply status filter
                if (!string.IsNullOrWhiteSpace(status))
                {
                    baseQuery = baseQuery.Where(r => r.Status == status);
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var searchTerm = search.Trim().ToLower();
                    baseQuery = baseQuery.Where(r => 
                        (r.FullName != null && r.FullName.ToLower().Contains(searchTerm)) ||
                        (r.MobileNo != null && r.MobileNo.Contains(searchTerm)) ||
                        (r.AccountNo != null && r.AccountNo.Contains(searchTerm)) ||
                        r.ExternalReference.ToLower().Contains(searchTerm) ||
                        (r.RegId != null && r.RegId.ToLower().Contains(searchTerm)));
                }

                // Apply date range filters
                if (fromDate.HasValue)
                {
                    baseQuery = baseQuery.Where(r => r.CreatedAt >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    baseQuery = baseQuery.Where(r => r.CreatedAt <= endOfDay);
                }

                // Get total count for pagination
                var totalCount = await baseQuery.CountAsync();

                // Apply pagination and includes
                var items = await baseQuery
                    .Include(r => r.Branch)
                    .Include(r => r.GeneratedByUser)
                    .OrderByDescending(r => r.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var pagedResult = PagedResult<KbankOddRegistration>.Create(items, page, pageSize, totalCount);

                _logger.LogDebug("Found {ItemCount} registrations out of {TotalCount} total matching search criteria", 
                    items.Count, totalCount);

                return Result<PagedResult<KbankOddRegistration>>.Success(pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching registrations");
                return Result<PagedResult<KbankOddRegistration>>.Failure($"Failed to search registrations: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets registration by ID (API compatible method)
        /// </summary>
        /// <param name="id">Registration ID</param>
        /// <returns>Result with registration data</returns>
        public async Task<Result<KbankOddRegistration>> GetRegistrationByIdAsync(int id)
        {
            return await GetByIdAsync(id);
        }

        /// <summary>
        /// Gets registration statistics (API compatible method)
        /// </summary>
        /// <param name="fromDate">Start date</param>
        /// <param name="toDate">End date</param>
        /// <returns>Result with statistics</returns>
        public async Task<Result<RegistrationStatistics>> GetRegistrationStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            return await GetStatisticsAsync(fromDate, toDate);
        }

        /// <summary>
        /// Exports registrations data
        /// </summary>
        /// <param name="format">Export format</param>
        /// <param name="status">Status filter</param>
        /// <param name="fromDate">Start date filter</param>
        /// <param name="toDate">End date filter</param>
        /// <returns>Result with exported data</returns>
        public async Task<Result<byte[]>> ExportRegistrationsAsync(string format = "csv", string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                _logger.LogInformation("Exporting registrations: format={Format}, status={Status}, fromDate={FromDate}, toDate={ToDate}", 
                    format, status, fromDate, toDate);

                var exportResult = await GetForExportAsync(status, fromDate, toDate);
                if (!exportResult.IsSuccess)
                {
                    return Result<byte[]>.Failure(exportResult.ErrorMessage);
                }

                var registrations = exportResult.Data.ToList();
                byte[] exportData;

                switch (format.ToLower())
                {
                    case "csv":
                        exportData = ExportToCsv(registrations);
                        break;
                    case "json":
                        exportData = ExportToJson(registrations);
                        break;
                    default:
                        return Result<byte[]>.Failure($"Unsupported export format: {format}");
                }

                _logger.LogInformation("Exported {Count} registrations in {Format} format", registrations.Count, format);
                return Result<byte[]>.Success(exportData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting registrations");
                return Result<byte[]>.Failure($"Failed to export registrations: {ex.Message}");
            }
        }

        private byte[] ExportToCsv(List<KbankOddRegistration> registrations)
        {
            var lines = new List<string>
            {
                "Id,ExternalReference,RegId,Status,FullName,MobileNo,AccountNo,Branch,CreatedAt,UpdatedAt,OtacCode,GeneratedByUser"
            };

            foreach (var reg in registrations)
            {
                var line = $"{reg.Id}," +
                          $"\"{reg.ExternalReference?.Replace("\"", "\"\"")}\"," +
                          $"\"{reg.RegId?.Replace("\"", "\"\"")}\"," +
                          $"\"{reg.Status?.Replace("\"", "\"\"")}\"," +
                          $"\"{reg.FullName?.Replace("\"", "\"\"")}\"," +
                          $"\"{reg.MobileNo?.Replace("\"", "\"\"")}\"," +
                          $"\"{reg.AccountNo?.Replace("\"", "\"\"")}\"," +
                          $"\"{reg.Branch?.Name?.Replace("\"", "\"\"")}\"," +
                          $"{reg.CreatedAt:yyyy-MM-dd HH:mm:ss}," +
                          $"{reg.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss")}," +
                          $"\"{reg.OtacCode?.Replace("\"", "\"\"")}\"," +
                          $"\"{reg.GeneratedByUser?.Username?.Replace("\"", "\"\"")}\"";
                
                lines.Add(line);
            }

            var csvContent = string.Join("\r\n", lines);
            return System.Text.Encoding.UTF8.GetBytes(csvContent);
        }

        private byte[] ExportToJson(List<KbankOddRegistration> registrations)
        {
            var exportData = registrations.Select(reg => new
            {
                reg.Id,
                reg.ExternalReference,
                reg.RegId,
                reg.Status,
                reg.FullName,
                reg.MobileNo,
                reg.AccountNo,
                Branch = reg.Branch?.Name,
                reg.CreatedAt,
                reg.UpdatedAt,
                reg.OtacCode,
                GeneratedByUser = reg.GeneratedByUser?.Username
            });

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            return System.Text.Encoding.UTF8.GetBytes(json);
        }
    }
}