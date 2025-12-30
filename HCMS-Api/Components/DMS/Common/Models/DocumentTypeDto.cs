namespace HCMS_Api.Components.DMS.Common.Models;

public class DocumentTypeDto
{
}

public class DocumentType : BaseEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;

    public ICollection<Document> Documents { get; set; } = new HashSet<Document>();
}

public class DocumentTypeCreateDto
{
    public string Code { get; set; }
    public string Description { get; set; }

    public string Name { get; set; }
}

public class DocumentTypeUpdateDto
{
    public string Code { get; set; }
    public string Name { get; set; }

    public string Description { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}

public class DocumentTypeReadDto : AuditableEntity
{
    public string Code { get; set; }
    public string Name { get; set; }

    public string Description { get; set; }
     
}
