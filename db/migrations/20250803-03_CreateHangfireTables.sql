-- BizConnect Hangfire Background Job Tables
-- Migration: 20250803-03_CreateHangfireTables.sql
-- Description: Creates Hangfire tables for background job storage and management
-- This migration is idempotent and handles existing Hangfire tables gracefully

-- Create Hangfire schema for better organization
CREATE SCHEMA IF NOT EXISTS hangfire;

-- Drop existing tables in public schema if they exist (from previous Hangfire installations)
-- This prevents conflicts when moving to the hangfire schema
DROP TABLE IF EXISTS public.jobqueue CASCADE;
DROP TABLE IF EXISTS public.jobparameter CASCADE;
DROP TABLE IF EXISTS public.jobstate CASCADE;
DROP TABLE IF EXISTS public.counter CASCADE;
DROP TABLE IF EXISTS public.hash CASCADE;
DROP TABLE IF EXISTS public.list CASCADE;
DROP TABLE IF EXISTS public.set CASCADE;
DROP TABLE IF EXISTS public.state CASCADE;
DROP TABLE IF EXISTS public.server CASCADE;
DROP TABLE IF EXISTS public.job CASCADE;

-- Create Hangfire job table
CREATE TABLE IF NOT EXISTS hangfire.job (
    id BIGSERIAL PRIMARY KEY,
    stateid BIGINT,
    statename VARCHAR(20),
    invocationdata TEXT NOT NULL,
    arguments TEXT NOT NULL,
    createdat TIMESTAMPTZ NOT NULL,
    expireat TIMESTAMPTZ
);

-- Create Hangfire job parameter table
CREATE TABLE IF NOT EXISTS hangfire.jobparameter (
    id BIGSERIAL PRIMARY KEY,
    jobid BIGINT NOT NULL,
    name VARCHAR(40) NOT NULL,
    value TEXT,
    CONSTRAINT fk_hangfire_jobparameter_job FOREIGN KEY (jobid) REFERENCES hangfire.job (id) ON UPDATE CASCADE ON DELETE CASCADE
);

-- Create Hangfire job state table
CREATE TABLE IF NOT EXISTS hangfire.jobstate (
    id BIGSERIAL PRIMARY KEY,
    jobid BIGINT NOT NULL,
    name VARCHAR(20) NOT NULL,
    reason VARCHAR(100),
    createdat TIMESTAMPTZ NOT NULL,
    data TEXT,
    CONSTRAINT fk_hangfire_jobstate_job FOREIGN KEY (jobid) REFERENCES hangfire.job (id) ON UPDATE CASCADE ON DELETE CASCADE
);

-- Create Hangfire job queue table
CREATE TABLE IF NOT EXISTS hangfire.jobqueue (
    id BIGSERIAL PRIMARY KEY,
    jobid BIGINT NOT NULL,
    queue VARCHAR(50) NOT NULL,
    fetchedat TIMESTAMPTZ
);

-- Create Hangfire server table
CREATE TABLE IF NOT EXISTS hangfire.server (
    id VARCHAR(200) PRIMARY KEY,
    data TEXT,
    lastheartbeat TIMESTAMPTZ NOT NULL
);

-- Create Hangfire set table
CREATE TABLE IF NOT EXISTS hangfire.set (
    id BIGSERIAL PRIMARY KEY,
    key VARCHAR(100) NOT NULL,
    score NUMERIC NOT NULL,
    value VARCHAR(256) NOT NULL,
    expireat TIMESTAMPTZ
);

-- Create Hangfire counter table
CREATE TABLE IF NOT EXISTS hangfire.counter (
    id BIGSERIAL PRIMARY KEY,
    key VARCHAR(100) NOT NULL,
    value BIGINT NOT NULL,
    expireat TIMESTAMPTZ
);

-- Create Hangfire hash table
CREATE TABLE IF NOT EXISTS hangfire.hash (
    id BIGSERIAL PRIMARY KEY,
    key VARCHAR(100) NOT NULL,
    field VARCHAR(100) NOT NULL,
    value TEXT,
    expireat TIMESTAMPTZ
);

-- Create Hangfire list table
CREATE TABLE IF NOT EXISTS hangfire.list (
    id BIGSERIAL PRIMARY KEY,
    key VARCHAR(100) NOT NULL,
    value TEXT,
    expireat TIMESTAMPTZ
);

-- Create Hangfire state table
CREATE TABLE IF NOT EXISTS hangfire.state (
    id BIGSERIAL PRIMARY KEY,
    jobid BIGINT NOT NULL,
    name VARCHAR(20) NOT NULL,
    reason VARCHAR(100),
    createdat TIMESTAMPTZ NOT NULL,
    data TEXT,
    CONSTRAINT fk_hangfire_state_job FOREIGN KEY (jobid) REFERENCES hangfire.job (id) ON UPDATE CASCADE ON DELETE CASCADE
);

-- Performance indexes for Hangfire
CREATE INDEX IF NOT EXISTS ix_hangfire_job_expireat ON hangfire.job (expireat);
CREATE INDEX IF NOT EXISTS ix_hangfire_job_statename ON hangfire.job (statename);
CREATE INDEX IF NOT EXISTS ix_hangfire_jobparameter_jobid_name ON hangfire.jobparameter (jobid, name);
CREATE INDEX IF NOT EXISTS ix_hangfire_jobqueue_queue_fetchedat_jobid ON hangfire.jobqueue (queue, fetchedat, jobid);
CREATE INDEX IF NOT EXISTS ix_hangfire_jobstate_jobid ON hangfire.jobstate (jobid);
CREATE INDEX IF NOT EXISTS ix_hangfire_server_lastheartbeat ON hangfire.server (lastheartbeat);
CREATE INDEX IF NOT EXISTS ix_hangfire_set_key_score ON hangfire.set (key, score);
CREATE UNIQUE INDEX IF NOT EXISTS uix_hangfire_set_key_value ON hangfire.set (key, value);
CREATE INDEX IF NOT EXISTS ix_hangfire_counter_key ON hangfire.counter (key);
CREATE INDEX IF NOT EXISTS ix_hangfire_hash_key ON hangfire.hash (key);
CREATE UNIQUE INDEX IF NOT EXISTS uix_hangfire_hash_key_field ON hangfire.hash (key, field);
CREATE INDEX IF NOT EXISTS ix_hangfire_list_key ON hangfire.list (key);
CREATE INDEX IF NOT EXISTS ix_hangfire_state_jobid ON hangfire.state (jobid);

-- Update job.stateid foreign key reference (only if tables have data)
-- This is safe to run multiple times
DO $$
BEGIN
    -- Only add the foreign key constraint if it doesn't already exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_hangfire_job_state' 
        AND table_name = 'job' 
        AND table_schema = 'hangfire'
    ) THEN
        -- Clean up any orphaned references first
        UPDATE hangfire.job SET stateid = NULL 
        WHERE stateid IS NOT NULL 
        AND NOT EXISTS (SELECT 1 FROM hangfire.jobstate WHERE id = job.stateid);
        
        -- Add the foreign key constraint
        ALTER TABLE hangfire.job 
        ADD CONSTRAINT fk_hangfire_job_state 
        FOREIGN KEY (stateid) REFERENCES hangfire.jobstate (id) 
        ON UPDATE CASCADE ON DELETE SET NULL;
    END IF;
END $$;

-- Add comments for documentation
COMMENT ON SCHEMA hangfire IS 'Hangfire background job processing tables';
COMMENT ON TABLE hangfire.job IS 'Stores background job definitions and metadata';
COMMENT ON TABLE hangfire.jobparameter IS 'Stores parameters for background jobs';
COMMENT ON TABLE hangfire.jobstate IS 'Tracks state changes for background jobs';
COMMENT ON TABLE hangfire.jobqueue IS 'Queue for pending background jobs';
COMMENT ON TABLE hangfire.server IS 'Tracks active Hangfire server instances';
COMMENT ON TABLE hangfire.set IS 'Stores sorted sets for Hangfire operations';
COMMENT ON TABLE hangfire.counter IS 'Stores counters for Hangfire statistics';
COMMENT ON TABLE hangfire.hash IS 'Stores hash data for Hangfire operations';
COMMENT ON TABLE hangfire.list IS 'Stores list data for Hangfire operations';
COMMENT ON TABLE hangfire.state IS 'Alternative state tracking for Hangfire jobs';

-- Record this migration in schema version tracking
INSERT INTO "_SchemaVersion" ("Filename")
VALUES ('20250803-03_CreateHangfireTables.sql')
ON CONFLICT ("Filename") DO NOTHING;