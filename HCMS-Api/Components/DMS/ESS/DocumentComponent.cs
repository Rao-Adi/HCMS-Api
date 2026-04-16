using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using Newtonsoft.Json;
using Npgsql;
using System.Data;
using static HCMS_Api.Controllers.EmployeeAuthorityController;

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
        PeoplePartnersComponent peoplePartnersComponent
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
        //string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        //_dataservice.BeginProcess(connectionString);

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
        string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
        var clientIp = _clientContextService.GetClientIP();
        var prefix = _utilities.GetPrefix(clientIp);
        var userId = _utilities.GetUserid(prefix);

        // 2️⃣ Prepare upload path
        var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");
        if (!Directory.Exists(uploadsRoot))
            Directory.CreateDirectory(uploadsRoot);

        // 3️⃣ Create unique filename
        var fileExtension = Path.GetExtension(input.DocumentFile.FileName);
        var fileName = $"{Guid.NewGuid()}{fileExtension}";
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
            string checkDuplicateQuery = @"
                SELECT COUNT(1)
                FROM Documents
                WHERE (Title = @Title OR DocumentNumber = @DocumentNumber)
                  AND IsDeleted = FALSE";

            int exists = await _common.ExecuteScalarAsync<int>(checkDuplicateQuery, new { input.Title, input.DocumentNumber }, tx);

            if (exists > 0)
                throw new CustomException("Document Title or Document Number already exists.", 409);

            // Insert Document
            string insertQuery = @"
                INSERT INTO Documents
                (   CompanyId, DocumentNumber, DocumentTypeCode, DivisionCode, DepartmentCode,
                    SubDepartmentCode, BusinessDomainCode, Title, NextReviewdate, DocumentURL, EffectiveDate,
                    IsActive, IsDeleted, CreatedAt, CreatedBy, LastModifiedAt, LastModifiedBy
                )
                VALUES
                (
                    @CompanyId, @DocumentNumber, @DocumentTypeCode, @DivisionCode, @DepartmentCode,
                    @SubDepartmentCode, @BusinessDomainCode, @Title, @NextReviewDate, @DocumentUrl, NOW(),
                    TRUE, FALSE, NOW(), @UserId, NOW(), @UserId
                )
                RETURNING Id;";

            int newId = await _common.ExecuteScalarAsync<int>(insertQuery, new
            {
                CompanyId,
                input.DocumentNumber,
                input.DocumentTypeCode,
                input.DivisionCode,
                input.DepartmentCode,
                input.SubDepartmentCode,
                input.BusinessDomainCode,
                input.Title,
                input.NextReviewDate,
                DocumentUrl = documentUrl,
                UserId = userId
            }, tx);

            // UC-32: Active Archival - Insert initial effective version
            string versionQuery = @"
                INSERT INTO DocumentVersions
                (CompanyId, DocumentId, Version, VersionType, IsActive, CreatedBy, CreatedAt)
                VALUES (@CompanyId, @DocumentId, @Version, 2, TRUE, @UserId, NOW());"; // VersionType 2 = Effective

            await _common.ExecuteAsync(versionQuery, new
            {
                CompanyId,
                DocumentId = newId,
                Version = string.IsNullOrWhiteSpace(input.Version) ? "1.0" : input.Version,
                UserId = userId
            }, tx);

            // UC-32: Active Archival - Insert State History (State 4 = EFFECTIVE)
            string stateQuery = @"
                INSERT INTO DocumentStateHistory
                (CompanyId, DocumentId, ToStateId, ChangedBy, Comments, ChangedAt)
                VALUES (@CompanyId, @DocumentId, 4, @UserId, 'Legacy Document Uploaded', NOW());";

            await _common.ExecuteAsync(stateQuery, new { CompanyId, DocumentId = newId, UserId = userId }, tx);

            await tx.CommitAsync();

            // Fetch inserted record
            string selectQuery = $@"
            SELECT doc.*, dt.Name AS DocumentTypeName, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain,
                        c.Id AS CompanyId, c.Name AS Company
                        FROM Documents doc
                        LEFT JOIN DocumentTypes dt
                        ON doc.DocumentTypeCode = dt.Code
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c
                        ON doc.CompanyId = c.Id
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

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
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


    public async Task<bool> DeleteAsync(string code)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM Documents
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Documents not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Documents
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE doc.IsDeleted = False 
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
                "ISACTIVE" => "doc.IsActive",
                _ => "doc.DocumentNumber"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT doc.*,dt.Name AS DocumentTypeName, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain,
                        c.Id AS CompanyId, c.Name AS Company
                        FROM Documents doc
                        LEFT JOIN DocumentTypes dt
                        ON doc.DocumentTypeCode = dt.Code
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c
                        ON doc.CompanyId = c.Id
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

                    //DocumentType = row.Table.Columns.Contains("DocumentTypeName") ? row.Field<string>("DocumentTypeName") : string.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

                    Division = row.Table.Columns.Contains("DivisionName") ? row.Field<string>("DivisionName") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,

                    Department = row.Table.Columns.Contains("DepartmentName") ? row.Field<string>("DepartmentName") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,

                    SubDepartment = row.Table.Columns.Contains("SubDepartmentName") ? row.Field<string>("SubDepartmentName") : string.Empty,
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
            string query = $@"
                SELECT doc.*,dt.Name AS DocumentTypeName, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain,
                        c.Id AS CompanyId, c.Name AS Company
                        FROM Documents doc
                        LEFT JOIN DocumentTypes dt
                        ON doc.DocumentTypeCode = dt.Code
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c
                        ON doc.CompanyId = c.Id
                WHERE doc.DocumentNumber = {documentNumber}
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
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
            string query = $@"
                SELECT doc.*,dt.Name AS DocumentTypeName, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain,
                        c.Id AS CompanyId, c.Name AS Company
                        FROM Documents doc
                        LEFT JOIN DocumentTypes dt
                        ON doc.DocumentTypeCode = dt.Code
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c
                        ON doc.CompanyId = c.Id
                WHERE doc.Id = {id}
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
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


    public async Task<DocumentReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT doc.*,dt.Name AS DocumentTypeName, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain,
                        c.Id AS CompanyId, c.Name AS Company
                        FROM Documents doc
                        LEFT JOIN DocumentTypes dt
                        ON doc.DocumentTypeCode = dt.Code
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c
                        ON doc.CompanyId = c.Id
                WHERE doc.DivisionCode = {dCode}
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Documents not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
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


    public async Task<DocumentReadDto> UpdateAsync(DocumentUpdateDto input)
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
            FROM Documents
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Documents not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Documents
            SET 
                DocumentNumber = '{input.DocumentNumber}',
                DocumentTypeCode = '{input.DocumentTypeCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}',
                BusinessDomainCode = '{input.BusinessDomainCode}',
                Title = '{input.Title}',
                Version = '{input.Version}',
                NextReviewDate = '{input.NextReviewDate}',
                DocumentURL = '{input.DocumentFile}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                    SELECT doc.*,dt.Name AS DocumentTypeName, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain,
                        c.Id AS CompanyId, c.Name AS Company
                        FROM Documents doc
                        LEFT JOIN DocumentTypes dt
                        ON doc.DocumentTypeCode = dt.Code
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c
                        ON doc.CompanyId = c.Id
            WHERE doc.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentReadDto
            {
                Id = row.Field<int>("Id"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentNumber = row.Field<string>("DocumentNumber"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
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
        catch
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
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

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
                throw new Exception("Document not found.");


            //-------------------------------------------------
            // Validate & Save Attributes (NEW METHOD)
            //-------------------------------------------------

            await ValidateAndSaveAttributesAsync(input, doc, transaction);

            //-------------------------------------------------
            // 2️⃣ Resolve Correct Workflow Policy
            //-------------------------------------------------

            var policyId = await _common.ExecuteScalarAsync<long?>(@"
                SELECT Id
                FROM WorkflowPolicies
                WHERE CompanyId = @CompanyId
                AND EntityType = 'Document'
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
                DocType = doc.documenttypecode,
                DivisionCode = doc.divisioncode,
                DepartmentCode = doc.departmentcode,
                SubDepartmentCode = doc.subdepartmentcode,
                BusinessDomainCode = doc.businessdomaincode
            }, transaction);

            if (policyId == null)
                throw new Exception("No workflow policy defined for selected Cabinet Scope.");

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
                throw new Exception("Workflow policy version not found.");

            //-------------------------------------------------
            // 4️⃣ Promote Version (Rework Case)
            //-------------------------------------------------

            await PromoteVersionAfterReworkAsync(CompanyId, input.DocumentId, userId);

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
                    @CompanyId, @VersionId, 'Document', @DocumentId, 'Running', @UserId
                )
                RETURNING Id;",
            new
            {
                CompanyId,
                VersionId = versionId,
                input.DocumentId,
                userId
            }, transaction);

            //-------------------------------------------------
            // 6️⃣ Snapshot Workflow Steps
            //-------------------------------------------------

            var inserted = await _common.ExecuteAsync(@"
                INSERT INTO WorkflowExecutionSteps
                (
                    CompanyId, WorkflowExecutionId, StepDefinitionId, AssignedUserId, AssignedRoleId,
                    StepOrder, Observation, IsActive
                )
                SELECT
                    @CompanyId, @ExecutionId, Id, UserId, RoleId,
                    StepOrder, '', FALSE
                FROM WorkflowStepDefinitions
                WHERE WorkflowPolicyVersionId = @VersionId;",
            new
            {
                CompanyId,
                ExecutionId = executionId,
                VersionId = versionId
            }, transaction);

            if (inserted < 1)
                throw new Exception("Workflow misconfigured — no steps copied.");

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
                    @CompanyId, @DocumentId, 1, 2, @ExecutionId, @UserId
                );",
            new
            {
                CompanyId,
                input.DocumentId,
                ExecutionId = executionId,
                userId
            }, transaction);

            // Prepare notification data
            string? firstStepUserId = null;
            string? docTitle = null;
            string? docVersion = null;
            var firstStepInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT wes.AssignedUserId, d.Title, dv.Version
                FROM WorkflowExecutionSteps wes
                JOIN WorkflowExecutions we ON we.Id = wes.WorkflowExecutionId
                JOIN Documents d ON d.Id = we.EntityId
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.IsActive = TRUE
                WHERE wes.WorkflowExecutionId = @ExecutionId AND wes.IsActive = TRUE
                ORDER BY dv.CreatedAt DESC LIMIT 1;", new { ExecutionId = executionId }, transaction);

            if (firstStepInfo != null && firstStepInfo!.assigneduserid != null)
            {
                firstStepUserId = firstStepInfo!.assigneduserid;
                docTitle = Convert.ToString(firstStepInfo.title);
                docVersion = Convert.ToString(firstStepInfo.version);
            }

            await transaction.CommitAsync();

            if (firstStepUserId != string.Empty)
            {
                var placeholders = new Dictionary<string, string> { { "Doc Name", docTitle ?? "Unknown" }, { "V#", docVersion ?? "1.0" } };
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PendingDocumentApproval, CompanyId, input.DocumentId, firstStepUserId, placeholders);
            }

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
        var userId = _utilities.GetUserid(prefix);
        int CompanyId = int.Parse(_CompanyId);

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
                     @UserId)
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
                    userId
                }, transaction);
            }
        }
    }

    public async Task PromoteVersionAfterReworkAsync(int companyId, int documentId, string userId)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);

            //-----------------------------------------
            // 1️⃣ Check Last State Was Rework Draft
            //-----------------------------------------
            var wasReworked = await _common.QuerySingleAsync<int>(@"
                SELECT COUNT(*)
                FROM DocumentStateHistory
                WHERE CompanyId = @CompanyId
                  AND DocumentId = @DocumentId
                  AND ToStateId = 1
                  AND WorkflowExecutionId IS NOT NULL
                ", new { companyId, documentId });

            if (wasReworked == 0)
                return;

            //-----------------------------------------
            // 2️⃣ Get Latest Version
            //-----------------------------------------
            var current = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT *
                FROM DocumentVersions
                WHERE CompanyId = @CompanyId
                  AND DocumentId = @DocumentId
                  AND Content IS NOT NULL
                  AND IsActive = TRUE
                ORDER BY CreatedAt DESC
                LIMIT 1",
            new { companyId, documentId });

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
                    CompanyId, DocumentId, Version, VersionType, Content,
                    ChangeDescription, CreatedBy
                )
                VALUES
                (
                    @CompanyId, @DocumentId, @Version, 1, @Content,
                    'Version promoted after rework', @UserId
                )
            ", new
            {
                companyId,
                documentId,
                Version = newVersion,
                Content = current.Content,
                userId
            });
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
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var empDetail = await _peoplePartnersComponent.GetAllEmployeeByEmpIdAsync(input.EmpId);
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

            //var notifyPlaceholders = new Dictionary<string, string> { { "Doc Name", Convert.ToString(docInfo?.title) ?? "Unknown" }, { "V#", Convert.ToString(docInfo?.version) ?? "1.0" } };

            string approverName = empDetail?.firstname + " " + empDetail?.midname + " " + empDetail?.lastname;

            var notifyPlaceholders = new Dictionary<string, string>
            {
                { "ID", Convert.ToString(docInfo?.requestnumber) ?? "Unknown" },
                { "Approver", approverName },
                { "Observation", input.Observation ?? "" }
            };

            

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

            string? nextStepUserId = null;

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

                if (nextStep.assigneduserid != null)
                    nextStepUserId = nextStep.assigneduserid;
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
                        @CompanyId, @DocumentId, 2, 3, @ExecutionId, @ChangedBy
                    );",
                new
                {
                    CompanyId,
                    input.DocumentId,
                    input.ExecutionId,
                    ChangedBy = userId
                }, transaction);
            }

            await transaction.CommitAsync();

            if (nextStepUserId != string.Empty && docInfo != null)
            {
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentApprovedForwarded, CompanyId, input.DocumentId, nextStepUserId, notifyPlaceholders);
            }

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
    public async Task<bool> MakeDocumentEffectiveAsync(int companyId, int documentId, string userId)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            //var userId = _utilities.GetUserid(prefix);

            //-----------------------------------------
            // Activate Final Version
            //-----------------------------------------
            await _common.ExecuteAsync(@"
                UPDATE DocumentVersions
                SET VersionType = 2
                WHERE CompanyId = @CompanyId
                  AND DocumentId = @DocumentId
                ORDER BY CreatedAt DESC
                LIMIT 1
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
                new { companyId, documentId, userId }, transaction);


            //-----------------------------------------
            // 3️⃣ CREATE TRAINING MATRIX (UC-22)
            //-----------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO DocumentUserTraining
                (
                    CompanyId, DocumentId, EmployeeCode, TrainingMode, TrainingStatus, CreatedBy, LastModifiedBy
                )
                SELECT
                    CompanyId,
                    DocumentId,
                    EmployeeCode,
                    1,   -- e.g. Read & Acknowledge
                    0,   -- Pending
                    @UserId,
                    @UserId
                FROM DocumentUserDistributions
                WHERE CompanyId = @CompanyId
                  AND DocumentId = @DocumentId
                  AND IsActive = TRUE
                  AND IsDeleted = FALSE;
            ", new { companyId, documentId, userId }, transaction);


            await transaction.CommitAsync();

            //-----------------------------------------
            // 4️⃣ Notify Users AFTER COMMIT
            //-----------------------------------------
            await NotifyPendingUsersAsync(companyId, documentId);

            var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT d.Title, dv.Version, d.CreatedBy
                FROM Documents d
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = d.Id AND dv.VersionType = 2
                WHERE d.Id = @DocumentId
                ORDER BY dv.CreatedAt DESC LIMIT 1", new { DocumentId = documentId });

            string initiatorId = "";
            if (docInfo != null && docInfo!.createdby != null)
            {
                //int.TryParse(Convert.ToString(docInfo.createdby), out initiatorId);
                initiatorId = docInfo!.createdby;
            }

            if (initiatorId != string.Empty)
            {
                var notifyPlaceholders = new Dictionary<string, string> { { "Doc Name", Convert.ToString(docInfo.title) ?? "Unknown" }, { "V#", Convert.ToString(docInfo.version) ?? "1.0" }, { "Date", DateTime.Now.ToString("yyyy-MM-dd") } };
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.DocumentAuthorizedEffective, companyId, documentId, initiatorId, notifyPlaceholders);
            }

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task NotifyPendingUsersAsync(int companyId, int documentId)
    {
        var clientIp = _clientContextService.GetClientIP();
        var prefix = _utilities.GetPrefix(clientIp);
        var userId = _utilities.GetUserid(prefix);

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
            ", new { companyId, documentId });

        foreach (var user in users)
        {
            //-----------------------------------------
            // 1️⃣ INSERT PORTAL NOTIFICATION
            //-----------------------------------------

            await _common.ExecuteAsync(@"
                INSERT INTO Notifications
                (
                    CompanyId, UserId, Title, Message, NotificationType, RelatedEntityType, RelatedEntityId
                )
                VALUES
                (
                    @CompanyId, @UserId, @Title, @Message, @Type, 'Document', @DocumentId
                );",
            new
            {
                CompanyId = companyId,
                UserId = user.UserId,
                Title = "New SOP Training Assigned",
                Message = $"Training is required for Document: {user.Title}",
                Type = 2, // e.g. Training Notification
                DocumentId = documentId
            });

            //-----------------------------------------
            // 2️⃣ SEND EMAIL
            //-----------------------------------------

            //await _emailService.SendAsync(
            //    user.Email,
            //    "New SOP Training Assigned",
            //    $@"
            //A new effective document requires your training.

            //Document: {user.Title}

            //Please login to DMS portal and complete the required training.

            //Regards,
            //DMS Team
            //");
        }
    }


    public async Task<bool> CompleteDocumentTrainingAsync(CompleteDocumentTrainingDto dto)
    {
        await using var tx = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

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

    private async Task HandlePostApprovalAsync(int companyId, int documentId, string userId)
    {

        /*
         Excellent — this is the last critical orchestration point in your Document Lifecycle.

            Because:

            “Approved” ≠ “Effective”
            in Enterprise DMS

            🧠 FIRST — WHAT DOES "Effective" MEAN?

            A document becomes Effective when:

            It is legally enforceable inside the organization
            and employees must follow it

            Auditor language:

            “From what date was SOP-001 mandatory for staff?”

            That date = Effective Date

            So:

            Approved = Management accepted content
            Effective = Organization must comply
            🟩 WHEN SHOULD MakeDocumentEffectiveAsync() BE CALLED?

            That depends on your:

            DocumentStateTransitions
            RequiresTraining
            RequiresAuthorization

            You already have this table 👇
            (Ref: 

            currenttables_for_gpt

            )

            🎯 USE THIS RULE ENGINE

            After:

            ApproveDocumentAsync()

            DO NOT immediately call:

            MakeDocumentEffectiveAsync()

            Instead:

            STEP 1 — CHECK TRANSITION RULE

            After inserting:

            PendingApproval → Approved

            Run:

            SELECT 
                RequiresTraining,
                RequiresAuthorization
            FROM DocumentStateTransitions
            WHERE FromStateId = 3 -- Approved
            🟦 SCENARIO A — NO TRAINING, NO AUTHORIZATION
            RequiresTraining = FALSE
            RequiresAuthorization = FALSE

            👉 Call internally:

            await MakeDocumentEffectiveAsync(companyId, documentId, userId);

            Because document is ready for enforcement.

            🟨 SCENARIO B — TRAINING REQUIRED
            RequiresTraining = TRUE

            DO NOT call Effective yet ❌

            Instead:

            Insert:

            Approved → TrainingPending

            Create:

            DocumentTraining
            TrainingStatus = Pending

            Now wait for:

            Training Completion Event

            🔁 AFTER TRAINING COMPLETED

            Call internally:

            MakeDocumentEffectiveAsync()
            🟧 SCENARIO C — AUTHORIZATION REQUIRED
            RequiresAuthorization = TRUE

            DO NOT call Effective ❌

            Instead:

            Insert:

            Approved → AuthorizationPending

            Start Authorization Workflow

            Wait for:

            Authorization Workflow Approval

            🔁 AFTER AUTHORIZATION APPROVED

            Call internally:

            MakeDocumentEffectiveAsync()
            🟥 SCENARIO D — BOTH REQUIRED
            RequiresTraining = TRUE
            RequiresAuthorization = TRUE

            Sequence becomes:

            Approved
               ↓
            TrainingPending
               ↓
            TrainingCompleted
               ↓
            AuthorizationPending
               ↓
            AuthorizationApproved
               ↓
            Effective  ← NOW call MakeDocumentEffectiveAsync()
            🚀 WHERE EXACTLY TO CALL IT?

            Never from Controller ❌
            Always from:

            Event	Call MakeEffective?
            ApproveDocumentAsync	Maybe
            TrainingCompletedAsync	Maybe
            AuthorizationApprovedAsync	Yes
            🧩 CLEAN ORCHESTRATION

            Inside:

            ApproveDocumentAsync()

            Add:

            await HandlePostApprovalAsync(companyId, documentId, userId);



            🏁 FINAL ANSWER

            Use:

            MakeDocumentEffectiveAsync()

            ONLY when:

            ✔ Document Approved
            ✔ Training Completed (if required)
            ✔ Authorization Completed (if required)

            Then:

            Approved / AuthorizationApproved → Effective

            That is when document becomes legally active.

         */

        var clientIp = _clientContextService.GetClientIP();
        var prefix = _utilities.GetPrefix(clientIp);
        //var userId = _utilities.GetUserid(prefix);

        var rule = await _common.QuerySingleAsync<dynamic>(@"
            SELECT RequiresTraining, RequiresAuthorization
            FROM DocumentStateTransitions
            WHERE FromStateId = 3
            LIMIT 1
            ");

        if (!rule.RequiresTraining && !rule.RequiresAuthorization)
        {
            await MakeDocumentEffectiveAsync(companyId, documentId, userId);
            return;
        }

        if (rule.RequiresTraining)
        {
            // Move → TrainingPending
            // Insert DocumentTraining
            return;
        }

        if (rule.RequiresAuthorization)
        {
            // Move → AuthorizationPending
            // Start Workflow
            return;
        }
    }

    public async Task<bool> RejectDocumentAsync(ActionOnDocument input)
    {
        await using var transaction = await _common.BeginTransactionAsync();

        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var empDetail = await _peoplePartnersComponent.GetAllEmployeeByEmpIdAsync(input.EmpId);

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
                    @CompanyId, @DocumentId, 2, 5, @ExecutionId, @Comments, @UserId
                );",
            new
            {
                CompanyId,
                input.DocumentId,
                input.ExecutionId,
                Comments = input.Observation,
                userId
            }, transaction);

            await transaction.CommitAsync();

            string initiatorId = "";
            if (docInfo != null && docInfo!.createdby != null)
            {
                //int.TryParse(Convert.ToString(docInfo!.createdby), out initiatorId);
                initiatorId = docInfo!.createddby;
            }

            if (initiatorId != string.Empty)
            {
                var notifyPlaceholders = new Dictionary<string, string> {
                    { "Doc Name", Convert.ToString(docInfo.title) ?? "Unknown" }, { "V#", Convert.ToString(docInfo.version) ?? "1.0" },
                    { "Approver", approverName ?? userId.ToString() }, { "Observation", input.Observation ?? "" }
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
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            var empDetail = await _peoplePartnersComponent.GetAllEmployeeByEmpIdAsync(input.EmpId);

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
                SET Decision = 'Rework',
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
                    CompanyId, DocumentId, FromStateId, ToStateId, WorkflowExecutionId, Comments, ChangedBy
                )
                VALUES
                (
                    @CompanyId, @DocumentId, 2, 1, @ExecutionId, @Comments, @UserId
                );",
            new
            {
                CompanyId,
                input.DocumentId,
                input.ExecutionId,
                Comments = input.Observation,
                userId
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
                    { "Approver", approverName ?? userId.ToString() }, { "Observation", input.Observation ?? "" }
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);


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
            , new { CompanyId = CompanyId, RequestId = requestId });

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
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            //get Employee details by Id
            var empDetail = await _peoplePartnersComponent.GetAllEmployeeByEmpIdAsync(input.EmpId);
            if(empDetail == null)
            {
                throw new Exception("Employe dosen't exist.");
            }
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
                UserId = empDetail.empcode,
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
                SELECT *
                FROM DocumentRequestUserDistributions
                WHERE CompanyId = @CompanyId
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
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            // Architecture Note: A document is pending final authorization if it is fully approved,
            // AND (if training is applicable) training has been verified (ReadyForAuthorization = TRUE).
            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId 
                  AND doc.IsDeleted = FALSE
                  AND (
                      SELECT ds.Code 
                      FROM DocumentStateHistory dsh 
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id 
                      ORDER BY dsh.ChangedAt DESC LIMIT 1
                  ) IN ('APPROVED', 'TRAINING_PENDING')
                  AND (
                      tr.Id IS NULL OR tr.ReadyForAuthorization = TRUE
                  )";

            // FSD UC-30 Extension: Filter View for SOP vs Other Documents
            if (!string.IsNullOrWhiteSpace(input.DocumentCategoryFilter))
            {
                if (input.DocumentCategoryFilter.ToUpper() == "SOP")
                {
                    whereClause += " AND UPPER(dt.Code) = 'SOP'";
                }
                else if (input.DocumentCategoryFilter.ToUpper() == "OTHER" || input.DocumentCategoryFilter.ToUpper() == "OTHER DOCUMENT")
                {
                    whereClause += " AND UPPER(dt.Code) != 'SOP'";
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
                _ => "doc.CreatedAt"
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
                    tr.TrainingProofURL,
                    LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' ||COALESCE(e.firstname, '') || ' ' || COALESCE(e.lastname, ''))) AS Initiator,
                    doc.CreatedAt, div.Name AS DivisionName,
                    dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain

                FROM Documents doc 
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c
                        ON doc.CompanyId = c.Id
                LEFT JOIN DocumentTypes dt ON doc.DocumentTypeCode = dt.Code
                LEFT JOIN DocumentVersions dv ON dv.DocumentId = doc.Id AND dv.IsActive = TRUE
                LEFT JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                LEFT JOIN tblEmployee e ON CAST(e.empId AS VARCHAR) = doc.CreatedBy
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(1) 
                FROM Documents doc 
                LEFT JOIN DocumentTraining tr ON tr.DocumentId = doc.Id AND tr.IsActive = TRUE
                {whereClause};";

            var queryParams = new { CompanyId = CompanyId };

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

    public async Task<bool> AuthorizeDocumentPostTrainingAsync(AuthorizeDocumentDto input)
    {
        await using var transaction = await _common.BeginTransactionAsync();
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);


            if (string.IsNullOrWhiteSpace(input.Observation))
                throw new Exception("Observation comment is mandatory for final authorization.");

            // 1. Update Document Effective Date
            // Depending on policy, you might set a future effective date here, 
            // but for immediate enforcement, NOW() is used.
            await _common.ExecuteAsync(@"
                UPDATE Documents 
                SET 
                    EffectiveDate = NOW(), 
                    LastModifiedAt = NOW(), 
                    LastModifiedBy = @UserId 
                WHERE Id = @DocumentId AND CompanyId = @CompanyId;",
                new { input.DocumentId, CompanyId, userId }, transaction);

            // 2. Archive previous effective versions (e.g., VersionType 2 = Effective, 3 = Archived)
            await _common.ExecuteAsync(@"
                UPDATE DocumentVersions 
                SET VersionType = 3, 
                    IsActive = FALSE 
                WHERE DocumentId = @DocumentId 
                  AND VersionType = 2 
                  AND CompanyId = @CompanyId;",
                new { input.DocumentId, CompanyId }, transaction);

            // 3. Mark the current pending version as Effective
            await _common.ExecuteAsync(@"
                UPDATE DocumentVersions 
                SET VersionType = 2 
                WHERE DocumentId = @DocumentId 
                  AND VersionType = 1 
                  AND CompanyId = @CompanyId;",
                new { input.DocumentId, CompanyId }, transaction);

            // 4. Update Document State History to 'EFFECTIVE'
            await _common.ExecuteAsync(@"
                INSERT INTO DocumentStateHistory (CompanyId, DocumentId, FromStateId, ToStateId, ChangedBy, Comments, ChangedAt)
                SELECT @CompanyId, @DocumentId, 
                       (SELECT ToStateId FROM DocumentStateHistory WHERE DocumentId = @DocumentId ORDER BY ChangedAt DESC LIMIT 1),
                       (SELECT Id FROM DocumentStates WHERE Code = 'EFFECTIVE'), 
                       @UserId, @Observation, NOW();",
                new { CompanyId, input.DocumentId, userId, input.Observation }, transaction);

            await transaction.CommitAsync();

            // 5. Trigger DCA Notification (Physical Copy Retrieval / Obsoletion Task)
            // Assuming RoleId for DCA is known or we look it up. Using a placeholder role fetch mechanism.
            var dcaUsers = await _common.QueryAsync<string>(@"SELECT UserId FROM UserRoles r JOIN Roles rl ON r.RoleId = rl.Id WHERE rl.Name = 'DCA' AND r.CompanyId = @CompanyId", new { CompanyId });

            var docInfo = await _common.QueryFirstOrDefaultAsync<dynamic>("SELECT Title FROM Documents WHERE Id = @DocumentId", new { input.DocumentId });
            var placeholders = new Dictionary<string, string> { { "Doc Name", docInfo?.title ?? "Document" }, { "V#", "Latest" } };

            foreach (var dcaUser in dcaUsers)
            {
                await _notificationComponent.TriggerNotificationAsync(NotificationScenario.PhysicalCopyRetrievalTask, CompanyId, input.DocumentId, dcaUser, placeholders);
            }

            return true;
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);

            // UC-31: Fetch historical documents where the *current user* was the one 
            // who transitioned the document to 'EFFECTIVE' or 'AUTHORIZED'
            var whereClause = @"
                WHERE doc.CompanyId = @CompanyId 
                  AND doc.IsDeleted = FALSE
                  AND EXISTS (
                      SELECT 1 
                      FROM DocumentStateHistory dsh 
                      JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                      WHERE dsh.DocumentId = doc.Id 
                        AND ds.Code IN ('EFFECTIVE', 'AUTHORIZED')
                        AND dsh.ChangedBy = @UserId
                  )";

            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@" AND (UPPER(doc.Title) LIKE '%{search}%' OR UPPER(doc.DocumentNumber) LIKE '%{search}%')";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DOCUMENTNUMBER" => "doc.DocumentNumber",
                "TITLE" => "doc.Title",
                "DATEOFAUTHORIZATION" => "DateOfAuthorization",
                "VERSION" => "dv.Version",
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

            var queryParams = new { CompanyId = CompanyId, UserId = input.UserId };

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

}

public class AuthorizeDocumentDto
{
    public int DocumentId { get; set; }
    public string Observation { get; set; }
}

public class GetPendingAuthorization : TableFiltersDto
{
    public string? DocumentCategoryFilter { get; set; }
}

public class GetAuthorizedDocumentsDto : TableFiltersDto
{
    public string UserId { get; set; }
}