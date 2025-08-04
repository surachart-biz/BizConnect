using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BizConnect.Dal.Models;
using BizConnect.Services.Models.Responses;
using BizConnect.Services.Models.Results;

namespace BizConnect.Services.Interfaces
{
    /// <summary>
    /// Service interface for querying KBank ODD registration data
    /// Handles read-only operations, reporting, and data export functionality
    /// </summary>
    public interface IRegistrationQueryService
    {
        /// <summary>
        /// Retrieves a paginated list of registrations with optional filtering
        /// </summary>
        /// <param name="page">Page number (1-based)</param>
        /// <param name="pageSize">Number of items per page</param>
        /// <param name="status">Optional status filter</param>
        /// <param name="search">Optional search term for full name, mobile, or account number</param>
        /// <returns>Result containing paged registration data</returns>
        Task<Result<Models.Responses.PagedResult<KbankOddRegistration>>> GetPagedAsync(int page = 1, int pageSize = 20, string? status = null, string? search = null);

        /// <summary>
        /// Retrieves a single registration by its ID
        /// </summary>
        /// <param name="id">Registration ID</param>
        /// <returns>Result containing the registration or failure if not found</returns>
        Task<Result<KbankOddRegistration>> GetByIdAsync(int id);

        /// <summary>
        /// Retrieves the most recent registrations
        /// </summary>
        /// <param name="count">Number of recent registrations to retrieve</param>
        /// <returns>Result containing list of recent registrations</returns>
        Task<Result<IEnumerable<KbankOddRegistration>>> GetRecentAsync(int count = 10);

        /// <summary>
        /// Retrieves all registrations for data export purposes
        /// </summary>
        /// <param name="status">Optional status filter</param>
        /// <param name="fromDate">Optional start date filter</param>
        /// <param name="toDate">Optional end date filter</param>
        /// <returns>Result containing all matching registrations</returns>
        Task<Result<IEnumerable<KbankOddRegistration>>> GetForExportAsync(string? status = null, DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Generates comprehensive statistics for registrations
        /// </summary>
        /// <param name="fromDate">Optional start date for statistics calculation</param>
        /// <param name="toDate">Optional end date for statistics calculation</param>
        /// <returns>Result containing registration statistics</returns>
        Task<Result<RegistrationStatistics>> GetStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);
    }
}