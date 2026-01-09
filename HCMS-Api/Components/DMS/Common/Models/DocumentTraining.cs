using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentTraining")]
public class DocumentTraining : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public int TrainingMode { get; set; }
    public int TrainingStatus { get; set; }

    [MaxLength(500)]
    public string? TrainingProofURL { get; set; }

    public decimal? AssessmentScore { get; set; }

    public int ValidationStatus { get; set; }
    public bool ReadyForAuthorization { get; set; }

}


public class DocumentTrainingReadDto : AuditableEntity
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public int TrainingMode { get; set; }
    public int TrainingStatus { get; set; }

    [MaxLength(500)]
    public string? TrainingProofURL { get; set; }

    public decimal? AssessmentScore { get; set; }

    public int ValidationStatus { get; set; }
    public bool ReadyForAuthorization { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}


public class DocumentTrainingCreateDto
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public int TrainingMode { get; set; }
    public int TrainingStatus { get; set; }

    [MaxLength(500)]
    public string? TrainingProofURL { get; set; }

    public decimal? AssessmentScore { get; set; }

    public int ValidationStatus { get; set; }
    public bool ReadyForAuthorization { get; set; }

}


public class DocumentTrainingUpdateDto
{
    public int Id { get; set; }

    public int DocumentId { get; set; }

    public int TrainingMode { get; set; }
    public int TrainingStatus { get; set; }

    [MaxLength(500)]
    public string? TrainingProofURL { get; set; }

    public decimal? AssessmentScore { get; set; }

    public int ValidationStatus { get; set; }
    public bool ReadyForAuthorization { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}