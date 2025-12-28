using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("WorkflowSteps")]
public class WorkflowStep
{
    [Key]
    public Guid Id { get; set; }

    public Guid WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public Guid? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; }

    public WorkflowPolicy WorkflowPolicy { get; set; } = null!;
}
