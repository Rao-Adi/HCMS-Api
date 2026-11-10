using HCMS_Api.Common;
using HCMS_Api.Common.DMS;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// Approval workflow policies are written for a Division / Department / Sub-Department. When such a unit was deleted the policies kept pointing at it,
/// and nobody could tell: the policy screen shows only live units, so the policy looked as if it applied to "any" sub-department, while no
/// document can be filed under a deleted one -- the policy never applied and approvers added to it never saw a document.
/// So (1) a unit that a live policy is written for cannot be deleted (EnsureUnitNotUsedAsync), and (2) the policy list says so when its unit was
/// deleted before (UnitNamesAsync, used by WorkflowPolicyComponent.GetAllAsync).
/// </summary>
public static class WorkflowPolicyUnitUsage
{
    public const string DivisionColumn = "DivisionCode";
    public const string DepartmentColumn = "DepartmentCode";
    public const string SubDepartmentColumn = "SubDepartmentCode";

    /// <summary>Throws a 409 naming the live policies written for this unit; does nothing when there are none.</summary>
    public static async Task EnsureUnitNotUsedAsync(DMSCommon common, int companyId, string column, string unitCode, string unitLabel)
    {
        // column is one of the three constants above, never user input
        if (column != DivisionColumn && column != DepartmentColumn && column != SubDepartmentColumn)
            throw new ArgumentException("Unknown policy column", nameof(column));

        var rows = (await common.QueryAsync<dynamic>($@"
            SELECT Name AS name, EntityType AS entitytype
            FROM WorkflowPolicies
            WHERE CompanyId = @CompanyId AND IsDeleted = FALSE AND IsActive = TRUE AND {column} = @Code
            ORDER BY Name, EntityType;", new { CompanyId = companyId, Code = unitCode })).ToList();
        if (rows.Count == 0) return;

        var names = string.Join(", ", rows.GroupBy(r => (string)r.name)
            .Select(g => $"{g.Key} ({string.Join(", ", g.Select(x => (string)x.entitytype).Distinct())})"));
        throw new CustomException(
            $"This {unitLabel} cannot be deleted: approval workflow polic{(rows.Select(r => (string)r.name).Distinct().Count() == 1 ? "y" : "ies")} {names} "
            + $"{(rows.Select(r => (string)r.name).Distinct().Count() == 1 ? "is" : "are")} written for it. Delete those policies (or move them to another {unitLabel}) first.", 409);
    }

    /// <summary>
    /// For policies whose Division / Department / Sub-Department no longer exists (deleted or deactivated): that unit's name by code, so the list can
    /// show "IT Development (deleted)" instead of the bare code. Units that are live are not in the result.
    /// </summary>
    public static async Task<Dictionary<string, string>> DeletedUnitNamesAsync(
        DMSCommon common, int companyId, IEnumerable<string?> divisionCodes, IEnumerable<string?> departmentCodes, IEnumerable<string?> subDepartmentCodes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        async Task Load(string table, IEnumerable<string?> codes)
        {
            var list = codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()).Distinct().ToArray();
            if (list.Length == 0) return;
            var rows = await common.QueryAsync<dynamic>($@"
                SELECT Code AS code, Name AS name FROM {table}
                WHERE CompanyId = @CompanyId AND Code = ANY(@Codes) AND (IsDeleted = TRUE OR IsActive = FALSE);",
                new { CompanyId = companyId, Codes = list });
            foreach (var r in rows) result[(string)r.code] = (string)r.name;
        }
        await Load("Divisions", divisionCodes);
        await Load("Departments", departmentCodes);
        await Load("SubDepartments", subDepartmentCodes);
        return result;
    }
}
