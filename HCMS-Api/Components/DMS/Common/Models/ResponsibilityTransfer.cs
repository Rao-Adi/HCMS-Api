using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("ResponsibilityTransfers")]
public class ResponsibilityTransfer : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string EmployeeFrom { get; set; }
    public string EmployeeTo { get; set; }
    public string ReasonForTransfer { get; set; }
    public DateTime EffectiveDateFrom { get; set; }
    public DateTime EffectiveDateTo { get; set; }
    public bool PermanentTransfer { get; set; }
    public string Attachment { get; set; }
    public string Remarks { get; set; }

}

public class ResponsibilityTransferReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string EmployeeFrom { get; set; }
    public string EmployeeTo { get; set; }
    public string ReasonForTransfer { get; set; }
    public DateTime EffectiveDateFrom { get; set; }
    public DateTime EffectiveDateTo { get; set; }
    public bool PermanentTransfer { get; set; }
    public string Attachment { get; set; }
    public string Remarks { get; set; }

}

public class ResponsibilityTransferCreateDto
{
    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public string EmployeeFrom { get; set; }
    public string EmployeeTo { get; set; }
    public string ReasonForTransfer { get; set; }
    public DateTime EffectiveDateFrom { get; set; }
    public DateTime EffectiveDateTo { get; set; }
    public bool PermanentTransfer { get; set; }
    public IFormFile Attachment { get; set; }
    public string Remarks { get; set; }

}

public class ResponsibilityTransferUpdateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 
    public string EmployeeFrom { get; set; }
    public string EmployeeTo { get; set; }
    public string ReasonForTransfer { get; set; }
    public DateTime EffectiveDateFrom { get; set; }
    public DateTime EffectiveDateTo { get; set; }
    public bool PermanentTransfer { get; set; }
    public IFormFile Attachment { get; set; }
    public string Remarks { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
