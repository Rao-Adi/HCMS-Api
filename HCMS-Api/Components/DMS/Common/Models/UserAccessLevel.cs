using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;
 

[Table("UserAccessLevels")]
public class UserAccessLevel : AuditableEntity
{
    [Key]
    public int Id { get; set; }


    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;



    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;
      
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string DocumentTypeCode { get; set; }
     
}

public class UserAccessLevelReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!; 

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }

    public string DocumentType { get; set; }
    public string DocumentTypeCode { get; set; }

}


public class UserAccessLevelCreateDto
{ 

    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;
     

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
     
    public string DocumentTypeCode { get; set; }

}


public class UserAccessLevelUpdateDto
{
    public int Id { get; set; }
     
    [MaxLength(20)]
    public string EmployeeCode { get; set; } = null!;
     

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomainCode { get; set; }
    public string? DesignationCode { get; set; } 
    public string DocumentTypeCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

