-- ====================================================================
-- BizConnect Frontend Compatibility Enhancements Migration
-- Migration: 20250810-02_FrontendCompatibilityEnhancements.sql
-- Date: 2025-08-10
-- Description: Database enhancements for optimal frontend data consumption
-- Purpose: Ensure backwards compatibility and frontend-friendly data structures
-- ====================================================================

-- Enable error handling and logging
\set ON_ERROR_STOP on

BEGIN;

-- ====================================================================
-- SECTION 1: FRONTEND-FRIENDLY DATA VIEWS
-- ====================================================================

-- Registration list view with all frontend requirements
CREATE OR REPLACE VIEW v_frontend_registration_list AS
SELECT 
    k."Id",
    k."ExternalReference",
    -- Masked sensitive data for frontend security
    CASE 
        WHEN k."OtacCode" IS NOT NULL THEN LEFT(k."OtacCode", 4) || '****'
        ELSE NULL
    END as "MaskedOtacCode",
    k."OtacState",
    k."Status",
    CASE 
        WHEN k."IdValue" IS NOT NULL THEN 
            CASE k."IdType"
                WHEN 'National ID' THEN LEFT(k."IdValue", 3) || '****' || RIGHT(k."IdValue", 3)
                WHEN 'Passport' THEN LEFT(k."IdValue", 2) || '****' || RIGHT(k."IdValue", 2)
                ELSE LEFT(k."IdValue", 2) || '****'
            END
        ELSE NULL
    END as "MaskedIdValue",
    k."IdType",
    k."FullName",
    CASE 
        WHEN k."AccountNo" IS NOT NULL THEN LEFT(k."AccountNo", 3) || '****' || RIGHT(k."AccountNo", 3)
        ELSE NULL
    END as "MaskedAccountNo",
    k."CreatedAt",
    k."UpdatedAt",
    k."OtacExpiresAt",
    k."AttemptCount",
    k."IsLocked",
    -- Multi-language status messages
    COALESCE(k."StatusMessageEn", k."Status") as "StatusDisplayEn",
    COALESCE(k."StatusMessageTh", k."Status") as "StatusDisplayTh",
    -- Branch information
    b."BranchId",
    COALESCE(b."NameEn", b."Name") as "BranchNameEn",
    COALESCE(b."NameTh", b."Name") as "BranchNameTh",
    b."Code" as "BranchCode",
    -- User information
    u."Username" as "GeneratedByUsername",
    -- Calculated fields for frontend
    CASE 
        WHEN k."OtacExpiresAt" IS NOT NULL AND k."OtacExpiresAt" > NOW() 
        THEN EXTRACT(EPOCH FROM (k."OtacExpiresAt" - NOW()))::INTEGER
        ELSE 0
    END as "SecondsUntilExpiry",
    -- Priority for sorting (matches v_recent_activities logic)
    CASE 
        WHEN k."Status" = 'Fail' THEN 1
        WHEN k."Status" = 'Success' THEN 2
        WHEN k."OtacState" = 'Used' THEN 3
        WHEN k."OtacState" = 'Validated' THEN 4
        WHEN k."IsLocked" = true THEN 5
        ELSE 6
    END as "DisplayPriority",
    -- Activity type for UI display
    CASE 
        WHEN k."UpdatedAt" IS NOT NULL AND k."Status" = 'Success' THEN 'Registration Approved'
        WHEN k."UpdatedAt" IS NOT NULL AND k."Status" = 'Fail' THEN 'Registration Rejected'
        WHEN k."OtacState" = 'Used' THEN 'OTAC Used'
        WHEN k."OtacState" = 'Validated' THEN 'OTAC Validated'
        WHEN k."OtacState" = 'Generated' AND k."CreatedAt" = COALESCE(k."UpdatedAt", k."CreatedAt") THEN 'OTAC Generated'
        WHEN k."Status" IS NULL THEN 'New Registration'
        ELSE 'Updated'
    END as "ActivityType"
    
FROM "KbankOddRegistration" k
LEFT JOIN "Branch" b ON b."BranchId" = k."BranchId"
LEFT JOIN "Users" u ON u."Id" = k."GeneratedByUserId";

-- Branch selection view with active branches only
CREATE OR REPLACE VIEW v_frontend_branch_options AS
SELECT 
    b."BranchId" as "Value",
    COALESCE(b."NameEn", b."Name") as "LabelEn",
    COALESCE(b."NameTh", b."Name") as "LabelTh",
    b."Code",
    b."IsActive",
    -- Registration count for branch popularity
    COUNT(k."Id") as "RegistrationCount",
    -- Success rate for branch performance indicator
    ROUND(
        COUNT(k."Id") FILTER (WHERE k."Status" = 'Success') * 100.0 / 
        NULLIF(COUNT(k."Id") FILTER (WHERE k."Status" IS NOT NULL), 0), 1
    ) as "SuccessRate"
    
FROM "Branch" b
LEFT JOIN "KbankOddRegistration" k ON k."BranchId" = b."BranchId" 
    AND k."CreatedAt" >= CURRENT_DATE - INTERVAL '30 days'
WHERE b."IsActive" = true
GROUP BY b."BranchId", b."Name", b."NameEn", b."NameTh", b."Code", b."IsActive"
ORDER BY "RegistrationCount" DESC, "LabelEn";

-- ====================================================================
-- SECTION 2: FRONTEND API OPTIMIZED FUNCTIONS
-- ====================================================================

-- Function to get dashboard data optimized for frontend API consumption
CREATE OR REPLACE FUNCTION get_frontend_dashboard_data()
RETURNS jsonb
LANGUAGE plpgsql
STABLE
PARALLEL SAFE
AS $$
DECLARE
    result jsonb;
BEGIN
    -- Combine multiple queries into single JSON response
    WITH dashboard_stats AS (
        SELECT 
            COUNT(*) as total_registrations,
            COUNT(*) FILTER (WHERE "Status" = 'Success') as successful_registrations,
            COUNT(*) FILTER (WHERE "Status" = 'Fail') as failed_registrations,
            COUNT(*) FILTER (WHERE "Status" IS NULL OR "Status" = 'Pending') as pending_registrations,
            COUNT(*) FILTER (WHERE "CreatedAt" >= CURRENT_DATE) as today_registrations,
            COUNT(*) FILTER (WHERE "OtacState" = 'Generated') as active_otac_codes,
            COUNT(*) FILTER (WHERE "OtacState" = 'Validated') as validated_otac_codes,
            COUNT(*) FILTER (WHERE "OtacState" = 'Used') as used_otac_codes,
            ROUND(
                COUNT(*) FILTER (WHERE "Status" = 'Success') * 100.0 / 
                NULLIF(COUNT(*) FILTER (WHERE "Status" IS NOT NULL), 0), 2
            ) as overall_success_rate
        FROM "KbankOddRegistration"
        WHERE "CreatedAt" >= CURRENT_DATE - INTERVAL '90 days'
    ),
    recent_activities AS (
        SELECT array_to_json(array_agg(row_to_json(ra))) as activities
        FROM (
            SELECT 
                "Id",
                "MaskedOtacCode",
                "OtacState", 
                "Status",
                "ActivityType",
                "CreatedAt",
                "UpdatedAt",
                "BranchNameEn",
                "GeneratedByUsername"
            FROM v_frontend_registration_list
            ORDER BY "DisplayPriority", "CreatedAt" DESC
            LIMIT 10
        ) ra
    )
    SELECT jsonb_build_object(
        'statistics', row_to_json(ds),
        'recent_activities', COALESCE(ra.activities, '[]'::json),
        'last_updated', NOW(),
        'cache_duration_seconds', 120
    ) INTO result
    FROM dashboard_stats ds, recent_activities ra;
    
    RETURN result;
END;
$$;

-- Function to get validation rules for frontend
CREATE OR REPLACE FUNCTION get_frontend_validation_rules(entity_type text DEFAULT 'registration')
RETURNS jsonb
LANGUAGE plpgsql
STABLE
PARALLEL SAFE
AS $$
DECLARE
    result jsonb;
BEGIN
    -- Return validation rules based on entity type
    result := CASE entity_type
        WHEN 'registration' THEN jsonb_build_object(
            'entityType', 'KbankOddRegistration',
            'rules', jsonb_build_object(
                'OtacCode', jsonb_build_object(
                    'required', true,
                    'pattern', '^[A-Z0-9]{8}$',
                    'minLength', 8,
                    'maxLength', 8,
                    'errorMessage', 'OTAC code must be 8 characters (A-Z, 0-9)',
                    'errorMessageTh', 'รหัส OTAC ต้องเป็นตัวอักษรและตัวเลข 8 ตัวอักษร'
                ),
                'ExternalReference', jsonb_build_object(
                    'required', false,
                    'pattern', '^BIZ\d{17}$',
                    'errorMessage', 'External reference must follow BIZyyyyMMddHHmmssfff format',
                    'errorMessageTh', 'รหัสอ้างอิงภายนอกต้องเป็นรูปแบบ BIZyyyyMMddHHmmssfff'
                ),
                'MobileNo', jsonb_build_object(
                    'required', false,
                    'pattern', '^(08\d{8}|\+66\d{8,9})$',
                    'errorMessage', 'Mobile number must be in format 08xxxxxxxx or +66xxxxxxxx',
                    'errorMessageTh', 'หมายเลขโทรศัพท์ต้องเป็นรูปแบบ 08xxxxxxxx หรือ +66xxxxxxxx'
                ),
                'AccountNo', jsonb_build_object(
                    'required', false,
                    'pattern', '^\d{10,15}$',
                    'minLength', 10,
                    'maxLength', 15,
                    'errorMessage', 'Account number must be 10-15 digits',
                    'errorMessageTh', 'หมายเลขบัญชีต้องเป็นตัวเลข 10-15 หลัก'
                )
            )
        )
        WHEN 'branch' THEN jsonb_build_object(
            'entityType', 'Branch',
            'rules', jsonb_build_object(
                'Name', jsonb_build_object(
                    'required', true,
                    'maxLength', 100,
                    'errorMessage', 'Branch name is required and cannot exceed 100 characters',
                    'errorMessageTh', 'ชื่อสาขาจำเป็นและต้องไม่เกิน 100 ตัวอักษร'
                ),
                'Code', jsonb_build_object(
                    'required', false,
                    'maxLength', 10,
                    'errorMessage', 'Branch code cannot exceed 10 characters',
                    'errorMessageTh', 'รหัสสาขาต้องไม่เกิน 10 ตัวอักษร'
                )
            )
        )
        ELSE jsonb_build_object('entityType', entity_type, 'rules', jsonb_build_object())
    END;
    
    RETURN result;
END;
$$;

-- ====================================================================
-- SECTION 3: BACKWARDS COMPATIBILITY VIEWS
-- ====================================================================

-- Ensure existing frontend code continues to work
CREATE OR REPLACE VIEW v_legacy_registration_summary AS
SELECT 
    "Id",
    "ExternalReference",
    "Status",
    "OtacState",
    "CreatedAt",
    "UpdatedAt",
    COALESCE("StatusMessageEn", "Status") as "StatusMessage"
FROM "KbankOddRegistration"
ORDER BY "CreatedAt" DESC;

-- Legacy dashboard stats view
CREATE OR REPLACE VIEW v_legacy_dashboard_stats AS
SELECT 
    COUNT(*) as "TotalRegistrations",
    COUNT(*) FILTER (WHERE "Status" = 'Success') as "SuccessfulRegistrations",
    COUNT(*) FILTER (WHERE "Status" = 'Fail') as "FailedRegistrations",
    COUNT(*) FILTER (WHERE "Status" IS NULL OR "Status" = 'Pending') as "PendingRegistrations",
    COUNT(*) FILTER (WHERE "CreatedAt" >= CURRENT_DATE) as "TodayRegistrations",
    COUNT(*) FILTER (WHERE "OtacState" = 'Generated') as "ActiveOtacCodes"
FROM "KbankOddRegistration";

-- ====================================================================
-- SECTION 4: FRONTEND CONFIGURATION TABLE
-- ====================================================================

-- Table to store frontend configuration and feature flags
CREATE TABLE IF NOT EXISTS "_FrontendConfiguration" (
    "Key" VARCHAR(100) PRIMARY KEY,
    "Value" JSONB NOT NULL,
    "Description" TEXT,
    "IsActive" BOOLEAN DEFAULT true,
    "CreatedAt" TIMESTAMPTZ DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ DEFAULT NOW()
);

-- Insert default frontend configuration
INSERT INTO "_FrontendConfiguration" ("Key", "Value", "Description") VALUES
('dashboard.refresh_interval', '30', 'Dashboard auto-refresh interval in seconds'),
('registration_list.page_size', '20', 'Default page size for registration lists'),
('otac.expiry_warning_seconds', '300', 'Show expiry warning when OTAC has less than this many seconds'),
('search.debounce_ms', '300', 'Search input debounce delay in milliseconds'),
('validation.show_warnings', 'true', 'Whether to show validation warnings to users'),
('ui.enable_realtime_updates', 'true', 'Enable real-time UI updates via SignalR'),
('security.mask_sensitive_data', 'true', 'Mask sensitive data in frontend displays'),
('analytics.enable_advanced_charts', 'true', 'Enable advanced analytics charts'),
('localization.default_language', '"en"', 'Default language for UI'),
('performance.enable_caching', 'true', 'Enable frontend data caching')
ON CONFLICT ("Key") DO NOTHING;

-- ====================================================================
-- SECTION 5: DATA INTEGRITY ENHANCEMENTS
-- ====================================================================

-- Function to validate data consistency for frontend
CREATE OR REPLACE FUNCTION validate_frontend_data_integrity()
RETURNS TABLE(
    check_name text,
    is_valid boolean,
    issue_count integer,
    description text
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Check for orphaned registrations without valid users
    RETURN QUERY
    SELECT 
        'orphaned_registrations'::text,
        (COUNT(*) = 0),
        COUNT(*)::integer,
        'Registrations with invalid GeneratedByUserId'::text
    FROM "KbankOddRegistration" k
    LEFT JOIN "Users" u ON u."Id" = k."GeneratedByUserId"
    WHERE u."Id" IS NULL;

    -- Check for registrations with invalid branch references
    RETURN QUERY
    SELECT 
        'invalid_branch_references'::text,
        (COUNT(*) = 0),
        COUNT(*)::integer,
        'Registrations with invalid BranchId'::text
    FROM "KbankOddRegistration" k
    WHERE k."BranchId" IS NOT NULL 
    AND NOT EXISTS (SELECT 1 FROM "Branch" b WHERE b."BranchId" = k."BranchId");

    -- Check for duplicate OTAC codes
    RETURN QUERY
    WITH duplicates AS (
        SELECT "OtacCode", COUNT(*) as cnt
        FROM "KbankOddRegistration"
        WHERE "OtacCode" IS NOT NULL
        GROUP BY "OtacCode"
        HAVING COUNT(*) > 1
    )
    SELECT 
        'duplicate_otac_codes'::text,
        (COUNT(*) = 0),
        COUNT(*)::integer,
        'Duplicate OTAC codes found'::text
    FROM duplicates;

    -- Check for invalid OTAC states
    RETURN QUERY
    SELECT 
        'invalid_otac_states'::text,
        (COUNT(*) = 0),
        COUNT(*)::integer,
        'Registrations with invalid OtacState values'::text
    FROM "KbankOddRegistration"
    WHERE "OtacState" NOT IN ('Generated', 'Validated', 'Used', 'Expired', 'Invalidated', 'Purged');
END;
$$;

-- ====================================================================
-- SECTION 6: PERFORMANCE INDEXES FOR FRONTEND QUERIES
-- ====================================================================

-- Index for frontend registration filtering
CREATE INDEX IF NOT EXISTS idx_frontend_registration_filter 
ON "KbankOddRegistration"("Status", "OtacState", "BranchId", "CreatedAt" DESC)
WHERE "CreatedAt" >= CURRENT_DATE - INTERVAL '1 year';

-- Index for OTAC expiry checks (used in frontend countdown timers)
CREATE INDEX IF NOT EXISTS idx_frontend_otac_expiry 
ON "KbankOddRegistration"("OtacExpiresAt", "OtacState")
WHERE "OtacExpiresAt" IS NOT NULL AND "OtacState" IN ('Generated', 'Validated');

-- Index for branch performance queries
CREATE INDEX IF NOT EXISTS idx_frontend_branch_performance
ON "KbankOddRegistration"("BranchId", "Status", "CreatedAt")
WHERE "BranchId" IS NOT NULL AND "CreatedAt" >= CURRENT_DATE - INTERVAL '90 days';

-- ====================================================================
-- SECTION 7: COMMENTS AND DOCUMENTATION
-- ====================================================================

COMMENT ON VIEW v_frontend_registration_list IS 'Frontend-optimized registration list with masked sensitive data and calculated display fields';
COMMENT ON VIEW v_frontend_branch_options IS 'Active branches formatted for frontend dropdown/select components with performance metrics';
COMMENT ON FUNCTION get_frontend_dashboard_data() IS 'Single API call to get all dashboard data in frontend-ready JSON format';
COMMENT ON FUNCTION get_frontend_validation_rules(text) IS 'Returns validation rules for frontend form validation in JSON format';
COMMENT ON TABLE "_FrontendConfiguration" IS 'Configuration settings for frontend behavior and feature flags';
COMMENT ON FUNCTION validate_frontend_data_integrity() IS 'Validates data consistency for frontend safety and error prevention';

-- Record this migration
INSERT INTO "_SchemaVersion" ("Filename") VALUES ('20250810-02_FrontendCompatibilityEnhancements.sql')
ON CONFLICT ("Filename") DO NOTHING;

COMMIT;

-- ====================================================================
-- POST-MIGRATION INSTRUCTIONS
-- ====================================================================
/*
FRONTEND INTEGRATION GUIDE:

1. New Views for Frontend Consumption:
   - v_frontend_registration_list: Use for all registration displays
   - v_frontend_branch_options: Use for branch selection dropdowns
   - All sensitive data is automatically masked

2. Optimized API Functions:
   - get_frontend_dashboard_data(): Single call for dashboard data
   - get_frontend_validation_rules(): Client-side validation rules
   - Both return JSON for direct frontend consumption

3. Configuration Management:
   - Use _FrontendConfiguration table for feature flags
   - Update configuration via admin panel
   - No frontend code changes needed for config updates

4. Data Integrity:
   - Run validate_frontend_data_integrity() to check data health
   - Automated checks prevent frontend errors
   - Monitor for data consistency issues

5. Performance Optimizations:
   - New indexes support frontend query patterns
   - Filtered indexes reduce index size and improve speed
   - Cursor-based pagination from previous migration

6. Backwards Compatibility:
   - Legacy views maintain existing API compatibility
   - Gradual migration path for existing frontend code
   - No breaking changes to current functionality

RECOMMENDED SERVICE LAYER UPDATES:

```csharp
// Use new frontend-optimized function
public async Task<object> GetDashboardDataAsync()
{
    var result = await _context.Database
        .SqlQueryRaw<string>("SELECT get_frontend_dashboard_data()::text")
        .FirstOrDefaultAsync();
    
    return JsonSerializer.Deserialize<object>(result);
}

// Get validation rules for frontend
public async Task<object> GetValidationRulesAsync(string entityType)
{
    var result = await _context.Database
        .SqlQueryRaw<string>("SELECT get_frontend_validation_rules(@p0)::text", entityType)
        .FirstOrDefaultAsync();
    
    return JsonSerializer.Deserialize<object>(result);
}

// Use frontend-optimized registration view
public async Task<List<FrontendRegistrationDto>> GetRegistrationsAsync()
{
    return await _context.Database
        .SqlQueryRaw<FrontendRegistrationDto>(
            "SELECT * FROM v_frontend_registration_list ORDER BY \"DisplayPriority\", \"CreatedAt\" DESC LIMIT 50")
        .ToListAsync();
}
```

FRONTEND JAVASCRIPT PATTERNS:

```javascript
// Real-time countdown for OTAC expiry
function startOtacCountdown(secondsUntilExpiry, otacId) {
    const interval = setInterval(() => {
        secondsUntilExpiry--;
        updateCountdownDisplay(otacId, secondsUntilExpiry);
        
        if (secondsUntilExpiry <= 0) {
            clearInterval(interval);
            markOtacAsExpired(otacId);
        }
    }, 1000);
}

// Use masked data directly from view
function displayRegistrationList(registrations) {
    registrations.forEach(reg => {
        // Data is already masked for security
        console.log(`OTAC: ${reg.MaskedOtacCode}`);
        console.log(`Account: ${reg.MaskedAccountNo}`);
    });
}
```
*/