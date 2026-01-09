using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TrainingPolicies")]
public class TrainingPolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    public int DocumentTypeId { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }
     
}

public class TrainingPolicyReadDto : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    public int DocumentTypeId { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }

}

public class TrainingPolicyCreateDto
{ 
    public int Id { get; set; }

    public int DocumentTypeId { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }

}

public class TrainingPolicyUpdateDto
{ 
    public int Id { get; set; }

    public int DocumentTypeId { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

