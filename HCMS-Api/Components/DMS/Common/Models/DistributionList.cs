using HCMS_Api.Components.DMS.Common.Models.Departments;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DistributionLists")]
public class DistributionList : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string DocumentRequestTypeCode { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public int RoleId { get; set; }

    public int DistributionType { get; set; }

    // Navigation Properties
    public DocumentRequest DocumentRequest { get; set; } = null!;
    public DocumentRequestType DocumentRequestType { get; set; } = null!;
    public Division Division { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public Role Role { get; set; } = null!;
}


public class DistributionListReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string DocumentRequestType { get; set; }
    public string DocumentRequestTypeCode { get; set; }

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }
    public int RoleId { get; set; }
    public string Role { get; set; }

    public string Distribution { get; set; }
    public int DistributionType { get; set; }

}


public class DistributionListCreateDto
{ 
    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string DocumentRequestTypeCode { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public int RoleId { get; set; }

    public int DistributionType { get; set; }
}


public class DistributionListUpdateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string DocumentRequestTypeCode { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public int RoleId { get; set; }

    public int DistributionType { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}

