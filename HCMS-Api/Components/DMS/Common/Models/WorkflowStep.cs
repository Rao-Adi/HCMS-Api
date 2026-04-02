using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("WorkflowSteps")]
public class WorkflowStep : AuditableEntity
{
    [Key]
    public int Id { get; set; }


    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int WorkflowPolicyId { get; set; }
    public string DocumentTypeCode { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public int? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; }
    public WorkflowPolicy WorkflowPolicy { get; set; } = null!;
}

public class WorkflowStepReadDto : AuditableEntity
{ 
    public int Id { get; set; }


    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;
    public int WorkflowPolicyVersionId { get; set; }

    public string DocumentType { get; set; }
    public string DocumentTypeCode { get; set; }

    public int StepOrder { get; set; }
     
    public int? UserId { get; set; }

    public int? ApprovalLevel { get; set; }

    public string EmployeeCode { get; set; } = null!;
     
    public string EmployeeName { get; set; } = null!;

    public string? Designation { get; set; }
    public string? DesignationCode { get; set; }


    public int? RoleId { get; set; }
    public string? UserRole { get; set; }

    public bool CanEdit { get; set; }
    public bool RequireCrossFunctionalHead { get; set; }
    public bool IsParallelApproval { get; set; }
}

public class WorkflowStepCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public int WorkflowPolicyId { get; set; }

    public string DocumentTypeCode { get; set; }

    public int Sequence { get; set; }

    public int? RoleId { get; set; }
    public int? UserId { get; set; }

    public int? ApprovalLevel { get; set; }

    public bool CanEdit { get; set; }
    public bool RequireCrossFunctionalHead { get; set; }
    public bool IsParallelApproval { get; set; }
}

public class WorkflowStepUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public int WorkflowPolicyId { get; set; }

    public string DocumentTypeCode { get; set; }

    public int Sequence { get; set; }

    public int? RoleId { get; set; }
    public int? UserId { get; set; }

    public int? ApprovalLevel { get; set; }

    public bool CanEdit { get; set; }
    public bool RequireCrossFunctionalHead { get; set; }
    public bool IsParallelApproval { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}


public class PendingRequestDto
{
    public long RequestId { get; set; }
    public string RequestNumber { get; set; }
    public string DocumentName { get; set; }
    public string RequestType { get; set; }

    public string SubmittedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public long WorkflowExecutionId { get; set; }
    public long StepId { get; set; }
}



// DTO for filter parameters
public class WorkFlowStepsFilterDto
{ 
    public int WorkflowPolicyId { get; set; }
    public string StepType { get; set; }
    public string EntityType { get; set; }
    public string DocumentTypeCode { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public List<long>? Roles { get; set; }
    public List<string>? EmployeeCodes { get; set; }
    public List<string>? DesignationCodes { get; set; }

    public bool CanEdit { get; set; }
    public bool RequireCrossFunctionalHead { get; set; }
    public bool IsParallelApproval { get; set; }
}


public class GetStepDefinitionFilterDto
{ 
    public string EntityType { get; set; }
    public string DocumentTypeCode { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
     
}