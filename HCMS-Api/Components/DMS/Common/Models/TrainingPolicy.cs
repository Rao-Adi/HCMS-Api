using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TrainingPolicies")]
public class TrainingPolicy : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public int DocumentTypeId { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }
     
}

