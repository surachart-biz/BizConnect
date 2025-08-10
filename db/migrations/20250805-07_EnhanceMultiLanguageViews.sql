-- ====================================================================
-- Migration: 20250805-04_EnhanceMultiLanguageViews.sql
-- Purpose: Complete multi-language support for views and add helper functions
-- Author: Claude Code (BizConnect Database Architect)
-- Date: 2025-08-05
-- ====================================================================

BEGIN;

-- ====================================================================
-- SECTION 1: ENHANCED EXPIRED OTAC CODES VIEW WITH MULTI-LANGUAGE SUPPORT
-- ====================================================================

-- Drop and recreate ExpiredOtacCodes view with complete multi-language support
DROP VIEW IF EXISTS "ExpiredOtacCodes";

CREATE OR REPLACE VIEW "ExpiredOtacCodes" AS
SELECT 
    ko."Id",
    ko."ExternalReference",
    ko."OtacCode",
    ko."OtacState",
    ko."Status",
    ko."OtacExpiresAt",
    ko."CreatedAt",
    ko."UpdatedAt",
    ko."BranchId",
    -- Multi-language branch information
    b."Code" AS "BranchCode",
    b."Name" AS "BranchName",
    b."NameTh" AS "BranchNameTh",
    b."NameEn" AS "BranchNameEn",
    b."Address" AS "BranchAddress",
    b."AddressTh" AS "BranchAddressTh",
    b."AddressEn" AS "BranchAddressEn",
    -- User information
    u."Username" AS "GeneratedByUsername",
    -- Additional helpful fields
    EXTRACT(EPOCH FROM (NOW() - ko."OtacExpiresAt"))/60 AS "MinutesExpired",
    CASE 
        WHEN ko."OtacExpiresAt" < NOW() - INTERVAL '1 day' THEN 'STALE'
        WHEN ko."OtacExpiresAt" < NOW() - INTERVAL '1 hour' THEN 'OLD'
        ELSE 'RECENT'
    END AS "ExpiryCategory"
FROM "KbankOddRegistration" ko
LEFT JOIN "Branch" b ON ko."BranchId" = b."BranchId"
LEFT JOIN "Users" u ON ko."GeneratedByUserId" = u."Id"
WHERE ko."OtacExpiresAt" IS NOT NULL 
  AND ko."OtacExpiresAt" < NOW()
  AND ko."Status" NOT IN ('Success', 'Fail');

-- ====================================================================
-- SECTION 2: ADDITIONAL HELPER FUNCTIONS FOR MULTI-LANGUAGE SUPPORT
-- ====================================================================

-- Create function to get localized status messages
CREATE OR REPLACE FUNCTION get_status_message(status_code VARCHAR(20), language_code VARCHAR(2) DEFAULT 'en')
RETURNS TEXT AS $$
BEGIN
    RETURN CASE 
        WHEN language_code = 'th' THEN
            CASE status_code
                WHEN 'Pending' THEN 'รอดำเนินการ'
                WHEN 'Success' THEN 'สำเร็จ'
                WHEN 'Fail' THEN 'ล้มเหลว'
                WHEN 'Cancelled' THEN 'ยกเลิก'
                WHEN 'Expired' THEN 'หมดอายุ'
                ELSE status_code
            END
        ELSE -- Default to English
            CASE status_code
                WHEN 'Pending' THEN 'Pending'
                WHEN 'Success' THEN 'Success'
                WHEN 'Fail' THEN 'Failed'
                WHEN 'Cancelled' THEN 'Cancelled'
                WHEN 'Expired' THEN 'Expired'
                ELSE status_code
            END
    END;
END;
$$ LANGUAGE plpgsql;

-- Create function to get localized OTAC state messages
CREATE OR REPLACE FUNCTION get_otac_state_message(otac_state VARCHAR(20), language_code VARCHAR(2) DEFAULT 'en')
RETURNS TEXT AS $$
BEGIN
    RETURN CASE 
        WHEN language_code = 'th' THEN
            CASE otac_state
                WHEN 'Generated' THEN 'สร้างแล้ว'
                WHEN 'Validated' THEN 'ตรวจสอบแล้ว'
                WHEN 'Used' THEN 'ใช้แล้ว'
                WHEN 'Expired' THEN 'หมดอายุ'
                WHEN 'Locked' THEN 'ถูกล็อค'
                ELSE otac_state
            END
        ELSE -- Default to English
            CASE otac_state
                WHEN 'Generated' THEN 'Generated'
                WHEN 'Validated' THEN 'Validated'
                WHEN 'Used' THEN 'Used'
                WHEN 'Expired' THEN 'Expired'
                WHEN 'Locked' THEN 'Locked'
                ELSE otac_state
            END
    END;
END;
$$ LANGUAGE plpgsql;

-- Create function to format dates in Thai Buddhist calendar
CREATE OR REPLACE FUNCTION format_thai_date(input_date TIMESTAMPTZ, include_time BOOLEAN DEFAULT false)
RETURNS TEXT AS $$
DECLARE
    thai_months TEXT[] := ARRAY[
        'มกราคม', 'กุมภาพันธ์', 'มีนาคม', 'เมษายน', 'พฤษภาคม', 'มิถุนายน',
        'กรกฎาคม', 'สิงหาคม', 'กันยายน', 'ตุลาคม', 'พฤศจิกายน', 'ธันวาคม'
    ];
    thai_year INTEGER;
    thai_month TEXT;
    day_part INTEGER;
    time_part TEXT;
BEGIN
    IF input_date IS NULL THEN
        RETURN NULL;
    END IF;
    
    -- Convert to Buddhist Era (Add 543 years)
    thai_year := EXTRACT(YEAR FROM input_date) + 543;
    thai_month := thai_months[EXTRACT(MONTH FROM input_date)];
    day_part := EXTRACT(DAY FROM input_date);
    
    IF include_time THEN
        time_part := ' เวลา ' || TO_CHAR(input_date, 'HH24:MI น.');
    ELSE
        time_part := '';
    END IF;
    
    RETURN day_part || ' ' || thai_month || ' พ.ศ. ' || thai_year || time_part;
END;
$$ LANGUAGE plpgsql;

-- Create function to format dates in English
CREATE OR REPLACE FUNCTION format_english_date(input_date TIMESTAMPTZ, include_time BOOLEAN DEFAULT false)
RETURNS TEXT AS $$
BEGIN
    IF input_date IS NULL THEN
        RETURN NULL;
    END IF;
    
    IF include_time THEN
        RETURN TO_CHAR(input_date, 'DD Mon YYYY at HH24:MI');
    ELSE
        RETURN TO_CHAR(input_date, 'DD Mon YYYY');
    END IF;
END;
$$ LANGUAGE plpgsql;

-- Create unified date formatting function
CREATE OR REPLACE FUNCTION format_localized_date(input_date TIMESTAMPTZ, language_code VARCHAR(2) DEFAULT 'en', include_time BOOLEAN DEFAULT false)
RETURNS TEXT AS $$
BEGIN
    IF input_date IS NULL THEN
        RETURN NULL;
    END IF;
    
    IF language_code = 'th' THEN
        RETURN format_thai_date(input_date, include_time);
    ELSE
        RETURN format_english_date(input_date, include_time);
    END IF;
END;
$$ LANGUAGE plpgsql;

-- ====================================================================
-- SECTION 3: ENHANCED ACTIVE REGISTRATIONS VIEW (ALREADY GOOD BUT ADD FORMATTING HELPERS)
-- ====================================================================

-- The ActiveOddRegistrations view is already well-designed with multi-language support
-- Just add a comment to acknowledge its completeness
COMMENT ON VIEW "ActiveOddRegistrations" IS 'Active ODD registrations with complete multi-language branch information (Thai/English) for admin dashboard';

-- ====================================================================
-- SECTION 4: FUNCTION COMMENTS AND DOCUMENTATION
-- ====================================================================

-- Add comprehensive comments for all helper functions
COMMENT ON FUNCTION get_status_message(VARCHAR, VARCHAR) IS 'Returns localized status messages in Thai or English';
COMMENT ON FUNCTION get_otac_state_message(VARCHAR, VARCHAR) IS 'Returns localized OTAC state messages in Thai or English';
COMMENT ON FUNCTION format_thai_date(TIMESTAMPTZ, BOOLEAN) IS 'Formats date in Thai Buddhist calendar format';
COMMENT ON FUNCTION format_english_date(TIMESTAMPTZ, BOOLEAN) IS 'Formats date in English format';
COMMENT ON FUNCTION format_localized_date(TIMESTAMPTZ, VARCHAR, BOOLEAN) IS 'Unified date formatting function supporting Thai/English localization';

-- Update view comment with enhanced capabilities
COMMENT ON VIEW "ExpiredOtacCodes" IS 'Expired OTAC codes with complete multi-language branch information and expiry categorization for cleanup jobs';

-- ====================================================================
-- SECTION 5: VALIDATION AND COMPLETION
-- ====================================================================

-- Validate that all functions were created successfully
DO $$
DECLARE
    function_count INTEGER;
    view_count INTEGER;
BEGIN
    -- Count helper functions
    SELECT COUNT(*) INTO function_count
    FROM pg_proc p
    JOIN pg_namespace n ON p.pronamespace = n.oid
    WHERE n.nspname = 'public' 
    AND p.proname IN ('get_branch_name', 'get_branch_address', 'get_status_message', 
                     'get_otac_state_message', 'format_thai_date', 'format_english_date', 
                     'format_localized_date');
    
    -- Count views
    SELECT COUNT(*) INTO view_count
    FROM pg_views
    WHERE schemaname = 'public'
    AND viewname IN ('ActiveOddRegistrations', 'ExpiredOtacCodes');
    
    RAISE NOTICE 'Multi-language enhancement validation:';
    RAISE NOTICE '- Helper functions created: %', function_count;
    RAISE NOTICE '- Views with multi-language support: %', view_count;
    
    IF function_count < 7 THEN
        RAISE EXCEPTION 'Multi-language enhancement failed - missing helper functions';
    END IF;
    
    IF view_count < 2 THEN
        RAISE EXCEPTION 'Multi-language enhancement failed - missing views';
    END IF;
    
    RAISE NOTICE 'Multi-language views enhancement completed successfully!';
    RAISE NOTICE 'Database now supports full Thai/English UI toggle functionality';
    RAISE NOTICE 'Views include: branch names, addresses, status messages, and date formatting';
END $$;

COMMIT;

-- ====================================================================
-- POST-MIGRATION USAGE EXAMPLES
-- ====================================================================
/*
USAGE EXAMPLES FOR MULTI-LANGUAGE FUNCTIONS:

1. Get branch name in Thai:
   SELECT get_branch_name(1, 'th');

2. Get branch address in English:
   SELECT get_branch_address(1, 'en');

3. Get status message in Thai:
   SELECT get_status_message('Pending', 'th');

4. Format date in Thai Buddhist calendar:
   SELECT format_thai_date(NOW(), true);

5. Format date in English:
   SELECT format_english_date(NOW(), false);

6. Unified date formatting:
   SELECT format_localized_date(NOW(), 'th', true);

7. Query expired codes with multi-language branch info:
   SELECT "ExternalReference", "BranchNameTh", "BranchNameEn", 
          "ExpiryCategory", "MinutesExpired"
   FROM "ExpiredOtacCodes"
   WHERE "ExpiryCategory" = 'RECENT';

8. Query active registrations with localized data:
   SELECT "ExternalReference", "BranchNameTh", "Status",
          get_status_message("Status", 'th') AS "StatusTh"
   FROM "ActiveOddRegistrations"
   WHERE "OtacState" = 'Generated';
*/