# Changelog

All notable changes to the BizConnect project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Enhanced PostgreSQL client (`psql`) discovery in database migration scripts
- Support for `PG_BIN` environment variable to specify custom PostgreSQL installation path
- Comprehensive platform-specific installation instructions for PostgreSQL client
- Automatic discovery of PostgreSQL client in common Windows installation paths
- Automatic discovery of PostgreSQL client in common Linux/macOS installation paths
- Integration tests for script PostgreSQL client discovery functionality

### Changed
- Improved error messages in migration scripts with detailed installation instructions
- Enhanced `scripts/update-db.ps1` with robust psql discovery logic
- Enhanced `scripts/update-db.sh` with robust psql discovery logic
- Updated `scripts/update-db` cross-platform launcher to pass through `PG_BIN` environment variable
- Expanded README.md with comprehensive PostgreSQL client installation guide

### Fixed
- Database migration scripts now work on Windows systems with PostgreSQL installed in standard locations
- Migration scripts provide helpful error messages when PostgreSQL client is not found
- Scripts now respect custom PostgreSQL installation paths via `PG_BIN` environment variable

## [Previous Releases]

### Database Migration Workflow
- Initial implementation of database-first migration workflow
- Cross-platform migration scripts (PowerShell and Bash)
- EF Core model scaffolding automation
- SQL migration file execution with error handling

### Core Application Features
- ASP.NET Core 8 MVC application with three-tier architecture
- PostgreSQL database with Entity Framework Core
- Cookie-based authentication with Admin/User roles
- BCrypt password hashing
- Comprehensive test coverage with xUnit
- Service Worker implementation for PWA capabilities
- Responsive UI with Bootstrap and custom CSS
- Health checks and monitoring capabilities
