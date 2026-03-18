using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TransferWorkflowPolicies")]
public class TransferWorkflowPolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }

    public int ApprovalRoleId { get; set; }
    public int ApprovalUserId { get; set; }

}

public class TransferWorkflowPolicyReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }

    public int ApprovalRoleId { get; set; }
    public int ApprovalUserId { get; set; }

}

public class TransferWorkflowPolicyCreateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }

    public int ApprovalRoleId { get; set; }
    public int ApprovalUserId { get; set; }

}

public class TransferWorkflowPolicyUpdateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;  
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }

    public int ApprovalRoleId { get; set; }
    public int ApprovalUserId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
