using Dapper;
using HCMS_Api.Common.DMS;
using HCMS_Api.Components.DMS.Common.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentGovernanceComponent
{
    //private readonly string _connectionString;
    //private readonly ITenantProvider _tenant;
    //private readonly IUserProvider _user;
    private readonly DMSCommon _common;
    public DocumentGovernanceComponent(
        DMSCommon common)
    {
        _common = common;
    }

    //    =========================================================
    //✅ 1. SUBMIT DOCUMENT REQUEST
    //=========================================================
    public async Task<long> SubmitDocumentRequestAsync(DocumentRequestCreateDto dto)
    {
        await using var tx = await _common.BeginTransactionAsync();
        //await using var _common = await GetOpenConnection();
        //await using var tx = await _common.BeginTransactionAsync();

        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; //_user.UserId;

        // Resolve workflow
        var workflowVersionId = await _common.ExecuteScalarAsync<long>(
            @"SELECT wpv.Id
          FROM WorkflowPolicyVersions wpv
          JOIN WorkflowPolicies wp ON wp.Id = wpv.WorkflowPolicyId
          WHERE wp.CompanyId=@CompanyId
          AND wp.DocumentTypeId=@DocType
          AND wpv.IsActive=TRUE
          LIMIT 1",
            new { CompanyId = companyId, DocType = dto.DocumentTypeId }, tx);

        if (workflowVersionId == 0)
            throw new Exception("No active workflow found.");

        // Insert request
        var requestId = await _common.ExecuteScalarAsync<long>(
            @"INSERT INTO DocumentRequests
          (CompanyId, DocumentTypeId, RequestType, DocumentName, Justification, CreatedBy)
          VALUES (@CompanyId,@DocType,@Type,@DocumentName,@Just,@User)
          RETURNING Id",
            new
            {
                CompanyId = companyId,
                DocType = dto.DocumentTypeId,
                Type = dto.RequestType,
                DocumentName = dto.DocumentName,
                Just = dto.Justification,
                User = userId
            }, tx);

        // State
        await _common.ExecuteAsync(
            @"INSERT INTO RequestStateHistory
          (CompanyId, RequestId, State, ChangedBy)
          VALUES (@CompanyId,@Request,'Submitted',@User)",
            new { CompanyId = companyId, Request = requestId, User = userId }, tx);

        // Workflow execution
        await _common.ExecuteAsync(
            @"INSERT INTO WorkflowExecutions
          (CompanyId, WorkflowPolicyVersionId, EntityType, EntityId, Status, StartedBy)
          VALUES (@CompanyId,@Workflow,'Request',@Id,'Running',@User)",
            new
            {
                CompanyId = companyId,
                Workflow = workflowVersionId,
                Id = requestId,
                User = userId
            }, tx);

        await tx.CommitAsync();

        return requestId;
    }


    //    =========================================================
    //✅ 2. APPROVE WORKFLOW STEP(CORE ENGINE)
    //=========================================================

    public async Task ApproveWorkflowStepAsync(long executionId, string comments)
    {
        //await using var _common = await GetOpenConnection();
        //await using var tx = await _common.BeginTransactionAsync();
        await using var tx = await _common.BeginTransactionAsync();
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;

        var step = await _common.QueryFirstOrDefaultAsync<int>(
            @"SELECT *
          FROM WorkflowExecutionSteps
          WHERE CompanyId=@CompanyId
          AND WorkflowExecutionId=@Exec
          AND AssignedUserId=@User
          AND Decision IS NULL
          LIMIT 1",
            new { CompanyId = companyId, Exec = executionId, User = userId }, tx);

        if (step == null)
            throw new Exception("No pending step.");

        await _common.ExecuteAsync(
            @"UPDATE WorkflowExecutionSteps
          SET Decision='Approved',
              Observation=@Obs,
              ActionAt=NOW()
          WHERE Id=@Id",
            new { Obs = comments, Id = step }, tx);

        // check remaining steps
        var remaining = await _common.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1)
          FROM WorkflowExecutionSteps
          WHERE WorkflowExecutionId=@Exec
          AND Decision IS NULL",
            new { Exec = executionId }, tx);

        if (remaining == 0)
        {
            await _common.ExecuteAsync(
                @"UPDATE WorkflowExecutions
              SET Status='Completed',
                  CompletedAt=NOW()
              WHERE Id=@Exec",
                new { Exec = executionId }, tx);

            await TransitionEntityAfterWorkflow(tx, executionId);
        }

        await tx.CommitAsync();
    }

    //    =========================================================
    //🔥 TRANSITION HELPER(VERY IMPORTANT)
    //=========================================================

    private async Task TransitionEntityAfterWorkflow(
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
                    User = userId //_user.UserId
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
                    User = userId //_user.UserId
                }, tx);
        }
    }


    //    =========================================================
    //✅ 3. CREATE DRAFT FROM APPROVED REQUEST
    //=========================================================

    public async Task<long> CreateDraftFromApprovedRequestAsync(long requestId, string title)
    {
        //await using var _common = await GetOpenConnection();
        //await using var tx = await _common.BeginTransactionAsync();

        //var companyId = _tenant.CompanyId;
        //var userId = _user.UserId;
        await using var tx = await _common.BeginTransactionAsync();
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;


        var approved = await _common.ExecuteScalarAsync<bool>(
            @"SELECT EXISTS(
            SELECT 1 FROM RequestStateHistory
            WHERE RequestId=@Id
            AND State='Approved')",
            new { Id = requestId }, tx);

        if (!approved)
            throw new Exception("Request not approved.");

        var docNumber = await _common.ExecuteScalarAsync<string>(
            "SELECT generate_document_number(@CompanyId)",
            new { CompanyId = companyId }, tx);

        var docId = await _common.ExecuteScalarAsync<long>(
            @"INSERT INTO Documents
          (CompanyId, DocumentNumber, Title, CreatedBy)
          VALUES (@CompanyId,@Num,@Title,@User)
          RETURNING Id",
            new
            {
                CompanyId = companyId,
                Num = docNumber,
                Title = title,
                User = userId
            }, tx);

        await _common.ExecuteAsync(
            @"INSERT INTO DocumentVersions
          (CompanyId, DocumentId, Version, CreatedBy)
          VALUES (@CompanyId,@Doc,'1.0',@User)",
            new { CompanyId = companyId, Doc = docId, User = userId }, tx);

        await _common.ExecuteAsync(
            @"INSERT INTO DocumentStateHistory
          (CompanyId, DocumentId, ToStateId, ChangedBy)
          SELECT @CompanyId,@Doc,Id,@User
          FROM DocumentStates WHERE Code='Draft'",
            new { CompanyId = companyId, Doc = docId, User = userId }, tx);

        await tx.CommitAsync();
        return docId;
    }

    //    =========================================================
    //✅ 4. SUBMIT DRAFT FOR APPROVAL
    //=========================================================


    public async Task SubmitDraftForApprovalAsync(long documentId)
    {
        //await using var _common = await GetOpenConnection();
        //await using var tx = await _common.BeginTransactionAsync();

        //var companyId = _tenant.CompanyId;
        //var userId = _user.UserId;

        await using var tx = await _common.BeginTransactionAsync();
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;

        await _common.ExecuteAsync(
            @"INSERT INTO DocumentStateHistory
          (CompanyId, DocumentId, ToStateId, ChangedBy)
          SELECT @CompanyId,@Doc,Id,@User
          FROM DocumentStates
          WHERE Code='PendingApproval'",
            new { CompanyId = companyId, Doc = documentId, User = userId }, tx);

        await tx.CommitAsync();
    }


    //    =========================================================
    //✅ 5. UPLOAD TRAINING PROOF
    //=========================================================

    public async Task UploadTrainingProofAsync(long documentId, string proofUrl)
    {
        //await using var _common = await GetOpenConnection();
        //await using var tx = await _common.BeginTransactionAsync();
        await using var tx = await _common.BeginTransactionAsync();
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;
        await _common.ExecuteAsync(
            @"INSERT INTO DocumentTraining
          (CompanyId, DocumentId, ProofUrl, UploadedBy)
          VALUES (@CompanyId,@Doc,@Url,@User)",
            new
            {
                CompanyId = companyId,
                Doc = documentId,
                Url = proofUrl,
                User = userId
            }, tx);

        await _common.ExecuteAsync(
            @"INSERT INTO DocumentStateHistory
          (CompanyId, DocumentId, ToStateId, ChangedBy)
          SELECT @CompanyId,@Doc,Id,@User
          FROM DocumentStates
          WHERE Code='AuthorizationPending'",
            new
            {
                CompanyId = companyId,
                Doc = documentId,
                User = userId
            }, tx);

        await tx.CommitAsync();
    }


    //    =========================================================
    //✅ 6. AUTHORIZE DOCUMENT(LEGAL ACTIVATION)
    //=========================================================

    public async Task AuthorizeDocumentAsync(long documentId)
    {
        //await using var _common = await GetOpenConnection();
        //await using var tx = await _common.BeginTransactionAsync();

        //var companyId = _tenant.CompanyId;
        //var userId = _user.UserId;

        await using var tx = await _common.BeginTransactionAsync();
        var companyId = ""; //_tenant.CompanyId;
        var userId = ""; // _user.UserId;

        await _common.ExecuteAsync(
            @"UPDATE Documents
          SET EffectiveDate=NOW()
          WHERE Id=@Doc AND CompanyId=@CompanyId",
            new { Doc = documentId, CompanyId = companyId }, tx);

        await _common.ExecuteAsync(
            @"INSERT INTO DocumentStateHistory
          (CompanyId, DocumentId, ToStateId, ChangedBy)
          SELECT @CompanyId,@Doc,Id,@User
          FROM DocumentStates
          WHERE Code='Effective'",
            new { CompanyId = companyId, Doc = documentId, User = userId }, tx);

        await tx.CommitAsync();
    }

    //    =========================================================
    //✅ 7. INITIATE REVISION
    //=========================================================

    public async Task<long> InitiateRevisionRequestAsync(long documentId, string reason)
    {
        return await SubmitDocumentRequestAsync(new DocumentRequestCreateDto
        {
            DocumentTypeId = 1, // derive from doc
            RequestType = "Revision",
            DocumentName = $"Revision for Doc {documentId}",
            Justification = reason
        });
    }

    //    =========================================================
    //✅ 8. INITIATE OBSOLETION
    //=========================================================

    public async Task<long> InitiateObsoletionRequestAsync(long documentId, string reason)
    {
        return await SubmitDocumentRequestAsync(new DocumentRequestCreateDto
        {
            DocumentTypeId = 1,
            RequestType = "Obsoletion",
            DocumentName = $"Obsolete Doc {documentId}",
            Justification = reason
        });
    }


}
