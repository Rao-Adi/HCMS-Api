using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("WorkflowPolicies")]
public class WorkflowPolicy
{
    [Key]
    public Guid Id { get; set; }

    public int PolicyType { get; set; }

    [MaxLength(10)]
    public string? DivisionCode { get; set; }

    [MaxLength(10)]
    public string? DepartmentCode { get; set; }

    [MaxLength(10)]
    public string? SubDepartmentCode { get; set; }

    [MaxLength(10)]
    public string? DocumentTypeCode { get; set; }

    public int? SharingType { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; }

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}
