using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess; 
using HCMS_Api.Components.DMS.Common.Models.Departments;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DepartmentComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DepartmentComponent(
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


    public async Task<DepartmentReadDto> CreateAsync(DepartmentCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // 🔒 Validation
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Department name is required.", 409);

            if (string.IsNullOrWhiteSpace(input.DivisionCode))
                throw new CustomException("Division code is required.", 409);


            // 🔍 Validate parent Division exists
            string divisionCheckQuery = $@"
                        SELECT COUNT(1)
                        FROM Divisions
                        WHERE Code = '{input.DivisionCode.Replace("'", "''")}'
                          AND IsDeleted = FALSE";

            int divisionExists = Convert.ToInt32(_common.ExecuteScalarQuery(divisionCheckQuery));

            if (divisionExists == 0)
                throw new CustomException("Parent Division not found", 404);

            // 🔍 Prevent duplicate department name PER DIVISION
            string duplicateCheckQuery = $@"
                        SELECT COUNT(1)
                        FROM Departments
                        WHERE Name = '{input.Name.Replace("'", "''")}'
                          AND DivisionCode = '{input.DivisionCode.Replace("'", "''")}'
                          AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(duplicateCheckQuery));

            if (exists > 0)
                throw new CustomException("Department already exists in this Division", 409);

            // 🔢 Generate next DPT code PER DIVISION
            string lastCodeQuery = $@"
                        SELECT Code
                        FROM Departments
                        WHERE DivisionCode = '{input.DivisionCode.Replace("'", "''")}'
                          AND Code IS NOT NULL
                        ORDER BY Id DESC
                        LIMIT 1";

            var lastCodeObj = _common.ExecuteScalarQuery(lastCodeQuery);

            int nextNumber = 1;

            if (lastCodeObj != null)
            {
                // Example: DIV-0003-DPT-0004
                var lastCode = lastCodeObj.ToString();
                var numericPart = lastCode.Split("-DPT-").Last();

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            string generatedCode = $"{input.DivisionCode}-DPT-{nextNumber:D4}";

            // Insert (PostgreSQL syntax)
            // 🧾 Insert
            string insertQuery = $@"
                    INSERT INTO Departments
                    (
                        CompanyId,
                        Code,
                        Name,
                        DivisionCode,
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
                        '{input.Name.Replace("'", "''")}',
                        '{input.DivisionCode.Replace("'", "''")}',
                        TRUE,
                        FALSE,
                        NOW(),
                        '{empCode.Replace("'", "''")}',
                        NOW(),
                        '{empCode.Replace("'", "''")}'
                    )
                    RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // 📥 Fetch inserted record
            string selectQuery = $@" 
                SELECT dep.*,c.Name As Company,  div.Name AS Division,
                -- 🔹 Audit Fields
                 COALESCE(e.EmployeeName, dep.CreatedBy::text) AS CreatedByName,
 
                 COALESCE(m.EmployeeName, dep.LastModifiedBy::text) AS LastModifiedByName
 
                    FROM Departments dep
                    LEFT JOIN Divisions div
				                ON dep.DivisionCode = div.Code
                    LEFT JOIN Companies c
                    ON dep.CompanyId = c.Id
 
                  -- 🔹 Created By Employee
                LEFT JOIN Vw_EmployeeNames e
                    ON e.CleanEmpCode = LTRIM(dep.CreatedBy::text, '0')

                -- 🔹 Last Modified By Employee
                LEFT JOIN Vw_EmployeeNames m 
                    ON m.CleanEmpCode = LTRIM(dep.LastModifiedBy::text, '0')
                    WHERE dep.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created department");

            DataRow row = dt.Rows[0];

            return new DepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Division = row.Table.Columns.Contains("Division") ? row.Field<string>("Division") : string.Empty,
                DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy"),
                CreatedByName = row.Field<string>("CreatedByName"),
                LastModifiedByName = row.Field<string>("LastModifiedByName")
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
                FROM Departments
                WHERE Code = '{code}'
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Department not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Departments
                SET IsDeleted = TRUE,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Code = '{code}'";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DepartmentReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE dep.IsDeleted = False 
                  AND dep.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(dep.Name) LIKE '%{search}%'
                    OR UPPER(dep.Code) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "dep.Name",
                "CODE" => "dep.Code",
                "ISACTIVE" => "dep.IsActive",
                _ => "dep.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT dep.*,c.Name As Company, div.Name AS Division,
                        -- 🔹 Audit Fields
                         COALESCE(e.EmployeeName, dep.CreatedBy::text) AS CreatedByName,
 
                         COALESCE(m.EmployeeName, dep.LastModifiedBy::text) AS LastModifiedByName
                        -- 🔹 Audit Fields
                          FROM Departments dep
                            LEFT JOIN Divisions div
				                        ON dep.DivisionCode = div.Code
                            LEFT JOIN Companies c
                            ON dep.CompanyId = c.Id
 
                          -- 🔹 Created By Employee
                        LEFT JOIN Vw_EmployeeNames e
                            ON e.CleanEmpCode = LTRIM(dep.CreatedBy::text, '0')

                        -- 🔹 Last Modified By Employee
                        LEFT JOIN Vw_EmployeeNames m 
                            ON m.CleanEmpCode = LTRIM(dep.LastModifiedBy::text, '0')
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Departments dep
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DepartmentReadDto>
                {
                    Items = new List<DepartmentReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DepartmentReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                    Division = row.Table.Columns.Contains("Division") ? row.Field<string>("Division") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                    IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                    IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                    LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                    CreatedByName = row.Field<string>("CreatedByName"),
                    LastModifiedByName = row.Field<string>("LastModifiedByName")
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<DepartmentReadDto>
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
            SELECT Code, Name
            FROM Departments
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


    public async Task<DepartmentReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@" SELECT dep.*,c.Name As Company,
                    -- 🔹 Audit Fields
                     COALESCE(e.EmployeeName, dep.CreatedBy::text) AS CreatedByName,
 
                     COALESCE(m.EmployeeName, dep.LastModifiedBy::text) AS LastModifiedByName
 
                        FROM Departments dep
                        LEFT JOIN Divisions div
				                    ON dep.DivisionCode = div.Code
                        LEFT JOIN Companies c
                        ON dep.CompanyId = c.Id
 
                      -- 🔹 Created By Employee
                    LEFT JOIN Vw_EmployeeNames e
                        ON e.CleanEmpCode = LTRIM(dep.CreatedBy::text, '0')

                    -- 🔹 Last Modified By Employee
                    LEFT JOIN Vw_EmployeeNames m 
                        ON m.CleanEmpCode = LTRIM(dep.LastModifiedBy::text, '0')
 
                WHERE dep.Code = '{code}'
                  AND dep.IsActive = True
                  AND dep.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Department not found", 200);

            DataRow row = dt.Rows[0];

            return new DepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Division = row.Table.Columns.Contains("Division") ? row.Field<string>("Division") : string.Empty,
                DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy"),
                CreatedByName = row.Field<string>("CreatedByName"),
                LastModifiedByName = row.Field<string>("LastModifiedByName")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<List<DepartmentReadDto>> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                 SELECT dep.*,c.Name As Company,  div.Name AS Division,
                    -- 🔹 Audit Fields
                     COALESCE(e.EmployeeName, dep.CreatedBy::text) AS CreatedByName,
 
                     COALESCE(m.EmployeeName, dep.LastModifiedBy::text) AS LastModifiedByName
 
                        FROM Departments dep
                        LEFT JOIN Divisions div
				                    ON dep.DivisionCode = div.Code
                        LEFT JOIN Companies c
                        ON dep.CompanyId = c.Id
 
                      -- 🔹 Created By Employee
                    LEFT JOIN Vw_EmployeeNames e
                        ON e.CleanEmpCode = LTRIM(dep.CreatedBy::text, '0')

                    -- 🔹 Last Modified By Employee
                    LEFT JOIN Vw_EmployeeNames m 
                        ON m.CleanEmpCode = LTRIM(dep.LastModifiedBy::text, '0')
 
                WHERE dep.DivisionCode = '{dCode}'
                  AND dep.IsActive = True
                  AND dep.IsDeleted = False";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable departmentTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[0];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (departmentTable == null || departmentTable.Rows.Count == 0)
            {
                throw new CustomException("SubDepartment not found", 200);
            }

            var departments = departmentTable.AsEnumerable()
                .Select(row => new DepartmentReadDto
                {
                    Id = row.Field<int>("Id"),
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Code = row.Field<string>("Code"),
                    Name = row.Field<string>("Name"),
                    Division = row.Table.Columns.Contains("Division") ? row.Field<string>("Division") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                    IsDeleted = row.Field<bool>("IsDeleted"),
                    IsActive = row.Field<bool>("IsActive"),
                    CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                    CreatedBy = row.Field<string>("CreatedBy"),
                    LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                    LastModifiedBy = row.Field<string>("LastModifiedBy"),
                    CreatedByName = row.Field<string>("CreatedByName"),
                    LastModifiedByName = row.Field<string>("LastModifiedByName")
                }).ToList();

            return departments;
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DepartmentReadDto> UpdateAsync(DepartmentUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // 🔒 Mandatory validations
            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("Department code is required.", 200);

            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Department name is required.", 200);

            if (string.IsNullOrWhiteSpace(input.DivisionCode))
                throw new CustomException("Division code is required.", 200);

            // 🔍 Check department exists
            string departmentExistsQuery = $@"
                    SELECT COUNT(1)
                    FROM Departments
                    WHERE Code = '{input.Code.Replace("'", "''")}'
                      AND IsDeleted = FALSE";

            int departmentExists =
                Convert.ToInt32(_common.ExecuteScalarQuery(departmentExistsQuery));

            if (departmentExists == 0)
                throw new CustomException("Department not found", 200);

            // 🔍 Validate parent Division exists
            string divisionExistsQuery = $@"
                    SELECT COUNT(1)
                    FROM Divisions
                    WHERE Code = '{input.DivisionCode.Replace("'", "''")}'
                      AND IsDeleted = FALSE";

            int divisionExists =
                Convert.ToInt32(_common.ExecuteScalarQuery(divisionExistsQuery));

            if (divisionExists == 0)
                throw new CustomException("Parent Division not found", 200);

            // 🚫 Prevent duplicate Department Name PER Division
            string duplicateNameQuery = $@"
                    SELECT COUNT(1)
                    FROM Departments
                    WHERE Name = '{input.Name.Replace("'", "''")}'
                      AND DivisionCode = '{input.DivisionCode.Replace("'", "''")}'
                      AND Code <> '{input.Code.Replace("'", "''")}'
                      AND IsDeleted = FALSE";

            int duplicate =
                Convert.ToInt32(_common.ExecuteScalarQuery(duplicateNameQuery));

            if (duplicate > 0)
                throw new CustomException(
                    "Department name already exists in this Division", 200);

            // ✏️ Update ONLY mutable fields
            string updateQuery = $@"
                UPDATE Departments
                SET
                    Name = '{input.Name.Replace("'", "''")}',
                    DivisionCode = '{input.DivisionCode.Replace("'", "''")}',
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Code = '{input.Code.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // 📥 Fetch updated record
            string selectQuery = $@"
                   SELECT dep.*,c.Name As Company,  div.Name AS Division,
                    -- 🔹 Audit Fields
                     COALESCE(e.EmployeeName, dep.CreatedBy::text) AS CreatedByName,
 
                     COALESCE(m.EmployeeName, dep.LastModifiedBy::text) AS LastModifiedByName
 
                        FROM Departments dep
                        LEFT JOIN Divisions div
                                    ON dep.DivisionCode = div.Code
                        LEFT JOIN Companies c
                        ON dep.CompanyId = c.Id
 
                      -- 🔹 Created By Employee
                    LEFT JOIN Vw_EmployeeNames e
                        ON e.CleanEmpCode = LTRIM(dep.CreatedBy::text, '0')

                    -- 🔹 Last Modified By Employee
                    LEFT JOIN Vw_EmployeeNames m 
                        ON m.CleanEmpCode = LTRIM(dep.LastModifiedBy::text, '0')
                    WHERE dep.Code = '{input.Code.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated department");

            DataRow row = dt.Rows[0];

            return new DepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"), // 🔒 Immutable
                Name = row.Field<string>("Name"),
                Division = row.Table.Columns.Contains("Division") ? row.Field<string>("Division") : string.Empty,
                DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt")
                                .ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt")
                                .ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy"),
                CreatedByName = row.Field<string>("CreatedByName"),
                LastModifiedByName = row.Field<string>("LastModifiedByName")
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
                FROM Departments 
                  WHERE IsDeleted = FALSE";
            int count = Convert.ToInt32(_common.ExecuteScalarQuery(query));
            return count;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
