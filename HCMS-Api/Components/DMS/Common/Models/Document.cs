using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common.Models.Departments;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    public string? DocumentName { get; set; } = null!;

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

    // SubmitDocument (this class) is only ever bound via [FromForm] on the multipart
    // "submit-document" endpoint (required since DocumentFile can only travel that way) -- the
    // controller reads these from JSON-encoded "attributes"/"trainingusers"/etc. form fields and
    // populates these lists itself, since ASP.NET Core's form binder can't bind a List<T> from a
    // single JSON string field. [BindNever] on all of them (not just the ones that happen to
    // trigger it) is deliberate: without it, ASP.NET Core's default collection binder still
    // tries to bind each property from indexed form keys (e.g. "UserIds[0].EmployeeCode") that
    // don't exist here at all, and for a DTO whose properties are all nullable/reference-typed
    // (UserDistributionInputDto), that binder considers every index "successfully bound" (as an
    // all-null object) and keeps probing indices until it hits ASP.NET Core's own safety cap
    // (MvcOptions.MaxModelBindingCollectionSize, 1024), throwing InvalidOperationException --
    // confirmed live on UserIds. [BindNever] skips the automatic binder for these properties
    // entirely, leaving them for the manual JSON deserialization further down to populate.
    [BindNever]
    public List<CreateDocumentAttributeValueDto> Attributes { get; set; } = new();
    [BindNever]
    public List<TraningUsers>? TrainingUsers { get; set; }

    // Template attachment for when it wasn't provided at Document Request creation time.
    // Only one of these is expected, depending on the DocumentType's configured Template:
    // DocumentFile for a file-based template (PDF/Word), ProposedContent for an HTML template.
    public IFormFile? DocumentFile { get; set; }
    public string? ProposedContent { get; set; }

    // Only populated (and only used) when DocumentId is omitted/0 -- i.e. the "Create Document
    // Directly" path that skips the Request/approval stage entirely. When DocumentId is a real
    // existing id (today's request-driven flow), these are ignored -- that path already went
    // through CreateDocumentFromApprovedRequestAsync's own Justification/Distribution
    // promotion (DocumentRequests.Justification -> Documents.Justification,
    // DocumentRequestRoleDistributions/DocumentRequestUserDistributions ->
    // DocumentRoleDistributions/DocumentUserDistributions) when the Request was approved.
    public string? DocumentTypeCode { get; set; }
    public string? DocumentName { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? Justification { get; set; }
    [BindNever]
    public List<DistributionListCreateDto>? DistributionList { get; set; }
    [BindNever]
    public List<UserDistributionInputDto>? UserIds { get; set; }

    // This-document-only approver(s), appended after the policy-resolved workflow steps.
    // Never persisted to WorkflowPolicies/WorkflowStepDefinitions -- see
    // DocumentComponent.EnsureAdHocApproverStepDefinitionAsync.
    [BindNever]
    public List<AdHocApproverDto>? AdHocApprovers { get; set; }
}
public class AdHocApproverDto
{
    public string EmployeeCode { get; set; } = null!;
}
public class TraningUsers
{
    public int TrainingMode { get; set; }
    public string EmployeeCode { get; set; }
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
    public int EmpId { get; set; } 
    public int DocumentId { get; set; } 
    public int ExecutionId { get; set; }
    public string Observation { get; set; } 
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
    public string? DocumentJustification { get; set; }
    public string? RequestJustification { get; set; }

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

    // Populated only for Revision requests: when/by whom the document version being revised was created
    public string? PreviousVersionCreatedOn { get; set; }
    public string? PreviousVersionCreatedBy { get; set; }


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
    public int EmpId { get; set; }
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
