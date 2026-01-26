using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("ESignatures")]
public class ESignature : AuditableEntity
{
    [Key]
    public int Id { get; set; }


    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }
    
}
public class ESignatureReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public int UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }

}

public class ESignatureCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public int UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }

}

public class ESignatureUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public int UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}

