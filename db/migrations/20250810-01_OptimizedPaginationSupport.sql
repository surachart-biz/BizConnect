-- ====================================================================
-- BizConnect Optimized Pagination Support Migration
-- Migration: 20250810-01_OptimizedPaginationSupport.sql
-- Date: 2025-08-10
-- Description: Add optimized pagination support for frontend UI
-- Purpose: Improve large dataset loading performance and user experience
-- ====================================================================

-- Enable error handling and logging
\set ON_ERROR_STOP on

BEGIN;

-- ====================================================================
-- SECTION 1: CURSOR-BASED PAGINATION FUNCTIONS
-- ====================================================================

-- Optimized pagination function for registration list
CREATE OR REPLACE FUNCTION get_registrations_paginated(
    page_size integer DEFAULT 20,
    cursor_id integer DEFAULT 0,
    filter_status text DEFAULT NULL,
    filter_branch_id integer DEFAULT NULL,
    filter_date_from timestamp with time zone DEFAULT NULL,
    filter_date_to timestamp with time zone DEFAULT NULL
)
RETURNS TABLE(
    id integer,
    external_reference text,
    otac_code text,
    otac_state text,
    status text,
    created_at timestamp with time zone,
    updated_at timestamp with time zone,
    branch_name_en text,
    branch_name_th text,
    branch_code text,
    generated_by_username text,
    activity_type text,
    priority_sort integer,
    total_count bigint,
    has_next_page boolean,
    next_cursor integer
)
LANGUAGE sql
STABLE
PARALLEL SAFE
AS $$
    WITH filtered_data AS (
        SELECT 
            ra.id,
            ra.external_reference,
            ra.otac_code,
            ra.otac_state,
            ra.status,
            ra.created_at,
            ra.updated_at,
            ra.branch_name_en,
            ra.branch_name_th,
            ra.branch_code,
            ra.generated_by_username,
            ra.activity_type,
            ra.priority_sort,
            COUNT(*) OVER() as total_count
        FROM v_recent_activities ra
        WHERE 
            (cursor_id = 0 OR ra.id < cursor_id)
            AND (filter_status IS NULL OR ra.status = filter_status)
            AND (filter_branch_id IS NULL OR EXISTS (
                SELECT 1 FROM "Branch" b 
                WHERE b."BranchId" = filter_branch_id 
                AND (b."Code" = ra.branch_code OR b."NameEn" = ra.branch_name_en)
            ))
            AND (filter_date_from IS NULL OR ra.created_at >= filter_date_from)
            AND (filter_date_to IS NULL OR ra.created_at <= filter_date_to)
        ORDER BY ra.priority_sort ASC, ra.id DESC
        LIMIT page_size + 1
    )
    SELECT 
        fd.id,
        fd.external_reference,
        CASE 
            WHEN fd.otac_code IS NOT NULL THEN LEFT(fd.otac_code, 4) || '****'
            ELSE NULL
        END as otac_code,
        fd.otac_state,
        fd.status,
        fd.created_at,
        fd.updated_at,
        fd.branch_name_en,
        fd.branch_name_th,
        fd.branch_code,
        fd.generated_by_username,
        fd.activity_type,
        fd.priority_sort,
        COALESCE(MAX(fd.total_count), 0) as total_count,
        (COUNT(*) > page_size) as has_next_page,
        CASE 
            WHEN COUNT(*) > page_size THEN (
                SELECT MIN(sub.id) 
                FROM filtered_data sub 
                OFFSET page_size
            )
            ELSE NULL
        END as next_cursor
    FROM filtered_data fd
    WHERE rownum <= page_size OR (SELECT COUNT(*) FROM filtered_data) <= page_size
    GROUP BY 
        fd.id, fd.external_reference, fd.otac_code, fd.otac_state, 
        fd.status, fd.created_at, fd.updated_at, fd.branch_name_en, 
        fd.branch_name_th, fd.branch_code, fd.generated_by_username, 
        fd.activity_type, fd.priority_sort
    ORDER BY fd.priority_sort ASC, fd.id DESC
    LIMIT page_size;
$$;

-- Function for branch performance pagination
CREATE OR REPLACE FUNCTION get_branch_performance_paginated(
    page_size integer DEFAULT 10,
    cursor_branch_id integer DEFAULT 0,
    sort_by text DEFAULT 'total_registrations',
    sort_direction text DEFAULT 'desc'
)
RETURNS TABLE(
    branch_id integer,
    branch_code text,
    branch_name_en text,
    branch_name_th text,
    is_active boolean,
    total_registrations bigint,
    successful_registrations bigint,
    failed_registrations bigint,
    pending_registrations bigint,
    success_rate numeric,
    today_count bigint,
    week_count bigint,
    month_count bigint,
    avg_otac_attempts numeric,
    locked_otac_count bigint,
    total_count bigint,
    has_next_page boolean,
    next_cursor integer
)
LANGUAGE sql
STABLE
PARALLEL SAFE
AS $$
    WITH filtered_data AS (
        SELECT 
            bp."BranchId" as branch_id,
            bp.branch_code,
            bp.branch_name_en,
            bp.branch_name_th,
            bp.is_active,
            bp.total_registrations,
            bp.successful_registrations,
            bp.failed_registrations,
            bp.pending_registrations,
            bp.success_rate,
            bp.today_count,
            bp.week_count,
            bp.month_count,
            bp.avg_otac_attempts,
            bp.locked_otac_count,
            COUNT(*) OVER() as total_count
        FROM v_branch_performance bp
        WHERE 
            (cursor_branch_id = 0 OR (
                CASE 
                    WHEN sort_direction = 'desc' THEN bp."BranchId" < cursor_branch_id
                    ELSE bp."BranchId" > cursor_branch_id
                END
            ))
        ORDER BY 
            CASE 
                WHEN sort_by = 'total_registrations' AND sort_direction = 'desc' THEN bp.total_registrations
                WHEN sort_by = 'success_rate' AND sort_direction = 'desc' THEN bp.success_rate::bigint
                ELSE bp."BranchId"
            END DESC,
            bp."BranchId"
        LIMIT page_size + 1
    )
    SELECT 
        fd.branch_id,
        fd.branch_code,
        fd.branch_name_en,
        fd.branch_name_th,
        fd.is_active,
        fd.total_registrations,
        fd.successful_registrations,
        fd.failed_registrations,
        fd.pending_registrations,
        fd.success_rate,
        fd.today_count,
        fd.week_count,
        fd.month_count,
        fd.avg_otac_attempts,
        fd.locked_otac_count,
        COALESCE(MAX(fd.total_count), 0) as total_count,
        (COUNT(*) > page_size) as has_next_page,
        CASE 
            WHEN COUNT(*) > page_size THEN (
                SELECT fd2.branch_id 
                FROM filtered_data fd2 
                OFFSET page_size 
                LIMIT 1
            )
            ELSE NULL
        END as next_cursor
    FROM filtered_data fd
    WHERE rownum <= page_size OR (SELECT COUNT(*) FROM filtered_data) <= page_size
    GROUP BY 
        fd.branch_id, fd.branch_code, fd.branch_name_en, fd.branch_name_th,
        fd.is_active, fd.total_registrations, fd.successful_registrations,
        fd.failed_registrations, fd.pending_registrations, fd.success_rate,
        fd.today_count, fd.week_count, fd.month_count, fd.avg_otac_attempts,
        fd.locked_otac_count
    ORDER BY 
        CASE 
            WHEN sort_by = 'total_registrations' AND sort_direction = 'desc' THEN fd.total_registrations
            WHEN sort_by = 'success_rate' AND sort_direction = 'desc' THEN fd.success_rate::bigint
            ELSE fd.branch_id
        END DESC,
        fd.branch_id
    LIMIT page_size;
$$;

-- ====================================================================
-- SECTION 2: SEARCH AND FILTERING OPTIMIZATION
-- ====================================================================

-- Full-text search index for registration data
CREATE INDEX IF NOT EXISTS idx_kbank_fulltext_search 
ON "KbankOddRegistration" 
USING gin(to_tsvector('english', 
    COALESCE("ExternalReference", '') || ' ' ||
    COALESCE("FullName", '') || ' ' ||
    COALESCE("AccountNo", '') || ' ' ||
    COALESCE("IdValue", '')
));

-- Search function with highlighting
CREATE OR REPLACE FUNCTION search_registrations(
    search_query text,
    page_size integer DEFAULT 20,
    cursor_id integer DEFAULT 0
)
RETURNS TABLE(
    id integer,
    external_reference text,
    full_name text,
    account_no text,
    id_value text,
    status text,
    otac_state text,
    created_at timestamp with time zone,
    branch_name text,
    search_rank real,
    highlight text,
    total_count bigint,
    has_next_page boolean,
    next_cursor integer
)
LANGUAGE sql
STABLE
PARALLEL SAFE
AS $$
    WITH search_results AS (
        SELECT 
            k."Id",
            k."ExternalReference",
            k."FullName",
            k."AccountNo",
            k."IdValue",
            k."Status",
            k."OtacState",
            k."CreatedAt",
            COALESCE(b."NameEn", b."Name") as branch_name,
            ts_rank(
                to_tsvector('english', 
                    COALESCE(k."ExternalReference", '') || ' ' ||
                    COALESCE(k."FullName", '') || ' ' ||
                    COALESCE(k."AccountNo", '') || ' ' ||
                    COALESCE(k."IdValue", '')
                ),
                plainto_tsquery('english', search_query)
            ) as search_rank,
            ts_headline('english',
                COALESCE(k."FullName", k."ExternalReference", k."AccountNo", ''),
                plainto_tsquery('english', search_query),
                'MaxWords=10, MinWords=3, ShortWord=3, HighlightAll=false'
            ) as highlight,
            COUNT(*) OVER() as total_count
        FROM "KbankOddRegistration" k
        LEFT JOIN "Branch" b ON b."BranchId" = k."BranchId"
        WHERE 
            (cursor_id = 0 OR k."Id" < cursor_id)
            AND to_tsvector('english', 
                COALESCE(k."ExternalReference", '') || ' ' ||
                COALESCE(k."FullName", '') || ' ' ||
                COALESCE(k."AccountNo", '') || ' ' ||
                COALESCE(k."IdValue", '')
            ) @@ plainto_tsquery('english', search_query)
        ORDER BY search_rank DESC, k."Id" DESC
        LIMIT page_size + 1
    )
    SELECT 
        sr.id,
        sr.external_reference,
        sr.full_name,
        sr.account_no,
        -- Mask sensitive data
        CASE 
            WHEN sr.id_value IS NOT NULL THEN 
                LEFT(sr.id_value, 2) || '****' || RIGHT(sr.id_value, 2)
            ELSE NULL
        END as id_value,
        sr.status,
        sr.otac_state,
        sr.created_at,
        sr.branch_name,
        sr.search_rank,
        sr.highlight,
        COALESCE(MAX(sr.total_count), 0) as total_count,
        (COUNT(*) > page_size) as has_next_page,
        CASE 
            WHEN COUNT(*) > page_size THEN (
                SELECT MIN(sr2.id) 
                FROM search_results sr2 
                OFFSET page_size
            )
            ELSE NULL
        END as next_cursor
    FROM search_results sr
    WHERE rownum <= page_size OR (SELECT COUNT(*) FROM search_results) <= page_size
    GROUP BY 
        sr.id, sr.external_reference, sr.full_name, sr.account_no, 
        sr.id_value, sr.status, sr.otac_state, sr.created_at, 
        sr.branch_name, sr.search_rank, sr.highlight
    ORDER BY sr.search_rank DESC, sr.id DESC
    LIMIT page_size;
$$;

-- ====================================================================
-- SECTION 3: CACHING AND PERFORMANCE OPTIMIZATION
-- ====================================================================

-- Create cached statistics table for expensive queries
CREATE TABLE IF NOT EXISTS "_CachedStatistics" (
    "Key" VARCHAR(100) PRIMARY KEY,
    "Value" JSONB NOT NULL,
    "ExpiresAt" TIMESTAMPTZ NOT NULL,
    "CreatedAt" TIMESTAMPTZ DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ DEFAULT NOW()
);

-- Create index for cache expiration cleanup
CREATE INDEX IF NOT EXISTS idx_cached_statistics_expires 
ON "_CachedStatistics"("ExpiresAt");

-- Function to get or refresh cached statistics
CREATE OR REPLACE FUNCTION get_cached_dashboard_stats()
RETURNS jsonb
LANGUAGE plpgsql
AS $$
DECLARE
    cached_result jsonb;
    cache_key text := 'dashboard_stats';
    cache_duration interval := '5 minutes';
BEGIN
    -- Try to get from cache
    SELECT "Value" INTO cached_result
    FROM "_CachedStatistics"
    WHERE "Key" = cache_key
    AND "ExpiresAt" > NOW();
    
    -- If not cached or expired, generate new
    IF cached_result IS NULL THEN
        SELECT row_to_json(v)::jsonb INTO cached_result 
        FROM v_realtime_dashboard_stats v;
        
        -- Store in cache
        INSERT INTO "_CachedStatistics" ("Key", "Value", "ExpiresAt")
        VALUES (cache_key, cached_result, NOW() + cache_duration)
        ON CONFLICT ("Key") DO UPDATE SET
            "Value" = EXCLUDED."Value",
            "ExpiresAt" = EXCLUDED."ExpiresAt",
            "UpdatedAt" = NOW();
    END IF;
    
    RETURN cached_result;
END;
$$;

-- Background job function to clean expired cache entries
CREATE OR REPLACE FUNCTION cleanup_expired_cache()
RETURNS void
LANGUAGE sql
AS $$
    DELETE FROM "_CachedStatistics" 
    WHERE "ExpiresAt" <= NOW();
$$;

-- ====================================================================
-- SECTION 4: REAL-TIME UPDATE TRIGGERS
-- ====================================================================

-- Function to invalidate cache when data changes
CREATE OR REPLACE FUNCTION invalidate_dashboard_cache()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    -- Remove dashboard cache entries
    DELETE FROM "_CachedStatistics" 
    WHERE "Key" LIKE 'dashboard%' OR "Key" LIKE 'stats%';
    
    RETURN COALESCE(NEW, OLD);
END;
$$;

-- Create triggers for cache invalidation
DROP TRIGGER IF EXISTS trigger_invalidate_cache_on_registration_change 
ON "KbankOddRegistration";

CREATE TRIGGER trigger_invalidate_cache_on_registration_change
    AFTER INSERT OR UPDATE OR DELETE ON "KbankOddRegistration"
    FOR EACH STATEMENT
    EXECUTE FUNCTION invalidate_dashboard_cache();

-- ====================================================================
-- SECTION 5: VALIDATION AND COMMENTS
-- ====================================================================

-- Add comprehensive comments
COMMENT ON FUNCTION get_registrations_paginated IS 'Optimized cursor-based pagination for registration lists with filtering support';
COMMENT ON FUNCTION get_branch_performance_paginated IS 'Paginated branch performance data with sorting options';
COMMENT ON FUNCTION search_registrations IS 'Full-text search with highlighting and pagination for registrations';
COMMENT ON FUNCTION get_cached_dashboard_stats IS 'Cached dashboard statistics with automatic refresh';
COMMENT ON FUNCTION cleanup_expired_cache IS 'Maintenance function to clean expired cache entries';
COMMENT ON TABLE "_CachedStatistics" IS 'Application-level cache for expensive query results';

-- Record this migration
INSERT INTO "_SchemaVersion" ("Filename") VALUES ('20250810-01_OptimizedPaginationSupport.sql')
ON CONFLICT ("Filename") DO NOTHING;

COMMIT;

-- ====================================================================
-- POST-MIGRATION INSTRUCTIONS
-- ====================================================================
/*
FRONTEND INTEGRATION RECOMMENDATIONS:

1. Use cursor-based pagination instead of offset-based:
   - Better performance for large datasets
   - Consistent results during concurrent updates
   - Built-in filtering and sorting

2. Implement caching in service layer:
   - Use get_cached_dashboard_stats() for dashboard data
   - Cache invalidation handled automatically
   - 5-minute cache duration balances performance and freshness

3. Search functionality:
   - Use search_registrations() for user search queries
   - Built-in highlighting for search results
   - Automatic data masking for security

4. Performance monitoring:
   - All functions include query performance optimizations
   - Parallel execution where safe
   - Proper indexing for filter conditions

5. Service layer updates needed:
   - Update RegistrationQueryService to use new pagination functions
   - Implement cursor tracking in frontend state management
   - Add search functionality to admin controllers

EXAMPLE SERVICE USAGE:
```csharp
public async Task<PagedResult<RegistrationDto>> GetRegistrationsPaginatedAsync(
    int pageSize = 20, 
    int? cursor = null, 
    string? statusFilter = null)
{
    var results = await _context.Database
        .SqlQueryRaw<PaginatedRegistrationResult>(
            "SELECT * FROM get_registrations_paginated(@p0, @p1, @p2)",
            pageSize, cursor ?? 0, statusFilter)
        .ToListAsync();
    
    return new PagedResult<RegistrationDto>
    {
        Data = results.Select(MapToDto).ToList(),
        HasNextPage = results.FirstOrDefault()?.HasNextPage ?? false,
        NextCursor = results.FirstOrDefault()?.NextCursor,
        TotalCount = results.FirstOrDefault()?.TotalCount ?? 0
    };
}
```
*/