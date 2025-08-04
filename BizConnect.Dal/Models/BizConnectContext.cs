using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Dal.Models;

public partial class BizConnectContext : DbContext
{
    public BizConnectContext(DbContextOptions<BizConnectContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Counter> Counters { get; set; }

    public virtual DbSet<Hash> Hashes { get; set; }

    public virtual DbSet<Job> Jobs { get; set; }

    public virtual DbSet<Jobparameter> Jobparameters { get; set; }

    public virtual DbSet<Jobqueue> Jobqueues { get; set; }

    public virtual DbSet<Jobstate> Jobstates { get; set; }

    public virtual DbSet<KbankOddRegistration> KbankOddRegistrations { get; set; }

    public virtual DbSet<List> Lists { get; set; }

    public virtual DbSet<Lock> Locks { get; set; }

    public virtual DbSet<Schema> Schemas { get; set; }

    public virtual DbSet<SchemaVersion> SchemaVersions { get; set; }

    public virtual DbSet<Server> Servers { get; set; }

    public virtual DbSet<Set> Sets { get; set; }

    public virtual DbSet<State> States { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("Branch_pkey");

            entity.ToTable("Branch", tb => tb.HasComment("Bank branch information for ODD registration management"));

            entity.HasIndex(e => e.Code, "IX_Branch_Code").IsUnique();

            entity.HasIndex(e => e.IsActive, "IX_Branch_IsActive");

            entity.HasIndex(e => e.Name, "IX_Branch_Name");

            entity.Property(e => e.BranchId).HasComment("Primary key, auto-incrementing branch identifier");
            entity.Property(e => e.Address).HasComment("Physical address of the branch");
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
                .HasComment("Human-readable branch name");
            entity.Property(e => e.UpdatedAt).HasComment("Timestamp when branch was last updated (auto-updated by trigger)");
        });

        modelBuilder.Entity<Counter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("counter_pkey");

            entity.ToTable("counter", "hangfire", tb => tb.HasComment("Stores counters for Hangfire statistics"));

            entity.HasIndex(e => e.Expireat, "ix_hangfire_counter_expireat");

            entity.HasIndex(e => e.Key, "ix_hangfire_counter_key");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Expireat).HasColumnName("expireat");
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Hash>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("hash_pkey");

            entity.ToTable("hash", "hangfire", tb => tb.HasComment("Stores hash data for Hangfire operations"));

            entity.HasIndex(e => e.Key, "ix_hangfire_hash_key");

            entity.HasIndex(e => new { e.Key, e.Field }, "uix_hangfire_hash_key_field").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Expireat).HasColumnName("expireat");
            entity.Property(e => e.Field).HasColumnName("field");
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("job_pkey");

            entity.ToTable("job", "hangfire", tb => tb.HasComment("Stores background job definitions and metadata"));

            entity.HasIndex(e => e.Expireat, "ix_hangfire_job_expireat");

            entity.HasIndex(e => e.Statename, "ix_hangfire_job_statename");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Arguments).HasColumnName("arguments");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Expireat).HasColumnName("expireat");
            entity.Property(e => e.Invocationdata).HasColumnName("invocationdata");
            entity.Property(e => e.Stateid).HasColumnName("stateid");
            entity.Property(e => e.Statename).HasColumnName("statename");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");

            entity.HasOne(d => d.State).WithMany(p => p.Jobs)
                .HasForeignKey(d => d.Stateid)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_hangfire_job_state");
        });

        modelBuilder.Entity<Jobparameter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("jobparameter_pkey");

            entity.ToTable("jobparameter", "hangfire", tb => tb.HasComment("Stores parameters for background jobs"));

            entity.HasIndex(e => new { e.Jobid, e.Name }, "ix_hangfire_jobparameter_jobid_name");

            entity.HasIndex(e => new { e.Jobid, e.Name }, "ix_hangfire_jobparameter_jobidandname");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Jobid).HasColumnName("jobid");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasOne(d => d.Job).WithMany(p => p.Jobparameters)
                .HasForeignKey(d => d.Jobid)
                .HasConstraintName("fk_hangfire_jobparameter_job");
        });

        modelBuilder.Entity<Jobqueue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("jobqueue_pkey");

            entity.ToTable("jobqueue", "hangfire", tb => tb.HasComment("Queue for pending background jobs"));

            entity.HasIndex(e => new { e.Jobid, e.Queue }, "ix_hangfire_jobqueue_jobidandqueue");

            entity.HasIndex(e => new { e.Queue, e.Fetchedat, e.Jobid }, "ix_hangfire_jobqueue_queue_fetchedat_jobid");

            entity.HasIndex(e => new { e.Queue, e.Fetchedat }, "ix_hangfire_jobqueue_queueandfetchedat");

            entity.HasIndex(e => new { e.Queue, e.Fetchedat, e.Jobid }, "jobqueue_queue_fetchat_jobid");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Fetchedat).HasColumnName("fetchedat");
            entity.Property(e => e.Jobid).HasColumnName("jobid");
            entity.Property(e => e.Queue).HasColumnName("queue");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");
        });

        modelBuilder.Entity<Jobstate>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("jobstate_pkey");

            entity.ToTable("jobstate", "hangfire", tb => tb.HasComment("Tracks state changes for background jobs"));

            entity.HasIndex(e => e.Jobid, "ix_hangfire_jobstate_jobid");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.Jobid).HasColumnName("jobid");
            entity.Property(e => e.Name)
                .HasMaxLength(20)
                .HasColumnName("name");
            entity.Property(e => e.Reason)
                .HasMaxLength(100)
                .HasColumnName("reason");

            entity.HasOne(d => d.Job).WithMany(p => p.Jobstates)
                .HasForeignKey(d => d.Jobid)
                .HasConstraintName("fk_hangfire_jobstate_job");
        });

        modelBuilder.Entity<KbankOddRegistration>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("KbankOddRegistration_pkey");

            entity.ToTable("KbankOddRegistration", tb => tb.HasComment("Consolidated table tracking KBank Online Direct Debit registration requests with integrated OTAC functionality"));

            entity.HasIndex(e => e.BranchId, "IX_KbankOddRegistration_BranchId");

            entity.HasIndex(e => e.OtacExpiresAt, "IX_KbankOddRegistration_CodeExpiresAt");

            entity.HasIndex(e => e.CreatedAt, "IX_KbankOddRegistration_CreatedAt");

            entity.HasIndex(e => e.ExternalReference, "IX_KbankOddRegistration_ExternalReference").IsUnique();

            entity.HasIndex(e => e.GeneratedByUserId, "IX_KbankOddRegistration_GeneratedByUserId");

            entity.HasIndex(e => new { e.IdType, e.IdValue }, "IX_KbankOddRegistration_IdType_IdValue");

            entity.HasIndex(e => e.OtacCode, "IX_KbankOddRegistration_OtacCode");

            entity.HasIndex(e => new { e.OtacCode, e.OtacState, e.OtacExpiresAt }, "IX_KbankOddRegistration_OtacCode_State_Expires");

            entity.HasIndex(e => e.OtacExpiresAt, "IX_KbankOddRegistration_OtacExpiresAt");

            entity.HasIndex(e => e.OtacState, "IX_KbankOddRegistration_OtacState");

            entity.HasIndex(e => e.RegId, "IX_KbankOddRegistration_RegId");

            entity.HasIndex(e => new { e.OtacState, e.Status, e.CreatedAt }, "IX_KbankOddRegistration_State_Status_Created");

            entity.HasIndex(e => e.Status, "IX_KbankOddRegistration_Status");

            entity.HasIndex(e => new { e.Status, e.CreatedAt }, "IX_KbankOddRegistration_Status_CreatedAt");

            entity.HasIndex(e => e.ExternalReference, "KbankOddRegistration_ExternalReference_key").IsUnique();

            entity.HasIndex(e => e.OtacCode, "UQ_KbankOddRegistration_OtacCode").IsUnique();

            entity.Property(e => e.AccountNo)
                .HasMaxLength(20)
                .HasComment("Bank account number for the ODD registration (10-15 digits)");
            entity.Property(e => e.AttemptCount)
                .HasDefaultValue(0)
                .HasComment("Number of OTAC validation attempts made");
            entity.Property(e => e.BranchId).HasComment("Foreign key reference to Branch table");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.EspaId)
                .HasMaxLength(40)
                .HasComment("ESPA ID returned by KBank after successful registration");
            entity.Property(e => e.ExternalReference)
                .HasMaxLength(40)
                .HasComment("Unique external reference generated by BizConnect (format: BIZyyyyMMddHHmmssfff)");
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .HasComment("User full name for registration");
            entity.Property(e => e.GeneratedByUserId).HasComment("User ID who generated this OTAC code");
            entity.Property(e => e.IdType)
                .HasMaxLength(30)
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
                .HasComment("User mobile number in format 08xxxxxxxx or +66xxxxxxxx");
            entity.Property(e => e.OtacCode)
                .HasMaxLength(8)
                .HasComment("The actual OTAC code (8-character alphanumeric)");
            entity.Property(e => e.OtacExpiresAt).HasComment("When the OTAC code expires (typically 30 minutes from creation)");
            entity.Property(e => e.OtacState)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Generated'::character varying")
                .HasComment("OTAC state: Generated → Validated → Used");
            entity.Property(e => e.RegId)
                .HasMaxLength(40)
                .HasComment("Registration ID returned by KBank after initialization");
            entity.Property(e => e.ReturnCode)
                .HasMaxLength(10)
                .HasComment("Return code from KBank status update");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasComment("Registration status: Pending, Success, or Fail");

            entity.HasOne(d => d.Branch).WithMany(p => p.KbankOddRegistrations)
                .HasForeignKey(d => d.BranchId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_KbankOddRegistration_Branch");

            entity.HasOne(d => d.GeneratedByUser).WithMany(p => p.KbankOddRegistrations)
                .HasForeignKey(d => d.GeneratedByUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_KbankOddRegistration_GeneratedByUserId");
        });

        modelBuilder.Entity<List>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("list_pkey");

            entity.ToTable("list", "hangfire", tb => tb.HasComment("Stores list data for Hangfire operations"));

            entity.HasIndex(e => e.Key, "ix_hangfire_list_key");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Expireat).HasColumnName("expireat");
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Lock>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("lock", "hangfire");

            entity.HasIndex(e => e.Resource, "lock_resource_key").IsUnique();

            entity.Property(e => e.Acquired)
                .HasColumnType("timestamp without time zone")
                .HasColumnName("acquired");
            entity.Property(e => e.Resource).HasColumnName("resource");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");
        });

        modelBuilder.Entity<Schema>(entity =>
        {
            entity.HasKey(e => e.Version).HasName("schema_pkey");

            entity.ToTable("schema", "hangfire");

            entity.Property(e => e.Version)
                .ValueGeneratedNever()
                .HasColumnName("version");
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

        modelBuilder.Entity<Server>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("server_pkey");

            entity.ToTable("server", "hangfire", tb => tb.HasComment("Tracks active Hangfire server instances"));

            entity.HasIndex(e => e.Lastheartbeat, "ix_hangfire_server_lastheartbeat");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.Lastheartbeat).HasColumnName("lastheartbeat");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");
        });

        modelBuilder.Entity<Set>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("set_pkey");

            entity.ToTable("set", "hangfire", tb => tb.HasComment("Stores sorted sets for Hangfire operations"));

            entity.HasIndex(e => new { e.Key, e.Score }, "ix_hangfire_set_key_score");

            entity.HasIndex(e => new { e.Key, e.Value }, "uix_hangfire_set_key_value").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Expireat).HasColumnName("expireat");
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");
            entity.Property(e => e.Value)
                .HasMaxLength(256)
                .HasColumnName("value");
        });

        modelBuilder.Entity<State>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("state_pkey");

            entity.ToTable("state", "hangfire", tb => tb.HasComment("Alternative state tracking for Hangfire jobs"));

            entity.HasIndex(e => e.Jobid, "ix_hangfire_state_jobid");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Createdat).HasColumnName("createdat");
            entity.Property(e => e.Data).HasColumnName("data");
            entity.Property(e => e.Jobid).HasColumnName("jobid");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.Updatecount)
                .HasDefaultValue(0)
                .HasColumnName("updatecount");

            entity.HasOne(d => d.Job).WithMany(p => p.States)
                .HasForeignKey(d => d.Jobid)
                .HasConstraintName("fk_hangfire_state_job");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Users_pkey");

            entity.ToTable(tb => tb.HasComment("Application users with authentication and authorization data"));

            entity.HasIndex(e => e.IsActive, "IX_Users_IsActive");

            entity.HasIndex(e => e.Role, "IX_Users_Role");

            entity.HasIndex(e => e.Username, "IX_Users_Username").IsUnique();

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

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
