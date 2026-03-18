using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("UserRoles")]
public class UserRole : AuditableEntity
{
    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;
     
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}


public class UserRoleReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;
     
}

public class UserRoleCreateDto
{
    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;
     
}

public class UserRoleUpdateDto
{
    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}