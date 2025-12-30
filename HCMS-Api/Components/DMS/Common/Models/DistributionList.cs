using HCMS_Api.Components.DMS.Common.Models.Departments;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS_Api.Components.DMS.Common.Models;

[Table("DistributionLists")]
public class DistributionList : AuditableEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid DocumentRequestId { get; set; }

    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public int RoleId { get; set; }

    public int DistributionType { get; set; }

    // Navigation Properties
    public DocumentRequest DocumentRequest { get; set; } = null!;
    public Division Division { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public Role Role { get; set; } = null!;
}


public class DistributionListReadDto : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid DocumentRequestId { get; set; }

    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public int RoleId { get; set; }

    public int DistributionType { get; set; }

}


public class DistributionListCreateDto
{
    public Guid Id { get; set; }

    public Guid DocumentRequestId { get; set; }

    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public int RoleId { get; set; }

    public int DistributionType { get; set; }
}


public class DistributionListUpdateDto
{
    public Guid Id { get; set; }

    public Guid DocumentRequestId { get; set; }

    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public int RoleId { get; set; }

    public int DistributionType { get; set; }

    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }

}

