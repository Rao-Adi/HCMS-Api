using Dapper;
using HCMS_Api.Common.DMS;
using HCMS_Api.Components.DMS.Common.Models;
using System.ComponentModel.Design;
using System.Data;
using System.Data.Entity.Infrastructure;

namespace HCMS_Api.Components.DMS.ESS;

public interface IDocumentRequestRepository
{
    Task<long> CreateAsync(DocumentRequests request, IDbTransaction tx);
    Task AddAttachmentAsync(DocumentRequestAttachments attachment, IDbTransaction tx);
    Task<DocumentRequests?> GetByIdAsync(long id);
}


public class DocumentRequestRepository : IDocumentRequestRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly DMSCommon _common;
    public DocumentRequestRepository(IDbConnectionFactory factory,
        DMSCommon common)
    {
        _factory = factory;
        _common = common;
    }

    public async Task<long> CreateAsync(DocumentRequests request, IDbTransaction tx2)
    {
        await using var tx = await _common.BeginTransactionAsync();

        const string sql = @"
            INSERT INTO DocumentRequest
            (RequestNumber, RequestType, Title, DocumentTypeId,
             DivisionId, DepartmentId, SubDepartmentId,
             ReasonForRequest, BusinessJustification,
             Priority, StatusId, RequestedBy)
             
            VALUES
            (@RequestNumber, @RequestType, @Title, @DocumentTypeId,
             @DivisionId, @DepartmentId, @SubDepartmentId,
             @ReasonForRequest, @BusinessJustification,
             @Priority, @StatusId, @RequestedBy)
             
            RETURNING Id;";
         

        return await _common.ExecuteScalarAsync<long>(
               sql,
               request, tx);

    }

    public async Task AddAttachmentAsync(DocumentRequestAttachments attachment, IDbTransaction tx)
    {
        const string sql = @"
            INSERT INTO DocumentRequestAttachment
            (RequestId, FileName, FilePath, UploadedBy)
            VALUES
            (@RequestId, @FileName, @FilePath, @UploadedBy);";

        //await tx.Connection.ExecuteAsync(sql, attachment, tx);
        await _common.ExecuteScalarAsync<long>(
              sql,
              attachment, tx);
    }

    public async Task<DocumentRequests?> GetByIdAsync(long id)
    {
        //using var conn = _factory.CreateConnection();

        return await _common.QueryFirstOrDefaultAsync<DocumentRequests>(
            "SELECT * FROM DocumentRequest WHERE Id = @id",
            new { id });
    }
}



public interface IRequestNumberService
{
    Task<string> GenerateAsync(IDbTransaction tx);
}
public class RequestNumberService : IRequestNumberService
{
    public async Task<string> GenerateAsync(IDbTransaction tx)
    {
        const string sql = @"
        UPDATE DocumentRequestSequence
        SET LastNumber = LastNumber + 1
        RETURNING Prefix, LastNumber;";

        var result = await tx.Connection.QuerySingleAsync<(string Prefix, long LastNumber)>(sql, transaction: tx);

        return $"{result.Prefix}-{result.LastNumber:D6}";
    }
}


public interface IWorkflowService
{
    Task<long> StartRequestWorkflowAsync(long requestId, IDbTransaction tx);
}

public class WorkflowService : IWorkflowService
{
    public async Task<long> StartRequestWorkflowAsync(long requestId, IDbTransaction tx)
    {
        const string sql = @"
        INSERT INTO WorkflowInstance
        (WorkflowDefinitionId, EntityType, EntityId, Status)
        VALUES (1, 'DocumentRequest', @requestId, 'Running')
        RETURNING Id;";

        return await tx.Connection.ExecuteScalarAsync<long>(sql, new { requestId }, tx);
    }
}



public class DocumentRequestService
{
    private readonly IDbConnectionFactory _factory;
    private readonly IDocumentRequestRepository _repo;
    private readonly IRequestNumberService _numberService;
    private readonly IWorkflowService _workflow;
    private readonly DMSCommon _common;
    public DocumentRequestService(
        IDbConnectionFactory factory,
        IDocumentRequestRepository repo,
        IRequestNumberService numberService,
        IWorkflowService workflow,
        DMSCommon common)
    {
        _factory = factory;
        _repo = repo;
        _numberService = numberService;
        _workflow = workflow;
        _common = common;
    }

    public async Task<long> CreateAsync(
        DocumentRequests request,
        List<DocumentRequestAttachments>? attachments)
    {
        //using var conn = _factory.CreateConnection();
        //conn.Open();

        await using var tx = await _common.BeginTransactionAsync();


        try
        {
            // Generate Request Number
            request.RequestNumber = await _numberService.GenerateAsync(tx);

            // Default status = Draft (1)
            request.StatusId = 1;

            var requestId = await _repo.CreateAsync(request, tx);

            // Attachments
            if (attachments != null)
            {
                foreach (var file in attachments)
                {
                    file.RequestId = requestId;
                    await _repo.AddAttachmentAsync(file, tx);
                }
            }

            // Start Workflow
            var workflowId = await _workflow.StartRequestWorkflowAsync(requestId, tx);

            await _common.ExecuteAsync(
                "UPDATE DocumentRequest SET WorkflowInstanceId = @workflowId WHERE Id = @requestId",
                new { workflowId, requestId },
                tx);

            tx.Commit();

            return requestId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
