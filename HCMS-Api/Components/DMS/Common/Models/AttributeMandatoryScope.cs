using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("AttributeMandatoryScopes")]
public class AttributeMandatoryScope
{
    [Key]
    public Guid Id { get; set; }

    public Guid DocumentAttributeId { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; }
    public string CreatedAt { get; set; }
    public string LastModifiedBy { get; set; }
    public string LastModifiedAt { get; set; }

    // Navigation Properties
    public DocumentAttribute DocumentAttribute { get; set; } = null!;
    public Division Division { get; set; } = null!;
    public Department Department { get; set; } = null!;
}

public class AttributeMandatoryScopeCreateDto
{ 

    public Guid DocumentAttributeId { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
     
}

public class AttributeMandatoryScopeUpdateDto
{
    public Guid Id { get; set; }

    public Guid DocumentAttributeId { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
     
}

public class AttributeMandatoryScopeReadDto
{
    public Guid Id { get; set; }

    public Guid DocumentAttributeId { get; set; }
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; }
    public string CreatedAt { get; set; }
    public string LastModifiedBy { get; set; }
    public string LastModifiedAt { get; set; }
}