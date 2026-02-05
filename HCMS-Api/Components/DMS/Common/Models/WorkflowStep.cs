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
    public int WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public int? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; } 
}

public class WorkflowStepCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public int WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public int? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; } 
}

public class WorkflowStepUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public int WorkflowPolicyId { get; set; }

    public int Sequence { get; set; }

    public int? ApproverRoleId { get; set; }
    public int? ApproverUserId { get; set; }

    public int? ApprovalLevel { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}