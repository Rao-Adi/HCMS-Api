using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("RequestApprovals")]
public class RequestApproval
{
    [Key]
    public Guid Id { get; set; }

    public Guid DocumentRequestId { get; set; }
    public Guid WorkflowStepId { get; set; }
    public Guid ApproverUserId { get; set; }

    public int Status { get; set; }

    [MaxLength(1000)]
    public string Observation { get; set; } = null!;

    public DateTime? ActionDate { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; }

    public DocumentRequest DocumentRequest { get; set; } = null!;
}

