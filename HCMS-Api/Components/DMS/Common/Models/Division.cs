using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Divisions")]
public class Division
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

    public ICollection<Department> Departments { get; set; } = new List<Department>();
}


public class DivisionReadDto :AuditableEntity
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string Code { get; set; } = null!;
     
    public string Name { get; set; } = null!;
     

    public List<Department> Departments { get; set; } = new List<Department>();
}



public class DivisionCreateDto
{
    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public string Name { get; set; } = null!; 
}



public class DivisionUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    public string Code { get; set; } = null!;
     
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
     
     
}
