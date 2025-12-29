using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("UserRoles")]
public class UserRole : AuditableEntity
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }

    public DateTime AssignedAt { get; set; }
    public string AssignedBy { get; set; } = null!;
     
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
