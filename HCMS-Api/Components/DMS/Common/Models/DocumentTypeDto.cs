namespace HCMS_Api.Components.DMS.Common.Models;

public class DocumentTypeDto
{
}

public class DocumentType : BaseEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; } = null!;

    public ICollection<Document> Documents { get; set; } = new HashSet<Document>();
}

public class DocumentTypeCreateDto
{
    // 🔑 Tenant
    public int CompanyId { get; set; } 
    public string Name { get; set; }
    public string? Description { get; set; }
}

public class DocumentTypeUpdateDto
{
    public int Id { get; set; }
    // 🔑 Tenant
    public int CompanyId { get; set; } 
    public string Code { get; set; }
    public string Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public class DocumentTypeReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!; 
    public string? Description { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }

     
}
