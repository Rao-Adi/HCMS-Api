namespace HCMS_Api.Components.DMS.Common.Models.Divisions;

public class SubDepartmentDto
{
}


public class SubDepartmentCreateDto
{
    public string Code { get; set; }
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

public class SubDepartmentReadDto
{
    public string Code { get; set; }
    public string Name { get; set; }

    public string DepartmentCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; }
}
