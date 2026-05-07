namespace HCMS_Api.Components.DMS.Common.Models;

public class BusinessDomainDto
{
}


public class BusinessDomainCreateDto
{ 

    public string Code { get; set; }

    public string Name { get; set; }
    public string SubDepartmentCode { get; set; }
}

public class BusinessDomainUpdateDto
{ 
    public string Code { get; set; }
    public string Name { get; set; }

    public string SubDepartmentCode { get; set; }

    public bool IsActive { get; set; }
}

public class BusinessDomainReadDto : AuditableEntity
{
    public int Id { get; set; }

    // 🔑 Tenant
    public int CompanyId { get; set; }
    public string Company { get; set; } = null!;

    public string Code { get; set; }
    public string Name { get; set; }
    public string SubDepartment { get; set; }
    public string SubDepartmentCode { get; set; } 
}