using System;
using System.Collections.Generic;

namespace CICD.EFModels.EFModels;

public partial class User
{
    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Username { get; set; } = null!;

    public string? EmployeeId { get; set; }

    public string? Title { get; set; }

    public bool Enabled { get; set; }

    public DateTime? LastLogin { get; set; }

    public string? LastLoginSource { get; set; }

    public bool Admin { get; set; }

    public bool ManageFiles { get; set; }
    
    public string? Password { get; set; }

    public bool PreventPasswordChange { get; set; }

    public int? FailedLoginAttempts { get; set; }

    public DateTime? LastLockoutDate { get; set; }

    public string? Source { get; set; }

    public DateTime Added { get; set; }

    public string? AddedBy { get; set; }

    public DateTime LastModified { get; set; }

    public string? LastModifiedBy { get; set; }

    public bool Deleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? Preferences { get; set; }

    public virtual ICollection<FileStorage> FileStorages { get; set; } = new List<FileStorage>();

    public virtual Tenant Tenant { get; set; } = null!;
}
