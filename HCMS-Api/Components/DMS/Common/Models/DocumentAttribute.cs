using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentAttributes")]
public class DocumentAttribute : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    // ⚠️ Matches DB column:
    // DocumentTypeCode INT NOT NULL REFERENCES DocumentTypes(Id)
    public string DocumentTypeCode { get; set; }

    [MaxLength(100)]
    public string ControlLabel { get; set; } = null!;

    public int ControlType { get; set; }

    [MaxLength(1000)]
    public string? ListValues { get; set; }

    public bool IsMandatory { get; set; }
     
    // Navigation Properties
    public DocumentType DocumentType { get; set; } = null!;
    public ICollection<AttributeMandatoryScope> MandatoryScopes { get; set; }
        = new List<AttributeMandatoryScope>();
}
