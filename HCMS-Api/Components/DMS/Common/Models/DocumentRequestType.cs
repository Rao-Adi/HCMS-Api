using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;


[Table("DocumentRequestTypes")]
public class DocumentRequestType : BaseEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; } = null!;

    public ICollection<Document> Documents { get; set; } = new HashSet<Document>();
}

public class DocumentRequestTypeCreateDto
{
    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
}

public class DocumentRequestTypeUpdateDto
{

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public class DocumentRequestTypeReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;
    public string Code { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }

}
