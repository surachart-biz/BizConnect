-- Master Migration Script
-- Executes all migrations in correct chronological order
-- Created: 2025-08-10
-- This script ensures all database migrations are applied in the proper sequence

-- Migration 1: Core schema foundation
\i 20250805-03_ConsolidatedSchema.sql

-- Migration 2: OTAC state constraint enhancements
\i 20250805-04_EnhanceOtacStateConstraint.sql

-- Migration 3: Emergency fix for external reference in OTAC
\i 20250805-05_EMERGENCY_FixExternalReferenceForOTAC.sql

-- Migration 4: Multi-language status columns
\i 20250805-06_AddMultiLanguageStatusColumns.sql

-- Migration 5: Enhanced multi-language views
\i 20250805-07_EnhanceMultiLanguageViews.sql

-- Migration 6: Modern UI performance optimizations
\i 20250806-01_ModernUIPerformanceOptimization.sql

-- End of migrations
SELECT 'All migrations completed successfully' as status;