using Dapper;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using OfficeOpenXml;
using System.Data;
using System.Text.Json;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    private readonly ILogger<DocumentComponent> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    private readonly NotificationComponent _notificationComponent;
    private readonly PeoplePartnersComponent _peoplePartnersComponent;
    private readonly WorkflowStepComponent _workflowStepComponent;
    private readonly AuditLogComponent _auditLogComponent;
    public DocumentComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        , ILogger<DocumentComponent> logger
        , IHttpContextAccessor http,
        DMSCommon common,
        NotificationComponent notificationComponent,
        PeoplePartnersComponent peoplePartnersComponent,
        WorkflowStepComponent workflowStepComponent,
        AuditLogComponent auditLogComponent
        )
    {
        _http = http;
        _logger = logger;
        _utilities = utilities;
        _dataservice = dataservice;
        _configuration = configuration;
        _clientContextService = clientContextService;
        _dapperService = dapper;
        _common = common;
        _notificationComponent = notificationComponent;
        _peoplePartnersComponent = peoplePartnersComponent;
        _workflowStepComponent = workflowStepComponent;
        _auditLogComponent = auditLogComponent;
    }

    // Full current-state snapshot of a document for the audit log -- see
    // DocumentRequestComponent.BuildRequestSnapshotJson for why this is assembled at the app
    // level (after all of an action's writes complete) instead of via the generic DB trigger:
    // a document's meaningful state spans the Documents row plus its user/role distribution
    // tables, and a row-level trigger can't see writes that happen later in the same transaction.
    private async Task<string?> BuildDocumentSnapshotJson(int companyId, int documentId, IDbTransaction transaction)
    {
        var document = await _common.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM Documents WHERE Id = @documentId AND CompanyId = @companyId;",
            new { documentId, companyId }, transaction);
        var userDistributions = await _common.QueryAsync<dynamic>(
            "SELECT * FROM DocumentUserDistributions WHERE DocumentId = @documentId AND CompanyId = @companyId;",
            new { documentId, companyId }, transaction);
        var roleDistributions = await _common.QueryAsync<dynamic>(
            "SELECT * FROM DocumentRoleDistributions WHERE DocumentId = @documentId AND CompanyId = @companyId;",
            new { documentId, companyId }, transaction);

        if (document == null) return null;

        return JsonSerializer.Serialize(new
        {
            Document = ToDict(document),
            UserDistributions = userDistributions.Select(ToDict).ToList(),
            RoleDistributions = roleDistributions.Select(ToDict).ToList()
        });
    }

    private static Dictionary<string, object>? ToDict(object? row) =>
        row == null ? null : new Dictionary<string, object>((IDictionary<string, object>)row);
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
        var fileName = $"{input.DocumentFile.FileName}";
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
                DocumentNumber = input.DocumentNumber,
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

            var createSnapshotJson = await BuildDocumentSnapshotJson(CompanyId, newId, tx);
            await _auditLogComponent.LogActionAsync(CompanyId, empCode, "Document Created", "Document",
                newId, _clientContextService.GetRequestIpAddress(), newValues: createSnapshotJson, transaction: tx);

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

                NextReviewDate = (row.Table.Columns.Contains("NextReviewDate") && !row.IsNull("NextReviewDate"))
                                ? row.Field<DateOnly>("NextReviewDate").ToString("yyyy-MM-dd") : string.Empty,

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
                    UPPER(doc.Title) LIKE '%{search}%'
                    OR UPPER(doc.DocumentNumber) LIKE '%{search}%' 
                    OR UPPER(doc.Division) LIKE '%{search}%'
                    OR UPPER(doc.Department) LIKE '%{search}%'
                    OR UPPER(doc.SubDepartment) LIKE '%{search}%'
                    OR UPPER(doc.DocumentType) LIKE '%{search}%' 
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
                        FROM VW_Documents doc
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
            // Attach/Update Template. If the caller provides a new file or content at
            // Document Creation time, it overwrites whatever the Document Request stage
            // set (or fills a gap if the Request stage set nothing). If nothing new is
            // provided here, whatever was already attached at Request time is kept as-is.
            //-------------------------------------------------

            await AttachOrUpdateTemplateAsync(input, doc, CompanyId, empCode, transaction);

            //-------------------------------------------------
            // Validate & Save Attributes (NEW METHOD)
            //-------------------------------------------------

            await ValidateAndSaveAttributesAsync(input, doc, transaction);

            //-------------------------------------------------
            // Validate & Save Training Users
            //-------------------------------------------------
            await ValidateAndSaveTrainingUsersAsync(input.TrainingUsers, input.DocumentId, doc, CompanyId, empCode, transaction);

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
                    @CompanyId, @DocumentId, 1, (SELECT Id FROM DocumentStates WHERE Code = 'PENDING_APPROVAL'), @ExecutionId, @empCode
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

    // Uploaded file names sometimes arrive with the extension duplicated (e.g. a browser-downloaded
    // template gets re-saved by the OS/browser as "Template.docx.docx" before being re-uploaded here).
    // Collapses exactly one trailing repeat back to the original name.
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

    // A Document created from an approved Request may not have had its template attached yet
    // (the Request Creation form allows submitting without one). This lets it be attached here,
    // at Document Creation/Submission time, instead. Which input is expected depends entirely on
    // the DocumentType's configured Template: a file-based template (PDF/Word, TemplateType 1/2)
    // expects DocumentFile; an HTML template (TemplateType 3) expects ProposedContent.
    private async Task AttachOrUpdateTemplateAsync(SubmitDocument input, dynamic doc, int companyId, string empCode, IDbTransaction transaction)
    {
        bool hasFile = !string.IsNullOrWhiteSpace((string)doc.documenturl);
        bool hasContent = await _common.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS(
                SELECT 1 FROM DocumentVersions
                WHERE DocumentId = @DocumentId AND CompanyId = @CompanyId AND VersionType = 1
                  AND Content IS NOT NULL AND Content <> ''
            );", new { input.DocumentId, CompanyId = companyId }, transaction);

        // A DocumentType can have more than one active Template row -- one scoped to a specific
        // Division/Department/SubDepartment/BusinessDomain, plus a company-wide IsDefault
        // fallback. Prefer the scoped match for this document's actual cabinet placement (NULL-
        // tolerant, matching the same pattern used for WorkflowPolicies/ReviewPolicies elsewhere),
        // falling back to the default only when no scoped template applies. Without this, "LIMIT 1"
        // with no ORDER BY over multiple candidate rows is non-deterministic and can silently pick
        // a template with the wrong TemplateType (e.g. HTML instead of Word), causing an uploaded
        // file to be routed into the wrong branch below and dropped without error.
        int? templateType = await _common.ExecuteScalarAsync<int?>(@"
            SELECT TemplateType FROM Templates
            WHERE DocumentTypeCode = @DocumentTypeCode AND CompanyId = @CompanyId
              AND IsActive = TRUE AND IsDeleted = FALSE
              AND (
                    IsDefault = TRUE
                    OR (
                        (((DivisionCode IS NULL OR DivisionCode = '') AND (@DivisionCode IS NULL OR @DivisionCode = '')) OR DivisionCode = @DivisionCode)
                        AND (((DepartmentCode IS NULL OR DepartmentCode = '') AND (@DepartmentCode IS NULL OR @DepartmentCode = '')) OR DepartmentCode = @DepartmentCode)
                        AND (((SubDepartmentCode IS NULL OR SubDepartmentCode = '') AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '')) OR SubDepartmentCode = @SubDepartmentCode)
                        AND (((BusinessDomainCode IS NULL OR BusinessDomainCode = '') AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '')) OR BusinessDomainCode = @BusinessDomainCode)
                    )
                  )
            ORDER BY IsDefault ASC
            LIMIT 1;",
            new
            {
                DocumentTypeCode = (string)doc.documenttypecode,
                CompanyId = companyId,
                DivisionCode = (string)doc.divisioncode,
                DepartmentCode = (string)doc.departmentcode,
                SubDepartmentCode = (string)doc.subdepartmentcode,
                BusinessDomainCode = (string)doc.businessdomaincode
            }, transaction);

        bool expectsFile = templateType == 1 || templateType == 2; // 1 = PDF, 2 = Word
        bool expectsContent = templateType == 3;                    // 3 = HTML

        bool fileProvided = input.DocumentFile != null && input.DocumentFile.Length > 0;
        bool contentProvided = !string.IsNullOrWhiteSpace(input.ProposedContent);

        // No Template configured for this DocumentType at all -- don't block submission over a
        // setup gap that isn't the caller's fault; accept whichever of the two was actually sent,
        // falling back to whichever already exists from Request Creation time if neither was sent now.
        if (templateType == null)
        {
            expectsFile = fileProvided || (!contentProvided && hasFile);
            expectsContent = !expectsFile && (contentProvided || hasContent);
        }

        if (expectsFile)
        {
            // A new file at Document Creation time overwrites whatever was set at Request time.
            // If none is provided now, keep the existing file (already attached at Request time).
            if (fileProvided)
            {
                var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileName = SanitizeDuplicatedExtension(input.DocumentFile!.FileName);
                var filePath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await input.DocumentFile.CopyToAsync(stream);
                }

                var documentUrl = $"/uploads/documents/{fileName}";

                await _common.ExecuteAsync(@"
                    UPDATE Documents
                    SET DocumentURL = @DocumentUrl, LastModifiedAt = NOW(), LastModifiedBy = @UserId
                    WHERE Id = @DocumentId AND CompanyId = @CompanyId;",
                    new { DocumentUrl = documentUrl, UserId = empCode, input.DocumentId, CompanyId = companyId }, transaction);
            }
            else if (!hasFile)
            {
                throw new CustomException("A document file is required before this document can be submitted.", 400);
            }
        }
        else if (expectsContent)
        {
            // New content at Document Creation time overwrites whatever was set at Request time.
            // If none is provided now, keep the existing content (already attached at Request time).
            if (contentProvided)
            {
                await _common.ExecuteAsync(@"
                    UPDATE DocumentVersions
                    SET Content = @Content, LastModifiedAt = NOW(), LastModifiedBy = @UserId
                    WHERE DocumentId = @DocumentId AND CompanyId = @CompanyId AND VersionType = 1;",
                    new { Content = input.ProposedContent, UserId = empCode, input.DocumentId, CompanyId = companyId }, transaction);
            }
            else if (!hasContent)
            {
                throw new CustomException("Document content is required before this document can be submitted.", 400);
            }
        }
        else
        {
            throw new CustomException("A document template (file or content) is required before this document can be submitted.", 400);
        }
    }

    // ================================================================================
    // Word Template Merge -- wired into the live flow at *download* time
    // (DMSDocumentController.DownloadDraftDocument, "download-submitted-document-template"),
    // not at upload/AttachOrUpdateTemplateAsync time. Merging fresh on every download instead
    // of once at upload keeps this correct for two things that are only known/final as of
    // "right now": the signature block reflects however much of the approval workflow has
    // actually happened by the time someone downloads (not a stale snapshot from upload time),
    // and metadata like EffectiveDate isn't even set until the document goes EFFECTIVE, well
    // after content is first uploaded. The uploaded content file itself (Documents.DocumentURL)
    // is left exactly as uploaded -- this only ever produces a derived, in-memory copy.
    //
    // Expected placeholders in the template:
    //   Metadata (anywhere in header/body/footer): {{DocumentTitle}}, {{DocumentNumber}},
    //     {{Version}}, {{EffectiveDate}}, {{ReviewDate}}, {{Supersede}}
    //   Content: {{DocumentContent}} -- replaced with the uploaded content file's body. Only
    //     ever looked for in the document body -- a variable-length content section doesn't
    //     make sense in a header/footer, which is fixed content that repeats identically on
    //     every page.
    //   Signature block: ONE table row containing {{ApproverRole}}, {{ApproverName}},
    //     {{ApproverDesignation}}, {{ApproverSignature}}, {{ApprovalDate}} -- cloned once
    //     per step actually configured on this Document's approval workflow (as few as one,
    //     or many, depending on the policy -- not a fixed 4 roles). Looked for in the body AND
    //     every header/footer, since templates commonly put the full approval matrix in a
    //     footer so it repeats on every printed page (all clones landing in that same footer,
    //     since a footer's content is identical on every page regardless).
    // ================================================================================

    // contentStream is now optional: DocumentVersions.Content (the rich-text editor's HTML, see
    // HtmlToOpenXmlConverter) is preferred whenever it's been saved, since it reflects whatever
    // the user last reviewed/edited there -- the uploaded file might no longer match if they
    // edited the preview. contentStream is the fallback, for documents saved before this HTML
    // content existed, or if HTML conversion ever comes back empty.
    public async Task<byte[]> MergeDocumentTemplateAsync(int documentId, Stream? contentStream, IDbTransaction transaction = null)
    {
        try
        {

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int companyId = int.Parse(_CompanyId);

            //-------------------------------------------------
            // 1. Document metadata (from the DB, not the uploaded file)
            //-------------------------------------------------

            var doc = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT Id, DocumentNumber, Title, DocumentTypeCode, NextReviewDate, ParentDocumentId,
                   DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode
            FROM Documents
            WHERE Id = @DocumentId AND CompanyId = @CompanyId AND IsDeleted = FALSE;",
                new { DocumentId = documentId, CompanyId = companyId }, transaction);

            if (doc == null)
                throw new CustomException("Document not found.", 404);

            // Prefer the Effective version (2); every document has a Draft version (1) from the
            // moment it's created (see CreateAsync), so this still shows the current working
            // version for a document that hasn't gone Effective yet instead of a blank field.
            // Content comes from the same row -- the rich-text editor's HTML for this version,
            // if it was ever saved (see HtmlToOpenXmlConverter's use below).
            var currentVersion = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT Version, Content FROM DocumentVersions
            WHERE DocumentId = @DocumentId AND CompanyId = @CompanyId AND VersionType IN (1, 2) AND IsActive = TRUE
            ORDER BY VersionType DESC, CreatedAt DESC LIMIT 1;",
                new { DocumentId = documentId, CompanyId = companyId }, transaction);
            string version = (string?)currentVersion?.version ?? "";
            string? versionHtmlContent = (string?)currentVersion?.content;

            string supersede = "";
            if (doc.parentdocumentid != null)
            {
                supersede = await _common.ExecuteScalarAsync<string>(@"
                SELECT DocumentNumber FROM Documents WHERE Id = @ParentId AND CompanyId = @CompanyId;",
                    new { ParentId = (int)doc.parentdocumentid, CompanyId = companyId }, transaction) ?? "";
            }

            DateTime? effectiveDate = await _common.ExecuteScalarAsync<DateTime?>(@"
            SELECT dsh.ChangedAt
            FROM DocumentStateHistory dsh
            JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
            WHERE dsh.DocumentId = @DocumentId AND ds.Code = 'EFFECTIVE'
            ORDER BY dsh.ChangedAt DESC LIMIT 1;",
                new { DocumentId = documentId }, transaction);

            string reviewDate = doc.nextreviewdate != null
                ? FormatMergeDate((object)doc.nextreviewdate)
                : "";

            var placeholders = new Dictionary<string, string>
        {
            { "DocumentTitle", (string)doc.title ?? "" },
            { "DocumentNumber", (string)doc.documentnumber ?? "" },
            { "Version", version },
            { "EffectiveDate", effectiveDate.HasValue ? effectiveDate.Value.ToString("dd-MMM-yyyy") : "N/A" },
            { "ReviewDate", reviewDate },
            { "Supersede", string.IsNullOrWhiteSpace(supersede) ? "N/A" : supersede }
        };

            //-------------------------------------------------
            // 2. The DocumentType's Word template
            //-------------------------------------------------

            // Same scoped-then-default resolution as AttachOrUpdateTemplateAsync -- a DocumentType
            // can have more than one active Template row (per cabinet scope, plus an IsDefault
            // fallback); this picks the one that actually applies to this document's placement.
            var templatePath = await _common.ExecuteScalarAsync<string>(@"
            SELECT TemplateFileUrl FROM Templates
            WHERE DocumentTypeCode = @DocumentTypeCode AND CompanyId = @CompanyId
              AND TemplateType IN (1,2) AND IsActive = TRUE AND IsDeleted = FALSE
              AND (
                    IsDefault = TRUE
                    OR (
                        (((DivisionCode IS NULL OR DivisionCode = '') AND (@DivisionCode IS NULL OR @DivisionCode = '')) OR DivisionCode = @DivisionCode)
                        AND (((DepartmentCode IS NULL OR DepartmentCode = '') AND (@DepartmentCode IS NULL OR @DepartmentCode = '')) OR DepartmentCode = @DepartmentCode)
                        AND (((SubDepartmentCode IS NULL OR SubDepartmentCode = '') AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '')) OR SubDepartmentCode = @SubDepartmentCode)
                        AND (((BusinessDomainCode IS NULL OR BusinessDomainCode = '') AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '')) OR BusinessDomainCode = @BusinessDomainCode)
                    )
                  )
            ORDER BY IsDefault ASC
            LIMIT 1;",
                new
                {
                    DocumentTypeCode = (string)doc.documenttypecode,
                    CompanyId = companyId,
                    DivisionCode = (string)doc.divisioncode,
                    DepartmentCode = (string)doc.departmentcode,
                    SubDepartmentCode = (string)doc.subdepartmentcode,
                    BusinessDomainCode = (string)doc.businessdomaincode
                }, transaction);

            if (string.IsNullOrWhiteSpace(templatePath))
                throw new CustomException("No Word template configured for this Document Type.", 404);

            var templateFullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", templatePath.TrimStart('/'));
            if (!File.Exists(templateFullPath))
                throw new CustomException("Template file is missing on disk.", 404);

            //-------------------------------------------------
            // 3. Approvers for this Document's workflow -- however many are actually configured,
            //    not a fixed set of roles. Signature/date are only populated for steps already
            //    actioned; a step still pending is included with a blank signature and date.
            //-------------------------------------------------

            var approvers = (await _common.QueryAsync<dynamic>(@"
            SELECT
                wsd.StepType AS ApproverRole,
                LTRIM(RTRIM(COALESCE(e.firstname,'') || ' ' || COALESCE(e.midname,'') || ' ' || COALESCE(e.lastname,''))) AS ApproverName,
                desig.name AS ApproverDesignation,
                wes.Decision,
                wes.ActionAt,
                es.SignatureURL,
                wes.AssignedUserId AS RawAssignedUserId
            FROM WorkflowExecutionSteps wes
            JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
            -- LEFT, not INNER: wsd is only used for display (ApproverRole below) -- a step
            -- whose StepDefinitionId was deleted out from under it (e.g. by a later edit to the
            -- Workflow Policy) would otherwise make that approver vanish from the merged Word
            -- document's signature block entirely, instead of just leaving their role blank.
            LEFT JOIN WorkflowStepDefinitions wsd ON wsd.Id = wes.StepDefinitionId
            LEFT JOIN tblEmployee e ON TRIM(e.empCode) = TRIM(wes.AssignedUserId) AND e.CompanyId = @CompanyId
            LEFT JOIN public.tblempjobprofile ejp ON ejp.empid = e.empid AND ejp.Active = TRUE AND ejp.CompanyId = @CompanyId
            LEFT JOIN public.tblsetupsdetail desig ON desig.sdlid = ejp.dsgid AND desig.CompanyId = @CompanyId
            LEFT JOIN ESignatures es ON TRIM(es.UserId) = TRIM(e.empCode) AND es.CompanyId = @CompanyId AND es.IsActive = TRUE AND es.IsDeleted = FALSE
            WHERE wes.CompanyId = @CompanyId AND we.EntityId = @DocumentId AND we.EntityType = 'Document'
            ORDER BY wes.StepOrder;",
                new { CompanyId = companyId, DocumentId = documentId }, transaction)).ToList();

            //-------------------------------------------------
            // 4. Content: the rich-text editor's HTML if it was saved for this version (reflects
            //    whatever the user actually reviewed/edited there), otherwise the uploaded file's
            //    own body (the page-setup sectPr is excluded -- that belongs to the uploaded
            //    file's own page layout, not the template's).
            //-------------------------------------------------

            List<OpenXmlElement> contentBodyElements;
            if (!string.IsNullOrWhiteSpace(versionHtmlContent))
            {
                contentBodyElements = HtmlToOpenXmlConverter.Convert(versionHtmlContent);
            }
            else if (contentStream != null && contentStream.Length > 0)
            {
                // Buffered into a private MemoryStream rather than opening the caller's stream
                // directly -- WordprocessingDocument.Open wants to own/seek the stream freely, and
                // the caller's stream (e.g. a FileStream held open by the download endpoint) may be
                // needed again or simply shouldn't be handed over.
                using var bufferedContentStream = new MemoryStream();
                contentStream.Position = 0;
                await contentStream.CopyToAsync(bufferedContentStream);
                bufferedContentStream.Position = 0;

                using var contentDoc = WordprocessingDocument.Open(bufferedContentStream, false);
                var contentBody = contentDoc.MainDocumentPart?.Document?.Body
                    ?? throw new CustomException("Uploaded content file is not a valid Word document.", 400);

                contentBodyElements = contentBody.Elements()
                    .Where(el => el is not SectionProperties)
                    .Select(el => el.CloneNode(true))
                    .ToList();
            }
            else
            {
                throw new CustomException("No content available for this document (neither saved rich-text content nor an uploaded content file).", 400);
            }

            //-------------------------------------------------
            // 5. Merge everything into a copy of the template
            //-------------------------------------------------

            using var outputStream = new MemoryStream();
            using (var templateFileStream = new FileStream(templateFullPath, FileMode.Open, FileAccess.Read))
            {
                await templateFileStream.CopyToAsync(outputStream);
            }
            outputStream.Position = 0;

            using (var wordDoc = WordprocessingDocument.Open(outputStream, true))
            {
                var mainPart = wordDoc.MainDocumentPart ?? throw new CustomException("Template file is invalid.", 400);
                var body = mainPart.Document.Body ?? throw new CustomException("Template file is invalid.", 400);

                // Each container is paired with the OpenXmlPart that actually owns it -- every
                // part (the main document, and each individual header/footer) has its own
                // separate image-relationship id space in the OOXML package, so an image
                // embedded into a footer MUST be added via that footer's own part, not
                // mainPart. Using the wrong part silently produces a relationship id the
                // owning part's XML can't resolve -- Word shows nothing, no error.
                var textContainers = new List<(OpenXmlElement Element, OpenXmlPart Part)> { (body, mainPart) };
                textContainers.AddRange(mainPart.HeaderParts.Select(h => ((OpenXmlElement)h.Header, (OpenXmlPart)h)));
                textContainers.AddRange(mainPart.FooterParts.Select(f => ((OpenXmlElement)f.Footer, (OpenXmlPart)f)));

                foreach (var (container, _) in textContainers)
                {
                    foreach (var placeholder in placeholders)
                        ReplacePlaceholderText(container, "{{" + placeholder.Key + "}}", placeholder.Value);
                }

                InsertContentPlaceholder(body, "{{DocumentContent}}", contentBodyElements);

                // The signature block's template row can live in the body, or (as in the SOP
                // template) in a footer so the full approval matrix repeats on every page --
                // check every text container, not just the body. drawingId is threaded through
                // and incremented for every embedded signature image across all containers --
                // OOXML requires each drawing's non-visual id to be unique document-wide, and a
                // fixed id (as this used to hardcode) corrupts the file once 2+ approvers both
                // have a saved signature.
                uint drawingId = 1;
                foreach (var (container, ownerPart) in textContainers)
                    PopulateSignatureBlock(ownerPart, container, approvers, ref drawingId);

                mainPart.Document.Save();
            }

            return outputStream.ToArray();
        }
        catch(Exception ex)
        {
            throw ex;
        }
    }

    // Replaces a {{placeholder}} token with a value, anywhere it appears within root. Handles
    // a placeholder split across multiple runs (Word does this sometimes, e.g. after spell-
    // check) by collapsing the whole paragraph's text down to a single run once a match is
    // found -- everything else in that paragraph's text (surrounding labels, etc.) is kept,
    // only its per-run formatting is simplified to the first run's formatting. Paragraphs
    // that don't contain the placeholder are left completely untouched.
    private static void ReplacePlaceholderText(OpenXmlElement root, string placeholder, string value)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            var runs = paragraph.Elements<Run>().ToList();
            if (runs.Count == 0) continue;

            string fullText = string.Concat(runs.SelectMany(r => r.Elements<Text>().Select(t => t.Text)));
            if (!fullText.Contains(placeholder)) continue;

            string replaced = fullText.Replace(placeholder, value);

            var firstRun = runs[0];
            var firstText = firstRun.Elements<Text>().FirstOrDefault();
            if (firstText == null)
            {
                firstText = new Text();
                firstRun.AppendChild(firstText);
            }
            firstText.Text = replaced;
            firstText.Space = SpaceProcessingModeValues.Preserve;

            foreach (var extraText in firstRun.Elements<Text>().Skip(1).ToList())
                extraText.Remove();

            for (int i = 1; i < runs.Count; i++)
                runs[i].Remove();
        }
    }

    // Finds the paragraph containing the content placeholder and replaces it with the
    // uploaded file's (already-cloned, detached) body elements, in order.
    private static void InsertContentPlaceholder(Body templateBody, string placeholder, List<OpenXmlElement> contentElements)
    {
        var targetParagraph = templateBody.Descendants<Paragraph>()
            .FirstOrDefault(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)).Contains(placeholder));

        if (targetParagraph == null)
            return; // Template doesn't have a content placeholder -- nothing to insert.

        foreach (var element in contentElements)
            targetParagraph.InsertBeforeSelf(element);

        targetParagraph.Remove();
    }

    // Finds the signature block's template row (the one containing {{ApproverRole}}) within the
    // given container -- body, a header, or a footer -- and clones it once per actual approver
    // on this document's workflow -- as many rows as are actually configured, not a fixed set of
    // roles. Called once per text container (see callers); most templates only have the row in
    // one of them, so this is a no-op for the rest.
    private void PopulateSignatureBlock(OpenXmlPart ownerPart, OpenXmlElement container, List<dynamic> approvers, ref uint drawingId)
    {
        var templateRow = container.Descendants<TableRow>()
            .FirstOrDefault(r => string.Concat(r.Descendants<Text>().Select(t => t.Text)).Contains("{{ApproverRole}}"));

        if (templateRow == null)
            return; // This container doesn't have a signature block row -- nothing to populate.

        if (approvers.Count == 0)
        {
            templateRow.Remove();
            return;
        }

        foreach (var approver in approvers)
        {
            var row = (TableRow)templateRow.CloneNode(true);

            ReplacePlaceholderText(row, "{{ApproverRole}}", Convert.ToString(approver.approverrole) ?? "");
            ReplacePlaceholderText(row, "{{ApproverName}}", Convert.ToString(approver.approvername) ?? "");
            ReplacePlaceholderText(row, "{{ApproverDesignation}}", Convert.ToString(approver.approverdesignation) ?? "");

            bool actioned = approver.decision != null && approver.actionat != null;
            ReplacePlaceholderText(row, "{{ApprovalDate}}", actioned ? FormatMergeDate((object)approver.actionat) : "");

            // Signatures are stored as a file on disk (ESignatureComponent), not as bytes in the
            // database -- read it from wwwroot the same way DMSDocumentController resolves any
            // other uploaded file URL. Missing/unreadable file is treated the same as "no
            // signature on file yet": leave the cell blank rather than failing the whole merge.
            byte[]? signatureData = null;
            string? signatureUrl = approver.signatureurl as string;
            string? signaturePath = null;
            if (!string.IsNullOrWhiteSpace(signatureUrl))
            {
                signaturePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                    signatureUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(signaturePath))
                    signatureData = File.ReadAllBytes(signaturePath);
            }

            if (signatureData != null && signatureData.Length > 0)
            {
                string? approverNameForLog = Convert.ToString(approver.approvername);
                _logger.LogInformation("MergeDocumentTemplateAsync: inserting signature image for approver {ApproverName} from {SignaturePath} ({ByteCount} bytes).",
                    approverNameForLog, signaturePath, signatureData.Length);

                bool inserted = InsertSignatureImage(ownerPart, row, "{{ApproverSignature}}", signatureData, signatureUrl, drawingId++);

                if (inserted)
                    _logger.LogInformation("MergeDocumentTemplateAsync: signature image inserted successfully for approver {ApproverName}.", approverNameForLog);
                else
                    _logger.LogWarning("MergeDocumentTemplateAsync: found signature file {SignaturePath} for approver {ApproverName} but no {{{{ApproverSignature}}}} paragraph was found in this row -- leaving cell as-is.", signaturePath, approverNameForLog);
            }
            else
            {
                bool fileFound = signaturePath != null && File.Exists(signaturePath);
                _logger.LogWarning("MergeDocumentTemplateAsync: no signature image for approver {ApproverName} (RawAssignedUserId={RawAssignedUserId}) -- SignatureURL={SignatureUrl}, ResolvedPath={SignaturePath}, FileExists={FileExists}. Leaving signature cell blank.",
                    (string?)Convert.ToString(approver.approvername), (string?)Convert.ToString(approver.rawassigneduserid), signatureUrl, signaturePath, fileFound);
                ReplacePlaceholderText(row, "{{ApproverSignature}}", ""); // No signature on file yet -- leave blank.
            }

            templateRow.InsertBeforeSelf(row);
        }

        templateRow.Remove();
    }

    // Npgsql maps a Postgres `date` column to System.DateOnly (not DateTime) when read into a
    // dynamic row, and DateOnly doesn't implement IConvertible -- Convert.ToDateTime(object)
    // throws InvalidCastException on it. `timestamp`/`timestamptz` columns still come back as
    // DateTime as usual. Handles both rather than assuming which one a given column actually is.
    private static string FormatMergeDate(object? value) => value switch
    {
        null => "",
        DateOnly d => d.ToString("dd-MMM-yyyy"),
        DateTime dt => dt.ToString("dd-MMM-yyyy"),
        _ => Convert.ToDateTime(value).ToString("dd-MMM-yyyy")
    };

    // Signature images have no FileType column of their own -- the saved file's extension
    // (from ESignatureComponent.SaveSignatureFile) is the source of truth for its format.
    private static string GetSignatureImageContentType(string? signatureUrl) => Path.GetExtension(signatureUrl)?.TrimStart('.').Trim().ToLower() switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "bmp" => "image/bmp",
        "gif" => "image/gif",
        _ => "image/png"
    };

    // Replaces the {{ApproverSignature}} placeholder with an embedded signature image.
    // Returns false if the placeholder wasn't found in this container (nothing was inserted).
    // ownerPart MUST be the specific part (MainDocumentPart, or the exact HeaderPart/FooterPart)
    // that actually owns `container` -- every part has its own separate relationship id space in
    // the OOXML package, so an image added via the wrong part produces a relationship id the
    // owning part's XML can't resolve. Word then renders nothing, with no error at all.
    private static bool InsertSignatureImage(OpenXmlPart ownerPart, OpenXmlElement container, string placeholder, byte[] imageBytes, string? signatureUrl, uint drawingId)
    {
        var paragraph = container.Descendants<Paragraph>()
            .FirstOrDefault(p => string.Concat(p.Descendants<Text>().Select(t => t.Text)).Contains(placeholder));

        if (paragraph == null)
            return false;

        // Can't reuse ReplacePlaceholderText here -- it clears text by walking
        // root.Descendants<Paragraph>(), which is empty when root is already the target
        // paragraph itself (a Paragraph can't contain a nested Paragraph), so it would be a
        // silent no-op and leave the literal "{{ApproverSignature}}" text behind. Clear the
        // paragraph's own runs directly instead, keeping the first run's formatting.
        var existingFormatting = paragraph.Elements<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
        foreach (var oldRun in paragraph.Elements<Run>().ToList())
            oldRun.Remove();

        var run = new Run();
        if (existingFormatting != null)
            run.RunProperties = existingFormatting;
        paragraph.AppendChild(run);

        // Generic AddNewPart<T> (rather than the type-specific AddImagePart(ImagePartType)
        // convenience method) since ownerPart's static type here is the common OpenXmlPart base
        // -- it works identically whether ownerPart is the MainDocumentPart, a HeaderPart, or a
        // FooterPart, all of which derive from OpenXmlPartContainer.
        var imagePart = ownerPart.AddNewPart<ImagePart>(GetSignatureImageContentType(signatureUrl));
        using (var ms = new MemoryStream(imageBytes))
            imagePart.FeedData(ms);

        string relationshipId = ownerPart.GetIdOfPart(imagePart);

        const long emuPerPixelAt96Dpi = 9525;
        const int widthPx = 120;
        const int heightPx = 50;

        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = widthPx * emuPerPixelAt96Dpi, Cy = heightPx * emuPerPixelAt96Dpi },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = drawingId, Name = "Signature" + drawingId },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = drawingId, Name = "Signature" + drawingId },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthPx * emuPerPixelAt96Dpi, Cy = heightPx * emuPerPixelAt96Dpi }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })
                        )
                    )
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            )
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            }
        );

        run.AppendChild(drawing);
        return true;
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

    private async Task ValidateAndSaveTrainingUsersAsync(List<TraningUsers> TrainingUsers, int documentId, dynamic documentInfo, int companyId, string empCode, IDbTransaction transaction)
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
            if (TrainingUsers == null || TrainingUsers.Count == 0)
                throw new CustomException("Training is required for this document type. Please select users for training.", 400);

            // Clear any existing training users (useful in case of rework/resubmission)
            await _common.ExecuteAsync(@"
                DELETE FROM DocumentUserTraining 
                WHERE DocumentId = @DocumentId 
                  AND CompanyId = @CompanyId;",
                new { DocumentId = documentId, CompanyId = companyId }, transaction);

            // Insert new explicitly attached training users
            foreach (var uid in TrainingUsers)
            {
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentUserTraining
                    (CompanyId, DocumentId, EmployeeCode, TrainingMode, TrainingStatus, TrainingProofURL, AssessmentScore, ValidationStatus, ReadyForAuthorization, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                    VALUES
                    (@CompanyId, @DocumentId, @EmployeeCode, @TrainingMode, 0, '', 0, 0, FALSE, TRUE, FALSE, NOW(), @UserId, NOW(), @UserId);",
                new { CompanyId = companyId, DocumentId = documentId, EmployeeCode = uid.EmployeeCode, TrainingMode = uid.TrainingMode, UserId = empCode }, transaction);
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
            // 3️⃣ Promote 1.0 → 1.0
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
                @CompanyId, @DocumentId, '1.0', 1, @Content, @CreatedBy, @LastModifiedBy
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
                        @CompanyId, @DocumentId,
                        (SELECT Id FROM DocumentStates WHERE Code = 'PENDING_APPROVAL'),
                        (SELECT Id FROM DocumentStates WHERE Code = 'APPROVED'),
                        @ExecutionId, @empCode
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

            var approveSnapshotJson = await BuildDocumentSnapshotJson(CompanyId, input.DocumentId, transaction);
            await _auditLogComponent.LogActionAsync(CompanyId, empCode, "Document Approved", "Document",
                input.DocumentId, _clientContextService.GetRequestIpAddress(), newValues: approveSnapshotJson, transaction: transaction);

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
                    @CompanyId, @DocumentId,
                    (SELECT ToStateId FROM DocumentStateHistory WHERE DocumentId = @DocumentId ORDER BY ChangedAt DESC, Id DESC LIMIT 1),
                    (SELECT Id FROM DocumentStates WHERE Code = 'EFFECTIVE'),
                    @UserId
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
            JOIN tblEmployee u ON u.empcode = dut.EmployeeCode AND u.CompanyId = @CompanyId
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
                if (stateId == null)
                    throw new Exception("DocumentStates is missing the 'TRAINING_PENDING' code.");

                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, ChangedAt)
                    VALUES (@CompanyId, @DocumentId,
                        (SELECT ToStateId FROM DocumentStateHistory WHERE DocumentId = @DocumentId ORDER BY ChangedAt DESC, Id DESC LIMIT 1),
                        @ToStateId, @UserId, NOW());",
                    new { CompanyId = companyId, DocumentId = documentId, ToStateId = stateId, UserId = userId }, tx);

                // 3. Create the Parent Training Record. ReadyForAuthorization starts FALSE — it only
                // flips to TRUE once AcknowledgeAndSendForAuthorizationAsync actually runs.
                await _common.ExecuteAsync(@"
                    INSERT INTO DocumentTraining
                    (CompanyId, DocumentId, TrainingMode,TrainingStatus,TrainingProofURL, AssessmentScore, ValidationStatus, ReadyForAuthorization, IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy)
                    VALUES
                    (@CompanyId, @DocumentId, 1, 0,'', 0, 0, FALSE, TRUE, FALSE, NOW(), @UserId, NOW(), @UserId);",
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
                SET Status = 'Rejected',
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
                    @CompanyId, @DocumentId, 2, (SELECT Id FROM DocumentStates WHERE Code = 'REJECTED'), @ExecutionId, @Comments, @empCode
                );",
            new
            {
                CompanyId,
                input.DocumentId,
                input.ExecutionId,
                Comments = input.Observation,
                empCode
            }, transaction);

            var rejectSnapshotJson = await BuildDocumentSnapshotJson(CompanyId, input.DocumentId, transaction);
            await _auditLogComponent.LogActionAsync(CompanyId, empCode, "Document Rejected", "Document",
                input.DocumentId, _clientContextService.GetRequestIpAddress(), newValues: rejectSnapshotJson, transaction: transaction);

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
                SET Status = 'Reworked',
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
                    (SELECT Id FROM DocumentStates WHERE Code = 'DRAFT'),
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

            var reworkSnapshotJson = await BuildDocumentSnapshotJson(CompanyId, input.DocumentId, transaction);
            await _auditLogComponent.LogActionAsync(CompanyId, empCode, "Document Reverted for Rework", "Document",
                input.DocumentId, _clientContextService.GetRequestIpAddress(), newValues: reworkSnapshotJson, transaction: transaction);

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
                    ORDER BY dsh.ChangedAt DESC, dsh.Id DESC
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
                  ORDER BY dsh.ChangedAt DESC, dsh.Id DESC
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
                LEFT JOIN tblEmployee e on LPAD(drd.EmployeeCode::text, 9, '0') = e.empCode AND e.CompanyId = @CompanyId
                INNER JOIN TblEmpJobProfile ejp ON e.empid = ejp.empid AND COALESCE(ejp.active, TRUE) = TRUE AND ejp.CompanyId = @CompanyId
                LEFT JOIN tblsetupsdetail des ON ejp.dsgid = des.sdlid AND des.CompanyId = @CompanyId
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
                stateFilter = input.IsAuthorized ? "IN ('EFFECTIVE', 'AUTHORIZED')" : "IN ('APPROVED', 'AUTHORIZATION_PENDING')";
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
                      ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
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
                "DOCUMENTNUMBER" => "sub.DocumentNumber",
                "TITLE" => "sub.Title",
                "CREATEDAT" => "sub.CreatedAt",
                "CREATEDBY" => "sub.CreatedBy",
                "LASTMODIFIEDAT" => "sub.LastModifiedAt",
                "LASTMODIFIEDBY" => "sub.LastModifiedBy",
                _ => "sub.CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            // Total count is derived from this same query (COUNT(*) OVER(), attached to every
            // returned row) instead of a separate countSql -- countSql's simplified joins don't
            // match dataSql's DISTINCT/join set closely enough to be trusted as the same count.
            // The DISTINCT dedup happens in the inner subquery *before* COUNT(*) OVER() runs in
            // the outer query, so the count reflects the same de-duplicated row set as Items,
            // not the pre-DISTINCT (possibly fanned-out) join result.
            string dataSql = $@"
                SELECT sub.*, COUNT(*) OVER() AS TotalCount
                FROM (
                    SELECT DISTINCT
                        doc.*,
                        dut.TrainingMode,
                        tr.TrainingProofURL,
                        LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' ||COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS Initiator,
                        (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.IsDeleted = FALSE) AS TotalAssigned,
                        (SELECT COUNT(1) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.TrainingStatus = 1 AND dut.IsDeleted = FALSE) AS TotalCompleted,
                        (SELECT COALESCE(AVG(AssessmentScore), 0) FROM DocumentUserTraining dut WHERE dut.DocumentId = doc.Id AND dut.TrainingStatus = 1 AND dut.IsDeleted = FALSE) AS AverageScore,
                        prevdoc.CreatedAt AS PreviousVersionCreatedOn,
                        COALESCE(
                            NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
                            prevdoc.CreatedBy
                        )::character varying AS PreviousVersionCreatedBy

                    FROM VW_Documents doc
                    LEFT JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                    LEFT JOIN (
                            SELECT
                                DocumentId,
                                MAX(TrainingProofURL) AS TrainingProofURL,
                                CASE
                                    -- Check if both IDs (1 and 2) exist for the same document
                                    WHEN COUNT(DISTINCT TrainingMode) > 1
                                         AND SUM(CASE WHEN TrainingMode = 2 THEN 1 ELSE 0 END) > 0
                                         AND SUM(CASE WHEN TrainingMode = 1 THEN 1 ELSE 0 END) > 0
                                         THEN 'Classroom/Online'
                                    -- Map single numeric IDs back to their corresponding text names
                                    WHEN MAX(TrainingMode) = 1 THEN 'Classroom'
                                    WHEN MAX(TrainingMode) = 2 THEN 'Online'
                                    ELSE NULL
                                END AS TrainingMode
                            FROM DocumentUserTraining
                            WHERE IsActive = TRUE
                            GROUP BY DocumentId
                        ) dut ON dut.DocumentId = doc.Id
                    LEFT JOIN tblEmployee e ON CAST(e.empId AS VARCHAR) = doc.CreatedBy  AND e.CompanyId = @CompanyId
                    -- Raw document row, needed for ParentDocumentId (VW_Documents may not expose it)
                    LEFT JOIN Documents rawdoc ON rawdoc.Id = doc.Id AND rawdoc.CompanyId = doc.CompanyId
                    -- The earlier document this one is a revision of (only present for revisions)
                    LEFT JOIN Documents prevdoc ON prevdoc.Id = rawdoc.ParentDocumentId AND prevdoc.CompanyId = doc.CompanyId
                    LEFT JOIN public.tblEmployee prevemp ON LTRIM(RTRIM(prevemp.empcode::text), '0') = LTRIM(RTRIM(prevdoc.CreatedBy::text), '0')
                    {whereClause}
                ) sub
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

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

            int totalCount = 0;
            if (items.Count > 0)
            {
                var firstRow = (IDictionary<string, object>)items[0];
                totalCount = Convert.ToInt32(firstRow["totalcount"]);
            }

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

    public async Task<PendingAuthorizationCountsDto> GetPendingAuthorizationCountsAsync(GetPendingAuthorization input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId 
                  AND doc.IsDeleted = FALSE
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

            // COUNT(DISTINCT doc.Id) FILTER (...), not COUNT(CASE WHEN ... THEN 1 END): the
            // DocumentTraining join below can fan a single document out into multiple rows
            // (more than one active DocumentTraining row for the same DocumentId), and a plain
            // COUNT(CASE WHEN...) counts each of those duplicate rows separately. The list query
            // (GetPendingAuthorizationsAsync) guards against the exact same fan-out with SELECT
            // DISTINCT; COUNT(DISTINCT doc.Id) is the equivalent guard for a bucketed count.
            string sql = $@"
                SELECT
                    COUNT(DISTINCT doc.Id) FILTER (WHERE (
                        SELECT ds.Code
                        FROM DocumentStateHistory dsh
                        JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                        WHERE dsh.DocumentId = doc.Id
                        ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                    ) IN ('APPROVED', 'AUTHORIZATION_PENDING')) AS PendingCount,

                    COUNT(DISTINCT doc.Id) FILTER (WHERE (
                        SELECT ds.Code
                        FROM DocumentStateHistory dsh
                        JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                        WHERE dsh.DocumentId = doc.Id
                        ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                    ) IN ('EFFECTIVE', 'AUTHORIZED')) AS AuthorizedCount,

                    COUNT(DISTINCT doc.Id) FILTER (WHERE (
                        SELECT ds.Code
                        FROM DocumentStateHistory dsh
                        JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                        WHERE dsh.DocumentId = doc.Id
                        ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                    ) IN ('REJECTED')) AS RejectedCount
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

            var result = await _common.QuerySingleAsync<PendingAuthorizationCountsDto>(sql, queryParams);
            return result ?? new PendingAuthorizationCountsDto();
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
                  ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
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
                 ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1) AS CurrentStatus,
                -- Resolve the current workflow authority dynamically
                COALESCE(
                    (SELECT STRING_AGG(
                        COALESCE(LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, ''))), r.Name, des.Name), ', '
                    )
                    FROM WorkflowExecutionSteps wes
                    LEFT JOIN tblEmployee e ON e.empcode = wes.AssignedUserId AND e.CompanyId = doc.CompanyId AND COALESCE(e.Active, 1) = 1
                    LEFT JOIN tblsetupsdetail r ON r.sdlid = wes.AssignedRoleId  AND r.CompanyId = @CompanyId
                    LEFT JOIN tblsetupsdetail des ON des.sdlid = wes.AssignedDesignationId  AND des.CompanyId = @CompanyId
                    WHERE wes.WorkflowExecutionId = we.Id AND wes.IsActive = TRUE),
                    'Pending Training/Authorization'
                ) AS CurrentWorkflowAuthority,
                doc.CreatedAt,
                prevdoc.CreatedAt AS PreviousVersionCreatedOn,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
                    prevdoc.CreatedBy
                )::character varying AS PreviousVersionCreatedBy
            FROM Vw_Documents doc
            LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code AND dt.CompanyId = doc.CompanyId
            LEFT JOIN LATERAL (
                SELECT Version FROM DocumentVersions
                WHERE DocumentId = doc.Id AND CompanyId = doc.CompanyId AND IsActive = TRUE
                ORDER BY CreatedAt DESC LIMIT 1
            ) dv ON TRUE
            LEFT JOIN WorkflowExecutions we ON we.EntityId = doc.Id AND we.CompanyId = doc.CompanyId AND we.EntityType = 'Document' AND we.Status = 'Running'
            -- Raw document row, needed for ParentDocumentId (Vw_Documents may not expose it)
            LEFT JOIN Documents rawdoc ON rawdoc.Id = doc.Id AND rawdoc.CompanyId = doc.CompanyId
            -- The earlier document this one is a revision of (only present for revisions)
            LEFT JOIN Documents prevdoc ON prevdoc.Id = rawdoc.ParentDocumentId AND prevdoc.CompanyId = doc.CompanyId
            LEFT JOIN public.tblEmployee prevemp ON LTRIM(RTRIM(prevemp.empcode::text), '0') = LTRIM(RTRIM(prevdoc.CreatedBy::text), '0')
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
    //                  ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
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
    //                 ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1) AS CurrentStatus,
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

            // Get current state for history. Ties on ChangedAt (multiple transitions can land in the
            // same DB transaction, and NOW() is fixed per-transaction in Postgres) are broken by Id so
            // this always reflects the truly latest transition, not an arbitrary one.
            var currentState = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT dsh.ToStateId, ds.Code
                FROM DocumentStateHistory dsh
                JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                WHERE dsh.DocumentId = @DocumentId
                ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1",
                new { input.DocumentId }, transaction);

            if (currentState == null)
                throw new CustomException("Document has no recorded state.", 404);

            int fromStateId = (int)currentState.tostateid;
            string currentStateCode = (string)currentState.code;

            if (input.Action.Equals("APPROVED", StringComparison.OrdinalIgnoreCase))
            {
                // Final authorization is only valid once the document is actually Authorization
                // Pending, or Approved with no training requirement (so it never went through
                // Authorization Pending to begin with). This is the same "ready" definition
                // GetPendingAuthorizationsAsync already uses for what it shows as pending authorization.
                bool trainingRequired = await _common.ExecuteScalarAsync<bool>(@"
                    SELECT EXISTS(SELECT 1 FROM DocumentTraining WHERE DocumentId = @DocumentId AND CompanyId = @CompanyId AND IsActive = TRUE);",
                    new { input.DocumentId, CompanyId }, transaction);

                bool readyForFinalAuthorization = currentStateCode == "AUTHORIZATION_PENDING"
                    || (!trainingRequired && currentStateCode == "APPROVED");

                if (!readyForFinalAuthorization)
                    throw new CustomException($"Document cannot be authorized from its current state ('{currentStateCode}'). Training must be acknowledged (or not required) first.", 409);

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
                     ORDER BY dsh2.ChangedAt DESC, dsh2.Id DESC LIMIT 1) AS DateOfAuthorization
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
                  AND (tr.ReadyForAuthorization IS NULL OR tr.ReadyForAuthorization = FALSE)
                  AND (
                      SELECT ds.Code
                      FROM DocumentStateHistory dsh
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id
                      ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                  ) = 'TRAINING_PENDING'";

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
                    dut.TrainingMode AS TrainingMode,
                    tr.TrainingProofURL,
                    doc.CreatedAt,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut2 WHERE dut2.DocumentId = doc.Id AND dut2.TrainingMode = @TrainingMode AND dut2.IsDeleted = FALSE) AS TotalAssigned,
                    (SELECT COUNT(1) FROM DocumentUserTraining dut2 WHERE dut2.DocumentId = doc.Id AND dut2.TrainingMode = @TrainingMode AND dut2.TrainingStatus = 1 AND dut2.IsDeleted = FALSE) AS TotalCompleted,
                    (SELECT COALESCE(AVG(AssessmentScore), 0) FROM DocumentUserTraining dut2 WHERE dut2.DocumentId = doc.Id AND dut2.TrainingMode = @TrainingMode AND dut2.TrainingStatus = 1 AND dut2.IsDeleted = FALSE) AS AverageScore,
                    prevdoc.CreatedAt AS PreviousVersionCreatedOn,
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
                        prevdoc.CreatedBy
                    )::character varying AS PreviousVersionCreatedBy
                FROM Vw_Documents doc
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = doc.Id AND dv.IsActive = TRUE
                INNER JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                LEFT JOIN DocumentUserTraining dut ON dut.DocumentId = doc.Id
                -- Raw document row, needed for ParentDocumentId (Vw_Documents may not expose it)
                LEFT JOIN Documents rawdoc ON rawdoc.Id = doc.Id AND rawdoc.CompanyId = doc.CompanyId
                -- The earlier document this one is a revision of (only present for revisions)
                LEFT JOIN Documents prevdoc ON prevdoc.Id = rawdoc.ParentDocumentId AND prevdoc.CompanyId = doc.CompanyId
                LEFT JOIN public.tblEmployee prevemp ON LTRIM(RTRIM(prevemp.empcode::text), '0') = LTRIM(RTRIM(prevdoc.CreatedBy::text), '0')
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

    /// <summary>
    /// Lightweight companion to GetDocumentsPendingTrainingAcknowledgmentAsync (same "list" +
    /// "list-counts" pattern as GetPendingAuthorizationsAsync / GetPendingAuthorizationCountsAsync)
    /// for badge counts (e.g. the sidebar's Training for SOP Documents entry) that only need a
    /// number, not the full paginated dataset with its per-document training-progress joins.
    /// Unlike the list endpoint, this ignores Requeststatus/TrainingMode and counts documents
    /// pending training across both Classroom and Online — a document is deduplicated (counted
    /// once) even if it has assignees in both modes.
    /// </summary>
    public async Task<int> GetDocumentsPendingTrainingCountAsync(GetDocumentsPendingTrainingDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId
                  AND doc.IsDeleted = FALSE
                  AND dut.TrainingMode = @TrainingMode
                  AND (tr.ReadyForAuthorization IS NULL OR tr.ReadyForAuthorization = FALSE)
                  AND (
                      SELECT ds.Code
                      FROM DocumentStateHistory dsh
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id
                      ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                  ) = 'TRAINING_PENDING'";

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

            string countSql = $@"
                SELECT COUNT(DISTINCT doc.Id)
                FROM Vw_Documents doc
                INNER JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                LEFT JOIN DocumentUserTraining dut ON dut.DocumentId = doc.Id
                {whereClause};";

            var queryParams = new
            {
                CompanyId = CompanyId,
                DivisionCode = input.DivisionCode,
                DepartmentCode = input.DepartmentCode,
                SubDepartmentCode = input.SubDepartmentCode,
                BusinessDomainCode = input.BusinessDomainCode,
                DocumentTypeCode = input.DocumentTypeCode,
                TrainingMode = input.Requeststatus == "Classroom" ? 1 : 2
            };

            return await _common.ExecuteScalarAsync<int>(countSql, queryParams);
        }
        catch (Exception)
        {
            throw;
        }
    }

    // Same scope/filters as GetDocumentsPendingTrainingCountAsync, but returns both tabs'
    // counts (plus their total) in one query instead of requiring one call per TrainingMode --
    // for populating both tab badges on the SOP Document Training screen from a single request.
    public async Task<DocumentsPendingTrainingCountsDto> GetDocumentsPendingTrainingCountsAsync(GetDocumentsPendingTrainingDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId
                  AND doc.IsDeleted = FALSE
                  AND (tr.ReadyForAuthorization IS NULL OR tr.ReadyForAuthorization = FALSE)
                  AND (
                      SELECT ds.Code
                      FROM DocumentStateHistory dsh
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id
                      ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                  ) = 'TRAINING_PENDING'";

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

            string countSql = $@"
                SELECT
                    COUNT(DISTINCT doc.Id) FILTER (WHERE dut.TrainingMode = 1) AS ClassroomCount,
                    COUNT(DISTINCT doc.Id) FILTER (WHERE dut.TrainingMode = 2) AS OnlineCount,
                    COUNT(DISTINCT doc.Id) AS TotalCount
                FROM Vw_Documents doc
                INNER JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                LEFT JOIN DocumentUserTraining dut ON dut.DocumentId = doc.Id
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

            var result = await _common.QuerySingleAsync<DocumentsPendingTrainingCountsDto>(countSql, queryParams);
            return result ?? new DocumentsPendingTrainingCountsDto();
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
                      ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
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
                     ORDER BY dsh2.ChangedAt DESC, dsh2.Id DESC LIMIT 1) AS DateOfAuthorization,
                    prevdoc.CreatedAt AS PreviousVersionCreatedOn,
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(COALESCE(prevemp.firstname, '') || ' ' || COALESCE(prevemp.midname, '') || ' ' || COALESCE(prevemp.lastname, ''))), ''),
                        prevdoc.CreatedBy
                    )::character varying AS PreviousVersionCreatedBy
                FROM VW_Documents doc
                LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code AND dt.CompanyId = doc.CompanyId
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = doc.Id AND dv.VersionType = 2 AND dv.IsActive = TRUE
                -- Raw document row, needed for ParentDocumentId (VW_Documents may not expose it)
                LEFT JOIN Documents rawdoc ON rawdoc.Id = doc.Id AND rawdoc.CompanyId = doc.CompanyId
                -- The earlier document this one is a revision of (only present for revisions)
                LEFT JOIN Documents prevdoc ON prevdoc.Id = rawdoc.ParentDocumentId AND prevdoc.CompanyId = doc.CompanyId
                LEFT JOIN public.tblEmployee prevemp ON LTRIM(RTRIM(prevemp.empcode::text), '0') = LTRIM(RTRIM(prevdoc.CreatedBy::text), '0')
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

            // LEFT JOINs to TblEmpJobProfile/tblEmployee resolve each role-distribution row to the
            // employee(s) actually holding that role -- a role held by several people fans out
            // into one row per employee here, and a role nobody currently holds still shows once
            // with blank employee columns (LEFT, not INNER, so a role rule is never silently
            // dropped just because it's unstaffed). This is a display-only resolution for this
            // report; it doesn't touch DocumentUserDistributions or how that table gets populated.
            var roleDistributions = (await _common.QueryAsync<dynamic>(@"
                SELECT drd.DocumentId, r.sdlid AS RoleId, r.Name AS RoleName, div.Name AS Division, dep.Name AS Department,
                       e.empCode AS EmployeeCode,
                       LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName
                FROM DocumentRoleDistributions drd
                LEFT JOIN tblsetupsdetail r ON drd.RoleId = r.sdlid
                LEFT JOIN Divisions div ON drd.DivisionCode = div.Code
                LEFT JOIN Departments dep ON drd.DepartmentCode = dep.Code
                LEFT JOIN TblEmpJobProfile ejp ON ejp.roleid = drd.RoleId AND COALESCE(ejp.Active, TRUE) = TRUE AND ejp.CompanyId = drd.CompanyId
                LEFT JOIN tblEmployee e ON e.empid = ejp.empid AND e.CompanyId = drd.CompanyId AND COALESCE(e.Active, 1) = 1
                WHERE drd.CompanyId = @CompanyId AND drd.DocumentId = ANY(@DocumentIds);",
                new { CompanyId, DocumentIds = documentIds })).ToList();

            var userDistributions = (await _common.QueryAsync<dynamic>(@"
                SELECT dud.DocumentId, e.empCode AS EmployeeCode, 
                       LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, ''))) AS EmployeeName
                FROM DocumentUserDistributions dud
                LEFT JOIN tblEmployee e on LPAD(dud.EmployeeCode::text, 9, '0') = e.empCode  AND e.CompanyId = @CompanyId
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
                var docTypeCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM DocumentTypes WHERE IsDeleted=FALSE AND CompanyId = @CompanyId AND Name = @Name AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = docTypeName }, tx);
                if (string.IsNullOrEmpty(docTypeCode))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Document Type '{docTypeName}' not found. Skipped.");
                    results.Add($"Row {row}: Skipped. Document Type '{docTypeName}' not found.");
                    await tx.RollbackAsync();
                    continue;
                }

                //LogToFile($"[BULK IMPORT] Row {row}: Querying lookup for Division Code where Name='{divName}'");
                var divCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM Divisions WHERE IsDeleted=FALSE AND CompanyId = @CompanyId AND Name = @Name AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = divName }, tx);
                if (string.IsNullOrEmpty(divCode))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Division '{divName}' not found. Skipped.");
                    results.Add($"Row {row}: Skipped. Division '{divName}' not found.");
                    await tx.RollbackAsync();
                    continue;
                }

                //LogToFile($"[BULK IMPORT] Row {row}: Querying lookup for Department Code where Name='{deptName}' and DivCode='{divCode}'");
                var deptCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM Departments WHERE IsDeleted=FALSE AND CompanyId = @CompanyId AND Name = @Name AND DivisionCode = @DivCode AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = deptName, DivCode = divCode }, tx);
                if (string.IsNullOrEmpty(deptCode))
                {
                    //LogToFile($"[BULK IMPORT] Row {row}: Department '{deptName}' not found in Division '{divName}'. Skipped.");
                    results.Add($"Row {row}: Skipped. Department '{deptName}' not found in Division '{divName}'.");
                    await tx.RollbackAsync();
                    continue;
                }

                //LogToFile($"[BULK IMPORT] Row {row}: Querying lookup for SubDepartment Code where Name='{subDeptName}' and DeptCode='{deptCode}'");
                //var subDeptCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM SubDepartments WHERE CompanyId = @CompanyId AND Name = @Name AND DepartmentCode = @DeptCode AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = subDeptName, DeptCode = deptCode }, tx);
                //if (string.IsNullOrEmpty(subDeptCode))
                //{
                //    //LogToFile($"[BULK IMPORT] Row {row}: Sub-Department '{subDeptName}' not found in Department '{deptName}'. Skipped.");
                //    results.Add($"Row {row}: Skipped. Sub-Department '{subDeptName}' not found in Department '{deptName}'.");
                //    await tx.RollbackAsync();
                //    continue;
                //}



                string? subDeptCode = null;
                if (!string.IsNullOrWhiteSpace(subDeptName) &&
                    (subDeptName.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                     subDeptName.Trim().Equals("NA", StringComparison.OrdinalIgnoreCase)))
                {
                    subDeptCode = null;
                }
                else
                {
                    subDeptCode = await _common.ExecuteScalarAsync<string>("SELECT Code FROM SubDepartments WHERE CompanyId = @CompanyId AND Name = @Name AND DepartmentCode = @DeptCode AND IsActive = TRUE LIMIT 1", new { CompanyId, Name = subDeptName, DeptCode = deptCode }, tx);
                    if (string.IsNullOrEmpty(subDeptCode))
                    {
                        results.Add($"Row {row}: Skipped. Sub-Department '{subDeptName}' not found in Department '{deptName}'.");
                        await tx.RollbackAsync();
                        continue;
                    }
                }

                // --- Database Insertion / Update ---

                // Match against the Documents table by Title OR DocumentNumber. If either already
                // exists, reject the row with a warning instead of inserting or silently updating.
                //LogToFile($"[BULK IMPORT] Row {row}: Checking if document with Title='{title}' or DocumentNumber='{docNum}' already exists...");
                var existingDoc = await _common.QueryFirstOrDefaultAsync<dynamic>(
                    @"SELECT Id, DocumentNumber, Title FROM Documents
                      WHERE CompanyId = @CompanyId AND IsDeleted = FALSE
                        AND (Title = @Title OR (@DocumentNumber <> '' AND DocumentNumber = @DocumentNumber))
                      LIMIT 1",
                    new { Title = title, DocumentNumber = docNum, CompanyId }, tx);

                if (existingDoc != null)
                {
                    var docDict = (IDictionary<string, object>)existingDoc;
                    string existingDocNum = docDict["documentnumber"]?.ToString() ?? "";
                    string existingTitle = docDict["title"]?.ToString() ?? "";
                    //LogToFile($"[BULK IMPORT] Row {row}: Existing document found. DocNum: '{existingDocNum}', Title: '{existingTitle}'. Skipping.");

                    results.Add($"Row {row}: Skipped. Document ID '{docNum}' and/or Document Name '{title}' already exists (matches existing Document ID '{existingDocNum}', Document Name '{existingTitle}').");
                    await tx.RollbackAsync();
                    continue;
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
                            @CompanyId, @DocumentNumber, @DocumentTypeCode, @DivisionCode, @DepartmentCode,
                            @SubDepartmentCode, @Title, @NextReviewDate, @ExpectedFileName,
                            TRUE, FALSE, NOW(), @UserId, NOW(), @UserId
                        )
                        RETURNING Id;", new
                    {
                        CompanyId,
                        DocumentNumber = docNum,
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
                    // Log success physically but do not return in skipped/error list
                    LogToFile($"Row {row}: Successfully imported metadata for '{docNum}'.");
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
                        ds.Code AS StateCode,
                        ROW_NUMBER() OVER(PARTITION BY dsh.DocumentId ORDER BY dsh.ChangedAt DESC, dsh.Id DESC) as rn
                    FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.CompanyId = @CompanyId
                )
                SELECT
                    COUNT(1) FILTER (WHERE ls.StateCode = 'DRAFT') AS Draft,
                    COUNT(1) FILTER (WHERE ls.StateCode = 'PENDING_APPROVAL') AS InReview,
                    COUNT(1) FILTER (WHERE ls.StateCode IN ('APPROVED', 'TRAINING_PENDING', 'EFFECTIVE')) AS Approved,
                    COUNT(1) FILTER (WHERE ls.StateCode = 'REJECTED') AS Rejected
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

    // Every Document the current user has ever created, any status -- mirrors
    // DocumentRequestComponent.GetMyTotalRequestsAsync's shape/intent (no RequestStatus filter,
    // CreatedBy-scoped only), so an Initiator can see everything they've created regardless of
    // where it currently sits in the approval/publication pipeline.
    public async Task<PaginationResult<dynamic>> GetMyDocumentsAsync(GetDocumentDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            var whereClause = @"WHERE doc.CompanyId = @CompanyId
                AND doc.CreatedBy = @CreatedBy
                AND doc.IsDeleted = FALSE
                AND (@DivisionCode IS NULL OR @DivisionCode = '' OR doc.DivisionCode = @DivisionCode)
                AND (@DepartmentCode IS NULL OR @DepartmentCode = '' OR doc.DepartmentCode = @DepartmentCode)
                AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '' OR doc.SubDepartmentCode = @SubDepartmentCode)
                AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '' OR doc.BusinessDomainCode = @BusinessDomainCode)
                AND (@DocumentTypeCode IS NULL OR @DocumentTypeCode = '' OR doc.DocumentTypeCode = @DocumentTypeCode)
                -- Exclude Documents still sitting in Draft (DocumentStates.Id = 1) -- this tab
                -- is for tracking submitted work, not in-progress drafts never sent anywhere.
                AND (
                    SELECT ds.Id
                    FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.DocumentId = doc.Id
                    ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                ) <> 1";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(doc.Title) LIKE '%{search}%'
                    OR UPPER(doc.DocumentNumber) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "TITLE" => "doc.Title",
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                _ => "doc.CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            var dataSql = $@"
                SELECT DISTINCT
                    doc.*,
                    dv.Version,
                    (SELECT ds.Name
                     FROM DocumentStateHistory dsh
                     JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                     WHERE dsh.DocumentId = doc.Id
                     ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1) AS CurrentStatus,
                    COALESCE(cn.EmployeeName, doc.CreatedBy) AS CreatedByName,
                    COALESCE(mn.EmployeeName, doc.LastModifiedBy) AS LastModifiedByName
                FROM Vw_Documents doc
                LEFT JOIN LATERAL (
                    SELECT Version FROM DocumentVersions
                    WHERE DocumentId = doc.Id AND CompanyId = doc.CompanyId AND IsActive = TRUE
                    ORDER BY CreatedAt DESC LIMIT 1
                ) dv ON TRUE
                LEFT JOIN Vw_EmployeeNames cn ON cn.CleanEmpCode = LTRIM(doc.CreatedBy::text, '0')
                LEFT JOIN Vw_EmployeeNames mn ON mn.CleanEmpCode = LTRIM(doc.LastModifiedBy::text, '0')
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            var countSql = $@"SELECT COUNT(DISTINCT doc.Id) FROM Vw_Documents doc {whereClause};";

            var queryParams = new
            {
                CompanyId,
                CreatedBy = empCode,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode,
                input.DocumentTypeCode
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
            throw new CustomException("Failed to fetch your documents.", 500);
        }
    }

    // Badge count for the "My Documents" tab -- mirrors GetMyDocumentsAsync's CreatedBy/
    // IsDeleted/non-Draft scope, but always the overall total (not scoped to any
    // cabinet/document-type filters, which only apply to the paginated list view).
    public async Task<int> GetMyDocumentsCountAsync()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            var countSql = @"SELECT COUNT(DISTINCT doc.Id) FROM Vw_Documents doc
                WHERE doc.CompanyId = @CompanyId
                AND doc.CreatedBy = @CreatedBy
                AND doc.IsDeleted = FALSE
                AND (
                    SELECT ds.Id
                    FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.DocumentId = doc.Id
                    ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                ) <> 1;";

            return await _common.ExecuteScalarAsync<int>(countSql, new { CompanyId, CreatedBy = empCode });
        }
        catch (Exception)
        {
            throw new CustomException("Failed to fetch your documents count.", 500);
        }
    }

    // Excel export for the "My Documents" tab (create-update-document) -- mirrors
    // GetMyDocumentsAsync's scope (CreatedBy + IsDeleted = FALSE + non-Draft, same optional
    // cabinet/document-type filters) and the columns my-documents.ts's grid shows, following
    // the same EPPlus pattern as this file's own ExportMyDocumentsAsync below (which exports a
    // different dataset -- the "My Approvals" inbox, not documents the user created).
    public async Task<byte[]> ExportMyDocumentsListAsync(GetDocumentDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            var whereClause = @"WHERE doc.CompanyId = @CompanyId
                AND doc.CreatedBy = @CreatedBy
                AND doc.IsDeleted = FALSE
                AND (@DivisionCode IS NULL OR @DivisionCode = '' OR doc.DivisionCode = @DivisionCode)
                AND (@DepartmentCode IS NULL OR @DepartmentCode = '' OR doc.DepartmentCode = @DepartmentCode)
                AND (@SubDepartmentCode IS NULL OR @SubDepartmentCode = '' OR doc.SubDepartmentCode = @SubDepartmentCode)
                AND (@BusinessDomainCode IS NULL OR @BusinessDomainCode = '' OR doc.BusinessDomainCode = @BusinessDomainCode)
                AND (@DocumentTypeCode IS NULL OR @DocumentTypeCode = '' OR doc.DocumentTypeCode = @DocumentTypeCode)
                AND (
                    SELECT ds.Id
                    FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.DocumentId = doc.Id
                    ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1
                ) <> 1";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(doc.Title) LIKE '%{search}%'
                    OR UPPER(doc.DocumentNumber) LIKE '%{search}%'
                )";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "TITLE" => "doc.Title",
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "CREATEDAT" => "doc.CreatedAt",
                "CREATEDBY" => "doc.CreatedBy",
                "LASTMODIFIEDAT" => "doc.LastModifiedAt",
                "LASTMODIFIEDBY" => "doc.LastModifiedBy",
                _ => "doc.CreatedAt"
            };

            string sortDirection = input.SortBy?.ToUpper() == "ASC" ? "ASC" : "DESC";

            // No DISTINCT here: Postgres rejects SELECT DISTINCT + ORDER BY once the ordered
            // column (CreatedAt) is only exposed in transformed form (TO_CHAR) -- "for SELECT
            // DISTINCT, ORDER BY expressions must appear in select list".
            var dataSql = $@"SELECT
                    doc.DocumentNumber AS ""Document Number"",
                    doc.DocumentType AS ""Document Type"",
                    doc.Title AS ""Document Title"",
                    dv.Version AS ""Version"",
                    doc.Division AS ""Division"",
                    doc.Department AS ""Department"",
                    doc.SubDepartment AS ""Sub-Department"",
                    doc.BusinessDomain AS ""Business Domain"",
                    (SELECT ds.Name
                     FROM DocumentStateHistory dsh
                     JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                     WHERE dsh.DocumentId = doc.Id
                     ORDER BY dsh.ChangedAt DESC, dsh.Id DESC LIMIT 1) AS ""Status"",
                    COALESCE(cn.EmployeeName, doc.CreatedBy) AS ""Created By"",
                    TO_CHAR(doc.CreatedAt, 'Mon DD, YYYY HH24:MI:SS') AS ""Created On"",
                    COALESCE(mn.EmployeeName, doc.LastModifiedBy) AS ""Last Modified By"",
                    TO_CHAR(doc.LastModifiedAt, 'Mon DD, YYYY HH24:MI:SS') AS ""Last Modified On""
                FROM Vw_Documents doc
                LEFT JOIN LATERAL (
                    SELECT Version FROM DocumentVersions
                    WHERE DocumentId = doc.Id AND CompanyId = doc.CompanyId AND IsActive = TRUE
                    ORDER BY CreatedAt DESC LIMIT 1
                ) dv ON TRUE
                LEFT JOIN Vw_EmployeeNames cn ON cn.CleanEmpCode = LTRIM(doc.CreatedBy::text, '0')
                LEFT JOIN Vw_EmployeeNames mn ON mn.CleanEmpCode = LTRIM(doc.LastModifiedBy::text, '0')
                {whereClause}
                ORDER BY {sortColumn} {sortDirection};";

            var queryParams = new
            {
                CompanyId,
                CreatedBy = empCode,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode,
                input.DocumentTypeCode
            };

            var items = await _common.QueryAsync<dynamic>(dataSql, queryParams);

            if (!items.Any())
            {
                return Array.Empty<byte>();
            }

            var headers = ((IDictionary<string, object>)items.First()).Keys.ToList();

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Documents");

            for (int col = 0; col < headers.Count; col++)
            {
                worksheet.Cells[1, col + 1].Value = headers[col];
            }
            using (var headerRange = worksheet.Cells[1, 1, 1, headers.Count])
            {
                headerRange.Style.Font.Bold = true;
            }

            int rowIndex = 2;
            foreach (var row in items)
            {
                var dict = (IDictionary<string, object>)row;
                for (int col = 0; col < headers.Count; col++)
                {
                    worksheet.Cells[rowIndex, col + 1].Value = dict[headers[col]]?.ToString() ?? "";
                }
                rowIndex++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns(10, 60);

            return await package.GetAsByteArrayAsync();
        }
        catch (Exception)
        {
            throw new CustomException("Failed to export data.", 500);
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

            // Limited to the same columns the "My Approvals – Documents" grid shows
            // (leadingColumnDefs/trailingColumnDefs in my-approval-document.ts) instead of every
            // internal field on AllDocumentDto (ExecutionId, StepId, draftFileUrl, etc.) — the
            // export previously dumped 30 raw columns, most of which never appear on screen, and
            // was even missing "Justification", which does.
            var headers = new List<string>
            {
                "Document Type", "Document ID", "Document Name", "Justification", "Company",
                "Proposed Document Number", "Proposed Version Number",
                "Division", "Department", "Sub-Department", "Business Domain",
                "Date of Creation", "Requested By", "Requested On",
                "Previous Version Created By", "Previous Version Created On"
            };

            // Real .xlsx via EPPlus (bold header row, auto-fit column widths) -- see
            // DocumentRequestComponent.ExportMyInboxRequestsAsync for the matching Request-side
            // export and why this replaced a plain-CSV StringBuilder.
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Documents");

            for (int col = 0; col < headers.Count; col++)
            {
                worksheet.Cells[1, col + 1].Value = headers[col];
            }
            using (var headerRange = worksheet.Cells[1, 1, 1, headers.Count])
            {
                headerRange.Style.Font.Bold = true;
            }

            int rowIndex = 2;
            foreach (var row in requests)
            {
                var values = new List<string>
                {
                    row.DocumentType ?? "",
                    row.Id.ToString(),
                    row.Title ?? "",
                    row.Justification ?? "",
                    row.Company ?? "",
                    row.DocumentNumber ?? "",
                    row.ProposedVersionNumber ?? "1.0",
                    row.Division ?? "",
                    row.Department ?? "",
                    row.SubDepartment ?? "",
                    row.BusinessDomain ?? "",
                    FormatExportDate(row.CreatedAt),
                    row.RequestCreatedBy ?? "",
                    FormatExportDate(row.RequestCreatedAt),
                    row.PreviousVersionCreatedBy ?? "",
                    FormatExportDate(row.PreviousVersionCreatedOn),
                };

                for (int col = 0; col < values.Count; col++)
                {
                    worksheet.Cells[rowIndex, col + 1].Value = values[col];
                }
                rowIndex++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns(10, 60);

            return await package.GetAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            // In a real application, you'd log this exception
            throw new CustomException("Failed to export data.", 500);
        }
    }

    // AllDocumentDto's date-like fields are already strings (the underlying SQL function casts
    // timestamps to text), in whatever raw format that cast produced -- reparses and reformats
    // them to "Aug, 02 2026 09:00:00" for the export. Non-date or unparseable values pass through
    // unchanged rather than being blanked out.
    private static string FormatExportDate(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt.ToString("MMM, dd yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        return value ?? "";
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
                  AND d.IsActive = TRUE
                  AND EXISTS (SELECT 1 FROM DocumentStateHistory dsh 
			       JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
			       WHERE dsh.DocumentId = d.Id
			         AND ds.Code IN ('EFFECTIVE', 'AUTHORIZED') 
			     )";

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
                SELECT DISTINCT d.*
                FROM Vw_Documents d
                INNER JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.CompanyId = d.CompanyId AND dv.VersionType = 2 
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            var countSql = $@"
                SELECT COUNT(DISTINCT d.Id)
                FROM Vw_Documents d
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

public class DocumentsPendingTrainingCountsDto
{
    public int ClassroomCount { get; set; }
    public int OnlineCount { get; set; }
    public int TotalCount { get; set; }
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

public class PendingAuthorizationCountsDto
{
    public int PendingCount { get; set; }
    public int AuthorizedCount { get; set; }
    public int RejectedCount { get; set; }
}