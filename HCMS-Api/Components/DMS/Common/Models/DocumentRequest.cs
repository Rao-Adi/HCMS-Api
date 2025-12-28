using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DocumentRequests")]
public class DocumentRequest : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(50)]
    public string RequestNumber { get; set; } = null!;

    public int RequestType { get; set; } // 1=Creation, 2=Revision, 3=Obsoletion

    public Guid? DocumentId { get; set; }

    [MaxLength(10)]
    public string DocumentTypeCode { get; set; } = null!;

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    [MaxLength(10)]
    public string DepartmentCode { get; set; } = null!;

    [MaxLength(10)]
    public string SubDepartmentCode { get; set; } = null!;

    [MaxLength(500)]
    public string DocumentName { get; set; } = null!;

    [MaxLength(2000)]
    public string Justification { get; set; } = null!;

    public int Status { get; set; }
    public int CurrentStep { get; set; }

    public Document? Document { get; set; }
    public ICollection<RequestApproval> Approvals { get; set; } = new List<RequestApproval>();
}
