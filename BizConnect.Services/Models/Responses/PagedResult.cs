using System;
using System.Collections.Generic;

namespace BizConnect.Services.Models.Responses
{
    /// <summary>
    /// Generic paged result for data queries
    /// </summary>
    /// <typeparam name="T">Type of data being paged</typeparam>
    public class PagedResult<T> where T : class
    {
        /// <summary>
        /// Collection of items for the current page
        /// </summary>
        public IEnumerable<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Current page number (1-based)
        /// </summary>
        public int CurrentPage { get; set; }

        /// <summary>
        /// Number of items per page
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of items across all pages
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        /// <summary>
        /// Whether there is a previous page
        /// </summary>
        public bool HasPreviousPage => CurrentPage > 1;

        /// <summary>
        /// Whether there is a next page
        /// </summary>
        public bool HasNextPage => CurrentPage < TotalPages;

        /// <summary>
        /// Index of first item on current page (1-based)
        /// </summary>
        public int FirstItemIndex => (CurrentPage - 1) * PageSize + 1;

        /// <summary>
        /// Index of last item on current page (1-based)
        /// </summary>
        public int LastItemIndex => Math.Min(CurrentPage * PageSize, TotalCount);

        /// <summary>
        /// Creates an empty paged result
        /// </summary>
        public static PagedResult<T> Empty(int page = 1, int pageSize = 10)
        {
            return new PagedResult<T>
            {
                Items = new List<T>(),
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = 0
            };
        }

        /// <summary>
        /// Creates a paged result with data
        /// </summary>
        public static PagedResult<T> Create(IEnumerable<T> items, int currentPage, int pageSize, int totalCount)
        {
            return new PagedResult<T>
            {
                Items = items ?? new List<T>(),
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }
    }
}