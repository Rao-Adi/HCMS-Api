using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Templates")]
public class Template : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileUrl { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public bool IsDefault { get; set; }

}

public class TemplateReadDto : AuditableEntity
{ 
    public int Id { get; set; }


    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileUrl { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? TemplateContent { get; set; }

    public bool IsDefault { get; set; }

}


public class TemplateCreateDto
{

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileUrl { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? TemplateContent { get; set; }

    public bool IsDefault { get; set; }

}


public class TemplateUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string DocumentTypeCode { get; set; }

    [MaxLength(200)]
    public string TemplateName { get; set; } = null!;

    [MaxLength(500)]
    public string TemplateFileURL { get; set; } = null!;

    public int TemplateType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? TemplateContent { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
