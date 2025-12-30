using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TransferWorkflowPolicies")]
public class TransferWorkflowPolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ApprovalRoleId { get; set; }
    public Guid ApprovalUserId { get; set; }

}

public class TransferWorkflowPolicyReadDto : AuditableEntity
{
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ApprovalRoleId { get; set; }
    public Guid ApprovalUserId { get; set; }

}

public class TransferWorkflowPolicyCreateDto
{
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ApprovalRoleId { get; set; }
    public Guid ApprovalUserId { get; set; }

}

public class TransferWorkflowPolicyUpdateDto
{
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ApprovalRoleId { get; set; }
    public Guid ApprovalUserId { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
