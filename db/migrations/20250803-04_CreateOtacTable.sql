-- BizConnect OTAC (One-Time Access Code) Table
-- Migration: 20250803-04_CreateOtacTable.sql
-- Description: Creates OTAC table for secure one-time access code management

BEGIN;

-- Create OTAC codes table
CREATE TABLE IF NOT EXISTS "OtacCode" (
    "Id" SERIAL PRIMARY KEY,
    "Code" VARCHAR(10) NOT NULL,
    "Purpose" VARCHAR(100) NOT NULL,
    "IssuedTo" VARCHAR(255) NOT NULL,
    "AttemptCount" INTEGER NOT NULL DEFAULT 0,
    "IsLocked" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsUsed" BOOLEAN NOT NULL DEFAULT FALSE,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ExpiresAt" TIMESTAMPTZ NOT NULL,
    "UsedAt" TIMESTAMPTZ NULL,
    "GeneratedByUserId" INTEGER NOT NULL,
    "LastAttemptAt" TIMESTAMPTZ NULL,
    "LastAttemptIp" VARCHAR(45) NULL,
    "Notes" TEXT NULL,
    -- Foreign Key to Users table
    CONSTRAINT "FK_OtacCode_GeneratedByUserId" FOREIGN KEY ("GeneratedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

-- Performance indexes for OTAC table
CREATE INDEX IF NOT EXISTS "IX_OtacCode_Code" ON "OtacCode" ("Code");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_Purpose" ON "OtacCode" ("Purpose");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_IssuedTo" ON "OtacCode" ("IssuedTo");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_ExpiresAt" ON "OtacCode" ("ExpiresAt");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_IsUsed" ON "OtacCode" ("IsUsed");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_IsLocked" ON "OtacCode" ("IsLocked");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_CreatedAt" ON "OtacCode" ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_GeneratedByUserId" ON "OtacCode" ("GeneratedByUserId");

-- Composite indexes for common queries
CREATE INDEX IF NOT EXISTS "IX_OtacCode_Code_IsUsed_ExpiresAt" ON "OtacCode" ("Code", "IsUsed", "ExpiresAt");
CREATE INDEX IF NOT EXISTS "IX_OtacCode_Purpose_IssuedTo_IsUsed" ON "OtacCode" ("Purpose", "IssuedTo", "IsUsed");

-- Add table comment
COMMENT ON TABLE "OtacCode" IS 'One-Time Access Codes for secure user verification and authentication';
COMMENT ON COLUMN "OtacCode"."Code" IS 'The actual OTAC code (8-character alphanumeric)';
COMMENT ON COLUMN "OtacCode"."Purpose" IS 'Purpose of the code (e.g., PASSWORD_RESET, TWO_FACTOR_AUTH)';
COMMENT ON COLUMN "OtacCode"."IssuedTo" IS 'Email, phone, or identifier the code was issued to';
COMMENT ON COLUMN "OtacCode"."AttemptCount" IS 'Number of validation attempts made';
COMMENT ON COLUMN "OtacCode"."IsLocked" IS 'TRUE if code is locked due to too many failed attempts';
COMMENT ON COLUMN "OtacCode"."IsUsed" IS 'TRUE if code has been successfully used';
COMMENT ON COLUMN "OtacCode"."ExpiresAt" IS 'When the code expires (typically 30 minutes from creation)';
COMMENT ON COLUMN "OtacCode"."GeneratedByUserId" IS 'User ID who generated this code';

COMMIT;

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250803-04_CreateOtacTable.sql')
ON CONFLICT ("Filename") DO NOTHING;