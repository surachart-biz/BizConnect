-- BizConnect KBank ODD Registration Contact Fields
-- Migration: 20250713-02_AddContactColumns.sql
-- Description: Adds Email, MobileNo, IdType, IdValue columns to KbankOddRegistration table for form data collection

-- Add contact information columns to KbankOddRegistration table (only if they don't exist)
DO $$
BEGIN
    -- Add Email column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'Email') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "Email" VARCHAR(256);
    END IF;
    
    -- Add MobileNo column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'MobileNo') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "MobileNo" VARCHAR(20);
    END IF;
    
    -- Add IdType column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'IdType') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "IdType" VARCHAR(30);
    END IF;
    
    -- Add IdValue column if it doesn't exist
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'KbankOddRegistration' AND column_name = 'IdValue') THEN
        ALTER TABLE "KbankOddRegistration" ADD COLUMN "IdValue" VARCHAR(30);
    END IF;
END $$;

-- Add comments for documentation
COMMENT ON COLUMN "KbankOddRegistration"."Email" IS 'User email address for ODD registration';
COMMENT ON COLUMN "KbankOddRegistration"."MobileNo" IS 'User mobile number (format: 08xxxxxxxx or +66xxxxxxxx)';
COMMENT ON COLUMN "KbankOddRegistration"."IdType" IS 'ID type: National ID, Passport, Tax ID, or Company Tax ID';
COMMENT ON COLUMN "KbankOddRegistration"."IdValue" IS 'ID number/value corresponding to the selected ID type';

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250713-02_AddContactColumns.sql')
ON CONFLICT ("Filename") DO NOTHING;
