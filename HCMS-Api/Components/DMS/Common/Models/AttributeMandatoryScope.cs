using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("AttributeMandatoryScopes")]
public class AttributeMandatoryScope : AuditableEntity
{
    [Key]
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public int DocumentAttributeId { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }

    public bool IsMandatory { get; set; }

    // Navigation Properties
    public DocumentAttribute DocumentAttribute { get; set; } = null!;
    public Division Division { get; set; } = null!;
    public Department Department { get; set; } = null!;
}

public class AttributeMandatoryScopeCreateDto
{

    // 🔑 Tenant
    public int CompanyId { get; set; } 
    public int DocumentAttributeId { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public string SubDepartmentCode { get; set; }

    public bool IsMandatory { get; set; }

}

public class AttributeMandatoryScopeUpdateDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; } 

    public int DocumentAttributeId { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }

    public string SubDepartmentCode { get; set; }

    public bool IsMandatory { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}

public class AttributeMandatoryScopeReadDto
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public int DocumentAttributeId { get; set; }
    public string Division { get; set; }
    public string DivisionCode { get; set; }
    public string Department { get; set; }
    public string DepartmentCode { get; set; }

    public string SubDepartment { get; set; }
    public string SubDepartmentCode { get; set; }

    public bool IsMandatory { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; }
    public string CreatedAt { get; set; }
    public string LastModifiedBy { get; set; }
    public string LastModifiedAt { get; set; }
}