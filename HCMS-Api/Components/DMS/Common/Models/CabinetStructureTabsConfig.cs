namespace HCMS_Api.Components.DMS.Common.Models;

public class CabinetStructureTabsConfig
{
}


public class CabinetStructureTabsConfigCreateDto
{
    public int Id { get; set; }

    public string Name { get; set; }
}

public class CabinetStructureTabsConfigUpdateDto
{
    public int Id { get; set; }
    public string Name { get; set; }

    public bool IsActive { get; set; }
}

public class CabinetStructureTabsConfigReadDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public bool IsDeleted { get; set; }
    public string? CreatedAt { get; set; }
    public string CreatedBy { get; set; }
    public string? LastModifiedAt { get; set; }
    public string LastModifiedBy { get; set; }
}