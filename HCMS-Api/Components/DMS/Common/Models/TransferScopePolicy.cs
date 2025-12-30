using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("TransferScopePolicies")]
public class TransferScopePolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;
     
    public int ReportingToLevel { get; set; } 

    public ICollection<Division> Divisions { get; set; } = new List<Division>();
}


public class TransferScopePolicyReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ReportingToLevel { get; set; }
     
}


public class TransferScopePolicyCreateDto
{ 
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ReportingToLevel { get; set; }
     
}



public class TransferScopePolicyUpdateDto
{ 
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;

    public int ReportingToLevel { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}
