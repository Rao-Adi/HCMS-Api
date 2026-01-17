using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Documents")]
public class Document : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    [MaxLength(50)]
    public string DocumentNumber { get; set; } = null!;

    public string DocumentTypeCode { get; set; } = null!;
    public string DivisionCode { get; set; } = null!;
    public string DepartmentCode { get; set; } = null!;
    public string SubDepartmentCode { get; set; } = null!;

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    public int Status { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime NextReviewDate { get; set; }

    public string DocumentURL { get; set; }


    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
}

public class DocumentReadDto : AuditableEntity
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string DocumentNumber { get; set; } = null!;

    public string DocumentType { get; set; } = null!;
    public string DocumentTypeCode { get; set; } = null!;
    public string Division { get; set; } = null!;
    public string DivisionCode { get; set; } = null!;
    public string Department { get; set; } = null!;
    public string DepartmentCode { get; set; } = null!;
    public string SubDepartment { get; set; } = null!;
    public string SubDepartmentCode { get; set; } = null!;

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    public int Status { get; set; }

    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }
    public string NextReviewDate { get; set; }

    public string DocumentURL { get; set; }
}

public class DocumentCreateDto
{
    public string DocumentNumber { get; set; } = null!;

    public string DocumentTypeCode { get; set; } = null!;
    public string DivisionCode { get; set; } = null!;
    public string DepartmentCode { get; set; } = null!;
    public string SubDepartmentCode { get; set; } = null!;

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    public int Status { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime NextReviewDate { get; set; }
    public IFormFile DocumentFile { get; set; }
}


public class DocumentUpdateDto : AuditableEntity
{
    public int Id { get; set; }

    [MaxLength(50)]
    public string DocumentNumber { get; set; } = null!;

    public string DocumentTypeCode { get; set; } = null!;
    public string DivisionCode { get; set; } = null!;
    public string DepartmentCode { get; set; } = null!;
    public string SubDepartmentCode { get; set; } = null!;

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    public int Status { get; set; }

    public string? EffectiveFrom { get; set; }
    public string? EffectiveTo { get; set; }
    public string? NextReviewDate { get; set; }

    public IFormFile DocumentFile { get; set; }
}

