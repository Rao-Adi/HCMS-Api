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
    }

    // Uploaded file names sometimes arrive with the extension duplicated (e.g. a browser-downloaded
    // "Template.docx" gets re-saved by the OS/browser as "Template.docx.docx" before the user
    // re-uploads it during a revision) — collapses exactly one trailing repeat back to the original.
    private static string SanitizeDuplicatedExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return fileName;

        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(ext) &&
            fileName.Length > ext.Length * 2 &&
            fileName.EndsWith(ext + ext, StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Substring(0, fileName.Length - ext.Length);
        }

        return fileName;
    }

    public async Task<long> CreateDraftDocumentRequestAsync(DraftDocumentRequestDto dto)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());


            string? draftFileUrl = null;
            if (dto.DraftFile != null && dto.DraftFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "drafts");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileExtension = Path.GetExtension(dto.DraftFile.FileName);
                var fileName = SanitizeDuplicatedExtension(dto.DraftFile.FileName);
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
                    DraftFileUrl, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode, Status, CreatedBy, LastModifiedBy, IsContentFinalized, ParentDocumentId
                )
                VALUES
                (
                    @CompanyId, 'DR-' || nextval('document_request_seq'), @RequestType, @DocumentTypeCode, @DocumentName, @Justification, @ProposedContent,
                    @DraftFileUrl, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode, @Status, @CreatedBy, @LastModifiedBy, FALSE, @ParentDocumentId
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
                CreatedBy = empCode,
                LastModifiedBy = empCode,
                dto.ParentDocumentId
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
                        CreatedBy = empCode,
                        LastModifiedBy = empCode
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
                        CreatedBy = empCode,
                        LastModifiedBy = empCode
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
                UserId = empCode
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
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            string? draftFileUrl = null;
            if (dto.DraftFile != null && dto.DraftFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "drafts");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileExtension = Path.GetExtension(dto.DraftFile.FileName); 
                var fileName = SanitizeDuplicatedExtension(dto.DraftFile.FileName);
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
                LastModifiedBy = empCode,
                DraftStatus = DocumentRequestStatus.Draft
            },
            transaction);

            if (rowsAffected == 0)
                throw new Exception(
                    "Draft was modified by another user OR already submitted.");


            // Remove old distributions
            await _common.ExecuteAsync(@"
                DELETE FROM DocumentRequestRoleDistributions
                WHERE DocumentRequestId = @RequestId AND CompanyId = @CompanyId;",
                new { dto.RequestId, CompanyId }, transaction);

            await _common.ExecuteAsync(@"
                DELETE FROM DocumentRequestUserDistributions
                WHERE DocumentRequestId = @RequestId AND CompanyId = @CompanyId;",
                new { dto.RequestId, CompanyId }, transaction);

            // Insert new distributions
            await InsertDistributionsAsync(CompanyId, dto.RequestId,
                dto!.DistributionList, dto!.UserIds,
                empCode, transaction);


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
                    UserId = empCode
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

     
    public async Task<long> CreateAndSubmitDocumentRequestAsync(DraftDocumentRequestDto dto)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            // 1. Get User/Company Info
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // ────────────────────────────────────────────────
            // Convert empty strings → null (this is the key fix)
            // ────────────────────────────────────────────────
            string? Normalize(string? value) =>
                string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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

                var fileName = SanitizeDuplicatedExtension(dto.DraftFile.FileName);
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
                    Status, CreatedBy, LastModifiedBy, IsContentFinalized, SubmittedAt, SubmittedBy, ParentDocumentId
                )
                VALUES
                (
                    @CompanyId, 'DR-' || nextval('document_request_seq'), @RequestType, @DocumentTypeCode, @DocumentName, @Justification, @ProposedContent,
                    @DraftFileUrl, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode, 
                    @Status, @UserId, @UserId, TRUE, NOW(), @UserId, @ParentDocumentId
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
                UserId = empCode,
                dto.ParentDocumentId
            }, transaction);

            // 4. Insert Distribution Lists
            await InsertDistributionsAsync(CompanyId, requestId, dto.DistributionList, dto.UserIds, empCode, transaction);


            // 5. Workflow Execution Logic
            var policyId = await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId AND EntityType = 'Request' AND DocumentTypeCode = @DocType
                AND (((DivisionCode IS NULL OR DivisionCode = '') AND (@DivisionCode IS NULL OR @DivisionCode = '')) OR DivisionCode = @DivisionCode)
                AND (((DepartmentCode IS NULL OR DepartmentCode = '') AND (@DepartmentCode IS NULL OR @DepartmentCode = '')) OR DepartmentCode = @DepartmentCode)
                AND (((SubDepartmentCode IS NULL OR SubDepartmentCode = '') AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '')) OR SubDepartmentCode = @SubDepartmentCode)
                AND (((BusinessDomainCode IS NULL OR BusinessDomainCode = '') AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '')) OR BusinessDomainCode = @BusinessDomainCode)
                AND IsActive = TRUE AND IsDeleted = FALSE;",
            new
            {
                CompanyId,
                DocType = dto.DocumentTypeCode,
                DivisionCode = Normalize(dto.DivisionCode),
                DepartmentCode = Normalize(dto.DepartmentCode),
                SubDepartmentCode = Normalize(dto.SubDepartmentCode),
                BusinessDomainCode = Normalize(dto.BusinessDomainCode)
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
                    StartedBy = empCode
                }, transaction);

            var stepDefs = await _common.QueryAsync<dynamic>(@"
                SELECT Id, UserId, RoleId, DesignationId, StepOrder
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId AND CompanyId = @CompanyId
                ORDER BY StepOrder;", new { VersionId = versionId, CompanyId }, transaction);

            int runningStepOrder = 1;
            int insertedSteps = 0;

            foreach (var stepDef in stepDefs)
            {
                if (stepDef.userid != null)
                {
                    string actualUserId = stepDef.userid;
                    var transferTo = await _common.ExecuteScalarAsync<string>(@"
                        SELECT EmployeeTo FROM ResponsibilityTransfers 
                        WHERE EmployeeFrom = @EmpFrom 
                        AND CompanyId = @CompanyId AND Status = 2 
                        AND EffectiveDateFrom <= CURRENT_DATE 
                        AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE) 
                        ORDER BY Id DESC LIMIT 1;", 
                        new { EmpFrom = actualUserId, CompanyId }, transaction);

                    if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                    await _common.ExecuteAsync(@"
                        INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                        VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                        new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, transaction);

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

                    foreach (var emp in employees)
                    {
                        string actualUserId = emp;
                        var transferTo = await _common.ExecuteScalarAsync<string>(@"
                            SELECT EmployeeTo FROM ResponsibilityTransfers 
                            WHERE EmployeeFrom = @EmpFrom 
                            AND CompanyId = @CompanyId AND Status = 2 
                            AND EffectiveDateFrom <= CURRENT_DATE 
                            AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE) 
                            ORDER BY Id DESC LIMIT 1;", 
                            new { EmpFrom = actualUserId, CompanyId }, transaction);

                        if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                        await _common.ExecuteAsync(@"
                            INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                            VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                            new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, transaction);
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
                WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                AND StepOrder = (SELECT MIN(StepOrder) FROM WorkflowExecutionSteps WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId);",
                new { ExecutionId = executionId, CompanyId }, transaction);

            // 6. History
            await InsertHistoryAsync(CompanyId, requestId, DocumentRequestStatus.Submitted, empCode, "Request Created and Submitted", transaction);


            // 7. Prepare and Send Notification

            // Prepare notification data
            var activeStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.StepOrder, dr.RequestNumber
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN DocumentRequests dr ON dr.Id = we.EntityId
                WHERE wes.WorkflowExecutionId = @ExecutionId AND wes.CompanyId = @CompanyId
                AND wes.IsActive = TRUE LIMIT 1;", new { ExecutionId = executionId, CompanyId }, transaction);

            List<string> approvers = new List<string>();
            string requestNumberStr = requestId.ToString();
            if (activeStep != null)
            {
                requestNumberStr = Convert.ToString(activeStep.requestnumber) ?? requestId.ToString();
                approvers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, (int)activeStep.steporder, transaction);
            }

            if (approvers.Any())
            {
                var placeholders = new Dictionary<string, string> { { "ID", requestNumberStr } };
                foreach (var approver in approvers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, CompanyId, (int)requestId, approver, placeholders, transaction);
                }
            }

            // 8. Commit
            await transaction.CommitAsync();

            return requestId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // UC-22: One-shot create+submit for a Revision of an existing document. Kept separate from
    // CreateAndSubmitDocumentRequestAsync because a Revision must reference a ParentDocumentId,
    // must validate that content differs from that document, and may route to a different policy.
    public async Task<long> CreateAndSubmitRevisionDocumentRequestAsync(DraftDocumentRequestDto dto)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            // 1. Get User/Company Info
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            string? Normalize(string? value) =>
                string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            // Validation: Justification is mandatory for submission
            if (string.IsNullOrWhiteSpace(dto.Justification))
                throw new CustomException("Justification is required to submit a document request.", 400);

            // A Revision must reference the existing document being revised
            if (dto.ParentDocumentId == null)
                throw new CustomException("ParentDocumentId is required to submit a revision request.", 400);

            // 2. Handle File Upload
            string? draftFileUrl = null;
            if (dto.DraftFile != null && dto.DraftFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "drafts");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileName = SanitizeDuplicatedExtension(dto.DraftFile.FileName);
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.DraftFile.CopyToAsync(stream);
                }
                draftFileUrl = $"/uploads/drafts/{fileName}";
            }

            // UC-22 Validation: Ensure content has been altered from the original document being revised
            var originalDoc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.DocumentURL, dv.Content
                FROM Documents d
                LEFT JOIN DocumentVersions dv ON d.Id = dv.DocumentId AND dv.IsActive = TRUE
                WHERE d.Id = @DocumentId AND d.CompanyId = @CompanyId
                ORDER BY dv.CreatedAt DESC LIMIT 1;",
                new { DocumentId = dto.ParentDocumentId, CompanyId }, transaction);

            if (originalDoc != null)
            {
                bool contentUnchanged = (dto.ProposedContent == originalDoc.content) || (string.IsNullOrWhiteSpace(dto.ProposedContent) && string.IsNullOrWhiteSpace(originalDoc.content));
                bool fileUnchanged = (draftFileUrl == originalDoc.documenturl) || (string.IsNullOrWhiteSpace(draftFileUrl) && string.IsNullOrWhiteSpace(originalDoc.documenturl));

                if (contentUnchanged && fileUnchanged)
                    throw new CustomException("Document Content must be altered from the original version before submitting a revision request.", 400);
            }

            // 3. Insert Document Request with 'Submitted' status
            var requestId = await _common.ExecuteScalarAsync<long>(@"
                INSERT INTO DocumentRequests
                (
                    CompanyId, RequestNumber, DocumentRequestTypeCode, DocumentTypeCode, DocumentName, Justification, ProposedContent,
                    DraftFileUrl, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode,
                    Status, CreatedBy, LastModifiedBy, IsContentFinalized, SubmittedAt, SubmittedBy, ParentDocumentId
                )
                VALUES
                (
                    @CompanyId, 'DR-' || nextval('document_request_seq'), @DocumentRequestTypeCode, @DocumentTypeCode, @DocumentName, @Justification, @ProposedContent,
                    @DraftFileUrl, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode,
                    @Status, @UserId, @UserId, TRUE, NOW(), @UserId, @ParentDocumentId
                )
                RETURNING Id;",
            new
            {
                CompanyId,
                dto.DocumentTypeCode,
                dto.DocumentRequestTypeCode,
                dto.DocumentName,
                dto.Justification,
                dto.ProposedContent,
                DraftFileUrl = draftFileUrl,
                dto.DivisionCode,
                dto.DepartmentCode,
                dto.SubDepartmentCode,
                dto.BusinessDomainCode,
                Status = DocumentRequestStatus.Submitted,
                UserId = empCode,
                dto.ParentDocumentId
            }, transaction);

            // 4. Insert Distribution Lists
            await InsertDistributionsAsync(CompanyId, requestId, dto.DistributionList, dto.UserIds, empCode, transaction);


            // 5. Workflow Execution Logic
            // Prefer a Revision-specific policy; fall back to the standard Request policy if
            // none has been configured for this DocumentType/scope yet.
            async Task<long?> ResolvePolicyIdAsync(string entityType) => await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId AND EntityType = @EntityType AND DocumentTypeCode = @DocType
                AND (((DivisionCode IS NULL OR DivisionCode = '') AND (@DivisionCode IS NULL OR @DivisionCode = '')) OR DivisionCode = @DivisionCode)
                AND (((DepartmentCode IS NULL OR DepartmentCode = '') AND (@DepartmentCode IS NULL OR @DepartmentCode = '')) OR DepartmentCode = @DepartmentCode)
                AND (((SubDepartmentCode IS NULL OR SubDepartmentCode = '') AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '')) OR SubDepartmentCode = @SubDepartmentCode)
                AND (((BusinessDomainCode IS NULL OR BusinessDomainCode = '') AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '')) OR BusinessDomainCode = @BusinessDomainCode)
                AND IsActive = TRUE AND IsDeleted = FALSE;",
            new
            {
                CompanyId,
                EntityType = entityType,
                DocType = dto.DocumentTypeCode,
                DivisionCode = Normalize(dto.DivisionCode),
                DepartmentCode = Normalize(dto.DepartmentCode),
                SubDepartmentCode = Normalize(dto.SubDepartmentCode),
                BusinessDomainCode = Normalize(dto.BusinessDomainCode)
            }, transaction);

            var policyId = await ResolvePolicyIdAsync("Revision") ?? await ResolvePolicyIdAsync("Request");

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
                    StartedBy = empCode
                }, transaction);

            var stepDefs = await _common.QueryAsync<dynamic>(@"
                SELECT Id, UserId, RoleId, DesignationId, StepOrder
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId AND CompanyId = @CompanyId
                ORDER BY StepOrder;", new { VersionId = versionId, CompanyId }, transaction);

            int runningStepOrder = 1;
            int insertedSteps = 0;

            foreach (var stepDef in stepDefs)
            {
                if (stepDef.userid != null)
                {
                    string actualUserId = stepDef.userid;
                    var transferTo = await _common.ExecuteScalarAsync<string>(@"
                        SELECT EmployeeTo FROM ResponsibilityTransfers
                        WHERE EmployeeFrom = @EmpFrom
                        AND CompanyId = @CompanyId AND Status = 2
                        AND EffectiveDateFrom <= CURRENT_DATE
                        AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE)
                        ORDER BY Id DESC LIMIT 1;",
                        new { EmpFrom = actualUserId, CompanyId }, transaction);

                    if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                    await _common.ExecuteAsync(@"
                        INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                        VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                        new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, transaction);

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

                    foreach (var emp in employees)
                    {
                        string actualUserId = emp;
                        var transferTo = await _common.ExecuteScalarAsync<string>(@"
                            SELECT EmployeeTo FROM ResponsibilityTransfers
                            WHERE EmployeeFrom = @EmpFrom
                            AND CompanyId = @CompanyId AND Status = 2
                            AND EffectiveDateFrom <= CURRENT_DATE
                            AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE)
                            ORDER BY Id DESC LIMIT 1;",
                            new { EmpFrom = actualUserId, CompanyId }, transaction);

                        if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                        await _common.ExecuteAsync(@"
                            INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                            VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                            new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, transaction);
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
                WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                AND StepOrder = (SELECT MIN(StepOrder) FROM WorkflowExecutionSteps WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId);",
                new { ExecutionId = executionId, CompanyId }, transaction);

            // 6. History
            await InsertHistoryAsync(CompanyId, requestId, DocumentRequestStatus.Submitted, empCode, "Revision Request Created and Submitted", transaction);


            // 7. Prepare and Send Notification

            var activeStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.StepOrder, dr.RequestNumber
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN DocumentRequests dr ON dr.Id = we.EntityId
                WHERE wes.WorkflowExecutionId = @ExecutionId AND wes.CompanyId = @CompanyId
                AND wes.IsActive = TRUE LIMIT 1;", new { ExecutionId = executionId, CompanyId }, transaction);

            List<string> approvers = new List<string>();
            string requestNumberStr = requestId.ToString();
            if (activeStep != null)
            {
                requestNumberStr = Convert.ToString(activeStep.requestnumber) ?? requestId.ToString();
                approvers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, (int)activeStep.steporder, transaction);
            }

            if (approvers.Any())
            {
                var placeholders = new Dictionary<string, string> { { "ID", requestNumberStr } };
                foreach (var approver in approvers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, CompanyId, (int)requestId, approver, placeholders, transaction);
                }
            }

            // 8. Commit
            await transaction.CommitAsync();

            return requestId;
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
        //var empId = _utilities.GetEmpid(clientIp);
        //var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

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

    private async Task<bool> SubmitDocumentRequestAsync(SubmitDocumentRequestDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();

        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

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
                    WHERE d.Id = @DocumentId AND d.CompanyId = @CompanyId
                    ORDER BY dv.CreatedAt DESC LIMIT 1;",
                    new { DocumentId = request.documentid, CompanyId }, tx);

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
            WHERE DocumentRequestId = @RequestId AND CompanyId = @CompanyId;", new { input.RequestId, CompanyId }, tx);

            await _common.ExecuteAsync(@"
            DELETE FROM DocumentRequestUserDistributions
            WHERE DocumentRequestId = @RequestId AND CompanyId = @CompanyId;", new { input.RequestId, CompanyId }, tx);
            //-------------------------------------------------
            // RE-INSERT ALL DISTRIBUTIONS (INCLUDING ROLE EXPANSION)
            //-------------------------------------------------
            await InsertDistributionsAsync(CompanyId, input.RequestId,
                input.DistributionList, input.UserIds,
                empCode, tx);


            //-------------------------------------------------
            // Resolve Correct Policy FIRST (Scope Routing)
            //-------------------------------------------------

            string Normalize(string? v) => string.IsNullOrWhiteSpace(v) || v == "0" || v.ToLower() == "null" ? "" : v.Trim();

            var policyId = await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id
                FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId
                AND EntityType = 'Request'
                AND DocumentTypeCode = @DocType
                AND (((DivisionCode IS NULL OR DivisionCode = '') AND (@DivisionCode IS NULL OR @DivisionCode = '')) OR DivisionCode = @DivisionCode)
                AND (((DepartmentCode IS NULL OR DepartmentCode = '') AND (@DepartmentCode IS NULL OR @DepartmentCode = '')) OR DepartmentCode = @DepartmentCode)
                AND (((SubDepartmentCode IS NULL OR SubDepartmentCode = '') AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '')) OR SubDepartmentCode = @SubDepartmentCode)
                AND (((BusinessDomainCode IS NULL OR BusinessDomainCode = '') AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '')) OR BusinessDomainCode = @BusinessDomainCode)
                AND IsActive = TRUE
                AND IsDeleted = FALSE;",
            new
            {
                CompanyId,
                DocType = request.documenttypecode,
                DivisionCode = Normalize(Convert.ToString(request.divisioncode)),
                DepartmentCode = Normalize(Convert.ToString(request.departmentcode)),
                SubDepartmentCode = Normalize(Convert.ToString(request.subdepartmentcode)),
                BusinessDomainCode = Normalize(Convert.ToString(request.businessdomaincode))
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
                    StartedBy = empCode
                }, tx);

            var stepDefs = await _common.QueryAsync<dynamic>(@"
                SELECT Id, UserId, RoleId, DesignationId, StepOrder
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId AND CompanyId = @CompanyId
                ORDER BY StepOrder;", new { VersionId = versionId, CompanyId }, tx);

            int runningStepOrder = 1;
            int inserted = 0;

            foreach (var stepDef in stepDefs)
            {
                if (stepDef.userid != null)
                {
                    string actualUserId = stepDef.userid;
                    var transferTo = await _common.ExecuteScalarAsync<string>(@"
                        SELECT EmployeeTo FROM ResponsibilityTransfers 
                        WHERE EmployeeFrom = @EmpFrom 
                        AND CompanyId = @CompanyId AND Status = 2 
                        AND EffectiveDateFrom <= CURRENT_DATE 
                        AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE) 
                        ORDER BY Id DESC LIMIT 1;", 
                        new { EmpFrom = actualUserId, CompanyId }, tx);

                    if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                    await _common.ExecuteAsync(@"
                        INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                        VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                        new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, tx);
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

                    foreach (var emp in employees)
                    {
                        string actualUserId = emp;
                        var transferTo = await _common.ExecuteScalarAsync<string>(@"
                            SELECT EmployeeTo FROM ResponsibilityTransfers 
                            WHERE EmployeeFrom = @EmpFrom 
                            AND CompanyId = @CompanyId AND Status = 2 
                            AND EffectiveDateFrom <= CURRENT_DATE 
                            AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE) 
                            ORDER BY Id DESC LIMIT 1;", 
                            new { EmpFrom = actualUserId, CompanyId }, tx);

                        if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                        await _common.ExecuteAsync(@"
                            INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                            VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                            new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, tx);
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
                WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                AND StepOrder =
                (
                    SELECT MIN(StepOrder)
                    FROM WorkflowExecutionSteps
                    WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                );",
                new { ExecutionId = executionId, CompanyId }, tx);

            //-------------------------------------------------
            // Update Request
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET Status = @Status,
                    SubmittedAt = NOW(),
                    SubmittedBy = @UserId,
                    IsContentFinalized = TRUE
                WHERE Id = @RequestId AND CompanyId = @CompanyId;",
                new
                {
                    Status = DocumentRequestStatus.Submitted,
                    UserId = empCode,
                    input.RequestId,
                    CompanyId 
                }, tx);

            // Prepare notification data
            var activeStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.StepOrder, dr.RequestNumber
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN DocumentRequests dr ON dr.Id = we.EntityId
                WHERE wes.WorkflowExecutionId = @ExecutionId AND wes.CompanyId = @CompanyId
                AND wes.IsActive = TRUE LIMIT 1;", new { ExecutionId = executionId, CompanyId }, tx);

            List<string> approvers = new List<string>();
            string requestNumber = input.RequestId.ToString();
            if (activeStep != null)
            {
                requestNumber = Convert.ToString(activeStep.requestnumber) ?? input.RequestId.ToString();
                approvers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, (int)activeStep.steporder, tx);
            }

            if (approvers.Any())
            {
                var placeholders = new Dictionary<string, string> { { "ID", requestNumber } };
                foreach (var approver in approvers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, CompanyId, (int)input.RequestId, approver, placeholders, tx);
                }
            }

            await tx.CommitAsync();

            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // UC-22: Submits an existing Revision draft. Kept separate from SubmitDocumentRequestAsync
    // because Revision requests carry different validation (content must differ from the
    // original document being revised) and can route to a different WorkflowPolicy.
    public async Task<bool> SubmitRevisionDocumentRequestAsync(SubmitRevisionDocumentRequestDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

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

            // DocumentRequestTypeCode is a lookup-driven code (e.g. "DRT-0002"), not the literal
            // string "Revision", so it can't be reliably string-matched here. ParentDocumentId is
            // the authoritative signal that this draft is actually a revision of an existing document.
            if (request.parentdocumentid == null)
                throw new Exception("This endpoint only accepts Revision requests linked to an existing document (ParentDocumentId is missing).");

            // UC-22 Validation: Justification is mandatory
            if (string.IsNullOrWhiteSpace(request.justification))
                throw new Exception("Justification is required to submit a document request.");

            // UC-22 Validation: Ensure content has been altered from the original document being revised
            if (request.parentdocumentid != null)
            {
                var originalDoc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT d.DocumentURL, dv.Content
                    FROM Documents d
                    LEFT JOIN DocumentVersions dv ON d.Id = dv.DocumentId AND dv.IsActive = TRUE
                    WHERE d.Id = @DocumentId AND d.CompanyId = @CompanyId
                    ORDER BY dv.CreatedAt DESC LIMIT 1;",
                    new { DocumentId = request.parentdocumentid, CompanyId }, tx);

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
            WHERE DocumentRequestId = @RequestId AND CompanyId = @CompanyId;", new { input.RequestId, CompanyId }, tx);

            await _common.ExecuteAsync(@"
            DELETE FROM DocumentRequestUserDistributions
            WHERE DocumentRequestId = @RequestId AND CompanyId = @CompanyId;", new { input.RequestId, CompanyId }, tx);
            //-------------------------------------------------
            // RE-INSERT ALL DISTRIBUTIONS (INCLUDING ROLE EXPANSION)
            //-------------------------------------------------
            await InsertDistributionsAsync(CompanyId, input.RequestId,
                input.DistributionList, input.UserIds,
                empCode, tx);

            //-------------------------------------------------
            // Resolve Correct Policy FIRST (Scope Routing)
            // Prefer a Revision-specific policy; fall back to the standard Request policy if
            // none has been configured for this DocumentType/scope yet.
            //-------------------------------------------------

            string Normalize(string? v) => string.IsNullOrWhiteSpace(v) || v == "0" || v.ToLower() == "null" ? "" : v.Trim();

            async Task<long?> ResolvePolicyIdAsync(string entityType) => await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id
                FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId
                AND EntityType = @EntityType
                AND DocumentTypeCode = @DocType
                AND (((DivisionCode IS NULL OR DivisionCode = '') AND (@DivisionCode IS NULL OR @DivisionCode = '')) OR DivisionCode = @DivisionCode)
                AND (((DepartmentCode IS NULL OR DepartmentCode = '') AND (@DepartmentCode IS NULL OR @DepartmentCode = '')) OR DepartmentCode = @DepartmentCode)
                AND (((SubDepartmentCode IS NULL OR SubDepartmentCode = '') AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '')) OR SubDepartmentCode = @SubDepartmentCode)
                AND (((BusinessDomainCode IS NULL OR BusinessDomainCode = '') AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '')) OR BusinessDomainCode = @BusinessDomainCode)
                AND IsActive = TRUE
                AND IsDeleted = FALSE;",
            new
            {
                CompanyId,
                EntityType = entityType,
                DocType = request.documenttypecode,
                DivisionCode = Normalize(Convert.ToString(request.divisioncode)),
                DepartmentCode = Normalize(Convert.ToString(request.departmentcode)),
                SubDepartmentCode = Normalize(Convert.ToString(request.subdepartmentcode)),
                BusinessDomainCode = Normalize(Convert.ToString(request.businessdomaincode))
            }, tx);

            var policyId = await ResolvePolicyIdAsync("Revision") ?? await ResolvePolicyIdAsync("Request");

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
                    StartedBy = empCode
                }, tx);

            var stepDefs = await _common.QueryAsync<dynamic>(@"
                SELECT Id, UserId, RoleId, DesignationId, StepOrder
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId AND CompanyId = @CompanyId
                ORDER BY StepOrder;", new { VersionId = versionId, CompanyId }, tx);

            int runningStepOrder = 1;
            int inserted = 0;

            foreach (var stepDef in stepDefs)
            {
                if (stepDef.userid != null)
                {
                    string actualUserId = stepDef.userid;
                    var transferTo = await _common.ExecuteScalarAsync<string>(@"
                        SELECT EmployeeTo FROM ResponsibilityTransfers
                        WHERE EmployeeFrom = @EmpFrom
                        AND CompanyId = @CompanyId AND Status = 2
                        AND EffectiveDateFrom <= CURRENT_DATE
                        AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE)
                        ORDER BY Id DESC LIMIT 1;",
                        new { EmpFrom = actualUserId, CompanyId }, tx);

                    if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                    await _common.ExecuteAsync(@"
                        INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                        VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                        new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, tx);
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

                    foreach (var emp in employees)
                    {
                        string actualUserId = emp;
                        var transferTo = await _common.ExecuteScalarAsync<string>(@"
                            SELECT EmployeeTo FROM ResponsibilityTransfers
                            WHERE EmployeeFrom = @EmpFrom
                            AND CompanyId = @CompanyId AND Status = 2
                            AND EffectiveDateFrom <= CURRENT_DATE
                            AND (EffectiveDateTo IS NULL OR EffectiveDateTo >= CURRENT_DATE)
                            ORDER BY Id DESC LIMIT 1;",
                            new { EmpFrom = actualUserId, CompanyId }, tx);

                        if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                        await _common.ExecuteAsync(@"
                            INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                            VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                            new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, tx);
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
                WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                AND StepOrder =
                (
                    SELECT MIN(StepOrder)
                    FROM WorkflowExecutionSteps
                    WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                );",
                new { ExecutionId = executionId, CompanyId }, tx);

            //-------------------------------------------------
            // Update Request
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET Status = @Status,
                    SubmittedAt = NOW(),
                    SubmittedBy = @UserId,
                    IsContentFinalized = TRUE
                WHERE Id = @RequestId AND CompanyId = @CompanyId;",
                new
                {
                    Status = DocumentRequestStatus.Submitted,
                    UserId = empCode,
                    input.RequestId,
                    CompanyId
                }, tx);

            // Prepare notification data
            var activeStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.StepOrder, dr.RequestNumber
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN DocumentRequests dr ON dr.Id = we.EntityId
                WHERE wes.WorkflowExecutionId = @ExecutionId AND wes.CompanyId = @CompanyId
                AND wes.IsActive = TRUE LIMIT 1;", new { ExecutionId = executionId, CompanyId }, tx);

            List<string> approvers = new List<string>();
            string requestNumber = input.RequestId.ToString();
            if (activeStep != null)
            {
                requestNumber = Convert.ToString(activeStep.requestnumber) ?? input.RequestId.ToString();
                approvers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, (int)activeStep.steporder, tx);
            }

            if (approvers.Any())
            {
                var placeholders = new Dictionary<string, string> { { "ID", requestNumber } };
                foreach (var approver in approvers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingRequest, CompanyId, (int)input.RequestId, approver, placeholders, tx);
                }
            }

            await tx.CommitAsync();

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
            string empCode,
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
                        CreatedBy = empCode,
                        LastModifiedBy = empCode
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
                        CreatedBy = empCode,
                        LastModifiedBy = empCode
                    }, tx);
                }
            }
            
            //-------------------------------------------------
            // Auto-expand roles into specific users and insert them into UserDistributions
            //-------------------------------------------------
            if (roles?.Any() == true)
            {
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentRequestUserDistributions 
                    (CompanyId, DocumentRequestId, EmployeeCode, CreatedBy, LastModifiedBy)
                    SELECT DISTINCT 
                        dr.CompanyId, 
                        dr.DocumentRequestId, 
                        TRIM(e.empcode), 
                        @CreatedBy, 
                        @LastModifiedBy
                    FROM DocumentRequestRoleDistributions dr
                    INNER JOIN tblEmployee e ON e.CompanyId = dr.CompanyId AND COALESCE(e.Active, 1) = 1  AND e.CompanyId = @CompanyId
                    INNER JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.Active, TRUE) = TRUE  AND ejp.CompanyId = @CompanyId
                    LEFT JOIN UserAccessLevels ual ON LTRIM(RTRIM(ual.EmployeeCode::text), '0') = LTRIM(RTRIM(e.empcode::text), '0') AND ual.IsActive = TRUE
                    WHERE dr.DocumentRequestId = @RequestId
                      AND dr.CompanyId = @CompanyId
                      AND ejp.roleid = dr.RoleId
                      AND (COALESCE(dr.DivisionCode, '') = '' OR dr.DivisionCode = ual.DivisionCode)
                      AND (COALESCE(dr.DepartmentCode, '') = '' OR dr.DepartmentCode = ual.DepartmentCode)
                      AND (COALESCE(dr.SubDepartmentCode, '') = '' OR dr.SubDepartmentCode = ual.SubDepartmentCode)
                      AND (COALESCE(dr.BusinessDomainCode, '') = '' OR dr.BusinessDomainCode = ual.BusinessDomainCode)
                      AND NOT EXISTS (
                          SELECT 1 FROM DocumentRequestUserDistributions u 
                          WHERE u.DocumentRequestId = dr.DocumentRequestId 
                            AND u.EmployeeCode = TRIM(e.empcode)
                      );",
                    new
                    {
                        CompanyId = companyId,
                        RequestId = requestId,
                        CreatedBy = empCode,
                        LastModifiedBy = empCode
                    }, tx);
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
        string empCode,
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
            ChangedBy = empCode,
            Comments = comments
        }, tx);
    }
     
    public async Task<PaginationResult<DocumentRequestReadDto>> GetMyInboxRequestsAsync(GetPendingRequestDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());
             
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
                "CREATEDBY" => "CreatedBy",
                "LASTMODIFIEDAT" => "LastModifiedAt",
                "LASTMODIFIEDBY" => "LastModifiedBy",
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
                UserId = empCode,
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
                    ExecutionStatus = GetValue<string>(dict, "executionstatus"),
                    DraftFileUrl = GetValue<string>(dict, "draftfileurl"),
                    IsContentFinalized = GetValue<bool>(dict, "iscontentfinalized"),
                    DraftContentLastModifiedAt = GetValue<DateTime?>(dict, "draftcontentlastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    DraftContentLastModifiedBy = GetValue<string>(dict, "draftcontentlastmodifiedby"),
                    IsActive = GetValue<bool>(dict, "isactive"),
                    IsDeleted = GetValue<bool>(dict, "isdeleted"),
                    CreatedAt = GetValue<DateTime?>(dict, "createdat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    CreatedBy = GetValue<string>(dict, "createdby"),
                    LastModifiedAt = GetValue<DateTime?>(dict, "lastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    LastModifiedBy = GetValue<string>(dict, "lastmodifiedby"),
                    PreviousVersionCreatedOn = GetValue<DateTime?>(dict, "previousversioncreatedon")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    PreviousVersionCreatedBy = GetValue<string>(dict, "previousversioncreatedby")
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


    public async Task<byte[]> ExportMyInboxRequestsAsync(GetPendingRequestDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

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
                "CREATEDBY" => "CreatedBy",
                "LASTMODIFIEDAT" => "LastModifiedAt",
                "LASTMODIFIEDBY" => "LastModifiedBy",
                _ => "CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            // Limited to the same columns the "My Approvals – Request for Document Creation/Update"
            // grid shows (documentColumnDefsWithoutStatus in my-approval-request.ts) instead of
            // SELECT * — the export previously dumped every raw column from the underlying
            // function (ids, codes, internal flags, etc.) instead of matching what's on screen.
            // Dates are formatted in SQL to match the UI's "Mon DD, YYYY HH24:MI:SS" display.
            var dataSql = $@"SELECT
                    documenttype AS ""Document Type"",
                    requestnumber AS ""Request ID"",
                    documentname AS ""Document Name"",
                    justification AS ""Justification"",
                    rowversion AS ""Proposed Version Number"",
                    division AS ""Division"",
                    department AS ""Department"",
                    subdepartment AS ""Sub-Department"",
                    businessdomain AS ""Business Domain"",
                    createdby AS ""Request Created By"",
                    TO_CHAR(createdat, 'Mon DD, YYYY HH24:MI:SS') AS ""Request Created On"",
                    previousversioncreatedby AS ""Previous Version Created By"",
                    TO_CHAR(previousversioncreatedon, 'Mon DD, YYYY HH24:MI:SS') AS ""Previous Version Created On""
                FROM fn_get_my_inbox_requests(
                    @CompanyId,
                    @UserId,
                    @RequestStatus
                )
                {whereClause}
                ORDER BY {sortColumn} {sortDirection};";

            var queryParams = new
            {
                CompanyId,
                UserId = empCode,
                input.RequestStatus,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode,
                input.DocumentTypeCode
            };

            var requests = await _common.QueryAsync<dynamic>(dataSql, queryParams);

            if (!requests.Any())
            {
                return Array.Empty<byte>();
            }

            var sb = new System.Text.StringBuilder();
            // Add header row
            var headers = ((IDictionary<string, object>)requests.First()).Keys;
            sb.AppendLine(string.Join(",", headers));

            // Add data rows
            foreach (var row in requests)
            {
                var dict = row as IDictionary<string, object>;
                var values = new List<string>();
                foreach (var header in headers)
                {
                    var raw = dict[header];
                    // Postgres timestamp columns come back as DateTime, not string, so
                    // ToString() would use the server's current-culture default format instead
                    // of the requested "Aug, 02 2026 09:00:00" style.
                    var value = raw is DateTime dt
                        ? dt.ToString("MMM, dd yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
                        : raw?.ToString() ?? "";
                    // Escape commas and quotes
                    var escapedValue = $"\"{value.Replace("\"", "\"\"")}\"";
                    values.Add(escapedValue);
                }
                sb.AppendLine(string.Join(",", values));
            }

            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }
        catch (Exception ex)
        {
            // In a real application, you'd log this exception
            throw new CustomException("Failed to export data.", 500);
        }
    }
    public async Task<PaginationResult<DocumentRequestReadDto>> GetDraftDocumentRequestAsync(GetDocumentDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

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
                "CREATEDBY" => "dr.CreatedBy",
                "LASTMODIFIEDAT" => "dr.LastModifiedAt",
                "LASTMODIFIEDBY" => "dr.LastModifiedBy",
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
                CreatedBy = empCode
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
                LEFT JOIN tblEmployee e on LPAD(drd.EmployeeCode::text, 9, '0') = e.empCode  AND e.CompanyId = @CompanyId
                INNER JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE  AND ejp.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid  AND des.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid AND des_fallback.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid AND r.CompanyId = @CompanyId
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
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

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
                "APPROVED" => "Approved",
                "REJECTED" => "Rejected",
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
            WHERE dr.Id = (SELECT EntityId FROM WorkflowExecutions WHERE Id = @ExecutionId AND CompanyId = @CompanyId) 
            AND dr.CompanyId = @CompanyId", new { ExecutionId = executionId, CompanyId }, tx);

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
                AND CompanyId = @CompanyId
                AND StepOrder = @StepOrder
                AND Decision IS NULL;",
                    new
                    {
                        ExecutionId = executionId,
                        StepOrder = stepOrder,
                        CompanyId
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
                WHERE Id = @StepId AND CompanyId = @CompanyId;",
                new
                {
                    Decision = decision,
                    input.Observation,
                    input.StepId,
                    CompanyId 
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
            if (decision == "Rejected" || decision == "REJECTED")
            {
                await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutions
                SET Status = 'Rejected'
                WHERE Id = @ExecutionId AND CompanyId = @CompanyId;",
                    new { ExecutionId = executionId, CompanyId }, tx);

                await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET Status = @RejectedStatus
                WHERE CompanyId = @CompanyId AND Id =
                (
                    SELECT EntityId
                    FROM WorkflowExecutions
                    WHERE Id = @ExecutionId AND CompanyId = @CompanyId
                );",
                    new { ExecutionId = executionId, CompanyId, RejectedStatus = DocumentRequestStatus.Rejected }, tx); // Or whatever your enum uses for Rejected

                if (initiatorId != string.Empty)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestRejected, CompanyId, (int)requestInfo!.id, initiatorId, notifyPlaceholders, tx);
                }

                await tx.CommitAsync();

                return true;
            }

            //-------------------------------------------------
            // REWORK → Cancel Workflow and Revert to Draft (Non-Terminal)
            //-------------------------------------------------
            if (decision == "Reworked" || decision == "REWORKED")
            {
                await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutions
                SET Status = 'Reworked'
                WHERE Id = @ExecutionId AND CompanyId = @CompanyId;",
                    new { ExecutionId = executionId, CompanyId }, tx);

                await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET Status = @DraftStatus,
                IsContentFinalized = FALSE
                WHERE CompanyId = @CompanyId AND Id =
                (
                    SELECT EntityId
                    FROM WorkflowExecutions
                    WHERE Id = @ExecutionId AND CompanyId = @CompanyId
                );",
                    new { ExecutionId = executionId, CompanyId, DraftStatus = DocumentRequestStatus.Draft }, tx);

                if (initiatorId != string.Empty)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestRevertedForRework, CompanyId, (int)requestInfo!.id, initiatorId, notifyPlaceholders, tx);
                }

                await tx.CommitAsync();

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
                    WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                    AND StepOrder > @Current;",
                        new
                        {
                            ExecutionId = executionId,
                            Current = stepOrder,
                            CompanyId
                        }, tx);

                if (next.HasValue)
                {
                    //-------------------------------------------------
                    // Activate next step
                    //-------------------------------------------------

                    var rows = await _common.ExecuteAsync(@"
                        UPDATE WorkflowExecutionSteps
                        SET IsActive = TRUE
                        WHERE WorkflowExecutionId = @ExecutionId AND CompanyId = @CompanyId
                        AND StepOrder = @Next;",
                            new
                            {
                                ExecutionId = executionId,
                                Next = next.Value,
                                CompanyId = CompanyId
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
                    WHERE Id = @ExecutionId AND CompanyId = @CompanyId;",
                        new { ExecutionId = executionId, CompanyId }, tx);

                    await _common.ExecuteAsync(@"
                    UPDATE DocumentRequests
                    SET Status = @Approved
                    WHERE CompanyId = @CompanyId AND Id =
                    (
                        SELECT EntityId
                        FROM WorkflowExecutions
                        WHERE Id = @ExecutionId AND CompanyId = @CompanyId
                    );",
                        new
                        {
                            ExecutionId = executionId,
                            Approved = DocumentRequestStatus.Approved,
                            CompanyId = CompanyId
                        }, tx);


                    //-------------------------------------------------
                    // COMPLETE WORKFLOW
                    //-------------------------------------------------
                    var requestId = await _common.ExecuteScalarAsync<int>(@"
                    SELECT EntityId
                    FROM WorkflowExecutions
                    WHERE Id = @ExecutionId AND CompanyId = @CompanyId",
                        new { ExecutionId = executionId, CompanyId }, tx);

                    await CreateDocumentFromApprovedRequestAsync(
                        CompanyId,
                        requestId,
                        empCode,
                        tx);

                }
            }

            if (nextStepApprovers.Any() && requestInfo != null)
            {
                foreach(var approver in nextStepApprovers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestApprovedForwarded, CompanyId, (int)requestInfo.id, approver, notifyPlaceholders, tx);
                }
            }
            else if (requestInfo != null && !string.IsNullOrEmpty(initiatorId))
            {
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.RequestApprovedForwarded, CompanyId, (int)requestInfo.id, initiatorId, notifyPlaceholders, tx);
            }

            await tx.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<EffectiveDocumentDetailsDto> GetEffectiveDocumentDetailsForRevisionAsync(int documentId)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            // 1. Fetch Cabinet Info & Effective Content
            var docSql = @"
                SELECT 
                    d.Id AS DocumentId,
                    d.Title AS DocumentName,
                    d.DocumentTypeCode,
                    d.DivisionCode,
                    d.DepartmentCode,
                    d.SubDepartmentCode,
                    d.BusinessDomainCode,
                    dv.Content,
                    dv.Version
                FROM Documents d
                INNER JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.CompanyId = d.CompanyId AND dv.VersionType = 2
                WHERE d.Id = @DocumentId AND d.CompanyId = @CompanyId AND d.IsDeleted = FALSE;";

            var document = await _common.QueryFirstOrDefaultAsync<EffectiveDocumentDetailsDto>(docSql, new { DocumentId = documentId, CompanyId });

            if (document == null)
                throw new CustomException("Effective document not found or it has no published version.", 404);

            // 2. Fetch Role Distributions (Mapping DocumentRoleDistributions -> DistributionListReadDto)
            var roleDistSql = @"
                SELECT drd.Id,
                       drd.CompanyId,
                       c.Name AS Company,
                       drd.DocumentId AS DocumentRequestId, -- Mapped to trick frontend DTO into reusing the Request table UI
                       drd.RoleId,
                       r.Name AS Role,
                       drd.DistributionType AS DistributionTypeId,
                       dt.Name AS DistributionType,
                       drd.DivisionCode, div.Name AS Division,
                       drd.DepartmentCode, dep.Name AS Department,
                       drd.SubDepartmentCode, subd.Name AS SubDepartment,
                       drd.BusinessDomainCode, bd.Name AS BusinessDomain
                FROM DocumentRoleDistributions drd
                LEFT JOIN Divisions div ON drd.DivisionCode = div.Code 
                LEFT JOIN Departments dep ON drd.DepartmentCode = dep.Code
                LEFT JOIN SubDepartments subd ON drd.SubDepartmentCode = subd.Code
                LEFT JOIN BusinessDomains bd ON drd.BusinessDomainCode = bd.Code
                LEFT JOIN Companies c ON drd.CompanyId = c.Id
                LEFT JOIN Roles r ON drd.RoleId = r.Id 
                LEFT JOIN DistributionTypes dt ON drd.DistributionType = dt.Id
                WHERE drd.DocumentId = @DocumentId AND drd.CompanyId = @CompanyId;";

            document.DistributionList = (await _common.QueryAsync<DistributionListReadDto>(roleDistSql, new { DocumentId = documentId, CompanyId })).ToList();

            // 3. Fetch User Distributions (Mapping DocumentUserDistributions -> DocumentRequestUserDistribution)
            var userDistSql = @"
                SELECT dud.Id, dud.CompanyId, dud.DocumentId AS DocumentRequestId, dud.EmployeeCode,
                       LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' ||COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName,
                       COALESCE(des.name, des_fallback.name) AS Designation, r.name AS Role
                FROM DocumentUserDistributions dud
                LEFT JOIN tblEmployee e on LPAD(dud.EmployeeCode::text, 9, '0') = e.empCode  AND e.CompanyId = @CompanyId
                LEFT JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE  AND ejp.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid  AND des.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid  AND des_fallback.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid  AND r.CompanyId = @CompanyId
                WHERE dud.DocumentId = @DocumentId AND dud.CompanyId = @CompanyId;";

            document.UserList = (await _common.QueryAsync<DocumentRequestUserDistribution>(userDistSql, new { DocumentId = documentId, CompanyId })).ToList();

            return document;
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<PaginationResult<EffectiveDocumentDetailsDto>> GetEffectiveDocumentsForRevisionAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE d.CompanyId = @CompanyId 
                  AND d.IsDeleted = FALSE 
                  AND d.IsActive = TRUE";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(d.Title) LIKE '%{search}%'
                    OR UPPER(d.DocumentNumber) LIKE '%{search}%'
                )";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNUMBER" => "d.DocumentNumber",
                "DOCUMENTNAME" => "d.Title",
                "TITLE" => "d.Title",
                "CREATEDAT" => "d.CreatedAt",
                "CREATEDBY" => "d.CreatedBy",
                "LASTMODIFIEDAT" => "d.LastModifiedAt",
                "LASTMODIFIEDBY" => "d.LastModifiedBy",
                _ => "d.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            var dataSql = $@"
                SELECT d.*
                FROM Vw_Documents d
                INNER JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.CompanyId = d.CompanyId AND dv.VersionType = 2 
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            var countSql = $@"
                SELECT COUNT(1)
                FROM Documents d
                INNER JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.CompanyId = d.CompanyId AND dv.VersionType = 2
                {whereClause};";

            var queryParams = new
            {
                CompanyId
            };

            var dynamicRequests = await _common.QueryAsync<dynamic>(dataSql, queryParams);
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, new { CompanyId });


            var requests = new List<EffectiveDocumentDetailsDto>();
            foreach (var row in dynamicRequests)
            {
                var dict = row as IDictionary<string, object>;
                if (dict == null) continue;

                requests.Add(new EffectiveDocumentDetailsDto
                {
                    Id = GetValue<int>(dict, "id"),
                    DocumentNumber = GetValue<string>(dict, "documentnumber"),
                    CompanyId = GetValue<int>(dict, "companyid"),
                    Company = GetValue<string>(dict, "company"),
                    RequestId = GetValue<int>(dict, "requestid"),
                    Version = GetValue<string>(dict, "version"),
                    VersionType = GetValue<string>(dict, "versiontype"),
                    NextReviewDate = GetValue<string>(dict, "nextreviewdate"),
                    ParentDocumentId = GetValue<int>(dict, "parentdocumentid"),
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
                    DocumentName = GetValue<string>(dict, "title"),  
                    DocumentURL = GetValue<string>(dict, "documenturl"),  
                    IsActive = GetValue<bool>(dict, "isactive"),
                    IsDeleted = GetValue<bool>(dict, "isdeleted"),
                    CreatedAt = GetValue<DateTime?>(dict, "createdat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    CreatedBy = GetValue<string>(dict, "createdby"),
                    LastModifiedAt = GetValue<DateTime?>(dict, "lastmodifiedat")?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                    LastModifiedBy = GetValue<string>(dict, "lastmodifiedby"),
                    CreatedByName = GetValue<string>(dict, "createdbyname"),
                    LastModifiedByName = GetValue<string>(dict, "lastmodifiedbyname")
                });
            }
           
            if (!requests.Any())
                return new PaginationResult<EffectiveDocumentDetailsDto>
                {
                    Items = new List<EffectiveDocumentDetailsDto>(),
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
                LEFT JOIN tblEmployee e on LPAD(drd.EmployeeCode::text, 9, '0') = e.empCode  AND e.CompanyId = @CompanyId
                INNER JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE  AND ejp.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid  AND des.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail des_fallback ON e.dsgid = des_fallback.sdlid  AND des_fallback.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail r ON ejp.roleid = r.sdlid  AND r.CompanyId = @CompanyId
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

            return new PaginationResult<EffectiveDocumentDetailsDto>
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


    public async Task<PaginationResult<MyRequestPendingDto>> GetMyRequestsPendingApprovalAsync(MyRequestFilterDto filter)
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            filter.Initiator = empCode;
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
                "CREATEDAT" => "dr.CreatedAt",
                "CREATEDBY" => "dr.CreatedBy",
                "LASTMODIFIEDAT" => "dr.LastModifiedAt",
                "LASTMODIFIEDBY" => "dr.LastModifiedBy",
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
            LEFT JOIN tblEmployee e ON wes.AssignedUserId = e.empcode::Text   AND e.CompanyId = @CompanyId
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
                {searchCondition};";

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

    public async Task<dynamic> GetMyRequestsPendingApprovalCountAsync()
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());
            

            var countSql = $@"SELECT COUNT(1)                
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
                    LEFT JOIN tblEmployee e ON wes.AssignedUserId = e.empcode::Text   AND e.CompanyId = @CompanyId
                    --LEFT JOIN Roles r ON wes.AssignedRoleId = r.Id
                    LEFT JOIN WorkflowStepDefinitions wsd 
                        ON wsd.CompanyId = wes.CompanyId
                        AND wsd.Id = wes.StepDefinitionId
                
                    WHERE dr.CompanyId = @CompanyId
                      AND dr.SubmittedBy = @empCode
                      AND dr.IsDeleted = FALSE
                      AND dr.Status IN (1, 2) -- 1 = Submitted, 2 = In Approval
                      --AND (@Status IS NULL OR dr.Status = @Status)
                        ;";

            var myDocumentsCounts = await _common.QueryFirstOrDefaultAsync<dynamic>(countSql, new { CompanyId, empCode });
            return myDocumentsCounts;
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
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);
            //var empId = _utilities.GetEmpid(clientIp);
            //var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // A document can go through multiple requests over its lifetime (the original Creation
            // request, then one or more Revision requests). Resolve forward through that chain
            // (Request -> the Document it produced -> any newer Request revising that Document) so
            // callers always get observations for the Actual/current request, even if the requestId
            // they passed is from before the latest revision.
            if (entityType == "Request")
            {
                var actualRequestId = await _common.ExecuteScalarAsync<int?>(@"
                    WITH RECURSIVE RequestChain AS (
                        SELECT dr.Id, dr.CreatedAt
                        FROM DocumentRequests dr
                        WHERE dr.Id = @RequestId AND dr.CompanyId = @CompanyId

                        UNION ALL

                        SELECT nextdr.Id, nextdr.CreatedAt
                        FROM RequestChain rc
                        INNER JOIN Documents d ON d.RequestId = rc.Id AND d.CompanyId = @CompanyId
                        INNER JOIN DocumentRequests nextdr ON nextdr.ParentDocumentId = d.Id AND nextdr.CompanyId = @CompanyId
                    )
                    SELECT Id FROM RequestChain ORDER BY CreatedAt DESC LIMIT 1;",
                    new { RequestId = documentId, CompanyId });

                if (actualRequestId.HasValue)
                    documentId = actualRequestId.Value;
            }

            //var sql = $@"
            //    SELECT 
            //        we.EntityId,
            //        we.EntityType,
            //        wes.StepOrder,
            //        wsd.StepType,
            //        wes.AssignedUserId,
            //        e.empCode AS employeecode,
            //        LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName, 
            //        ejp.roleid AS RoleId,
            //        r.name AS RoleName,
            //        ejp.dsgid AS DesignationId,
            //        desig.name AS Designation,
            //        wes.Decision,
            //        wes.Observation,
            //        wes.ActionAt,
            //        wes.IsActive
            //    FROM WorkflowExecutionSteps wes
            //    INNER JOIN WorkflowExecutions we
            //        ON we.CompanyId = wes.CompanyId
            //        AND we.Id = wes.WorkflowExecutionId
            //        AND we.EntityType = @EntityType  -- Critical filter: Only get steps from Document workflow
            //    INNER JOIN WorkflowStepDefinitions wsd
            //        ON wsd.CompanyId = wes.CompanyId
            //        AND wsd.Id = wes.StepDefinitionId
            //    LEFT JOIN tblEmployee e
            //       ON e.empCode = wes.AssignedUserId
            //    LEFT JOIN public.tblempjobprofile ejp 
            //       ON ejp.empid = e.empid AND ejp.Active = TRUE
            //    LEFT JOIN public.tblsetupsdetail r 
            //       ON r.sdlid = ejp.roleid
            //    LEFT JOIN public.tblsetupsdetail desig 
            //       ON desig.sdlid = ejp.dsgid
            //    WHERE wes.CompanyId = @CompanyId
            //      AND we.EntityId = @DocumentId  -- The Document ID
            //      AND we.EntityType = @EntityType  -- Should be 'Document'
            //    ORDER BY wes.StepOrder";

            string sql = $@"
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
                        wes.IsActive,
	                    -- 🔹 Audit Fields
                        COALESCE(c.EmployeeName, we.StartedBy::text) AS CreatedByName,
 
                        COALESCE(m.EmployeeName, we.StartedBy::text) AS LastModifiedByName
                    FROM WorkflowExecutionSteps wes

                    INNER JOIN WorkflowExecutions we
                        ON we.CompanyId = wes.CompanyId
                        AND we.Id = wes.WorkflowExecutionId
                        AND we.EntityType = @EntityType

                    INNER JOIN WorkflowStepDefinitions wsd
                        ON wsd.CompanyId = wes.CompanyId
                        AND wsd.Id = wes.StepDefinitionId

                    -- Assigned Employee
                    LEFT JOIN tblEmployee e
                        ON e.empCode = wes.AssignedUserId AND e.CompanyId = @CompanyId

                    LEFT JOIN public.tblempjobprofile ejp 
                        ON ejp.empid = e.empid 
                        AND ejp.Active = TRUE   AND ejp.CompanyId = @CompanyId

                    LEFT JOIN public.tblsetupsdetail r 
                        ON r.sdlid = ejp.roleid   AND r.CompanyId = @CompanyId

                    LEFT JOIN public.tblsetupsdetail desig 
                        ON desig.sdlid = ejp.dsgid   AND desig.CompanyId = @CompanyId

                    -- 🔹 Created By Employee
                    LEFT JOIN Vw_EmployeeNames c 
                        ON c.CleanEmpCode = LTRIM(we.StartedBy::text, '0')

                    -- 🔹 Last Modified By Employee
                    LEFT JOIN Vw_EmployeeNames m 
                        ON m.CleanEmpCode = LTRIM(we.StartedBy::text, '0')

                    WHERE wes.CompanyId = @CompanyId
                      AND we.EntityId = @DocumentId
                      AND we.EntityType = @EntityType
                      AND wes.Observation IS NOT NULL
                      AND TRIM(wes.Observation) <> ''

                    ORDER BY wes.StepOrder;";

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
                   ON e.empCode = wes.AssignedUserId  AND e.CompanyId = @CompanyId
                LEFT JOIN public.tblempjobprofile ejp 
                   ON ejp.empid = e.empid AND ejp.Active = TRUE  AND ejp.CompanyId = @CompanyId
                LEFT JOIN public.tblsetupsdetail r 
                   ON r.sdlid = ejp.roleid AND r.CompanyId = @CompanyId
                LEFT JOIN public.tblsetupsdetail desig 
                   ON desig.sdlid = ejp.dsgid AND desig.CompanyId = @CompanyId
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

    // Returns the full revision chain for a document — the original document plus every
    // subsequent revision of it — regardless of which document in the chain is passed in.
    // Walks Documents.ParentDocumentId backward to find the original, then forward to collect
    // every revision made since, so the caller doesn't need to know where in the chain
    // `documentId` sits.
    public async Task<IEnumerable<RevisionHistoryItemDto>> GetDocumentRevisionHistoryAsync(int documentId)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var sql = @"
                WITH RECURSIVE Ancestors AS (
                    SELECT Id, ParentDocumentId, 0 AS Depth
                    FROM Documents
                    WHERE Id = @DocumentId AND CompanyId = @CompanyId

                    UNION ALL

                    SELECT d.Id, d.ParentDocumentId, a.Depth + 1
                    FROM Documents d
                    INNER JOIN Ancestors a ON d.Id = a.ParentDocumentId
                    WHERE d.CompanyId = @CompanyId
                ),
                RootDoc AS (
                    SELECT Id AS RootId FROM Ancestors ORDER BY Depth DESC LIMIT 1
                ),
                Chain AS (
                    SELECT d.Id
                    FROM Documents d
                    INNER JOIN RootDoc r ON d.Id = r.RootId

                    UNION ALL

                    SELECT d2.Id
                    FROM Documents d2
                    INNER JOIN Chain c ON d2.ParentDocumentId = c.Id
                    WHERE d2.CompanyId = @CompanyId
                )
                SELECT
                    d.Id AS DocumentId,
                    d.DocumentNumber,
                    d.Title AS DocumentName,
                    d.ParentDocumentId,
                    dv.Version,
                    d.RequestId,
                    dr.RequestNumber,
                    dr.DocumentRequestTypeCode,
                    dr.Justification,
                    dr.CreatedAt AS RequestedOn,
                    COALESCE(reqEmp.Name, dr.CreatedBy) AS RequestedBy,
                    apprv.ChangedAt AS ApprovedOn,
                    COALESCE(apEmp.Name, apprv.ChangedBy) AS ApprovedBy,
                    eff.ChangedAt AS EffectiveOn,
                    COALESCE(efEmp.Name, eff.ChangedBy) AS EffectiveBy,
                    curState.Code AS CurrentStatus,
                    NOT EXISTS (
                        SELECT 1 FROM Documents child
                        WHERE child.ParentDocumentId = d.Id AND child.CompanyId = @CompanyId
                    ) AS IsCurrentVersion
                FROM Chain c
                INNER JOIN Documents d ON d.Id = c.Id AND d.CompanyId = @CompanyId
                LEFT JOIN DocumentRequests dr ON dr.Id = d.RequestId AND dr.CompanyId = @CompanyId
                -- LATERAL + LIMIT 1 everywhere below: Vw_employeeNames can still hold more than one
                -- row per cleanempcode within a company, and DocumentVersions isn't DB-constrained
                -- to exactly one VersionType=2 row per document. A plain LEFT JOIN on either fans a
                -- single document row out into duplicates; LIMIT 1 guarantees at most one row per
                -- document no matter what.
                LEFT JOIN LATERAL (
                    SELECT dv2.Version
                    FROM DocumentVersions dv2
                    WHERE dv2.DocumentId = d.Id AND dv2.CompanyId = d.CompanyId AND dv2.VersionType = 2
                    ORDER BY dv2.Id DESC LIMIT 1
                ) dv ON TRUE
                LEFT JOIN LATERAL (
                    SELECT ven.employeename AS Name
                    FROM Vw_employeeNames ven
                    WHERE ven.cleanempcode = LTRIM(RTRIM(dr.CreatedBy::text), '0')
                    ORDER BY (ven.companyid = @CompanyId) DESC
                    LIMIT 1
                ) reqEmp ON TRUE
                LEFT JOIN LATERAL (
                    SELECT dsh.ChangedAt, dsh.ChangedBy
                    FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.DocumentId = d.Id AND dsh.CompanyId = d.CompanyId AND ds.Code = 'APPROVED'
                    ORDER BY dsh.ChangedAt ASC, dsh.Id ASC LIMIT 1
                ) apprv ON TRUE
                LEFT JOIN LATERAL (
                    SELECT ven.employeename AS Name
                    FROM Vw_employeeNames ven
                    WHERE ven.cleanempcode = LTRIM(RTRIM(apprv.ChangedBy::text), '0')
                    ORDER BY (ven.companyid = @CompanyId) DESC
                    LIMIT 1
                ) apEmp ON TRUE
                LEFT JOIN LATERAL (
                    SELECT dsh.ChangedAt, dsh.ChangedBy
                    FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.DocumentId = d.Id AND dsh.CompanyId = d.CompanyId AND ds.Code = 'EFFECTIVE'
                    ORDER BY dsh.ChangedAt ASC, dsh.Id ASC LIMIT 1
                ) eff ON TRUE
                LEFT JOIN LATERAL (
                    SELECT ven.employeename AS Name
                    FROM Vw_employeeNames ven
                    WHERE ven.cleanempcode = LTRIM(RTRIM(eff.ChangedBy::text), '0')
                    ORDER BY (ven.companyid = @CompanyId) DESC
                    LIMIT 1
                ) efEmp ON TRUE
                LEFT JOIN LATERAL (
                    SELECT ds.Code
                    FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.DocumentId = d.Id AND dsh.CompanyId = d.CompanyId
                    ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                ) curState ON TRUE
                ORDER BY d.CreatedAt ASC;";

            return await _common.QueryAsync<RevisionHistoryItemDto>(sql, new { DocumentId = documentId, CompanyId });
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    // Helper method to safely get values from the dynamic row
    private static T GetValue<T>(IDictionary<string, object> row, string columnName)
    {
        if (row.ContainsKey(columnName) && row[columnName] != null && row[columnName] != DBNull.Value)
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
         Document Draft 1.0 Ready */
    public async Task<int> CreateDocumentFromApprovedRequestAsync(int companyId, int requestId, string empCode, NpgsqlTransaction transaction)
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

            // 🔹 Get Review Period from Policy
            var reviewYears = await _common.QueryFirstOrDefaultAsync<int?>(@"
                SELECT ReviewPeriodYears
                FROM DocumentReviewPolicies
                WHERE DocumentTypeCode = @DocumentTypeCode
                  AND CompanyId = @CompanyId
                  AND IsActive = TRUE AND IsDeleted = FALSE
                LIMIT 1;",
            new { DocumentTypeCode = request.documenttypecode, companyId }, transaction);

            // 🔹 Calculate Next Review Date
            DateTime? nextReviewDate = null;
            if (reviewYears.HasValue && reviewYears.Value > 0)
            {
                nextReviewDate = DateTime.Now.AddYears(reviewYears.Value);
            }

            // Generate the system-designed document number
            string documentNumber = await GenerateDocumentNumberAsync(
                companyId,
                (string)request.divisioncode,
                (string)request.departmentcode,
                (string)request.subdepartmentcode,
                (string)request.documenttypecode,
                (int?)request.parentdocumentid,
                (string)request.businessdomaincode,
                transaction);

            //-----------------------------------------
            // 2️⃣ Create Document
            //-----------------------------------------
            var documentId = await _common.ExecuteScalarAsync<int>(@"
                INSERT INTO Documents
                ( 
                    CompanyId, DocumentNumber, ParentDocumentId, RequestId, DocumentTypeCode, Title, NextReviewDate, DivisionCode, DepartmentCode,
                    SubDepartmentCode, BusinessDomainCode, DocumentURL, CreatedBy, LastModifiedBy
                )
                VALUES
                (
                    @CompanyId,
                    @DocumentNumber,
                    @ParentDocumentId,
                    @RequestId, @DocumentTypeCode, @Title, @NextReviewDate, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode, @DocumentUrl, @CreatedBy, @LastModifiedBy
                )
                RETURNING Id
                ", new
            {
                companyId,
                requestId,
                DocumentNumber = documentNumber,
                ParentDocumentId = (int?)request.parentdocumentid,
                DocumentTypeCode = request.documenttypecode,
                Title = request.documentname,
                NextReviewDate = nextReviewDate,
                request.divisioncode,
                request.departmentcode,
                request.subdepartmentcode,
                request.businessdomaincode,
                DocumentUrl = request.draftfileurl,
                CreatedBy = request.createdby,
                LastModifiedBy = request.createdby
            }, transaction);

            //-----------------------------------------
            // 3️⃣ Create Draft Version 1.0
            //-----------------------------------------
            await _common.ExecuteAsync(@"
            INSERT INTO DocumentVersions
            (
                CompanyId, DocumentId, Version, VersionType, Content, CreatedBy, LastModifiedBy
            )
            VALUES
            (
                @CompanyId, @DocumentId, '1.0', 1, @Content, @CreatedBy, @LastModifiedBy
            )
            ", new
            {
                companyId,
                documentId,
                Content = request.proposedcontent,
                CreatedBy = request.createdby,
                LastModifiedBy = request.createdby
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
                @CompanyId, @DocumentId, 1, @empCode
            )
            ", new { companyId, documentId, empCode }, transaction);

            //-----------------------------------------
            // 5️⃣ Link Back To Request
            //-----------------------------------------
            await _common.ExecuteAsync(@"
                UPDATE DocumentRequests
                SET DocumentId = @DocumentId
                WHERE Id = @RequestId AND CompanyId = @CompanyId
                ", new { documentId, requestId, CompanyId = companyId }, transaction);

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
                UserId = request.createdby
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
                    CompanyId, @DocumentId, EmployeeCode, @CreatedBy
                FROM DocumentRequestUserDistributions
                WHERE DocumentRequestId = @RequestId;",
            new
            {
                DocumentId = documentId,
                RequestId = requestId,
                CreatedBy = request.createdby
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



    public async Task<PaginationResult<DocumentRequestReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE d.IsDeleted = False AND d.CompanyId = " + CompanyId + @" AND d.IsActive = " + (input.IsActive ? "True" : "False");

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
                "CREATEDAT" => "d.CreatedAt",
                "CREATEDBY" => "d.CreatedBy",
                "LASTMODIFIEDAT" => "d.LastModifiedAt",
                "LASTMODIFIEDBY" => "d.LastModifiedBy",
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

    public async Task<object> GetMyRequestCountsAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Query 1: Counts for requests CREATED BY the current user
            var myRequestsQuery = @"
                SELECT 
                    COUNT(1) FILTER (WHERE Status IN (1, 2)) AS Pending,
                    COUNT(1) FILTER (WHERE Status = 3) AS Approved,
                    COUNT(1) FILTER (WHERE Status IN (0, 4, 5)) AS RejectedOrReverted
                FROM DocumentRequests
                WHERE CompanyId = @CompanyId
                  AND CreatedBy = @empCode
                  AND IsDeleted = FALSE;";

            var myRequestsCounts = await _common.QueryFirstOrDefaultAsync<dynamic>(myRequestsQuery, new { CompanyId, empCode });

             // Query 2: Counts for requests in the current user's INBOX (for action)
             var myInboxQuery = @"
             SELECT
                 COUNT(1) FILTER (WHERE we.Status = 'Running' AND wes.IsActive = TRUE AND wes.Decision IS NULL) AS Pending,
                 COUNT(1) FILTER (WHERE wes.Decision = 'Approved') AS Approved,
                 COUNT(1) FILTER (WHERE (we.Status = 'Rejected' AND wes.Decision = 'Rejected') OR (we.Status = 'Reworked' AND wes.Decision = 'Reworked')) AS RejectedOrReverted
             FROM WorkflowExecutionSteps wes
             JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId AND we.CompanyId = wes.CompanyId
             JOIN Vw_DocumentRequests dr ON dr.Id = we.EntityId AND dr.CompanyId = we.CompanyId
             JOIN WorkflowStepDefinitions wsd ON wsd.Id = wes.StepDefinitionId
             WHERE wes.CompanyId = @CompanyId 
               AND we.EntityType = 'Request'
               AND (
                 wes.AssignedUserId = @empCode
                 OR 
                 wes.AssignedRoleId IN (
                     SELECT ejp.roleid
                     FROM public.tblempjobprofile ejp
                     INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                     WHERE e.CompanyId = @CompanyId 
                       AND TRIM(e.empcode) = @empCode 
                       AND ejp.Active = TRUE
                 )
                 OR 
                 wes.AssignedDesignationId IN (
                     SELECT ejp.dsgid
                     FROM public.tblempjobprofile ejp
                     INNER JOIN public.tblEmployee e ON e.empid = ejp.empid
                     WHERE e.CompanyId = @CompanyId 
                       AND TRIM(e.empcode) = @empCode 
                       AND ejp.Active = TRUE
                 )
               );";

            var myInboxCounts = await _common.QueryFirstOrDefaultAsync<dynamic>(myInboxQuery, new { CompanyId, empCode });

            // Safely extract counts, defaulting to 0 if null
            var myRequestsResult = (IDictionary<string, object>)myRequestsCounts ?? new Dictionary<string, object>
            {
                { "pending", 0L }, { "approved", 0L }, { "rejectedorreverted", 0L }
            };

            var myInboxResult = (IDictionary<string, object>)myInboxCounts ?? new Dictionary<string, object>
            {
                { "pending", 0L }, { "approved", 0L }, { "rejectedorreverted", 0L }
            };

            return new
            {
                MyRequests = new
                {
                    Pending = (long)myRequestsResult["pending"],
                    Approved = (long)myRequestsResult["approved"],
                    RejectedOrReverted = (long)myRequestsResult["rejectedorreverted"]
                },
                MyInbox = new
                {
                    Pending = (long)myInboxResult["pending"],
                    Approved = (long)myInboxResult["approved"],
                    RejectedOrReverted = (long)myInboxResult["rejectedorreverted"]
                }
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<IQueryable<SelectList2Dto>> GetAllSelectList()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
            SELECT dr.Id, dr.RequestNumber
            FROM DocumentRequests dr
            JOIN WorkflowExecutions we
              ON we.EntityId = dr.Id
              AND we.CompanyId = dr.CompanyId
            WHERE dr.IsActive = TRUE AND dr.CompanyId = {CompanyId}
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT * FROM Vw_DocumentRequests d
                WHERE d.Id = {id} AND d.CompanyId = {CompanyId}
                  AND d.IsActive = True
                  AND d.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentRequests not found", 404);

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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT * FROM Vw_DocumentRequests d
                WHERE d.DivisionCode = {dCode}
                  AND d.CompanyId = {CompanyId}
                  AND d.IsActive = True
                  AND d.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentRequests not found", 404);

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
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id < 0)
                throw new CustomException("Invalid division code.", 404);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentRequests
            WHERE Id = '{input.Id}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentRequests not found", 404);

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
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = '{input.Id}' AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                SELECT * FROM Vw_DocumentRequests d
            WHERE d.Id = '{input.Id}' AND d.CompanyId = {CompanyId}";

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



    public async Task<dynamic> GetDraftDocumentCountAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Query 2: Counts for documents in the current user's INBOX (for approval)
            var myInboxQuery = @"
            SELECT COUNT(1) FROM Vw_DocumentRequests dr WHERE dr.CompanyId = @CompanyId
                AND dr.Status = 0 -- Draft
                AND dr.CompanyId = @CompanyId
                AND dr.CreatedBy = @empCode;";

            var draftCount = await _common.QueryFirstOrDefaultAsync<dynamic>(myInboxQuery, new { CompanyId, empCode });

            return draftCount;
        }
        catch (Exception)
        {
            throw;
        }
    }
     

    public async Task<IQueryable<SelectListDto>> GetRequestCreatedByUserListAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"SELECT DISTINCT
                        e.empCode AS Employeecode,
                        LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName 
                    FROM DocumentRequests dr
 
                    -- Assigned Employee
                    LEFT JOIN tblEmployee e
                        ON e.empCode = dr.createdBy
                WHERE dr.CompanyId = {CompanyId};";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Employeecode"),
                    Value = row.Field<string>("EmployeeName")
                }).ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<string> GenerateDocumentNumberAsync(int companyId, string divisionCode, string departmentCode, string subDepartmentCode, string documentTypeCode, int? parentDocumentId, string businessDomainCode = null, NpgsqlTransaction transaction = null)
    {
        // 1. Lookup Document Type Name to use as TYP
        var docTypeName = await _common.ExecuteScalarAsync<string>(@"
            SELECT Name FROM documenttypes 
            WHERE Code = @DocumentTypeCode AND CompanyId = @CompanyId AND IsActive = TRUE AND IsDeleted = FALSE LIMIT 1",
            new { DocumentTypeCode = documentTypeCode, CompanyId = companyId }, transaction);

        if (string.IsNullOrWhiteSpace(docTypeName))
            docTypeName = "DOC"; // fallback

        // 2. Lookup Names for Cabinet Hierarchy
        var cabinetNames = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT 
                (SELECT Name FROM divisions WHERE Code = @DivisionCode AND CompanyId = @CompanyId AND IsActive = TRUE AND IsDeleted = FALSE LIMIT 1) AS DivisionName,
                (SELECT Name FROM departments WHERE Code = @DepartmentCode AND CompanyId = @CompanyId AND IsActive = TRUE AND IsDeleted = FALSE LIMIT 1) AS DepartmentName,
                (SELECT Name FROM subdepartments WHERE Code = @SubDepartmentCode AND CompanyId = @CompanyId AND IsActive = TRUE AND IsDeleted = FALSE LIMIT 1) AS SubDepartmentName,
                (SELECT Name FROM businessdomains WHERE Code = @BusinessDomainCode AND CompanyId = @CompanyId AND IsActive = TRUE AND IsDeleted = FALSE LIMIT 1) AS BusinessDomainName",
            new
            {
                DivisionCode = divisionCode,
                DepartmentCode = departmentCode,
                SubDepartmentCode = subDepartmentCode,
                BusinessDomainCode = businessDomainCode,
                CompanyId = companyId
            }, transaction);

        string GetAbbreviation(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            
            name = name.Trim().ToUpper();
            
            // Remove noise words
            var noiseWords = new[] { "DIVISION", "DEPARTMENT", "SUB-DEPARTMENT", "SUBDEPARTMENT", "SECTION", "DOMAIN" };
            foreach (var nw in noiseWords)
            {
                name = name.Replace(nw, "").Trim();
            }

            if (string.IsNullOrWhiteSpace(name)) return null;

            // Common dictionary mappings
            var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "MARKETING", "MKT" },
                { "QUALITY ASSURANCE", "QA" },
                { "SOFTWARE DEVELOPMENT", "SD" },
                { "INFORMATION TECHNOLOGY", "IT" },
                { "HUMAN RESOURCES", "HR" },
                { "PRODUCTION", "PROD" },
                { "FINANCE", "FIN" },
                { "TEST", "TST" }
            };

            if (mappings.TryGetValue(name, out string mapped))
                return mapped;

            // If it's already a short abbreviation
            if (name.Length <= 4 && name.All(char.IsLetter))
                return name;

            var words = name.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                var w = words[0];
                return w.Length > 3 ? w.Substring(0, 3) : w;
            }

            var abbr = "";
            foreach (var word in words)
            {
                if (char.IsLetterOrDigit(word[0]))
                {
                    abbr += word[0];
                }
            }
            return abbr;
        }

        string div = null;
        string dpt = null;
        string sct = null;
        string bsd = null;

        if (cabinetNames != null)
        {
            div = GetAbbreviation((string)cabinetNames.divisionname);
            dpt = GetAbbreviation((string)cabinetNames.departmentname);
            sct = GetAbbreviation((string)cabinetNames.subdepartmentname);
            bsd = GetAbbreviation((string)cabinetNames.businessdomainname);
        }

        string typ = docTypeName.ToUpper();
 
         // 3. If it is an annexure (parentDocumentId is provided and > 0)
         if (parentDocumentId.HasValue && parentDocumentId.Value > 0)
         {
             var parentDoc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                 SELECT DocumentNumber FROM documents 
                 WHERE Id = @ParentId AND CompanyId = @CompanyId AND IsDeleted = FALSE",
                 new { ParentId = parentDocumentId.Value, CompanyId = companyId }, transaction);
 
             if (parentDoc == null || string.IsNullOrWhiteSpace((string)parentDoc.documentnumber))
                 throw new Exception("Parent document not found for annexure generation.");
 
             string parentNum = (string)parentDoc.documentnumber;
 
             // Query existing annexures for this parent to determine the next letter suffix
             var existingAnnexures = await _common.QueryAsync<string>(@"
                 SELECT DocumentNumber FROM documents 
                 WHERE ParentDocumentId = @ParentId AND CompanyId = @CompanyId AND IsDeleted = FALSE",
                 new { ParentId = parentDocumentId.Value, CompanyId = companyId }, transaction);
 
             var usedLetters = new HashSet<char>();
             foreach (var annNum in existingAnnexures)
             {
                 if (string.IsNullOrWhiteSpace(annNum)) continue;
                 var parts = annNum.Split('-');
                 var lastPart = parts[parts.Length - 1];
                 if (lastPart.Length == 1 && char.IsLetter(lastPart[0]))
                 {
                     usedLetters.Add(char.ToUpper(lastPart[0]));
                 }
             }
 
             char nextLetter = 'A';
             for (char c = 'A'; c <= 'Z'; c++)
             {
                 if (!usedLetters.Contains(c))
                 {
                     nextLetter = c;
                     break;
                 }
             }
 
             return $"{parentNum}-{nextLetter}";
         }
 
         // 4. Otherwise, normal document numbering: Build prefix using only linked cabinet segments
         var segments = new List<string>();
         if (!string.IsNullOrWhiteSpace(div)) segments.Add(div);
         if (!string.IsNullOrWhiteSpace(dpt)) segments.Add(dpt);
         if (!string.IsNullOrWhiteSpace(sct)) segments.Add(sct);
         if (!string.IsNullOrWhiteSpace(bsd)) segments.Add(bsd);
         segments.Add(typ);
 
         string prefix = string.Join("-", segments) + "-";

        var existingDocs = await _common.QueryAsync<string>(@"
            SELECT DocumentNumber FROM documents 
            WHERE CompanyId = @CompanyId 
              AND (@DivisionCode IS NULL AND DivisionCode IS NULL OR DivisionCode = @DivisionCode)
              AND (@DepartmentCode IS NULL AND DepartmentCode IS NULL OR DepartmentCode = @DepartmentCode)
              AND (@SubDepartmentCode IS NULL AND SubDepartmentCode IS NULL OR SubDepartmentCode = @SubDepartmentCode)
              AND (@BusinessDomainCode IS NULL AND BusinessDomainCode IS NULL OR BusinessDomainCode = @BusinessDomainCode)
              AND DocumentTypeCode = @DocumentTypeCode 
              AND DocumentNumber LIKE @Prefix || '%'
              AND IsDeleted = FALSE",
            new
            {
                CompanyId = companyId,
                DivisionCode = divisionCode,
                DepartmentCode = departmentCode,
                SubDepartmentCode = subDepartmentCode,
                BusinessDomainCode = businessDomainCode,
                DocumentTypeCode = documentTypeCode,
                Prefix = prefix
            }, transaction);

        int maxSeq = 0;
        foreach (var docNum in existingDocs)
        {
            if (string.IsNullOrWhiteSpace(docNum)) continue;
            if (docNum.Length <= prefix.Length) continue;
            var suffix = docNum.Substring(prefix.Length);
            if (suffix.Length >= 3)
            {
                var seqStr = suffix.Substring(0, 3);
                if (int.TryParse(seqStr, out int seqVal))
                {
                    if (seqVal > maxSeq)
                    {
                        maxSeq = seqVal;
                    }
                }
            }
        }

        int nextSeq = maxSeq + 1;
        string seqPart = nextSeq.ToString("D3");
        return $"{prefix}{seqPart}";
    }
}
