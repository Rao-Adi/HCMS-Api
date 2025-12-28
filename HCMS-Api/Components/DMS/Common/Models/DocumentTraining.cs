using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentTraining")]
public class DocumentTraining : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int TrainingMode { get; set; }
    public int TrainingStatus { get; set; }

    [MaxLength(500)]
    public string? TrainingProofURL { get; set; }

    public decimal? AssessmentScore { get; set; }

    public int ValidationStatus { get; set; }
    public bool ReadyForAuthorization { get; set; }

   
}
