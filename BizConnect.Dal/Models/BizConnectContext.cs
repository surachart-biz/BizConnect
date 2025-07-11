using System;
using System.Collections.Generic;
using BizConnect.Dal.Models;
using Microsoft.EntityFrameworkCore;

namespace BizConnect.Dal;

public partial class BizConnectContext : DbContext
{
    public BizConnectContext(DbContextOptions<BizConnectContext> options)
        : base(options)
    {
    }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

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
