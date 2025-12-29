using System.ComponentModel.DataAnnotations;

namespace HCMS_Api.Components.DMS.Common.Models;

public class TransferWorkflowPolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ApprovalRoleId { get; set; }
    public Guid ApprovalUserId { get; set; }

}
