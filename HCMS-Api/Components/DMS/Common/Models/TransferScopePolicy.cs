using System.ComponentModel.DataAnnotations;

namespace HCMS_Api.Components.DMS.Common.Models;

public class TransferScopePolicy : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    [MaxLength(10)]
    public string DivisionCode { get; set; } = null!;
     
    public int ReportingToLevel { get; set; } 

    public ICollection<Division> Divisions { get; set; } = new List<Division>();
}
