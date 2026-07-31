using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TransferWorkflowPolicies")]
public class TransferWorkflowPolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
     
    public string? DivisionCode { get; set; }  

    public string ApprovalRoleId { get; set; }
    public string ApprovalUserId { get; set; }
    public string ApproverEmpCode { get; set; }

}

public class TransferWorkflowPolicyReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string? Division { get; set; }
    public string? DivisionCode { get; set; } 

    public string ApprovalRoleId { get; set; }
    public string ApprovalUserId { get; set; }

     
    public string DivisionHeadName { get; set; }
    public string DivisionHeadDesignation { get; set; } 

}

public class TransferWorkflowPolicyCreateDto
{
    public int Id { get; set; } 
     
    public string? DivisionCode { get; set; }  
    public string ApproverEmpCode { get; set; }  
    public string ApprovalRoleId { get; set; }
    public string ApprovalUserId { get; set; }

}

public class TransferWorkflowPolicyUpdateDto
{
    public int Id { get; set; }
      
    public string? DivisionCode { get; set; }  

    public string ApprovalRoleId { get; set; }
    public string ApprovalUserId { get; set; } 

}
