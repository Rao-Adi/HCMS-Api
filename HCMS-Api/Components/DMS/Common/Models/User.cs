using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Users")]
public class User
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;

    [MaxLength(100)]
    public string UserName { get; set; } = null!;

    [MaxLength(255)]
    public string Email { get; set; } = null!;

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; } = null!;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

