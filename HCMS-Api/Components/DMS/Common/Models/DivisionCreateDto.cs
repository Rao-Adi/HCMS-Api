namespace HCMS_Api.Components.DMS.Common.Models;

public class DivisionCreateDto
{ 
    public string Code { get; set; }

    public string Name { get; set; }
}

public class DivisionUpdateDto
{
    public string Code { get; set; }
    public string Name { get; set; }

    public bool IsActive { get; set; }
}

public class DivisionReadDto
{      
    public string Code { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; }
}