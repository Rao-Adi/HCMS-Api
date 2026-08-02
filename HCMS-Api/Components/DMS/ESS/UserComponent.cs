using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.HCMS.ESS;
using System.ComponentModel.Design;
using System.Data;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

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
    }


    public async Task<UserReadDto> CreateAsync(UserCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Users
            WHERE EmployeeCode = '{input.EmployeeCode}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("User already exists", 409);


            // Get Employee Name from Code
            string empQuery = $@"SELECT EmployeeName 
                            FROM Users
                            WHERE EmployeeCode ='{input.ReportingTo}' AND CompanyId = {CompanyId}";
            var employeeName = _common.ExecuteScalarQuery(empQuery);

            // 🔢 Generate next Division Code
            string getLastCodeQuery = @"
                            SELECT EmployeeCode
                            FROM users
                            WHERE EmployeeCode IS NOT NULL AND CompanyId = " + CompanyId + @"
                            ORDER BY Id DESC
                            LIMIT 1";

            var lastCodeObj = _common.ExecuteScalarQuery(getLastCodeQuery);

            int nextNumber = 1;

            if (lastCodeObj != null)
            {
                var lastCode = lastCodeObj.ToString(); // e.g. DIV-0012
                var numericPart = lastCode.Replace("EMP-", "");

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            string generatedCode = $"EMP-{nextNumber:D4}";


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
                BusinessDomainCode,
                DesignationCode,
                ReportingTo,
                Grade,
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
                {CompanyId},
                '{generatedCode}',
                '{input.EmployeeName}',
                '{input.Email}', 
                '{input.DivisionCode}', 
                '{input.DepartmentCode}', 
                '{input.SubDepartmentCode}',
                '{input.BusinessDomainCode}',
                '{input.DesignationCode}',
                '{employeeName}',
                '{input.Grade}',
                '{input.DateOfJoining}',
                TRUE,
                FALSE,
                NOW(),
                '{empCode.Replace("'", "''")}',
                NOW(),
                '{empCode.Replace("'", "''")}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"
             Select * from VW_Users u
                        WHERE u.Id = {newId} AND u.CompanyId = {CompanyId}";

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

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                Designation = row.Field<string>("Designation"),
                DesignationCode = row.Field<string>("DesignationCode"),

                ReportingTo = row.Field<string>("ReportingTo"),
                Grade = row.Field<string>("Grade"),
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM Users
                WHERE Id = {code} AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Users not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Users
                SET IsDeleted = True,
                    IsActive = False,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Id = {code} AND CompanyId = {CompanyId}";

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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = @"
            SELECT EmployeeCode,EmployeeName
                FROM Users
            WHERE IsActive = True AND CompanyId = " + CompanyId + @"
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE u.IsDeleted = False AND u.CompanyId = " + CompanyId + @"
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
                    OR UPPER(u.DesignationCode) LIKE '%{search}%'
                    OR UPPER(u.BusinessDomain) LIKE '%{search}%'
                    OR UPPER(u.Email) LIKE '%{search}%'
                    OR UPPER(u.DateOfJoining) LIKE '%{search}%'
                    OR UPPER(u.ReportingTo) LIKE '%{search}%'
                    OR UPPER(u.Grade) LIKE '%{search}%'
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
                "DesignationCode" => "u.DesignationCode",
                "EMAIL" => "u.Email",
                "DATEOFJOINING" => "u.DateOfJoining",
                "REPORTINGTO" => "u.ReportingTo",
                "GRADE" => "u.Grade",
                "ISACTIVE" => "u.IsActive",
                _ => "u.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        Select * from VW_Users u
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

                    Division = row.Field<string>("Division"),
                    DivisionCode = row.Field<string>("DivisionCode"),

                    Department = row.Field<string>("Department"),
                    DepartmentCode = row.Field<string>("DepartmentCode"),

                    SubDepartment = row.Field<string>("SubDepartment"),
                    SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                    BusinessDomain = row.Field<string>("BusinessDomain"),
                    BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                    Designation = row.Field<string>("Designation"),
                    DesignationCode = row.Field<string>("DesignationCode"),

                    ReportingTo = row.Table.Columns.Contains("ReportingTo") ? row.Field<string>("ReportingTo") : string.Empty,
                    Grade = row.Field<string>("Grade"),
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                   Select * from VW_Users u
                WHERE u.Id = {id} AND u.CompanyId = {CompanyId}
                  AND u.IsActive = True
                  AND u.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Users not found", 404);

            DataRow row = dt.Rows[0];

            return new UserReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                EmployeeName = row.Field<string>("EmployeeName"),
                Email = row.Field<string>("Email"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                Designation = row.Field<string>("Designation"),
                DesignationCode = row.Field<string>("DesignationCode"),

                ReportingTo = row.Field<string>("ReportingTo"),
                Grade = row.Field<string>("Grade"),
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id < 0)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Users
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Users not found", 404);

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
                BusinessDomainCode = '{input.BusinessDomainCode}', 
                DesignationCode = '{input.DesignationCode}', 
                ReportingTo = '{input.ReportingTo}', 
                Grade = '{input.Grade}', 
                DateOfJoining = '{input.DateOfJoining}', 
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                    Select * from VW_Users u
                    WHERE u.Id = {input.Id} AND u.CompanyId = {CompanyId}";

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

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                Designation = row.Field<string>("Designation"),
                DesignationCode = row.Field<string>("DesignationCode"),

                ReportingTo = row.Field<string>("ReportingTo"),
                Grade = row.Field<string>("Grade"),
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


    public async Task<UserReadDto> GetUsersByFiltersAsync(UserFilterDto filters)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var parameters = new DynamicParameters();
            var whereConditions = new List<string>();

            // Base query
            string baseQuery = @"
            SELECT u.*, 
                   div.Name as Division, 
                   dep.Name as Department,
                   sdep.Name as SubDepartment, 
                   c.Name as Company, 
                   bd.Name as BusinessDomain, 
                   des.Name as Designation,
	               r.Name AS UserRole
            FROM Users u
            LEFT JOIN Divisions div ON u.DivisionCode = div.Code
            LEFT JOIN Departments dep ON u.DepartmentCode = dep.Code
            LEFT JOIN SubDepartments sdep ON u.SubDepartmentCode = sdep.Code
            LEFT JOIN Designations des ON u.DesignationCode = des.Code
            LEFT JOIN Companies c ON u.CompanyId = c.Id
            LEFT JOIN BusinessDomains bd ON u.BusinessDomainCode = bd.Code
            LEFT JOIN UserRoles ur
            ON u.Id = ur.UserId
	            LEFT JOIN Roles r
	            ON ur.RoleId = r.Id
            WHERE u.IsActive = TRUE AND u.IsDeleted = FALSE ";

            whereConditions.Add("u.CompanyId = @CompanyId");
            parameters.Add("@CompanyId", filters.CompanyId);


            // Add conditions based on provided filters
            if (!string.IsNullOrEmpty(filters.DivisionCode))
            {
                whereConditions.Add("u.DivisionCode = @DivisionCode");
                parameters.Add("@DivisionCode", filters.DivisionCode);
            }

            if (!string.IsNullOrEmpty(filters.DepartmentCode))
            {
                whereConditions.Add("u.DepartmentCode = @DepartmentCode");
                parameters.Add("@DepartmentCode", filters.DepartmentCode);
            }

            if (!string.IsNullOrEmpty(filters.SubDepartmentCode))
            {
                whereConditions.Add("u.SubDepartmentCode = @SubDepartmentCode");
                parameters.Add("@SubDepartmentCode", filters.SubDepartmentCode);
            }

            if (!string.IsNullOrEmpty(filters.BusinessDomainCode))
            {
                whereConditions.Add("u.BusinessDomainCode = @BusinessDomainCode");
                parameters.Add("@BusinessDomainCode", filters.BusinessDomainCode);
            }

            // Multiple Designations
            if (filters.DesignationCodes != null && filters.DesignationCodes.Any())
            {
                var designationList = filters.DesignationCodes.ToList();
                var designationParams = new List<string>();

                for (int i = 0; i < designationList.Count; i++)
                {
                    var paramName = $"@DesignationCode{i}";
                    designationParams.Add(paramName);
                    parameters.Add(paramName, designationList[i]);
                }

                whereConditions.Add($"u.DesignationCode IN ({string.Join(",", designationParams)})");
            }

            // Multiple Roles
            if (filters.Roles != null && filters.Roles.Any())
            {
                var roleList = filters.Roles.ToList();
                var roleParams = new List<string>();

                for (int i = 0; i < roleList.Count; i++)
                {
                    var paramName = $"@Role{i}";
                    roleParams.Add(paramName);
                    parameters.Add(paramName, roleList[i]);
                }

                whereConditions.Add($"ur.RoleId IN ({string.Join(",", roleParams)})");
            }

             
            // Multiple Employee Codes
            if (filters.EmployeeCodes != null && filters.EmployeeCodes.Any())
            {
                var employeeCodeList = filters.EmployeeCodes.ToList();
                var employeeCodeParams = new List<string>();

                for (int i = 0; i < employeeCodeList.Count; i++)
                {
                    var paramName = $"@EmployeeCode{i}";
                    employeeCodeParams.Add(paramName);
                    parameters.Add(paramName, employeeCodeList[i]);
                }

                whereConditions.Add($"u.EmployeeCode IN ({string.Join(",", employeeCodeParams)})");
            }

            // Combine all conditions
            if (whereConditions.Count > 0)
            {
                baseQuery += " AND " + string.Join(" AND ", whereConditions);
            }

            // Add ORDER BY for consistent results
            baseQuery += " ORDER BY u.EmployeeName";

            var resultList = (await _dapperService.QuerySingleAsync<UserReadDto>(baseQuery, parameters));


            if (resultList == null)
                throw new CustomException("No users found matching the criteria", 200);

            var divisions = new UserReadDto
            {
                Id = resultList.Id,
                CompanyId = resultList.CompanyId,
                Company = resultList.Company,
                EmployeeCode = resultList.EmployeeCode,
                EmployeeName = resultList.EmployeeName,
                Email = resultList.Email,

                Division = resultList.Division,
                DivisionCode = resultList.DivisionCode,

                Department = resultList.Department,
                DepartmentCode = resultList.DepartmentCode,

                SubDepartment = resultList.SubDepartment,
                SubDepartmentCode = resultList.SubDepartmentCode,

                BusinessDomain = resultList.BusinessDomain,
                BusinessDomainCode = resultList.BusinessDomainCode,

                Designation = resultList.Designation,
                DesignationCode = resultList.DesignationCode,

                ReportingTo = resultList.ReportingTo,
                Grade = resultList.Grade,

            };

            return divisions;


        }
        catch (Exception)
        {
            throw;
        }
    }


}


// DTO for filter parameters
public class UserFilterDto
{ 
    public int CompanyId { get; set; }
    public int WorkflowPolicyId { get; set; }
    public string DocumentTypeCode { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? SubDepartmentCode { get; set; }
    public string? BusinessDomainCode { get; set; }
    public List<long>? Roles { get; set; }
    public List<string>? EmployeeCodes { get; set; }
    public List<string>? DesignationCodes { get; set; }
}
