using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

/// <summary>
/// Application users with authentication and authorization data
/// </summary>
public partial class User
{
    /// <summary>
    /// Primary key, auto-incrementing user identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique username for authentication
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// BCrypt hashed password
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// User role: Admin or User
    /// </summary>
    public string Role { get; set; } = null!;

    /// <summary>
    /// Timestamp when user was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when user was last updated (auto-updated by trigger)
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Whether the user account is active and can log in
    /// </summary>
    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public virtual ICollection<KbankOddRegistration> KbankOddRegistrations { get; set; } = new List<KbankOddRegistration>();
}
