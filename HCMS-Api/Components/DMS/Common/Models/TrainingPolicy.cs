using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TrainingPolicies")]
public class TrainingPolicy
{
    [Key]
    public Guid Id { get; set; }

    public int DocumentTypeId { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; }
}

