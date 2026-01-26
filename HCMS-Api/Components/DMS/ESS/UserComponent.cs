using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class UserComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public UserComponent(
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


    public async Task<UserReadDto> CreateAsync(UserCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("User Id is required.", 400);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Users
            WHERE (Id = '{input.Id}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("User already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Users
            (   CompanyId,
                EmployeeCode,
                EmployeeName,
                Email, 
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                ReportingTo,
                DateOfJoining, 
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
                '{input.EmployeeCode}',
                '{input.EmployeeName}',
                '{input.Email}', 
                '{input.DivisionCode}', 
                '{input.DepartmentCode}', 
                '{input.SubDepartmentCode}',
                '{input.ReportingTo}',
                '{input.DateOfJoining}',
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
            SELECT u.Id,u.EmployeeCode,u.EmployeeName,u.DivisionCode,u.DepartmentCode,u.SubDepartmentCode,
			   		           u.Email,u.ReportingTo,u.DateOfJoining,u.IsActive,u.IsDeleted,u.CreatedAt,u.CreatedBy,u.LastModifiedAt,u.LastModifiedBy,
					           div.Code as DivisionCode2,div.Name as DivisionName, dep.Code as DepartmentCode2,dep.Name as DepartmentName,
					           sdep.Code as SubDepartmentCode2, sdep.Name SubDepartmentName, c.Id AS CompanyId, c.Name Company
                        FROM Users u
                        LEFT JOIN Divisions div 
						ON u.divisionCode = div.Code
						LEFT JOIN Departments dep
						ON u.DepartmentCode = dep.Code
						LEFT JOIN SubDepartments sdep
						ON u.SubdepartmentCode = sdep.Code
                        LEFT JOIN Company c
                        ON u.CompanyId = c.Id
            WHERE u.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new UserReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                EmployeeName = row.Field<string>("EmployeeName"),
                Email = row.Field<string>("Email"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                ReportingTo = row.Field<string>("ReportingTo"),
                DateOfJoining = row.Field<DateTime>("DateOfJoining").ToString("yyyy-MM-dd HH:mm:ss"),
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
                FROM Users
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Users not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Users
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
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
            SELECT EmployeeCode,EmployeeName
                FROM Users
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY Id";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("EmployeeCode"),
                    Value = row.Field<string>("EmployeeName")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<UserReadDto>> GetAllAsync(TableFiltersDto input)
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
                    OR UPPER(u.EmployeeName) LIKE '%{search}%'
                    OR UPPER(u.DivisionCode) LIKE '%{search}%'
                    OR UPPER(u.DepartmentCode) LIKE '%{search}%'
                    OR UPPER(u.SubDepartmentCode) LIKE '%{search}%'
                    OR UPPER(u.Email) LIKE '%{search}%'
                    OR UPPER(u.DateOfJoining) LIKE '%{search}%'
                    OR UPPER(u.ReportingTo) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "Id" => "u.Id",
                "EMPLOYEECODE" => "u.EmployeeCode",
                "USERNAME" => "u.EmployeeName",
                "DIVISIONCODE" => "u.DivisionCode",
                "DEPARTMENTCODE" => "u.DepartmentCode",
                "SUBDEPARTMENTCODE" => "u.SubDepartmentCode",
                "EMAIL" => "u.Email",
                "DATEOFJOINING" => "u.DateOfJoining",
                "REPORTINGTO" => "u.ReportingTo",
                "ISACTIVE" => "u.IsActive",
                _ => "u.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT u.Id,u.EmployeeCode,u.EmployeeName,u.DivisionCode,u.DepartmentCode,u.SubDepartmentCode,
			   		           u.Email,u.ReportingTo,u.DateOfJoining,u.IsActive,u.IsDeleted,u.CreatedAt,u.CreatedBy,u.LastModifiedAt,u.LastModifiedBy,
					           div.Code as DivisionCode2,div.Name as DivisionName, dep.Code as DepartmentCode2,dep.Name as DepartmentName,
					           sdep.Code as SubDepartmentCode2, sdep.Name SubDepartmentName, c.Id AS CompanyId, c.Name Company
                        FROM Users u
                        LEFT JOIN Divisions div 
						ON u.divisionCode = div.Code
						LEFT JOIN Departments dep
						ON u.DepartmentCode = dep.Code
						LEFT JOIN SubDepartments sdep
						ON u.SubdepartmentCode = sdep.Code
                        LEFT JOIN Company c
                        ON u.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Users u
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<UserReadDto>
                {
                    Items = new List<UserReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new UserReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0, 
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    EmployeeCode = row.Table.Columns.Contains("EmployeeCode") ? row.Field<string>("EmployeeCode") : string.Empty,
                    EmployeeName = row.Table.Columns.Contains("EmployeeName") ? row.Field<string>("EmployeeName") : string.Empty,
                    Email = row.Table.Columns.Contains("Email") ? row.Field<string>("Email") : string.Empty,
                    DivisionName = row.Table.Columns.Contains("DivisionName") ? row.Field<string>("DivisionName") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                    DepartmentName = row.Table.Columns.Contains("DepartmentName") ? row.Field<string>("DepartmentName") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,
                    SubDepartmentName = row.Table.Columns.Contains("SubDepartmentName") ? row.Field<string>("SubDepartmentName") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,
                    ReportingTo = row.Table.Columns.Contains("ReportingTo") ? row.Field<string>("ReportingTo") : string.Empty,
                    DateOfJoining = (row.Table.Columns.Contains("DateOfJoining") && !row.IsNull("DateOfJoining"))
                                ? row.Field<DateTime>("DateOfJoining").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
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

            return new PaginationResult<UserReadDto>
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

    public async Task<UserReadDto> GetByCodeAsync(int id)
    {
        try
        {
            string query = $@"
               SELECT u.Id,u.EmployeeCode,u.EmployeeName,u.DivisionCode,u.DepartmentCode,u.SubDepartmentCode,
			   		           u.Email,u.ReportingTo,u.DateOfJoining,u.IsActive,u.IsDeleted,u.CreatedAt,u.CreatedBy,u.LastModifiedAt,u.LastModifiedBy,
					           div.Code as DivisionCode2,div.Name as DivisionName, dep.Code as DepartmentCode2,dep.Name as DepartmentName,
					           sdep.Code as SubDepartmentCode2, sdep.Name SubDepartmentName, c.Id AS CompanyId, c.Name Company
                        FROM Users u
                        LEFT JOIN Divisions div 
						ON u.divisionCode = div.Code
						LEFT JOIN Departments dep
						ON u.DepartmentCode = dep.Code
						LEFT JOIN SubDepartments sdep
						ON u.SubdepartmentCode = sdep.Code
                        LEFT JOIN Company c
                        ON u.CompanyId = c.Id
                WHERE u.Id = {id}
                  AND u.IsActive = True
                  AND u.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Users not found", 200);

            DataRow row = dt.Rows[0];

            return new UserReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                EmployeeName = row.Field<string>("EmployeeName"),
                Email = row.Field<string>("Email"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                ReportingTo = row.Field<string>("ReportingTo"),
                DateOfJoining = row.Field<DateTime>("DateOfJoining").ToString("yyyy-MM-dd HH:mm:ss"),
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

 

    public async Task<UserReadDto> UpdateAsync(UserUpdateDto input)
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
                EmployeeName = '{input.EmployeeName}',
                Email = '{input.Email}', 
                DivisionCode = '{input.DivisionCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}', 
                ReportingTo = '{input.ReportingTo}', 
                DateOfJoining = '{input.DateOfJoining}', 
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT u.Id,u.EmployeeCode,u.EmployeeName,u.DivisionCode,u.DepartmentCode,u.SubDepartmentCode,
			   		           u.Email,u.ReportingTo,u.DateOfJoining,u.IsActive,u.IsDeleted,u.CreatedAt,u.CreatedBy,u.LastModifiedAt,u.LastModifiedBy,
					           div.Code as DivisionCode2,div.Name as DivisionName, dep.Code as DepartmentCode2,dep.Name as DepartmentName,
					           sdep.Code as SubDepartmentCode2, sdep.Name SubDepartmentName, c.Id AS CompanyId, c.Name Company
                        FROM Users u
                        LEFT JOIN Divisions div 
						ON u.divisionCode = div.Code
						LEFT JOIN Departments dep
						ON u.DepartmentCode = dep.Code
						LEFT JOIN SubDepartments sdep
						ON u.SubdepartmentCode = sdep.Code
                        LEFT JOIN Company c
                        ON u.CompanyId = c.Id
            WHERE u.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new UserReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                EmployeeName = row.Field<string>("EmployeeName"),
                Email = row.Field<string>("Email"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                ReportingTo = row.Field<string>("ReportingTo"),
                DateOfJoining = row.Field<DateTime>("DateOfJoining").ToString("yyyy-MM-dd HH:mm:ss"),
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
