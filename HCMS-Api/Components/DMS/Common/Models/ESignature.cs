using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("ESignatures")]
public class ESignature : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }
    
}
public class ESignatureReadDto : AuditableEntity
{ 
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }

}

public class ESignatureCreateDto
{ 
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }

}

public class ESignatureUpdateDto
{ 
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public byte[] SignatureData { get; set; } = null!;
    public int SignatureType { get; set; }

    [MaxLength(10)]
    public string? FileType { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}

