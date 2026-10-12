using System.Data;
using HCMS_Api.Common.DMS;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Models;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// Reads of the Role &lt;-&gt; Cabinet mapping (Setups configurations &gt; Users &gt; Role Cabinet Mapping) for
/// the code that has to turn "Any role" into real roles.
/// </summary>
public static class RoleCabinetMappingQueries
{
    /// <summary>
    /// Every role with an active employee that is mapped to a cabinet covering the given one.
    ///
    /// A mapping covers a level when its own level is blank ("Any" -- it covers everything below the level
    /// above) or is exactly the value given. A level left blank on the row is "Any" too, so only a mapping
    /// that is blank at that level covers it. A mapping's Division is always set, so a row with no Division
    /// has no roles.
    ///
    /// This is exactly the set the Document Users / Distribution List Role dropdown offers for that
    /// cabinet, so "Any role" on a Distribution List row expands into precisely those roles and no others.
    /// </summary>
    public static async Task<List<int>> GetMappedActiveRoleIdsAsync(
        DMSCommon common,
        int companyId,
        string? divisionCode,
        string? departmentCode,
        string? subDepartmentCode,
        string? businessDomainCode,
        IDbTransaction? tx = null)
    {
        var roleIds = await common.QueryAsync<int>(@"
            SELECT DISTINCT m.RoleId
            FROM RoleCabinetMappings m
            WHERE m.CompanyId = @CompanyId AND m.IsDeleted = FALSE AND m.IsActive = TRUE
              AND m.DivisionCode = @Division::text
              AND (COALESCE(m.DepartmentCode, '') = '' OR m.DepartmentCode = @Department::text)
              AND (COALESCE(m.SubDepartmentCode, '') = '' OR m.SubDepartmentCode = @SubDepartment::text)
              AND (COALESCE(m.BusinessDomainCode, '') = '' OR m.BusinessDomainCode = @BusinessDomain::text)
              AND EXISTS (
                    SELECT 1
                    FROM tblempjobprofile a
                    INNER JOIN tblsetupsdetail b ON a.roleid = b.sdlid
                    WHERE b.smsid = 189 AND a.roleid = m.RoleId
                      AND a.CompanyId = @CompanyId AND a.Active = TRUE);",
            new
            {
                CompanyId = companyId,
                Division = divisionCode,
                Department = departmentCode,
                SubDepartment = subDepartmentCode,
                BusinessDomain = businessDomainCode
            }, tx);

        return roleIds.ToList();
    }

    /// <summary>
    /// The read side of "Any role": a Distribution List whose rows were expanded into one row per role at
    /// submission is shown as the single "Any" row the person picked. "Any" now stands for the roles mapped
    /// to that row's cabinet, so a group collapses when it holds exactly that set.
    /// </summary>
    public static async Task<List<DistributionListReadDto>> CollapseAnyRoleGroupsAsync(
        DMSCommon common, int companyId, List<DistributionListReadDto> rows, IDbTransaction? tx = null)
    {
        if (rows == null || rows.Count == 0)
            return rows ?? new List<DistributionListReadDto>();

        var byCabinet = new Dictionary<(string?, string?, string?, string?), List<int>>();
        foreach (var row in rows)
        {
            var key = (row.DivisionCode, row.DepartmentCode, row.SubDepartmentCode, row.BusinessDomainCode);
            if (!byCabinet.ContainsKey(key))
                byCabinet[key] = await GetMappedActiveRoleIdsAsync(
                    common, companyId, row.DivisionCode, row.DepartmentCode, row.SubDepartmentCode, row.BusinessDomainCode, tx);
        }

        return DMSUtilities.CollapseAnyRoleGroups(rows, r =>
            byCabinet[(r.DivisionCode, r.DepartmentCode, r.SubDepartmentCode, r.BusinessDomainCode)]);
    }
}
