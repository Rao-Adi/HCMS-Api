using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("WorkflowPolicies")]
public class WorkflowPolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;


    public int PolicyType { get; set; }

    public string? EntityType { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }

    [MaxLength(10)]
    public string? DocumentTypeCode { get; set; }

    public int? SharingType { get; set; }

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();

}

public class WorkflowPolicyReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;
      
    public string Name { get; set; }

    public string? EntityType { get; set; }

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }

    public string? DocumentType { get; set; }
    public string? DocumentTypeCode { get; set; }

    public List<WorkflowStepCreateDto> Steps { get; set; }

}

public class WorkflowPolicyCreateDto
{ 

    // 🔑 Tenant
    public int CompanyId { get; set; }

    public string Name { get; set; }

    public string? EntityType { get; set; }
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; } 
    public string? DocumentTypeCode { get; set; }

}

public class WorkflowPolicyUpdateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }

    public string Name { get; set; }

    public string? EntityType { get; set; }

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }

    public string? DocumentType { get; set; }
    public string? DocumentTypeCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}
