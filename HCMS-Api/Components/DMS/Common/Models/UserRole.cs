using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("UserRoles")]
public class UserRole : AuditableEntity
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;
     
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}


public class UserRoleReadDto : AuditableEntity
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;
     
}

public class UserRoleCreateDto
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;
     
}

public class UserRoleUpdateDto
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}