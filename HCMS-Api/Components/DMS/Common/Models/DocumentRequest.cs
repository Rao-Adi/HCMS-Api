﻿using HCMS_Api.Common.Misc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

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
 
    public int DocumentTypeId { get; set; }
     
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

    public string DocumentRequestTypeCode { get; set; } // 1=Creation, 2=Revision, 3=Obsoletion

    public int? DocumentId { get; set; }
     
    public string DocumentType { get; set; }
    public string DocumentTypeCode { get; set; }

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    public string Justification { get; set; } = null!;
    public string ProposedContent { get; set; } = null!;
    public string DraftFileUrl { get; set; } = null!;
    public bool IsContentFinalized { get; set; }
    public string DraftContentLastModifiedAt { get; set; } = null!;
    public string DraftContentLastModifiedBy { get; set; } = null!;


    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DraftFileURL { get; set; }
    public int? ParentDocumentId { get; set; }
    public int? ParentRequestId { get; set; }

    // Populated only for Revision requests: when/by whom the document version being revised was created
    public string? PreviousVersionCreatedOn { get; set; }
    public string? PreviousVersionCreatedBy { get; set; }

    public bool IsReworked { get; set; }

    public string? CreatedByName { get; set; }
    public string? LastModifiedByName { get; set; }

    public int Status { get; set; }
    public string RowVersion { get; set; }
    public int StepId { get; set; }
    public int StepOrder { get; set; }
    public string StartedAt { get; set; }
    public string ExecutionStatus { get; set; }


    // 🟩 UC-22
    public List<DistributionListReadDto>? DistributionList { get; set; }
    public List<DocumentRequestUserDistribution>? UserList { get; set; }

}

public class DocumentRequestUserDistribution
{
    // Id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY
    public int Id { get; set; }

    // CompanyId INT NOT NULL
    public int CompanyId { get; set; }

    // DocumentRequestId INT NOT NULL
    public int DocumentRequestId { get; set; }

    // UserId BIGINT NOT NULL
    public string EmployeeCode { get; set; }
    public string EmployeeName { get; set; }
    public string Designation { get; set; }
    public string Role { get; set; }

    // Which Role + Cabinet grid row (DRUsersComponent) this employee was selected under --
    // null for employees auto-expanded from the separate Distribution List (Role-only) section,
    // which has its own DocumentRequestRoleDistributions table and doesn't need this grouping.
    public int? RoleId { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }

    // IsActive BOOLEAN NOT NULL DEFAULT TRUE
    public bool IsActive { get; set; } = true;

    // IsDeleted BOOLEAN NOT NULL DEFAULT FALSE
    public bool IsDeleted { get; set; } = false;

    // CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    public string? CreatedAt { get; set; }  

    // CreatedBy BIGINT NOT NULL
    public string CreatedBy { get; set; }

    // LastModifiedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    public string? LastModifiedAt { get; set; }

    // LastModifiedBy BIGINT NOT NULL
    public string LastModifiedBy { get; set; }
}

public class DocumentRoleDistribution
{
    // Id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY 
    public int Id { get; set; }

    // CompanyId INT NOT NULL
    public int CompanyId { get; set; }

    // DocumentId INT NOT NULL
    public int DocumentId { get; set; }

    // DivisionCode VARCHAR(10) (Nullable if SQL doesn't say NOT NULL)
    [MaxLength(10)]
    public string? DivisionCode { get; set; }

    // DepartmentCode VARCHAR(20)
    [MaxLength(20)]
    public string? DepartmentCode { get; set; }

    // SubDepartmentCode VARCHAR(30)
    [MaxLength(30)]
    public string? SubDepartmentCode { get; set; }

    // BusinessDomainCode VARCHAR(30)
    [MaxLength(30)]
    public string? BusinessDomainCode { get; set; }

    // RoleId INT NOT NULL
    public int RoleId { get; set; }

    // DistributionType INT NOT NULL
    public int DistributionType { get; set; }

    // CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    public string CreatedAt { get; set; }

    // CreatedBy BIGINT NOT NULL
    public long CreatedBy { get; set; }
}

public class DocumentRequestCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 


    [MaxLength(50)]
    public string RequestNumber { get; set; } = null!;

    public string RequestType { get; set; } // 1=Creation, 2=Revision, 3=Obsoletion

    public int? DocumentId { get; set; }

    [MaxLength(10)]
    public int DocumentTypeId { get; set; }  
     
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
    public int DocumentTypeId { get; set; }  
     
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

public class DocumentRequestCreate
{
    public int DocumentTypeId { get; set; }
    public string RequestType { get; set; }
    public string Title { get; set; }
    public string Justification { get; set; }
}

public class GetPendingRequestDto : TableFiltersDto
{
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; } 

    public string? RequestStatus { get; set; }
}

public class DraftRequestReadDto
{
    public long Id { get; set; }
    public string RequestNumber { get; set; }
    public string DocumentName { get; set; }
    public string DocumentTypeCode { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MyRequestFilterDto : TableFiltersDto
{ 
    public string? Initiator { get; set; }  // logged in username
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public int? Status { get; set; }       // Pending / Approved / Rejected
    public int? CompanyId { get; set; } // this will be filled via generic way
}

public class MyRequestPendingDto  
{ 
    // Request Details
    [JsonPropertyName("requestid")]
    public int RequestId { get; set; }

    [JsonPropertyName("requestnumber")]
    public string RequestNumber { get; set; }

    [JsonPropertyName("documentname")]
    public string DocumentName { get; set; }

    [JsonPropertyName("justification")]
    public string Justification { get; set; }

    [JsonPropertyName("proposedcontent")]
    public string ProposedContent { get; set; }


    [JsonPropertyName("draftfileurl")]
    public string DraftFileUrl { get; set; }


    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("submittedat")]
    public DateTime? SubmittedAt { get; set; }

    // Organizational Details
    [JsonPropertyName("divisioncode")]
    public string DivisionCode { get; set; }

    [JsonPropertyName("division")]
    public string Division { get; set; }

    [JsonPropertyName("departmentcode")]
    public string DepartmentCode { get; set; }

    [JsonPropertyName("department")]
    public string Department { get; set; }

    [JsonPropertyName("subdepartmentcode")]
    public string SubDepartmentCode { get; set; }

    [JsonPropertyName("subdepartment")]
    public string SubDepartment { get; set; }

    [JsonPropertyName("documenttypecode")]
    public string DocumentTypeCode { get; set; }

    [JsonPropertyName("documenttype")]
    public string DocumentType { get; set; }

    // Workflow Progress Details - ALL LOWERCASE to match your pattern
    [JsonPropertyName("currentsteporder")]
    public int CurrentStepOrder { get; set; }

    [JsonPropertyName("currentsteptype")]        // Changed to lowercase
    public string CurrentStepType { get; set; }

    [JsonPropertyName("currentassigneduser")]     // Changed to lowercase
    public string CurrentAssignedUser { get; set; }

    [JsonPropertyName("currentassigneduserid")]   // Changed to lowercase
    public int? CurrentAssignedUserId { get; set; }

    [JsonPropertyName("currentassignedroleid")]   // Changed to lowercase
    public int? CurrentAssignedRoleId { get; set; }

    [JsonPropertyName("createdby")]
    public string CreatedBy { get; set; }

    [JsonPropertyName("createdat")]
    public string CreatedAt { get; set; }

}


public class WorkflowStepHistoryDto
{
    public int StepOrder { get; set; }
    public string StepType { get; set; }
    public int? AssignedUserId { get; set; }
    public int? AssignedRoleId { get; set; }
    public string? Decision { get; set; }
    public string Observation { get; set; }
    public DateTime? ActionAt { get; set; }
    public bool IsActive { get; set; }

    public string UserName { get; set; }
}

public class DocumentRequestDetailsDto
{
    // we.EntityId, we.EntityType
    public int EntityId { get; set; }
    public string EntityType { get; set; }

    // wes.StepOrder, wsd.StepType
    public int StepOrder { get; set; }
    public string StepType { get; set; }

    // wes.AssignedUserId, u.EmployeeName, u.EmployeeCode
    public int? AssignedUserId { get; set; } // Nullable if a step can be unassigned
    public string EmployeeName { get; set; }
    public string EmployeeCode { get; set; }
    public string Division { get; set; }
    public string Department { get; set; }
    public string SubDepartment { get; set; }
    public string BusinessDomain { get; set; }
    public int? DesignationId { get; set; }
    public string Designation { get; set; }

    public int? RoleId { get; set; }
    // r.Name AS RoleName
    public string RoleName { get; set; }

    // wes.Decision, wes.Observation
    public string Decision { get; set; }
    public string Observation { get; set; }

    // wes.ActionAt
    public string? ActionAt { get; set; } // Nullable if the action hasn't happened yet

    public string ReceivedOn { get; set; }
    public string StatusUpdatedOn { get; set; }
    // wes.IsActive
    public bool IsActive { get; set; }
    public string CreatedByName { get; set; }
    public string LastModifiedByName { get; set; }

}

// One row per Document in a revision chain (original + every subsequent revision),
// returned in chronological order by GetDocumentRevisionHistoryAsync.
public class RevisionHistoryItemDto
{
    public int DocumentId { get; set; }
    public string DocumentNumber { get; set; }
    public string DocumentName { get; set; }
    public int? ParentDocumentId { get; set; }
    public string Version { get; set; }

    public int? RequestId { get; set; }
    public string RequestNumber { get; set; }
    public string DocumentRequestTypeCode { get; set; }
    public string Justification { get; set; }

    public DateTime? RequestedOn { get; set; }
    public string RequestedBy { get; set; }

    public DateTime? ApprovedOn { get; set; }
    public string ApprovedBy { get; set; }

    public DateTime? EffectiveOn { get; set; }
    public string EffectiveBy { get; set; }

    public string CurrentStatus { get; set; }

    // True for the newest document in the chain (the one with no revision made of it yet).
    public bool IsCurrentVersion { get; set; }
}



/// <summary>
// Very important class.
//public static class RequestStatus
//{
//    public const int Draft = 0;
//    public const int Submitted = 1;
//    public const int InApproval = 2;
//    public const int Approved = 3;
//    public const int Rejected = 4;
//}



///////////////////////////
///

// One employee selected in the "Document Users" grid (DRUsersComponent), tagged with the
// Role + Cabinet row it was selected under -- carried through to DocumentRequestUserDistributions
// so that row grouping can be reconstructed when the draft is reopened (see
// DocumentRequestUserDistribution.RoleId/*Code).
public class UserDistributionInputDto
{
    public string EmployeeCode { get; set; } = null!;
    public int? RoleId { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
}

public class DraftDocumentRequestDto
{
    public string DocumentRequestTypeCode { get; set; }
    public string DocumentTypeCode { get; set; }

    public string DocumentName { get; set; }
    public string Justification { get; set; }
    public string? ProposedContent { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public int? ParentDocumentId { get; set; }


    // 🟩 UC-22
    public List<DistributionListCreateDto>? DistributionList { get; set; }
    public List<UserDistributionInputDto>? UserIds { get; set; }

    public IFormFile? DraftFile { get; set; }
}


public class UpdateDraftRequestDto
{
    public int RequestId { get; set; }

    public string DocumentName { get; set; }
    public string Justification { get; set; }
    public string? ProposedContent { get; set; }


    // UC-22 User Modification Allowed
    public List<DistributionListCreateDto>? DistributionList { get; set; }
    public List<UserDistributionInputDto>? UserIds { get; set; }

    public IFormFile? DraftFile { get; set; }
}

public class EffectiveDocumentDetailsDto : AuditableEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Company { get; set; }
    public int DocumentId { get; set; }
    public string DocumentNumber { get; set; }
    public int RequestId { get; set; } 
    public int ParentDocumentId { get; set; } 
    public string DocumentName { get; set; }
    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string DocumentType { get; set; }
    public string DocumentTypeCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? NextReviewDate { get; set; } 
    public string? DocumentURL { get; set; } 
    public string? VersionContent { get; set; }
    public string? Version { get; set; }
    public string? VersionType { get; set; }
    public string? ChangeDescription { get; set; } 
    public string? CreatedByName { get; set; } 
    public string? LastModifiedByName { get; set; } 
     

    // Lists to populate on the FrontEnd
    public List<DistributionListReadDto> DistributionList { get; set; } = new List<DistributionListReadDto>();
    public List<DocumentRequestUserDistribution> UserList { get; set; } = new List<DocumentRequestUserDistribution>();
}

public class SubmitDocumentRequestDto
{
    public int RequestId { get; set; }
    public string DocumentRequestType { get; set; }

    // Allows the Template (file) or HTML content to be updated at Submit time, without
    // requiring a separate UpdateDraftDocumentRequestAsync call beforehand. Optional --
    // leave both blank to submit the draft's existing content/file unchanged.
    public string? ProposedContent { get; set; }
    public IFormFile? DraftFile { get; set; }

    // UC-22 User Modification Allowed
    public List<DistributionListCreateDto>? DistributionList { get; set; }
    public List<UserDistributionInputDto>? UserIds { get; set; }
}

public class SubmitRevisionDocumentRequestDto
{
    public int RequestId { get; set; }

    // UC-22 User Modification Allowed
    public List<DistributionListCreateDto>? DistributionList { get; set; }
    public List<UserDistributionInputDto>? UserIds { get; set; }
}


 

public class ApproveRejectWorkflowStepDto
{
    public int EmpId { get; set; }

    public int StepId { get; set; } 

    public string Action { get; set; }
    // Approve
    // Reject
    // Comment
    public string Observation { get; set; }
}

public class CreateDocumentRequestDto
{
    public long CompanyId { get; set; }
    public int RequestType { get; set; }
    public long DocumentTypeId { get; set; }

    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public string SubDepartmentCode { get; set; }
    public string BusinessDomainCode { get; set; }

    public string DocumentName { get; set; }
    public string Justification { get; set; }

    public string Content { get; set; }
}

public class ApprovalDto
{
    public long WorkflowExecutionId { get; set; }
    public string Observation { get; set; }
}


//////// 07-02-226

public class DocumentRequests
{
    public long Id { get; set; }
    public string RequestNumber { get; set; }
    public string RequestType { get; set; }
    public string Title { get; set; }
    public int DocumentTypeId { get; set; }

    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public string SubDepartmentCode { get; set; }

    public string ReasonForRequest { get; set; }
    public string BusinessJustification { get; set; }

    public short Priority { get; set; }

    public int StatusId { get; set; }
    public long? WorkflowInstanceId { get; set; }

    public string RequestedBy { get; set; }
}

public class DocumentRequestAttachments
{
    public long RequestId { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string UploadedBy { get; set; }
}
