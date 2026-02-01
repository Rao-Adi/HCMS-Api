using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

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
    public DocumentComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        //, ILogger<UtilitiesController> logger
        , IHttpContextAccessor http,
        DMSCommon common
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
        string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        _dataservice.BeginProcess(connectionString);

    }


    public async Task<DocumentReadDto> CreateAsync(DocumentCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);


            if (input.DocumentFile == null || input.DocumentFile.Length == 0)
                throw new CustomException("Document file is required", 400);

            // 2️⃣ Prepare upload path
            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents");

            if (!Directory.Exists(uploadsRoot))
                Directory.CreateDirectory(uploadsRoot);

            // 3️⃣ Create unique filename
            var fileExtension = Path.GetExtension(input.DocumentFile.FileName);
            var fileName = $"{fileExtension}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            // 4️⃣ Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await input.DocumentFile.CopyToAsync(stream);
            }

            // 5️⃣ Generate URL (adjust domain if needed)
            var documentUrl = $"/uploads/documents/{fileName}";
             
            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Documents
            WHERE DocumentName = '{input.DocumentName}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Documents already exists", 409);

            string getLastCodeQuery = @"
                SELECT DocumentNumber
                FROM Documents
                WHERE DocumentNumber IS NOT NULL
                ORDER BY Id DESC
                LIMIT 1";

            var lastCodeObj = _common.ExecuteScalarQuery(getLastCodeQuery);

            int nextNumber = 1;

            if (lastCodeObj != null)
            {
                var lastCode = lastCodeObj.ToString(); // e.g. DIV-0012
                var numericPart = lastCode.Replace("DOC-", "");

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            string generatedCode = $"DOC-{nextNumber:D4}";

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Documents
            (   CompanyId,
                DocumentNumber,
                DocumentTypeCode,
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                BusinessDomainCode,
                DocumentName,
                Version,
                EffectiveFrom, 
                EffectiveTo, 
                NextReviewdate, 
                DocumentURL,
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                '{input.CompanyId}',
                '{generatedCode}',
                '{input.DocumentTypeCode}',
                '{input.DivisionCode}',
                '{input.DepartmentCode}',
                '{input.SubDepartmentCode}', 
                '{input.BusinessDomainCode}', 
                '{input.DocumentName}', 
                '{input.Version}', 
                {(input.EffectiveFrom.HasValue ? $"'{input.EffectiveFrom:yyyy-MM-dd}'" : "NULL")}, 
                {(input.EffectiveTo.HasValue ? $"'{input.EffectiveTo:yyyy-MM-dd}'" : "NULL")}, 
                '{input.NextReviewDate:yyyy-MM-dd}', 
                '{documentUrl}',
                TRUE,
                FALSE,
                NOW(),
                '{userId.Replace("'", "''")}',
                NOW(),
                '{userId.Replace("'", "''")}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
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
            WHERE doc.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

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

                DocumentName = row.Field<string>("DocumentName"),
                Version = row.Field<string>("Version"),
                EffectiveFrom = row.Field<DateTime>("EffectiveFrom").ToString("yyyy-MM-dd HH:mm:ss"),
                EffectiveTo = row.Field<DateTime>("EffectiveTo").ToString("yyyy-MM-dd HH:mm:ss"),
                NextReviewDate = row.Field<DateTime>("NextReviewDate").ToString("yyyy-MM-dd HH:mm:ss"),
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


    public async Task<bool> DeleteAsync(string code)
    {
        try
        {
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
                "DocumentName" => "doc.DocumentName",
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

                    DocumentType = row.Table.Columns.Contains("DocumentTypeName") ? row.Field<string>("DocumentTypeName") : string.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,

                    Division = row.Table.Columns.Contains("DivisionName") ? row.Field<string>("DivisionName") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,

                    Department = row.Table.Columns.Contains("DepartmentName") ? row.Field<string>("DepartmentName") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,

                    SubDepartment = row.Table.Columns.Contains("SubDepartmentName") ? row.Field<string>("SubDepartmentName") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,

                    BusinessDomain = row.Table.Columns.Contains("BusinessDomain") ? row.Field<string>("BusinessDomain") : string.Empty,
                    BusinessDomainCode = row.Table.Columns.Contains("BusinessDomainCode") ? row.Field<string>("BusinessDomainCode") : string.Empty,

                    DocumentName = row.Table.Columns.Contains("DocumentName") ? row.Field<string>("DocumentName") : string.Empty,
                    Version = row.Table.Columns.Contains("Version") ? row.Field<string>("Version") : string.Empty,
                    EffectiveFrom = (row.Table.Columns.Contains("EffectiveFrom") && !row.IsNull("EffectiveFrom"))
                                ? row.Field<DateTime>("EffectiveFrom").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                     
                    EffectiveTo = (row.Table.Columns.Contains("EffectiveTo") && !row.IsNull("EffectiveTo"))
                                ? row.Field<DateTime>("EffectiveTo").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                     
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

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Field<string>("DocumentName"),
                Version = row.Field<string>("Version"),
                EffectiveFrom = row.Field<string>("EffectiveFrom"),
                EffectiveTo = row.Field<string>("EffectiveTo"),
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

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Field<string>("DocumentName"),
                Version = row.Field<string>("Version"),
                EffectiveFrom = row.Field<string>("EffectiveFrom"),
                EffectiveTo = row.Field<string>("EffectiveTo"),
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
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
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
                DocumentName = '{input.DocumentName}',
                Version = '{input.Version}',
                EffectiveFrom = '{input.EffectiveFrom}',
                EffectiveTo = '{input.EffectiveTo}',
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

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentName = row.Field<string>("DocumentName"),
                Version = row.Field<string>("Version"),
                EffectiveFrom = row.Field<string>("EffectiveFrom"),
                EffectiveTo = row.Field<string>("EffectiveTo"),
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

}
