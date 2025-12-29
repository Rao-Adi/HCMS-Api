using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("ResponsibilityTransfers")]
public class ResponsibilityTransfer : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid EmployeeFromId { get; set; }
    public Guid EmployeeToId { get; set; }

    public int Reason { get; set; }

    public DateTime EffectiveDateFrom { get; set; }
    public DateTime? EffectiveDateTo { get; set; }

    public bool IsPermanent { get; set; }

    [MaxLength(1000)]
    public string Remarks { get; set; } = null!;

    public int Status { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

}
