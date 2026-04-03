using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection.Metadata;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Documents")]
public class Document : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
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
    public int CompanyId { get; set; }
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

    public string DocumentNumber { get; set; } = null!;

    public string DocumentTypeCode { get; set; }

    public int? DocumentId { get; set; }

    public int? RequestId { get; set; }

    [MaxLength(500)]
    public string? Title { get; set; } = null!;

    public string? Version { get; set; }
    public string? Content { get; set; }

    public string? DivisionCode { get; set; } 
    public string? DepartmentCode { get; set; } 
    public string? SubDepartmentCode { get; set; } 
    public string? BusinessDomainCode { get; set; }
     
    public DateTime NextReviewDate { get; set; }
    public IFormFile? DocumentFile { get; set; }
}


public class DocumentUpdateDto : AuditableEntity
{
    public int Id { get; set; } 

    [MaxLength(50)]
    public string DocumentNumber { get; set; } = null!;
    public string DocumentTypeCode { get; set; }
     
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

public class SubmitDocument
{ 
    public int DocumentId { get; set; } 

    public List<CreateDocumentAttributeValueDto> Attributes { get; set; } = new();
}

public class CreateDocumentAttributeValueDto
{  

    // DocumentAttributeId INT NOT NULL
    public int DocumentAttributeId { get; set; }

    // ValueText TEXT
    public string? ValueText { get; set; }

    // ValueNumber NUMERIC
    public decimal? ValueNumber { get; set; }

    // ValueDate DATE
    public DateTime? ValueDate { get; set; }

    // ValueBoolean BOOLEAN
    public bool? ValueBoolean { get; set; }

   
}


public class ActionOnDocument
{ 
    public int DocumentId { get; set; } 
    public int ExecutionId { get; set; }
    public string Observation { get; set; }
    public string EmployeeCode { get; set; }
}

public  class AllDocumentDto
{
    // Primary Identifiers
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Company { get; set; }

    // Document Details
    public string Title { get; set; }
    public string DocumentNumber { get; set; }
    public string DocumentType { get; set; }
    public string DocumentTypeCode { get; set; }
    public string VersionContent { get; set; }
    public string ProposedVersionNumber { get; set; }
    public string DraftFileURL { get; set; }

    // Organizational Hierarchy
    public string Division { get; set; }
    public string DivisionCode { get; set; }
    public string Department { get; set; }
    public string DepartmentCode { get; set; }
    public string SubDepartment { get; set; }
    public string SubDepartmentCode { get; set; }
    public string BusinessDomain { get; set; }
    public string BusinessDomainCode { get; set; }

    // Audit and Workflow State
    public string? CreatedAt { get; set; } // Maps to TIMESTAMP
    public string CreatedBy { get; set; }
    public string RequestCreatedBy { get; set; }
    public string RequestCreatedAt { get; set; }
    public int ExecutionId { get; set; }
    public int StepId { get; set; }
    public int StepOrder { get; set; }
    public string StepType { get; set; }
    public string ExecutionStatus { get; set; }
    public string? StartedAt { get; set; } // Maps to TIMESTAMP

    // 🟩 UC-22
    public List<DistributionListReadDto>? DistributionList { get; set; }
    public List<DocumentRequestUserDistribution>? UserList { get; set; }
}

public class GetDocumentDto : TableFiltersDto
{  
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; } 

    public string? RequestStatus { get; set; }
}


public class CompleteDocumentTrainingDto
{ 
    public int DocumentId { get; set; }
    public long UserId { get; set; }

    public string? TrainingProofUrl { get; set; }
    public decimal? AssessmentScore { get; set; }
}

public class GetApprovedRequestForDocumentCreationDto
{ 
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string DocumentTypeCode { get; set; }
}
