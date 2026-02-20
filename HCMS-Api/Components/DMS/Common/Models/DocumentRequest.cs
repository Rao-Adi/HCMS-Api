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

    

    public int Status { get; set; }
    public string RowVersion { get; set; }
    public int StepId { get; set; }
    public int StepOrder { get; set; }
    public string StartedAt { get; set; }
     
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

public class GetPendingRequestDto
{
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; }
    public string? EmployeeCode { get; set; }

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

public class MyRequestFilterDto
{
    public int CompanyId { get; set; }
    public string Initiator { get; set; }  // logged in username
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public int? Status { get; set; }       // Pending / Approved / Rejected
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
    public int Id { get; set; }
    public string RequestNumber { get; set; }
    public string DocumentName { get; set; }
    public string Justification { get; set; }
    public string ProposedContent { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string SubmittedBy { get; set; }
    public int Status { get; set; }


    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }

    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }

    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }


    public string? WorkflowStatus { get; set; }
    public DateTime? WorkflowStartedAt { get; set; }
    public DateTime? WorkflowCompletedAt { get; set; }

    public List<WorkflowStepHistoryDto> Steps { get; set; } = new();
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

public class DraftDocumentRequestDto
{
    public long CompanyId { get; set; }
    public string DocumentRequestTypeCode { get; set; }
    public string DocumentTypeCode { get; set; }

    public string DocumentName { get; set; }
    public string Justification { get; set; }
    public string ProposedContent { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }

    public long CreatedByUserId { get; set; }
}

public class UpdateDraftRequestDto
{
    public long CompanyId { get; set; }
    public long RequestId { get; set; }

    public string DocumentName { get; set; }
    public string Justification { get; set; }
    public string ProposedContent { get; set; }

    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public string SubDepartmentCode { get; set; }
    public string BusinessDomainCode { get; set; }

    public long ModifiedByUserId { get; set; }

    public long RowVersion { get; set; }
}


public class SubmitDocumentRequestDto
{
    public int CompanyId { get; set; }
    public int RequestId { get; set; }
    public int SubmittedBy { get; set; }
}


 

public class ApproveRejectWorkflowStepDto
{
    public int CompanyId { get; set; }
    public int StepId { get; set; }
    public int UserId { get; set; }

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
