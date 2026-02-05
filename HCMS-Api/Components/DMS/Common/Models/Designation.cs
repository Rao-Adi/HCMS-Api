using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

 
[Table("Designations")]
public class Designation
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [MaxLength(10)]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; } = null!;
     
}


public class DesignationReadDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; } = null!;
     
}



public class DesignationCreateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}



public class DesignationUpdateDto
{
    public int Id { get; set; }


    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}
