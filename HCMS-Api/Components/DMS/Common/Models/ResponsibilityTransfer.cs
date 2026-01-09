using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("ResponsibilityTransfers")]
public class ResponsibilityTransfer : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    public int EmployeeFromId { get; set; }
    public int EmployeeToId { get; set; }

    public int Reason { get; set; }

    public DateTime EffectiveDateFrom { get; set; }
    public DateTime? EffectiveDateTo { get; set; }

    public bool IsPermanent { get; set; }

    [MaxLength(1000)]
    public string Remarks { get; set; } = null!;

    public int Status { get; set; }

    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

}

public class ResponsibilityTransferReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    public int EmployeeFromId { get; set; }
    public int EmployeeToId { get; set; }

    public int Reason { get; set; }

    public DateTime EffectiveDateFrom { get; set; }
    public DateTime? EffectiveDateTo { get; set; }

    public bool IsPermanent { get; set; }

    [MaxLength(1000)]
    public string Remarks { get; set; } = null!;

    public int Status { get; set; }

    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

}

public class ResponsibilityTransferCreateDto
{ 
    public int Id { get; set; }

    public int EmployeeFromId { get; set; }
    public int EmployeeToId { get; set; }

    public int Reason { get; set; }

    public DateTime EffectiveDateFrom { get; set; }
    public DateTime? EffectiveDateTo { get; set; }

    public bool IsPermanent { get; set; }

    [MaxLength(1000)]
    public string Remarks { get; set; } = null!;

    public int Status { get; set; }

    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

}

public class ResponsibilityTransferUpdateDto
{ 
    public int Id { get; set; }

    public int EmployeeFromId { get; set; }
    public int EmployeeToId { get; set; }

    public int Reason { get; set; }

    public DateTime EffectiveDateFrom { get; set; }
    public DateTime? EffectiveDateTo { get; set; }

    public bool IsPermanent { get; set; }

    [MaxLength(1000)]
    public string Remarks { get; set; } = null!;

    public int Status { get; set; }

    public int? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
