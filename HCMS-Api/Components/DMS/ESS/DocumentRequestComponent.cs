﻿using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using Npgsql;
using System.Data;
using System.Data.Entity.Infrastructure;
using System.Reflection.Metadata;
using static Dapper.SqlMapper;
using static HCMS_Api.Controllers.HCMS.Common.SecurityController;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentRequestComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    private readonly DocumentComponent _documentComponent;
    private readonly NotificationComponent _notificationComponent;
    public DocumentRequestComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        //, ILogger<UtilitiesController> logger
        , IHttpContextAccessor http,
        DMSCommon common,
        DocumentComponent documentComponent,
        NotificationComponent notificationComponent
        )
    {
        _http = http;
        //_logger = logger;
        _utilities = utilities;
        _dataservice = dataservice;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _dapperService = dapper;
        _common = common;
        _documentComponent = documentComponent;
        _notificationComponent = notificationComponent;
        //string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        //_dataservice.BeginProcess(connectionString);

    }


    //public async Task<DocumentRequestReadDto> CreateAsync(DocumentRequestCreateDto input)
    //{
    //    try
    //    {
    //        //var clientIp = _clientContextService.GetClientIP();
    //        //var prefix = _utilities.GetPrefix(clientIp);
    //        var userId = "manual"; //_utilities.GetUserid(prefix);
    //        if (input.Id < 0)
    //            throw new CustomException("DocumentRequests code is required.", 400);

    //        // Check duplicate by Id OR Name
    //        string checkQuery = $@"
    //        SELECT COUNT(1)
    //        FROM DocumentRequests
    //        WHERE Id = '{input.Id}' 
    //          AND IsDeleted = FALSE";

    //        int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

    //        if (exists > 0)
    //            throw new CustomException("DocumentRequests already exists", 409);

    //        // Insert (PostgreSQL syntax)
    //        string insertQuery = $@"
    //        INSERT INTO DocumentRequests
    //        (   CompanyId,
    //            RequestNumber,
    //            RequestType,
    //            DocumentId,
    //            DocumentTypeId,
    //            DivisionCode,
    //            DepartmentCode,
    //            SubDepartmentCode,
    //            BusinessDomainCode,
    //            DocumentName, 
    //            Justification, 
    //            Status,  
    //            CurrentStep,  
    //            IsActive,
    //            IsDeleted,
    //            CreatedAt,
    //            CreatedBy,
    //            LastModifiedAt,
    //            LastModifiedBy
    //        )
    //        VALUES
    //        (
    //            '{input.CompanyId}',
    //            '{input.RequestNumber}',
    //            '{input.RequestType}',
    //            '{input.DocumentId}',
    //            '{input.DocumentTypeId}',
    //            '{input.DivisionCode}', 
    //            '{input.DepartmentCode}', 
    //            '{input.SubDepartmentCode}', 
    //            '{input.BusinessDomainCode}', 
    //            '{input.DocumentName}', 
    //            '{input.Justification}', 
    //            '{input.Status}', 
    //            '{input.CurrentStep}', 
    //            TRUE,
    //            FALSE,
    //            NOW(),
    //            '{userId.Replace("'", "''")}',
    //            NOW(),
    //            '{userId.Replace("'", "''")}'
    //        )
    //        RETURNING Id;";

    //        int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

    //        // Fetch inserted record
    //        string selectQuery = $@"
    //            SELECT d.*, c.Name AS Company,div.Name AS Division, dep.Name AS Department, bd.Name BusinessDomain
    //            FROM DocumentRequests d
    //            LEFT JOIN Companies c
    //            ON d.CompanyId = c.Id
    //            LEFT JOIN Divisions div
    //            ON d.DivisionCode = div.Code
    //            LEFT JOIN Department dep
    //            ON d.DepartmentCode = dep.Code
    //            LEFT JOIN BusinessDomains bd
    //            ON d.BusinessDomainCode = bd.Code
    //        WHERE d.Id = {newId}";

    //        DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

    //        if (dt == null || dt.Rows.Count == 0)
    //            throw new Exception("Failed to fetch created division");

    //        DataRow row = dt.Rows[0];

    //        return new DocumentRequestReadDto
    //        {
    //            Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

    //            CompanyId = row.Field<int>("CompanyId"),
    //            Company = row.Field<string>("Company"),

    //            RequestNumber = row.Table.Columns.Contains("RequestNumber") ? row.Field<string>("RequestNumber") : string.Empty,
    //            DocumentRequestTypeCode = row.Table.Columns.Contains("DocumentRequestTypeCode") ? row.Field<string>("DocumentRequestTypeCode") : string.Empty,
    //            DocumentId = row.Table.Columns.Contains("DocumentId") && !row.IsNull("DocumentId") ? row.Field<int>("DocumentId") : 0,
    //            DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

    //            Division = row.Field<string>("Division"),
    //            DivisionCode = row.Field<string>("DivisionCode"),

    //            Department = row.Field<string>("Department"),
    //            DepartmentCode = row.Field<string>("DepartmentCode"),

    //            SubDepartment = row.Field<string>("SubDepartment"),
    //            SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

    //            BusinessDomain = row.Field<string>("BusinessDomain"),
    //            BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

    //            DocumentName = row.Table.Columns.Contains("DocumentName") ? row.Field<string>("DocumentName") : string.Empty,
    //            Justification = row.Table.Columns.Contains("Justification") ? row.Field<string>("Justification") : string.Empty,
    //            Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
    //            RowVersion = row.Table.Columns.Contains("RowVersion") ? row.Field<string>("RowVersion") : string.Empty,
    //            ProposedContent = row.Table.Columns.Contains("ProposedContent") ? row.Field<string>("ProposedContent") : string.Empty,
    //            IsContentFinalized = row.Table.Columns.Contains("IsContentFinalized") && row.Field<bool?>("IsContentFinalized") == true,
    //            DraftContentLastModifiedAt = (row.Table.Columns.Contains("DraftContentLastModifiedAt") && !row.IsNull("DraftContentLastModifiedAt"))
    //             ? row.Field<DateTime>("DraftContentLastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
    //            DraftContentLastModifiedBy = row.Table.Columns.Contains("DraftContentLastModifiedBy") ? row.Field<string>("DraftContentLastModifiedBy") : string.Empty,

    //            IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
    //            IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
    //            CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
    //        ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
    //            CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
    //            LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
    //             ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
    //            LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
    //        };
    //    }
    //    catch
    //    {
    //        throw;
    //    }
    //}

    public async Task<long> CreateDraftDocumentRequestAsync(DraftDocumentRequestDto dto)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string? draftFileUrl = null;
            if (dto.DraftFile != null && dto.DraftFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "drafts");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileExtension = Path.GetExtension(dto.DraftFile.FileName);
                var fileName = $"{dto.DraftFile.FileName}";
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.DraftFile.CopyToAsync(stream);
                }
                draftFileUrl = $"/uploads/drafts/{fileName}";
            }

            //-------------------------------------------------
            // Insert Draft Request
            //-------------------------------------------------

            var requestId = await _common.ExecuteScalarAsync<long>(@"
                INSERT INTO DocumentRequests
                (
                    CompanyId,
                    RequestNumber,
                    DocumentRequestTypeCode,
                    DocumentTypeCode,
                    DocumentName,
                    Justification,
                    ProposedContent,
                    DraftFileUrl,
                    DivisionCode,
                    DepartmentCode,
                    SubDepartmentCode,
                    BusinessDomainCode,
                    Status,
                    CreatedBy,
                    LastModifiedBy,
                    IsContentFinalized
                )
                VALUES
                (
                    @CompanyId,
                    'DR-' || nextval('document_request_seq'),
                    @RequestType,
                    @DocumentTypeCode,
                    @DocumentName,
                    @Justification,
                    @ProposedContent,
                    @DraftFileUrl,
                    @DivisionCode,
                    @DepartmentCode,
                    @SubDepartmentCode,
                    @BusinessDomainCode,
                    @Status,
                    @CreatedBy,
                    @CreatedBy,
                    FALSE
                )
                RETURNING Id;",
            new
            {
                dto.CompanyId,
                RequestType = dto.DocumentRequestTypeCode,
                dto.DocumentTypeCode,
                dto.DocumentName,
                dto.Justification,
                dto.ProposedContent,
                DraftFileUrl = draftFileUrl,
                dto.DivisionCode,
                dto.DepartmentCode,
                dto.SubDepartmentCode,
                dto.BusinessDomainCode,
                Status = DocumentRequestStatus.Draft,
                CreatedBy = dto.CreatedByUserId
            }, transaction);

            //-----------------------------------------
            // ROLE DISTRIBUTION (Editable)
            //-----------------------------------------

            if (dto.DistributionList?.Any() == true)
            {
                foreach (var d in dto.DistributionList)
                {
                    await _common.ExecuteAsync(@"
                        INSERT INTO DocumentRequestRoleDistributions
                        (CompanyId,DocumentRequestId,DivisionCode,
                         DepartmentCode,SubDepartmentCode,
                         BusinessDomainCode,RoleId,DistributionTypeId,
                         CreatedBy,LastModifiedBy)
                        VALUES
                        (@CompanyId,@RequestId,@DivisionCode,
                         @DepartmentCode,@SubDepartmentCode,
                         @BusinessDomainCode,@RoleId,@DistributionTypeId,
                         @UserId,@UserId);",
                    new
                    {
                        dto.CompanyId,
                        RequestId = requestId,
                        d.DivisionCode,
                        d.DepartmentCode,
                        d.SubDepartmentCode,
                        d.BusinessDomainCode,
                        d.RoleId,
                        d.DistributionTypeId,
                        UserId = dto.CreatedByUserId
                    }, transaction);
                }
            }

            //-----------------------------------------
            // USER DISTRIBUTION (Editable)
            //-----------------------------------------

            if (dto.UserIds?.Any() == true)
            {
                foreach (var userId in dto.UserIds)
                {
                    await _common.ExecuteAsync(@"
                        INSERT INTO DocumentRequestUserDistributions
                        (CompanyId,DocumentRequestId,UserId,
                         CreatedBy,LastModifiedBy)
                        VALUES
                        (@CompanyId,@RequestId,@UserId,
                         @CreatedBy,@CreatedBy);",
                    new
                    {
                        dto.CompanyId,
                        RequestId = requestId,
                        UserId = userId,
                        CreatedBy = dto.CreatedByUserId
                    }, transaction);
                }
            }

            //-------------------------------------------------
            // History
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO DocumentRequestHistory
                (CompanyId,RequestId,ToStateId,ChangedBy,Comments)
                VALUES
                (@CompanyId,@RequestId,@Status,@UserId,'Draft Created');",
            new
            {
                dto.CompanyId,
                RequestId = requestId,
                Status = DocumentRequestStatus.Draft,
                UserId = dto.CreatedByUserId
            }, transaction);

            await transaction.CommitAsync();
            return requestId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<long> UpdateDraftDocumentRequestAsync(UpdateDraftRequestDto dto)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string? draftFileUrl = null;
            if (dto.DraftFile != null && dto.DraftFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "drafts");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileExtension = Path.GetExtension(dto.DraftFile.FileName);
                var fileName = $"DRF_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.DraftFile.CopyToAsync(stream);
                }
                draftFileUrl = $"/uploads/drafts/{fileName}";
            }

            //-------------------------------------------------
            // 1️⃣ Attempt Safe Update (Optimistic Lock)
            //-------------------------------------------------

            var rowsAffected = await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET
                    DocumentName = @DocumentName,
                    Justification = @Justification,
                    ProposedContent = @ProposedContent, 
                    DraftFileUrl = COALESCE(@DraftFileUrl, DraftFileUrl),
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @UserId
                WHERE Id = @RequestId
                AND CompanyId = @CompanyId
                AND Status = @DraftStatus
                AND IsContentFinalized = FALSE;",
            new
            {
                dto.RequestId,
                dto.CompanyId,
                dto.DocumentName,
                dto.Justification,
                dto.ProposedContent, 
                DraftFileUrl = draftFileUrl,
                UserId = dto.ModifiedByUserId.ToString(), 
                DraftStatus = DocumentRequestStatus.Draft
            },
            transaction);

            if (rowsAffected == 0)
                throw new Exception(
                    "Draft was modified by another user OR already submitted.");


            // Remove old distributions
            await _common.ExecuteAsync(@"
                DELETE FROM DocumentRequestRoleDistributions
                WHERE DocumentRequestId = @RequestId;",
                new { dto.RequestId }, transaction);

            await _common.ExecuteAsync(@"
                DELETE FROM DocumentRequestUserDistributions
                WHERE DocumentRequestId = @RequestId;",
                new { dto.RequestId }, transaction);

            // Insert new distributions
            await InsertDistributionsAsync(dto.CompanyId, dto.RequestId,
                dto.DistributionList, dto.UserList,
                dto.ModifiedByUserId, transaction);


            //-------------------------------------------------
            // 2️⃣ Insert History
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO DocumentRequestHistory
                (
                    CompanyId,
                    RequestId,
                    ToStateId,
                    ChangedBy,
                    Comments
                )
                VALUES
                (
                    @CompanyId,
                    @RequestId,
                    @Status,
                    @UserId,
                    'Draft Updated'
                );",
                new
                {
                    dto.CompanyId,
                    dto.RequestId,
                    Status = DocumentRequestStatus.Draft,
                    UserId = dto.ModifiedByUserId
                },
                transaction);

            await transaction.CommitAsync();
            return 1; // 1= success
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }



    public async Task<bool> SubmitDraftDocumentRequestAsync(SubmitDocumentRequestDto input)
    {
        // Optional: ensure draft exists before calling original submit
        var draftExists = await _common.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM DocumentRequests
            WHERE Id = @RequestId
            AND CompanyId = @CompanyId
            AND Status = @DraftStatus;",
        new
        {
            input.RequestId,
            input.CompanyId,
            DraftStatus = DocumentRequestStatus.Draft
        });

        if (draftExists == 0)
            throw new Exception("Only draft requests can be submitted.");

        // Call your EXISTING working method
        return await SubmitDocumentRequestAsync(input);
    }

    private async Task<bool> SubmitDocumentRequestAsync(SubmitDocumentRequestDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();

        try
        {
            //-------------------------------------------------
            // Validate Request
            //-------------------------------------------------

            var request = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM DocumentRequests
                WHERE Id = @RequestId
                AND CompanyId = @CompanyId
                FOR UPDATE;",
                new { input.RequestId, input.CompanyId }, tx);

            if (request == null)
                throw new Exception("Request not found.");

            if (request.status != (int)DocumentRequestStatus.Draft)
                throw new Exception("Only draft requests can be submitted.");


            //-------------------------------------------------
            // UC-22 USER MODIFICATION BEFORE FREEZE
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
            DELETE FROM DocumentRequestRoleDistributions
            WHERE DocumentRequestId = @RequestId;", new { input.RequestId }, tx);

            await _common.ExecuteAsync(@"
            DELETE FROM DocumentRequestUserDistributions
            WHERE DocumentRequestId = @RequestId;", new { input.RequestId }, tx);

            //-------------------------------------------------
            // RE-INSERT UPDATED ROLE DISTRIBUTION
            //-------------------------------------------------

            if (input.DistributionList?.Any() == true)
            {
                foreach (var d in input.DistributionList)
                {
                    await _common.ExecuteAsync(@"
                        INSERT INTO DocumentRequestRoleDistributions
                        (CompanyId,DocumentRequestId,DivisionCode,
                         DepartmentCode,SubDepartmentCode,
                         BusinessDomainCode,RoleId,DistributionTypeId,
                         CreatedBy,LastModifiedBy)
                        VALUES
                        (@CompanyId,@RequestId,@DivisionCode,
                         @DepartmentCode,@SubDepartmentCode,
                         @BusinessDomainCode,@RoleId,@DistributionTypeId,
                         @UserId,@UserId);",
                    new
                    {
                        input.CompanyId,
                        input.RequestId,
                        d.DivisionCode,
                        d.DepartmentCode,
                        d.SubDepartmentCode,
                        d.BusinessDomainCode,
                        d.RoleId,
                        d.DistributionTypeId,
                        UserId = input.SubmittedBy
                    }, tx);
                }
            }

            //-------------------------------------------------
            // RE-INSERT UPDATED USER LIST
            //-------------------------------------------------

            if (input.UserList?.Any() == true)
            {
                foreach (var userId in input.UserList)
                {
                    await _common.ExecuteAsync(@"
                    INSERT INTO DocumentRequestUserDistributions
                    (CompanyId,DocumentRequestId,UserId,
                     CreatedBy,LastModifiedBy)
                    VALUES
                    (@CompanyId,@RequestId,@UserId,
                     @CreatedBy,@CreatedBy);",
                    new
                    {
                        input.CompanyId,
                        input.RequestId,
                        UserId = userId,
                        CreatedBy = input.SubmittedBy
                    }, tx);
                }
            }


            //-------------------------------------------------
            // Resolve Correct Policy FIRST (Scope Routing)
            //-------------------------------------------------

            var policyId = await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id
                FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId
                AND EntityType = 'Request'
                AND DocumentTypeCode = @DocType
                AND COALESCE(DivisionCode,'') = COALESCE(@DivisionCode,'')
                AND COALESCE(DepartmentCode,'') = COALESCE(@DepartmentCode,'')
                AND COALESCE(SubDepartmentCode,'') = COALESCE(@SubDepartmentCode,'')
                AND COALESCE(BusinessDomainCode,'') = COALESCE(@BusinessDomainCode,'')
                AND IsActive = TRUE
                AND IsDeleted = FALSE;",
            new
            {
                input.CompanyId,
                DocType = request.documenttypecode,
                DivisionCode = request.divisioncode,
                DepartmentCode = request.departmentcode,
                SubDepartmentCode = request.subdepartmentcode,
                BusinessDomainCode = request.businessdomaincode
            }, tx);

            if (policyId == null)
                throw new Exception("No workflow policy defined for selected Cabinet Scope.");

            //-------------------------------------------------
            // Resolve ACTIVE Version using PolicyId
            //-------------------------------------------------

            var versionId = await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id
                FROM WorkflowPolicyVersions
                WHERE CompanyId = @CompanyId
                AND WorkflowPolicyId = @PolicyId
                AND IsActive = TRUE
                LIMIT 1;",
            new
            {
                input.CompanyId,
                PolicyId = policyId
            }, tx);

            if (versionId == null)
                throw new Exception("Workflow policy version not found.");


            //-------------------------------------------------
            // Create Execution
            //-------------------------------------------------

            var executionId = await _common.ExecuteScalarAsync<long>(@"
                INSERT INTO WorkflowExecutions
                (
                    CompanyId,
                    WorkflowPolicyVersionId,
                    EntityType,
                    EntityId,
                    Status,
                    StartedBy
                )
                VALUES
                (
                    @CompanyId,
                    @VersionId,
                    'Request',
                    @RequestId,
                    'Running',
                    @UserId
                )
                RETURNING Id;",
                new
                {
                    input.CompanyId,
                    VersionId = versionId,
                    input.RequestId,
                    UserId = input.SubmittedBy
                }, tx);

            //-------------------------------------------------
            // Insert Steps (SNAPSHOT)
            //-------------------------------------------------

            var inserted = await _common.ExecuteAsync(@"
                INSERT INTO WorkflowExecutionSteps
                (
                    CompanyId,
                    WorkflowExecutionId,
                    StepDefinitionId,
                    AssignedUserId,
                    AssignedRoleId,
                    StepOrder,
                    Observation,
                    IsActive
                )
                SELECT
                    @CompanyId,
                    @ExecutionId,
                    Id,
                    UserId,
                    RoleId,
                    StepOrder,
                    '',
                    FALSE
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId;",
                new
                {
                    input.CompanyId,
                    ExecutionId = executionId,
                    VersionId = versionId
                }, tx);

            if (inserted < 1)
                throw new Exception("Workflow misconfigured — no steps copied.");

            //-------------------------------------------------
            // Activate FIRST step (NO JOINS)
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutionSteps
                SET IsActive = TRUE
                WHERE WorkflowExecutionId = @ExecutionId
                AND StepOrder =
                (
                    SELECT MIN(StepOrder)
                    FROM WorkflowExecutionSteps
                    WHERE WorkflowExecutionId = @ExecutionId
                );",
                new { ExecutionId = executionId }, tx);

            //-------------------------------------------------
            // Update Request
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET Status = @Status,
                    SubmittedAt = NOW(),
                    SubmittedBy = @UserId,
                    IsContentFinalized = TRUE
                WHERE Id = @RequestId;",
                new
                {
                    Status = DocumentRequestStatus.Submitted,
                    UserId = input.SubmittedBy,
                    input.RequestId
                }, tx);

            // Prepare notification data
            int? firstStepUserId = null;
            string? requestNumber = null;
            var firstStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.AssignedUserId, dr.RequestNumber
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN DocumentRequests dr ON dr.Id = we.EntityId
                WHERE wes.WorkflowExecutionId = @ExecutionId
                AND wes.IsActive = TRUE;", new { ExecutionId = executionId }, tx);
            
            if (firstStep != null && firstStep.assigneduserid != null)
            {
                firstStepUserId = (int)firstStep.assigneduserid;
                requestNumber = Convert.ToString(firstStep.requestnumber);
            }

            await tx.CommitAsync();

            if (firstStepUserId.HasValue)
            {
                var placeholders = new Dictionary<string, string> { { "ID", requestNumber ?? input.RequestId.ToString() } };
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, input.CompanyId, (int)input.RequestId, firstStepUserId.Value, placeholders);
            }

            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }


    private async Task InsertDistributionsAsync(
            int companyId,
            long requestId,
            IEnumerable<DistributionListCreateDto> roles,
            IEnumerable<long> users,
            long userId,
            IDbTransaction tx)
    {
        if (roles?.Any() == true)
        {
            foreach (var d in roles)
            {
                await _common.ExecuteAsync(@"
                INSERT INTO DocumentRequestRoleDistributions
                (CompanyId,DocumentRequestId,DivisionCode,
                 DepartmentCode,SubDepartmentCode,
                 BusinessDomainCode,RoleId,DistributionTypeId,
                 CreatedBy,LastModifiedBy)
                VALUES
                (@CompanyId,@RequestId,@DivisionCode,
                 @DepartmentCode,@SubDepartmentCode,
                 @BusinessDomainCode,@RoleId,@DistributionTypeId,
                 @UserId,@UserId);",
                new
                {
                    CompanyId = companyId,
                    RequestId = requestId,
                    d.DivisionCode,
                    d.DepartmentCode,
                    d.SubDepartmentCode,
                    d.BusinessDomainCode,
                    d.RoleId,
                    d.DistributionTypeId,
                    UserId = userId
                }, tx);
            }
        }

        if (users?.Any() == true)
        {
            foreach (var uid in users)
            {
                await _common.ExecuteAsync(@"
                INSERT INTO DocumentRequestUserDistributions
                (CompanyId,DocumentRequestId,UserId,
                 CreatedBy,LastModifiedBy)
                VALUES
                (@CompanyId,@RequestId,@UserId,
                 @UserId,@UserId);",
                new
                {
                    CompanyId = companyId,
                    RequestId = requestId,
                    UserId = uid
                }, tx);
            }
        }
    }

    private async Task InsertHistoryAsync(
        int companyId,
        long requestId,
        DocumentRequestStatus status,
        long userId,
        string comments,
        IDbTransaction tx)
    {
        await _common.ExecuteAsync(@"
        INSERT INTO DocumentRequestHistory
        (CompanyId,RequestId,ToStateId,ChangedBy,Comments)
        VALUES
        (@CompanyId,@RequestId,@Status,@UserId,@Comments);",
        new
        {
            CompanyId = companyId,
            RequestId = requestId,
            Status = status,
            UserId = userId,
            Comments = comments
        }, tx);
    }

    //public async Task<bool> ApproveWorkflowStepAsync(ApproveRejectWorkflowStepDto input)
    //{
    //    await using var tx = await _common.BeginTransactionAsync();

    //    try
    //    {
    //        //-------------------------------------------------
    //        // 1️⃣ Lock Step
    //        //-------------------------------------------------

    //        var step = await _common.QuerySingleAsync<dynamic>(@"
    //        SELECT *
    //        FROM WorkflowExecutionSteps
    //        WHERE Id = @StepId
    //        AND CompanyId = @CompanyId
    //        FOR UPDATE;",
    //            new { StepId = input.StepId, CompanyId = input.CompanyId },
    //            tx);

    //        if (step == null)
    //            throw new Exception("Approval step not found.");

    //        if (!(bool)step.IsActive)
    //            throw new Exception("Step is not active.");

    //        if (step.Decision != null)
    //            throw new Exception("Step already processed.");

    //        //-------------------------------------------------
    //        // 2️⃣ Mark Approved
    //        //-------------------------------------------------

    //        await _common.ExecuteAsync(@"
    //        UPDATE WorkflowExecutionSteps
    //        SET Decision = 'Approved',
    //            Observation = @Comments,
    //            ActionAt = NOW()
    //        WHERE Id = @StepId;",
    //            new { StepId = input.StepId, Comments = input.Observation },
    //            tx);

    //        //-------------------------------------------------
    //        // 3️⃣ Get Step Order
    //        //-------------------------------------------------

    //        var stepInfo = await _common.QuerySingleAsync<dynamic>(@"
    //        SELECT wsd.StepOrder, wes.WorkflowExecutionId
    //        FROM WorkflowExecutionSteps wes
    //        JOIN WorkflowStepDefinitions wsd
    //            ON wes.StepDefinitionId = wsd.Id
    //        WHERE wes.Id = @StepId;",
    //            new { StepId = input.StepId },
    //            tx);

    //        //-------------------------------------------------
    //        // 4️⃣ Check Parallel Pending
    //        //-------------------------------------------------

    //        var pending = await _common.ExecuteScalarAsync<int>(@"
    //        SELECT COUNT(*)
    //        FROM WorkflowExecutionSteps wes
    //        JOIN WorkflowStepDefinitions wsd
    //            ON wes.StepDefinitionId = wsd.Id
    //        WHERE wes.WorkflowExecutionId = @ExecutionId
    //        AND wsd.StepOrder = @StepOrder
    //        AND wes.Decision IS NULL;",
    //            new
    //            {
    //                ExecutionId = stepInfo.WorkflowExecutionId,
    //                StepOrder = stepInfo.StepOrder
    //            },
    //            tx);

    //        if (pending > 0)
    //        {
    //            await tx.CommitAsync();
    //            return true; // wait for other approvers
    //        }

    //        //-------------------------------------------------
    //        // 5️⃣ Activate Next Step
    //        //-------------------------------------------------

    //        var nextOrder = await _common.ExecuteScalarAsync<int?>(@"
    //            SELECT MIN(wsd.StepOrder)
    //            FROM WorkflowExecutionSteps wes
    //            JOIN WorkflowStepDefinitions wsd
    //                ON wes.StepDefinitionId = wsd.Id
    //            WHERE wes.WorkflowExecutionId = @ExecutionId
    //            AND wsd.StepOrder > @Current;",
    //            new
    //            {
    //                ExecutionId = stepInfo.WorkflowExecutionId,
    //                Current = stepInfo.StepOrder
    //            },
    //            tx);

    //        if (nextOrder.HasValue)
    //        {
    //            await _common.ExecuteAsync(@"
    //                UPDATE WorkflowExecutionSteps wes
    //                SET IsActive = TRUE
    //                FROM WorkflowStepDefinitions wsd
    //                WHERE wes.StepDefinitionId = wsd.Id
    //                AND wes.WorkflowExecutionId = @ExecutionId
    //                AND wsd.StepOrder = @NextOrder;",
    //                new
    //                {
    //                    ExecutionId = stepInfo.WorkflowExecutionId,
    //                    NextOrder = nextOrder
    //                },
    //                tx);
    //        }
    //        else
    //        {
    //            //-------------------------------------------------
    //            // 6️⃣ COMPLETE WORKFLOW
    //            //-------------------------------------------------

    //            await _common.ExecuteAsync(@"
    //                UPDATE WorkflowExecutions
    //                SET Status = 'Completed',
    //                    CompletedAt = NOW()
    //                WHERE Id = @ExecutionId;",
    //                new { ExecutionId = stepInfo.WorkflowExecutionId },
    //                tx);

    //            //-------------------------------------------------
    //            // AUTO APPROVE REQUEST
    //            //-------------------------------------------------

    //            await _common.ExecuteAsync(@"
    //                UPDATE DocumentRequests
    //                SET Status = @Approved
    //                WHERE Id =
    //                (
    //                    SELECT EntityId
    //                    FROM WorkflowExecutions
    //                    WHERE Id = @ExecutionId
    //                );",
    //                new
    //                {
    //                    ExecutionId = stepInfo.WorkflowExecutionId,
    //                    Approved = DocumentRequestStatus.Approved
    //                },
    //                tx);
    //        }

    //        await tx.CommitAsync();
    //        return true;
    //    }
    //    catch
    //    {
    //        await tx.RollbackAsync();
    //        throw;
    //    }
    //}


    //public async Task<bool> RejectWorkflowStepAsync(ApproveRejectWorkflowStepDto input)
    //{
    //    await using var tx = await _common.BeginTransactionAsync();

    //    try
    //    {
    //        var executionId = await _common.ExecuteScalarAsync<long>(@"
    //        UPDATE WorkflowExecutionSteps
    //        SET Decision = 'Rejected',
    //            Observation = @Comments,
    //            ActionAt = NOW()
    //        WHERE Id = @StepId
    //        RETURNING WorkflowExecutionId;",
    //            new { StepId = input.StepId, Comments = input.Observation },
    //            tx);

    //        //-------------------------------------------------
    //        // Cancel Workflow
    //        //-------------------------------------------------

    //        await _common.ExecuteAsync(@"
    //        UPDATE WorkflowExecutions
    //        SET Status = 'Cancelled'
    //        WHERE Id = @ExecutionId;",
    //            new { ExecutionId = executionId },
    //            tx);

    //        //-------------------------------------------------
    //        // Return Request to Draft
    //        //-------------------------------------------------

    //        await _common.ExecuteAsync(@"
    //        UPDATE DocumentRequests
    //        SET Status = @Draft
    //        WHERE Id =
    //        (
    //            SELECT EntityId
    //            FROM WorkflowExecutions
    //            WHERE Id = @ExecutionId
    //        );",
    //            new
    //            {
    //                ExecutionId = executionId,
    //                Draft = DocumentRequestStatus.Draft
    //            },
    //            tx);

    //        await tx.CommitAsync();
    //        return true;
    //    }
    //    catch
    //    {
    //        await tx.RollbackAsync();
    //        throw;
    //    }
    //}

    public async Task<IEnumerable<DocumentRequestReadDto>> GetMyInboxRequestsAsync(GetPendingRequestDto input)
    {
        try
        {

            if (input.EmployeeCode == "" || input.EmployeeCode == null)
                throw new Exception("Requests not found.");

            var userId = await GetEmployeeID(input.EmployeeCode);
            var sql = @"SELECT * FROM fn_get_my_inbox_requests(
                    @CompanyId,
                    @UserId,
                    @RequestStatus,
                    @DivisionCode,
                    @DepartmentCode,
                    @SubDepartmentCode,
                    @BusinessDomainCode,
                    @DocumentTypeCode
                );";

            return await _common.QueryAsync<DocumentRequestReadDto>(sql, new
            {
                input.CompanyId,
                userId,
                input.RequestStatus,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode,
                input.DocumentTypeCode
            });

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<IEnumerable<DocumentRequestReadDto>>GetDraftDocumentRequestAsync(int companyId, string createdByUserId)
    {
        try
        {
            var userId = await GetEmployeeID(createdByUserId);

            //-------------------------------------------------
            // 1️⃣ Get Draft Requests
            //-------------------------------------------------

            var requests = (await _common.QueryAsync<DocumentRequestReadDto>(@"
                SELECT *
                FROM Vw_DocumentRequests
                WHERE CompanyId = @CompanyId
                AND Status = @DraftStatus
                AND CreatedBy = @CreatedBy
                ORDER BY Id DESC;",
                new
                {
                    CompanyId = companyId,
                    DraftStatus = DocumentRequestStatus.Draft,
                    CreatedBy = userId.ToString()
                })).ToList();

            if (!requests.Any())
                return Enumerable.Empty<DocumentRequestReadDto>();

            //-------------------------------------------------
            // 2️⃣ Extract Ids
            //-------------------------------------------------

            var requestIds = requests.Select(x => x.Id).ToArray();

            //-------------------------------------------------
            // 3️⃣ Get Role Distributions
            //-------------------------------------------------

            var roleDistributions = (await _common.QueryAsync<DistributionListReadDto>(@"
                SELECT dl.*,
                       div.Name AS Division,
                       dep.Name AS Department,
                       subd.Name AS SubDepartment,
                       bd.Name AS BusinessDomain,
	                   dt.Name AS DistributionType
                   FROM DocumentRequestRoleDistributions dl
                        LEFT JOIN Divisions div ON dl.DivisionCode = div.Code 
                        LEFT JOIN Departments dep ON dl.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd ON dl.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd ON dl.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c ON dl.CompanyId = c.Id
                        LEFT JOIN Roles r ON dl.RoleId = r.Id 
		                LEFT JOIN DistributionTypes dt ON dl.DistributionTypeId = dt.Id
                WHERE dl.CompanyId = @CompanyId
                AND dl.DocumentRequestId = ANY(@RequestIds);",
                new
                {
                    CompanyId = companyId,
                    RequestIds = requestIds
                })).ToList();

            //-------------------------------------------------
            // 4️⃣ Get User Distributions
            //-------------------------------------------------

            var userDistributions = (await _common.QueryAsync<DocumentRequestUserDistribution>(@"
                SELECT *
                FROM DocumentRequestUserDistributions
                WHERE CompanyId = @CompanyId
                AND DocumentRequestId = ANY(@RequestIds);",
                new
                {
                    CompanyId = companyId,
                    RequestIds = requestIds
                })).ToList();

            //-------------------------------------------------
            // 5️⃣ Map Distributions Into Each Request
            //-------------------------------------------------

            foreach (var request in requests)
            {
                request.DistributionList = roleDistributions
                    .Where(x => x.DocumentRequestId == request.Id)
                    .Select(x=> new DistributionListReadDto { 
                        Id = x.Id,
                        DocumentRequestId = x.DocumentRequestId,
                        CompanyId = x.CompanyId,
                        Company = x.Company,
                        RoleId = x.RoleId,
                        Role = x.Role,
                        DistributionTypeId = x.DistributionTypeId,
                        DistributionType = x.DistributionType,
                        Division =x.Division,
                        DivisionCode = x.DivisionCode,
                        Department = x.Department,
                        DepartmentCode = x.DepartmentCode,
                        SubDepartment = x.SubDepartment,
                        SubDepartmentCode = x.SubDepartmentCode,
                        BusinessDomain = x.BusinessDomain,
                        BusinessDomainCode = x.BusinessDomainCode,
                        }).ToList();

                request.UserList = userDistributions
                    .Where(x => x.DocumentRequestId == request.Id)
                    .ToList();
            }

            return requests;
        }
        catch
        {
            throw;
        }
    }
   

    public async Task<bool> TakeWorkflowActionAsync(ApproveRejectWorkflowStepDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();

        //-------------------------------------------------
        // 1️⃣ Lock Step
        //-------------------------------------------------

        var step = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
        SELECT *
        FROM WorkflowExecutionSteps
        WHERE Id = @StepId
        AND CompanyId = @CompanyId
        FOR UPDATE;",
            new { input.StepId, input.CompanyId }, tx);

        if (step == null)
            throw new Exception("Step not found.");

        if (!step.isactive)
            throw new Exception("Step is not active.");

        if (step.decision != null)
            throw new Exception("Step already processed.");

        //-------------------------------------------------
        // 2️⃣ Resolve decision
        //-------------------------------------------------

        string decision = input.Action switch
        {
            "APPROVE" => "Approved",
            "REJECT" => "Rejected",
            "REWORK" => "Rework",
            "COMMENT" => null,
            _ => throw new Exception("Invalid action.")
        };

        int executionId = step.workflowexecutionid;
        int stepOrder = step.steporder;

        // Fetch request info for notifications
        var requestInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT dr.Id, dr.RequestNumber, dr.CreatedBy
            FROM DocumentRequests dr
            WHERE dr.Id = (SELECT EntityId FROM WorkflowExecutions WHERE Id = @ExecutionId)", new { ExecutionId = executionId }, tx);
            
        var approverInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"SELECT EmployeeName FROM Users WHERE Id = @UserId", new { UserId = input.UserId }, tx);
        string approverName = Convert.ToString(approverInfo?.employeename) ?? input.UserId.ToString();
        
        var notifyPlaceholders = new Dictionary<string, string>
        {
            { "ID", Convert.ToString(requestInfo?.requestnumber) ?? "Unknown" },
            { "Approver", approverName },
            { "Observation", input.Observation ?? "" }
        };

        int initiatorId = 0;
        if (requestInfo != null && requestInfo.createdby != null)
        {
            int.TryParse(Convert.ToString(requestInfo.createdby), out initiatorId);
        }

        //-------------------------------------------------
        // 3️⃣ Count pending BEFORE approving
        //-------------------------------------------------

        var pending = await _common.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM WorkflowExecutionSteps
            WHERE WorkflowExecutionId = @ExecutionId
            AND StepOrder = @StepOrder
            AND Decision IS NULL;",
                new
                {
                    ExecutionId = executionId,
                    StepOrder = stepOrder
                }, tx);

        //-------------------------------------------------
        // 4️⃣ Update step
        //-------------------------------------------------

        await _common.ExecuteAsync(@"
            UPDATE WorkflowExecutionSteps
            SET
                Decision = @Decision,
                Observation = @Observation,
                ActionAt = NOW(),
                IsActive = CASE WHEN @Decision IS NULL THEN TRUE ELSE FALSE END
            WHERE Id = @StepId;",
            new
            {
                Decision = decision,
                input.Observation,
                input.StepId
            }, tx);

        //-------------------------------------------------
        // COMMENT ONLY → STOP
        //-------------------------------------------------

        if (decision == null)
        {
            await tx.CommitAsync();
            return true;
        }

        //-------------------------------------------------
        // REJECT → Cancel Workflow and mark Request as Rejected (Terminal)
        //-------------------------------------------------
        if (decision == "Rejected")
        {
            await _common.ExecuteAsync(@"
            UPDATE WorkflowExecutions
            SET Status = 'Cancelled'
            WHERE Id = @ExecutionId;",
                new { ExecutionId = executionId }, tx);

            await _common.ExecuteAsync(@"
            UPDATE DocumentRequests
            SET Status = @RejectedStatus
            WHERE Id =
            (
                SELECT EntityId
                FROM WorkflowExecutions
                WHERE Id = @ExecutionId
            );",
                new { ExecutionId = executionId, RejectedStatus = DocumentRequestStatus.Rejected }, tx); // Or whatever your enum uses for Rejected

            await tx.CommitAsync();

            if (initiatorId > 0)
            {
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestRejected, input.CompanyId, (int)requestInfo!.id, initiatorId, notifyPlaceholders);
            }

            return true;
        }
        
        //-------------------------------------------------
        // REWORK → Cancel Workflow and Revert to Draft (Non-Terminal)
        //-------------------------------------------------
        if (decision == "Rework")
        {
            await _common.ExecuteAsync(@"
            UPDATE WorkflowExecutions
            SET Status = 'Cancelled'
            WHERE Id = @ExecutionId;",
                new { ExecutionId = executionId }, tx);

            await _common.ExecuteAsync(@"
            UPDATE DocumentRequests
            SET Status = @DraftStatus
            WHERE Id =
            (
                SELECT EntityId
                FROM WorkflowExecutions
                WHERE Id = @ExecutionId
            );",
                new { ExecutionId = executionId, DraftStatus = DocumentRequestStatus.Draft }, tx);

            await tx.CommitAsync();

            if (initiatorId > 0)
            {
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestRevertedForRework, input.CompanyId, (int)requestInfo!.id, initiatorId, notifyPlaceholders);
            }

            return true;
        }

        //-------------------------------------------------
        // APPROVE → If last approver in group
        //-------------------------------------------------

        int? nextStepUserId = null;
        if (pending == 1) // YOU were the final approver
        {
            //-------------------------------------------------
            // Find next step (NO JOIN — execution only)
            //-------------------------------------------------

            var next = await _common.ExecuteScalarAsync<int?>(@"
                SELECT MIN(StepOrder)
                FROM WorkflowExecutionSteps
                WHERE WorkflowExecutionId = @ExecutionId
                AND StepOrder > @Current;",
                    new
                    {
                        ExecutionId = executionId,
                        Current = stepOrder
                    }, tx);

            if (next.HasValue)
            {
                //-------------------------------------------------
                // Activate next step
                //-------------------------------------------------

                var rows = await _common.ExecuteAsync(@"
                    UPDATE WorkflowExecutionSteps
                    SET IsActive = TRUE
                    WHERE WorkflowExecutionId = @ExecutionId
                    AND StepOrder = @Next;",
                        new
                        {
                            ExecutionId = executionId,
                            Next = next.Value
                        }, tx);

                if (rows == 0)
                    throw new Exception("Workflow activation failed. Next step not found.");

                var nextStepInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"SELECT AssignedUserId FROM WorkflowExecutionSteps WHERE WorkflowExecutionId = @ExecutionId AND StepOrder = @Next;", new { ExecutionId = executionId, Next = next.Value }, tx);
                if (nextStepInfo != null && nextStepInfo.assigneduserid != null)
                {
                    nextStepUserId = (int)nextStepInfo.assigneduserid;
                }
            }
            else
            {
                //-------------------------------------------------
                // COMPLETE WORKFLOW
                //-------------------------------------------------

                await _common.ExecuteAsync(@"
                    UPDATE WorkflowExecutions
                    SET Status = 'Completed',
                        CompletedAt = NOW()
                    WHERE Id = @ExecutionId;",
                        new { ExecutionId = executionId }, tx);

                await _common.ExecuteAsync(@"
                    UPDATE DocumentRequests
                    SET Status = @Approved
                    WHERE Id =
                    (
                        SELECT EntityId
                        FROM WorkflowExecutions
                        WHERE Id = @ExecutionId
                    );",
                    new
                    {
                        ExecutionId = executionId,
                        Approved = DocumentRequestStatus.Approved
                    }, tx);


                //-------------------------------------------------
                // COMPLETE WORKFLOW
                //-------------------------------------------------
                var requestId = await _common.ExecuteScalarAsync<int>(@"
                    SELECT EntityId
                    FROM WorkflowExecutions
                    WHERE Id = @ExecutionId",
                    new { ExecutionId = executionId }, tx);

                await CreateDocumentFromApprovedRequestAsync(
                    input.CompanyId,
                    requestId,
                    input.UserId,
                    tx);


                //await CreateDocumentFromApprovedRequestAsync(input.CompanyId, executionId, input.UserId, tx);
            }
        }

        await tx.CommitAsync();

        if (nextStepUserId.HasValue && requestInfo != null)
        {
            await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestApprovedForwarded, input.CompanyId, (int)requestInfo.id, nextStepUserId.Value, notifyPlaceholders);
        }

        return true;
    }


    public async Task<IEnumerable<MyRequestPendingDto>> GetMyRequestsPendingApprovalAsync(MyRequestFilterDto filter)
    {
        var userId = await GetEmployeeID(filter.Initiator);
        filter.Initiator = userId.ToString();
        var sql = @"
            SELECT 
            dr.*,

            wes.StepOrder        AS CurrentStepOrder,
            wsd.StepType         AS CurrentStepType,
            COALESCE(u.EmployeeName, r.Name) AS CurrentAssignedUser,
            wes.AssignedUserId   AS CurrentAssignedUserId,
            wes.AssignedRoleId   AS CurrentAssignedRoleId

        FROM Vw_DocumentRequests dr

        LEFT JOIN WorkflowExecutions we
            ON we.CompanyId = dr.CompanyId
            AND we.EntityId = dr.Id
            AND we.EntityType = 'Request'
            --AND we.Status = 'Running'
            AND we.Id = (
                SELECT MAX(Id)
                FROM WorkflowExecutions we2
                WHERE we2.CompanyId = dr.CompanyId
                  AND we2.EntityId = dr.Id
                  AND we2.EntityType = 'Request'
            )

        LEFT JOIN WorkflowExecutionSteps wes
            ON wes.CompanyId = we.CompanyId
            AND wes.WorkflowExecutionId = we.Id
            --AND wes.IsActive = TRUE               -- keep, but maybe add ORDER BY / LIMIT if multi-steps possible

        LEFT JOIN USERS u ON wes.AssignedUserId = u.Id
        LEFT JOIN Roles r ON wes.AssignedRoleId = r.Id
        LEFT JOIN WorkflowStepDefinitions wsd 
            ON wsd.CompanyId = wes.CompanyId
            AND wsd.Id = wes.StepDefinitionId

            WHERE dr.CompanyId = @CompanyId
              AND dr.SubmittedBy = @Initiator
              AND dr.IsDeleted = FALSE

              AND (@DivisionCode IS NULL OR dr.DivisionCode = @DivisionCode)
              AND (@DepartmentCode IS NULL OR dr.DepartmentCode = @DepartmentCode)
              AND (@Status IS NULL OR dr.Status = @Status)

            ORDER BY dr.SubmittedAt DESC
            ";

        //return await _common.QueryAsync<MyRequestPendingDto>(sql, filter);
        var results = await _common.QueryAsync<dynamic>(sql, filter);

        var dtos = new List<MyRequestPendingDto>();
        foreach (var row in results)
        {
            // Cast row to IDictionary<string, object> to access properties safely
            var rowDict = row as IDictionary<string, object>;

            dtos.Add(new MyRequestPendingDto
            {
                // Request Details
                RequestId = GetValue<int>(rowDict, "id"),
                RequestNumber = GetValue<string>(rowDict, "requestnumber"),
                DocumentName = GetValue<string>(rowDict, "documentname"),
                Justification = GetValue<string>(rowDict, "justification"),
                ProposedContent = GetValue<string>(rowDict, "proposedcontent"),
                DraftFileUrl = GetValue<string>(rowDict, "draftfileurl"),
                Status = GetValue<int>(rowDict, "status"),
                SubmittedAt = GetValue<DateTime?>(rowDict, "submittedat"),

                // Organizational Details
                DivisionCode = GetValue<string>(rowDict, "divisioncode"),
                Division = GetValue<string>(rowDict, "division"),
                DepartmentCode = GetValue<string>(rowDict, "departmentcode"),
                Department = GetValue<string>(rowDict, "department"),
                SubDepartmentCode = GetValue<string>(rowDict, "subdepartmentcode"),
                SubDepartment = GetValue<string>(rowDict, "subdepartment"),
                DocumentTypeCode = GetValue<string>(rowDict, "documenttypecode"),
                DocumentType = GetValue<string>(rowDict, "documenttype"),
                CreatedAt = GetValue<string>(rowDict, "createdat"),
                CreatedBy = GetValue<string>(rowDict, "createdby"),

                // Workflow Progress Details
                CurrentStepOrder = GetValue<int>(rowDict, "currentsteporder"),
                CurrentStepType = GetValue<string>(rowDict, "currentsteptype"),
                CurrentAssignedUser = GetValue<string>(rowDict, "currentassigneduser"),
                CurrentAssignedUserId = GetValue<int?>(rowDict, "currentassigneduserid"),
                CurrentAssignedRoleId = GetValue<int?>(rowDict, "currentassignedroleid")
            });
        }

        return dtos;
    }


    public async Task<IEnumerable<DocumentRequestDetailsDto>> GetRequestDetailsAsync(int companyId, int documentId, string entityType)
    {

        var sql = $@"
        SELECT 
            we.EntityId,
            we.EntityType,
            wes.StepOrder,
            wsd.StepType,
            wes.AssignedUserId,
            u.EmployeeName,
            u.EmployeeCode,
	        u.Division,
	        u.Department,
	        u.SubDepartment,
	        u.Designation,
            r.Name AS RoleName,
            wes.Decision,
            wes.Observation,
            wes.ActionAt,
            wes.IsActive
        FROM WorkflowExecutionSteps wes
        INNER JOIN WorkflowExecutions we
            ON we.CompanyId = wes.CompanyId
            AND we.Id = wes.WorkflowExecutionId
            AND we.EntityType = @EntityType  -- Critical filter: Only get steps from Document workflow
        INNER JOIN WorkflowStepDefinitions wsd
            ON wsd.CompanyId = wes.CompanyId
            AND wsd.Id = wes.StepDefinitionId
        LEFT JOIN vw_users u
            ON u.Id = wes.AssignedUserId
        LEFT JOIN Roles r
            ON r.Id = wes.AssignedRoleId
        WHERE wes.CompanyId = @CompanyId
          AND we.EntityId = @DocumentId  -- The Document ID
          AND we.EntityType = @EntityType  -- Should be 'Document'
        ORDER BY wes.StepOrder";

        return await _common.QueryAsync<DocumentRequestDetailsDto>(sql, new
        {
            companyId,
            documentId,  // Renamed from requestId for clarity
            entityType   // Should be "Document"
        });

        //var query = $@"
        //    -----------------------------------------------------
        //    -- 1️⃣ Document Request
        //    -----------------------------------------------------
        //    SELECT *
        //    FROM Vw_DocumentRequests
        //    WHERE CompanyId = {companyId}
        //      AND Id = {requestId}
        //      --AND SubmittedBy = '{initiator}'
        //      AND IsDeleted = FALSE;

        //    -----------------------------------------------------
        //    -- 2️⃣ Workflow Execution
        //    -----------------------------------------------------
        //    SELECT *
        //    FROM WorkflowExecutions
        //    WHERE CompanyId = {companyId}
        //      AND EntityId = {requestId}
        //      AND EntityType = 'Request'
        //    ORDER BY Id DESC
        //    LIMIT 1;

        //    -----------------------------------------------------
        //    -- 3️⃣ Workflow Steps
        //    -----------------------------------------------------
        //    SELECT 
        //        wes.StepOrder,
        //        wsd.StepType,
        //        wes.AssignedUserId,
        //        wes.AssignedRoleId,
        //        wes.Decision,
        //        wes.Observation,
        //        wes.ActionAt,
        //        wes.IsActive,
        //        u.EmployeeName
        //    FROM WorkflowExecutionSteps wes
        //    INNER JOIN WorkflowExecutions we
        //        ON we.CompanyId = wes.CompanyId
        //        AND we.Id = wes.WorkflowExecutionId
        //    INNER JOIN WorkflowStepDefinitions wsd
        //        ON wsd.CompanyId = wes.CompanyId
        //        AND wsd.Id = wes.StepDefinitionId
        //    INNER JOIN Users u
        //     ON wsd.UserId = u.Id
        //    WHERE wes.CompanyId = {companyId}
        //      AND we.EntityId = {requestId}
        //      AND we.EntityType = 'Request'
        //    ORDER BY wes.StepOrder;
        //    ";

        //var dataSet = await _common.ExecuteSqlQueryMultiple(query);

        //if (dataSet.Tables.Count < 1 || dataSet.Tables[0].Rows.Count == 0)
        //    return null;

        ////-----------------------------------------------------
        //// 1️⃣ Map DocumentRequest
        ////-----------------------------------------------------
        //var drRow = dataSet.Tables[0].Rows[0];

        //var result = new DocumentRequestDetailsDto
        //{
        //    Id = Convert.ToInt32(drRow["Id"]),
        //    RequestNumber = drRow["RequestNumber"]?.ToString(),
        //    DocumentName = drRow["DocumentName"]?.ToString(),
        //    Justification = drRow["Justification"]?.ToString(),
        //    ProposedContent = drRow["ProposedContent"]?.ToString(),
        //    Division = drRow["Division"]?.ToString(),
        //    DivisionCode = drRow["DivisionCode"]?.ToString(),
        //    Department = drRow["Department"]?.ToString(),
        //    DepartmentCode = drRow["DepartmentCode"]?.ToString(),
        //    SubDepartment = drRow["SubDepartment"]?.ToString(),
        //    SubDepartmentCode = drRow["SubDepartmentCode"]?.ToString(),
        //    BusinessDomain = drRow["BusinessDomain"]?.ToString(),
        //    BusinessDomainCode = drRow["BusinessDomainCode"]?.ToString(),
        //    SubmittedAt = drRow["SubmittedAt"] == DBNull.Value
        //                    ? null
        //                    : Convert.ToDateTime(drRow["SubmittedAt"]),
        //    SubmittedBy = drRow["SubmittedBy"]?.ToString(),
        //    Status = Convert.ToInt32(drRow["Status"])
        //};

        ////-----------------------------------------------------
        //// 2️⃣ Map WorkflowExecution
        ////-----------------------------------------------------
        //if (dataSet.Tables.Count > 1 && dataSet.Tables[1].Rows.Count > 0)
        //{
        //    var wfRow = dataSet.Tables[1].Rows[0];

        //    result.WorkflowStatus = wfRow["Status"]?.ToString();
        //    result.WorkflowStartedAt = wfRow["StartedAt"] == DBNull.Value
        //                                ? null
        //                                : Convert.ToDateTime(wfRow["StartedAt"]);
        //    result.WorkflowCompletedAt = wfRow["CompletedAt"] == DBNull.Value
        //                                ? null
        //                                : Convert.ToDateTime(wfRow["CompletedAt"]);
        //}

        ////-----------------------------------------------------
        //// 3️⃣ Map Workflow Steps
        ////-----------------------------------------------------
        //if (dataSet.Tables.Count > 2 && dataSet.Tables[2].Rows.Count > 0)
        //{
        //    foreach (DataRow row in dataSet.Tables[2].Rows)
        //    {
        //        result.Steps.Add(new WorkflowStepHistoryDto
        //        {
        //            UserName = row["EmployeeName"]?.ToString(),
        //            StepOrder = Convert.ToInt32(row["StepOrder"]),
        //            StepType = row["StepType"]?.ToString(),
        //            AssignedUserId = row["AssignedUserId"] == DBNull.Value
        //                                ? null
        //                                : Convert.ToInt32(row["AssignedUserId"]),
        //            AssignedRoleId = row["AssignedRoleId"] == DBNull.Value
        //                                ? null
        //                                : Convert.ToInt32(row["AssignedRoleId"]),
        //            Decision = row["Decision"] == DBNull.Value
        //                                ? null
        //                                : row["Decision"].ToString(),
        //            Observation = row["Observation"]?.ToString(),
        //            ActionAt = row["ActionAt"] == DBNull.Value
        //                                ? null
        //                                : Convert.ToDateTime(row["ActionAt"]),
        //            IsActive = Convert.ToBoolean(row["IsActive"])
        //        });
        //    }
        //}

        //return result;
    }


    public async Task<IEnumerable<DocumentRequestDetailsDto>> GetWorkflowDetailsAsync(
    int companyId,
    int entityId,  // This could be either RequestId or DocumentId
    string entityType)  // "Request" or "Document"
    {
        var sql = $@"
        SELECT 
            we.EntityId,
            we.EntityType,
            wes.StepOrder,
            wsd.StepType,
            wes.AssignedUserId,
            u.EmployeeName,
            u.EmployeeCode,
            r.Name AS RoleName,
            wes.Decision,
            wes.Observation,
            wes.ActionAt AS StatusUpdatedOn,
            wes.IsActive,
            we.Status AS ExecutionStatus,
            we.StartedAt,
            we.CompletedAt,
            COALESCE(wes.ActionAt, we.StartedAt) AS ReceivedOn
        FROM WorkflowExecutionSteps wes
        INNER JOIN WorkflowExecutions we
            ON we.CompanyId = wes.CompanyId
            AND we.Id = wes.WorkflowExecutionId
        INNER JOIN WorkflowStepDefinitions wsd
            ON wsd.CompanyId = wes.CompanyId
            AND wsd.Id = wes.StepDefinitionId
        LEFT JOIN Users u
            ON u.Id = wes.AssignedUserId
        LEFT JOIN Roles r
            ON r.Id = wes.AssignedRoleId
        WHERE wes.CompanyId = @CompanyId
          AND we.EntityId = @EntityId
          AND we.EntityType = @EntityType
        ORDER BY wes.StepOrder";

        return await _common.QueryAsync<DocumentRequestDetailsDto>(sql, new
        {
            companyId,
            entityId,
            entityType
        });
    }

    // Helper method to safely get values from the dynamic row
    private static T GetValue<T>(IDictionary<string, object> row, string columnName)
    {
        if (row.ContainsKey(columnName) && row[columnName] != DBNull.Value)
        {
            try
            {
                var value = row[columnName];

                // Handle type conversions
                if (typeof(T) == typeof(int?) || typeof(T) == typeof(int))
                {
                    if (value is int intValue)
                        return (T)(object)intValue;
                    if (value is long longValue)
                        return (T)(object)(int)longValue;
                    if (value is decimal decimalValue)
                        return (T)(object)(int)decimalValue;
                }

                if (typeof(T) == typeof(string) && value != null)
                    return (T)(object)value.ToString();

                return (T)value;
            }
            catch
            {
                return default(T);
            }
        }
        return default(T);
    }

    private async Task<int> GetEmployeeID(string employeeCode)
    {
        await using var tx = await _common.BeginTransactionAsync();

        var emplId = await _common.ExecuteScalarAsync<int>(@"
            Select Id from Users Where EmployeeCode = @EmployeeCode ",
                new
                {
                    EmployeeCode = employeeCode
                },
                tx);

        return emplId;
    }


    /* 
         Draft Request
             ↓
         User edits Content + Users + Distribution
             ↓
         Submit (Frozen)
             ↓
         Approval
             ↓
         Create Document
             ↓
         PROMOTE Audience ✅
             ↓
         Document Draft 0.1 Ready */
    public async Task<int> CreateDocumentFromApprovedRequestAsync(int companyId, int requestId, long userId, NpgsqlTransaction transaction)
    {
        //await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            //-----------------------------------------
            // 1️⃣ Get Approved Request
            //-----------------------------------------
            var request = await _common.QuerySingleAsync<dynamic>(@"
                SELECT *
                FROM DocumentRequests
                WHERE Id = @RequestId
                  AND CompanyId = @CompanyId
                  AND Status = 3 -- Approved
                  AND DocumentId IS NULL
                ", new { requestId, companyId }, transaction);

            if (request == null)
                throw new Exception("Approved request not found");

            //-----------------------------------------
            // 2️⃣ Create Document
            //-----------------------------------------
            var documentId = await _common.ExecuteScalarAsync<int>(@"
                INSERT INTO Documents
                (
                    CompanyId,
                    DocumentNumber,
                    RequestId,
                    DocumentTypeCode,
                    Title,
                    DivisionCode,
                    DepartmentCode,
                    SubDepartmentCode,
                    BusinessDomainCode,
                    DocumentURL,
                    CreatedBy,
                    LastModifiedBy
                )
                VALUES
                (
                    @CompanyId,
                    'DOC-' || nextval('document_seq'),
                    @RequestId,
                    @DocumentTypeCode,
                    @Title,
                    @DivisionCode,
                    @DepartmentCode,
                    @SubDepartmentCode,
                    @BusinessDomainCode,
                    @DocumentUrl,
                    @UserId,
                    @UserId
                )
                RETURNING Id
                ", new
            {
                companyId,
                requestId,
                DocumentTypeCode = request.documenttypecode,
                Title = request.documentname,
                request.divisioncode,
                request.departmentcode,
                request.subdepartmentcode,
                request.businessdomaincode,
                DocumentUrl = request.draftfileurl,
                userId
            }, transaction);

            //-----------------------------------------
            // 3️⃣ Create Draft Version 0.1
            //-----------------------------------------
            await _common.ExecuteAsync(@"
            INSERT INTO DocumentVersions
            (
                CompanyId,
                DocumentId,
                Version,
                VersionType,
                Content,
                CreatedBy,
                LastModifiedBy
            )
            VALUES
            (
                @CompanyId,
                @DocumentId,
                '0.1',
                1,
                @Content,
                @UserId,
                @UserId
            )
            ", new
            {
                companyId,
                documentId,
                Content = request.proposedcontent,
                userId
            }, transaction);

            //-----------------------------------------
            // 4️⃣ Insert Draft State
            //-----------------------------------------
            await _common.ExecuteAsync(@"
            INSERT INTO DocumentStateHistory
            (
                CompanyId,
                DocumentId,
                ToStateId,
                ChangedBy
            )
            VALUES
            (
                @CompanyId,
                @DocumentId,
                1,
                @UserId
            )
            ", new { companyId, documentId, userId }, transaction);

            //-----------------------------------------
            // 5️⃣ Link Back To Request
            //-----------------------------------------
            await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET DocumentId = @DocumentId
                WHERE Id = @RequestId
                ", new { documentId, requestId }, transaction);

            //-----------------------------------------
            // 6️⃣ Promote Role Distribution
            //-----------------------------------------
            await _common.ExecuteAsync(@"
                INSERT INTO DocumentRoleDistributions
                (
                    CompanyId, DocumentId, DivisionCode, DepartmentCode, SubDepartmentCode,
                    BusinessDomainCode, RoleId, DistributionType, CreatedBy
                )
                SELECT
                    CompanyId, @DocumentId, DivisionCode, DepartmentCode, SubDepartmentCode,
                    BusinessDomainCode, RoleId, DistributionTypeId, @UserId
                FROM DocumentRequestRoleDistributions
                WHERE DocumentRequestId = @RequestId;",
            new
            {
                DocumentId = documentId,
                RequestId = requestId,
                UserId = userId
            }, transaction);

            //-----------------------------------------
            // 7️⃣ Promote User Distribution
            //-----------------------------------------
            await _common.ExecuteAsync(@"
                INSERT INTO DocumentUserDistributions
                (
                    CompanyId, DocumentId, UserId, CreatedBy
                )
                SELECT
                    CompanyId, @DocumentId, UserId, @UserId
                FROM DocumentRequestUserDistributions
                WHERE DocumentRequestId = @RequestId;",
            new
            {
                DocumentId = documentId,
                RequestId = requestId,
                UserId = userId
            }, transaction);

            //await transaction.CommitAsync();
            return documentId;
        }
        catch
        {
            //await transaction.RollbackAsync();
            throw;
        }
    }


    // this method is obselute, need to be deleted after evry thing works well.
    //public async Task<long> CreateDocumentRequestAsync(DraftDocumentRequestDto dto)
    //{ 
    //    await using var transaction = await _common.BeginTransactionAsync();
    //    try
    //    {
    //        //-------------------------------------------------
    //        // 1️⃣ Generate Request Number
    //        //-------------------------------------------------
    //        var requestNumber = $"DR-{DateTime.UtcNow:yyyyMMddHHmmss}";

    //        //-------------------------------------------------
    //        // 2️⃣ Insert Document Request
    //        //-------------------------------------------------
    //        var insertRequestSql = @"
    //            INSERT INTO DocumentRequests
    //            (
    //                CompanyId,
    //                RequestNumber,
    //                DocumentRequestTypeCode,
    //                DocumentTypeCode,
    //                DocumentName,
    //                Justification,
    //                ProposedContent,
    //                DivisionCode,
    //                DepartmentCode,
    //                SubDepartmentCode,
    //                BusinessDomainCode,
    //                Status,
    //                CurrentStep,
    //                CreatedBy,
    //                LastModifiedBy
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @RequestNumber,
    //                @DocumentRequestTypeCode,
    //                @DocumentTypeCode,
    //                @DocumentName,
    //                @Justification,
    //                @ProposedContent,
    //                @DivisionCode,
    //                @DepartmentCode,
    //                @SubDepartmentCode,
    //                @BusinessDomainCode,
    //                1, -- Draft / Pending
    //                1,
    //                @CreatedBy,
    //                @CreatedBy
    //            )
    //            RETURNING Id;
    //            ";

    //        var requestId = await _common.ExecuteScalarAsync<long>(
    //            insertRequestSql,
    //            new
    //            {
    //                dto.CompanyId,
    //                requestNumber,
    //                dto.DocumentRequestTypeCode,
    //                dto.DocumentTypeCode,
    //                dto.DocumentName,
    //                dto.Justification,
    //                dto.ProposedContent,
    //                dto.DivisionCode,
    //                dto.DepartmentCode,
    //                dto.SubDepartmentCode,
    //                dto.BusinessDomainCode,
    //                CreatedBy = dto.CreatedByUserId.ToString()
    //            },
    //            transaction
    //        );

    //        //-------------------------------------------------
    //        // 3️⃣ Resolve Workflow Policy
    //        //-------------------------------------------------
    //        var policySql = @"
    //            SELECT Id
    //            FROM WorkflowPolicies
    //            WHERE CompanyId = @CompanyId
    //            AND DocumentTypeCode =
    //                (SELECT Code FROM DocumentTypes
    //                 WHERE CompanyId = @CompanyId
    //                 AND Code = @DocumentTypeCode)
    //            AND IsActive = TRUE
    //            LIMIT 1;
    //            ";

    //        var policyId = await _common.ExecuteScalarAsync<int?>(
    //            policySql,
    //            new { dto.CompanyId, dto.DocumentTypeCode },
    //            transaction
    //        );

    //        if (!policyId.HasValue)
    //            throw new Exception("No active workflow policy found.");

    //        //-------------------------------------------------
    //        // 4️⃣ Get Active Policy Version
    //        //-------------------------------------------------
    //        var versionSql = @"
    //            SELECT Id
    //            FROM WorkflowPolicyVersions
    //            WHERE CompanyId = @CompanyId
    //            AND WorkflowPolicyId = @PolicyId
    //            AND IsActive = TRUE
    //            LIMIT 1;
    //            ";

    //        var policyVersionId = await _common.ExecuteScalarAsync<long?>(
    //            versionSql,
    //            new { dto.CompanyId, PolicyId = policyId },
    //            transaction
    //        );

    //        if (!policyVersionId.HasValue)
    //            throw new Exception("No active workflow version found.");

    //        //-------------------------------------------------
    //        // 5️⃣ Create Workflow Execution
    //        //-------------------------------------------------
    //        var executionSql = @"
    //            INSERT INTO WorkflowExecutions
    //            (
    //                CompanyId,
    //                WorkflowPolicyVersionId,
    //                EntityType,
    //                EntityId,
    //                Status,
    //                CurrentStep,
    //                StartedBy
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @VersionId,
    //                'Request',
    //                @EntityId,
    //                'Running',
    //                1,
    //                @StartedBy
    //            )
    //            RETURNING Id;
    //            ";

    //        var executionId = await _common.ExecuteScalarAsync<long>(
    //            executionSql,
    //            new
    //            {
    //                dto.CompanyId,
    //                VersionId = policyVersionId,
    //                EntityId = requestId,
    //                StartedBy = dto.CreatedByUserId
    //            },
    //            transaction
    //        );

    //        //-------------------------------------------------
    //        // 6️⃣ Generate Execution Steps
    //        //-------------------------------------------------
    //        var stepsSql = @"
    //            INSERT INTO WorkflowExecutionSteps
    //            (
    //                CompanyId,
    //                WorkflowExecutionId,
    //                StepDefinitionId,
    //                AssignedUserId,
    //                AssignedRoleId,
    //                Observation
    //            )
    //            SELECT
    //                @CompanyId,
    //                @ExecutionId,
    //                wsd.Id,
    //                wsd.UserId,
    //                wsd.RoleId,
    //                ''
    //            FROM WorkflowStepDefinitions wsd
    //            WHERE wsd.WorkflowPolicyVersionId = @VersionId
    //            ORDER BY wsd.StepOrder;
    //            ";

    //        await _common.ExecuteAsync(
    //            stepsSql,
    //            new
    //            {
    //                dto.CompanyId,
    //                ExecutionId = executionId,
    //                VersionId = policyVersionId
    //            },
    //            transaction
    //        );

    //        //-------------------------------------------------
    //        // 7️⃣ Insert Request History
    //        //-------------------------------------------------
    //        var historySql = @"
    //            INSERT INTO DocumentRequestHistory
    //            (
    //                CompanyId,
    //                RequestId,
    //                ToStateId,
    //                ChangedBy,
    //                WorkflowExecutionId,
    //                Comments
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @RequestId,
    //                1, -- Draft
    //                @ChangedBy,
    //                @ExecutionId,
    //                'Request Created'
    //            );
    //            ";

    //        await _common.ExecuteAsync(
    //            historySql,
    //            new
    //            {
    //                dto.CompanyId,
    //                RequestId = requestId,
    //                ChangedBy = dto.CreatedByUserId,
    //                ExecutionId = executionId
    //            },
    //            transaction
    //        );

    //        //-------------------------------------------------
    //        // ✅ Commit
    //        //-------------------------------------------------
    //        transaction.Commit();

    //        return requestId;
    //    }
    //    catch
    //    {
    //        transaction.Rollback();
    //        throw;
    //    }
    //}


    public async Task<bool> DeleteAsync(string code)
    {
        try
        {
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM DocumentRequests
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentRequests not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentRequests
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentRequestReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE d.IsDeleted = False 
                  AND d.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(d.Name) LIKE '%{search}%'
                    OR UPPER(d.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "CODE" => "d.Id",
                "ISACTIVE" => "d.IsActive",
                _ => "d.ID"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT * FROM Vw_DocumentRequests d
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DocumentRequests d
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentRequestReadDto>
                {
                    Items = new List<DocumentRequestReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentRequestReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    RequestNumber = row.Table.Columns.Contains("RequestNumber") ? row.Field<string>("RequestNumber") : string.Empty,
                    DocumentRequestTypeCode = row.Table.Columns.Contains("DocumentRequestTypeCode") ? row.Field<string>("DocumentRequestTypeCode") : string.Empty,
                    DocumentId = row.Table.Columns.Contains("DocumentId") && !row.IsNull("DocumentId") ? row.Field<int>("DocumentId") : 0,
                    DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

                    Division = row.Field<string>("Division"),
                    DivisionCode = row.Field<string>("DivisionCode"),

                    Department = row.Field<string>("Department"),
                    DepartmentCode = row.Field<string>("DepartmentCode"),

                    SubDepartment = row.Field<string>("SubDepartment"),
                    SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                    BusinessDomain = row.Field<string>("BusinessDomain"),
                    BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                    DocumentName = row.Table.Columns.Contains("DocumentName") ? row.Field<string>("DocumentName") : string.Empty,
                    Justification = row.Table.Columns.Contains("Justification") ? row.Field<string>("Justification") : string.Empty,
                    Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                    RowVersion = row.Table.Columns.Contains("RowVersion") ? row.Field<string>("RowVersion") : string.Empty,
                    ProposedContent = row.Table.Columns.Contains("ProposedContent") ? row.Field<string>("ProposedContent") : string.Empty,
                    DraftFileUrl = row.Table.Columns.Contains("DraftFileUrl") ? row.Field<string>("DraftFileUrl") : string.Empty,
                    IsContentFinalized = row.Table.Columns.Contains("IsContentFinalized") && row.Field<bool?>("IsContentFinalized") == true,
                    DraftContentLastModifiedAt = (row.Table.Columns.Contains("DraftContentLastModifiedAt") && !row.IsNull("DraftContentLastModifiedAt"))
                                     ? row.Field<DateTime>("DraftContentLastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    DraftContentLastModifiedBy = row.Table.Columns.Contains("DraftContentLastModifiedBy") ? row.Field<string>("DraftContentLastModifiedBy") : string.Empty,

                    IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                    IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                    LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<DocumentRequestReadDto>
            {
                Items = divisions,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<IQueryable<SelectList2Dto>> GetAllSelectList()
    {
        try
        {
            string query = @"
            SELECT dr.Id, dr.RequestNumber
            FROM DocumentRequests dr
            JOIN WorkflowExecutions we
              ON we.EntityId = dr.Id
              AND we.CompanyId = dr.CompanyId
            WHERE dr.IsActive = TRUE
              AND dr.IsDeleted = FALSE
              AND we.Status = 'Completed'
            ORDER BY dr.Id;";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("Id"),
                    Value = row.Field<string>("RequestNumber")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentRequestReadDto> GetByIdAsync(int id)
    {
        try
        {
            string query = $@"
                SELECT * FROM Vw_DocumentRequests d
                WHERE d.Id = {id}
                  AND d.IsActive = True
                  AND d.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentRequests not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                RequestNumber = row.Table.Columns.Contains("RequestNumber") ? row.Field<string>("RequestNumber") : string.Empty,
                DocumentRequestTypeCode = row.Table.Columns.Contains("DocumentRequestTypeCode") ? row.Field<string>("DocumentRequestTypeCode") : string.Empty,
                DocumentId = row.Table.Columns.Contains("DocumentId") && !row.IsNull("DocumentId") ? row.Field<int>("DocumentId") : 0,
                DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Table.Columns.Contains("DocumentName") ? row.Field<string>("DocumentName") : string.Empty,
                Justification = row.Table.Columns.Contains("Justification") ? row.Field<string>("Justification") : string.Empty,
                Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                RowVersion = row.Table.Columns.Contains("RowVersion") ? row.Field<string>("RowVersion") : string.Empty,
                ProposedContent = row.Table.Columns.Contains("ProposedContent") ? row.Field<string>("ProposedContent") : string.Empty,
                DraftFileUrl = row.Table.Columns.Contains("DraftFileUrl") ? row.Field<string>("DraftFileUrl") : string.Empty,
                IsContentFinalized = row.Table.Columns.Contains("IsContentFinalized") && row.Field<bool?>("IsContentFinalized") == true,
                DraftContentLastModifiedAt = (row.Table.Columns.Contains("DraftContentLastModifiedAt") && !row.IsNull("DraftContentLastModifiedAt"))
                                     ? row.Field<DateTime>("DraftContentLastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                DraftContentLastModifiedBy = row.Table.Columns.Contains("DraftContentLastModifiedBy") ? row.Field<string>("DraftContentLastModifiedBy") : string.Empty,

                IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentRequestReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT * FROM Vw_DocumentRequests d
                WHERE d.DivisionCode = {dCode}
                  AND d.IsActive = True
                  AND d.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentRequests not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                RequestNumber = row.Table.Columns.Contains("RequestNumber") ? row.Field<string>("RequestNumber") : string.Empty,
                DocumentRequestTypeCode = row.Table.Columns.Contains("DocumentRequestTypeCode") ? row.Field<string>("DocumentRequestTypeCode") : string.Empty,
                DocumentId = row.Table.Columns.Contains("DocumentId") && !row.IsNull("DocumentId") ? row.Field<int>("DocumentId") : 0,
                DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Table.Columns.Contains("DocumentName") ? row.Field<string>("DocumentName") : string.Empty,
                Justification = row.Table.Columns.Contains("Justification") ? row.Field<string>("Justification") : string.Empty,
                Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                RowVersion = row.Table.Columns.Contains("RowVersion") ? row.Field<string>("RowVersion") : string.Empty,
                ProposedContent = row.Table.Columns.Contains("ProposedContent") ? row.Field<string>("ProposedContent") : string.Empty,
                DraftFileUrl = row.Table.Columns.Contains("DraftFileUrl") ? row.Field<string>("DraftFileUrl") : string.Empty,
                IsContentFinalized = row.Table.Columns.Contains("IsContentFinalized") && row.Field<bool?>("IsContentFinalized") == true,
                DraftContentLastModifiedAt = (row.Table.Columns.Contains("DraftContentLastModifiedAt") && !row.IsNull("DraftContentLastModifiedAt"))
                                     ? row.Field<DateTime>("DraftContentLastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                DraftContentLastModifiedBy = row.Table.Columns.Contains("DraftContentLastModifiedBy") ? row.Field<string>("DraftContentLastModifiedBy") : string.Empty,

                IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentRequestReadDto> UpdateAsync(DocumentRequestUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentRequests
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentRequests not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DocumentRequests
            SET 
                RequestType = '{input.RequestType}',
                DocumentTypeId = '{input.DocumentTypeId}',
                DivisionCode = '{input.DivisionCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}',
                BusinessDomainCode = '{input.BusinessDomainCode}',
                DocumentName = '{input.DocumentName}',
                Justification = '{input.Justification}',
                Status = '{input.Status}',
                CurrentStep = '{input.CurrentStep}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                SELECT * FROM Vw_DocumentRequests d
            WHERE d.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentRequestReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                RequestNumber = row.Table.Columns.Contains("RequestNumber") ? row.Field<string>("RequestNumber") : string.Empty,
                DocumentRequestTypeCode = row.Table.Columns.Contains("DocumentRequestTypeCode") ? row.Field<string>("DocumentRequestTypeCode") : string.Empty,
                DocumentId = row.Table.Columns.Contains("DocumentId") && !row.IsNull("DocumentId") ? row.Field<int>("DocumentId") : 0,
                DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Table.Columns.Contains("DocumentName") ? row.Field<string>("DocumentName") : string.Empty,
                Justification = row.Table.Columns.Contains("Justification") ? row.Field<string>("Justification") : string.Empty,
                Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                RowVersion = row.Table.Columns.Contains("RowVersion") ? row.Field<string>("RowVersion") : string.Empty,
                ProposedContent = row.Table.Columns.Contains("ProposedContent") ? row.Field<string>("ProposedContent") : string.Empty,
                DraftFileUrl = row.Table.Columns.Contains("DraftFileUrl") ? row.Field<string>("DraftFileUrl") : string.Empty,
                IsContentFinalized = row.Table.Columns.Contains("IsContentFinalized") && row.Field<bool?>("IsContentFinalized") == true,
                DraftContentLastModifiedAt = (row.Table.Columns.Contains("DraftContentLastModifiedAt") && !row.IsNull("DraftContentLastModifiedAt"))
                                     ? row.Field<DateTime>("DraftContentLastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                DraftContentLastModifiedBy = row.Table.Columns.Contains("DraftContentLastModifiedBy") ? row.Field<string>("DraftContentLastModifiedBy") : string.Empty,

                IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
            };
        }
        catch
        {
            throw;
        }
    }



    //public async Task<long> SubmitDocumentRequestAsync(DocumentRequestCreateDto input)
    //{
    //    //await using var conn = new NpgsqlConnection(_connectionString);
    //    //await conn.OpenAsync();

    //    //await using var tx = await conn.BeginTransactionAsync();
    //    await using var tx = await _common.BeginTransactionAsync();
    //    try
    //    {
    //        //var companyId = _tenantProvider.CompanyId;
    //        //var userId = _currentUserProvider.UserId;
    //        var userId = "";
    //        var companyId = "";
    //        //------------------------------------------------
    //        // ✅ 1. Resolve ACTIVE Workflow Policy Version
    //        //------------------------------------------------

    //        var workflowVersionId = await _common.ExecuteScalarAsync<long?>(
    //        @"
    //            SELECT wpv.Id
    //            FROM WorkflowPolicyVersions wpv
    //            JOIN WorkflowPolicies wp 
    //                ON wp.Id = wpv.WorkflowPolicyId
    //            WHERE wp.CompanyId = @CompanyId
    //              AND wp.DocumentTypeId = @DocumentTypeId
    //              AND wpv.IsActive = TRUE
    //            LIMIT 1;
    //            ",
    //        new
    //        {
    //            CompanyId = companyId,
    //            input.DocumentTypeId
    //        }, tx);

    //        if (workflowVersionId == null)
    //            throw new CustomException("No active workflow configured for this document type.");

    //        //------------------------------------------------
    //        // ✅ 2. Insert DocumentRequest
    //        //------------------------------------------------

    //        var requestId = await _common.ExecuteScalarAsync<long>(
    //        @"
    //            INSERT INTO DocumentRequests
    //            (
    //                CompanyId,
    //                DocumentTypeId,
    //                RequestType,
    //                DocumentName,
    //                Justification,
    //                Status,
    //                CreatedBy,
    //                CreatedAt
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @DocumentTypeId,
    //                @RequestType,
    //                @DocumentName,
    //                @Justification,
    //                'Submitted',
    //                @UserId,
    //                NOW()
    //            )
    //            RETURNING Id;
    //            ",
    //        new
    //        {
    //            CompanyId = companyId,
    //            input.DocumentTypeId,
    //            input.RequestType, // Creation / Revision / Obsoletion
    //            input.DocumentName,
    //            input.Justification,
    //            UserId = userId
    //        }, tx);

    //        //------------------------------------------------
    //        // ✅ 3. Insert STATE HISTORY (CRITICAL)
    //        //------------------------------------------------

    //        await _common.ExecuteAsync(
    //        @"
    //            INSERT INTO RequestStateHistory
    //            (
    //                CompanyId,
    //                RequestId,
    //                State,
    //                ChangedBy
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @RequestId,
    //                'Submitted',
    //                @UserId
    //            );
    //            ",
    //        new
    //        {
    //            CompanyId = companyId,
    //            RequestId = requestId,
    //            UserId = userId
    //        }, tx);

    //        //------------------------------------------------
    //        // ✅ 4. Start Workflow Execution
    //        //------------------------------------------------

    //        var executionId = await _common.ExecuteScalarAsync<long>(
    //        @"
    //            INSERT INTO WorkflowExecutions
    //            (
    //                CompanyId,
    //                WorkflowPolicyVersionId,
    //                EntityType,
    //                EntityId,
    //                Status,
    //                StartedBy,
    //                StartedAt
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @WorkflowVersionId,
    //                'Request',
    //                @RequestId,
    //                'Running',
    //                @UserId,
    //                NOW()
    //            )
    //            RETURNING Id;
    //            ",
    //        new
    //        {
    //            CompanyId = companyId,
    //            WorkflowVersionId = workflowVersionId,
    //            RequestId = requestId,
    //            UserId = userId
    //        }, tx);

    //        //------------------------------------------------
    //        // ✅ 5. Insert Domain Event (Audit Power)
    //        //------------------------------------------------

    //        await _common.ExecuteAsync(
    //        @"
    //            INSERT INTO DocumentEvents
    //            (
    //                CompanyId,
    //                EventType,
    //                Metadata,
    //                PerformedBy
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                'DocumentRequestSubmitted',
    //                jsonb_build_object('RequestId', @RequestId),
    //                @UserId
    //            );
    //            ",
    //        new
    //        {
    //            CompanyId = companyId,
    //            RequestId = requestId,
    //            UserId = userId
    //        }, tx);

    //        //------------------------------------------------
    //        // ✅ COMMIT
    //        //------------------------------------------------

    //        await tx.CommitAsync();

    //        return requestId;
    //    }
    //    catch
    //    {
    //        await tx.RollbackAsync();
    //        throw;
    //    }
    //}

    //public async Task<IEnumerable<WorkflowTaskDto>> GetPendingRequestStepsAsync()
    //{
    //    WorkflowExecutionSteps
    //WHERE AssignedUserId = @User
    //AND Decision IS NULL
    //}


    //public async Task ApproveWorkflowStepAsync(ApproveStepDto input)
    //{
    //    using var tx = await _common.BeginTransactionAsync();

    //    // Validate step ownership
    //    // Insert decision row
    //    // Move to next step

    //    if (finalStep)
    //    {
    //        await TransitionState(entity);
    //    }

    //    await tx.CommitAsync();
    //}



    //public async Task<long> CreateRequestAsync(CreateDocumentRequestDto dto )
    //{
    //    await using var tx = await _common.BeginTransactionAsync();
    //    var userId = 2;
    //    try
    //    {
    //        var requestSql = @"
    //            INSERT INTO DocumentRequests
    //            (CompanyId, RequestNumber, RequestType, DocumentTypeId,
    //             DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode,
    //             DocumentName, Justification, CreatedBy)
    //            VALUES
    //            (@CompanyId, @RequestNumber, @RequestType, @DocumentTypeId,
    //             @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode,
    //             @DocumentName, @Justification, @CreatedBy)
    //            RETURNING Id;
    //            ";

    //        var requestId = await _common.ExecuteScalarAsync<long>(
    //            requestSql,
    //            new
    //            {
    //                dto.CompanyId,
    //                RequestNumber = $"REQ-{DateTime.UtcNow:yyyyMMddHHmmss}",
    //                dto.RequestType,
    //                dto.DocumentTypeId,
    //                dto.DivisionCode,
    //                dto.DepartmentCode,
    //                dto.SubDepartmentCode,
    //                dto.BusinessDomainCode,
    //                dto.DocumentName,
    //                dto.Justification,
    //                CreatedBy = 1 //userId
    //            },
    //            tx
    //        );

    //        //var draftSql = @"
    //        //    INSERT INTO RequestDraftVersions
    //        //    (CompanyId, DocumentRequestId, Content, CreatedBy)
    //        //    VALUES
    //        //    (@CompanyId, @RequestId, @Content, @CreatedBy);";

    //        //await _common.ExecuteAsync(
    //        //    draftSql,
    //        //    new
    //        //    {
    //        //        dto.CompanyId,
    //        //        RequestId = requestId,
    //        //        dto.Content,
    //        //        CreatedBy = 1//userId
    //        //    },
    //        //    tx
    //        //);

    //        tx.Commit();
    //        return requestId;
    //    }
    //    catch
    //    {
    //        tx.Rollback();
    //        throw;
    //    }
    //}

    //public async Task SubmitRequestAsync(long companyId, long requestId)
    //{
    //    await using var tx = await _common.BeginTransactionAsync();
    //    var userId = 2;

    //    //------------------------------------------------
    //    // 1️⃣ Get Request Metadata (Scope Resolution)
    //    //------------------------------------------------

    //    var request = await _common.ExecuteScalarAsync<dynamic>(@"
    //        SELECT *
    //        FROM DocumentRequests
    //        WHERE Id = @RequestId
    //        AND CompanyId = @CompanyId;",
    //    new { RequestId = requestId, CompanyId = companyId }, tx);


    //    //------------------------------------------------
    //    // 2️⃣ Resolve Workflow Policy
    //    // Scope-based selection (VERY IMPORTANT)
    //    //------------------------------------------------

    //    var policy = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
    //            SELECT wp.Id
    //            FROM WorkflowPolicies wp
    //            WHERE wp.CompanyId = @CompanyId
    //            AND (wp.DivisionCode IS NULL OR wp.DivisionCode = @DivisionCode)
    //            AND (wp.DepartmentCode IS NULL OR wp.DepartmentCode = @DepartmentCode)
    //            AND (wp.SubDepartmentCode IS NULL OR wp.SubDepartmentCode = @SubDepartmentCode)
    //            AND wp.DocumentTypeId = @DocTypeId
    //            AND wp.IsActive = TRUE
    //            LIMIT 1;",
    //            new
    //            {
    //                CompanyId = companyId,
    //                request.DivisionCode,
    //                request.DepartmentCode,
    //                request.SubDepartmentCode,
    //                DocTypeId = request.DocumentTypeId
    //            }, tx);

    //    if (policy == null)
    //        throw new Exception("No workflow policy configured.");


    //    //------------------------------------------------
    //    // 3️⃣ Get ACTIVE POLICY VERSION
    //    //------------------------------------------------

    //    var version = await _common.ExecuteScalarAsync<dynamic>(@"
    //        SELECT Id
    //        FROM WorkflowPolicyVersions
    //        WHERE WorkflowPolicyId = @PolicyId
    //        AND IsActive = TRUE;",
    //    new { PolicyId = policy.id }, tx);


    //    //------------------------------------------------
    //    // 4️⃣ Create Workflow Execution
    //    //------------------------------------------------

    //    var executionId = await _common.ExecuteScalarAsync<long>(@"
    //            INSERT INTO WorkflowExecutions
    //            (CompanyId, WorkflowPolicyVersionId,
    //             EntityType, EntityId,
    //             Status, CurrentStep,
    //             StartedBy)
    //            VALUES
    //            (@CompanyId, @VersionId,
    //             'REQUEST', @RequestId,
    //             'RUNNING', 1,
    //             @UserId)
    //            RETURNING Id;",
    //                new
    //                {
    //                    CompanyId = companyId,
    //                    VersionId = version.id,
    //                    RequestId = requestId,
    //                    UserId = userId
    //                }, tx);


    //    //------------------------------------------------
    //    // 5️⃣ Expand Step Definitions → Execution Steps
    //    //------------------------------------------------

    //    await _common.ExecuteAsync(@"
    //        INSERT INTO WorkflowExecutionSteps
    //        (
    //            CompanyId,
    //            WorkflowExecutionId,
    //            StepDefinitionId,
    //            AssignedUserId,
    //            AssignedRoleId
    //        )
    //        SELECT
    //            CompanyId,
    //            @ExecutionId,
    //            Id,
    //            UserId,
    //            RoleId
    //        FROM WorkflowStepDefinitions
    //        WHERE WorkflowPolicyVersionId = @VersionId
    //        AND IsActive = TRUE
    //        ORDER BY StepOrder;",
    //            new
    //            {
    //                ExecutionId = executionId,
    //                VersionId = version.id
    //            }, tx);


    //    //------------------------------------------------
    //    // 6️⃣ Attach Workflow to Request
    //    //------------------------------------------------

    //    await _common.ExecuteAsync(@"
    //        UPDATE DocumentRequests
    //        SET Status = 'PENDING_APPROVAL',
    //            WorkflowExecutionId = @ExecutionId
    //        WHERE Id = @RequestId;",
    //            new
    //            {
    //                ExecutionId = executionId,
    //                RequestId = requestId
    //            }, tx);


    //    tx.Commit();
    //}

    //public async Task ApproveRequestAsync(
    //                            long companyId,
    //                            long workflowExecutionId,
    //                            long userId,
    //                            string observation)
    //{
    //    await using var tx = await _common.BeginTransactionAsync();

    //    //------------------------------------------------
    //    // 1️⃣ Validate Step Ownership
    //    //------------------------------------------------

    //    var step = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
    //        SELECT *
    //        FROM WorkflowExecutionSteps
    //        WHERE WorkflowExecutionId = @ExecutionId
    //        AND CompanyId = @CompanyId
    //        AND AssignedUserId = @UserId
    //        AND Decision IS NULL;",
    //            new
    //            {
    //                ExecutionId = workflowExecutionId,
    //                CompanyId = companyId,
    //                UserId = userId
    //            }, tx);

    //    if (step == null)
    //        throw new Exception("No pending approval for this user.");


    //    //------------------------------------------------
    //    // 2️⃣ Approve Step
    //    //------------------------------------------------

    //    await _common.ExecuteAsync(@"
    //        UPDATE WorkflowExecutionSteps
    //        SET Decision = 'APPROVED',
    //            Observation = @Observation,
    //            ActionAt = NOW()
    //        WHERE Id = @StepId;",
    //            new
    //            {
    //                Observation = observation,
    //                StepId = step.id
    //            }, tx);


    //    //------------------------------------------------
    //    // 3️⃣ Check Remaining Steps in SAME GROUP
    //    // (Parallel approval safe)
    //    //------------------------------------------------

    //    var remainingInGroup = await _common.ExecuteScalarAsync<int>(@"
    //        SELECT COUNT(*)
    //        FROM WorkflowExecutionSteps es
    //        JOIN WorkflowStepDefinitions sd
    //        ON es.StepDefinitionId = sd.Id
    //        WHERE es.WorkflowExecutionId = @ExecutionId
    //        AND sd.StepGroup = (
    //            SELECT StepGroup
    //            FROM WorkflowStepDefinitions
    //            WHERE Id = @DefinitionId
    //        )
    //        AND es.Decision IS NULL;",
    //            new
    //            {
    //                ExecutionId = workflowExecutionId,
    //                DefinitionId = step.stepdefinitionid
    //            }, tx);


    //    if (remainingInGroup > 0)
    //    {
    //        tx.Commit();
    //        return; // Wait for parallel approvers
    //    }


    //    //------------------------------------------------
    //    // 4️⃣ Move to Next Step
    //    //------------------------------------------------

    //    var nextStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
    //        SELECT es.*
    //        FROM WorkflowExecutionSteps es
    //        JOIN WorkflowStepDefinitions sd
    //        ON es.StepDefinitionId = sd.Id
    //        WHERE es.WorkflowExecutionId = @ExecutionId
    //        AND es.Decision IS NULL
    //        ORDER BY sd.StepOrder
    //        LIMIT 1;",
    //            new { ExecutionId = workflowExecutionId }, tx);


    //    //------------------------------------------------
    //    // 5️⃣ FINAL APPROVAL
    //    //------------------------------------------------

    //    if (nextStep == null)
    //    {
    //        await _common.ExecuteAsync(@"
    //            UPDATE WorkflowExecutions
    //            SET Status = 'COMPLETED',
    //                CompletedAt = NOW()
    //            WHERE Id = @ExecutionId;",
    //                new { ExecutionId = workflowExecutionId }, tx);


    //        //------------------------------------------------
    //        // APPROVE REQUEST
    //        //------------------------------------------------

    //        await _common.ExecuteAsync(@"
    //            UPDATE DocumentRequests
    //            SET Status = 'APPROVED'
    //            WHERE WorkflowExecutionId = @ExecutionId;",
    //                new { ExecutionId = workflowExecutionId }, tx);

    //        //------------------------------------------------
    //        // 🔥 HERE you trigger DOCUMENT CREATION
    //        // (Call your DocumentService)
    //        //------------------------------------------------
    //        await CreateDocumentFromApprovedRequest(
    //              companyId,
    //              workflowExecutionId,
    //              userId,
    //              tx);
    //    }
    //    else
    //    {
    //        //------------------------------------------------
    //        // Update current step pointer
    //        //------------------------------------------------

    //        await _common.ExecuteAsync(@"
    //            UPDATE WorkflowExecutions
    //            SET CurrentStep = CurrentStep + 1
    //            WHERE Id = @ExecutionId;",
    //                new { ExecutionId = workflowExecutionId }, tx);
    //    }

    //    tx.Commit();
    //}


    //public async Task<long> CreateDocumentFromApprovedRequest(
    //            long companyId,
    //            long workflowExecutionId,
    //            long userId,
    //            IDbTransaction tx)
    //{
    //    //------------------------------------------------
    //    // 1️⃣ Fetch Approved Request
    //    //------------------------------------------------

    //    var request = await _common.ExecuteScalarAsync<dynamic>(@"
    //            SELECT *
    //            FROM DocumentRequests
    //            WHERE WorkflowExecutionId = @ExecutionId
    //            AND CompanyId = @CompanyId;",
    //                new
    //                {
    //                    ExecutionId = workflowExecutionId,
    //                    CompanyId = companyId
    //                }, tx);


    //    //------------------------------------------------
    //    // 2️⃣ Generate Document Number
    //    // Replace later with numbering engine
    //    //------------------------------------------------

    //    var documentNumber =
    //        $"DOC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6]}";


    //    //------------------------------------------------
    //    // 3️⃣ Create Document Container
    //    //------------------------------------------------

    //    var documentId = await _common.ExecuteScalarAsync<long>(@"
    //            INSERT INTO Documents
    //            (
    //                CompanyId,
    //                DocumentNumber,
    //                DocumentTypeId,
    //                DivisionCode,
    //                DepartmentCode,
    //                SubDepartmentCode,
    //                BusinessDomainCode
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @DocNumber,
    //                @DocType,
    //                @Division,
    //                @Department,
    //                @SubDept,
    //                @Domain
    //            )
    //            RETURNING Id;",
    //                new
    //                {
    //                    CompanyId = companyId,
    //                    DocNumber = documentNumber,
    //                    DocType = request.documenttypeid,
    //                    Division = request.divisioncode,
    //                    Department = request.departmentcode,
    //                    SubDept = request.subdepartmentcode,
    //                    Domain = request.businessdomaincode
    //                }, tx);


    //    //------------------------------------------------
    //    // 4️⃣ Get Latest Draft
    //    //------------------------------------------------

    //    var draftContent = await _common.ExecuteScalarAsync<string>(@"
    //        SELECT Content
    //        FROM RequestDraftVersions
    //        WHERE DocumentRequestId = @RequestId
    //        ORDER BY Version DESC
    //        LIMIT 1;",
    //            new { RequestId = request.id }, tx);


    //    //------------------------------------------------
    //    // 5️⃣ Create Immutable Version
    //    //------------------------------------------------

    //    var versionId = await _common.ExecuteScalarAsync<long>(@"
    //            INSERT INTO DocumentVersions
    //            (
    //                CompanyId,
    //                DocumentId,
    //                Version,
    //                Content,
    //                CreatedFromRequestId,
    //                State,
    //                CreatedBy
    //            )
    //            VALUES
    //            (
    //                @CompanyId,
    //                @DocumentId,
    //                '1.0',
    //                @Content,
    //                @RequestId,
    //                'DRAFT',
    //                @UserId
    //            )
    //            RETURNING Id;",
    //                new
    //                {
    //                    CompanyId = companyId,
    //                    DocumentId = documentId,
    //                    Content = draftContent,
    //                    RequestId = request.id,
    //                    UserId = userId
    //                }, tx);


    //    //------------------------------------------------
    //    // 6️⃣ Link Current Version
    //    //------------------------------------------------

    //    await _common.ExecuteAsync(@"
    //            UPDATE Documents
    //            SET CurrentVersionId = @VersionId
    //            WHERE Id = @DocumentId;",
    //                new
    //                {
    //                    VersionId = versionId,
    //                    DocumentId = documentId
    //                }, tx);


    //    //------------------------------------------------
    //    // 7️⃣ Mark Request Completed
    //    //------------------------------------------------

    //    await _common.ExecuteAsync(@"
    //            UPDATE DocumentRequests
    //            SET Status = 'COMPLETED',
    //                DocumentId = @DocumentId
    //            WHERE Id = @RequestId;",
    //                new
    //                {
    //                    DocumentId = documentId,
    //                    RequestId = request.id
    //                }, tx);


    //    //------------------------------------------------
    //    // 🔥 OPTIONAL BUT HIGHLY RECOMMENDED
    //    // Start DOCUMENT WORKFLOW immediately
    //    //------------------------------------------------

    //    // Example:
    //    // await _workflowService.StartDocumentWorkflow(...)

    //    return documentId;
    //}




    //// ✅ SUBMIT → START WORKFLOW
    //public async Task SubmitRequestAsync2(long requestId)
    //{
    //    await using var tx = await _common.BeginTransactionAsync();
    //    //var companyId = ""; //_tenant.CompanyId;
    //    var userId = 2; // _user.UserId;


    //    var companyId = await _common.ExecuteScalarAsync<long>(
    //        "SELECT CompanyId FROM DocumentRequests WHERE Id=@Id",
    //        new { Id = requestId }, tx);

    //    var workflowId = await _common.ExecuteScalarAsync<long>(@"
    //        INSERT INTO WorkflowExecutions
    //        (CompanyId, EntityType, EntityId, CurrentStep, Status, StartedBy)
    //        VALUES (@CompanyId,'REQUEST',@RequestId,1,'RUNNING',@UserId)
    //        RETURNING Id;",
    //    new { CompanyId = companyId, RequestId = requestId, UserId = userId }, tx);

    //    // Example: 2-step approval
    //    await _common.ExecuteAsync(@"
    //        INSERT INTO WorkflowExecutionSteps
    //        (WorkflowExecutionId, StepOrder, ApproverUserId)
    //        VALUES
    //        (@WorkflowId,1,2),
    //        (@WorkflowId,2,3);",
    //    new { WorkflowId = workflowId }, tx);

    //    await _common.ExecuteAsync(@"
    //            UPDATE DocumentRequests
    //            SET Status='PENDING_APPROVAL',
    //            WorkflowExecutionId=@WorkflowId
    //            WHERE Id=@RequestId;",
    //    new { WorkflowId = workflowId, RequestId = requestId }, tx);

    //    tx.Commit();
    //}

    //// ✅ APPROVE + AUTO CREATE DOCUMENT
    //public async Task ApproveAsync(long workflowId, string observation)
    //{
    //    await using var tx = await _common.BeginTransactionAsync();
    //    var companyId = ""; //_tenant.CompanyId;
    //    var userId = ""; // _user.UserId;


    //    var step = await _common.ExecuteScalarAsync<dynamic>(@"
    //            SELECT * FROM WorkflowExecutionSteps
    //            WHERE WorkflowExecutionId=@WorkflowId
    //            AND ApproverUserId=@UserId
    //            AND Decision IS NULL;",
    //    new { WorkflowId = workflowId, UserId = userId }, tx);

    //    await _common.ExecuteAsync(@"
    //            UPDATE WorkflowExecutionSteps
    //            SET Decision='APPROVED',
    //            Observation=@Obs,
    //            ActionAt=NOW()
    //            WHERE Id=@StepId;",
    //    new { Obs = observation, StepId = step.id }, tx);

    //    var pending = await _common.ExecuteScalarAsync<int>(@"
    //            SELECT COUNT(*) FROM WorkflowExecutionSteps
    //            WHERE WorkflowExecutionId=@WorkflowId
    //            AND Decision IS NULL;", 
    //    new { WorkflowId = workflowId }, tx);

    //    // ✅ FINAL APPROVAL
    //    if (pending == 0)
    //    {
    //        var request = await _common.ExecuteScalarAsync<dynamic>(@"
    //            SELECT * FROM DocumentRequests
    //            WHERE WorkflowExecutionId=@WorkflowId",
    //            new { WorkflowId = workflowId }, tx);

    //        // Create Document
    //        var docId = await _common.ExecuteScalarAsync<long>(@"
    //                INSERT INTO Documents
    //                (CompanyId, DocumentNumber, DocumentTypeId,
    //                 DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode)
    //                VALUES
    //                (@CompanyId, @DocNo, @DocType,
    //                 @Div,@Dept,@Sub,@Domain)
    //                RETURNING Id;",
    //        new
    //        {
    //            request.companyid,
    //            DocNo = $"DOC-{DateTime.UtcNow.Ticks}",
    //            DocType = request.documenttypeid,
    //            Div = request.divisioncode,
    //            Dept = request.departmentcode,
    //            Sub = request.subdepartmentcode,
    //            Domain = request.businessdomaincode
    //        }, tx);

    //        var draft = await _common.ExecuteScalarAsync<string>(@"
    //            SELECT Content
    //            FROM RequestDraftVersions
    //            WHERE DocumentRequestId=@ReqId
    //            ORDER BY Version DESC
    //            LIMIT 1;",
    //        new { ReqId = request.id }, tx);

    //        var versionId = await _common.ExecuteScalarAsync<long>(@"
    //            INSERT INTO DocumentVersions
    //            (CompanyId, DocumentId, Version, Content,
    //             CreatedFromRequestId, State, CreatedBy)
    //            VALUES
    //            (@CompanyId,@DocId,'1.0',@Content,
    //             @ReqId,'APPROVED',@UserId)
    //            RETURNING Id;",
    //        new
    //        {
    //            request.companyid,
    //            DocId = docId,
    //            Content = draft,
    //            ReqId = request.id,
    //            UserId = userId
    //        }, tx);

    //        await _common.ExecuteAsync(@"
    //            UPDATE Documents
    //            SET CurrentVersionId=@VersionId
    //            WHERE Id=@DocId;",
    //        new { VersionId = versionId, DocId = docId }, tx);

    //        await _common.ExecuteAsync(@"
    //            UPDATE DocumentRequests
    //            SET Status='APPROVED'
    //            WHERE Id=@ReqId;",
    //        new { ReqId = request.id }, tx);

    //        await _common.ExecuteAsync(@"
    //            UPDATE WorkflowExecutions
    //            SET Status='COMPLETED'
    //            WHERE Id=@WorkflowId;",
    //        new { WorkflowId = workflowId }, tx);
    //    }

    //    tx.Commit();
    //}
}
