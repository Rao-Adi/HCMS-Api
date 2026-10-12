using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Models;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// Maps a People Partners ROLE to a DMS Cabinet scope. Division is required; Department, Sub-Department
/// and Business Domain are optional and, when left blank, mean "everything under the level above" --
/// the same way a workflow policy's cabinet scope reads. A lower level can only be given together with
/// the level above it, and must really belong to it.
/// </summary>
public class RoleCabinetMappingComponent
{
    private const int RoleSetupMasterId = 189; // tblsetupsdetail.smsid for People Partners roles

    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;
    private readonly DMSCommon _common;

    public RoleCabinetMappingComponent(
        DMSUtilities utilities,
        ClientContextService clientContextService,
        DMSCommon common)
    {
        _utilities = utilities;
        _clientContextService = clientContextService;
        _common = common;
    }

    private int GetCompanyId() => int.Parse(_utilities.GetCompanyId(_clientContextService.GetClientIP()));

    private string GetEmpCode()
    {
        var empId = _utilities.GetEmpid(_clientContextService.GetClientIP());
        return _utilities.GetEmpCodeForHCMS(empId.ToString());
    }

    private const string SelectSql = @"
        SELECT m.Id, m.CompanyId, m.RoleId, r.name AS RoleName,
               m.DivisionCode, div.Name AS Division,
               m.DepartmentCode, dep.Name AS Department,
               m.SubDepartmentCode, sdep.Name AS SubDepartment,
               m.BusinessDomainCode, bd.Name AS BusinessDomain,
               m.IsActive, m.IsDeleted,
               to_char(m.CreatedAt, 'YYYY-MM-DD HH24:MI:SS') AS CreatedAt, m.CreatedBy,
               to_char(m.LastModifiedAt, 'YYYY-MM-DD HH24:MI:SS') AS LastModifiedAt, m.LastModifiedBy
        FROM RoleCabinetMappings m
        LEFT JOIN public.tblsetupsdetail r ON r.sdlid = m.RoleId AND r.smsid = 189
        LEFT JOIN Divisions div ON div.Code = m.DivisionCode AND div.CompanyId = m.CompanyId
        LEFT JOIN Departments dep ON dep.Code = m.DepartmentCode AND dep.CompanyId = m.CompanyId
        LEFT JOIN SubDepartments sdep ON sdep.Code = m.SubDepartmentCode AND sdep.CompanyId = m.CompanyId
        LEFT JOIN BusinessDomains bd ON bd.Code = m.BusinessDomainCode AND bd.CompanyId = m.CompanyId
        WHERE m.CompanyId = @CompanyId AND m.IsDeleted = FALSE AND m.IsActive = TRUE";

    public async Task<List<RoleCabinetMappingReadDto>> GetAllAsync()
    {
        var rows = await _common.QueryAsync<RoleCabinetMappingReadDto>(
            SelectSql + " ORDER BY m.Id DESC;",
            new { CompanyId = GetCompanyId() });
        return rows.ToList();
    }

    public async Task<RoleCabinetMappingReadDto> GetByIdAsync(int id)
    {
        var row = await _common.QueryFirstOrDefaultAsync<RoleCabinetMappingReadDto>(
            SelectSql + " AND m.Id = @Id;",
            new { CompanyId = GetCompanyId(), Id = id });

        return row ?? throw new CustomException("Role cabinet mapping not found", 404);
    }

    public async Task<RoleCabinetMappingReadDto> CreateAsync(RoleCabinetMappingCreateDto input)
    {
        int companyId = GetCompanyId();
        string empCode = GetEmpCode();

        var scope = await ValidateAsync(companyId, input);
        await EnsureNotDuplicateAsync(companyId, null, input.RoleId, scope);

        int newId = await _common.ExecuteScalarAsync<int>(@"
            INSERT INTO RoleCabinetMappings
                (CompanyId, RoleId, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode,
                 IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
            VALUES
                (@CompanyId, @RoleId, @Division, @Department, @SubDepartment, @BusinessDomain,
                 TRUE, FALSE, NOW(), @EmpCode, NOW(), @EmpCode)
            RETURNING Id;",
            new
            {
                CompanyId = companyId,
                input.RoleId,
                scope.Division,
                scope.Department,
                scope.SubDepartment,
                scope.BusinessDomain,
                EmpCode = empCode
            });

        return await GetByIdAsync(newId);
    }

    public async Task<RoleCabinetMappingReadDto> UpdateAsync(RoleCabinetMappingUpdateDto input)
    {
        int companyId = GetCompanyId();
        string empCode = GetEmpCode();

        await EnsureExistsAsync(companyId, input.Id);

        var scope = await ValidateAsync(companyId, input);
        await EnsureNotDuplicateAsync(companyId, input.Id, input.RoleId, scope);

        await _common.ExecuteAsync(@"
            UPDATE RoleCabinetMappings
            SET RoleId = @RoleId,
                DivisionCode = @Division,
                DepartmentCode = @Department,
                SubDepartmentCode = @SubDepartment,
                BusinessDomainCode = @BusinessDomain,
                LastModifiedAt = NOW(),
                LastModifiedBy = @EmpCode
            WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = FALSE;",
            new
            {
                input.Id,
                CompanyId = companyId,
                input.RoleId,
                scope.Division,
                scope.Department,
                scope.SubDepartment,
                scope.BusinessDomain,
                EmpCode = empCode
            });

        return await GetByIdAsync(input.Id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        int companyId = GetCompanyId();
        string empCode = GetEmpCode();

        await EnsureExistsAsync(companyId, id);

        await _common.ExecuteAsync(@"
            UPDATE RoleCabinetMappings
            SET IsDeleted = TRUE, IsActive = FALSE, LastModifiedAt = NOW(), LastModifiedBy = @EmpCode
            WHERE Id = @Id AND CompanyId = @CompanyId;",
            new { Id = id, CompanyId = companyId, EmpCode = empCode });

        return true;
    }

    // ------------------------------------------------------------------------------------------------

    private readonly record struct Scope(string Division, string? Department, string? SubDepartment, string? BusinessDomain);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureExistsAsync(int companyId, int id)
    {
        int exists = await _common.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM RoleCabinetMappings WHERE Id = @Id AND CompanyId = @CompanyId AND IsDeleted = FALSE;",
            new { Id = id, CompanyId = companyId });

        if (exists == 0)
            throw new CustomException("Role cabinet mapping not found", 404);
    }

    /// <summary>Checks the role and the cabinet scope, and returns the scope with blank levels as null.</summary>
    private async Task<Scope> ValidateAsync(int companyId, RoleCabinetMappingCreateDto input)
    {
        if (input.RoleId <= 0)
            throw new CustomException("Please select a Role.", 400);

        string? division = Blank(input.DivisionCode);
        string? department = Blank(input.DepartmentCode);
        string? subDepartment = Blank(input.SubDepartmentCode);
        string? businessDomain = Blank(input.BusinessDomainCode);

        if (division == null)
            throw new CustomException("Division is required.", 400);
        if (department == null && subDepartment != null)
            throw new CustomException("Select a Department before choosing a Sub-Department.", 400);
        if (subDepartment == null && businessDomain != null)
            throw new CustomException("Select a Sub-Department before choosing a Business Domain.", 400);

        int roleExists = await _common.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM public.tblsetupsdetail WHERE sdlid = @RoleId AND smsid = @MasterId;",
            new { input.RoleId, MasterId = RoleSetupMasterId });
        if (roleExists == 0)
            throw new CustomException("The selected Role was not found.", 404);

        if (await CountAsync("Divisions", "Code = @Code", companyId, new { Code = division }) == 0)
            throw new CustomException("The selected Division was not found.", 404);

        if (department != null &&
            await CountAsync("Departments", "Code = @Code AND DivisionCode = @Parent", companyId,
                new { Code = department, Parent = division }) == 0)
            throw new CustomException("The selected Department does not belong to the selected Division.", 400);

        if (subDepartment != null &&
            await CountAsync("SubDepartments", "Code = @Code AND DepartmentCode = @Parent", companyId,
                new { Code = subDepartment, Parent = department }) == 0)
            throw new CustomException("The selected Sub-Department does not belong to the selected Department.", 400);

        if (businessDomain != null &&
            await CountAsync("BusinessDomains", "Code = @Code AND SubDepartmentCode = @Parent", companyId,
                new { Code = businessDomain, Parent = subDepartment }) == 0)
            throw new CustomException("The selected Business Domain does not belong to the selected Sub-Department.", 400);

        return new Scope(division, department, subDepartment, businessDomain);
    }

    // Table and condition come from the fixed strings above, never from user input.
    private Task<int> CountAsync(string table, string condition, int companyId, object parameters)
    {
        var dp = new Dapper.DynamicParameters(parameters);
        dp.Add("CompanyId", companyId);
        return _common.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {table} WHERE {condition} AND CompanyId = @CompanyId AND IsDeleted = FALSE;", dp);
    }

    private async Task EnsureNotDuplicateAsync(int companyId, int? ignoreId, int roleId, Scope scope)
    {
        int duplicates = await _common.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM RoleCabinetMappings
            WHERE CompanyId = @CompanyId AND IsDeleted = FALSE
              AND RoleId = @RoleId AND DivisionCode = @Division
              AND COALESCE(DepartmentCode, '') = COALESCE(@Department::text, '')
              AND COALESCE(SubDepartmentCode, '') = COALESCE(@SubDepartment::text, '')
              AND COALESCE(BusinessDomainCode, '') = COALESCE(@BusinessDomain::text, '')
              AND (@IgnoreId::int IS NULL OR Id <> @IgnoreId::int);",
            new
            {
                CompanyId = companyId,
                RoleId = roleId,
                scope.Division,
                scope.Department,
                scope.SubDepartment,
                scope.BusinessDomain,
                IgnoreId = ignoreId
            });

        if (duplicates > 0)
            throw new CustomException("This Role is already mapped to this Cabinet.", 409);
    }
}
