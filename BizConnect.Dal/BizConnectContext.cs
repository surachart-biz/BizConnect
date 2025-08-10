using System;
using System.Collections.Generic;
using BizConnect.Dal.Models;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Dal;

public partial class BizConnectContext : DbContext
{
    public BizConnectContext()
    {
    }

    public BizConnectContext(DbContextOptions<BizConnectContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ActiveOddRegistration> ActiveOddRegistrations { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<ExpiredOtacCode> ExpiredOtacCodes { get; set; }

    public virtual DbSet<KbankOddRegistration> KbankOddRegistrations { get; set; }

    public virtual DbSet<MvMonthlyStatistic> MvMonthlyStatistics { get; set; }

    public virtual DbSet<SchemaVersion> SchemaVersions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<VBranchPerformance> VBranchPerformances { get; set; }

    public virtual DbSet<VDatabaseSizeMonitor> VDatabaseSizeMonitors { get; set; }

    public virtual DbSet<VOtacTrend> VOtacTrends { get; set; }

    public virtual DbSet<VQueryPerformance> VQueryPerformances { get; set; }

    public virtual DbSet<VRealtimeDashboardStat> VRealtimeDashboardStats { get; set; }

    public virtual DbSet<VRecentActivity> VRecentActivities { get; set; }

    public virtual DbSet<VSystemHealthStat> VSystemHealthStats { get; set; }

    public virtual DbSet<VwOtacLifecycleStat> VwOtacLifecycleStats { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Database=bizconnect_test;Username=postgres;Password=bizitadmin");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<ActiveOddRegistration>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ActiveOddRegistrations");

            entity.Property(e => e.AccountNo).HasMaxLength(20);
            entity.Property(e => e.BranchCode).HasMaxLength(10);
            entity.Property(e => e.BranchName).HasMaxLength(100);
            entity.Property(e => e.BranchNameEn).HasMaxLength(100);
            entity.Property(e => e.BranchNameTh).HasMaxLength(100);
            entity.Property(e => e.EspaId).HasMaxLength(40);
            entity.Property(e => e.ExternalReference).HasMaxLength(50);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.GeneratedByUsername).HasMaxLength(100);
            entity.Property(e => e.IdType).HasMaxLength(20);
            entity.Property(e => e.IdValue).HasMaxLength(30);
            entity.Property(e => e.MobileNo).HasMaxLength(20);
            entity.Property(e => e.MobileNumber).HasMaxLength(20);
            entity.Property(e => e.NationalId).HasMaxLength(20);
            entity.Property(e => e.OtacCode).HasMaxLength(8);
            entity.Property(e => e.OtacState).HasMaxLength(20);
            entity.Property(e => e.RegId).HasMaxLength(40);
            entity.Property(e => e.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("Branch_pkey");

            entity.ToTable("Branch", tb => tb.HasComment("Bank branch information with multi-language support for ODD registration management"));

            entity.HasIndex(e => e.Code, "IX_Branch_Code").IsUnique();

            entity.HasIndex(e => e.IsActive, "IX_Branch_IsActive");

            entity.HasIndex(e => e.Name, "IX_Branch_Name");

            entity.HasIndex(e => new { e.NameEn, e.NameTh, e.Name, e.IsActive }, "idx_branch_multilang_names").HasFilter("(\"IsActive\" = true)");

            entity.Property(e => e.BranchId).HasComment("Primary key, auto-incrementing branch identifier");
            entity.Property(e => e.Address).HasComment("Default physical address (fallback)");
            entity.Property(e => e.AddressEn).HasComment("Physical address in English language");
            entity.Property(e => e.AddressTh).HasComment("Physical address in Thai language");
            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .HasComment("Unique branch code for identification");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("Timestamp when branch was created");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("Whether the branch is currently active and accepting registrations");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasComment("Default branch name (fallback)");
            entity.Property(e => e.NameEn)
                .HasMaxLength(100)
                .HasComment("Branch name in English language");
            entity.Property(e => e.NameTh)
                .HasMaxLength(100)
                .HasComment("Branch name in Thai language");
            entity.Property(e => e.UpdatedAt).HasComment("Timestamp when branch was last updated (auto-updated by trigger)");
        });

        modelBuilder.Entity<ExpiredOtacCode>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("ExpiredOtacCodes");

            entity.Property(e => e.BranchCode).HasMaxLength(10);
            entity.Property(e => e.BranchName).HasMaxLength(100);
            entity.Property(e => e.BranchNameEn).HasMaxLength(100);
            entity.Property(e => e.BranchNameTh).HasMaxLength(100);
            entity.Property(e => e.ExternalReference).HasMaxLength(50);
            entity.Property(e => e.GeneratedByUsername).HasMaxLength(100);
            entity.Property(e => e.OtacCode).HasMaxLength(8);
            entity.Property(e => e.OtacState).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<KbankOddRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("KbankOddRegistration_pkey");

            entity.ToTable("KbankOddRegistration", tb => tb.HasComment("Consolidated table tracking KBank Online Direct Debit registration requests with integrated OTAC functionality"));

            entity.HasIndex(e => e.BranchId, "IX_KbankOddRegistration_BranchId");

            entity.HasIndex(e => e.CreatedAt, "IX_KbankOddRegistration_CreatedAt");

            entity.HasIndex(e => e.ExternalReference, "IX_KbankOddRegistration_ExternalReference_NotNull")
                .IsUnique()
                .HasFilter("(\"ExternalReference\" IS NOT NULL)");

            entity.HasIndex(e => e.GeneratedByUserId, "IX_KbankOddRegistration_GeneratedByUserId");

            entity.HasIndex(e => new { e.IdType, e.IdValue }, "IX_KbankOddRegistration_IdType_IdValue");

            entity.HasIndex(e => e.MobileNumber, "IX_KbankOddRegistration_MobileNumber").HasFilter("(\"MobileNumber\" IS NOT NULL)");

            entity.HasIndex(e => e.NationalId, "IX_KbankOddRegistration_NationalId").HasFilter("(\"NationalId\" IS NOT NULL)");

            entity.HasIndex(e => e.OtacCode, "IX_KbankOddRegistration_OtacCode");

            entity.HasIndex(e => new { e.OtacCode, e.OtacState, e.OtacExpiresAt }, "IX_KbankOddRegistration_OtacCode_State_Expires");

            entity.HasIndex(e => e.OtacExpiresAt, "IX_KbankOddRegistration_OtacExpiresAt");

            entity.HasIndex(e => e.OtacState, "IX_KbankOddRegistration_OtacState");

            entity.HasIndex(e => new { e.OtacState, e.OtacExpiresAt }, "IX_KbankOddRegistration_OtacState_ExpiresAt").HasFilter("((\"OtacState\")::text = ANY ((ARRAY['Generated'::character varying, 'Validated'::character varying])::text[]))");

            entity.HasIndex(e => e.RegId, "IX_KbankOddRegistration_RegId");

            entity.HasIndex(e => new { e.OtacState, e.Status, e.CreatedAt }, "IX_KbankOddRegistration_State_Status_Created");

            entity.HasIndex(e => e.Status, "IX_KbankOddRegistration_Status");

            entity.HasIndex(e => e.StatusMessageEn, "IX_KbankOddRegistration_StatusMessageEn").HasFilter("(\"StatusMessageEn\" IS NOT NULL)");

            entity.HasIndex(e => e.StatusMessageTh, "IX_KbankOddRegistration_StatusMessageTh").HasFilter("(\"StatusMessageTh\" IS NOT NULL)");

            entity.HasIndex(e => new { e.OtacState, e.CreatedAt, e.AccountNo }, "IX_KbankOddRegistration_UsedRecords").HasFilter("((\"OtacState\")::text = 'Used'::text)");

            entity.HasIndex(e => e.OtacCode, "UQ_KbankOddRegistration_OtacCode").IsUnique();

            entity.HasIndex(e => new { e.BranchId, e.Status, e.CreatedAt }, "idx_kbank_branch_performance_modern").HasFilter("((\"BranchId\" IS NOT NULL) AND (\"Status\" IS NOT NULL))");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "idx_kbank_status_created_modern_ui")
                .IsDescending(false, true)
                .HasFilter("(\"Status\" IS NOT NULL)");

            entity.HasIndex(e => new { e.GeneratedByUserId, e.Status, e.CreatedAt }, "idx_kbank_user_productivity").HasFilter("(\"Status\" IS NOT NULL)");

            entity.Property(e => e.AccountNo)
                .HasMaxLength(20)
                .HasComment("Bank account number for the ODD registration (10-15 digits)");
            entity.Property(e => e.AttemptCount)
                .HasDefaultValue(0)
                .HasComment("Number of OTAC validation attempts made");
            entity.Property(e => e.BranchId).HasComment("Foreign key reference to Branch table");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("Timestamp when record was created");
            entity.Property(e => e.ErrorMessageEn).HasComment("Error message in English language for UI display");
            entity.Property(e => e.ErrorMessageTh).HasComment("Error message in Thai language for UI display");
            entity.Property(e => e.EspaId)
                .HasMaxLength(40)
                .HasComment("ESPA ID returned by KBank after successful registration");
            entity.Property(e => e.ExternalReference)
                .HasMaxLength(50)
                .HasComment("Unique external reference generated by BizConnect (format: BIZyyyyMMddHHmmssfff). NULL during OTAC generation/validation phases, assigned upon guest registration submission per KBank ODD V1.9.7 business flow.");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasComment("Customer full name for registration");
            entity.Property(e => e.GeneratedByUserId).HasComment("User ID who generated this OTAC code");
            entity.Property(e => e.IdType)
                .HasMaxLength(20)
                .HasComment("Type of identification: National ID, Passport, Tax ID, or Company Tax ID");
            entity.Property(e => e.IdValue)
                .HasMaxLength(30)
                .HasComment("Identification document number/value corresponding to the selected ID type");
            entity.Property(e => e.IsLocked)
                .HasDefaultValue(false)
                .HasComment("TRUE if OTAC is locked due to too many failed attempts");
            entity.Property(e => e.LastAttemptAt).HasComment("Timestamp of last OTAC validation attempt");
            entity.Property(e => e.LastAttemptIp)
                .HasMaxLength(45)
                .HasComment("IP address of last OTAC validation attempt");
            entity.Property(e => e.MobileNo)
                .HasMaxLength(20)
                .HasComment("Customer mobile number in format 08xxxxxxxx or +66xxxxxxxx");
            entity.Property(e => e.MobileNumber)
                .HasMaxLength(20)
                .HasComment("Mobile phone number in international format (alternative to MobileNo)");
            entity.Property(e => e.NationalId)
                .HasMaxLength(20)
                .HasComment("National identification number (separate from IdValue for clarity)");
            entity.Property(e => e.OtacCode)
                .HasMaxLength(8)
                .HasComment("The actual OTAC code (8-character alphanumeric)");
            entity.Property(e => e.OtacExpiresAt).HasComment("When the OTAC code expires (typically 30 minutes from creation)");
            entity.Property(e => e.OtacState)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Generated'::character varying")
                .HasComment("OTAC lifecycle state. CRITICAL: Used state is PERMANENT - required for daily payment processing. Valid states: Generated, Validated, Used, Expired, Invalidated, Purged. State transitions: Generated→Validated→Used (permanent), Generated/Validated→Expired→Purged (cleanup), Generated→Invalidated→Purged (failed validation)");
            entity.Property(e => e.RegId)
                .HasMaxLength(40)
                .HasComment("Registration ID returned by KBank after initialization");
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(10)
                .HasComment("Return code from KBank status update");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasComment("Registration status: NULL (unprocessed), Pending, Success, or Fail - set by KBank integration");
            entity.Property(e => e.StatusMessageEn).HasComment("Status message in English language for UI display");
            entity.Property(e => e.StatusMessageTh).HasComment("Status message in Thai language for UI display");
            entity.Property(e => e.UpdatedAt).HasComment("Timestamp when record was last updated (auto-updated by trigger)");

            entity.HasOne(d => d.Branch).WithMany(p => p.KbankOddRegistrations)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KbankOddRegistration_Branch");

            entity.HasOne(d => d.GeneratedByUser).WithMany(p => p.KbankOddRegistrations)
                .HasForeignKey(d => d.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_KbankOddRegistration_GeneratedByUserId");
        });

        modelBuilder.Entity<MvMonthlyStatistic>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("mv_monthly_statistics");

            entity.Property(e => e.ApprovedCount).HasColumnName("approved_count");
            entity.Property(e => e.AvgOtacAttempts).HasColumnName("avg_otac_attempts");
            entity.Property(e => e.BranchesUsed).HasColumnName("branches_used");
            entity.Property(e => e.LockedOtacCount).HasColumnName("locked_otac_count");
            entity.Property(e => e.MaxOtacAttempts).HasColumnName("max_otac_attempts");
            entity.Property(e => e.Month).HasColumnName("month");
            entity.Property(e => e.PendingCount).HasColumnName("pending_count");
            entity.Property(e => e.RejectedCount).HasColumnName("rejected_count");
            entity.Property(e => e.SuccessRate).HasColumnName("success_rate");
            entity.Property(e => e.TotalRegistrations).HasColumnName("total_registrations");
            entity.Property(e => e.UniqueUsers).HasColumnName("unique_users");
        });

        modelBuilder.Entity<SchemaVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("_SchemaVersion_pkey");

            entity.ToTable("_SchemaVersion", tb => tb.HasComment("Tracks applied database migration files"));

            entity.HasIndex(e => e.Filename, "IX_SchemaVersion_Filename").IsUnique();

            entity.HasIndex(e => e.Filename, "_SchemaVersion_Filename_key").IsUnique();

            entity.Property(e => e.AppliedAt)
                .HasDefaultValueSql("now()")
                .HasComment("Timestamp when the migration was applied");
            entity.Property(e => e.Filename)
                .HasMaxLength(255)
                .HasComment("Name of the migration file that was applied");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Users_pkey");

            entity.ToTable(tb => tb.HasComment("Application users with authentication and authorization data"));

            entity.HasIndex(e => e.IsActive, "IX_Users_IsActive");

            entity.HasIndex(e => e.Role, "IX_Users_Role");

            entity.HasIndex(e => e.Username, "IX_Users_Username").IsUnique();

            entity.HasIndex(e => e.LastLoginAt, "idx_user_last_login")
                .IsDescending()
                .HasFilter("((\"IsActive\" = true) AND (\"LastLoginAt\" IS NOT NULL))");

            entity.Property(e => e.Id).HasComment("Primary key, auto-incrementing user identifier");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("Timestamp when user was created");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("Whether the user account is active and can log in");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasComment("BCrypt hashed password");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasComment("User role: Admin or User");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasComment("Timestamp when user was last updated (auto-updated by trigger)");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .HasComment("Unique username for authentication");
        });

        modelBuilder.Entity<VBranchPerformance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_branch_performance");

            entity.Property(e => e.AvgOtacAttempts).HasColumnName("avg_otac_attempts");
            entity.Property(e => e.BranchCode)
                .HasMaxLength(10)
                .HasColumnName("branch_code");
            entity.Property(e => e.BranchNameEn)
                .HasMaxLength(100)
                .HasColumnName("branch_name_en");
            entity.Property(e => e.BranchNameTh)
                .HasMaxLength(100)
                .HasColumnName("branch_name_th");
            entity.Property(e => e.FailedRegistrations).HasColumnName("failed_registrations");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.LockedOtacCount).HasColumnName("locked_otac_count");
            entity.Property(e => e.MonthCount).HasColumnName("month_count");
            entity.Property(e => e.PendingRegistrations).HasColumnName("pending_registrations");
            entity.Property(e => e.SuccessRate).HasColumnName("success_rate");
            entity.Property(e => e.SuccessfulRegistrations).HasColumnName("successful_registrations");
            entity.Property(e => e.TodayCount).HasColumnName("today_count");
            entity.Property(e => e.TotalRegistrations).HasColumnName("total_registrations");
            entity.Property(e => e.WeekCount).HasColumnName("week_count");
        });

        modelBuilder.Entity<VDatabaseSizeMonitor>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_database_size_monitor");

            entity.Property(e => e.Size).HasColumnName("size");
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
        });

        modelBuilder.Entity<VOtacTrend>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_otac_trends");

            entity.Property(e => e.AvgAttempts).HasColumnName("avg_attempts");
            entity.Property(e => e.Date).HasColumnName("date");
            entity.Property(e => e.LockedCodes).HasColumnName("locked_codes");
            entity.Property(e => e.MaxAttempts).HasColumnName("max_attempts");
            entity.Property(e => e.TotalExpired).HasColumnName("total_expired");
            entity.Property(e => e.TotalGenerated).HasColumnName("total_generated");
            entity.Property(e => e.TotalUsed).HasColumnName("total_used");
            entity.Property(e => e.TotalValidated).HasColumnName("total_validated");
        });

        modelBuilder.Entity<VQueryPerformance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_query_performance");

            entity.Property(e => e.LastChecked).HasColumnName("last_checked");
            entity.Property(e => e.ObjectName).HasColumnName("object_name");
            entity.Property(e => e.RecordCount).HasColumnName("record_count");
            entity.Property(e => e.ViewName).HasColumnName("view_name");
        });

        modelBuilder.Entity<VRealtimeDashboardStat>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_realtime_dashboard_stats");

            entity.Property(e => e.ActiveOtacCodes).HasColumnName("active_otac_codes");
            entity.Property(e => e.ActiveUsers).HasColumnName("active_users");
            entity.Property(e => e.ApprovedRegistrations).HasColumnName("approved_registrations");
            entity.Property(e => e.AvgOtacAttempts).HasColumnName("avg_otac_attempts");
            entity.Property(e => e.MaxOtacAttempts).HasColumnName("max_otac_attempts");
            entity.Property(e => e.OverallSuccessRate).HasColumnName("overall_success_rate");
            entity.Property(e => e.PendingRegistrations).HasColumnName("pending_registrations");
            entity.Property(e => e.RegistrationsMonth).HasColumnName("registrations_month");
            entity.Property(e => e.RegistrationsToday).HasColumnName("registrations_today");
            entity.Property(e => e.RegistrationsWeek).HasColumnName("registrations_week");
            entity.Property(e => e.RejectedRegistrations).HasColumnName("rejected_registrations");
            entity.Property(e => e.SnapshotTime).HasColumnName("snapshot_time");
            entity.Property(e => e.UsedOtacCodes).HasColumnName("used_otac_codes");
            entity.Property(e => e.UsersActiveWeek).HasColumnName("users_active_week");
            entity.Property(e => e.UsersOnlineToday).HasColumnName("users_online_today");
            entity.Property(e => e.ValidatedOtacCodes).HasColumnName("validated_otac_codes");
        });

        modelBuilder.Entity<VRecentActivity>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_recent_activities");

            entity.Property(e => e.ActivityType).HasColumnName("activity_type");
            entity.Property(e => e.BranchCode)
                .HasMaxLength(10)
                .HasColumnName("branch_code");
            entity.Property(e => e.BranchName).HasComment("Computed branch name column that prefers English (NameEn), falls back to Thai (NameTh), then \"Unknown\". \nAdded for DashboardService.cs compatibility.");
            entity.Property(e => e.BranchNameEn)
                .HasMaxLength(100)
                .HasColumnName("branch_name_en");
            entity.Property(e => e.BranchNameTh)
                .HasMaxLength(100)
                .HasColumnName("branch_name_th");
            entity.Property(e => e.ExternalReference).HasMaxLength(50);
            entity.Property(e => e.GeneratedByUsername)
                .HasMaxLength(100)
                .HasColumnName("generated_by_username");
            entity.Property(e => e.OtacCode).HasMaxLength(8);
            entity.Property(e => e.OtacState).HasMaxLength(20);
            entity.Property(e => e.PrioritySort).HasColumnName("priority_sort");
            entity.Property(e => e.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<VSystemHealthStat>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("v_system_health_stats");

            entity.Property(e => e.ActiveConnections).HasColumnName("active_connections");
            entity.Property(e => e.AvgProcessingTimeSeconds).HasColumnName("avg_processing_time_seconds");
            entity.Property(e => e.DatabaseSizeBytes).HasColumnName("database_size_bytes");
            entity.Property(e => e.ExpiredOtacCount).HasColumnName("expired_otac_count");
            entity.Property(e => e.RegistrationsPerHour).HasColumnName("registrations_per_hour");
            entity.Property(e => e.RegistrationsToday).HasColumnName("registrations_today");
            entity.Property(e => e.SnapshotTime).HasColumnName("snapshot_time");
            entity.Property(e => e.TodaysErrorRate).HasColumnName("todays_error_rate");
        });

        modelBuilder.Entity<VwOtacLifecycleStat>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_OtacLifecycleStats");

            entity.Property(e => e.OtacState).HasMaxLength(20);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
