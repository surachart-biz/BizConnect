-- =============================================================================
-- Simple Modern UI Database Optimizations
-- Date: 2025-08-06
-- Purpose: Essential indexes and views for modern UI performance
-- =============================================================================

BEGIN;

-- Essential performance indexes
CREATE INDEX IF NOT EXISTS "idx_kbank_created_status" 
ON "KbankOddRegistration" ("CreatedAt", "Status");

CREATE INDEX IF NOT EXISTS "idx_kbank_otac_state" 
ON "KbankOddRegistration" ("OtacState", "CreatedAt");

CREATE INDEX IF NOT EXISTS "idx_kbank_branch_date" 
ON "KbankOddRegistration" ("BranchId", "CreatedAt");

-- Simple dashboard statistics view
CREATE OR REPLACE VIEW "v_dashboard_stats" AS
SELECT 
    -- Today's statistics
    COUNT(*) FILTER (WHERE DATE("CreatedAt") = CURRENT_DATE) as "today_total",
    COUNT(*) FILTER (WHERE DATE("CreatedAt") = CURRENT_DATE AND "Status" = 'Success') as "today_success",
    COUNT(*) FILTER (WHERE DATE("CreatedAt") = CURRENT_DATE AND "Status" = 'Fail') as "today_failed",
    
    -- This month's statistics
    COUNT(*) FILTER (WHERE DATE_TRUNC('month', "CreatedAt") = DATE_TRUNC('month', CURRENT_DATE)) as "month_total",
    COUNT(*) FILTER (WHERE DATE_TRUNC('month', "CreatedAt") = DATE_TRUNC('month', CURRENT_DATE) AND "Status" = 'Success') as "month_success",
    
    -- OTAC statistics
    COUNT(*) FILTER (WHERE "OtacState" = 'Generated') as "otac_generated",
    COUNT(*) FILTER (WHERE "OtacState" = 'Validated') as "otac_validated",
    COUNT(*) FILTER (WHERE "OtacState" = 'Used') as "otac_used",
    
    -- Active counters
    COUNT(*) FILTER (WHERE "OtacState" IN ('Generated', 'Validated') AND "CreatedAt" >= CURRENT_DATE - INTERVAL '7 days') as "active_otac"
    
FROM "KbankOddRegistration"
WHERE "CreatedAt" >= CURRENT_DATE - INTERVAL '30 days';

-- Simple recent activity view
CREATE OR REPLACE VIEW "v_recent_activity" AS
SELECT 
    k."Id",
    k."ExternalReference",
    k."OtacCode",
    k."OtacState",
    k."Status",
    k."CreatedAt",
    k."UpdatedAt",
    b."Name" as "BranchName",
    u."Username" as "CreatedBy"
FROM "KbankOddRegistration" k
LEFT JOIN "Branch" b ON k."BranchId" = b."BranchId"
LEFT JOIN "Users" u ON k."GeneratedByUserId" = u."Id"
WHERE k."CreatedAt" >= NOW() - INTERVAL '24 hours'
ORDER BY k."CreatedAt" DESC
LIMIT 50;

-- Simple performance metrics function
CREATE OR REPLACE FUNCTION get_simple_metrics()
RETURNS TABLE (
    total_today INTEGER,
    success_today INTEGER,
    success_rate NUMERIC,
    active_otac INTEGER
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        COUNT(*)::INTEGER as total_today,
        COUNT(*) FILTER (WHERE "Status" = 'Success')::INTEGER as success_today,
        ROUND(
            COUNT(*) FILTER (WHERE "Status" = 'Success') * 100.0 / 
            NULLIF(COUNT(*) FILTER (WHERE "Status" IS NOT NULL), 0), 2
        ) as success_rate,
        COUNT(*) FILTER (WHERE "OtacState" IN ('Generated', 'Validated'))::INTEGER as active_otac
    FROM "KbankOddRegistration"
    WHERE "CreatedAt" >= CURRENT_DATE;
END;
$$ LANGUAGE plpgsql;

-- Comments
COMMENT ON VIEW "v_dashboard_stats" IS 'Simple dashboard statistics for modern UI';
COMMENT ON VIEW "v_recent_activity" IS 'Recent activity feed for dashboard';
COMMENT ON FUNCTION get_simple_metrics() IS 'Simple metrics function for API calls';

COMMIT;