using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Users")]
public class User : AuditableEntity
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

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserReadDto : AuditableEntity
{
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

}


public class UserCreateDto
{
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

}


public class UserUpdateDto
{
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
}

