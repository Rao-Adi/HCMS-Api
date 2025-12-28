using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentVersions")]
public class DocumentVersion
{
    [Key]
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    [MaxLength(10)]
    public string Version { get; set; } = null!;

    public int VersionType { get; set; }
    public string Content { get; set; } = null!;
    public string? ChangeDescription { get; set; }
    public int Status { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; }
    public string CreatedAt { get; set; }

    public Document Document { get; set; } = null!;
}
