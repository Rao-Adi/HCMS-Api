namespace HCMS_Api.Components.DMS.Common.Models;

// A People Partners role mapped to a DMS cabinet scope. Division is required; the lower levels are
// optional and blank means "everything under the level above".
public class RoleCabinetMappingReadDto : AuditableEntity
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int RoleId { get; set; }
    public string? RoleName { get; set; }

    public string? Division { get; set; }
    public string? DivisionCode { get; set; }
    public string? Department { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartment { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomain { get; set; }
    public string? BusinessDomainCode { get; set; }
}

public class RoleCabinetMappingCreateDto
{
    public int RoleId { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
}

public class RoleCabinetMappingUpdateDto : RoleCabinetMappingCreateDto
{
    public int Id { get; set; }
}
