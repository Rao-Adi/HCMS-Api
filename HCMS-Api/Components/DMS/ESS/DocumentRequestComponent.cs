using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using Npgsql;
using System.Data;

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
    private readonly PeoplePartnersComponent _peoplePartnersComponent;
    private readonly WorkflowStepComponent _workflowStepComponent;
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
        NotificationComponent notificationComponent,
        PeoplePartnersComponent peoplePartnersComponent,
        WorkflowStepComponent workflowStepComponent
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
        _peoplePartnersComponent = peoplePartnersComponent;
        _workflowStepComponent = workflowStepComponent;
        //string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        //_dataservice.BeginProcess(connectionString);

    }
     
    public async Task<long> CreateDraftDocumentRequestAsync(DraftDocumentRequestDto dto)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);


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
                    CompanyId, RequestNumber, DocumentRequestTypeCode, DocumentTypeCode, DocumentName, Justification, ProposedContent,
                    DraftFileUrl, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode, Status, CreatedBy, LastModifiedBy, IsContentFinalized
                )
                VALUES
                (
                    @CompanyId, 'DR-' || nextval('document_request_seq'), @RequestType, @DocumentTypeCode, @DocumentName, @Justification, @ProposedContent,
                    @DraftFileUrl, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode, @Status, @CreatedBy, @LastModifiedBy, FALSE
                )
                RETURNING Id;",
            new
            {
                CompanyId,
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
                CreatedBy = userId,
                LastModifiedBy = userId
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
                         @CreatedBy,@LastModifiedBy);",
                    new
                    {
                        CompanyId,
                        RequestId = requestId,
                        d.DivisionCode,
                        d.DepartmentCode,
                        d.SubDepartmentCode,
                        d.BusinessDomainCode,
                        d.RoleId,
                        d.DistributionTypeId,
                        CreatedBy = userId,
                        LastModifiedBy = userId
                    }, transaction);
                }
            }

            //-----------------------------------------
            // USER DISTRIBUTION (Editable)
            //-----------------------------------------

            if (dto.UserIds?.Any() == true)
            {
                foreach (var _userId in dto.UserIds)
                {
                    await _common.ExecuteAsync(@"
                        INSERT INTO DocumentRequestUserDistributions
                        (CompanyId,DocumentRequestId,EmployeeCode,
                         CreatedBy,LastModifiedBy)
                        VALUES
                        (@CompanyId,@RequestId,@EmployeeCode,
                         @CreatedBy,@LastModifiedBy);",
                    new
                    {
                        CompanyId,
                        RequestId = requestId,
                        EmployeeCode = _userId,
                        CreatedBy = userId,
                        LastModifiedBy = userId
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
                CompanyId,
                RequestId = requestId,
                Status = DocumentRequestStatus.Draft,
                UserId = userId
            }, transaction);

            await transaction.CommitAsync();
            return requestId;
        }
        catch (Exception ex)
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

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
                    LastModifiedBy = @LastModifiedBy
                WHERE Id = @RequestId
                AND CompanyId = @CompanyId
                AND Status = @DraftStatus
                AND IsContentFinalized = FALSE;",
            new
            {
                dto.RequestId,
                CompanyId,
                dto.DocumentName,
                dto.Justification,
                dto.ProposedContent,
                DraftFileUrl = draftFileUrl,
                LastModifiedBy = userId,
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
            await InsertDistributionsAsync(CompanyId, dto.RequestId,
                dto!.DistributionList, dto!.UserIds,
                userId, transaction);


            //-------------------------------------------------
            // 2️⃣ Insert History
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO DocumentRequestHistory
                (
                    CompanyId, RequestId, ToStateId, ChangedBy, Comments
                )
                VALUES
                (
                    @CompanyId, @RequestId, @Status, @UserId, 'Draft Updated'
                );",
                new
                {
                    CompanyId,
                    dto.RequestId,
                    Status = DocumentRequestStatus.Draft,
                    UserId = userId
                },
                transaction);

            await transaction.CommitAsync();
            return 1; // 1= success
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }



    public async Task<bool> SubmitDraftDocumentRequestAsync(SubmitDocumentRequestDto input)
    {
        //string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        //var clientIp = _clientContextService.GetClientIP();
        //var prefix = _utilities.GetPrefix(clientIp);
        //var userId = _utilities.GetUserid(prefix);
        //int CompanyId = int.Parse(_CompanyId);


        //// Optional: ensure draft exists before calling original submit
        //var draftExists = await _common.ExecuteScalarAsync<int>(@"
        //    SELECT COUNT(1)
        //    FROM DocumentRequests
        //    WHERE Id = @RequestId
        //    AND CompanyId = @CompanyId
        //    AND Status = @DraftStatus;",
        //new
        //{
        //    input.RequestId,
        //    CompanyId,
        //    DraftStatus = DocumentRequestStatus.Draft
        //});

        //if (draftExists == 0)
        //    throw new Exception("Only draft requests can be submitted.");

        // Call your EXISTING working method
        return await SubmitDocumentRequestAsync(input);
    }

    public async Task<long> CreateAndSubmitDocumentRequestAsync(DraftDocumentRequestDto dto)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            // 1. Get User/Company Info
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            // Validation: Justification is mandatory for submission
            if (string.IsNullOrWhiteSpace(dto.Justification))
                throw new CustomException("Justification is required to submit a document request.", 400);

            // 2. Handle File Upload
            string? draftFileUrl = null;
            if (dto.DraftFile != null && dto.DraftFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "drafts");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileName = $"{dto.DraftFile.FileName}";
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.DraftFile.CopyToAsync(stream);
                }
                draftFileUrl = $"/uploads/drafts/{fileName}";
            }

            // 3. Insert Document Request with 'Submitted' status
            var requestId = await _common.ExecuteScalarAsync<long>(@"
                INSERT INTO DocumentRequests
                (
                    CompanyId, RequestNumber, DocumentRequestTypeCode, DocumentTypeCode, DocumentName, Justification, ProposedContent,
                    DraftFileUrl, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode, 
                    Status, CreatedBy, LastModifiedBy, IsContentFinalized, SubmittedAt, SubmittedBy
                )
                VALUES
                (
                    @CompanyId, 'DR-' || nextval('document_request_seq'), @RequestType, @DocumentTypeCode, @DocumentName, @Justification, @ProposedContent,
                    @DraftFileUrl, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode, 
                    @Status, @UserId, @UserId, TRUE, NOW(), @UserId
                )
                RETURNING Id;",
            new
            {
                CompanyId,
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
                Status = DocumentRequestStatus.Submitted,
                UserId = userId
            }, transaction);

            // 4. Insert Distribution Lists
            await InsertDistributionsAsync(CompanyId, requestId, dto.DistributionList, dto.UserIds, userId, transaction);


            // 5. Workflow Execution Logic
            var policyId = await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId AND EntityType = 'Request' AND DocumentTypeCode = @DocType
                AND COALESCE(DivisionCode,'') = COALESCE(@DivisionCode,'')
                AND COALESCE(DepartmentCode,'') = COALESCE(@DepartmentCode,'')
                AND COALESCE(SubDepartmentCode,'') = COALESCE(@SubDepartmentCode,'')
                AND COALESCE(BusinessDomainCode,'') = COALESCE(@BusinessDomainCode,'')
                AND IsActive = TRUE AND IsDeleted = FALSE;",
            new
            {
                CompanyId,
                DocType = dto.DocumentTypeCode,
                dto.DivisionCode,
                dto.DepartmentCode,
                dto.SubDepartmentCode,
                dto.BusinessDomainCode
            }, transaction);

            if (policyId == null)
                throw new CustomException("No workflow policy defined for selected Cabinet Scope.", 404);

            var versionId = await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id FROM WorkflowPolicyVersions
                WHERE CompanyId = @CompanyId AND WorkflowPolicyId = @PolicyId AND IsActive = TRUE LIMIT 1;",
            new { CompanyId, PolicyId = policyId }, transaction);

            if (versionId == null)
                throw new CustomException("Active workflow policy version not found.", 404);

            var executionId = await _common.ExecuteScalarAsync<long>(@"
                INSERT INTO WorkflowExecutions (CompanyId, WorkflowPolicyVersionId, EntityType, EntityId, Status, StartedBy)
                VALUES (@CompanyId, @WorkflowPolicyVersionId, @EntityType, @EntityId, @Status, @StartedBy)
                RETURNING Id;",
                new
                {
                    CompanyId,
                    WorkflowPolicyVersionId = versionId,
                    EntityType = "Request",
                    EntityId = requestId,
                    Status = "Running",
                    StartedBy = userId
                }, transaction);

            var stepDefs = await _common.QueryAsync<dynamic>(@"
                SELECT Id, UserId, RoleId, DesignationId, StepOrder
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId
                ORDER BY StepOrder;", new { VersionId = versionId }, transaction);

            int runningStepOrder = 1;
            int insertedSteps = 0;

            foreach (var stepDef in stepDefs)
            {
                if (stepDef.userid != null)
                {
                    await _common.ExecuteAsync(@"
                        INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                        VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                        new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = stepDef.userid, StepOrder = runningStepOrder }, transaction);
                    runningStepOrder++;
                    insertedSteps++;
                }
                else if (stepDef.roleid != null || stepDef.designationid != null)
                {
                    var employees = await _common.QueryAsync<string>(@"
                        SELECT TRIM(e.empcode)
                        FROM public.tblempjobprofile ejp
                        INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                        WHERE e.CompanyId = @CompanyId 
                          AND COALESCE(e.Active, 1) = 1           -- Must be an active employee
                          AND COALESCE(ejp.Active, TRUE) = TRUE   -- Must currently hold this role
                          AND ((@RoleId::int IS NOT NULL AND ejp.roleid = @RoleId::int) OR (@DesignationId::int IS NOT NULL AND ejp.dsgid = @DesignationId::int))
                        ORDER BY e.empid ASC;",
                        new { CompanyId, RoleId = (int?)stepDef.roleid, DesignationId = (int?)stepDef.designationid }, transaction);

                    if (!employees.Any())
                        throw new CustomException("Workflow misconfigured — no active employees found for a configured Role/Designation step.", 409);

                    foreach (var empCode in employees)
                    {
                        await _common.ExecuteAsync(@"
                            INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                            VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                            new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = empCode, StepOrder = runningStepOrder }, transaction);
                        runningStepOrder++;
                        insertedSteps++;
                    }
                }
            }

            if (insertedSteps < 1)
                throw new CustomException("Workflow misconfigured — no steps defined for this policy version.", 409);

            await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutionSteps
                SET IsActive = TRUE
                WHERE WorkflowExecutionId = @ExecutionId
                AND StepOrder = (SELECT MIN(StepOrder) FROM WorkflowExecutionSteps WHERE WorkflowExecutionId = @ExecutionId);",
                new { ExecutionId = executionId }, transaction);

            // 6. History
            await InsertHistoryAsync(CompanyId, requestId, DocumentRequestStatus.Submitted, userId, "Request Created and Submitted", transaction);


            // 7. Prepare and Send Notification

            // Prepare notification data
            var activeStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.StepOrder, dr.RequestNumber
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN DocumentRequests dr ON dr.Id = we.EntityId
                WHERE wes.WorkflowExecutionId = @ExecutionId
                AND wes.IsActive = TRUE LIMIT 1;", new { ExecutionId = executionId }, transaction);

            List<string> approvers = new List<string>();
            string requestNumberStr = requestId.ToString();
            if (activeStep != null)
            {
                requestNumberStr = Convert.ToString(activeStep.requestnumber) ?? requestId.ToString();
                approvers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, (int)activeStep.steporder, transaction);
            }

            // 8. Commit
            await transaction.CommitAsync();

            if (approvers.Any())
            {
                var placeholders = new Dictionary<string, string> { { "ID", requestNumberStr } };
                foreach (var approver in approvers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, CompanyId, (int)requestId, approver, placeholders);
                }
            }

            return requestId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<bool> SubmitDocumentRequestAsync(SubmitDocumentRequestDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();

        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            //-------------------------------------------------
            // Validate Request
            //-------------------------------------------------

            var request = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM DocumentRequests
                WHERE Id = @RequestId
                AND CompanyId = @CompanyId
                FOR UPDATE;",
                new { input.RequestId, CompanyId }, tx);

            if (request == null)
                throw new Exception("Request not found.");

            if (request.status != (int)DocumentRequestStatus.Draft)
                throw new Exception("Only draft requests can be submitted.");

            // UC-22 Validation: Justification is mandatory
            if (string.IsNullOrWhiteSpace(request.justification))
                throw new Exception("Justification is required to submit a document request.");

            // UC-22 Validation: Ensure content has been altered for a Revision
            // Assuming 'Revision' or 'REV' is the code for revision requests. Adjust as per your actual codes.
            if (request.documentrequesttypecode == "Revision" && request.documentid != null)
            {
                var originalDoc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT d.DocumentURL, dv.Content
                    FROM Documents d
                    LEFT JOIN DocumentVersions dv ON d.Id = dv.DocumentId AND dv.IsActive = TRUE
                    WHERE d.Id = @DocumentId
                    ORDER BY dv.CreatedAt DESC LIMIT 1;",
                    new { DocumentId = request.documentid }, tx);

                if (originalDoc != null)
                {
                    bool contentUnchanged = (request.proposedcontent == originalDoc.content) || (string.IsNullOrWhiteSpace(request.proposedcontent) && string.IsNullOrWhiteSpace(originalDoc.content));
                    bool fileUnchanged = (request.draftfileurl == originalDoc.documenturl) || (string.IsNullOrWhiteSpace(request.draftfileurl) && string.IsNullOrWhiteSpace(originalDoc.documenturl));

                    if (contentUnchanged && fileUnchanged)
                        throw new Exception("Document Content must be altered from the original version before submitting a revision request.");
                }
            }

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
                         @CreatedBy,@LastModifiedBy);",
                    new
                    {
                        CompanyId,
                        input.RequestId,
                        d.DivisionCode,
                        d.DepartmentCode,
                        d.SubDepartmentCode,
                        d.BusinessDomainCode,
                        d.RoleId,
                        d.DistributionTypeId,
                        CreatedBy = userId,
                        LastModifiedBy = userId
                    }, tx);
                }
            }

            //-------------------------------------------------
            // RE-INSERT UPDATED USER LIST
            //-------------------------------------------------

            if (input.UserIds?.Any() == true)
            {
                foreach (var _userId in input.UserIds)
                {
                    await _common.ExecuteAsync(@"
                    INSERT INTO DocumentRequestUserDistributions
                    (CompanyId,DocumentRequestId,EmployeeCode,
                     CreatedBy,LastModifiedBy)
                    VALUES
                    (@CompanyId,@RequestId,@EmployeeCode,
                     @CreatedBy,@LastModifiedBy);",
                    new
                    {
                        CompanyId,
                        input.RequestId,
                        EmployeeCode = _userId,
                        CreatedBy = userId,
                        LastModifiedBy = userId
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
                CompanyId,
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
                CompanyId,
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
                    CompanyId, WorkflowPolicyVersionId, EntityType, EntityId, Status, StartedBy
                )
                VALUES
                (
                    @CompanyId, @WorkflowPolicyVersionId, @EntityType, @EntityId, @Status, @StartedBy
                )
                RETURNING Id;",
                new
                {
                    CompanyId,
                    WorkflowPolicyVersionId = versionId,
                    EntityType = "Request",
                    EntityId = input.RequestId,
                    Status = "Running",
                    StartedBy = userId
                }, tx);

            var stepDefs = await _common.QueryAsync<dynamic>(@"
                SELECT Id, UserId, RoleId, DesignationId, StepOrder
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId
                ORDER BY StepOrder;", new { VersionId = versionId }, tx);

            int runningStepOrder = 1;
            int inserted = 0;

            foreach (var stepDef in stepDefs)
            {
                if (stepDef.userid != null)
                {
                    await _common.ExecuteAsync(@"
                        INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                        VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                        new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = stepDef.userid, StepOrder = runningStepOrder }, tx);
                    runningStepOrder++;
                    inserted++;
                }
                else if (stepDef.roleid != null || stepDef.designationid != null)
                {
                    var employees = await _common.QueryAsync<string>(@"
                        SELECT TRIM(e.empcode)
                        FROM public.tblempjobprofile ejp
                        INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                        WHERE e.CompanyId = @CompanyId 
                          AND COALESCE(e.Active, 1) = 1           -- Must be an active employee
                          AND COALESCE(ejp.Active, TRUE) = TRUE   -- Must currently hold this role
                          AND ((@RoleId::int IS NOT NULL AND ejp.roleid = @RoleId::int) OR (@DesignationId::int IS NOT NULL AND ejp.dsgid = @DesignationId::int))
                        ORDER BY e.empid ASC;",
                        new { CompanyId, RoleId = (int?)stepDef.roleid, DesignationId = (int?)stepDef.designationid }, tx);

                    if (!employees.Any())
                        throw new Exception("Workflow misconfigured — no active employees found for a configured Role/Designation step.");

                    foreach (var empCode in employees)
                    {
                        await _common.ExecuteAsync(@"
                            INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                            VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                            new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = empCode, StepOrder = runningStepOrder }, tx);
                        runningStepOrder++;
                        inserted++;
                    }
                }
            }

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
                    UserId = userId,
                    input.RequestId
                }, tx);

            // Prepare notification data
            var activeStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.StepOrder, dr.RequestNumber
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN DocumentRequests dr ON dr.Id = we.EntityId
                WHERE wes.WorkflowExecutionId = @ExecutionId
                AND wes.IsActive = TRUE LIMIT 1;", new { ExecutionId = executionId }, tx);

            List<string> approvers = new List<string>();
            string requestNumber = input.RequestId.ToString();
            if (activeStep != null)
            {
                requestNumber = Convert.ToString(activeStep.requestnumber) ?? input.RequestId.ToString();
                approvers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, (int)activeStep.steporder, tx);
            }

            await tx.CommitAsync();

            if (approvers.Any())
            {
                var placeholders = new Dictionary<string, string> { { "ID", requestNumber } };
                foreach (var approver in approvers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, CompanyId, (int)input.RequestId, approver, placeholders);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            throw;
        }
    }


    private async Task InsertDistributionsAsync(
            int companyId,
            long requestId,
            IEnumerable<DistributionListCreateDto>? roles,
            IEnumerable<string>? users,
            string userId,
            IDbTransaction tx)
    {
        try
        {

            if (roles?.Any() == true)
            {
                foreach (var d in roles)
                {
                    await _common.ExecuteAsync(@"
                INSERT INTO DocumentRequestRoleDistributions
                (CompanyId, DocumentRequestId, DivisionCode, DepartmentCode, SubDepartmentCode,
                 BusinessDomainCode, RoleId, DistributionTypeId, CreatedBy, LastModifiedBy)
                VALUES
                (@CompanyId, @DocumentRequestId, @DivisionCode, @DepartmentCode, @SubDepartmentCode,
                 @BusinessDomainCode, @RoleId, @DistributionTypeId, @CreatedBy, @LastModifiedBy);",
                    new
                    {
                        CompanyId = companyId,
                        DocumentRequestId = requestId,
                        d.DivisionCode,
                        d.DepartmentCode,
                        d.SubDepartmentCode,
                        d.BusinessDomainCode,
                        d.RoleId,
                        d.DistributionTypeId,
                        CreatedBy = userId,
                        LastModifiedBy = userId
                    }, tx);
                }
            }

            if (users?.Any() == true)
            {
                foreach (var uid in users)
                {
                    await _common.ExecuteAsync(@"
                INSERT INTO DocumentRequestUserDistributions
                (CompanyId, DocumentRequestId, EmployeeCode, CreatedBy, LastModifiedBy)
                VALUES
                (@CompanyId, @RequestId, @EmployeeCode,  @CreatedBy, @LastModifiedBy);",
                    new
                    {
                        CompanyId = companyId,
                        RequestId = requestId,
                        EmployeeCode = uid,
                        CreatedBy = userId,
                        LastModifiedBy = userId
                    }, tx);
                }
            }
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    private async Task InsertHistoryAsync(
        int companyId,
        long requestId,
        DocumentRequestStatus status,
        string userId,
        string comments,
        IDbTransaction tx)
    {
        await _common.ExecuteAsync(@"
        INSERT INTO DocumentRequestHistory
        (CompanyId,RequestId,ToStateId,ChangedBy,Comments)
        VALUES
        (@CompanyId,@RequestId,@Status,@ChangedBy,@Comments);",
        new
        {
            CompanyId = companyId,
            RequestId = requestId,
            Status = status,
            ChangedBy = userId,
            Comments = comments
        }, tx);
    }
     
    public async Task<PaginationResult<DocumentRequestReadDto>> GetMyInboxRequestsAsync(GetPendingRequestDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);


            //get Employee details by Id
            var empDetail = await _peoplePartnersComponent.GetEmployeeByEmpIdAsync(input.EmpId);

            var whereClause = "WHERE 1=1";

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(DocumentName) LIKE '%{search}%'
                    OR UPPER(RequestNumber) LIKE '%{search}%'
                )";
            }

            if (!string.IsNullOrWhiteSpace(input.DivisionCode))
                whereClause += " AND DivisionCode = @DivisionCode";
            if (!string.IsNullOrWhiteSpace(input.DepartmentCode))
                whereClause += " AND DepartmentCode = @DepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.SubDepartmentCode))
                whereClause += " AND SubDepartmentCode = @SubDepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.BusinessDomainCode))
                whereClause += " AND BusinessDomainCode = @BusinessDomainCode";
            if (!string.IsNullOrWhiteSpace(input.DocumentTypeCode))
                whereClause += " AND DocumentTypeCode = @DocumentTypeCode";

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNAME" => "DocumentName",
                "REQUESTNUMBER" => "RequestNumber",
                "CREATEDAT" => "CreatedAt",
                _ => "CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            var dataSql = $@"SELECT * FROM fn_get_my_inbox_requests(
                    @CompanyId,
                    @UserId,
                    @RequestStatus
                )
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            var countSql = $@"SELECT COUNT(1) FROM fn_get_my_inbox_requests(
                    @CompanyId,
                    @UserId,
                    @RequestStatus
                ) {whereClause};";

            var queryParams = new
            {
                CompanyId,
                UserId = empDetail?.empcode,
                input.RequestStatus,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode,
                input.DocumentTypeCode
            };

            var dynamicRequests = await _common.QueryAsync<dynamic>(dataSql, queryParams);
            var requests = new List<DocumentRequestReadDto>();
            foreach (var row in dynamicRequests)
            {
                var dict = row as IDictionary<string, object>;
                if (dict == null) continue;

                requests.Add(new DocumentRequestReadDto
                {
                    Id = GetValue<int>(dict, "id"),
                    CompanyId = GetValue<int>(dict, "companyid"),
                    Company = GetValue<string>(dict, "company"),
                    RequestNumber = GetValue<string>(dict, "requestnumber"),
                    DocumentRequestTypeCode = GetValue<string>(dict, "documentrequesttypecode"),
                    DocumentId = GetValue<int>(dict, "documentid"),
                    DocumentType = GetValue<string>(dict, "documenttype"),
                    DocumentTypeCode = GetValue<string>(dict, "documenttypecode"),
                    Division = GetValue<string>(dict, "division"),
                    DivisionCode = GetValue<string>(dict, "divisioncode"),
                    Department = GetValue<string>(dict, "department"),
                    DepartmentCode = GetValue<string>(dict, "departmentcode"),
                    SubDepartment = GetValue<string>(dict, "subdepartment"),
                    SubDepartmentCode = GetValue<string>(dict, "subdepartmentcode"),
                    BusinessDomain = GetValue<string>(dict, "businessdomain"),
                    BusinessDomainCode = GetValue<string>(dict, "businessdomaincode"),
                    DocumentName = GetValue<string>(dict, "documentname"),
                    Justification = GetValue<string>(dict, "justification"),
                    IsReworked = GetValue<bool>(dict, "isreworked"),
                    Status = GetValue<int>(dict, "status"),
                    StepId = GetValue<int>(dict, "stepid"),
                    StepOrder = GetValue<int>(dict, "steporder"),
                    StartedAt = GetValue<string>(dict, "startedat"),
                    RowVersion = GetValue<string>(dict, "rowversion"),
                    ProposedContent = GetValue<string>(dict, "proposedcontent"),
                    DraftFileUrl = GetValue<string>(dict, "draftfileurl"),
                    IsContentFinalized = GetValue<bool>(dict, "iscontentfinalized"),
                    DraftContentLastModifiedAt = GetValue<DateTime?>(dict, "draftcontentlastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    DraftContentLastModifiedBy = GetValue<string>(dict, "draftcontentlastmodifiedby"),
                    IsActive = GetValue<bool>(dict, "isactive"),
                    IsDeleted = GetValue<bool>(dict, "isdeleted"),
                    CreatedAt = GetValue<DateTime?>(dict, "createdat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    CreatedBy = GetValue<string>(dict, "createdby"),
                    LastModifiedAt = GetValue<DateTime?>(dict, "lastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    LastModifiedBy = GetValue<string>(dict, "lastmodifiedby")
                });
            }
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<DocumentRequestReadDto>
            {
                Items = requests,
                TotalCount = totalCount
            };

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<PaginationResult<DocumentRequestReadDto>> GetDraftDocumentRequestAsync(GetDocumentDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"WHERE dr.CompanyId = @CompanyId
                AND dr.Status = @DraftStatus
                AND dr.CreatedBy = @CreatedBy";

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(dr.DocumentName) LIKE '%{search}%'
                    OR UPPER(dr.RequestNumber) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNAME" => "dr.DocumentName",
                "REQUESTNUMBER" => "dr.RequestNumber",
                "CREATEDAT" => "dr.CreatedAt",
                _ => "dr.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            //-------------------------------------------------
            // 1️⃣ Get Draft Requests
            //-------------------------------------------------

            var dataSql = $@"SELECT dr.*,
                CASE WHEN EXISTS(SELECT 1 FROM WorkflowExecutions we WHERE we.EntityId = dr.Id AND we.EntityType = 'Request') THEN TRUE ELSE FALSE END AS isreworked
                FROM Vw_DocumentRequests dr
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            var countSql = $@"SELECT COUNT(1) FROM Vw_DocumentRequests dr {whereClause};";

            var queryParams = new
            {
                CompanyId,
                DraftStatus = DocumentRequestStatus.Draft,
                CreatedBy = userId.ToString()
            };

            var dynamicRequests = await _common.QueryAsync<dynamic>(dataSql, queryParams);
            var requests = new List<DocumentRequestReadDto>();
            foreach (var row in dynamicRequests)
            {
                var dict = row as IDictionary<string, object>;
                if (dict == null) continue;

                requests.Add(new DocumentRequestReadDto
                {
                    Id = GetValue<int>(dict, "id"),
                    CompanyId = GetValue<int>(dict, "companyid"),
                    Company = GetValue<string>(dict, "company"),
                    RequestNumber = GetValue<string>(dict, "requestnumber"),
                    DocumentRequestTypeCode = GetValue<string>(dict, "documentrequesttypecode"),
                    DocumentId = GetValue<int>(dict, "documentid"),
                    DocumentType = GetValue<string>(dict, "documenttype"),
                    DocumentTypeCode = GetValue<string>(dict, "documenttypecode"),
                    Division = GetValue<string>(dict, "division"),
                    DivisionCode = GetValue<string>(dict, "divisioncode"),
                    Department = GetValue<string>(dict, "department"),
                    DepartmentCode = GetValue<string>(dict, "departmentcode"),
                    SubDepartment = GetValue<string>(dict, "subdepartment"),
                    SubDepartmentCode = GetValue<string>(dict, "subdepartmentcode"),
                    BusinessDomain = GetValue<string>(dict, "businessdomain"),
                    BusinessDomainCode = GetValue<string>(dict, "businessdomaincode"),
                    DocumentName = GetValue<string>(dict, "documentname"),
                    Justification = GetValue<string>(dict, "justification"),
                    Status = GetValue<int>(dict, "status"),
                    IsReworked = GetValue<bool>(dict, "isreworked"),
                    RowVersion = GetValue<string>(dict, "rowversion"),
                    ProposedContent = GetValue<string>(dict, "proposedcontent"),
                    DraftFileUrl = GetValue<string>(dict, "draftfileurl"),
                    IsContentFinalized = GetValue<bool>(dict, "iscontentfinalized"),
                    DraftContentLastModifiedAt = GetValue<DateTime?>(dict, "draftcontentlastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    DraftContentLastModifiedBy = GetValue<string>(dict, "draftcontentlastmodifiedby"),
                    IsActive = GetValue<bool>(dict, "isactive"),
                    IsDeleted = GetValue<bool>(dict, "isdeleted"),
                    CreatedAt = GetValue<DateTime?>(dict, "createdat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    CreatedBy = GetValue<string>(dict, "createdby"),
                    LastModifiedAt = GetValue<DateTime?>(dict, "lastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    LastModifiedBy = GetValue<string>(dict, "lastmodifiedby")
                });
            }
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            if (!requests.Any())
                return new PaginationResult<DocumentRequestReadDto>
                {
                    Items = new List<DocumentRequestReadDto>(),
                    TotalCount = 0
                };

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
                    CompanyId = CompanyId,
                    RequestIds = requestIds
                })).ToList();

            //-------------------------------------------------
            // 4️⃣ Get User Distributions
            //-------------------------------------------------

            var userDistributions = (await _common.QueryAsync<DocumentRequestUserDistribution>(@"
                SELECT drd.*, LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' ||COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName,
                COALESCE(des.name, des_fallback.name) AS Designation, r.name AS Role
                FROM DocumentRequestUserDistributions drd 
                LEFT JOIN tblEmployee e on LPAD(drd.EmployeeCode::text, 9, '0') = e.empCode
                INNER JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE
                LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid
                LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid
                LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid
                WHERE drd.CompanyId = @CompanyId
                AND DocumentRequestId = ANY(@RequestIds);",
                new
                {
                    CompanyId = CompanyId,
                    RequestIds = requestIds
                })).ToList();

            //-------------------------------------------------
            // 5️⃣ Map Distributions Into Each Request
            //-------------------------------------------------

            foreach (var request in requests)
            {
                request.DistributionList = roleDistributions
                    .Where(x => x.DocumentRequestId == request.Id)
                    .Select(x => new DistributionListReadDto
                    {
                        Id = x.Id,
                        DocumentRequestId = x.DocumentRequestId,
                        CompanyId = x.CompanyId,
                        Company = x.Company,
                        RoleId = x.RoleId,
                        Role = x.Role,
                        DistributionTypeId = x.DistributionTypeId,
                        DistributionType = x.DistributionType,
                        Division = x.Division,
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

            return new PaginationResult<DocumentRequestReadDto>
            {
                Items = requests,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }


    public async Task<bool> TakeWorkflowActionAsync(ApproveRejectWorkflowStepDto input)
    {
        try
        {

            await using var tx = await _common.BeginTransactionAsync();

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var empDetail = await _peoplePartnersComponent.GetEmployeeByEmpIdAsync(input.EmpId);
            //-------------------------------------------------
            // 1️⃣ Lock Step
            //-------------------------------------------------

            var step = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM WorkflowExecutionSteps
                WHERE Id = @StepId
                AND CompanyId = @CompanyId
                FOR UPDATE;",
                new { input.StepId, CompanyId }, tx);

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
                "REWORKED" => "Reworked",
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

            //var approverInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"SELECT EmployeeName FROM Users WHERE Id = @UserId", new { UserId = userId }, tx);
            string approverName = empDetail?.firstname + " " + empDetail?.midname + " " + empDetail?.lastname;

            var notifyPlaceholders = new Dictionary<string, string>
            {
                { "ID", Convert.ToString(requestInfo?.requestnumber) ?? "Unknown" },
                { "Approver", approverName },
                { "Observation", input.Observation ?? "" }
            };

            string initiatorId = "";
            if (requestInfo != null && requestInfo!.createdby != null)
            {
                //int.TryParse(Convert.ToString(requestInfo!.createdby), out initiatorId);
                initiatorId = requestInfo!.createdby;
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
                SET Status = 'Rejected'
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

                if (initiatorId != string.Empty)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestRejected, CompanyId, (int)requestInfo!.id, initiatorId, notifyPlaceholders);
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
                SET Status = 'Reworked'
                WHERE Id = @ExecutionId;",
                    new { ExecutionId = executionId }, tx);

                await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET Status = @DraftStatus,
                IsContentFinalized = FALSE
                WHERE Id =
                (
                    SELECT EntityId
                    FROM WorkflowExecutions
                    WHERE Id = @ExecutionId
                );",
                    new { ExecutionId = executionId, DraftStatus = DocumentRequestStatus.Draft }, tx);

                await tx.CommitAsync();

                if (initiatorId != string.Empty)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestRevertedForRework, CompanyId, (int)requestInfo!.id, initiatorId, notifyPlaceholders);
                }

                return true;
            }

            //-------------------------------------------------
            // APPROVE → If last approver in group
            //-------------------------------------------------

            List<string> nextStepApprovers = new List<string>();
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

                    nextStepApprovers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, next.Value, tx);
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
                        CompanyId,
                        requestId,
                        userId,
                        tx);

                    //await CreateDocumentFromApprovedRequestAsync(input.CompanyId, executionId, input.UserId, tx);
                }
            }

            await tx.CommitAsync();

            if (nextStepApprovers.Any() && requestInfo != null)
            {
                foreach(var approver in nextStepApprovers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestApprovedForwarded, CompanyId, (int)requestInfo.id, approver, notifyPlaceholders);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }


    public async Task<PaginationResult<MyRequestPendingDto>> GetMyRequestsPendingApprovalAsync(MyRequestFilterDto filter)
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            filter.Initiator = userId.ToString();
            filter.CompanyId = CompanyId; 

            var searchCondition = "";
            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Replace("'", "''").ToUpper();
                searchCondition = $@"
              AND (
                  UPPER(dr.DocumentName) LIKE '%{search}%'
                  OR UPPER(dr.RequestNumber) LIKE '%{search}%'
              )";
            }

            // Sorting (whitelisted)
            string sortColumn = filter.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNAME" => "dr.DocumentName",
                "REQUESTNUMBER" => "dr.RequestNumber",
                "SUBMITTEDAT" => "dr.SubmittedAt",
                "STATUS" => "dr.Status",
                _ => "dr.SubmittedAt"
            };

            // Defaulting to DESC to match the original "ORDER BY dr.SubmittedAt DESC"
            string sortDirection = filter.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (filter.PageNumber - 1) * filter.PageSize;

            var fromJoins = @"
            FROM Vw_DocumentRequests dr
            LEFT JOIN WorkflowExecutions we
                ON we.CompanyId = dr.CompanyId
                AND we.EntityId = dr.Id
                AND we.EntityType = 'Request'
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
                AND wes.IsActive = TRUE
            LEFT JOIN tblEmployee e ON wes.AssignedUserId = e.empcode::Text
            --LEFT JOIN Roles r ON wes.AssignedRoleId = r.Id
            LEFT JOIN WorkflowStepDefinitions wsd 
                ON wsd.CompanyId = wes.CompanyId
                AND wsd.Id = wes.StepDefinitionId";

            var baseWhere = @"
            WHERE dr.CompanyId = @CompanyId
              AND dr.SubmittedBy = @Initiator
              AND dr.IsDeleted = FALSE
              AND dr.Status IN (1, 2) -- 1 = Submitted, 2 = In Approval
              --AND (@Status IS NULL OR dr.Status = @Status)";

            var dataSql = $@"
            SELECT 
                dr.*,
                wes.StepOrder        AS CurrentStepOrder,
                wsd.StepType         AS CurrentStepType,
                LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' ||COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS CurrentAssignedUser, 
                wes.AssignedUserId   AS CurrentAssignedUserId,
                wes.AssignedRoleId   AS CurrentAssignedRoleId
            {fromJoins}
            {baseWhere}
            {searchCondition}
            ORDER BY {sortColumn} {sortDirection}
            OFFSET {offset} ROWS FETCH NEXT {filter.PageSize} ROWS ONLY;
            ";

            var countSql = $@"
                SELECT COUNT(1)
                {fromJoins}
                {baseWhere}
                {searchCondition};
            ";

            var results = await _common.QueryAsync<dynamic>(dataSql, filter);
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, filter);

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

            return new PaginationResult<MyRequestPendingDto>
            {
                Items = dtos,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    // This mehod is doing the same jo as GetWorkflowDetailsAsync. 
    public async Task<IEnumerable<DocumentRequestDetailsDto>> GetDocumentObservationDetailsAsync(int documentId, string entityType)
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var sql = $@"
                SELECT 
                    we.EntityId,
                    we.EntityType,
                    wes.StepOrder,
                    wsd.StepType,
                    wes.AssignedUserId,
                    e.empCode AS employeecode,
                    LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName, 
                    ejp.roleid AS RoleId,
                    r.name AS RoleName,
                    ejp.dsgid AS DesignationId,
                    desig.name AS Designation,
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
                LEFT JOIN tblEmployee e
                   ON e.empCode = wes.AssignedUserId
                LEFT JOIN public.tblempjobprofile ejp 
                   ON ejp.empid = e.empid AND ejp.Active = TRUE
                LEFT JOIN public.tblsetupsdetail r 
                   ON r.sdlid = ejp.roleid
                LEFT JOIN public.tblsetupsdetail desig 
                   ON desig.sdlid = ejp.dsgid
                WHERE wes.CompanyId = @CompanyId
                  AND we.EntityId = @DocumentId  -- The Document ID
                  AND we.EntityType = @EntityType  -- Should be 'Document'
                ORDER BY wes.StepOrder";

            return await _common.QueryAsync<DocumentRequestDetailsDto>(sql, new
            {
                CompanyId,
                documentId,  // Renamed from requestId for clarity
                entityType   // Should be "Document"
            });
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }


    public async Task<IEnumerable<DocumentRequestDetailsDto>> GetWorkflowDetailsAsync(int documentId, string entityType) 
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var sql = $@"
                SELECT 
                    we.EntityId,
                    we.EntityType,
                    wes.StepOrder,
                    wsd.StepType,
                    wes.AssignedUserId,
                    e.empCode AS employeecode,
                    LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName, 
                    ejp.roleid AS RoleId,
                    r.name AS RoleName,
                    ejp.dsgid AS DesignationId,
                    desig.name AS Designation,
                    wes.Decision,
                    wes.Observation,
                    wes.ActionAt AS StatusUpdatedOn,
                    wes.IsActive,
                    we.Status AS ExecutionStatus,
                    we.StartedAt,
                    we.CompletedAt,
                    COALESCE(wes.ActionAt, we.StartedAt) AS ReceivedOn,
                    ualc.Division,
                    ualc.Department,
                    ualc.SubDepartment,
                    ualc.BusinessDomain
                FROM WorkflowExecutionSteps wes
                INNER JOIN WorkflowExecutions we
                    ON we.CompanyId = wes.CompanyId
                    AND we.Id = wes.WorkflowExecutionId
                INNER JOIN WorkflowStepDefinitions wsd
                    ON wsd.CompanyId = wes.CompanyId
                    AND wsd.Id = wes.StepDefinitionId
                LEFT JOIN tblEmployee e
                   ON e.empCode = wes.AssignedUserId
                LEFT JOIN public.tblempjobprofile ejp 
                   ON ejp.empid = e.empid AND ejp.Active = TRUE
                LEFT JOIN public.tblsetupsdetail r 
                   ON r.sdlid = ejp.roleid
                LEFT JOIN public.tblsetupsdetail desig 
                   ON desig.sdlid = ejp.dsgid
                LEFT JOIN (
                    SELECT 
                        CompanyId, 
                        EmployeeCode, 
                        MAX(DivisionName) AS Division, 
                        MAX(DepartmentName) AS Department, 
                        MAX(SubDepartmentName) AS SubDepartment, 
                        MAX(BusinessDomainName) AS BusinessDomain
                    FROM vw_UserAccessLevelCabinets
                    GROUP BY CompanyId, EmployeeCode
                ) ualc ON ualc.CompanyId = wes.CompanyId AND ualc.EmployeeCode = wes.AssignedUserId
                WHERE wes.CompanyId = @CompanyId
                  AND we.EntityId = @DocumentId
                  AND we.EntityType = @EntityType
                ORDER BY wes.StepOrder";

            return await _common.QueryAsync<DocumentRequestDetailsDto>(sql, new
            {
                CompanyId,
                documentId,
                entityType
            });
        }
        catch (Exception ex)
        {
            throw ex;
        }
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
    public async Task<int> CreateDocumentFromApprovedRequestAsync(int companyId, int requestId, string userId, NpgsqlTransaction transaction)
    {
        //await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);


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
                    CompanyId, DocumentNumber, RequestId, DocumentTypeCode, Title, DivisionCode, DepartmentCode,
                    SubDepartmentCode, BusinessDomainCode, DocumentURL, CreatedBy, LastModifiedBy
                )
                VALUES
                (
                    @CompanyId,
                    'DOC-' || nextval('document_seq'),
                    @RequestId, @DocumentTypeCode, @Title, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode, @DocumentUrl, @CreatedBy, @LastModifiedBy
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
                CreatedBy = userId,
                LastModifiedBy = userId
            }, transaction);

            //-----------------------------------------
            // 3️⃣ Create Draft Version 0.1
            //-----------------------------------------
            await _common.ExecuteAsync(@"
            INSERT INTO DocumentVersions
            (
                CompanyId, DocumentId, Version, VersionType, Content, CreatedBy, LastModifiedBy
            )
            VALUES
            (
                @CompanyId, @DocumentId, '0.1', 1, @Content, @CreatedBy, @LastModifiedBy
            )
            ", new
            {
                companyId,
                documentId,
                Content = request.proposedcontent,
                CreatedBy = userId,
                LastModifiedBy = userId
            }, transaction);

            //-----------------------------------------
            // 4️⃣ Insert Draft State
            //-----------------------------------------
            await _common.ExecuteAsync(@"
            INSERT INTO DocumentStateHistory
            (
                CompanyId, DocumentId, ToStateId, ChangedBy
            )
            VALUES
            (
                @CompanyId, @DocumentId, 1, @UserId
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
                    CompanyId, DocumentId, EmployeeCode, CreatedBy
                )
                SELECT
                    CompanyId, @DocumentId, @EmployeeCode, @CreatedBy
                FROM DocumentRequestUserDistributions
                WHERE DocumentRequestId = @RequestId;",
            new
            {
                DocumentId = documentId,
                RequestId = requestId,
                EmployeeCode = userId,
                CreatedBy = userId
            }, transaction);

            //await transaction.CommitAsync();
            return documentId;
        }
        catch (Exception ex)
        {
            //await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<bool> DeleteAsync(string code)
    {
        try
        {
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

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

}
