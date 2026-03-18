using System.ComponentModel.DataAnnotations;

namespace HCMS_Api.Components.DMS.Common.Models;
 
public class ControlType : AuditableEntity
{
    [Key]
    public int Id { get; set; } 

    [MaxLength(100)]
    public string Name { get; set; } = null!;  


    public ICollection<Division> Divisions { get; set; } = new List<Division>();
}

public class ControlTypeReadDto : AuditableEntity
{
    [Key]
    public int Id { get; set; }
     

    [MaxLength(100)]
    public string Name { get; set; } = null!; 

}


public class ControlTypeCreateDto
{
    public int Id { get; set; } 

    [MaxLength(100)]
    public string Name { get; set; } = null!; 
}


public class ControlTypeUpdateDto
{
    public int Id { get; set; } 

    [MaxLength(100)]
    public string Name { get; set; } = null!; 
}