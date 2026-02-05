using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class SubDepartmentComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public SubDepartmentComponent(
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


    public async Task<SubDepartmentReadDto> CreateAsync(SubDepartmentCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
                                   // 🔒 Validation
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Sub-Department name is required.", 200);

            if (string.IsNullOrWhiteSpace(input.DepartmentCode))
                throw new CustomException("Department code is required.", 200);

            // 🔍 Validate parent Department exists
            string departmentCheckQuery = $@"
                        SELECT COUNT(1)
                        FROM Departments
                        WHERE Code = '{input.DepartmentCode.Replace("'", "''")}'
                          AND IsDeleted = FALSE";

            int departmentExists =
                Convert.ToInt32(_common.ExecuteScalarQuery(departmentCheckQuery));

            if (departmentExists == 0)
                throw new CustomException("Parent Department not found", 200);

            // 🚫 Prevent duplicate Sub-Department name PER Department
            string duplicateCheckQuery = $@"
                        SELECT COUNT(1)
                        FROM SubDepartments
                        WHERE Name = '{input.Name.Replace("'", "''")}'
                          AND DepartmentCode = '{input.DepartmentCode.Replace("'", "''")}'
                          AND IsDeleted = FALSE";

            int exists =
                Convert.ToInt32(_common.ExecuteScalarQuery(duplicateCheckQuery));

            if (exists > 0)
                throw new CustomException(
                    "Sub-Department already exists in this Department", 200);

            // 🔢 Generate next SCT code PER Department
            string lastCodeQuery = $@"
                        SELECT Code
                        FROM SubDepartments
                        WHERE DepartmentCode = '{input.DepartmentCode.Replace("'", "''")}'
                          AND Code IS NOT NULL
                        ORDER BY Id DESC
                        LIMIT 1";

            var lastCodeObj = _common.ExecuteScalarQuery(lastCodeQuery);

            int nextNumber = 1;

            if (lastCodeObj != null)
            {
                // Example: DIV-0003-DPT-0002-SCT-0004
                var lastCode = lastCodeObj.ToString();
                var numericPart = lastCode.Split("-SCT-").Last();

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            string generatedCode =
                $"{input.DepartmentCode}-SCT-{nextNumber:D4}";

            // 🧾 Insert
            string insertQuery = $@"
                        INSERT INTO SubDepartments
                        (   CompanyId,
                            Code,
                            Name,
                            DepartmentCode,
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
                            '{input.Name.Replace("'", "''")}',
                            '{input.DepartmentCode.Replace("'", "''")}',
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
                    SELECT s.*, c.Id AS CompanyId, c.Name AS Company, d.Code As DepartmentCode, d.Name AS Department
                        FROM SubDepartments s
                        LEFT JOIN Departments d
                        ON s.DepartmentCode = d.Code
                        LEFT JOIN Companies c
                        ON s.CompanyId = c.Id
                    WHERE s.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created sub-department");

            DataRow row = dt.Rows[0];

            return new SubDepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"), 
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
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
                FROM SubDepartments
                WHERE Code = '{code}'
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("SubDepartment not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE SubDepartments
                SET IsDeleted = False
                WHERE Code = '{code}'";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<SubDepartmentReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE subd.IsDeleted = False 
                  AND subd.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(subd.Name) LIKE '%{search}%'
                    OR UPPER(subd.Code) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "subd.Name",
                "CODE" => "subd.Code",
                "ISACTIVE" => "subd.IsActive",
                _ => "subd.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT subd.*, c.Id AS CompanyId, c.Name AS Company, d.Code AS DepartmentCode,d.Name AS Department
                        FROM SubDepartments subd
                        LEFT JOIN Departments d
                        ON subd.DepartmentCode = d.Code
                        LEFT JOIN Companies c
                        ON subd.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM SubDepartments subd
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<SubDepartmentReadDto>
                {
                    Items = new List<SubDepartmentReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new SubDepartmentReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                    Department = row.Table.Columns.Contains("Department") ? row.Field<string>("Department") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode1") ? row.Field<string>("DepartmentCode1") : string.Empty,
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

            return new PaginationResult<SubDepartmentReadDto>
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
            FROM SubDepartments
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


    public async Task<SubDepartmentReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT subd.*, c.Id AS CompanyId, c.Name AS Company, d.Code AS DepartmentCode,d.Name AS Department
                        FROM SubDepartments subd
                        LEFT JOIN Departments d
                        ON subd.DepartmentCode = d.Code
                        LEFT JOIN Companies c
                        ON subd.CompanyId = c.Id
                WHERE subd.Code = '{code}'
                  AND subd.IsActive = True
                  AND subd.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("SubDepartment not found", 200);

            DataRow row = dt.Rows[0];

            return new SubDepartmentReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                Department = row.Table.Columns.Contains("Department") ? row.Field<string>("Department") : string.Empty,
                DepartmentCode = row.Table.Columns.Contains("DepartmentCode1") ? row.Field<string>("DepartmentCode1") : string.Empty,
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

    public async Task<List<SubDepartmentReadDto>> GetByDepartmentCodeAsync(string departmentCode)
    {
        try
        {
            string query = $@"
                SELECT subd.*, c.Id AS CompanyId, c.Name AS Company, d.Code AS DepartmentCode,d.Name AS Department
                        FROM SubDepartments subd
                        LEFT JOIN Departments d
                        ON subd.DepartmentCode = d.Code
                        LEFT JOIN Companies c
                        ON subd.CompanyId = c.Id
                WHERE subd.DepartmentCode = '{departmentCode}'
                  AND subd.IsActive = True
                  AND subd.IsDeleted = False";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable subDepartmentTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[0];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (subDepartmentTable == null || subDepartmentTable.Rows.Count == 0)
            {
                throw new CustomException("SubDepartment not found", 200);
            }

            var divisions = subDepartmentTable.AsEnumerable()
                .Select(row => new SubDepartmentReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                    Department = row.Table.Columns.Contains("Department") ? row.Field<string>("Department") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode1") ? row.Field<string>("DepartmentCode1") : string.Empty,
                    IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                    IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                    LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                             ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                }).ToList();

            return divisions;
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<SubDepartmentReadDto> UpdateAsync(SubDepartmentUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
                                   // 🔒 Mandatory validations
            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("Sub-Department code is required.", 200);

            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Sub-Department name is required.", 200);

            if (string.IsNullOrWhiteSpace(input.DepartmentCode))
                throw new CustomException("Department code is required.", 200);

            // 🔍 Check Sub-Department exists
            string subDeptExistsQuery = $@"
                        SELECT COUNT(1)
                        FROM SubDepartments
                        WHERE Code = '{input.Code.Replace("'", "''")}'
                          AND IsDeleted = FALSE";

            int subDeptExists =
                Convert.ToInt32(_common.ExecuteScalarQuery(subDeptExistsQuery));

            if (subDeptExists == 0)
                throw new CustomException("Sub-Department not found", 200);

            // 🔍 Validate parent Department exists
            string departmentExistsQuery = $@"
                        SELECT COUNT(1)
                        FROM Departments
                        WHERE Code = '{input.DepartmentCode.Replace("'", "''")}'
                          AND IsDeleted = FALSE";

            int departmentExists =
                Convert.ToInt32(_common.ExecuteScalarQuery(departmentExistsQuery));

            if (departmentExists == 0)
                throw new CustomException("Parent Department not found", 200);

            // 🚫 Prevent duplicate name PER Department
            string duplicateNameQuery = $@"
                        SELECT COUNT(1)
                        FROM SubDepartments
                        WHERE Name = '{input.Name.Replace("'", "''")}'
                          AND DepartmentCode = '{input.DepartmentCode.Replace("'", "''")}'
                          AND Code <> '{input.Code.Replace("'", "''")}'
                          AND IsDeleted = FALSE";

            int duplicate =
                Convert.ToInt32(_common.ExecuteScalarQuery(duplicateNameQuery));

            if (duplicate > 0)
                throw new CustomException(
                    "Sub-Department name already exists in this Department", 200);

            // ✏️ Update mutable fields ONLY
            string updateQuery = $@"
                    UPDATE SubDepartments
                    SET
                        Name = '{input.Name.Replace("'", "''")}',
                        DepartmentCode = '{input.DepartmentCode.Replace("'", "''")}',
                        IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                        LastModifiedAt = NOW(),
                        LastModifiedBy = '{userId.Replace("'", "''")}'
                    WHERE Code = '{input.Code.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // 📥 Fetch updated record (no incorrect JOINs)
            string selectQuery = $@"
                    SELECT subd.*, c.Id AS CompanyId, c.Name AS Company, d.Code AS DepartmentCode,d.Name AS Department
                        FROM SubDepartments s
                        LEFT JOIN Departments d
                        ON s.DepartmentCode = d.Code
                        LEFT JOIN Companies c
                        ON r.CompanyId = c.Id
                    WHERE s.Code = '{input.Code.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated sub-department");

            DataRow row = dt.Rows[0];

            return new SubDepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"), // 🔒 immutable
                Name = row.Field<string>("Name"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
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

    public async Task<int> GetCount()
    {
        try
        {
            string query = $@"
                SELECT COUNT(1)
                FROM SubDepartments 
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
