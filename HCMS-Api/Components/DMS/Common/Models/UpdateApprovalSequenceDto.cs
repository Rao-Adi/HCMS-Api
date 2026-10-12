using System.Collections.Generic;

namespace HCMS_Api.Components.DMS.Common.Models;

public class UpdateApprovalSequenceDto
{
    public int WorkflowPolicyId { get; set; }
    public List<WorkflowStepSequenceDto> Steps { get; set; } = new List<WorkflowStepSequenceDto>();
}

public class WorkflowStepSequenceDto
{
    public int StepOrder { get; set; }
    public int StepGroup { get; set; }
    public string StepType { get; set; }
    public int? RoleId { get; set; }
    public int? DesignationId { get; set; }
    public string? UserId { get; set; }
    public bool RequiresAllApprovals { get; set; }

    // Carried through a re-ordering: the sequence is rewritten delete-then-insert, so a field that is
    // not sent back here would be lost every time someone drags a step.
    public bool CanAddAdHocApprover { get; set; }
}