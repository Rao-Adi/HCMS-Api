using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentVersions")]
public class DocumentVersion
{
    [Key]
    public int Id { get; set; }


    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int DocumentId { get; set; }

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


public class DocumentVersionReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public int DocumentId { get; set; }

    [MaxLength(10)]
    public string Version { get; set; } = null!;

    public int VersionType { get; set; }
    public string Content { get; set; } = null!;
    public string? ChangeDescription { get; set; }
    public int Status { get; set; }
}



public class DocumentVersionCreateDto
{
    public int Id { get; set; }

     

    public int DocumentId { get; set; }

    [MaxLength(10)]
    public string Version { get; set; } = null!;

    public int VersionType { get; set; }
    public string Content { get; set; } = null!;
    public string? ChangeDescription { get; set; }
    public int Status { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}



public class DocumentVersionUpdateDto
{
    public int Id { get; set; }
     

    public int DocumentId { get; set; }

    [MaxLength(10)]
    public string Version { get; set; } = null!;

    public int VersionType { get; set; }
    public string Content { get; set; } = null!;
    public string? ChangeDescription { get; set; }
    public int Status { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
