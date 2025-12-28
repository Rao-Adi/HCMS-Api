using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Templates")]
public class Template
{
    [Key]
    public Guid Id { get; set; }

    public int DocumentTypeId { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileUrl { get; set; } = null!;

    public int TemplateType { get; set; }

    public int? DivisionId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SubDepartmentId { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; }
}
