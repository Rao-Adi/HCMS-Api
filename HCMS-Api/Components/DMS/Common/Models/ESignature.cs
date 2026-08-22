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

    // The employee this signature belongs to (tblEmployee.empCode -- matches how
    // DocumentComponent.MergeDocumentTemplateAsync joins ESignatures to the approver).
    public string UserId { get; set; }

    // Where the signature image actually lives on disk (e.g. "/uploads/signatures/xxx.png").
    // The raw image bytes are never stored in the database. The image's file extension is
    // derived from this path when needed (e.g. embedding into a Word document) rather than
    // stored separately.
    public string SignatureURL { get; set; } = null!;

    public int SignatureType { get; set; }
}
public class ESignatureReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string UserId { get; set; }
    public string SignatureURL { get; set; } = null!;
    public int SignatureType { get; set; }

}

public class ESignatureCreateDto
{
    // A data URI ("data:image/png;base64,...") or bare base64 -- either is accepted.
    // Matches the "SignatureBase64" field name esignature.ts already sends.
    public string SignatureBase64 { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public class ESignatureUpdateDto
{
    public int Id { get; set; }

    // Optional on update: omit to just change IsActive without replacing the image.
    public string? SignatureBase64 { get; set; }

    public bool IsActive { get; set; } = true;
}