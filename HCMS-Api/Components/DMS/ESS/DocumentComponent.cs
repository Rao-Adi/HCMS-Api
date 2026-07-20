using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using OfficeOpenXml;
using System.ComponentModel.Design;
using System.Data;
using System.Reflection.Metadata;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    private readonly NotificationComponent _notificationComponent;
    private readonly PeoplePartnersComponent _peoplePartnersComponent;
    private readonly WorkflowStepComponent _workflowStepComponent;
    public DocumentComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        //, ILogger<UtilitiesController> logger
        , IHttpContextAccessor http,
        DMSCommon common,
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
        _notificationComponent = notificationComponent;
        _peoplePartnersComponent = peoplePartnersComponent;
        _workflowStepComponent = workflowStepComponent;
    }
    static DocumentComponent()
    {
        // Required for EPPlus in non-Windows environments or when running in certain contexts.
        ExcelPackage.License.SetNonCommercialPersonal("DMS");
    }


    public async Task<DocumentReadDto> CreateAsync(DocumentCreateDto input)
    {

        // UC-32: Next Review Date is a mandatory field
        if (input.NextReviewDate == default || input.NextReviewDate.Year < 2000)
            throw new CustomException("A valid Next Review Date is mandatory.", 400);

        // UC-32: Document Number should be provided for Legacy Documents
        if (string.IsNullOrWhiteSpace(input.DocumentNumber))
            throw new CustomException("Document Number is required for legacy document upload.", 400);

        if (input.DocumentFile == null || input.DocumentFile.Length == 0)
            throw new CustomException("Document file is required.", 400);

        // 1️⃣ Extract userId
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        var clientIp = _clientContextService.GetClientIP();
        int CompanyId = int.Parse(_CompanyId);
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

        // 2️⃣ Prepare upload path
        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        // 3️⃣ Create unique filename
        var fileExtension = Path.GetExtension(input.DocumentFile.FileName);
        var fileName = $"{input.DocumentFile.FileName}{fileExtension}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        // 4️⃣ Save file to disk
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await input.DocumentFile.CopyToAsync(stream);
        }

        // 5️⃣ Generate URL
        var documentUrl = $"/uploads/documents/{fileName}";

        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            // Check duplicate by DocumentNumber OR Title
            int exists = await _common.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(1)
                FROM Documents
                WHERE Title = @Title OR DocumentNumber = @DocumentNumber
                  AND IsDeleted = FALSE;",
                    new { Title = input.DocumentName, DocumentNumber = input.DocumentNumber }, tx);

            if (exists > 0)
                throw new CustomException("Document Title or Document Number already exists.", 409);
             

            var newId = await _common.ExecuteScalarAsync<int>(@"
                INSERT INTO Documents
                ( 
                    CompanyId, DocumentNumber, DocumentTypeCode, Title, NextReviewDate, DivisionCode, DepartmentCode,
                    SubDepartmentCode, BusinessDomainCode, DocumentURL, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy
                )
                VALUES
                (
                    @CompanyId, @DocumentNumber, @DocumentTypeCode, @Title, @NextReviewDate, @DivisionCode, @DepartmentCode, 
                    @SubDepartmentCode, @BusinessDomainCode, @DocumentUrl, TRUE, FALSE, NOW(), @UserId, NOW(), @UserId
                )
                RETURNING Id
                ", new
                {
                    CompanyId,
                    DocumentNumber= input.DocumentNumber,
                    DocumentTypeCode = input.DocumentTypeCode,
                    Title = input.DocumentName,
                    NextReviewDate = input.NextReviewDate,
                    DivisionCode = input.DivisionCode,
                    DepartmentCode = input.DepartmentCode,
                    SubDepartmentCode = input.SubDepartmentCode,
                    BusinessDomainCode = input.BusinessDomainCode,
                    DocumentUrl = documentUrl,
                    UserId = empCode
            }, tx);

            // UC-32: Active Archival - Insert initial effective version
            await _common.ExecuteAsync(@"
            INSERT INTO DocumentVersions
            (
                CompanyId, DocumentId, Version, VersionType, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy
            )
            VALUES
            (
                @CompanyId, @DocumentId, @Version, 1, TRUE, FALSE, NOW(), @UserId, NOW(), @UserId
            )
            ", new
            {
                CompanyId,
                DocumentId = newId,
                Version = string.IsNullOrWhiteSpace(input.Version) ? "1.0" : input.Version,
                UserId = empCode
            }, tx);

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
            ", new { CompanyId, DocumentId = newId, UserId = empCode }, tx);
             
            await tx.CommitAsync();

            // Fetch inserted record
            string selectQuery = $@"
            SELECT doc.* from vw_documents doc
            WHERE doc.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created document");

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                Title = row.Field<string>("Title"),
                Version = row.Table.Columns.Contains("Version") && !row.IsNull("Version") ? row.Field<string>("Version") : string.Empty,

                NextReviewDate = row.Table.Columns.Contains("NextReviewdate") && !row.IsNull("NextReviewdate")
                    ? row.Field<DateTime>("NextReviewdate").ToString("yyyy-MM-dd HH:mm:ss")
                    : (row.Table.Columns.Contains("NextReviewDate") && !row.IsNull("NextReviewDate") ? row.Field<DateTime>("NextReviewDate").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty),
                DocumentURL = row.Field<string>("DocumentURL"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }


    public async Task<PaginationResult<DocumentReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE doc.IsDeleted = False 
                  AND doc.RequestId IS NULL
                  AND doc.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(doc.Name) LIKE '%{search}%'
                    OR UPPER(doc.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DocumentNumber" => "doc.DocumentNumber",
                "DocumentTypeCode" => "doc.DocumentTypeCode",
                "DepartmentCode" => "doc.DepartmentCode",
                "DivisionCode" => "doc.DivisionCode",
                "SubDepartmentCode" => "doc.SubDepartmentCode",
                "BusinessDomainCode" => "doc.BusinessDomainCode",
                "Title" => "doc.Title",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                "ISACTIVE" => "doc.IsActive",
                _ => "doc.DocumentNumber"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT doc.*
                        FROM VW_Documents doc 
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Documents doc
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentReadDto>
                {
                    Items = new List<DocumentReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    DocumentNumber = row.Table.Columns.Contains("DocumentNumber") ? row.Field<string>("DocumentNumber") : string.Empty,

                    DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

                    Division = row.Table.Columns.Contains("Division") ? row.Field<string>("Division") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,

                    Department = row.Table.Columns.Contains("Department") ? row.Field<string>("Department") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,

                    SubDepartment = row.Table.Columns.Contains("SubDepartment") ? row.Field<string>("SubDepartment") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,

                    BusinessDomain = row.Table.Columns.Contains("BusinessDomain") ? row.Field<string>("BusinessDomain") : string.Empty,
                    BusinessDomainCode = row.Table.Columns.Contains("BusinessDomainCode") ? row.Field<string>("BusinessDomainCode") : string.Empty,

                    Title = row.Table.Columns.Contains("Title") ? row.Field<string>("Title") : string.Empty,
                    Version = row.Table.Columns.Contains("Version") ? row.Field<string>("Version") : string.Empty,


                    NextReviewDate = (row.Table.Columns.Contains("NextReviewDate") && !row.IsNull("NextReviewDate"))
                                ? row.Field<DateOnly>("NextReviewDate").ToString("yyyy-MM-dd") : string.Empty,

                    DocumentURL = row.Table.Columns.Contains("DocumentURL") ? row.Field<string>("DocumentURL") : string.Empty,
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

            return new PaginationResult<DocumentReadDto>
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


    public async Task<IQueryable<SelectListDto>> GetAllSelectList()
    {
        try
        {
            string query = @"
            SELECT Id, Name
            FROM Documents
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY Name";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Id"),
                    Value = row.Field<string>("Name")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentReadDto> GetByCodeAsync(string documentNumber)
    {
        try
        {
            string query = $@"SELECT doc.* from vw_documents doc
                WHERE doc.DocumentNumber = {documentNumber}
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                Title = row.Field<string>("Title"),

                NextReviewDate = row.Field<string>("NextReviewDate"),
                DocumentURL = row.Field<string>("DocumentURL"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<DocumentReadDto> GetByIdAsync(int id)
    {
        try
        {
            string query = $@"SELECT doc.* from vw_documents doc
                WHERE doc.Id = {id}
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                Title = row.Field<string>("Title"),

                NextReviewDate = row.Field<DateOnly?>("NextReviewDate")?.ToString("yyyy-MM-dd") ?? string.Empty,
                DocumentURL = row.Field<string>("DocumentURL"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"SELECT doc.* from vw_documents doc
                WHERE doc.DivisionCode = '{dCode}'
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 404);

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                Title = row.Field<string>("Title"),
                Version = row.Field<string>("Version"),

                NextReviewDate = row.Field<string>("NextReviewDate"),
                DocumentURL = row.Field<string>("DocumentURL"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<bool> SubmitDocumentAsync(SubmitDocument input)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            //-------------------------------------------------
            // 1️⃣ Lock Document
            //-------------------------------------------------

            var doc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM Documents
                WHERE Id = @DocumentId
                AND CompanyId = @CompanyId
                FOR UPDATE;",
            new { input.DocumentId, CompanyId }, transaction);

            if (doc == null)
                throw new CustomException("Document not found.", 404);


            //-------------------------------------------------
            // Validate & Save Attributes (NEW METHOD)
            //-------------------------------------------------

            await ValidateAndSaveAttributesAsync(input, doc, transaction);

            //-------------------------------------------------
            // Validate & Save Training Users
            //-------------------------------------------------
            await ValidateAndSaveTrainingUsersAsync(input, doc, CompanyId, empCode, transaction);

            //-------------------------------------------------
            // 2️⃣ Resolve Correct Workflow Policy
            //-------------------------------------------------

            string Normalize(string? v) => string.IsNullOrWhiteSpace(v) || v == "0" || v.ToLower() == "null" ? "" : v.Trim();

            var policyId = await _common.ExecuteScalarAsync<int?>(@"
                SELECT Id
                FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId
                AND EntityType = 'Document'
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
                DocType = doc.documenttypecode,
                DivisionCode = Normalize(Convert.ToString(doc.divisioncode)),
                DepartmentCode = Normalize(Convert.ToString(doc.departmentcode)),
                SubDepartmentCode = Normalize(Convert.ToString(doc.subdepartmentcode)),
                BusinessDomainCode = Normalize(Convert.ToString(doc.businessdomaincode))
            }, transaction);

            if (policyId == null)
                throw new CustomException("No workflow policy defined for selected Cabinet Scope.", 404);

            //-------------------------------------------------
            // 3️⃣ Resolve Active Policy Version
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
            }, transaction);

            if (versionId == null)
                throw new CustomException("Workflow policy not defined for Document.", 404);

            //-------------------------------------------------
            // 4️⃣ Promote Version (Rework Case)
            //-------------------------------------------------

            await PromoteVersionAfterReworkAsync(CompanyId, input.DocumentId, empCode, transaction);

            //-------------------------------------------------
            // 5️⃣ Create Workflow Execution
            //-------------------------------------------------

            var executionId = await _common.ExecuteScalarAsync<long>(@"
                INSERT INTO WorkflowExecutions
                (
                    CompanyId, WorkflowPolicyVersionId, EntityType, EntityId, Status, StartedBy
                )
                VALUES
                (
                    @CompanyId, @VersionId, 'Document', @DocumentId, 'Running', @empCode
                )
                RETURNING Id;",
            new
            {
                CompanyId,
                VersionId = versionId,
                input.DocumentId,
                empCode
            }, transaction);

            //-------------------------------------------------
            // 6️⃣ Snapshot Workflow Steps
            //-------------------------------------------------

            var stepDefs = await _common.QueryAsync<dynamic>(@"
                SELECT Id, UserId, RoleId, DesignationId, StepOrder
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId
                ORDER BY StepOrder;", new { VersionId = versionId }, transaction);

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
                        new { EmpFrom = actualUserId, CompanyId }, transaction);

                    if (!string.IsNullOrEmpty(transferTo)) actualUserId = transferTo;

                    await _common.ExecuteAsync(@"
                        INSERT INTO WorkflowExecutionSteps (CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId, AssignedDesignationId, StepOrder, Observation, IsActive)
                        VALUES (@CompanyId, @ExecutionId, @StepDefId, @UserId, NULL, NULL, @StepOrder, '', FALSE);",
                        new { CompanyId, ExecutionId = executionId, StepDefId = stepDef.id, UserId = actualUserId, StepOrder = runningStepOrder }, transaction);
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
                          AND COALESCE(e.Active, 1) = 1 
                          AND COALESCE(ejp.Active, TRUE) = TRUE
                          AND ((@RoleId::int IS NOT NULL AND ejp.roleid = @RoleId::int) OR (@DesignationId::int IS NOT NULL AND ejp.dsgid = @DesignationId::int))
                        ORDER BY e.empid ASC;",
                        new { CompanyId, RoleId = (int?)stepDef.roleid, DesignationId = (int?)stepDef.designationid }, transaction);

                    if (!employees.Any())
                        throw new CustomException("Workflow misconfigured — no active employees found for a configured Role/Designation step.", 404);

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
                        inserted++;
                    }
                }
            }

            if (inserted < 1)
                throw new CustomException("Workflow misconfigured — no steps copied.", 404);

            //-------------------------------------------------
            // 7️⃣ Activate First Step
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
            new { ExecutionId = executionId }, transaction);

            //-------------------------------------------------
            // 8️⃣ Insert State Change
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO DocumentStateHistory
                (
                    CompanyId, DocumentId, FromStateId, ToStateId, WorkflowExecutionId, ChangedBy
                )
                VALUES
                (
                    @CompanyId, @DocumentId, 1, 2, @ExecutionId, @empCode
                );",
            new
            {
                CompanyId,
                input.DocumentId,
                ExecutionId = executionId,
                empCode
            }, transaction);

            // Prepare notification data
            var firstStepInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.StepOrder, d.Title, dv.Version
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN Documents d ON d.Id = we.EntityId
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.IsActive = TRUE
                WHERE wes.WorkflowExecutionId = @ExecutionId AND wes.IsActive = TRUE
                ORDER BY dv.CreatedAt DESC LIMIT 1;", new { ExecutionId = executionId }, transaction);

            List<string> approvers = new List<string>();
            string docTitle = "Unknown";
            string docVersion = "1.0";

            if (firstStepInfo != null)
            {
                docTitle = Convert.ToString(firstStepInfo.title) ?? "Unknown";
                docVersion = Convert.ToString(firstStepInfo.version) ?? "1.0";
                approvers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, executionId, (int)firstStepInfo.steporder, transaction);
            }

            if (approvers.Any())
            {
                var placeholders = new Dictionary<string, string> { { "Doc Name", docTitle }, { "V#", docVersion } };
                foreach (var approver in approvers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingDocumentApproval, CompanyId, input.DocumentId, approver, placeholders, transaction);
                }
            }

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ValidateAndSaveAttributesAsync(SubmitDocument input, dynamic documentInfo, IDbTransaction transaction)
    {
        //-------------------------------------------------
        // 1️⃣ Load Active Attributes
        //-------------------------------------------------
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        var clientIp = _clientContextService.GetClientIP();
        var prefix = _utilities.GetPrefix(clientIp);
        //var userId = _utilities.GetUserid(prefix);
        int CompanyId = int.Parse(_CompanyId);
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

        var attributes = await _common.QueryAsync<dynamic>(@"
                SELECT *
                FROM DocumentAttributes
                WHERE CompanyId = @CompanyId
                AND DocumentTypeCode = @DocumentTypeCode
                AND IsActive = TRUE
                AND IsDeleted = FALSE;",
            new
            {
                CompanyId,
                DocumentTypeCode = documentInfo.documenttypecode
            }, transaction);

        foreach (var attr in attributes)
        {
            var submitted = input.Attributes
                .FirstOrDefault(x => x.DocumentAttributeId == attr.id);

            bool isMandatory = attr.ismandatory;

            //-------------------------------------------------
            // 2️⃣ Check Scoped Mandatory
            //-------------------------------------------------

            var scopedMandatory = await _common.ExecuteScalarAsync<bool?>(@"
                SELECT TRUE
                FROM AttributeMandatoryScopes
                WHERE CompanyId = @CompanyId
                AND DocumentAttributeId = @AttributeId
                AND DivisionCode = @DivisionCode
                AND DepartmentCode = @DepartmentCode
                AND SubDepartmentCode = @SubDepartmentCode
                AND BusinessDomainCode = @BusinessDomainCode
                AND IsMandatory = TRUE
                AND IsActive = TRUE
                AND IsDeleted = FALSE
                LIMIT 1;",
            new
            {
                CompanyId,
                AttributeId = attr.id,
                DivisionCode = documentInfo.divisioncode,
                DepartmentCode = documentInfo.departmentcode,
                SubDepartmentCode = documentInfo.subdepartmentcode,
                BusinessDomainCode = documentInfo.businessdomaincode
            }, transaction);

            if (scopedMandatory == true)
                isMandatory = true;

            //-------------------------------------------------
            // 3️⃣ Mandatory Validation
            //-------------------------------------------------

            if (isMandatory)
            {
                if (submitted == null ||
                    (submitted.ValueText == null &&
                     submitted.ValueNumber == null &&
                     submitted.ValueDate == null &&
                     submitted.ValueBoolean == null))
                {
                    throw new Exception(
                        $"Attribute '{attr.controllabel}' is mandatory.");
                }
            }

            //-------------------------------------------------
            // 4️⃣ Insert / Update Value
            //-------------------------------------------------

            if (submitted != null)
            {
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentAttributeValues
                    (CompanyId, DocumentId, DocumentAttributeId,
                     ValueText, ValueNumber, ValueDate, ValueBoolean,
                     CreatedBy)
                    VALUES
                    (@CompanyId, @DocumentId, @AttributeId,
                     @ValueText, @ValueNumber, @ValueDate, @ValueBoolean,
                     @empCode)
                    ON CONFLICT (CompanyId, DocumentId, DocumentAttributeId)
                    DO UPDATE SET
                        ValueText = EXCLUDED.ValueText,
                        ValueNumber = EXCLUDED.ValueNumber,
                        ValueDate = EXCLUDED.ValueDate,
                        ValueBoolean = EXCLUDED.ValueBoolean;",
                new
                {
                    CompanyId,
                    input.DocumentId,
                    AttributeId = submitted.DocumentAttributeId,
                    submitted.ValueText,
                    submitted.ValueNumber,
                    submitted.ValueDate,
                    submitted.ValueBoolean,
                    empCode
                }, transaction);
            }
        }
    }

    private async Task ValidateAndSaveTrainingUsersAsync(dynamic input, dynamic documentInfo, int companyId, string empCode, IDbTransaction transaction)
    {
        // 1. Check if Training is required for this DocumentType
        var tp = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT TrainingRequired 
            FROM TrainingPolicies 
            WHERE CompanyId = @CompanyId 
              AND DocumentTypeCode = @DocTypeCode 
              AND IsActive = TRUE;",
            new { CompanyId = companyId, DocTypeCode = (string)documentInfo.documenttypecode }, transaction);

        bool requiresTraining = tp != null && tp.trainingrequired == true;

        if (requiresTraining)
        {
            // UC Requirement: Users must be attached if Training = True
            // Note: Ensure your 'SubmitDocument' DTO contains: public List<string>? TrainingUserIds { get; set; }
            if (input.TrainingUserIds == null || input.TrainingUserIds.Count == 0)
                throw new CustomException("Training is required for this document type. Please select users for training.", 400);

            // Clear any existing training users (useful in case of rework/resubmission)
            await _common.ExecuteAsync(@"
                DELETE FROM DocumentUserTraining 
                WHERE DocumentId = @DocumentId 
                  AND CompanyId = @CompanyId;",
                new { DocumentId = input.DocumentId, CompanyId = companyId }, transaction);

            // Insert new explicitly attached training users
            foreach (string uid in input.TrainingUserIds)
            {
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentUserTraining
                    (CompanyId, DocumentId, EmployeeCode, TrainingMode, TrainingStatus, TrainingProofURL, AssessmentScore, ValidationStatus, ReadyForAuthorization, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                    VALUES
                    (@CompanyId, @DocumentId, @EmployeeCode, 1, 0, '', 0, 0, TRUE, TRUE, FALSE, NOW(), @UserId, NOW(), @UserId);",
                new { CompanyId = companyId, DocumentId = input.DocumentId, EmployeeCode = uid, UserId = empCode }, transaction);
            }
        }
    }

    public async Task PromoteVersionAfterReworkAsync(int companyId, int documentId, string empCode, IDbTransaction transaction)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);
            //-----------------------------------------
            // 1️⃣ Check Last State Was Rework Draft
            //----------------------------------------- 
            var wasReworked = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT COUNT(*)
                        FROM DocumentStateHistory
                        WHERE CompanyId = @CompanyId
                          AND DocumentId = @DocumentId
                          AND ToStateId = 1
                          AND WorkflowExecutionId IS NOT NULL;",
            new { companyId, documentId }, transaction);


            if (wasReworked.Count() == 0)
                return;

            //-----------------------------------------
            // 2️⃣ Get Latest Version
            //-----------------------------------------
            var current = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM DocumentVersions
                WHERE CompanyId = @CompanyId
                  AND DocumentId = @DocumentId 
                  AND IsActive = TRUE
                ORDER BY CreatedAt DESC
                LIMIT 1",
            new { companyId, documentId }, transaction);

            if (current == null)
                throw new Exception("No valid document content found to promote.");

            //-----------------------------------------
            // 3️⃣ Promote 0.1 → 1.0
            //-----------------------------------------
            var newVersion = "1.0";

            //-----------------------------------------
            // 4️⃣ Insert New Version Row
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
                Content = current.Content,
                CreatedBy = empCode,
                LastModifiedBy = empCode
            }, transaction);

        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<bool> ApproveDocumentAsync(ActionOnDocument input)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            //var userId = await GetEmployeeID(input.EmployeeCode);
            //-------------------------------------------------
            // 1️⃣ Get Current Active Step
            //-------------------------------------------------

            var currentStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM WorkflowExecutionSteps
                WHERE CompanyId = @CompanyId
                AND WorkflowExecutionId = @ExecutionId
                AND IsActive = TRUE;",
            new { CompanyId, input.ExecutionId }, transaction);

            if (currentStep == null)
                throw new Exception("No active approval step found.");

            // Fetch document info for notifications
            var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.Title, dv.Version
                FROM Documents d
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = d.Id
                WHERE d.Id = @DocumentId
                ORDER BY dv.CreatedAt DESC LIMIT 1;", new { DocumentId = input.DocumentId }, transaction);
            var notifyPlaceholders = new Dictionary<string, string> { { "Doc Name", Convert.ToString(docInfo?.title) ?? "Unknown" }, { "V#", Convert.ToString(docInfo?.version) ?? "1.0" } };

            //-------------------------------------------------
            // 2️⃣ Approve Current Step
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutionSteps
                SET Decision = 'Approved',
                    Observation = @Observation,
                    ActionAt = NOW(),
                    IsActive = FALSE
                WHERE Id = @StepId;",
            new { Observation = input.Observation, StepId = currentStep.id }, transaction);

            //-------------------------------------------------
            // 3️⃣ Find Next Step
            //-------------------------------------------------

            List<string> nextStepApprovers = new List<string>();
            var nextStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM WorkflowExecutionSteps
                WHERE CompanyId = @CompanyId
                AND WorkflowExecutionId = @ExecutionId
                AND StepOrder > @CurrentOrder
                ORDER BY StepOrder
                LIMIT 1;",
            new
            {
                CompanyId,
                input.ExecutionId,
                CurrentOrder = currentStep.steporder
            }, transaction);

            //-------------------------------------------------
            // 4️⃣ Activate Next Step OR Complete Workflow
            //-------------------------------------------------

            if (nextStep != null)
            {
                // Activate next approver
                await _common.ExecuteAsync(@"
                    UPDATE WorkflowExecutionSteps
                    SET IsActive = TRUE, Observation = @Observation
                    WHERE Id = @NextStepId;",
                new { NextStepId = nextStep.id, input.Observation }, transaction);

                nextStepApprovers = await _workflowStepComponent.GetNextStepApproversAsync(CompanyId, input.ExecutionId, (int)nextStep.steporder, transaction);
            }
            else
            {
                // No more steps → Complete Workflow
                await _common.ExecuteAsync(@"
                    UPDATE WorkflowExecutions
                    SET Status = 'Completed',
                        CompletedAt = NOW()
                    WHERE Id = @ExecutionId
                    AND CompanyId = @CompanyId;",
                new { input.ExecutionId, CompanyId }, transaction);

                //-------------------------------------------------
                // Move Document → Approved
                //-------------------------------------------------

                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentStateHistory
                    (
                        CompanyId, DocumentId, FromStateId, ToStateId, WorkflowExecutionId, ChangedBy
                    )
                    VALUES
                    (
                        @CompanyId, @DocumentId, 2, 3, @ExecutionId, @empCode
                    );",
                new
                {
                    CompanyId,
                    input.DocumentId,
                    input.ExecutionId,
                    empCode
                }, transaction);
            }

            if (!nextStepApprovers.Any())
            {
                await HandlePostApprovalAsync(CompanyId, input.DocumentId, empCode, transaction);
            }

            if (nextStepApprovers.Any() && docInfo != null)
            {
                foreach (var approver in nextStepApprovers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentApprovedForwarded, CompanyId, input.DocumentId, approver, notifyPlaceholders, transaction);
                }
            }

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /* 7️⃣ MakeDocumentEffectiveAsync
    🚫 Usually NOT endpoint
    This depends on your compliance model.
    Two scenarios:

    Scenario A — Effective immediately after approval
    Call internally inside:
    ApproveDocumentAsync()
    /
    / 
    Scenario B — Requires Training / Authorization
    Then this becomes internal orchestration triggered after:
    Training completion
    Authorization workflow completion
    Still not a public endpoint.
    */


    /* 
     *  Draft Request
            ↓
        Modify Users / Distribution
            ↓
        Submit
            ↓
        Approve
            ↓
        Create Document
            ↓
        Promote Audience
            ↓
        Make Effective
            ↓
        AUTO CREATE TRAINING MATRIX ✅
     */
    public async Task<bool> MakeDocumentEffectiveAsync(int companyId, int documentId, string empCode, IDbTransaction transaction = null)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);

            //-----------------------------------------
            // Activate Final Version
            //-----------------------------------------

            // 1. Archive previous effective versions (Critical for UC-22 Revisions)
            await _common.ExecuteAsync(@"
                UPDATE DocumentVersions 
                SET VersionType = 3, 
                    IsActive = FALSE 
                WHERE DocumentId = @DocumentId 
                  AND VersionType = 2 
                  AND CompanyId = @CompanyId;", new { companyId, documentId }, transaction);

            // 2. Promote the current Draft version to Effective
            await _common.ExecuteAsync(@"
                UPDATE DocumentVersions
                SET VersionType = 2
                WHERE CompanyId = @CompanyId
                  AND DocumentId = @DocumentId
                  AND VersionType = 1
            ", new { companyId, documentId }, transaction);

            //-----------------------------------------
            // Approved → Effective
            //-----------------------------------------
            await _common.ExecuteAsync(@"
                INSERT INTO DocumentStateHistory
                (
                    CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy
                )
                VALUES
                (
                    @CompanyId, @DocumentId, 3, 4, @UserId
                )",
            new { CompanyId = companyId, DocumentId = documentId, UserId = empCode }, transaction);


            //if (isLocalTransaction)
            //{
            //    await ((System.Data.Common.DbTransaction)transaction).CommitAsync();
            //}


            var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.Title, dv.Version, d.CreatedBy
                FROM Documents d
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.VersionType = 2
                WHERE d.Id = @DocumentId
                ORDER BY dv.CreatedAt DESC LIMIT 1", new { DocumentId = documentId }, transaction);

            string initiatorId = "";
            if (docInfo != null && docInfo!.createdby != null)
            {
                //int.TryParse(Convert.ToString(docInfo.createdby), out initiatorId);
                initiatorId = docInfo!.createdby;
            }

            if (initiatorId != string.Empty)
            {
                var notifyPlaceholders = new Dictionary<string, string> { { "Doc Name", Convert.ToString(docInfo.title) ?? "Unknown" }, { "V#", Convert.ToString(docInfo.version) ?? "1.0" }, { "Date", DateTime.Now.ToString("yyyy-MM-dd") } };
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentAuthorizedEffective, companyId, documentId, initiatorId, notifyPlaceholders, transaction);
            }

            return true;
        }
        catch
        {
            //if (isLocalTransaction)
            {
                await ((System.Data.Common.DbTransaction)transaction).RollbackAsync();
            }
            throw;
        }
        //finally
        //{
        //    //if (isLocalTransaction && transaction != null)
        //    {
        //        transaction.Dispose();
        //    }
        //}
    }

    public async Task NotifyPendingUsersAsync(int companyId, int documentId, IDbTransaction tx = null)
    {

        var users = await _common.QueryAsync<dynamic>(@"
            SELECT 
                dut.EmployeeCode,
                u.Email,
                d.Title
            FROM DocumentUserTraining dut
            JOIN tblEmployee u ON u.empcode = dut.EmployeeCode
            JOIN Documents d ON d.Id = dut.DocumentId
            WHERE dut.CompanyId = @CompanyId
              AND dut.DocumentId = @DocumentId
              AND dut.TrainingStatus = 0
              AND dut.IsActive = TRUE
              AND dut.IsDeleted = FALSE;
            ", new { companyId, documentId }, tx);

        foreach (var user in users)
        {
            var placeholders = new Dictionary<string, string>
            {
                { "Doc Name", (string)user.title },
                { "V#", "Latest" }
            };

            // Utilize the central notification engine to broadcast SignalR, save to DB, and send Email
            await _notificationComponent.TriggerNotificationAsync(
                NotificationScenario.TrainingProofRequired,
                companyId,
                documentId,
                Convert.ToString(user.employeecode),
                placeholders, tx);
        }
    }

    //This method will be used later
    public async Task<bool> CompleteDocumentTrainingAsync(CompleteDocumentTrainingDto dto)
    {
        await using var tx = await _common.BeginTransactionAsync();

        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);


            //-----------------------------------------
            // 1️⃣ Update Training Status
            //-----------------------------------------
            var rows = await _common.ExecuteAsync(@"
                UPDATE DocumentUserTraining
                SET
                    TrainingStatus = 1, -- Completed
                    TrainingProofUrl = @ProofUrl,
                    AssessmentScore = @Score,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = @UserId
                WHERE CompanyId = @CompanyId
                  AND DocumentId = @DocumentId
                  AND EmployeeCode = @UserId
                  AND IsActive = TRUE
                  AND IsDeleted = FALSE;
                ",
            new
            {
                CompanyId,
                dto.DocumentId,
                dto.UserId,
                ProofUrl = dto.TrainingProofUrl,
                Score = dto.AssessmentScore
            }, tx);

            if (rows == 0)
                throw new Exception("Training record not found.");

            //-----------------------------------------
            // 2️⃣ Mark Notification as Read
            //-----------------------------------------
            await _common.ExecuteAsync(@"
            UPDATE Notifications
            SET IsRead = TRUE
            WHERE CompanyId = @CompanyId
              AND UserId = @UserId
              AND RelatedEntityType = 'Document'
              AND RelatedEntityId = @DocumentId
              AND NotificationType = 2;
            ",
                new
                {
                    CompanyId,
                    dto.UserId,
                    dto.DocumentId
                }, tx);

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task HandlePostApprovalAsync(int companyId, int documentId, string userId, IDbTransaction tx)
    {

        try
        {

            // 1. Fetch document type and training policy rules
            var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT doc.DocumentTypeCode, doc.CreatedBy, tp.TrainingRequired, tp.MinimumScore
                FROM Documents doc
                LEFT JOIN TrainingPolicies tp 
                    ON tp.DocumentTypeCode = doc.DocumentTypeCode 
                   AND tp.CompanyId = doc.CompanyId 
                   AND tp.IsActive = TRUE
                WHERE doc.Id = @DocumentId;", new { DocumentId = documentId }, tx);

            bool requiresTraining = docInfo != null && docInfo!.trainingrequired == true;

            if (!requiresTraining)
            {
                await MakeDocumentEffectiveAsync(companyId, documentId, userId, tx);
            }
            else
            {

                // 2. If Training is required, Transition to TRAINING_PENDING
                var stateId = await _common.QueryFirstOrDefaultAsync<int?>(@"SELECT Id FROM DocumentStates WHERE Code = 'TRAINING_PENDING'", tx);
                int trainingPendingStateId = stateId ?? 6;

                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, ChangedAt)
                    VALUES (@CompanyId, @DocumentId, 3, @ToStateId, @UserId, NOW());",
                    new { CompanyId = companyId, DocumentId = documentId, ToStateId = trainingPendingStateId, UserId = userId }, tx);

                // 3. Create the Parent Training Record
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentTraining
                    (CompanyId, DocumentId, TrainingMode,TrainingStatus,TrainingProofURL, AssessmentScore, ValidationStatus, ReadyForAuthorization, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                    VALUES
                    (@CompanyId, @DocumentId, 1, 0,'', 0, 0, TRUE, TRUE, FALSE, NOW(), @UserId, NOW(), @UserId);",
                    new { CompanyId = companyId, DocumentId = documentId, UserId = (string)docInfo.createdby }, tx);


                // 4. Notify Users about assigned training (Those already saved during Document Submit)
                await NotifyPendingUsersAsync(companyId, documentId, tx);
            }

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<bool> RejectDocumentAsync(ActionOnDocument input)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            var empDetail = await _peoplePartnersComponent.GetEmployeeByEmpIdAsync(input.EmpId);

            //-------------------------------------------------
            // 1️⃣ Get Current Active Step
            //-------------------------------------------------

            var currentStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM WorkflowExecutionSteps
                WHERE CompanyId = @CompanyId
                AND WorkflowExecutionId = @ExecutionId
                AND IsActive = TRUE;",
            new { CompanyId, input.ExecutionId }, transaction);

            if (currentStep == null)
                throw new Exception("No active approval step found.");

            // Fetch document info for notifications
            string approverName = empDetail?.firstname + " " + empDetail?.midname + " " + empDetail?.lastname;
            var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.Title, dv.Version, d.CreatedBy
                FROM Documents d
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = d.Id
                WHERE d.Id = @DocumentId
                ORDER BY dv.CreatedAt DESC LIMIT 1", new { DocumentId = input.DocumentId }, transaction);

            //-------------------------------------------------
            // 2️⃣ Mark Step Rejected
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutionSteps
                SET Decision = 'Rejected',
                    Observation = @Comments,
                    ActionAt = NOW(),
                    IsActive = FALSE
                WHERE Id = @StepId;",
            new
            {
                StepId = currentStep.id,
                Comments = input.Observation
            }, transaction);

            //-------------------------------------------------
            // 3️⃣ Complete WorkflowExecution
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutions
                SET Status = 'Cancelled',
                    CompletedAt = NOW()
                WHERE Id = @ExecutionId
                AND CompanyId = @CompanyId;",
            new { input.ExecutionId, CompanyId }, transaction);

            //-------------------------------------------------
            // 4️⃣ Move Document → Closed
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO DocumentStateHistory
                (
                    CompanyId, DocumentId, FromStateId, ToStateId, WorkflowExecutionId, Comments, ChangedBy
                )
                VALUES
                (
                    @CompanyId, @DocumentId, 2, 5, @ExecutionId, @Comments, @empCode
                );",
            new
            {
                CompanyId,
                input.DocumentId,
                input.ExecutionId,
                Comments = input.Observation,
                empCode
            }, transaction);

            await transaction.CommitAsync();

            string initiatorId = "";
            if (docInfo != null && docInfo!.createdby != null)
            {
                //int.TryParse(Convert.ToString(docInfo!.createdby), out initiatorId);
                initiatorId = docInfo!.createdby;
            }

            if (initiatorId != string.Empty)
            {
                var notifyPlaceholders = new Dictionary<string, string> {
                    { "Doc Name", Convert.ToString(docInfo.title) ?? "Unknown" }, { "V#", Convert.ToString(docInfo.version) ?? "1.0" },
                    { "Approver", approverName ?? empCode }, { "Observation", input.Observation ?? "" }
                };
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentRejected, CompanyId, input.DocumentId, initiatorId, notifyPlaceholders);
            }

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<bool> SendBackForReworkAsync(ActionOnDocument input)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());
            var empDetail = await _peoplePartnersComponent.GetEmployeeByEmpIdAsync(input.EmpId);

            //-------------------------------------------------
            // 1️⃣ Get Current Active Step
            //-------------------------------------------------

            var currentStep = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM WorkflowExecutionSteps
                WHERE CompanyId = @CompanyId
                AND WorkflowExecutionId = @ExecutionId
                AND IsActive = TRUE;",
            new { CompanyId, input.ExecutionId }, transaction);

            if (currentStep == null)
                throw new Exception("No active approval step found.");

            // Fetch document info for notifications
            string approverName = empDetail?.firstname + " " + empDetail?.midname + " " + empDetail?.lastname;

            var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.Title, dv.Version, d.CreatedBy
                FROM Documents d
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = d.Id
                WHERE d.Id = @DocumentId
                ORDER BY dv.CreatedAt DESC LIMIT 1", new { DocumentId = input.DocumentId }, transaction);

            //-------------------------------------------------
            // 2️⃣ Mark Step Rework
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutionSteps
                SET Decision = 'Reworked',
                    Observation = @Comments,
                    ActionAt = NOW(),
                    IsActive = FALSE
                WHERE Id = @StepId;",
            new
            {
                StepId = currentStep.id,
                Comments = input.Observation
            }, transaction);

            //-------------------------------------------------
            // 3️⃣ Complete WorkflowExecution
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                UPDATE WorkflowExecutions
                SET Status = 'Cancelled',
                    CompletedAt = NOW()
                WHERE Id = @ExecutionId
                AND CompanyId = @CompanyId;",
            new { input.ExecutionId, CompanyId }, transaction);

            //-------------------------------------------------
            // 4️⃣ Move Document → Draft
            //-------------------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO DocumentStateHistory
                (
                    CompanyId,
                    DocumentId,
                    FromStateId,
                    ToStateId,
                    WorkflowExecutionId,
                    Comments,
                    ChangedBy
                )
                VALUES
                (
                    @CompanyId,
                    @DocumentId,
                    2,
                    1,
                    @ExecutionId,
                    @Comments,
                    @empCode
                );",
            new
            {
                CompanyId,
                input.DocumentId,
                input.ExecutionId,
                Comments = input.Observation,
                empCode
            }, transaction);

            await transaction.CommitAsync();

            string initiatorId = "";
            if (docInfo != null && docInfo!.createdby != null)
            {
                //int.TryParse(Convert.ToString(docInfo.createdby), out initiatorId);
                initiatorId = docInfo!.createdby;
            }

            if (initiatorId != string.Empty)
            {
                var notifyPlaceholders = new Dictionary<string, string> {
                    { "Doc Name", Convert.ToString(docInfo.title) ?? "Unknown" }, { "V#", Convert.ToString(docInfo.version) ?? "1.0" },
                    { "Approver", approverName ?? empCode }, { "Observation", input.Observation ?? "" }
                };
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentRevertedForRework, CompanyId, input.DocumentId, initiatorId, notifyPlaceholders);
            }

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    public async Task<IEnumerable<dynamic>> GetRequestsPendingFinalizationAsync(GetApprovedRequestForDocumentCreationDto input)
    {
        try
        {

            // Basic validation (you can throw exceptions or handle differently)
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());

            if (string.IsNullOrWhiteSpace(input.DocumentTypeCode))
                throw new ArgumentException("DocumentTypeCode is required", nameof(input.DocumentTypeCode));


            // ────────────────────────────────────────────────
            // Convert empty strings → null (this is the key fix)
            // ────────────────────────────────────────────────
            string? Normalize(string? value) =>
                string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            var parameters = new
            {
                CompanyId = int.Parse(CompanyId),
                DocumentTypeCode = input.DocumentTypeCode.Trim(),
                DivisionCode = Normalize(input.DivisionCode),
                DepartmentCode = Normalize(input.DepartmentCode),
                SubDepartmentCode = Normalize(input.SubDepartmentCode),
                BusinessDomainCode = Normalize(input.BusinessDomainCode),
                // UserId = input.UserId   // add only if you're actually using it in WHERE
            };

            const string sql = $@"
                SELECT 
                    dr.Id,
                    dr.RequestNumber,
                    d.Id          AS DocumentId,
                    d.Title       AS DocumentTitle,
                    d.DocumentNumber

                FROM DocumentRequests dr
                INNER JOIN Documents d 
                    ON  d.CompanyId = dr.CompanyId 
                    AND d.Id        = dr.DocumentId

                WHERE dr.CompanyId       = @CompanyId
                  AND dr.Status          = 3                    -- Approved
                  AND dr.DocumentTypeCode = @DocumentTypeCode
                  AND dr.DocumentId      IS NOT NULL

                  -- Organizational filters (only applied when value is provided)
                  AND (@DivisionCode       IS NULL OR d.DivisionCode       = @DivisionCode)
                  AND (@DepartmentCode     IS NULL OR d.DepartmentCode     = @DepartmentCode)
                  AND (@SubDepartmentCode  IS NULL OR d.SubDepartmentCode  = @SubDepartmentCode)
                  AND (@BusinessDomainCode IS NULL OR d.BusinessDomainCode = @BusinessDomainCode)

                  -- Only documents that are currently in Draft state (latest state = 1)
                  AND (
                    SELECT dsh.ToStateId
                    FROM DocumentStateHistory dsh
                    WHERE dsh.CompanyId  = d.CompanyId
                      AND dsh.DocumentId = d.Id
                    ORDER BY dsh.ChangedAt DESC
                    LIMIT 1
                  ) = 1   -- Draft

                ORDER BY dr.RequestNumber DESC;   -- most recent requests first (common preference)
                ";

            var result = await _common.QueryAsync<dynamic>(sql, parameters);

            return result ?? Enumerable.Empty<dynamic>();  // never return null list

        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

    public async Task<IEnumerable<dynamic>> GetDraftDocumentByRequestAsync(int requestId)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);


            var result = await _common.QueryAsync<dynamic>(@"
                SELECT 
                d.Id AS DocumentId,
                d.Title,
                d.DivisionCode,
                d.DepartmentCode,
                d.SubDepartmentCode,
                d.BusinessDomainCode,
                d.NextReviewDate,
                dr.RequestNumber,
                dv.Version,
                dv.Content,
                dr.DraftFileURL
            FROM DocumentRequests dr
            INNER JOIN Documents d
                ON d.CompanyId = dr.CompanyId
                AND d.Id = dr.DocumentId
            INNER JOIN DocumentVersions dv
                ON dv.CompanyId = d.CompanyId
                AND dv.DocumentId = d.Id
                AND dv.VersionType = 1  -- Draft Version
            WHERE dr.CompanyId = @CompanyId
              AND dr.Id = @RequestId
              -- Fixed: Ensure the single latest state record is exactly 'Draft' (1)
              AND (
                  SELECT dsh.ToStateId
                  FROM DocumentStateHistory dsh
                  WHERE dsh.CompanyId = d.CompanyId
                    AND dsh.DocumentId = d.Id
                  ORDER BY dsh.ChangedAt DESC
                  LIMIT 1
              ) = 1; -- Draft Status ID "
            , new { CompanyId = int.Parse(CompanyId), RequestId = requestId });

            if (result == null)
                throw new Exception("Draft document not available for finalization.");

            return result;


        }
        catch (Exception ex)
        {
            throw ex;
        }
    }


    public async Task<PaginationResult<AllDocumentDto>> GetDocumentByStatusAsync(GetDocumentDto input)
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
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
                    UPPER(Title) LIKE '%{search}%'
                    OR UPPER(DocumentNumber) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "TITLE" => "Title",
                "DOCUMENTNUMBER" => "DocumentNumber",
                "CREATEDAT" => "CreatedAt",
                "CREATEDBY" => "CreatedBy",
                "LASTMODIFIEDAT" => "LastModifiedAt",
                "LASTMODIFIEDBY" => "LastModifiedBy",
                _ => "CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            var dataSql = $@"SELECT * FROM fn_get_my_inbox_documents(
                    @CompanyId,
                    @UserId,
                    @RequestStatus,
                    @DivisionCode,
                    @DepartmentCode,
                    @SubDepartmentCode,
                    @BusinessDomainCode,
                    @DocumentTypeCode
                )
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            var countSql = $@"SELECT COUNT(1) FROM fn_get_my_inbox_documents(
                    @CompanyId,
                    @UserId,
                    @RequestStatus,
                    @DivisionCode,
                    @DepartmentCode,
                    @SubDepartmentCode,
                    @BusinessDomainCode,
                    @DocumentTypeCode
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

            var requests = (await _common.QueryAsync<AllDocumentDto>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            if (!requests.Any())
                return new PaginationResult<AllDocumentDto>
                {
                    Items = new List<AllDocumentDto>(),
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

            return new PaginationResult<AllDocumentDto>
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




    public async Task<PaginationResult<dynamic>> GetPendingAuthorizationsAsync(GetPendingAuthorization input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);
            string stateFilter = "";
            if (input.ActionType.ToUpper() == "REJECTED")
            {
                stateFilter = "IN ('REJECTED')";
            }
            else
            {
                stateFilter = input.IsAuthorized ? "IN ('EFFECTIVE', 'AUTHORIZED')" : "IN ('APPROVED', 'TRAINING_PENDING')";
            }
            // Architecture Note: A document is pending final authorization if it is fully approved,
            // AND (if training is applicable) training has been verified (ReadyForAuthorization = TRUE).
            var whereClause = $@"
                WHERE doc.CompanyId = @CompanyId 
                  AND doc.IsDeleted = FALSE
                  AND (
                      SELECT ds.Code 
                      FROM DocumentStateHistory dsh 
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id 
                      ORDER BY dsh.ChangedAt DESC LIMIT 1
                  ) {stateFilter}
                  AND (
                      tr.Id IS NULL OR tr.ReadyForAuthorization = TRUE
                  )";

            if (!string.IsNullOrWhiteSpace(input.DivisionCode))
                whereClause += " AND doc.DivisionCode = @DivisionCode";
            if (!string.IsNullOrWhiteSpace(input.DepartmentCode))
                whereClause += " AND doc.DepartmentCode = @DepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.SubDepartmentCode))
                whereClause += " AND doc.SubDepartmentCode = @SubDepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.BusinessDomainCode))
                whereClause += " AND doc.BusinessDomainCode = @BusinessDomainCode";
            if (!string.IsNullOrWhiteSpace(input.DocumentTypeCode))
                whereClause += " AND doc.DocumentTypeCode = @DocumentTypeCode";


            // FSD UC-30 Extension: Filter View for SOP vs Other Documents
            if (!string.IsNullOrWhiteSpace(input.DocumentCategoryFilter))
            {
                if (input.DocumentCategoryFilter.ToUpper() == "SOP" || input.DocumentCategoryFilter.ToUpper() == "1")
                {
                    whereClause += " AND UPPER(doc.DocumentType) = 'SOP'";
                }
                else if (input.DocumentCategoryFilter.ToUpper() == "OTHER" || input.DocumentCategoryFilter.ToUpper() == "2")
                {
                    whereClause += " AND UPPER(doc.DocumentType) != 'SOP'";
                }
            }

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@" AND (UPPER(doc.Title) LIKE '%{search}%' OR UPPER(doc.DocumentNumber) LIKE '%{search}%')";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "TITLE" => "doc.Title",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                _ => "doc.CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
                SELECT Distinct
                    doc.*, 
                    dv.Version,
                    tr.TrainingMode,
                    tr.TrainingProofURL,
                    doc.CreatedAt,
                    dv.Version,
                    tr.TrainingProofURL,
                    LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' ||COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS Initiator,
                    doc.CreatedAt,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.IsDeleted = FALSE) AS TotalAssigned,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.TrainingStatus = 1 AND dut.IsDeleted = FALSE) AS TotalCompleted,
                    (SELECT COALESCE(AVG(AssessmentScore), 0) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.TrainingStatus = 1 AND dut.IsDeleted = FALSE) AS AverageScore

                FROM VW_Documents doc   
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = doc.Id AND dv.IsActive = TRUE
                LEFT JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                LEFT JOIN tblEmployee e ON CAST(e.empId AS VARCHAR) = doc.CreatedBy 
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(1) 
                FROM VW_Documents doc 
                LEFT JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                {whereClause};";

            var queryParams = new
            {
                CompanyId = CompanyId,
                DivisionCode = input.DivisionCode,
                DepartmentCode = input.DepartmentCode,
                SubDepartmentCode = input.SubDepartmentCode,
                BusinessDomainCode = input.BusinessDomainCode,
                DocumentTypeCode = input.DocumentTypeCode
            };

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic>
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }


    public async Task<PaginationResult<dynamic>> GetDocumentsPendingApprovalAsync(GetDocumentsPendingApprovalDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            // UC-33: Filter to show only documents in the pipeline (Not Draft, Effective, Rejected, or Obsolete)
            var whereClause = @"
            WHERE doc.CompanyId = @CompanyId 
              AND doc.IsDeleted = FALSE
              AND (@DocumentTypeCode IS NULL OR @DocumentTypeCode = '' OR doc.DocumentTypeCode = @DocumentTypeCode)
              AND (@DivisionCode IS NULL OR @DivisionCode = '' OR doc.DivisionCode = @DivisionCode)
              AND (@DepartmentCode IS NULL OR @DepartmentCode = '' OR doc.DepartmentCode = @DepartmentCode)
              AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '' OR doc.SubDepartmentCode = @SubDepartmentCode)
              AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '' OR doc.BusinessDomainCode = @BusinessDomainCode)
              AND (
                  SELECT ds.Code 
                  FROM DocumentStateHistory dsh 
                  JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                  WHERE dsh.DocumentId = doc.Id 
                  ORDER BY dsh.ChangedAt DESC LIMIT 1
              ) NOT IN ('DRAFT', 'EFFECTIVE', 'CLOSED', 'REJECTED', 'OBSOLETE', 'OBSOLETED')";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@" AND (UPPER(doc.Title) LIKE '%{search}%' OR UPPER(doc.DocumentNumber) LIKE '%{search}%')";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "TITLE" => "doc.Title",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                _ => "doc.CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
            SELECT DISTINCT
                doc.*,
                dv.Version,
                (SELECT ds.Name 
                 FROM DocumentStateHistory dsh 
                 JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                 WHERE dsh.DocumentId = doc.Id 
                 ORDER BY dsh.ChangedAt DESC LIMIT 1) AS CurrentStatus,
                -- Resolve the current workflow authority dynamically
                COALESCE(
                    (SELECT STRING_AGG(
                        COALESCE(LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, ''))), r.Name, des.Name), ', '
                    )
                    FROM WorkflowExecutionSteps wes
                    LEFT JOIN tblEmployee e ON e.empcode = wes.AssignedUserId AND e.CompanyId = doc.CompanyId AND COALESCE(e.Active, 1) = 1
                    LEFT JOIN tblsetupsdetail r ON r.sdlid = wes.AssignedRoleId
                    LEFT JOIN tblsetupsdetail des ON des.sdlid = wes.AssignedDesignationId
                    WHERE wes.WorkflowExecutionId = we.Id AND wes.IsActive = TRUE),
                    'Pending Training/Authorization'
                ) AS CurrentWorkflowAuthority,
                doc.CreatedAt
            FROM Vw_Documents doc
            LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code AND dt.CompanyId = doc.CompanyId
            LEFT JOIN LATERAL (
                SELECT Version FROM DocumentVersions 
                WHERE DocumentId = doc.Id AND CompanyId = doc.CompanyId AND IsActive = TRUE 
                ORDER BY CreatedAt DESC LIMIT 1
            ) dv ON TRUE
            LEFT JOIN WorkflowExecutions we ON we.EntityId = doc.Id AND we.CompanyId = doc.CompanyId AND we.EntityType = 'Document' AND we.Status = 'Running'
            {whereClause}
            ORDER BY {sortColumn} {sortDirection}
            OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
            SELECT COUNT(1) 
            FROM Documents doc 
            {whereClause};";

            var queryParams = new
            {
                CompanyId = CompanyId,
                DocumentTypeCode = input.DocumentTypeCode,
                DivisionCode = input.DivisionCode,
                DepartmentCode = input.DepartmentCode,
                SubDepartmentCode = input.SubDepartmentCode,
                BusinessDomainCode = input.BusinessDomainCode
            };

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic>
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    //public async Task<PaginationResult<dynamic>> GetDocumentsPendingApprovalAsync(TableFiltersDto input)
    //{
    //    try
    //    {
    //        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
    //        int CompanyId = int.Parse(_CompanyId);

    //        // UC-33: Filter to show only documents in the pipeline (Not Draft, Effective, Rejected, or Obsolete)
    //        var whereClause = @"
    //            WHERE doc.CompanyId = @CompanyId 
    //              AND doc.IsDeleted = FALSE
    //              AND (
    //                  SELECT ds.Code 
    //                  FROM DocumentStateHistory dsh 
    //                  JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
    //                  WHERE dsh.DocumentId = doc.Id 
    //                  ORDER BY dsh.ChangedAt DESC LIMIT 1
    //              ) NOT IN ('DRAFT', 'EFFECTIVE', 'CLOSED', 'REJECTED', 'OBSOLETE', 'OBSOLETED')";

    //        if (!string.IsNullOrWhiteSpace(input.SearchText))
    //        {
    //            var search = input.SearchText.Replace("'", "''").ToUpper();
    //            whereClause += $@" AND (UPPER(doc.Title) LIKE '%{search}%' OR UPPER(doc.DocumentNumber) LIKE '%{search}%')";
    //        }

    //        string sortColumn = input.SortColumn?.ToUpper() switch
    //        {
    //            "DOCUMENTNUMBER" => "doc.DocumentNumber",
    //            "TITLE" => "doc.Title",
    //            "CREATEDAT" => "doc.CreatedAt",
    //            "CREATEDBY" => "doc.CreatedBy",
    //            "LASTMODIFIEDAT" => "doc.LastModifiedAt",
    //            "LASTMODIFIEDBY" => "doc.LastModifiedBy",
    //            _ => "doc.CreatedAt"
    //        };

    //        string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
    //        int offset = (input.PageNumber - 1) * input.PageSize;

    //        string dataSql = $@"
    //            SELECT DISTINCT
    //                doc.*,
    //                dv.Version,
    //                (SELECT ds.Name 
    //                 FROM DocumentStateHistory dsh 
    //                 JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
    //                 WHERE dsh.DocumentId = doc.Id 
    //                 ORDER BY dsh.ChangedAt DESC LIMIT 1) AS CurrentStatus,
    //                -- Resolve the current workflow authority dynamically
    //                COALESCE(
    //                    (SELECT STRING_AGG(
    //                        COALESCE(LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, ''))), r.Name, des.Name), ', '
    //                    )
    //                    FROM WorkflowExecutionSteps wes
    //                    LEFT JOIN tblEmployee e ON e.empcode = wes.AssignedUserId AND e.CompanyId = doc.CompanyId AND COALESCE(e.Active, 1) = 1
    //                    LEFT JOIN tblsetupsdetail r ON r.sdlid = wes.AssignedRoleId
    //                    LEFT JOIN tblsetupsdetail des ON des.sdlid = wes.AssignedDesignationId
    //                    WHERE wes.WorkflowExecutionId = we.Id AND wes.IsActive = TRUE),
    //                    'Pending Training/Authorization'
    //                ) AS CurrentWorkflowAuthority,
    //                doc.CreatedAt
    //            FROM Vw_Documents doc
    //            LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code AND dt.CompanyId = doc.CompanyId
    //            LEFT JOIN LATERAL (
    //                SELECT Version FROM DocumentVersions 
    //                WHERE DocumentId = doc.Id AND CompanyId = doc.CompanyId AND IsActive = TRUE 
    //                ORDER BY CreatedAt DESC LIMIT 1
    //            ) dv ON TRUE
    //            LEFT JOIN WorkflowExecutions we ON we.EntityId = doc.Id AND we.CompanyId = doc.CompanyId AND we.EntityType = 'Document' AND we.Status = 'Running'
    //            {whereClause}
    //            ORDER BY {sortColumn} {sortDirection}
    //            OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

    //        string countSql = $@"
    //            SELECT COUNT(1) 
    //            FROM Documents doc 
    //            {whereClause};";

    //        var queryParams = new { CompanyId = CompanyId };

    //        var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
    //        var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

    //        return new PaginationResult<dynamic>
    //        {
    //            Items = items,
    //            TotalCount = totalCount
    //        };
    //    }
    //    catch (Exception ex)
    //    {
    //        throw;
    //    }
    //}

    public async Task<bool> AuthorizeDocumentPostTrainingAsync(AuthorizeDocumentDto input)
    {
        await using var transaction = await _common.BeginTransactionAsync();
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);
            var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (string.IsNullOrWhiteSpace(input.Action))
                throw new CustomException("An action (Approve/Reject) is required.", 400);

            // Get current state for history
            var fromStateId = await _common.ExecuteScalarAsync<int>(
                "SELECT ToStateId FROM DocumentStateHistory WHERE DocumentId = @DocumentId ORDER BY ChangedAt DESC LIMIT 1",
                new { input.DocumentId }, transaction);

            if (input.Action.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                // 1. Archive previous effective versions
                await _common.ExecuteAsync(@"
                    UPDATE DocumentVersions SET VersionType = 3, IsActive = FALSE 
                    WHERE DocumentId = @DocumentId AND VersionType = 2 AND CompanyId = @CompanyId;",
                    new { input.DocumentId, CompanyId }, transaction);

                // 2. Mark the current pending version as Effective
                await _common.ExecuteAsync(@"
                    UPDATE DocumentVersions SET VersionType = 2 
                    WHERE DocumentId = @DocumentId AND VersionType = 1 AND CompanyId = @CompanyId;",
                    new { input.DocumentId, CompanyId }, transaction);

                // 3. Update Document State History to 'EFFECTIVE'
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, Comments, ChangedAt)
                    VALUES (@CompanyId, @DocumentId, @FromStateId, (SELECT Id FROM DocumentStates WHERE Code = 'EFFECTIVE'), @empCode, @Observation, NOW());",
                    new { CompanyId, input.DocumentId, fromStateId, empCode, input.Observation }, transaction);

                // 4. Trigger DCA Notification for physical copy retrieval
                var dcaUsers = await _common.QueryAsync<string>(@"
                    SELECT TRIM(e.empcode) FROM public.tblempjobprofile ejp 
                    INNER JOIN public.tblEmployee e ON e.empid = ejp.empid 
                    INNER JOIN public.tblsetupsdetail sd ON sd.sdlid = ejp.roleid 
                    INNER JOIN DocumentUserDistributions dud ON dud.EmployeeCode = e.Empcode
                    WHERE sd.smsid = 189 AND e.CompanyId = @CompanyId AND dud.DocumentId = @DocumentId
                    AND COALESCE(e.Active, 1) = 1 AND COALESCE(ejp.Active, TRUE) = TRUE",
                    new { CompanyId, input.DocumentId }, transaction);

                var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>("SELECT Title FROM Documents WHERE Id = @DocumentId", new { input.DocumentId }, transaction);
                var placeholders = new Dictionary<string, string> { { "Doc Name", (string)docInfo?.title ?? "Document" }, { "V#", "Latest" } };

                foreach (var dcaUser in dcaUsers)
                {
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PhysicalCopyRetrievalTask, CompanyId, input.DocumentId, dcaUser, placeholders, transaction);
                }
            }
            else if (input.Action.Equals("REJECTED", StringComparison.OrdinalIgnoreCase))
            {
                // 1. Update Document State History to 'REJECTED'
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, Comments, ChangedAt)
                    VALUES (@CompanyId, @DocumentId, @FromStateId, (SELECT Id FROM DocumentStates WHERE Code = 'REJECTED'), @empCode, @Observation, NOW());",
                    new { CompanyId, input.DocumentId, fromStateId, empCode, input.Observation }, transaction);

                // 2. Notify the document creator about the rejection
                var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT d.Title, d.CreatedBy, dv.Version 
                    FROM Documents d 
                    JOIN DocumentVersions dv ON d.Id = dv.DocumentId AND dv.IsActive = TRUE
                    WHERE d.Id = @DocumentId ORDER BY dv.CreatedAt DESC LIMIT 1",
                    new { input.DocumentId }, transaction);

                if (docInfo != null && !string.IsNullOrEmpty(docInfo.createdby))
                {
                    var placeholders = new Dictionary<string, string>
                    {
                        { "Doc Name", (string)docInfo!.title ?? "Document" },
                        { "V#", (string)docInfo.version ?? "Latest" },
                        { "Approver", empCode },
                        { "Observation", input.Observation }
                    };
                    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentRejected, CompanyId, input.DocumentId, docInfo.createdby, placeholders, transaction);
                }
            }
            else
            {
                throw new CustomException("Invalid action specified. Must be 'APPROVE' or 'REJECTE'.", 400);
            }

            await transaction.CommitAsync();

            return true;


            //if (string.IsNullOrWhiteSpace(input.Observation))
            //    throw new Exception("Observation comment is mandatory for final authorization.");

            //// 1. Update Document Effective Date
            //// Depending on policy, you might set a future effective date here, 
            //// but for immediate enforcement, NOW() is used.
            ////await _common.ExecuteAsync(@"
            ////    UPDATE Documents 
            ////    SET 
            ////        EffectiveDate = NOW(), 
            ////        LastModifiedAt = NOW(), 
            ////        LastModifiedBy = @empCode 
            ////    WHERE Id = @DocumentId AND CompanyId = @CompanyId;",
            ////    new { input.DocumentId, CompanyId, empCode }, transaction);

            //// 2. Archive previous effective versions (e.g., VersionType 2 = Effective, 3 = Archived)
            //await _common.ExecuteAsync(@"
            //    UPDATE DocumentVersions 
            //    SET VersionType = 3, 
            //        IsActive = FALSE 
            //    WHERE DocumentId = @DocumentId 
            //      AND VersionType = 2 
            //      AND CompanyId = @CompanyId;",
            //    new { input.DocumentId, CompanyId }, transaction);

            //// 3. Mark the current pending version as Effective
            //await _common.ExecuteAsync(@"
            //    UPDATE DocumentVersions 
            //    SET VersionType = 2 
            //    WHERE DocumentId = @DocumentId 
            //      AND VersionType = 1 
            //      AND CompanyId = @CompanyId;",
            //    new { input.DocumentId, CompanyId }, transaction);

            //// 4. Update Document State History to 'EFFECTIVE'
            //await _common.ExecuteAsync(@"
            //    INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, Comments, ChangedAt)
            //    SELECT @CompanyId, @DocumentId, 
            //           (SELECT ToStateId FROM DocumentStateHistory WHERE DocumentId = @DocumentId ORDER BY ChangedAt DESC LIMIT 1),
            //           (SELECT Id FROM DocumentStates WHERE Code = 'EFFECTIVE'), 
            //           @empCode, @Observation, NOW();",
            //    new { CompanyId, input.DocumentId, empCode, input.Observation }, transaction);

            //// 5. Trigger DCA Notification (Physical Copy Retrieval / Obsoletion Task)
            //var dcaUsers = await _common.QueryAsync<string>(@"
            //    SELECT TRIM(e.empcode) 
            //    FROM public.tblempjobprofile ejp 
            //    INNER JOIN public.tblEmployee e ON e.empid = ejp.empid 
            //    INNER JOIN public.tblsetupsdetail sd ON sd.sdlid = ejp.roleid 
            //    WHERE sd.Name = 'DCA' 
            //      AND sd.smsid = 189 
            //      AND e.CompanyId = @CompanyId 
            //      AND COALESCE(e.Active, 1) = 1 
            //      AND COALESCE(ejp.Active, TRUE) = TRUE", new { CompanyId }, transaction);

            //var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>("SELECT Title FROM Documents WHERE Id = @DocumentId", new { input.DocumentId }, transaction);
            //var placeholders = new Dictionary<string, string> { { "Doc Name", (string)docInfo?.title ?? "Document" }, { "V#", "Latest" } };

            //foreach (var dcaUser in dcaUsers)
            //{
            //    await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PhysicalCopyRetrievalTask, CompanyId, input.DocumentId, dcaUser, placeholders, transaction);
            //}

            //await transaction.CommitAsync();

            //return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<PaginationResult<dynamic>> GetAuthorizedDocumentsAsync(GetAuthorizedDocumentsDto input)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // UC-31: Fetch historical documents where the *current user* was the one 
            // who transitioned the document to 'EFFECTIVE' or 'AUTHORIZED'
            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId 
                  AND doc.IsDeleted = FALSE
                  AND EXISTS (SELECT 1 FROM DocumentStateHistory dsh 
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id
                        AND ds.Code IN ('EFFECTIVE', 'AUTHORIZED')
                        AND dsh.ChangedBy = @UserId
                  )";


            if (!string.IsNullOrWhiteSpace(input.DivisionCode))
                whereClause += " AND doc.DivisionCode = @DivisionCode";
            if (!string.IsNullOrWhiteSpace(input.DepartmentCode))
                whereClause += " AND doc.DepartmentCode = @DepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.SubDepartmentCode))
                whereClause += " AND doc.SubDepartmentCode = @SubDepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.BusinessDomainCode))
                whereClause += " AND doc.BusinessDomainCode = @BusinessDomainCode";
            if (!string.IsNullOrWhiteSpace(input.DocumentTypeCode))
                whereClause += " AND doc.DocumentTypeCode = @DocumentTypeCode";


            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@" AND (UPPER(doc.Title) LIKE '%{search}%' OR UPPER(doc.DocumentNumber) LIKE '%{search}%')";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "TITLE" => "doc.Title",
                "DATEOFAUTHORIZATION" => "doc.DateOfAuthorization",
                "VERSION" => "dv.Version",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                _ => "DateOfAuthorization"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
                SELECT 
                    doc.Id AS DocumentId,
                    doc.DocumentNumber,
                    doc.Title,
                    dt.Name AS DocumentType,
                    dt.Code AS DocumentTypeCode,
                    dv.Version,
                    doc.EffectiveDate,
                    (SELECT dsh2.ChangedAt 
                     FROM DocumentStateHistory dsh2 
                     JOIN DocumentStates ds2 ON ds2.Id = dsh2.ToStateId
                     WHERE dsh2.DocumentId = doc.Id 
                       AND ds2.Code IN ('EFFECTIVE', 'AUTHORIZED') 
                       AND dsh2.ChangedBy = @UserId 
                     ORDER BY dsh2.ChangedAt DESC LIMIT 1) AS DateOfAuthorization
                FROM Documents doc
                LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = doc.Id AND dv.IsActive = TRUE
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(1) 
                FROM Documents doc 
                {whereClause};";

            var queryParams = new
            {
                CompanyId = CompanyId,
                UserId = empCode,
                DivisionCode = input.DivisionCode,
                DepartmentCode = input.DepartmentCode,
                SubDepartmentCode = input.SubDepartmentCode,
                BusinessDomainCode = input.BusinessDomainCode,
                DocumentTypeCode = input.DocumentTypeCode
            };

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic>
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<PaginationResult<dynamic>> GetDocumentsPendingTrainingAcknowledgmentAsync(GetDocumentsPendingTrainingDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId 
                  AND doc.IsDeleted = FALSE
                  AND dut.TrainingMode = @TrainingMode
                  AND (
                      SELECT ds.Code 
                      FROM DocumentStateHistory dsh 
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id 
                      ORDER BY dsh.ChangedAt DESC LIMIT 1
                  )  IN ('EFFECTIVE', 'AUTHORIZATION_PENDING')";

            //if (!string.IsNullOrWhiteSpace(input.DocumentCategoryFilter))
            //{
            //    if (input.DocumentCategoryFilter.ToUpper() == "SOP")
            //    {
            //        whereClause += " AND UPPER(dt.Code) = 'SOP'";
            //    }
            //    else if (input.DocumentCategoryFilter.ToUpper() == "OTHER" || input.DocumentCategoryFilter.ToUpper() == "OTHER DOCUMENT")
            //    {
            //        whereClause += " AND UPPER(dt.Code) != 'SOP'";
            //    }
            //}

            if (!string.IsNullOrWhiteSpace(input.DivisionCode))
                whereClause += " AND doc.DivisionCode = @DivisionCode";
            if (!string.IsNullOrWhiteSpace(input.DepartmentCode))
                whereClause += " AND doc.DepartmentCode = @DepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.SubDepartmentCode))
                whereClause += " AND doc.SubDepartmentCode = @SubDepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.BusinessDomainCode))
                whereClause += " AND doc.BusinessDomainCode = @BusinessDomainCode";
            if (!string.IsNullOrWhiteSpace(input.DocumentTypeCode))
                whereClause += " AND doc.DocumentTypeCode = @DocumentTypeCode";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@" AND (UPPER(doc.Title) LIKE '%{search}%' OR UPPER(doc.DocumentNumber) LIKE '%{search}%')";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "TITLE" => "doc.Title",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                _ => "doc.CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
                SELECT DISTINCT
                    doc.*, 
                    dv.Version,
                    tr.TrainingMode,
                    tr.TrainingProofURL,
                    doc.CreatedAt,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.IsDeleted = FALSE) AS TotalAssigned,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.TrainingStatus = 1 AND dut.IsDeleted = FALSE) AS TotalCompleted,
                    (SELECT COALESCE(AVG(AssessmentScore), 0) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.TrainingStatus = 1 AND dut.IsDeleted = FALSE) AS AverageScore
                FROM Vw_Documents doc  
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = doc.Id AND dv.IsActive = TRUE
                INNER JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                LEFT JOIN DocumentUserTraining dut ON dut.DocumentId = doc.Id
                {whereClause} 
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(DISTINCT doc.Id) 
                FROM Documents doc 
                LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code
                INNER JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE 
                LEFT JOIN DocumentUserTraining dut ON dut.DocumentId = doc.Id
                {whereClause};";

            var queryParams = new
            {
                CompanyId = CompanyId,
                UserId = empCode,
                DivisionCode = input.DivisionCode,
                DepartmentCode = input.DepartmentCode,
                SubDepartmentCode = input.SubDepartmentCode,
                BusinessDomainCode = input.BusinessDomainCode,
                DocumentTypeCode = input.DocumentTypeCode,
                TrainingMode = input.Requeststatus == "Classroom" ? 1 : 2
            };

            var items = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<dynamic>
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<PaginationResult<dynamic>> GetApprovedEffectiveDocumentsAsync(GetApprovedDocumentsFilterDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            // Base condition: Document is not deleted and its current state is 'EFFECTIVE'
            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId 
                  AND doc.IsDeleted = FALSE
                  AND (
                      SELECT ds.Code 
                      FROM DocumentStateHistory dsh 
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id 
                      ORDER BY dsh.ChangedAt DESC LIMIT 1
                  ) = 'EFFECTIVE'";

            if (!string.IsNullOrWhiteSpace(input.DivisionCode))
                whereClause += " AND doc.DivisionCode = @DivisionCode";
            if (!string.IsNullOrWhiteSpace(input.DepartmentCode))
                whereClause += " AND doc.DepartmentCode = @DepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.SubDepartmentCode))
                whereClause += " AND doc.SubDepartmentCode = @SubDepartmentCode";
            if (!string.IsNullOrWhiteSpace(input.BusinessDomainCode))
                whereClause += " AND doc.BusinessDomainCode = @BusinessDomainCode";
            if (!string.IsNullOrWhiteSpace(input.DocumentTypeCode))
                whereClause += " AND doc.DocumentTypeCode = @DocumentTypeCode";

            // 1. Keyword Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@" AND (UPPER(doc.Title) LIKE '%{search}%' OR UPPER(doc.DocumentNumber) LIKE '%{search}%')";
            }

            // 2. Date Range Filters
            if (!string.IsNullOrWhiteSpace(input.ApprovedFromDate) && !string.IsNullOrWhiteSpace(input.ApprovedToDate))
            {
                whereClause += @" AND EXISTS (
                        SELECT 1 FROM DocumentStateHistory dshDate 
                        JOIN DocumentStates dsDate ON dsDate.Id = dshDate.ToStateId
                        WHERE dshDate.DocumentId = doc.Id 
                          AND dsDate.Code = 'EFFECTIVE'
                          AND dshDate.ChangedAt::date >= @ApprovedFromDate::date AND dshDate.ChangedAt::date <= @ApprovedToDate::date
                    )";
            }

            if (!string.IsNullOrWhiteSpace(input.RequestCreatedFromDate) && !string.IsNullOrWhiteSpace(input.RequestCreatedToDate))
            {
                whereClause += " AND (doc.CreatedAt::date >= @RequestCreatedFromDate::date AND doc.CreatedAt::date <= @RequestCreatedToDate::date)";
            }
            if (!string.IsNullOrWhiteSpace(input.RequestCreatedBy) && !string.IsNullOrWhiteSpace(input.RequestCreatedBy))
            {
                whereClause += " AND doc.CreatedBy = @RequestCreatedBy";
            }

            if (input.DateFrom.HasValue && input.DateTo.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(input.DateFilterType) && input.DateFilterType.Equals("CreationDate", StringComparison.OrdinalIgnoreCase))
                {
                    whereClause += " AND (doc.CreatedAt::date >= @DateFrom::date AND doc.CreatedAt::date <= @DateTo::date)";
                }
                else
                {
                    // Default to Approval/Authorization Date filter
                    whereClause += @" AND EXISTS (
                        SELECT 1 FROM DocumentStateHistory dshDate 
                        JOIN DocumentStates dsDate ON dsDate.Id = dshDate.ToStateId
                        WHERE dshDate.DocumentId = doc.Id 
                          AND dsDate.Code = 'EFFECTIVE'
                          AND dshDate.ChangedAt::date >= @DateFrom::date AND dshDate.ChangedAt::date <= @DateTo::date
                    )";
                }
            }

            // 3. Sorting
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "TITLE" => "doc.Title",
                "DATEOFAUTHORIZATION" => "doc.DateOfAuthorization",
                "VERSION" => "dv.Version",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                _ => "DateOfAuthorization"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            // 4. Data Query
            string dataSql = $@"
                SELECT 
                    Distinct doc.*,
                    (SELECT dsh2.ChangedAt 
                     FROM DocumentStateHistory dsh2 
                     JOIN DocumentStates ds2 ON ds2.Id = dsh2.ToStateId
                     WHERE dsh2.DocumentId = doc.Id 
                       AND ds2.Code = 'EFFECTIVE'
                     ORDER BY dsh2.ChangedAt DESC LIMIT 1) AS DateOfAuthorization
                FROM VW_Documents doc
                LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code AND dt.CompanyId = doc.CompanyId
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = doc.Id AND dv.VersionType = 2 AND dv.IsActive = TRUE
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(Distinct doc.Id) 
                FROM VW_Documents doc 
                {whereClause};";

            var queryParams = new
            {
                CompanyId = CompanyId,
                DateFrom = input.DateFrom,
                DateTo = input.DateTo,
                ApprovedFromDate = input.ApprovedFromDate,
                ApprovedToDate = input.ApprovedToDate,
                RequestCreatedFromDate = input.RequestCreatedFromDate,
                RequestCreatedToDate = input.RequestCreatedToDate,
                DivisionCode = input.DivisionCode,
                DepartmentCode = input.DepartmentCode,
                SubDepartmentCode = input.SubDepartmentCode,
                BusinessDomainCode = input.BusinessDomainCode,
                DocumentTypeCode = input.DocumentTypeCode,
                RequestCreatedBy = input.RequestCreatedBy
            };

            var documents = (await _common.QueryAsync<dynamic>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            if (!documents.Any())
            {
                return new PaginationResult<dynamic> { Items = new List<dynamic>(), TotalCount = 0 };
            }

            // 5. Fetch Distribution Lists for the retrieved documents
            var documentIds = documents.Select(x => (int)x.id).ToArray();

            var roleDistributions = (await _common.QueryAsync<dynamic>(@"
                SELECT drd.DocumentId, r.Name AS RoleName, div.Name AS Division, dep.Name AS Department
                FROM DocumentRoleDistributions drd
                LEFT JOIN Roles r ON drd.RoleId = r.Id 
                LEFT JOIN Divisions div ON drd.DivisionCode = div.Code 
                LEFT JOIN Departments dep ON drd.DepartmentCode = dep.Code
                WHERE drd.CompanyId = @CompanyId AND drd.DocumentId = ANY(@DocumentIds);",
                new { CompanyId, DocumentIds = documentIds })).ToList();

            var userDistributions = (await _common.QueryAsync<dynamic>(@"
                SELECT dud.DocumentId, e.empCode AS EmployeeCode, 
                       LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName
                FROM DocumentUserDistributions dud
                LEFT JOIN tblEmployee e on LPAD(dud.EmployeeCode::text, 9, '0') = e.empCode
                WHERE dud.CompanyId = @CompanyId AND dud.DocumentId = ANY(@DocumentIds);",
                new { CompanyId, DocumentIds = documentIds })).ToList();

            // 6. Map Distributions back to their respective documents
            var finalResult = documents.Select(doc =>
            {
                var docDict = (IDictionary<string, object>)doc;
                int currentDocId = (int)docDict["id"];

                docDict["RoleDistributions"] = roleDistributions.Where(r => (int)r.documentid == currentDocId).ToList();
                docDict["UserDistributions"] = userDistributions.Where(u => (int)u.documentid == currentDocId).ToList();

                return docDict;
            }).ToList<dynamic>();

            return new PaginationResult<dynamic>
            {
                Items = finalResult,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    private class ParsedRow
    {
        public int RowNumber { get; set; }
        public string[] Columns { get; set; } = Array.Empty<string>();
    }

    public async Task<List<string>> BulkImportDocumentMetadataAsync(IFormFile excelFile)
    {
        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int CompanyId = int.Parse(_CompanyId);
        var clientIp = _clientContextService.GetClientIP();
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

        //LogToFile($"[BULK IMPORT] Starting import. ClientIP: {clientIp}, EmpId: {empId}, EmpCode: {empCode}, CompanyId: {CompanyId}");

        var results = new List<string>();
        if (excelFile == null || excelFile.Length == 0)
        {
            //LogToFile("[BULK IMPORT] Error: No file provided or file length is 0.");
            results.Add("Error: No file provided.");
            return results;
        } 

        var parsedRows = new List<ParsedRow>();
        var fileExtension = Path.GetExtension(excelFile.FileName).ToLower();
        //LogToFile($"[BULK IMPORT] File name: '{excelFile.FileName}', extension: '{fileExtension}', length: {excelFile.Length} bytes.");

        if (fileExtension == ".xlsx" || fileExtension == ".xls")
        {
            try
            {
                //LogToFile("[BULK IMPORT] Opening file as Excel package...");
                using var stream = excelFile.OpenReadStream();
                using var package = new ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                if (worksheet == null || worksheet.Dimension == null)
                {
                    //LogToFile("[BULK IMPORT] Error: Excel worksheet or dimension is null.");
                    results.Add("Error: Excel worksheet is empty or invalid.");
                    return results;
                }

                int totalRows = worksheet.Dimension.End.Row;
                int totalCols = worksheet.Dimension.End.Column;
                //LogToFile($"[BULK IMPORT] Excel sheet loaded. Rows: {totalRows}, Columns: {totalCols}");

                if (totalRows < 1)
                {
                    //LogToFile("[BULK IMPORT] Error: Excel totalRows < 1.");
                    results.Add("Error: Excel file has no rows.");
                    return results;
                }

                // Row 1 is header, data starts from Row 2
                for (int row = 2; row <= totalRows; row++)
                {
                    bool isRowBlank = true;
                    int maxCol = Math.Max(11, totalCols);
                    var cols = new string[maxCol];
                    for (int col = 1; col <= maxCol; col++)
                    {
                        var cellValue = worksheet.Cells[row, col].Text?.Trim() ?? worksheet.Cells[row, col].Value?.ToString()?.Trim() ?? "";
                        cols[col - 1] = cellValue;
                        if (!string.IsNullOrWhiteSpace(cellValue))
                        {
                            isRowBlank = false;
                        }
                    }

                    if (!isRowBlank)
                    {
                        parsedRows.Add(new ParsedRow { RowNumber = row, Columns = cols });
                    }
                }
                //LogToFile($"[BULK IMPORT] Excel parsing complete. Found {parsedRows.Count} non-empty rows.");
            }
            catch (Exception ex)
            {
                //LogToFile($"[BULK IMPORT] Exception during Excel read: {ex}");
                results.Add($"Error reading Excel file: {ex.Message}");
                return results;
            }
        }
        else if (fileExtension == ".csv")
        {
            try
            {
                //LogToFile("[BULK IMPORT] Opening file as CSV...");
                using var reader = new StreamReader(excelFile.OpenReadStream());
                var header = await reader.ReadLineAsync();

                if (string.IsNullOrWhiteSpace(header))
                {
                    //LogToFile("[BULK IMPORT] Error: CSV header is empty.");
                    results.Add("Error: CSV file is empty or has an invalid header.");
                    return results;
                }

                int rowCount = 1;
                while (!reader.EndOfStream)
                {
                    rowCount++;
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = System.Text.RegularExpressions.Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)")
                                                                   .Select(x => x.Trim('"', ' ')).ToArray();
                    parsedRows.Add(new ParsedRow { RowNumber = rowCount, Columns = cols });
                }
                //LogToFile($"[BULK IMPORT] CSV parsing complete. Found {parsedRows.Count} non-empty rows.");
            }
            catch (Exception ex)
            {
                //LogToFile($"[BULK IMPORT] Exception during CSV read: {ex}");
                results.Add($"Error reading CSV file: {ex.Message}");
                return results;
            }
        }
        else
        {
            //LogToFile($"[BULK IMPORT] Error: Invalid file format extension: '{fileExtension}'.");
            results.Add("Error: Invalid file format. Please upload an .xlsx, .xls or .csv file.");
            return results;
        }

        foreach (var parsedRow in parsedRows)
        {
            int row = parsedRow.RowNumber;
            var cols = parsedRow.Columns;

            //LogToFile($"[BULK IMPORT] Row {row}: Starting processing. Cols count: {cols.Length}");
            await using var tx = await _common.BeginTransactionAsync();
            try
            {
                if (cols.Length < 11)
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Insufficient columns ({cols.Length} < 11). Skipped.");
                    results.Add($"Row {row}: Skipped. Insufficient columns (expected 11, found {cols.Length}).");
                    await tx.RollbackAsync();
                    continue;
                }

                // New Column Order Mapping
                var docNum = cols[1].Trim();
                var title = cols[2].Trim();
                var initiatorName = cols[3].Trim();
                var version = string.IsNullOrWhiteSpace(cols[4]) ? "1.0" : cols[4].Trim();
                var docTypeName = cols[5].Trim();
                var divName = cols[6].Trim();
                var deptName = cols[7].Trim();
                var subDeptName = cols[8].Trim();
                var nextReviewDateStr = cols[9].Trim();
                var expectedFileName = cols[10].Trim();

                //LogToFile($"[BULK IMPORT] Row {row}: Mapping: DocNum='{docNum}', Title='{title}', Initiator='{initiatorName}', Version='{version}', DocType='{docTypeName}', Div='{divName}', Dept='{deptName}', SubDept='{subDeptName}', NextReviewStr='{nextReviewDateStr}', ExpectedFile='{expectedFileName}'");

                if (string.IsNullOrWhiteSpace(docNum) && string.IsNullOrWhiteSpace(title))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Both DocNum and Title are empty. Skipped.");
                    await tx.RollbackAsync();
                    continue;
                }

                // --- Data Validation and Lookups ---

                if (!DateTime.TryParse(nextReviewDateStr, out DateTime nextReviewDate) || nextReviewDate.Year < 2000)
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Invalid Next Review Date: '{nextReviewDateStr}'. Skipped.");
                    results.Add($"Row {row}: Skipped. Invalid Next Review Date '{nextReviewDateStr}'.");
                    await tx.RollbackAsync();
                    continue;
                }

                //LogToFile($"[BULK IMPORT] Row {row}: Querying lookup for Document Type Code where Name='{docTypeName}'");
                var docTypeCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM DocumentTypes WHERE CompanyId = @CompanyId AND Name = @Name AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = docTypeName }, tx);
                if (string.IsNullOrEmpty(docTypeCode))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Document Type '{docTypeName}' not found. Skipped.");
                    results.Add($"Row {row}: Skipped. Document Type '{docTypeName}' not found.");
                    await tx.RollbackAsync();
                    continue;
                }

                //LogToFile($"[BULK IMPORT] Row {row}: Querying lookup for Division Code where Name='{divName}'");
                var divCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM Divisions WHERE CompanyId = @CompanyId AND Name = @Name AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = divName }, tx);
                if (string.IsNullOrEmpty(divCode))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Division '{divName}' not found. Skipped.");
                    results.Add($"Row {row}: Skipped. Division '{divName}' not found.");
                    await tx.RollbackAsync();
                    continue;
                }

                //LogToFile($"[BULK IMPORT] Row {row}: Querying lookup for Department Code where Name='{deptName}' and DivCode='{divCode}'");
                var deptCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM Departments WHERE CompanyId = @CompanyId AND Name = @Name AND DivisionCode = @DivCode AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = deptName, DivCode = divCode }, tx);
                if (string.IsNullOrEmpty(deptCode))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Department '{deptName}' not found in Division '{divName}'. Skipped.");
                    results.Add($"Row {row}: Skipped. Department '{deptName}' not found in Division '{divName}'.");
                    await tx.RollbackAsync();
                    continue;
                }

                //LogToFile($"[BULK IMPORT] Row {row}: Querying lookup for SubDepartment Code where Name='{subDeptName}' and DeptCode='{deptCode}'");
                var subDeptCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM SubDepartments WHERE CompanyId = @CompanyId AND Name = @Name AND DepartmentCode = @DeptCode AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = subDeptName, DeptCode = deptCode }, tx);
                if (string.IsNullOrEmpty(subDeptCode))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Sub-Department '{subDeptName}' not found in Department '{deptName}'. Skipped.");
                    results.Add($"Row {row}: Skipped. Sub-Department '{subDeptName}' not found in Department '{deptName}'.");
                    await tx.RollbackAsync();
                    continue;
                }

                // --- Database Insertion / Update ---
                
                //LogToFile($"[BULK IMPORT] Row {row}: Checking if document with Title='{title}' exists...");
                var existingDoc = await _common.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT Id, DocumentNumber FROM Documents WHERE Title = @Title AND CompanyId = @CompanyId AND IsDeleted = FALSE LIMIT 1",
                    new { Title = title, CompanyId }, tx);

                if (existingDoc != null)
                {
                    var docDict = (IDictionary<string, object>)existingDoc;
                    int existingId = Convert.ToInt32(docDict["id"]);
                    string existingDocNum = docDict["documentnumber"]?.ToString() ?? "";
                    //LogToFile($"[BULK IMPORT] Row {row}: Existing document found. ID: {existingId}, DocNum: '{existingDocNum}'. Performing UPDATE.");

                    await _common.ExecuteAsync(@"
                        UPDATE Documents
                        SET DocumentTypeCode = @DocumentTypeCode,
                            DivisionCode = @DivisionCode,
                            DepartmentCode = @DepartmentCode,
                            SubDepartmentCode = @SubDepartmentCode,
                            NextReviewdate = @NextReviewDate,
                            DocumentURL = @ExpectedFileName,
                            LastModifiedAt = NOW(),
                            LastModifiedBy = @UserId
                        WHERE Id = @Id AND CompanyId = @CompanyId;", new
                    {
                        CompanyId,
                        Id = existingId,
                        DocumentTypeCode = docTypeCode,
                        DivisionCode = string.IsNullOrEmpty(divCode) ? null : divCode,
                        DepartmentCode = string.IsNullOrEmpty(deptCode) ? null : deptCode,
                        SubDepartmentCode = string.IsNullOrEmpty(subDeptCode) ? null : subDeptCode,
                        NextReviewDate = nextReviewDate,
                        ExpectedFileName = expectedFileName,
                        UserId = empCode
                    }, tx);

                    //LogToFile($"[BULK IMPORT] Row {row}: UPDATE on Documents table succeeded. Checking if version '{version}' exists...");
                    int versionExists = await _common.ExecuteScalarAsync<int>(
                        "SELECT COUNT(1) FROM DocumentVersions WHERE DocumentId = @DocumentId AND Version = @Version AND CompanyId = @CompanyId AND IsActive = TRUE",
                        new { DocumentId = existingId, Version = version, CompanyId }, tx);

                    if (versionExists == 0)
                    {
                        //LogToFile($"[BULK IMPORT] Row {row}: Version '{version}' does not exist. Inserting into DocumentVersions.");
                        await _common.ExecuteAsync(@"
                            INSERT INTO DocumentVersions
                            (CompanyId, DocumentId, Version, VersionType, IsActive, CreatedBy, CreatedAt, LastModifiedBy, LastModifiedAt)
                            VALUES (@CompanyId, @DocumentId, @Version, 2, TRUE, @UserId, NOW(), @UserId, NOW());",
                            new { CompanyId, DocumentId = existingId, Version = version, UserId = empCode }, tx);
                    }
                    else
                    {
                        //LogToFile($"[BULK IMPORT] Row {row}: Version '{version}' already exists.");
                    }

                    await tx.CommitAsync();
                    //LogToFile($"[BULK IMPORT] Row {row}: Transaction committed successfully (UPDATE).");
                    results.Add($"Row {row}: Successfully updated metadata for '{existingDocNum}'.");
                }
                else
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Document not found. Performing INSERT.");
                    int newId = await _common.ExecuteScalarAsync<int>(@"
                        INSERT INTO Documents
                        (   CompanyId, DocumentNumber, DocumentTypeCode, DivisionCode, DepartmentCode,
                            SubDepartmentCode, Title, NextReviewdate, DocumentURL,
                            IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy
                        )
                        VALUES
                        (
                            @CompanyId, 'DOC-' || nextval('document_seq'), @DocumentTypeCode, @DivisionCode, @DepartmentCode,
                            @SubDepartmentCode, @Title, @NextReviewDate, @ExpectedFileName,
                            TRUE, FALSE, NOW(), @UserId, NOW(), @UserId
                        )
                        RETURNING Id;", new
                    {
                        CompanyId,
                        DocumentTypeCode = docTypeCode,
                        DivisionCode = string.IsNullOrEmpty(divCode) ? null : divCode,
                        DepartmentCode = string.IsNullOrEmpty(deptCode) ? null : deptCode,
                        SubDepartmentCode = string.IsNullOrEmpty(subDeptCode) ? null : subDeptCode,
                        Title = title,
                        NextReviewDate = nextReviewDate,
                        ExpectedFileName = expectedFileName,
                        UserId = empCode
                    }, tx);

                    //LogToFile($"[BULK IMPORT] Row {row}: INSERT on Documents table succeeded. New ID: {newId}. Inserting into DocumentVersions...");
                    await _common.ExecuteAsync(@"
                        INSERT INTO DocumentVersions
                        (CompanyId, DocumentId, Version, VersionType, IsActive, CreatedBy, CreatedAt, LastModifiedBy, LastModifiedAt)
                        VALUES (@CompanyId, @DocumentId, @Version, 2, TRUE, @UserId, NOW(), @UserId, NOW());",
                        new { CompanyId, DocumentId = newId, Version = version, UserId = empCode }, tx);

                    await tx.CommitAsync();
                    //LogToFile($"[BULK IMPORT] Row {row}: Transaction committed successfully (INSERT).");
                    results.Add($"Row {row}: Successfully imported metadata for '{docNum}'.");
                }
            }
            catch (Exception ex)
            {
                //LogToFile($"[BULK IMPORT] Row {row}: Exception caught: {ex}");
                await tx.RollbackAsync();
                results.Add($"Row {row}: Skipped. An unexpected error occurred: {ex.Message}. StackTrace: {ex.StackTrace}");
            }
        }

        return results;
    }

    private void LogToFile(string message)
    {
        try
        {
            Console.WriteLine(message);
            var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            var logPath = Path.Combine(logDirectory, "bulk_import_log.txt");
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, logLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BULK IMPORT LOGGER ERROR] {ex.Message}");
        }
    }

    public async Task<List<string>> BulkUploadDocumentFilesAsync(List<IFormFile> files)
    {
        var results = new List<string>();
        if (files == null || !files.Any())
        {
            results.Add("No files provided.");
            return results;
        }

        string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        int CompanyId = int.Parse(_CompanyId);
        var clientIp = _clientContextService.GetClientIP();
        var empId = _utilities.GetEmpid(clientIp);
        var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                results.Add($"File {file.FileName}: Empty file.");
                continue;
            }

            try
            {
                var safeFileName = Path.GetFileName(file.FileName);
                var fileExtension = Path.GetExtension(safeFileName).ToLower();

                // 1. Hybrid processing: Handle Zip archives containing legacy documents of various types
                if (fileExtension == ".zip")
                {
                    using var stream = file.OpenReadStream();
                    using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // Skip directories

                        var entryExt = Path.GetExtension(entry.Name);
                        var entryNameWithoutExt = Path.GetFileNameWithoutExtension(entry.Name);

                        // Find matching document metadata by exact file name or base name
                        var docId = await _common.ExecuteScalarAsync<int?>(@"
                            SELECT Id 
                            FROM Documents 
                            WHERE CompanyId = @CompanyId 
                              AND (DocumentURL = @FileName OR DocumentURL = @FileNameWithoutExt)
                              AND IsDeleted = FALSE 
                            ORDER BY Id DESC 
                            LIMIT 1;",
                            new { CompanyId, FileName = entry.Name, FileNameWithoutExt = entryNameWithoutExt });

                        if (docId == null || docId == 0)
                        {
                            results.Add($"Zip Entry {entry.Name}: No matching metadata record found.");
                            continue;
                        }

                        var newFileName = $"{Guid.NewGuid()}{entryExt}";
                        var filePath = Path.Combine(uploadsRoot, newFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            using var entryStream = entry.Open();
                            await entryStream.CopyToAsync(fileStream);
                        }

                        var documentUrl = $"/uploads/documents/{newFileName}";

                        await _common.ExecuteAsync(@"
                            UPDATE Documents 
                            SET DocumentURL = @DocumentUrl,
                                LastModifiedAt = NOW(),
                                LastModifiedBy = @UserId
                            WHERE Id = @DocumentId;",
                            new { DocumentUrl = documentUrl, UserId = empCode, DocumentId = docId });

                        results.Add($"Zip Entry {entry.Name}: Successfully attached to Document ID {docId}.");
                    }
                }
                else
                {
                    // 2. Hybrid processing: Handle individual files of any type (Word, PDF, PNG, etc.)
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);

                    var documentId = await _common.ExecuteScalarAsync<int?>(@"
                        SELECT Id 
                        FROM Documents 
                        WHERE CompanyId = @CompanyId 
                          AND (DocumentURL = @FileName OR DocumentURL = @FileNameWithoutExt)
                          AND IsDeleted = FALSE 
                        ORDER BY Id DESC 
                        LIMIT 1;",
                        new { CompanyId, FileName = safeFileName, FileNameWithoutExt = fileNameWithoutExt });

                    if (documentId == null || documentId == 0)
                    {
                        results.Add($"File {safeFileName}: No matching metadata record found.");
                        continue;
                    }

                    var newFileName = $"{file.FileName}{fileExtension}";
                    var filePath = Path.Combine(uploadsRoot, newFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    var documentUrl = $"/uploads/documents/{newFileName}";

                    await _common.ExecuteAsync(@"
                        UPDATE Documents 
                        SET DocumentURL = @DocumentUrl,
                            LastModifiedAt = NOW(),
                            LastModifiedBy = @UserId
                        WHERE Id = @DocumentId;",
                        new { DocumentUrl = documentUrl, UserId = empCode, DocumentId = documentId });


                    results.Add($"File {safeFileName}: Successfully attached to Document ID {documentId}.");
                }
            }
            catch (Exception ex)
            {
                results.Add($"File {file.FileName}: Error - {ex.Message}");
            }
        }

        return results;
    }

    public async Task<object> GetMyDocumentCountsAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Query 1: Counts for documents CREATED BY the current user
            var myDocumentsQuery = @"
                WITH LatestStates AS (
                    SELECT 
                        dsh.DocumentId,
                        dsh.ToStateId,
                        ROW_NUMBER() OVER(PARTITION BY dsh.DocumentId ORDER BY dsh.ChangedAt DESC) as rn
                    FROM DocumentStateHistory dsh
                    WHERE dsh.CompanyId = @CompanyId
                )
                SELECT 
                    COUNT(1) FILTER (WHERE ls.ToStateId = 1) AS Draft,
                    COUNT(1) FILTER (WHERE ls.ToStateId = 2) AS InReview,
                    COUNT(1) FILTER (WHERE ls.ToStateId IN (3, 4, 6)) AS Approved,
                    COUNT(1) FILTER (WHERE ls.ToStateId = 5) AS Rejected
                FROM Documents d
                JOIN LatestStates ls ON d.Id = ls.DocumentId AND ls.rn = 1
                WHERE d.CompanyId = @CompanyId
                  AND d.CreatedBy = @empCode
                  AND d.IsDeleted = FALSE;";

            var myDocumentsCounts = await _common.QueryFirstOrDefaultAsync<dynamic>(myDocumentsQuery, new { CompanyId, empCode });

            // Query 2: Counts for documents in the current user's INBOX (for approval)
            var myInboxQuery = @"
            SELECT
                COUNT(1) FILTER (WHERE we.Status = 'Running' AND wes.IsActive = TRUE AND wes.Decision IS NULL) AS Pending,
                COUNT(1) FILTER (WHERE wes.Decision = 'Approved') AS Approved,
                COUNT(1) FILTER (WHERE we.Status = 'Rejected' AND wes.Decision = 'Rejected') AS Rejected,
                COUNT(1) FILTER (WHERE we.Status = 'Reworked' AND wes.Decision = 'Reworked') AS Reworked
            FROM WorkflowExecutionSteps wes
            JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId AND we.CompanyId = wes.CompanyId
            JOIN Vw_Documents d ON d.Id = we.EntityId AND d.CompanyId = we.CompanyId
            -- 👇 This JOIN is the final piece of the puzzle to match the function's logic
            LEFT JOIN DocumentRequests dr ON d.RequestId = dr.Id
            WHERE wes.CompanyId = @CompanyId 
              AND we.EntityType = 'Document'
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

            return new
            {
                MyDocuments = myDocumentsCounts,
                MyInbox = new
                {
                    pending = myInboxCounts?.pending ?? 0,
                    approved = myInboxCounts?.approved ?? 0,
                    rejectedorreverted = (myInboxCounts?.rejected ?? 0) + (myInboxCounts?.reworked ?? 0)
                }
            };
        }
        catch (Exception)
        {
            throw;
        }
    }



    public async Task<byte[]> ExportMyDocumentsAsync(GetDocumentDto input)
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
             
            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "TITLE" => "Title",
                "DOCUMENTNUMBER" => "DocumentNumber",
                "CREATEDAT" => "CreatedAt",
                "CREATEDBY" => "CreatedBy",
                "LASTMODIFIEDAT" => "LastModifiedAt",
                "LASTMODIFIEDBY" => "LastModifiedBy",
                _ => "CreatedAt"
            };


            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";
             

            var dataSql = $@"SELECT * FROM fn_get_my_inbox_documents(
                    @CompanyId,
                    @UserId,
                    @RequestStatus,
                    @DivisionCode,
                    @DepartmentCode,
                    @SubDepartmentCode,
                    @BusinessDomainCode,
                    @DocumentTypeCode
                )
                {whereClause}
                ORDER BY {sortColumn} {sortDirection} ;";

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

             
            var requests = (await _common.QueryAsync<AllDocumentDto>(dataSql, queryParams)).ToList();
            if (!requests.Any())
            {
                return Array.Empty<byte>();
            }

            var sb = new System.Text.StringBuilder();
            
            // Add header row matching Angular model columns
            var headers = new List<string>
            {
                "ExecutionId", "Id", "documentId", "stepId", "stepOrder", "ExecutionStatus",
                "documentType", "documentTypeCode", "documentName", "company", "proposedDocumentNumber", "proposedVersionNumber",
                "division", "department", "departmentId", "subDepartment", "subDepartmentId", "businessDomain", "businessDomainId",
                "proposedContent", "draftFileUrl", "requestCreatedBy", "dateOfCreation", "requestCreatedOn", "startedAt",
                "previsousVersionCreatedBy", "previousVersionCreatedOn", "observation", "requestedBy", "dateOfApproval", "approvalHistory"
            };
            sb.AppendLine(string.Join(",", headers));

            // Add data rows
            foreach (var row in requests)
            {
                var values = new List<string>
                {
                    row.ExecutionId.ToString(),
                    row.Id.ToString(),
                    row.Id.ToString(), // documentId
                    row.StepId.ToString(),
                    row.StepOrder.ToString(),
                    row.ExecutionStatus ?? "Unknown",
                    row.DocumentType ?? "",
                    row.DocumentTypeCode ?? "",
                    row.Title ?? "",
                    row.Company ?? "",
                    row.DocumentNumber ?? "",
                    row.ProposedVersionNumber ?? "1.0",
                    row.Division ?? "",
                    row.Department ?? "",
                    row.DepartmentCode ?? "",
                    row.SubDepartment ?? "",
                    row.SubDepartmentCode ?? "",
                    row.BusinessDomain ?? "",
                    row.BusinessDomainCode ?? "",
                    row.VersionContent ?? "",
                    row.DraftFileURL ?? "",
                    row.RequestCreatedBy ?? "",
                    row.CreatedAt ?? "",
                    row.RequestCreatedAt ?? "",
                    row.StartedAt ?? "",
                    row.RequestCreatedBy ?? "", // previsousVersionCreatedBy
                    row.RequestCreatedAt ?? "", // previousVersionCreatedOn
                    "", // observation (not present, defaults to empty)
                    row.CreatedBy ?? "", // requestedBy
                    "", // dateOfApproval (not present)
                    ""  // approvalHistory (not present)
                };

                // Escape commas and quotes for standard CSV formatting
                var escapedValues = values.Select(value => $"\"{value.Replace("\"", "\"\"")}\"");
                sb.AppendLine(string.Join(",", escapedValues));
            }

            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }
        catch (Exception ex)
        {
            // In a real application, you'd log this exception
            throw new CustomException("Failed to export data.", 500);
        }
    }
}

public class AuthorizeDocumentDto
{
    public int DocumentId { get; set; }
    public string Observation { get; set; }
    public string Action { get; set; } // "APPROVE" or "REJECT"
}

public class GetPendingAuthorization : TableFiltersDto
{
    public string? DocumentCategoryFilter { get; set; }
    public bool IsAuthorized { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; }
    public string? ActionType { get; set; }
}

public class GetDocumentsPendingApprovalDto : TableFiltersDto
{
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; }

}
public class GetAuthorizedDocumentsDto : TableFiltersDto
{ 
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; }
}

public class GetDocumentsPendingTrainingDto : TableFiltersDto
{
    public string? DocumentCategoryFilter { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; }
    public string? Requeststatus { get; set; }
}

public class GetApprovedDocumentsFilterDto : TableFiltersDto
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Accepts "ApprovalDate" or "CreationDate"
    /// </summary>
    public string? DateFilterType { get; set; }

    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public string? DocumentTypeCode { get; set; }
    public string? ApprovedFromDate { get; set; }
    public string? ApprovedToDate { get; set; }
    public string? RequestCreatedFromDate { get; set; }
    public string? RequestCreatedToDate { get; set; }
    public string? RequestCreatedBy { get; set; }
}