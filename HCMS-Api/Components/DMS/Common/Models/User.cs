using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Users")]
public class User : AuditableEntity
{
    [Key]
    public int Id { get; set; }


    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;



    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;

    [MaxLength(100)]
    public string EmployeeName { get; set; } = null!;

    [MaxLength(255)]
    public string Email { get; set; } = null!;

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }
    public string? DesignationCode { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;

    [MaxLength(100)]
    public string EmployeeName { get; set; } = null!;

    [MaxLength(255)]
    public string Email { get; set; } = null!;

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }

    public string? Designation { get; set; }
    public string? DesignationCode { get; set; }


    public int? RoleId { get; set; }
    public string? UserRole { get; set; }
    public string? ReportingTo { get; set; }
    public string Grade { get; set; }
    public string? DateOfJoining { get; set; }

}


public class UserCreateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;

    [MaxLength(100)]
    public string EmployeeName { get; set; } = null!;

    [MaxLength(255)]
    public string Email { get; set; } = null!;
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }
     
    public string? DesignationCode { get; set; }

    public string? ReportingTo { get; set; }

    public string Grade { get; set; }
    public DateTime? DateOfJoining { get; set; }

}


public class UserUpdateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;

    [MaxLength(100)]
    public string EmployeeName { get; set; } = null!;

    [MaxLength(255)]
    public string Email { get; set; } = null!;
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; }
     
    public string? SubDepartmentCode { get; set; }
     
    public string? BusinessDomainCode { get; set; } 
    public string? DesignationCode { get; set; }
    public string? ReportingTo { get; set; }

    public string Grade { get; set; }
    public DateTime? DateOfJoining { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

