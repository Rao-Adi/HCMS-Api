namespace HCMS_Api.Components.DMS.Common.Models;

public abstract class AuditableEntity
{
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public string CreatedAt { get; set; }

    public string LastModifiedBy { get; set; }
    public string? LastModifiedByName { get; set; }
    public string LastModifiedAt { get; set; }
}
 