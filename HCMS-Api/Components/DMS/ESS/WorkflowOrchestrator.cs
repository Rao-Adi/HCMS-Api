using Dapper;
using HCMS_Api.Common.DMS;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace HCMS_Api.Components.DMS.ESS;

public interface IWorkflowOrchestrator
{
    Task<long> StartWorkflowAsync(
        string entityType,
        long entityId,
        long workflowPolicyVersionId);

    Task ApproveStepAsync(long executionId, string comments);

    Task RejectStepAsync(long executionId, string comments);

    Task RevertStepAsync(long executionId, string comments);
}


public class WorkflowOrchestrator : IWorkflowOrchestrator
{
    //private readonly string _connectionString;
    //private readonly ITenantProvider _tenant;
    //private readonly IUserProvider _user;
    //public WorkflowOrchestrator(
    //    string connectionString,
    //    ITenantProvider tenant,
    //    IUserProvider user)
    //{
    //    _connectionString = connectionString;
    //    _tenant = tenant;
    //    _user = user;
    //}

    private readonly DMSCommon _common;
    public WorkflowOrchestrator(
        DMSCommon common)
    {
        _common = common;

    }


    //    =====================================================
    //✅ START WORKFLOW
    //=====================================================
    //Called from:

    //👉 SubmitDocumentRequest
    //👉 SubmitDraftForApproval

    public async Task<long> StartWorkflowAsync(
    string entityType,
    long entityId,
    long workflowPolicyVersionId)
    {
        await using var tx = await _common.BeginTransactionAsync();

        var companyId = ""; //_tenant.CompanyId;
        var userId = "";// _user.UserId;

        // Create execution
        var executionId = await _common.ExecuteScalarAsync<long>(
            @"INSERT INTO WorkflowExecutions
          (CompanyId, WorkflowPolicyVersionId, EntityType, EntityId, Status, StartedBy)
          VALUES (@CompanyId,@Policy,@Type,@Entity,'Running',@User)
          RETURNING Id",
            new
            {
                CompanyId = companyId,
                Policy = workflowPolicyVersionId,
                Type = entityType,
                Entity = entityId,
                User = userId
            }, tx);

        // Load step definitions
        var steps = await _common.QuerySingleAsync<string>(
            @"SELECT *
          FROM WorkflowStepDefinitions
          WHERE WorkflowPolicyVersionId=@Policy
          ORDER BY StepOrder",
            new { Policy = workflowPolicyVersionId }, tx);

        foreach (var step in steps)
        {
            await _common.ExecuteAsync(
                @"INSERT INTO WorkflowExecutionSteps
              (CompanyId, WorkflowExecutionId, StepDefinitionId,
               AssignedUserId, AssignedRoleId)
              VALUES
              (@CompanyId,@Exec,@Step,@User,@Role)",
                new
                {
                    CompanyId = companyId,
                    Exec = executionId,
                    //Step = step.id,
                    //User = step.userid,
                    //Role = step.roleid
                }, tx);
        }

        await tx.CommitAsync();
        return executionId;
    }


    //    =====================================================
    //🔥 APPROVE STEP(CORE ENGINE)
    //=====================================================

    public async Task ApproveStepAsync(long executionId, string comments)
    {
        await using var tx = await _common.BeginTransactionAsync();

        var companyId = ""; //_tenant.CompanyId;
        var userId = "";// _user.UserId;

        // Validate assignment
        var step = await _common.ExecuteScalarAsync<string>(
            @"SELECT *
          FROM WorkflowExecutionSteps
          WHERE CompanyId=@CompanyId
          AND WorkflowExecutionId=@Exec
          AND AssignedUserId=@User
          AND Decision IS NULL
          ORDER BY Id
          LIMIT 1",
            new { CompanyId = companyId, Exec = executionId, User = userId }, tx);

        if (step == null)
            throw new Exception("No pending approval.");

        // Approve
        await _common.ExecuteAsync(
            @"UPDATE WorkflowExecutionSteps
          SET Decision='Approved',
              Observation=@Obs,
              ActionAt=NOW()
          WHERE Id=@Id",
            new { Obs = comments, Id = step }, tx);

        // Remaining steps?
        var remaining = await _common.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1)
          FROM WorkflowExecutionSteps
          WHERE WorkflowExecutionId=@Exec
          AND Decision IS NULL",
            new { Exec = executionId }, tx);

        if (remaining == 0)
        {
            await FinalizeWorkflow(tx, executionId);
        }

        await tx.CommitAsync();
    }

    //    =====================================================
    //❌ REJECT STEP
    //=====================================================

    public async Task RejectStepAsync(long executionId, string comments)
    {
        await using var tx = await _common.BeginTransactionAsync();

        var companyId = ""; //_tenant.CompanyId;
        var userId = "";// _user.UserId;

        await _common.ExecuteAsync(
            @"UPDATE WorkflowExecutionSteps
          SET Decision='Rejected',
              Observation=@Obs,
              ActionAt=NOW()
          WHERE WorkflowExecutionId=@Exec
          AND AssignedUserId=@User
          AND Decision IS NULL",
            new { Obs = comments, Exec = executionId, User = userId }, tx);

        await _common.ExecuteAsync(
            @"UPDATE WorkflowExecutions
          SET Status='Cancelled'
          WHERE Id=@Exec",
            new { Exec = executionId }, tx);

        await TransitionAfterRejection(tx, executionId);

        await tx.CommitAsync();
    }

    //    =====================================================
    //🔁 REVERT STEP
    //=====================================================

    public async Task RevertStepAsync(long executionId, string comments)
    {
        await using var tx = await _common.BeginTransactionAsync();

        var companyId = ""; //_tenant.CompanyId;
        var userId = "";// _user.UserId;

        await _common.ExecuteAsync(
            @"UPDATE WorkflowExecutionSteps
          SET Decision='Reverted',
              Observation=@Obs,
              ActionAt=NOW()
          WHERE WorkflowExecutionId=@Exec
          AND AssignedUserId=@User
          AND Decision IS NULL",
            new { Obs = comments, Exec = executionId, User = userId }, tx);

        await TransitionAfterRevert(tx, executionId);

        await tx.CommitAsync();
    }

    //    =====================================================
    //⭐ FINALIZATION LOGIC
    //=====================================================
    //THIS enforces lifecycle transitions automatically.

    private async Task FinalizeWorkflow(
    //NpgsqlConnection _common,
    NpgsqlTransaction tx,
    long executionId)
    {

        //await using var tx = await _common.BeginTransactionAsync();
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;

        var entity = await _common.ExecuteScalarAsync<string>(
            @"SELECT EntityType, EntityId
          FROM WorkflowExecutions
          WHERE Id=@Id",
            new { Id = executionId }, tx);

        await _common.ExecuteAsync(
            @"UPDATE WorkflowExecutions
          SET Status='Completed',
              CompletedAt=NOW()
          WHERE Id=@Id",
            new { Id = executionId }, tx);

        if (entity == "Request")
        {
            await _common.ExecuteAsync(
                @"INSERT INTO RequestStateHistory
              (CompanyId, RequestId, State, ChangedBy)
              VALUES (@CompanyId,@Id,'Approved',@User)",
                new
                {
                    CompanyId = companyId,
                    Id = entity, //entity.entityid,
                    User = userId
                }, tx);
        }
        else
        {
            await _common.ExecuteAsync(
                @"INSERT INTO DocumentStateHistory
              (CompanyId, DocumentId, ToStateId, ChangedBy)
              SELECT @CompanyId,@Id,Id,@User
              FROM DocumentStates
              WHERE Code='Approved'",
                new
                {
                    CompanyId = companyId,
                    Id = entity, //entity.entityid,
                    User = userId
                }, tx);
        }
    }

    //    =====================================================
    //REJECTION / REVERT TRANSITIONS
    //=====================================================

    private async Task TransitionAfterRejection(
    //NpgsqlConnection _common,
    NpgsqlTransaction tx,
    long executionId)
    {
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;

        var entity = await _common.ExecuteScalarAsync<string>(
            @"SELECT EntityType, EntityId
          FROM WorkflowExecutions
          WHERE Id=@Id",
            new { Id = executionId }, tx);

        if (entity == "Request")
        {
            await _common.ExecuteAsync(
                @"INSERT INTO RequestStateHistory
              (CompanyId, RequestId, State, ChangedBy)
              VALUES (@CompanyId,@Id,'Rejected',@User)",
                new
                {
                    CompanyId = companyId,
                    Id = entity, //entity.entityid,
                    User = userId
                }, tx);
        }
    }


    private async Task TransitionAfterRevert(
    //NpgsqlConnection conn,
    NpgsqlTransaction tx,
    long executionId)
    {
         
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;

        //------------------------------------------------
        // Identify Entity
        //------------------------------------------------

        var entity = await _common.ExecuteScalarAsync<string>(
            @"SELECT EntityType, EntityId
          FROM WorkflowExecutions
          WHERE CompanyId=@CompanyId
          AND Id=@Exec",
            new { CompanyId = companyId, Exec = executionId }, tx);

        //------------------------------------------------
        // Cancel Workflow
        //------------------------------------------------

        await _common.ExecuteScalarAsync<string>(
            @"UPDATE WorkflowExecutions
          SET Status='Cancelled',
              CompletedAt=NOW()
          WHERE CompanyId=@CompanyId
          AND Id=@Exec",
            new { CompanyId = companyId, Exec = executionId }, tx);

        //------------------------------------------------
        // REQUEST REVERT
        //------------------------------------------------

        if (entity == "Request")
        {
            await _common.ExecuteAsync(
                @"INSERT INTO RequestStateHistory
              (CompanyId, RequestId, State, ChangedBy, ChangedAt)
              VALUES
              (@CompanyId, @RequestId, 'Reverted', @User, NOW())",
                new
                {
                    CompanyId = companyId,
                    RequestId = entity, //entity.entityid,
                    User = userId
                }, tx);

            return;
        }

        //------------------------------------------------
        // DOCUMENT REVERT (Back to Draft)
        //------------------------------------------------

        if (entity == "Document")
        {
            //------------------------------------------------
            // Get Draft StateId
            //------------------------------------------------

            var draftStateId = await _common.ExecuteScalarAsync<int>(
                @"SELECT Id
              FROM DocumentStates
              WHERE Code='Draft'
              LIMIT 1", tx);

            //------------------------------------------------
            // Insert State History
            //------------------------------------------------

            await _common.ExecuteAsync(
                @"INSERT INTO DocumentStateHistory
              (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, ChangedAt)
              VALUES
              (
                @CompanyId,
                @DocumentId,
                (
                    SELECT ToStateId
                    FROM DocumentStateHistory
                    WHERE DocumentId=@DocumentId
                    ORDER BY Id DESC
                    LIMIT 1
                ),
                @DraftState,
                @User,
                NOW()
              )",
                new
                {
                    CompanyId = companyId,
                    DocumentId = entity, //entity.entityid,
                    DraftState = draftStateId,
                    User = userId
                }, tx);
        }
    }


}
