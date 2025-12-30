using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Templates")]
public class Template : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileURL { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public bool IsDefault { get; set; }

}

public class TemplateReadDto : AuditableEntity
{ 
    public Guid Id { get; set; }

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileURL { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public bool IsDefault { get; set; }

}


public class TemplateCreateDto
{ 
    public Guid Id { get; set; }

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileURL { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public bool IsDefault { get; set; }

}


public class TemplateUpdateDto
{ 
    public Guid Id { get; set; }

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileURL { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
