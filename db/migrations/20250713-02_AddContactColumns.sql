-- BizConnect KBank ODD Registration Contact Fields
-- Migration: 20250713-02_AddContactColumns.sql
-- Description: Adds Email, MobileNo, IdType, IdValue columns to KbankOddRegistration table for form data collection

-- Add contact information columns to KbankOddRegistration table
ALTER TABLE "KbankOddRegistration"
    ADD COLUMN "Email" VARCHAR(256),
    ADD COLUMN "MobileNo" VARCHAR(20),
    ADD COLUMN "IdType" VARCHAR(30),
    ADD COLUMN "IdValue" VARCHAR(30);

-- Add comments for documentation
COMMENT ON COLUMN "KbankOddRegistration"."Email" IS 'User email address for ODD registration';
COMMENT ON COLUMN "KbankOddRegistration"."MobileNo" IS 'User mobile number (format: 08xxxxxxxx or +66xxxxxxxx)';
COMMENT ON COLUMN "KbankOddRegistration"."IdType" IS 'ID type: National ID, Passport, Tax ID, or Company Tax ID';
COMMENT ON COLUMN "KbankOddRegistration"."IdValue" IS 'ID number/value corresponding to the selected ID type';

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250713-02_AddContactColumns.sql');
