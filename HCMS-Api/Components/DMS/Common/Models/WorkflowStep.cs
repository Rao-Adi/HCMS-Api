using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("WorkflowSteps")]
public class WorkflowStep : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public Guid? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; }
    public WorkflowPolicy WorkflowPolicy { get; set; } = null!;
}

public class WorkflowStepReadDto : AuditableEntity
{ 
    public Guid Id { get; set; }

    public Guid WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public Guid? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; } 
}

public class WorkflowStepCreateDto
{ 
    public Guid Id { get; set; }

    public Guid WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public Guid? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; } 
}

public class WorkflowStepUpdateDto
{ 
    public Guid Id { get; set; }

    public Guid WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public Guid? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}