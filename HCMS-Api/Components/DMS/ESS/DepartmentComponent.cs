using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
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
        string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        _dataservice.BeginProcess(connectionString);

    }


    public async Task<DepartmentReadDto> CreateAsync(DepartmentCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("Department code is required.", 200);

            // Check duplicate by Code OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Departments
            WHERE (Code = '{input.Code.Replace("'", "''")}'
                   OR Name = '{input.Name.Replace("'", "''")}')
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Department already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Departments
            (
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
                '{input.Code.Replace("'", "''")}',
                '{input.Name.Replace("'", "''")}',
                '{input.DivisionCode}',
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
            SELECT *
            FROM Departments
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                DivisionCode = row.Field<string>("DivisionCode"),
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
                FROM Departments
                WHERE Code = '{code}'
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Department not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Departments
                SET IsDeleted = TRUE
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
                        SELECT *
                        FROM Departments dep
                        LEFT JOIN Divisions div
						ON dep.DivisionCode = div.Code
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
                    Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                    Division = row.Table.Columns.Contains("Name1") ? row.Field<string>("Name1") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("Code1") ? row.Field<string>("Code1") : string.Empty,
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
            string query = $@"
                SELECT *
                FROM Departments
                WHERE Code = '{code}'
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Department not found", 200);

            DataRow row = dt.Rows[0];

            return new DepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                DivisionCode = row.Field<string>("DivisionCode"),
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


    public async Task<List<DepartmentReadDto>> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT *
                FROM Departments
                WHERE DivisionCode = '{dCode}'
                  AND IsActive = True
                  AND IsDeleted = False";

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
                    Code = row.Field<string>("Code"),
                    Name = row.Field<string>("Name"),
                    DivisionCode = row.Field<string>("DivisionCode"),
                    IsDeleted = row.Field<bool>("IsDeleted"),
                    IsActive = row.Field<bool>("IsActive"),
                    CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                    CreatedBy = row.Field<string>("CreatedBy"),
                    LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                    LastModifiedBy = row.Field<string>("LastModifiedBy")
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
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Code is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Departments
            WHERE Code = '{input.Code.Replace("'", "''")}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Department not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Departments
            SET 
                Name = '{input.Name.Replace("'", "''")}',
                DivisionCode = '{input.DivisionCode.Replace("'", "''")}',
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Code = '{input.Code.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT *
                   FROM Departments dep
                   LEFT JOIN Divisions div
				   ON dep.DivisionCode = div.Code
            WHERE dep.Code = '{input.Code.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DepartmentReadDto
            {
                Id = row.Field<int>("Id"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
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
