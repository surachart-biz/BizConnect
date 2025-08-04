using System.Text.Json.Serialization;

namespace BizConnect.Models.Api;

/// <summary>
/// Generic paged result container for API endpoints that return lists of data.
/// Provides pagination metadata along with the actual data items.
/// </summary>
/// <typeparam name="T">The type of items in the paged result</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The items for the current page
    /// </summary>
    [JsonPropertyName("items")]
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

    /// <summary>
    /// The current page number (1-based)
    /// </summary>
    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; set; }

    /// <summary>
    /// The number of items per page
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    /// <summary>
    /// The total number of items across all pages
    /// </summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    /// <summary>
    /// The total number of pages
    /// </summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;

    /// <summary>
    /// Indicates whether there is a previous page
    /// </summary>
    [JsonPropertyName("hasPrevious")]
    public bool HasPrevious => CurrentPage > 1;

    /// <summary>
    /// Indicates whether there is a next page
    /// </summary>
    [JsonPropertyName("hasNext")]
    public bool HasNext => CurrentPage < TotalPages;

    /// <summary>
    /// The number of items on the current page
    /// </summary>
    [JsonPropertyName("currentPageSize")]
    public int CurrentPageSize => Items.Count();

    /// <summary>
    /// The starting index of the first item on the current page (1-based)
    /// </summary>
    [JsonPropertyName("startIndex")]
    public int StartIndex => PageSize > 0 ? ((CurrentPage - 1) * PageSize) + 1 : 0;

    /// <summary>
    /// The ending index of the last item on the current page (1-based)
    /// </summary>
    [JsonPropertyName("endIndex")]
    public int EndIndex => StartIndex + CurrentPageSize - 1;

    /// <summary>
    /// Creates an empty paged result
    /// </summary>
    public PagedResult()
    {
    }

    /// <summary>
    /// Creates a paged result with the specified parameters
    /// </summary>
    public PagedResult(IEnumerable<T> items, int currentPage, int pageSize, int totalItems)
    {
        Items = items;
        CurrentPage = currentPage;
        PageSize = pageSize;
        TotalItems = totalItems;
    }

    /// <summary>
    /// Creates a paged result from a full list of items, extracting the appropriate page
    /// </summary>
    public static PagedResult<T> FromList(IEnumerable<T> allItems, int page, int pageSize)
    {
        var itemsList = allItems.ToList();
        var totalItems = itemsList.Count;
        var pagedItems = itemsList.Skip((page - 1) * pageSize).Take(pageSize);

        return new PagedResult<T>(pagedItems, page, pageSize, totalItems);
    }

    /// <summary>
    /// Creates an empty paged result with the specified pagination parameters
    /// </summary>
    public static PagedResult<T> Empty(int page = 1, int pageSize = 10)
    {
        return new PagedResult<T>(Enumerable.Empty<T>(), page, pageSize, 0);
    }
}

/// <summary>
/// Pagination request parameters for API endpoints
/// </summary>
public class PaginationRequest
{
    /// <summary>
    /// The page number to retrieve (1-based, default: 1)
    /// </summary>
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// The number of items per page (default: 10, max: 100)
    /// </summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Optional search term to filter results
    /// </summary>
    [JsonPropertyName("search")]
    public string? Search { get; set; }

    /// <summary>
    /// Optional sort field name
    /// </summary>
    [JsonPropertyName("sortBy")]
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction (asc or desc, default: asc)
    /// </summary>
    [JsonPropertyName("sortDirection")]
    public string SortDirection { get; set; } = "asc";

    /// <summary>
    /// Validates and normalizes the pagination parameters
    /// </summary>
    public void Normalize()
    {
        // Ensure page is at least 1
        Page = Math.Max(1, Page);

        // Limit page size to reasonable bounds
        PageSize = Math.Max(1, Math.Min(100, PageSize));

        // Normalize sort direction
        SortDirection = SortDirection?.ToLowerInvariant() switch
        {
            "desc" or "descending" or "down" => "desc",
            _ => "asc"
        };

        // Trim search term
        Search = Search?.Trim();
        if (string.IsNullOrEmpty(Search))
        {
            Search = null;
        }

        // Trim sort field
        SortBy = SortBy?.Trim();
        if (string.IsNullOrEmpty(SortBy))
        {
            SortBy = null;
        }
    }

    /// <summary>
    /// Gets the number of items to skip for the current page
    /// </summary>
    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Gets the number of items to take for the current page
    /// </summary>
    public int Take => PageSize;

    /// <summary>
    /// Indicates whether sorting is specified
    /// </summary>
    public bool HasSorting => !string.IsNullOrEmpty(SortBy);

    /// <summary>
    /// Indicates whether search filtering is specified
    /// </summary>
    public bool HasSearch => !string.IsNullOrEmpty(Search);

    /// <summary>
    /// Indicates whether sort direction is descending
    /// </summary>
    public bool IsDescending => SortDirection == "desc";
}

/// <summary>
/// API response wrapper for paged results
/// </summary>
/// <typeparam name="T">The type of items in the paged result</typeparam>
public class PagedApiResponse<T> : ApiResponse<PagedResult<T>>
{
    /// <summary>
    /// Creates a successful paged response
    /// </summary>
    public static PagedApiResponse<T> Ok(PagedResult<T> pagedResult, string? message = null)
    {
        return new PagedApiResponse<T>
        {
            Success = true,
            Data = pagedResult,
            Message = message,
            Metadata = new Dictionary<string, object>
            {
                ["pagination"] = new
                {
                    pagedResult.CurrentPage,
                    pagedResult.PageSize,
                    pagedResult.TotalItems,
                    pagedResult.TotalPages,
                    pagedResult.HasPrevious,
                    pagedResult.HasNext
                }
            }
        };
    }

    /// <summary>
    /// Creates an empty paged response
    /// </summary>
    public static PagedApiResponse<T> Empty(int page = 1, int pageSize = 10, string? message = "No results found")
    {
        return Ok(PagedResult<T>.Empty(page, pageSize), message);
    }
}