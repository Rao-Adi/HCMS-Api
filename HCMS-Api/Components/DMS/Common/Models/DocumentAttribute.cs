using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentAttributes")]
public class DocumentAttribute : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    // ⚠️ Matches DB column:
    // DocumentTypeCode INT NOT NULL REFERENCES DocumentTypes(Id)
    public string DocumentTypeCode { get; set; }

    [MaxLength(100)]
    public string ControlLabel { get; set; } = null!;

    public int ControlTypeId { get; set; }

    [MaxLength(1000)]
    public string? ListValues { get; set; }

    public bool IsMandatory { get; set; }
     
    // Navigation Properties
    public DocumentType DocumentType { get; set; } = null!;
    public ICollection<AttributeMandatoryScope> MandatoryScopes { get; set; }
        = new List<AttributeMandatoryScope>();
}

public class DocumentAttributeReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;


    // ⚠️ Matches DB column:
    // DocumentTypeCode INT NOT NULL REFERENCES DocumentTypes(Id)
    public string DocumentTypeCode { get; set; }
    public string DocumentType { get; set; }

    [MaxLength(100)]
    public string ControlLabel { get; set; } = null!;

    public string ControlType { get; set; }
    public int ControlTypeId { get; set; }

    [MaxLength(1000)]
    public string? ListValues { get; set; }

    public bool IsMandatory { get; set; }
     
}

public class DocumentAttributeCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string DocumentTypeCode { get; set; }

    [MaxLength(100)]
    public string ControlLabel { get; set; } = null!;

    public int ControlTypeId { get; set; }

    [MaxLength(1000)]
    public string? ListValues { get; set; }

    public bool IsMandatory { get; set; }
     
}

public class DocumentAttributeUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string DocumentTypeCode { get; set; }

    [MaxLength(100)]
    public string ControlLabel { get; set; } = null!;

    public int ControlTypeId { get; set; }

    [MaxLength(1000)]
    public string? ListValues { get; set; }

    public bool IsMandatory { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}

public class DocumentAttributeReadDto2
{
    public int DocumentAttributeId { get; set; }
    public string ControlLabel { get; set; }
    public int ControlTypeId { get; set; }

    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
    public string? ValueDate { get; set; }
    public bool? ValueBoolean { get; set; }
}