-- BizConnect Initial Database Schema
-- Migration: 20250710-01_InitialSchema.sql
-- Description: Creates the initial database schema with Users table and indexes

-- Enable UUID extension if needed (optional for future use)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create Users table with PascalCase column names to match EF Core conventions
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" SERIAL PRIMARY KEY,
    "Username" VARCHAR(100) NOT NULL,
    "PasswordHash" VARCHAR(255) NOT NULL,
    "Role" VARCHAR(50) NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

-- Create unique index on Username
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Username" ON "Users" ("Username");

-- Create index on Role for faster role-based queries
CREATE INDEX IF NOT EXISTS "IX_Users_Role" ON "Users" ("Role");

-- Create index on IsActive for filtering active users
CREATE INDEX IF NOT EXISTS "IX_Users_IsActive" ON "Users" ("IsActive");

-- Insert initial admin user (password: admin123)
-- Note: This password hash is generated using BCrypt with cost factor 11
INSERT INTO "Users" ("Username", "PasswordHash", "Role", "CreatedAt", "UpdatedAt", "IsActive")
VALUES (
    'admin',
    '$2a$11$.cREN1iQUvZznsDi8abVhetwOBVxI3NkQLbNv1PO07mUfYYuzfldK',
    'Admin',
    NOW(),
    NOW(),
    TRUE
)
ON CONFLICT ("Username") DO NOTHING;

-- Create a function to automatically update UpdatedAt timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Create trigger to automatically update UpdatedAt on Users table
DROP TRIGGER IF EXISTS update_users_updated_at ON "Users";
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON "Users"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Add comments for documentation
COMMENT ON TABLE "Users" IS 'Application users with authentication and authorization data';
COMMENT ON COLUMN "Users"."Id" IS 'Primary key, auto-incrementing user identifier';
COMMENT ON COLUMN "Users"."Username" IS 'Unique username for authentication';
COMMENT ON COLUMN "Users"."PasswordHash" IS 'BCrypt hashed password';
COMMENT ON COLUMN "Users"."Role" IS 'User role: Admin or User';
COMMENT ON COLUMN "Users"."CreatedAt" IS 'Timestamp when user was created';
COMMENT ON COLUMN "Users"."UpdatedAt" IS 'Timestamp when user was last updated (auto-updated by trigger)';
COMMENT ON COLUMN "Users"."IsActive" IS 'Whether the user account is active and can log in';
