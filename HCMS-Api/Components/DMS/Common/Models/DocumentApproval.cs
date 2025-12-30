using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentApprovals")]
public class DocumentApproval : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid DocumentVersionId { get; set; }
    public Guid WorkflowStepId { get; set; }
    public Guid ApproverUserId { get; set; }

    public int Status { get; set; }

    [MaxLength(1000)]
    public string Observation { get; set; } = null!;

    public DateTime? ActionDate { get; set; }

}


public class DocumentApprovalReadDto : AuditableEntity
{ 
    public Guid Id { get; set; }

    public Guid DocumentVersionId { get; set; }
    public Guid WorkflowStepId { get; set; }
    public Guid ApproverUserId { get; set; }

    public int Status { get; set; }
     
    public string Observation { get; set; } = null!;

    public string? ActionDate { get; set; } 
}

public class DocumentApprovalCreateDto
{ 
    public Guid Id { get; set; }

    public Guid DocumentVersionId { get; set; }
    public Guid WorkflowStepId { get; set; }
    public Guid ApproverUserId { get; set; }

    public int Status { get; set; }
     
    public string Observation { get; set; } = null!;

    public string? ActionDate { get; set; } 
}

public class DocumentApprovalUpdateDto
{ 
    public Guid Id { get; set; }

    public Guid DocumentVersionId { get; set; }
    public Guid WorkflowStepId { get; set; }
    public Guid ApproverUserId { get; set; }

    public int Status { get; set; }
     
    public string Observation { get; set; } = null!;

    public string? ActionDate { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}