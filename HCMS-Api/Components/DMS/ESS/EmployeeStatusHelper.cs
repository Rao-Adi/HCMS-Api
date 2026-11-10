using HCMS_Api.Common.DMS;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// HCMS employee status (tblEmployee.Active): 1 = Active (Not Resigned), 2 = Active (Resigned but not Separated, still working the notice
/// period), 0 = Separated. A separated employee can no longer approve anything, so an approval workflow must not wait on them: their
/// step is skipped and the document goes on to the next approver. Everything they already did (decisions, observations, history, the
/// documents and requests they created) stays exactly as it is -- only steps that have NOT been decided yet are ever dropped.
/// An employee code with no employee record at all is not treated as separated (nothing is known about it).
/// </summary>
public static class EmployeeStatusHelper
{
    // wes.AssignedUserId belongs to an employee of this company who is separated and has no current (1 or 2) record
    private const string SeparatedAssigneeSql = @"
        EXISTS (SELECT 1 FROM public.tblEmployee e
                WHERE e.CompanyId = @CompanyId
                  AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(wes.AssignedUserId::text), '0')
                  AND e.Active = 0)
        AND NOT EXISTS (SELECT 1 FROM public.tblEmployee e
                WHERE e.CompanyId = @CompanyId
                  AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(wes.AssignedUserId::text), '0')
                  AND COALESCE(e.Active, 1) IN (1, 2))";

    /// <summary>True when the employee is separated (status 0) and has no current record.</summary>
    public static async Task<bool> IsSeparatedAsync(DMSCommon common, int companyId, string? empCode, IDbTransaction? tx)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return false;
        var n = await common.ExecuteScalarAsync<long?>(@"
            SELECT COUNT(*) FROM (SELECT 1 AS x) q
            WHERE EXISTS (SELECT 1 FROM public.tblEmployee e
                          WHERE e.CompanyId = @CompanyId AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(@Code), '0') AND e.Active = 0)
              AND NOT EXISTS (SELECT 1 FROM public.tblEmployee e
                          WHERE e.CompanyId = @CompanyId AND LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(@Code), '0') AND COALESCE(e.Active, 1) IN (1, 2));",
            new { CompanyId = companyId, Code = empCode.Trim() }, tx);
        return (n ?? 0) > 0;
    }

    /// <summary>
    /// Drops the steps of an execution after <paramref name="afterStepOrder"/> that are still waiting (no decision, not the active step) and
    /// belong to a separated employee, so the workflow moves on to the next approver instead of stopping at someone who has left.
    /// Returns how many were dropped.
    /// </summary>
    public static async Task<int> RemoveSeparatedWaitingStepsAsync(
        DMSCommon common, int companyId, long executionId, int afterStepOrder, IDbTransaction? tx)
    {
        return await common.ExecuteAsync($@"
            DELETE FROM WorkflowExecutionSteps wes
            WHERE wes.CompanyId = @CompanyId AND wes.WorkflowExecutionId = @ExecutionId
              AND wes.StepOrder > @After AND wes.Decision IS NULL AND wes.IsActive = FALSE
              AND {SeparatedAssigneeSql};",
            new { CompanyId = companyId, ExecutionId = executionId, After = afterStepOrder }, tx);
    }
}
