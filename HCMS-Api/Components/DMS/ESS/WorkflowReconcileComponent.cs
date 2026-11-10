using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using Npgsql;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

/// <summary>
/// Makes workflows that are ALREADY RUNNING follow the Approval Workflow Policy as it is now.
///
/// A request / document gets a copy of its policy's steps when it is submitted. Without this, changing the policy afterwards (removing an
/// approver, adding one, moving someone up or down) changed nothing for what was already out for approval -- a document still went to an
/// approver who had since been taken out of the policy. Now, whenever a policy's steps are edited, every running workflow of that policy is
/// brought in line:
///   * people who have ALREADY approved stay exactly where they are (that is history, and is never rewritten);
///   * everything still waiting is rebuilt from the policy's new list, in the new order -- someone taken out loses their pending step, someone
///     added gets one, and the first person left in the list is the one the item is with (they are told);
///   * ad-hoc approvers added by an approver for this one document are kept (after the step they were added after);
///   * if nobody is left to approve, the workflow completes as approved, exactly as if the last approver had approved.
/// A policy with no steps at all is never applied (that would approve everything waiting on it).
/// </summary>
public class WorkflowReconcileComponent
{
    private readonly DMSCommon _common;
    private readonly DMSUtilities _utilities;
    private readonly ClientContextService _clientContextService;
    private readonly NotificationComponent _notificationComponent;
    private readonly DocumentComponent _documentComponent;
    private readonly DocumentRequestComponent _documentRequestComponent;
    private readonly AuditLogComponent _auditLogComponent;
    private readonly ILogger<WorkflowReconcileComponent> _logger;

    public WorkflowReconcileComponent(
        DMSCommon common,
        DMSUtilities utilities,
        ClientContextService clientContextService,
        NotificationComponent notificationComponent,
        DocumentComponent documentComponent,
        DocumentRequestComponent documentRequestComponent,
        AuditLogComponent auditLogComponent,
        ILogger<WorkflowReconcileComponent> logger)
    {
        _common = common;
        _utilities = utilities;
        _clientContextService = clientContextService;
        _notificationComponent = notificationComponent;
        _documentComponent = documentComponent;
        _documentRequestComponent = documentRequestComponent;
        _auditLogComponent = auditLogComponent;
        _logger = logger;
    }

    private static string Z(string? code) => (code ?? "").Trim().TrimStart('0');

    private sealed class Entry
    {
        public string Code = "";
        public dynamic? Existing;        // the waiting step this person already has on the running workflow
        public int DefinitionId;         // policy step the person comes from (new steps only)
        public bool CanAddAdHoc;
        public bool IsAdHoc;
        public long RowId;               // id of the step row once it exists
    }

    /// <summary>The policy a workflow step definition belongs to (0 when unknown) -- looked up before the step is removed or changed.</summary>
    public async Task<int> GetPolicyIdOfStepAsync(string stepId)
    {
        if (!int.TryParse(stepId, out var id)) return 0;
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        return await _common.ExecuteScalarAsync<int?>(@"
            SELECT v.WorkflowPolicyId FROM WorkflowStepDefinitions s
            JOIN WorkflowPolicyVersions v ON v.Id = s.WorkflowPolicyVersionId
            WHERE s.Id = @Id AND s.CompanyId = @CompanyId;",
            new { Id = id, CompanyId = int.Parse(_CompanyId) }) ?? 0;
    }

    /// <summary>
    /// Brings every running workflow of the policy in line with its steps as they are now. Never throws: the policy edit has already been
    /// saved, and a problem here must not turn it into an error -- it is logged instead. Returns how many workflows were changed.
    /// </summary>
    public async Task<int> ReconcilePolicyAsync(int policyId)
    {
        if (policyId <= 0) return 0;
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int companyId = int.Parse(_CompanyId);
            var actor = _utilities.GetEmpCodeForHCMS(_utilities.GetEmpid(_clientContextService.GetClientIP()).ToString());

            var activeVersionId = await _common.ExecuteScalarAsync<int?>(@"
                SELECT Id FROM WorkflowPolicyVersions
                WHERE CompanyId = @CompanyId AND WorkflowPolicyId = @PolicyId AND IsActive = TRUE
                ORDER BY Id DESC LIMIT 1;",
                new { CompanyId = companyId, PolicyId = policyId });

            if (activeVersionId == null) return 0;

            var liveSteps = await _common.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM WorkflowStepDefinitions
                WHERE CompanyId = @CompanyId AND WorkflowPolicyVersionId = @VersionId AND IsActive = TRUE AND IsDeleted = FALSE;",
                new { CompanyId = companyId, VersionId = activeVersionId });

            if (liveSteps == 0)
            {
                _logger.LogWarning("Workflow policy {PolicyId} has no steps left; running workflows were not changed.", policyId);
                return 0;
            }

            var running = (await _common.QueryAsync<dynamic>(@"
                SELECT we.Id, we.EntityType, we.EntityId
                FROM WorkflowExecutions we
                JOIN WorkflowPolicyVersions v ON v.Id = we.WorkflowPolicyVersionId
                WHERE we.CompanyId = @CompanyId AND v.WorkflowPolicyId = @PolicyId
                  AND we.Status = 'Running' AND we.EntityType IN ('Request', 'Document')
                ORDER BY we.Id;",
                new { CompanyId = companyId, PolicyId = policyId })).ToList();

            int changed = 0;
            foreach (var we in running)
            {
                try
                {
                    changed += await ReconcileExecutionAsync(companyId, (int)we.id, (string)we.entitytype, (int)we.entityid, activeVersionId.Value, actor);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not bring running workflow {ExecutionId} in line with policy {PolicyId}.", (int)we.id, policyId);
                }
            }
            return changed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not bring running workflows in line with policy {PolicyId}.", policyId);
            return 0;
        }
    }

    private async Task<int> ReconcileExecutionAsync(
        int companyId, int executionId, string entityType, int entityId, int versionId, string actor)
    {
        await using var tx = await _common.BeginTransactionAsync();

        // Locked for this change: an approver acting at the same moment is taken before or after it, never in the middle.
        var status = await _common.ExecuteScalarAsync<string>(
            "SELECT Status FROM WorkflowExecutions WHERE Id = @Id AND CompanyId = @CompanyId FOR UPDATE;",
            new { Id = executionId, CompanyId = companyId }, tx);
        if (!string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase)) return 0;

        var rows = (await _common.QueryAsync<dynamic>(@"
            SELECT Id, StepOrder, TRIM(AssignedUserId) AS code, NULLIF(TRIM(Decision), '') AS decision, COALESCE(IsActive, FALSE) AS isactive,
                   COALESCE(IsAdHoc, FALSE) AS isadhoc
            FROM WorkflowExecutionSteps
            WHERE WorkflowExecutionId = @Id AND CompanyId = @CompanyId
            ORDER BY StepOrder, Id;",
            new { Id = executionId, CompanyId = companyId }, tx)).ToList();

        var decided = rows.Where(r => r.decision != null).ToList();
        var undecided = rows.Where(r => r.decision == null).ToList();
        var decidedCodes = new HashSet<string>(decided.Select(r => Z((string?)r.code)));

        // ---- the policy's list as it is now, expanded to people the way a submit does ----
        var defs = (await _common.QueryAsync<dynamic>(@"
            SELECT Id, UserId, RoleId, DesignationId, HeadScope, COALESCE(CanAddAdHocApprover, FALSE) AS canadhoc
            FROM WorkflowStepDefinitions
            WHERE WorkflowPolicyVersionId = @VersionId AND CompanyId = @CompanyId AND IsActive = TRUE AND IsDeleted = FALSE
            ORDER BY StepOrder, Id;",
            new { VersionId = versionId, CompanyId = companyId }, tx)).ToList();

        var policyPeople = new List<(string Code, int DefId, bool CanAddAdHoc)>();
        foreach (var d in defs)
        {
            var codes = new List<string>();
            if (d.userid != null)
            {
                // Separated (HCMS status 0): cannot approve any more, so not part of the chain (a running document moves on to the next approver).
                if (!await EmployeeStatusHelper.IsSeparatedAsync(_common, companyId, ((string)d.userid).Trim(), tx))
                    codes.Add(((string)d.userid).Trim());
            }
            else if (d.roleid != null || d.designationid != null)
            {
                codes.AddRange(await _common.QueryAsync<string>(@"
                    SELECT TRIM(e.empcode)
                    FROM public.tblempjobprofile ejp
                    INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                    WHERE e.CompanyId = @CompanyId
                      AND COALESCE(e.Active, 1) IN (1, 2)
                      AND COALESCE(ejp.Active, TRUE) = TRUE
                      AND ((@RoleId::int IS NOT NULL AND ejp.roleid = @RoleId::int) OR (@DesignationId::int IS NOT NULL AND ejp.dsgid = @DesignationId::int))
                    ORDER BY e.empid ASC;",
                    new { CompanyId = companyId, RoleId = (int?)d.roleid, DesignationId = (int?)d.designationid }, tx));
            }
            else if (d.headscope != null)
            {
                try
                {
                    codes.AddRange(await WorkflowHeadResolver.ResolveAsync(
                        _common, companyId, (string)d.headscope, entityType, entityId, tx));
                }
                catch (CustomException)
                {
                    // This item's cabinet has no such unit / nobody is set up as that head: nobody can be added for the step.
                }
            }

            foreach (var code in codes)
            {
                // A standing Responsibility Transfer hands the step to the person it was transferred to, as it does on a submit.
                var transferTo = await _common.ExecuteScalarAsync<string>(@"
                    SELECT EmployeeTo FROM ResponsibilityTransfers
                    WHERE EmployeeFrom = @EmpFrom AND CompanyId = @CompanyId AND Status = 2
                      AND EffectiveDateFrom <= CURRENT_DATE AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE)
                    ORDER BY Id DESC LIMIT 1;",
                    new { EmpFrom = code, CompanyId = companyId }, tx);
                policyPeople.Add((string.IsNullOrEmpty(transferTo) ? code : transferTo, (int)d.id, (bool)d.canadhoc));
            }
        }

        // A policy that yields nobody at all (every role empty, ...) is a configuration problem, not "everyone approved": leave it alone.
        if (policyPeople.Count == 0) return 0;

        // ---- who should still be waiting, in order ----
        var wanted = new List<Entry>();
        foreach (var p in policyPeople)
        {
            var key = Z(p.Code);
            if (key.Length == 0 || decidedCodes.Contains(key) || wanted.Any(w => Z(w.Code) == key)) continue;
            wanted.Add(new Entry
            {
                Code = p.Code,
                Existing = undecided.FirstOrDefault(r => !(bool)r.isadhoc && Z((string?)r.code) == key),
                DefinitionId = p.DefId,
                CanAddAdHoc = p.CanAddAdHoc
            });
        }

        // Ad-hoc approvers added for this one document are not in the policy: they stay, right after the step they were added after.
        foreach (var adHoc in undecided.Where(r => (bool)r.isadhoc))
        {
            var key = Z((string?)adHoc.code);
            if (decidedCodes.Contains(key) || wanted.Any(w => Z(w.Code) == key)) continue;

            var anchor = rows.TakeWhile(r => (long)r.id != (long)adHoc.id).LastOrDefault(r => !(bool)r.isadhoc);
            var anchorKey = Z((string?)anchor?.code);
            int at;
            var anchorAt = wanted.FindIndex(w => !w.IsAdHoc && Z(w.Code) == anchorKey);
            if (anchorAt >= 0) at = anchorAt + 1;
            else if (anchor != null && decidedCodes.Contains(anchorKey)) at = 0;
            else at = wanted.Count;
            while (at < wanted.Count && at > 0 && wanted[at].IsAdHoc) at++;   // keep several ad-hoc approvers in the order they were added
            wanted.Insert(at, new Entry { Code = (string)adHoc.code, Existing = adHoc, IsAdHoc = true });
        }

        // ---- nothing to do? ----
        var currentOrder = undecided.Select(r => Z((string?)r.code)).ToList();
        var wantedOrder = wanted.Select(w => Z(w.Code)).ToList();
        var removed = undecided.Where(r => !wanted.Any(w => w.Existing != null && (long)w.Existing.id == (long)r.id)).ToList();
        var added = wanted.Where(w => w.Existing == null).ToList();
        var activeNow = undecided.FirstOrDefault(r => (bool)r.isactive);
        bool activeIsFirst = wanted.Count > 0 && activeNow != null && wanted[0].Existing != null && (long)wanted[0].Existing.id == (long)activeNow.id;

        if (removed.Count == 0 && added.Count == 0 && currentOrder.SequenceEqual(wantedOrder) && (wanted.Count == 0 || activeIsFirst))
            return 0;

        // ---- apply ----
        if (removed.Count > 0)
            await _common.ExecuteAsync("DELETE FROM WorkflowExecutionSteps WHERE Id = ANY(@Ids);",
                new { Ids = removed.Select(r => (long)r.id).ToArray() }, tx);

        foreach (var w in wanted)
        {
            if (w.Existing != null) { w.RowId = (long)w.Existing.id; continue; }
            w.RowId = await _common.ExecuteScalarAsync<long>(@"
                INSERT INTO WorkflowExecutionSteps
                    (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive, CanAddAdHocApprover)
                VALUES (@CompanyId, @ExecutionId, @DefId, @UserId, NULL, NULL, 0, '', FALSE, @CanAddAdHoc)
                RETURNING Id;",
                new { CompanyId = companyId, ExecutionId = executionId, DefId = w.DefinitionId, UserId = w.Code, w.CanAddAdHoc }, tx);
        }

        // Steps already decided keep their order; the rest follow them in the policy's order.
        int order = 1;
        foreach (var d in decided)
            await _common.ExecuteAsync("UPDATE WorkflowExecutionSteps SET StepOrder = @Order WHERE Id = @Id;", new { Order = order++, Id = (long)d.id }, tx);
        foreach (var w in wanted)
            await _common.ExecuteAsync("UPDATE WorkflowExecutionSteps SET StepOrder = @Order WHERE Id = @Id;", new { Order = order++, Id = w.RowId }, tx);

        // Who the item is with now: the first of those still waiting.
        await _common.ExecuteAsync(
            "UPDATE WorkflowExecutionSteps SET IsActive = FALSE WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId AND Decision IS NULL;",
            new { ExecutionId = executionId, CompanyId = companyId }, tx);

        string? newlyActive = null;
        bool completed = false;
        if (wanted.Count > 0)
        {
            await _common.ExecuteAsync("UPDATE WorkflowExecutionSteps SET IsActive = TRUE WHERE Id = @Id;", new { Id = wanted[0].RowId }, tx);
            if (activeNow == null || (long)activeNow.id != wanted[0].RowId) newlyActive = wanted[0].Code;
        }
        else
        {
            // Everybody still waiting was taken out of the policy: nothing is left to approve, so the workflow completes as approved.
            completed = true;
            await _common.ExecuteAsync(
                "UPDATE WorkflowExecutions SET Status = 'Completed', CompletedAt = NOW() WHERE Id = @ExecutionId AND CompanyId = @CompanyId;",
                new { ExecutionId = executionId, CompanyId = companyId }, tx);

            if (string.Equals(entityType, "Request", StringComparison.OrdinalIgnoreCase))
            {
                await _common.ExecuteAsync(@"
                    UPDATE DocumentRequests SET Status = @Approved, LastModifiedBy = @Actor, LastModifiedAt = NOW()
                    WHERE Id = @RequestId AND CompanyId = @CompanyId;",
                    new { Approved = DocumentRequestStatus.Approved, Actor = actor, RequestId = entityId, CompanyId = companyId }, tx);
                await _documentRequestComponent.CreateDocumentFromApprovedRequestAsync(companyId, entityId, actor, tx);
            }
            else
            {
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, WorkflowExecutionId, ChangedBy)
                    VALUES (@CompanyId, @DocumentId, (SELECT Id FROM DocumentStates WHERE Code = 'PENDING_APPROVAL'),
                            (SELECT Id FROM DocumentStates WHERE Code = 'APPROVED'), @ExecutionId, @Actor);",
                    new { CompanyId = companyId, DocumentId = entityId, ExecutionId = executionId, Actor = actor }, tx);
                await _documentComponent.HandlePostApprovalAsync(companyId, entityId, actor, tx);
            }
        }

        var removedNames = string.Join(", ", removed.Select(r => (string?)r.code));
        var addedNames = string.Join(", ", added.Select(w => w.Code));
        await _auditLogComponent.LogActionAsync(companyId, actor,
            "Approval workflow updated to follow the policy (" + (removed.Count > 0 ? "removed: " + removedNames : "") +
            (removed.Count > 0 && added.Count > 0 ? "; " : "") + (added.Count > 0 ? "added: " + addedNames : "") +
            (removed.Count == 0 && added.Count == 0 ? "order changed" : "") + (completed ? "; nobody left, completed" : "") + ")",
            string.Equals(entityType, "Request", StringComparison.OrdinalIgnoreCase) ? "DocumentRequest" : "Document",
            entityId, _clientContextService.GetRequestIpAddress(), transaction: tx);

        await tx.CommitAsync();

        // ---- tell people (after the commit; a mail problem never undoes the change) ----
        try
        {
            if (newlyActive != null) await NotifyNewApproverAsync(companyId, entityType, entityId, newlyActive);
            if (completed) await NotifyInitiatorCompletedAsync(companyId, entityType, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow {ExecutionId} was updated, but the notification could not be sent.", executionId);
        }

        return 1;
    }

    private async Task NotifyNewApproverAsync(int companyId, string entityType, int entityId, string approver)
    {
        if (string.Equals(entityType, "Request", StringComparison.OrdinalIgnoreCase))
        {
            var req = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT dr.RequestNumber,
                       (SELECT LTRIM(RTRIM(COALESCE(e.firstname,'') || ' ' || COALESCE(e.midname,'') || ' ' || COALESCE(e.lastname,'')))
                          FROM tblEmployee e WHERE LTRIM(RTRIM(e.empcode::text), '0') = LTRIM(RTRIM(dr.CreatedBy::text), '0') AND e.CompanyId = dr.CompanyId LIMIT 1) AS creator
                FROM DocumentRequests dr WHERE dr.Id = @Id AND dr.CompanyId = @CompanyId;",
                new { Id = entityId, CompanyId = companyId });
            var placeholders = new Dictionary<string, string>
            {
                { "ID", Convert.ToString(req?.requestnumber) ?? entityId.ToString() },
                { "Employee Name", Convert.ToString(req?.creator) ?? "" }
            };
            await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, companyId, entityId, approver, placeholders);
        }
        else
        {
            var doc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.Title,
                       (SELECT dv.Version FROM DocumentVersions dv WHERE dv.DocumentId = d.Id AND COALESCE(dv.IsDeleted, FALSE) = FALSE ORDER BY dv.Id DESC LIMIT 1) AS version
                FROM Documents d WHERE d.Id = @Id AND d.CompanyId = @CompanyId;",
                new { Id = entityId, CompanyId = companyId });
            var placeholders = new Dictionary<string, string>
            {
                { "Doc Name", Convert.ToString(doc?.title) ?? "Unknown" },
                { "V#", Convert.ToString(doc?.version) ?? "1.0" }
            };
            await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingDocumentApproval, companyId, entityId, approver, placeholders);
        }
    }

    private async Task NotifyInitiatorCompletedAsync(int companyId, string entityType, int entityId)
    {
        const string by = "the updated approval workflow";
        if (string.Equals(entityType, "Request", StringComparison.OrdinalIgnoreCase))
        {
            var req = await _common.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT RequestNumber, CreatedBy FROM DocumentRequests WHERE Id = @Id AND CompanyId = @CompanyId;",
                new { Id = entityId, CompanyId = companyId });
            if (req?.createdby == null) return;
            await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestApproved, companyId, entityId, (string)req.createdby,
                new Dictionary<string, string> { { "ID", Convert.ToString(req.requestnumber) ?? entityId.ToString() }, { "Approver", by } });
        }
        else
        {
            var doc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.Title, d.CreatedBy,
                       (SELECT dv.Version FROM DocumentVersions dv WHERE dv.DocumentId = d.Id AND COALESCE(dv.IsDeleted, FALSE) = FALSE ORDER BY dv.Id DESC LIMIT 1) AS version
                FROM Documents d WHERE d.Id = @Id AND d.CompanyId = @CompanyId;",
                new { Id = entityId, CompanyId = companyId });
            if (doc?.createdby == null) return;
            await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentApproved, companyId, entityId, (string)doc.createdby,
                new Dictionary<string, string>
                {
                    { "Doc Name", Convert.ToString(doc.title) ?? "Unknown" }, { "V#", Convert.ToString(doc.version) ?? "1.0" }, { "Approver", by }
                });
        }
    }
}
