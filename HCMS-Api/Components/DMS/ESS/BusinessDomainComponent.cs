using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class BusinessDomainComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public BusinessDomainComponent(
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


    public async Task<BusinessDomainReadDto> CreateAsync(BusinessDomainCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Document Type name is required.", 200);


            string normalizedCode = input.Code.Trim().ToUpper();

            //// 🔒 Enforce controlled document types
            //if (!AllowedDocumentTypes.Contains(normalizedCode))
            //    throw new CustomException(
            //        "Invalid Document Type. Allowed values: SOP, POL, FRM", 400);

            // 🔍 Prevent duplicate Code or Name
            string duplicateCheckQuery = $@"
                    SELECT COUNT(1)
                    FROM BusinessDomains
                    WHERE (Code = '{normalizedCode}'
                           OR Name = '{input.Name.Replace("'", "''")}')
                      AND IsDeleted = FALSE";

            int exists =
                Convert.ToInt32(_common.ExecuteScalarQuery(duplicateCheckQuery));

            if (exists > 0)
                throw new CustomException("Document Type already exists", 409);

            // 🔢 Generate next Division Code
            string getLastCodeQuery = @"
                            SELECT Code
                            FROM BusinessDomains
                            WHERE Code IS NOT NULL
                            ORDER BY Id DESC
                            LIMIT 1";

            var lastCodeObj = _common.ExecuteScalarQuery(getLastCodeQuery);

            int nextNumber = 1;

            if (lastCodeObj != null)
            {
                var lastCode = lastCodeObj.ToString(); // e.g. DIV-0012
                var numericPart = lastCode.Replace("BSD-", "");

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            string generatedCode = $"BSD-{nextNumber:D4}";

            // 🧾 Insert (GLOBAL, no hierarchy)
            string insertQuery = $@"
                    INSERT INTO BusinessDomains
                    (
                        CompanyId,
                        SubDepartmentCode,
                        Code,
                        Name,
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
                        '{input.SubDepartmentCode}',
                        '{generatedCode}',
                        '{input.Name.Replace("'", "''")}',
                        TRUE,
                        FALSE,
                        NOW(),
                        '{userId.Replace("'", "''")}',
                        NOW(),
                        '{userId.Replace("'", "''")}'
                    )
                    RETURNING Id;";

            int newId =
                Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // 📥 Fetch inserted record
            string selectQuery = $@"
                    SELECT bd.*, dep.Code AS SubDepartmentCode, dep.Name AS SubDepartment, c.Id AS CompanyId, c.Name AS Company
                        FROM BusinessDomains bd
                        LEFT JOIN SubDepartments dep
                        ON bd.subdepartmentcode = dep.Code
                        LEFT JOIN Companies c
                        ON bd.CompanyId = c.Id
                    WHERE bd.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created Document Type");

            DataRow row = dt.Rows[0];

            return new BusinessDomainReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                IsActive = row.Field<bool>("IsActive"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                CreatedAt = row.Field<DateTime>("CreatedAt")
                                .ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt")
                                .ToString("yyyy-MM-dd HH:mm:ss"),
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
                FROM BusinessDomains
                WHERE Code = '{code}'
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("BusinessDomain not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE BusinessDomains
                SET IsDeleted = False
                WHERE Code = '{code}'";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<BusinessDomainReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE bd.IsDeleted = False 
                  AND bd.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(bd.Name) LIKE '%{search}%'
                    OR UPPER(bd.Code) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "bd.Name",
                "CODE" => "db.Code",
                "ISACTIVE" => "bd.IsActive",
                _ => "bd.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT bd.*, dep.Code AS SubDepartmentCode, dep.Name AS SubDepartment, c.Id AS CompanyId, c.Name AS Company
                        FROM BusinessDomains bd
                        LEFT JOIN SubDepartments dep
                        ON bd.subdepartmentcode = dep.Code
                        LEFT JOIN Companies c
                        ON bd.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM BusinessDomains bd
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable businessDomainTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (businessDomainTable == null || businessDomainTable.Rows.Count == 0)
            {
                return new PaginationResult<BusinessDomainReadDto>
                {
                    Items = new List<BusinessDomainReadDto>(),
                    TotalCount = 0
                };
            }

            var businessDomain = businessDomainTable.AsEnumerable()
                .Select(row => new BusinessDomainReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                    SubDepartment = row.Table.Columns.Contains("SubDepartment") ? row.Field<string>("SubDepartment") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode1") ? row.Field<string>("SubDepartmentCode1") : string.Empty,
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

            return new PaginationResult<BusinessDomainReadDto>
            {
                Items = businessDomain,
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
            SELECT Code, Name
            FROM BusinessDomains
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY Name";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Code"),
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


    public async Task<BusinessDomainReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT bd.*, dep.Code AS SubDepartmentCode, dep.Name AS SubDepartment, c.Id AS CompanyId, c.Name AS Company
                        FROM BusinessDomains bd
                        LEFT JOIN SubDepartments dep
                        ON bd.subdepartmentcode = dep.Code
                        LEFT JOIN Companies c
                        ON bd.CompanyId = c.Id
                WHERE bd.Code = '{code}'
                  AND bd.IsActive = True
                  AND bd.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("BusinessDomain not found", 200);

            DataRow row = dt.Rows[0];

            return new BusinessDomainReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
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


    public async Task<BusinessDomainReadDto> GetBySubDepartmentCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT bd.*, dep.Code AS SubDepartmentCode, dep.Name AS SubDepartment, c.Id AS CompanyId, c.Name AS Company
                        FROM BusinessDomains bd
                        LEFT JOIN SubDepartments dep
                        ON bd.subdepartmentcode = dep.Code
                        LEFT JOIN Companies c
                        ON bd.CompanyId = c.Id
                WHERE bd.SubDepartmentCode = '{dCode}'
                  AND bd.IsActive = True
                  AND bd.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("BusinessDomain not found", 200);

            DataRow row = dt.Rows[0];

            return new BusinessDomainReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
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


    public async Task<BusinessDomainReadDto> UpdateAsync(BusinessDomainUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("Invalid Business Domain code.", 200);

            // Check existence (Code is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM BusinessDomains
            WHERE Code = '{input.Code.Replace("'", "''")}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("BusinessDomain not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE BusinessDomains
            SET 
                Name = '{input.Name.Replace("'", "''")}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Code = '{input.Code.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                        SELECT bd.*, dep.Code AS SubDepartmentCode, dep.Name AS SubDepartment, c.Id AS CompanyId, c.Name AS Company
                        FROM BusinessDomains bd
                        LEFT JOIN SubDepartments dep
                        ON bd.subdepartmentcode = dep.Code
                        LEFT JOIN Companies c
                        ON bd.CompanyId = c.Id
            WHERE Code = '{input.Code.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated Business Domain");

            DataRow row = dt.Rows[0];

            return new BusinessDomainReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
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

    public async Task<int> GetCount()
    {
        try
        {
            string query = $@"
                SELECT COUNT(1)
                FROM BusinessDomains 
                  WHERE IsDeleted = FALSE";
            int count = Convert.ToInt32(_common.ExecuteScalarQuery(query));
            return count;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static readonly HashSet<string> AllowedDocumentTypes =
    new(StringComparer.OrdinalIgnoreCase)
    {
        "SOP",
        "POL",
        "FRM"
    };

}
