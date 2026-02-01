using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentRequests")]
public class DocumentRequest : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;


    [MaxLength(50)]
    public string RequestNumber { get; set; } = null!;

    public int RequestType { get; set; } // 1=Creation, 2=Revision, 3=Obsoletion

    public int? DocumentId { get; set; }

    [MaxLength(10)]
    public string DocumentTypeCode { get; set; } = null!;
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    [MaxLength(2000)]
    public string Justification { get; set; } = null!;

    public int Status { get; set; }
    public int CurrentStep { get; set; }

    public Document? Document { get; set; }
    public ICollection<RequestApproval> Approvals { get; set; } = new List<RequestApproval>();
}

public class DocumentRequestReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;


    [MaxLength(50)]
    public string RequestNumber { get; set; } = null!;

    public int RequestType { get; set; } // 1=Creation, 2=Revision, 3=Obsoletion

    public int? DocumentId { get; set; }

    [MaxLength(10)]
    public string DocumentTypeCode { get; set; } = null!;

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    [MaxLength(2000)]
    public string Justification { get; set; } = null!;

    public int Status { get; set; }
    public int CurrentStep { get; set; }
     
}

public class DocumentRequestCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 


    [MaxLength(50)]
    public string RequestNumber { get; set; } = null!;

    public int RequestType { get; set; } // 1=Creation, 2=Revision, 3=Obsoletion

    public int? DocumentId { get; set; }

    [MaxLength(10)]
    public string DocumentTypeCode { get; set; } = null!;
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    [MaxLength(2000)]
    public string Justification { get; set; } = null!;

    public int Status { get; set; }
    public int CurrentStep { get; set; }
     
}

public class DocumentRequestUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    [MaxLength(50)]
    public string RequestNumber { get; set; } = null!;

    public int RequestType { get; set; } // 1=Creation, 2=Revision, 3=Obsoletion

    public int? DocumentId { get; set; }

    [MaxLength(10)]
    public string DocumentTypeCode { get; set; } = null!;
     
    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; }
     
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    [MaxLength(2000)]
    public string Justification { get; set; } = null!;

    public int Status { get; set; }
    public int CurrentStep { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}
