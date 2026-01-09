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

public class BusinessDomainReadDto
{
    public int Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string SubDepartment { get; set; }
    public string SubDepartmentCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; }
}