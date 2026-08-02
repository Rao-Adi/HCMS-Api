using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("EmployeeDraftObservation")]
public class EmployeeDraftObservation
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string EmployeeCode { get; set; } = null!;

    [Required]
    public int CompanyId { get; set; }

    [Required]
    public string ObservationText { get; set; } = null!;
}

public class EmployeeDraftObservationDto
{ 

    [Required]
    public string ObservationText { get; set; } = null!;
}
