using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("RequestApprovals")]
public class RequestApproval : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int DocumentRequestId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }

    [MaxLength(1000)]
    public string Observation { get; set; } = null!;

    public DateTime? ActionDate { get; set; }
     
    public DocumentRequest DocumentRequest { get; set; } = null!;
}


public class RequestApprovalReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public int DocumentRequestId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }

    [MaxLength(1000)]
    public string Observation { get; set; } = null!;

    public DateTime? ActionDate { get; set; }
     
}


public class RequestApprovalCreateDto
{

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public int DocumentRequestId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }

    [MaxLength(1000)]
    public string Observation { get; set; } = null!;

    public DateTime? ActionDate { get; set; }

}


public class RequestApprovalUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public int DocumentRequestId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }

    [MaxLength(1000)]
    public string Observation { get; set; } = null!;

    public DateTime? ActionDate { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

