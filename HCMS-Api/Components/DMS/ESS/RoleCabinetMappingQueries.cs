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
    /// that is blank at that level covers it.
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
        // The Division / Department / Sub-Department on a row are People Partners ids (the hierarchy the Users
        // screen shows); a level left blank is "all". "Any role" is every role held by an active employee inside
        // that organisation unit -- exactly what the grid's Role dropdown offers for it. The Business Domain is not
        // part of this hierarchy and is ignored.
        var roleIds = await common.QueryAsync<int>(@"
            SELECT DISTINCT ejp.roleid
            FROM tblEmployee e
            INNER JOIN tblempjobprofile ejp ON ejp.empid = e.empid AND COALESCE(ejp.Active, TRUE) = TRUE AND ejp.CompanyId = @CompanyId
            INNER JOIN tblsetupsdetail b ON b.sdlid = ejp.roleid AND b.smsid = 189
            WHERE e.CompanyId = @CompanyId AND COALESCE(e.Active, 1) = 1 AND ejp.roleid IS NOT NULL
              AND (COALESCE(@Division::text, '') = '' OR e.divid::text = @Division::text)
              AND (COALESCE(@Department::text, '') = '' OR e.mdptid::text = @Department::text)
              AND (COALESCE(@SubDepartment::text, '') = '' OR e.dptid::text = @SubDepartment::text);",
            new
            {
                CompanyId = companyId,
                Division = divisionCode,
                Department = departmentCode,
                SubDepartment = subDepartmentCode
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
