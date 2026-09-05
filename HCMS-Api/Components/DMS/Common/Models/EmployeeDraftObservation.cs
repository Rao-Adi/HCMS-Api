using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("EmployeeDraftObservation")]
public class EmployeeDraftObservation
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string EmployeeCode { get; set; } = null!;

    [Required]
    public int CompanyId { get; set; }

    [Required]
    public string ObservationText { get; set; } = null!;

    // Exactly one of these is populated per row, matching which flow the draft belongs to --
    // a draft saved while reviewing a Document Request must not resurface when the same
    // employee later opens an unrelated Document (or a different Request), which is what
    // happened when this table only scoped by (EmployeeCode, CompanyId).
    public int? DocumentId { get; set; }
    public int? RequestId { get; set; }
}

public class EmployeeDraftObservationDto
{
    [Required]
    public string ObservationText { get; set; } = null!;

    // "Document" or "Request" -- matches the entityType values WorkflowObservationDialogComponent
    // already carries (modalData.entityType), so the frontend has no new concept to introduce.
    [Required]
    public string EntityType { get; set; } = null!;

    [Required]
    public int EntityId { get; set; }
}
