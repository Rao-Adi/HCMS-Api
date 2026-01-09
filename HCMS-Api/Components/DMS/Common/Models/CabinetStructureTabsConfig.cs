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

public class CabinetStructureTabsConfigReadDto : AuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
}