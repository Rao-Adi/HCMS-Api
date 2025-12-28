using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("UserRoles")]
public class UserRole
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
