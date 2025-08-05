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
        /// <param name="language">Language code for localization (optional, defaults to "en")</param>
        /// <returns>Result containing paged registration data</returns>
        Task<Result<Models.Responses.PagedResult<KbankOddRegistration>>> GetPagedAsync(int page = 1, int pageSize = 20, string? status = null, string? search = null, string language = "en");

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
        Task<Result<PagedResult<KbankOddRegistration>>> SearchRegistrationsAsync(
            int page = 1, 
            int pageSize = 20, 
            string? status = null, 
            string? search = null, 
            DateTime? fromDate = null, 
            DateTime? toDate = null);

        /// <summary>
        /// Gets registration by ID (API compatible method)
        /// </summary>
        /// <param name="id">Registration ID</param>
        /// <returns>Result with registration data</returns>
        Task<Result<KbankOddRegistration>> GetRegistrationByIdAsync(int id);

        /// <summary>
        /// Gets registration statistics (API compatible method)
        /// </summary>
        /// <param name="fromDate">Start date</param>
        /// <param name="toDate">End date</param>
        /// <returns>Result with statistics</returns>
        Task<Result<RegistrationStatistics>> GetRegistrationStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);

        /// <summary>
        /// Exports registrations data
        /// </summary>
        /// <param name="format">Export format</param>
        /// <param name="status">Status filter</param>
        /// <param name="fromDate">Start date filter</param>
        /// <param name="toDate">End date filter</param>
        /// <returns>Result with exported data</returns>
        Task<Result<byte[]>> ExportRegistrationsAsync(string format = "csv", string? status = null, DateTime? fromDate = null, DateTime? toDate = null);
    }
}