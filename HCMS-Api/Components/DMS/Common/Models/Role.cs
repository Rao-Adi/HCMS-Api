using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("Roles")]
public class Role : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;


    [MaxLength(50)]
    public string Name { get; set; } = null!;

    [MaxLength(200)]
    public string Description { get; set; } = null!;
     
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}


public class RoleReadDto : AuditableEntity
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public string Company { get; set; } = null!;


    [MaxLength(50)]
    public string Name { get; set; } = null!;

    [MaxLength(200)]
    public string Description { get; set; } = null!;
     
}


public class RoleCreateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    [MaxLength(50)]
    public string Name { get; set; } = null!;

    [MaxLength(200)]
    public string Description { get; set; } = null!;
     
}


public class RoleUpdateDto
{ 
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; } 

    [MaxLength(50)]
    public string Name { get; set; } = null!;

    [MaxLength(200)]
    public string Description { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
}
