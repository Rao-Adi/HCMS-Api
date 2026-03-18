using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class UserAccessLevelComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public UserAccessLevelComponent(
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
        //string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        //_dataservice.BeginProcess(connectionString);

    }


    public async Task<UserAccessLevelReadDto> CreateAsync(UserAccessLevelCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("UserAccessLevel Id is required.", 400);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM UserAccessLevels
            WHERE (Id = '{input.Id}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("UserAccessLevel already exists", 409);
             


            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO UserAccessLevels
            (   CompanyId,
                EmployeeCode,  
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                BusinessDomainCode, 
                DocumentTypeCode, 
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                {input.CompanyId}, 
                '{input.EmployeeCode}', 
                '{input.DivisionCode}', 
                '{input.DepartmentCode}', 
                '{input.SubDepartmentCode}',
                '{input.BusinessDomainCode}',
                '{input.DocumentTypeCode}',
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
                SELECT u.*,div.Name as Division, dep.Name as Department,
                     sdep.Name SubDepartment, c.Name Company, dt.Name AS DocumentType, bd.Name AS BusinessDomain
                     FROM UserAccessLevels u 
                     LEFT JOIN Divisions div 
                     ON u.divisionCode = div.Code
                     LEFT JOIN Departments dep
                     ON u.DepartmentCode = dep.Code
                     LEFT JOIN SubDepartments sdep
                     ON u.SubdepartmentCode = sdep.Code 
                     LEFT JOIN BusinessDomains bd
                     ON u.BusinessDomainCode = bd.Code
                     LEFT JOIN Companies c
                     ON u.CompanyId = c.Id
                     LEFT JOIN DocumentTypes dt
                     ON u.DocumentTypeCode = dt.Code
                     WHERE u.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new UserAccessLevelReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"), 

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentType = row.Field<string>("DocumentType"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                 
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


    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM UserAccessLevels
                WHERE Id = {id}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("UserAccessLevels not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE UserAccessLevels
                SET IsDeleted = False
                WHERE Id = {id}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }
     
    public async Task<PaginationResult<UserAccessLevelReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE u.IsDeleted = False 
                  AND u.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(u.EmployeeName) LIKE '%{search}%'
                    OR UPPER(u.Id) LIKE '%{search}%'
                    OR UPPER(u.EmployeeCode) LIKE '%{search}%' 
                    OR UPPER(u.DivisionCode) LIKE '%{search}%'
                    OR UPPER(u.DepartmentCode) LIKE '%{search}%'
                    OR UPPER(u.SubDepartmentCode) LIKE '%{search}%'
                    OR UPPER(u.BusinessDomainCode) LIKE '%{search}%' 
                    OR UPPER(u.DocumentTypeCode) LIKE '%{search}%' 
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "Id" => "u.Id",
                "EMPLOYEECODE" => "u.EmployeeCode",
                "USERNAME" => "u.EmployeeName",
                "level1Id" => "u.DivisionCode",
                "level2Id" => "u.DepartmentCode",
                "level3Id" => "u.SubDepartmentCode",
                "level4Id" => "u.BusinessDomainCode",
                "DocumentTypeCode" => "u.DocumentTypeCode",
                "ISACTIVE" => "u.IsActive",
                _ => "u.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT u.*,div.Name as Division, dep.Name as Department,
                         sdep.Name SubDepartment, c.Name Company, dt.Name AS DocumentType, bd.Name AS BusinessDomain
                         FROM UserAccessLevels u 
                         LEFT JOIN Divisions div 
                         ON u.divisionCode = div.Code
                         LEFT JOIN Departments dep
                         ON u.DepartmentCode = dep.Code
                         LEFT JOIN SubDepartments sdep
                         ON u.SubdepartmentCode = sdep.Code 
                         LEFT JOIN BusinessDomains bd
                         ON u.BusinessDomainCode = bd.Code
                         LEFT JOIN Companies c
                         ON u.CompanyId = c.Id
                         LEFT JOIN DocumentTypes dt
                         ON u.DocumentTypeCode = dt.Code
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM UserAccessLevels u
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<UserAccessLevelReadDto>
                {
                    Items = new List<UserAccessLevelReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new UserAccessLevelReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    EmployeeCode = row.Table.Columns.Contains("EmployeeCode") ? row.Field<string>("EmployeeCode") : string.Empty,
                  
                    Division = row.Field<string>("Division"),
                    DivisionCode = row.Field<string>("DivisionCode"),

                    Department = row.Field<string>("Department"),
                    DepartmentCode = row.Field<string>("DepartmentCode"),

                    SubDepartment = row.Field<string>("SubDepartment"),
                    SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                    BusinessDomain = row.Field<string>("BusinessDomain"),
                    BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                    DocumentType = row.Field<string>("DocumentType"),
                    DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

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

            return new PaginationResult<UserAccessLevelReadDto>
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

    public async Task<UserAccessLevelReadDto> GetByCodeAsync(int id)
    {
        try
        {
            string query = $@"
                   SELECT u.*,div.Name as Division, dep.Name as Department,
                     sdep.Name SubDepartment, c.Name Company, dt.Name AS DocumentType, bd.Name AS BusinessDomain
                     FROM UserAccessLevels u 
                     LEFT JOIN Divisions div 
                     ON u.divisionCode = div.Code
                     LEFT JOIN Departments dep
                     ON u.DepartmentCode = dep.Code
                     LEFT JOIN SubDepartments sdep
                     ON u.SubdepartmentCode = sdep.Code 
                     LEFT JOIN BusinessDomains bd
                     ON u.BusinessDomainCode = bd.Code
                     LEFT JOIN Companies c
                     ON u.CompanyId = c.Id
                     LEFT JOIN DocumentTypes dt
                     ON u.DocumentTypeCode = dt.Code
                WHERE u.Id = {id}
                  AND u.IsActive = True
                  AND u.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("UserAccessLevels not found", 200);

            DataRow row = dt.Rows[0];

            return new UserAccessLevelReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"), 

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentType = row.Field<string>("DocumentType"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

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


    public async Task<UserAccessLevelReadDto> UpdateAsync(UserAccessLevelUpdateDto input)
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
            FROM Users
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Users not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Users
            SET 
                EmployeeCode = '{input.EmployeeCode}',  
                DivisionCode = '{input.DivisionCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}', 
                BusinessDomainCode = '{input.BusinessDomainCode}', 
                DesignationCode = '{input.DesignationCode}', 
                DocumentTypeCode = '{input.DocumentTypeCode}',  
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                       SELECT u.*,div.Name as Division, dep.Name as Department,
                         sdep.Name SubDepartment, c.Name Company, dt.Name AS DocumentType, bd.Name AS BusinessDomain
                         FROM UserAccessLevels u 
                         LEFT JOIN Divisions div 
                         ON u.divisionCode = div.Code
                         LEFT JOIN Departments dep
                         ON u.DepartmentCode = dep.Code
                         LEFT JOIN SubDepartments sdep
                         ON u.SubdepartmentCode = sdep.Code 
                         LEFT JOIN BusinessDomains bd
                         ON u.BusinessDomainCode = bd.Code
                         LEFT JOIN Companies c
                         ON u.CompanyId = c.Id
                         LEFT JOIN DocumentTypes dt
                         ON u.DocumentTypeCode = dt.Code
            WHERE u.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new UserAccessLevelReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"), 

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                DocumentType = row.Field<string>("DocumentType"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),

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
