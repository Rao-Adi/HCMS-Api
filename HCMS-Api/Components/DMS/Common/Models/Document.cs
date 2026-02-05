using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Documents")]
public class Document : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;


    [MaxLength(50)]
    public string DocumentNumber { get; set; } = null!;
    public int DocumentTypeId { get; set; }

    public string Title { get; set; } = null!; 
    public string Version { get; set; } = null!;
    public string DocumentURL { get; set; }

    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; }
     
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }
      

    //public DateTime? EffectiveFrom { get; set; }
    //public DateTime? EffectiveTo { get; set; }
    public DateTime NextReviewDate { get; set; }




    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
}

public class DocumentReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;


    [MaxLength(50)]
    public string DocumentNumber { get; set; } = null!;

    public int DocumentTypeId { get; set; }

    [MaxLength(500)]
    public string Title { get; set; } = null!;

    public string Version { get; set; }

    public string DocumentType { get; set; } = null!;
    public string DocumentTypeCode { get; set; } = null!;
    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }


     
    public string NextReviewDate { get; set; }

    public string DocumentURL { get; set; }
}

public class DocumentCreateDto
{
    // 🔑 Tenant
    public Int64 CompanyId { get; set; }

    public string DocumentNumber { get; set; } = null!;

    public int DocumentTypeId { get; set; }
    public int RequestId { get; set; }

    [MaxLength(500)]
    public string Title { get; set; } = null!;

    public string Version { get; set; }

    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }
     
    public DateTime NextReviewDate { get; set; }
    public IFormFile DocumentFile { get; set; }
}


public class DocumentUpdateDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 


    [MaxLength(50)]
    public string DocumentNumber { get; set; } = null!;
    public int DocumentTypeId { get; set; }
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; }
     
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }

    [MaxLength(500)]
    public string Title { get; set; } = null!;

    public string Version { get; set; }
     
    public string? NextReviewDate { get; set; }

    public IFormFile DocumentFile { get; set; }
}

