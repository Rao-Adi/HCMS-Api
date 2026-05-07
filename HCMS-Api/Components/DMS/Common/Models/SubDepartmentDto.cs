using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

public class SubDepartmentDto
{
}

[Table("SubDepartments")]
public class SubDepartment
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public Int64 CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [MaxLength(10)]
    public string DepartmentCode { get; set; } = null!;

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

    [ForeignKey(nameof(DepartmentCode))]
    public Department? Department { get; set; }
}


public class SubDepartmentCreateDto
{

    public string DepartmentCode { get; set; }

    public string Name { get; set; }
}

public class SubDepartmentUpdateDto
{

    public string Code { get; set; }
    public string Name { get; set; }

    public string DepartmentCode { get; set; }

    public bool IsActive { get; set; }
}

public class SubDepartmentReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string Code { get; set; }
    public string Name { get; set; }

    public string Department { get; set; }
    public string DepartmentCode { get; set; } 
}
