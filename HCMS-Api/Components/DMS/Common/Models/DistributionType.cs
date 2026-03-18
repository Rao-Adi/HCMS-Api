using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;
 

[Table("DistributionTypes")]
public class DistributionType : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [MaxLength(50)]
    public string Name { get; set; } = null!; 
     
}


public class DistributionTypeReadDto : AuditableEntity
{
    public int Id { get; set; }


    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;


    [MaxLength(50)]
    public string Name { get; set; } = null!;

}


public class DistributionTypeCreateDto
{
    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 


    [MaxLength(50)]
    public string Name { get; set; } = null!;
     

}


public class DistributionTypeUpdateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 


    [MaxLength(50)]
    public string Name { get; set; } = null!; 

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}
