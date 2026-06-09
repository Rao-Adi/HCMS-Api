using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentApprovals")]
public class DocumentApproval : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int DocumentVersionId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }

    [MaxLength(1000)]
    public string Observation { get; set; } = null!;

    public DateTime? ActionDate { get; set; }

}


public class DocumentApprovalReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public int DocumentVersionId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }
     
    public string Observation { get; set; } = null!;

    public string? ActionDate { get; set; } 
}

public class DocumentApprovalCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public int DocumentVersionId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }
     
    public string Observation { get; set; } = null!;

    public string? ActionDate { get; set; } 
}

public class DocumentApprovalUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public int DocumentVersionId { get; set; }
    public int WorkflowStepId { get; set; }
    public int ApproverUserId { get; set; }

    public int Status { get; set; }
     
    public string Observation { get; set; } = null!;

    public string? ActionDate { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}