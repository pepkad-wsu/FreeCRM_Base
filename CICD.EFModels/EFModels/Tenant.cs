using System;
using System.Collections.Generic;

namespace CICD.EFModels.EFModels;

public partial class Tenant
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string TenantCode { get; set; } = null!;

    public bool Enabled { get; set; }

    public DateTime Added { get; set; }

    public string? AddedBy { get; set; }

    public DateTime LastModified { get; set; }

    public string? LastModifiedBy { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
