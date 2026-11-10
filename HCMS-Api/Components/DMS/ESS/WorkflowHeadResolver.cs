using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// A "Head of Division / Department / Sub-Department" approval step names no person. The policy only says WHICH unit's head
/// approves; who that is depends on the cabinet the request / document sits in, so it is worked out when it is submitted and
/// copied into the execution as ordinary approver steps -- from there the approval runs exactly as it does for a step that
/// names an employee (pending lists, notifications, responsibility transfers, ad-hoc approvers).
///
/// A head is an active employee who
///   * holds a role (role master, setup 189) whose name matches one of the rules for that scope in WorkflowHeadRoleRules. A rule that names
///     the unit ("Group Head of {unit}") is enough on its own; a generic rule ("Division Head") also needs
///   * a User Access Level on that very Division / Department / Sub-Department.
///
/// When no role names a head for a Division / Department (the head may simply be "Sr. Manager - IT Infrastructure" with an ordinary
/// role), the HR reporting lines decide: the member of the unit who reports to nobody inside it (see FindHeadByReportingAsync).
///
/// The role names are NOT hard-coded: they differ between companies (here the Division head is "Group Head of &lt;Division&gt;",
/// the Department head "Head of &lt;Department&gt;"), so each rule is an ILIKE pattern kept in WorkflowHeadRoleRules, where
/// "{unit}" stands for the name of the Division / Department / Sub-Department the document is filed under. When a role is
/// renamed, change the pattern -- no code change.
/// </summary>
public static class WorkflowHeadResolver
{
    public const string Division = "DIVISION";
    public const string Department = "DEPARTMENT";
    public const string SubDepartment = "SUBDEPARTMENT";

    /// <summary>The canonical scope for what was sent, or null when it is blank or not one of the three.</summary>
    public static string? NormalizeScope(string? scope) =>
        scope?.Trim().ToUpperInvariant() switch
        {
            Division => Division,
            Department => Department,
            SubDepartment => SubDepartment,
            _ => null
        };

    /// <summary>"Head of Division" / "Head of Department" / "Head of Sub-Department", as the Authority Type is named on screen.</summary>
    public static string Label(string? scope) =>
        NormalizeScope(scope) switch
        {
            Division => "Head of Division",
            Department => "Head of Department",
            SubDepartment => "Head of Sub-Department",
            _ => ""
        };

    private static (string Role, string Unit, string Table, string Column) Describe(string scope) =>
        scope switch
        {
            Division => ("Division Head", "Division", "Divisions", "DivisionCode"),
            Department => ("Department Head", "Department", "Departments", "DepartmentCode"),
            _ => ("Sub-Department Head", "Sub-Department", "SubDepartments", "SubDepartmentCode")
        };

    // The unit's name goes into an ILIKE pattern, so its own wildcard characters must be taken literally.
    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    /// <summary>One person who is the head of a unit: who they are, and the designation and role they hold.</summary>
    public sealed record HeadPerson(string EmpCode, string Name, string Designation, string Role);

    /// <summary>
    /// The head(s) of one Division / Department / Sub-Department (<paramref name="unitCode"/>), found by the role rules in
    /// WorkflowHeadRoleRules plus a User Access Level on that unit, each with their name, designation and role -- what the policy screen
    /// and the request / document forms show in place of the bare "Head of ..." label. Empty when nobody qualifies.
    /// </summary>
    public static async Task<(List<HeadPerson> Heads, string UnitName)> FindHeadsAsync(
        DMSCommon common, int companyId, string scope, string unitCode, IDbTransaction? tx)
    {
        var normalized = NormalizeScope(scope) ?? throw new CustomException("The approval policy has a Head step of an unknown kind.", 409);
        var info = Describe(normalized);

        var unitName = await common.ExecuteScalarAsync<string>(
            $"SELECT Name FROM {info.Table} WHERE CompanyId = @CompanyId AND Code = @Code LIMIT 1;",
            new { CompanyId = companyId, Code = unitCode }, tx) ?? "";

        var rows = await common.QueryAsync<dynamic>($@"
            SELECT x.empid, TRIM(x.empcode) AS empcode, x.fullname, x.designation, x.rolename
            FROM (
                SELECT DISTINCT ON (e.empid)
                       e.empid, e.empcode,
                       LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS fullname,
                       COALESCE((SELECT d.name FROM public.tblempjobprofile j
                                   JOIN public.tblsetupsdetail d ON d.sdlid = j.dsgid
                                  WHERE j.empid = e.empid AND COALESCE(j.active, TRUE) = TRUE
                                  ORDER BY (j.CompanyId = @CompanyId) DESC, j.jobprofileid DESC LIMIT 1),
                                (SELECT d2.name FROM public.tblsetupsdetail d2 WHERE d2.sdlid = e.dsgid)) AS designation,
                       r.name AS rolename
                FROM public.tblEmployee e
                INNER JOIN public.tblempjobprofile p
                        ON p.empid = e.empid AND p.CompanyId = @CompanyId AND COALESCE(p.active, TRUE) = TRUE
                INNER JOIN public.tblsetupsdetail r
                        ON r.sdlid = p.roleid AND r.smsid = 189
                       AND EXISTS (
                            SELECT 1 FROM WorkflowHeadRoleRules ru
                            WHERE ru.CompanyId = @CompanyId AND ru.HeadScope = @Scope
                              AND ru.IsActive = TRUE
                              AND TRIM(r.name) ILIKE REPLACE(ru.RoleNamePattern, '{{unit}}', @UnitLike)
                              -- A rule that names the unit (Group Head of {{unit}}) already says WHICH unit the person heads, so the role alone is
                              -- enough. A generic rule (Division Head) says nothing about the unit, so there the person must also have a User
                              -- Access Level on it.
                              AND (ru.RoleNamePattern LIKE '%{{unit}}%'
                                   OR EXISTS (SELECT 1 FROM UserAccessLevels ual
                                              WHERE LTRIM(RTRIM(ual.EmployeeCode), '0') = LTRIM(RTRIM(e.empcode::text), '0')
                                                AND ual.CompanyId = @CompanyId
                                                AND ual.IsActive = TRUE AND ual.IsDeleted = FALSE
                                                AND ual.{info.Column} = @UnitCode)))
                WHERE e.CompanyId = @CompanyId
                  AND COALESCE(e.Active, 1) IN (1, 2)
                ORDER BY e.empid, r.name
            ) x
            ORDER BY x.empid ASC;",
            new { CompanyId = companyId, Scope = normalized, UnitLike = EscapeLike(unitName.Trim()), UnitCode = unitCode }, tx);

        var found = rows.ToList();

        // No role says who heads this unit (a head can be a Sr. Manager, a Director, ... whatever the role is called): use the company's
        // own reporting lines instead. See FindHeadByReportingAsync. Sub-Departments exist only in the DMS, not in HR, so they have no such fallback.
        if (found.Count == 0 && normalized != SubDepartment && !string.IsNullOrWhiteSpace(unitName))
            found = await FindHeadByReportingAsync(common, companyId, normalized, unitCode, unitName.Trim(), tx);

        var people = found.Select(row => new HeadPerson(
            ((string?)row.empcode ?? "").Trim(),
            ((string?)row.fullname ?? "").Trim(),
            ((string?)row.designation ?? "").Trim(),
            ((string?)row.rolename ?? "").Trim())).ToList();
        return (people, unitName);
    }

    /// <summary>
    /// The head of a Division / Department worked out from the HR reporting lines, for when no role rule names one. The members of the unit are the
    /// active employees whose HR Division (Department) has the same name as the cabinet's; a Department's members must also sit in the Division that
    /// holds it. The head is the member who reports to nobody inside the unit (their manager is outside it) and has the most direct reports inside
    /// it and whose reporting tree (everyone below them, through any number of levels) holds more than half of the unit. When no one person
    /// does (the head is away or inactive, or the unit has several unconnected leads), nobody is picked rather than guessing -- the policy screen
    /// then says no head is set up.
    /// </summary>
    private static async Task<List<dynamic>> FindHeadByReportingAsync(
        DMSCommon common, int companyId, string scope, string unitCode, string unitName, IDbTransaction? tx)
    {
        var parentName = "";
        if (scope == Department)
            parentName = (await common.ExecuteScalarAsync<string>(@"
                SELECT dv.Name FROM Departments d
                JOIN Divisions dv ON dv.CompanyId = d.CompanyId AND dv.Code = d.DivisionCode
                WHERE d.CompanyId = @CompanyId AND d.Code = @Code LIMIT 1;",
                new { CompanyId = companyId, Code = unitCode }, tx) ?? "").Trim();

        // Division = HR divid, Department = HR mdptid (the HR columns hold the unit's name through the setup master)
        var unitColumn = scope == Division ? "e.divid" : "e.mdptid";

        var rows = await common.QueryAsync<dynamic>($@"
            WITH RECURSIVE mem AS (
                SELECT e.empid, e.empcode, e.reportto,
                       LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS fullname,
                       e.dsgid
                FROM public.tblEmployee e
                WHERE e.CompanyId = @CompanyId AND COALESCE(e.Active, 1) IN (1, 2)
                  AND LOWER(TRIM((SELECT s.name FROM public.tblsetupsdetail s WHERE s.sdlid = {unitColumn}))) = LOWER(@UnitName)
                  AND (@ParentName = ''
                       OR LOWER(TRIM((SELECT s.name FROM public.tblsetupsdetail s WHERE s.sdlid = e.divid))) = LOWER(@ParentName))
            ), tops AS (
                SELECT m.* FROM mem m
                WHERE NOT EXISTS (SELECT 1 FROM mem x WHERE x.empid = m.reportto)
            ), tree AS (
                SELECT t.empid AS root, t.empid AS cur, 0 AS depth FROM tops t
                UNION ALL
                SELECT tr.root, m.empid, tr.depth + 1 FROM tree tr
                JOIN mem m ON m.reportto = tr.cur AND m.empid <> tr.root
                WHERE tr.depth < 20
            ), cover AS (
                SELECT root, COUNT(DISTINCT cur) AS n FROM tree GROUP BY root
            ), best AS (
                -- the head is the top whose reporting tree holds MORE THAN HALF of the unit (so it is unique)
                SELECT t.* FROM tops t JOIN cover c ON c.root = t.empid
                WHERE c.n * 2 > (SELECT COUNT(*) FROM mem)
            )
            SELECT b.empid, TRIM(b.empcode) AS empcode, b.fullname,
                   COALESCE((SELECT d.name FROM public.tblempjobprofile j
                               JOIN public.tblsetupsdetail d ON d.sdlid = j.dsgid
                              WHERE j.empid = b.empid AND COALESCE(j.active, TRUE) = TRUE
                              ORDER BY (j.CompanyId = @CompanyId) DESC, j.jobprofileid DESC LIMIT 1),
                            (SELECT d2.name FROM public.tblsetupsdetail d2 WHERE d2.sdlid = b.dsgid)) AS designation,
                   (SELECT r.name FROM public.tblempjobprofile p
                      JOIN public.tblsetupsdetail r ON r.sdlid = p.roleid AND r.smsid = 189
                     WHERE p.empid = b.empid AND p.CompanyId = @CompanyId AND COALESCE(p.active, TRUE) = TRUE
                     ORDER BY p.jobprofileid DESC LIMIT 1) AS rolename
            FROM best b;",
            new { CompanyId = companyId, UnitName = unitName, ParentName = parentName }, tx);
        return rows.ToList();
    }

    /// <summary>
    /// The employee codes of the head(s) of the unit named by <paramref name="scope"/> in the cabinet of the request /
    /// document. <paramref name="entityType"/> is "Request" (a DocumentRequests row) or anything else for a Documents row.
    /// A Revision / Obsoletion request that carries no cabinet of its own uses that of the document it acts on.
    /// Throws a CustomException saying what is missing when no head can be found, so a submit never starts a workflow with
    /// nobody to approve it.
    /// </summary>
    public static async Task<List<string>> ResolveAsync(
        DMSCommon common, int companyId, string scope, string entityType, long entityId, IDbTransaction tx)
    {
        var normalized = NormalizeScope(scope)
            ?? throw new CustomException("The approval policy has a Head step of an unknown kind.", 409);
        var info = Describe(normalized);

        dynamic? cabinet;
        if (string.Equals(entityType, "Request", StringComparison.OrdinalIgnoreCase))
        {
            cabinet = await common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT COALESCE(NULLIF(TRIM(dr.DivisionCode), ''), NULLIF(TRIM(d.DivisionCode), ''))          AS divisioncode,
                       COALESCE(NULLIF(TRIM(dr.DepartmentCode), ''), NULLIF(TRIM(d.DepartmentCode), ''))      AS departmentcode,
                       COALESCE(NULLIF(TRIM(dr.SubDepartmentCode), ''), NULLIF(TRIM(d.SubDepartmentCode), '')) AS subdepartmentcode
                FROM DocumentRequests dr
                LEFT JOIN Documents d ON d.Id = dr.ParentDocumentId AND d.CompanyId = dr.CompanyId
                WHERE dr.Id = @EntityId AND dr.CompanyId = @CompanyId;",
                new { EntityId = (int)entityId, CompanyId = companyId }, tx);
        }
        else
        {
            cabinet = await common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT NULLIF(TRIM(DivisionCode), '')       AS divisioncode,
                       NULLIF(TRIM(DepartmentCode), '')     AS departmentcode,
                       NULLIF(TRIM(SubDepartmentCode), '')  AS subdepartmentcode
                FROM Documents
                WHERE Id = @EntityId AND CompanyId = @CompanyId;",
                new { EntityId = (int)entityId, CompanyId = companyId }, tx);
        }

        string? unitCode = normalized switch
        {
            Division => (string?)cabinet?.divisioncode,
            Department => (string?)cabinet?.departmentcode,
            _ => (string?)cabinet?.subdepartmentcode
        };

        if (string.IsNullOrWhiteSpace(unitCode) || unitCode == "0")
            throw new CustomException(
                $"The approval workflow has a \"{Label(normalized)}\" step, but this document is not filed under a {info.Unit}, "
                + $"so there is no {info.Unit.ToLowerInvariant()} head to send it to. Choose a {info.Unit} for the document, "
                + "or ask the administrator to change the approval workflow policy.", 409);

        var (headPeople, unitNameForRules) = await FindHeadsAsync(common, companyId, normalized, unitCode!, tx);
        var heads = headPeople.Select(h => h.EmpCode).ToList();

        if (heads.Count == 0)
        {
            var unitName = string.IsNullOrWhiteSpace(unitNameForRules) ? unitCode : unitNameForRules;

            throw new CustomException(
                $"The approval workflow routes this document to the {info.Role} of {info.Unit} \"{unitName}\", "
                + $"but no active employee is set up as that. An employee needs a role that counts as {info.Unit} Head "
                + $"(see the Head role rules, table WorkflowHeadRoleRules; a rule that does not name the unit also needs a User Access Level on this {info.Unit}), "
                + $"or must be the one person of the {info.Unit} whom the others report to in HR.", 409);
        }

        return heads;
    }
}
