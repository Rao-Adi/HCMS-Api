using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TrainingPolicies")]
public class TrainingPolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int DocumentTypeId { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }
     
}

public class TrainingPolicyReadDto : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string DocumentType { get; set; }
    public string DocumentTypeCode { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }

}

public class TrainingPolicyCreateDto
{ 
    public string DocumentTypeCode { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }

}

public class TrainingPolicyUpdateDto
{ 
    public int Id { get; set; }
     

    public string DocumentTypeCode { get; set; }
    public bool TrainingRequired { get; set; }
    public int? MinimumScore { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

