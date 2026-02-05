using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class RoleComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public RoleComponent(
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


    public async Task<RoleReadDto> CreateAsync(RoleCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("Role is required.", 400);

            // Check duplicate by Name OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Roles
            WHERE (Name = '{input.Name.Replace("'", "''")}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Role already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Roles
            (   CompanyId,
                Name,
                Description,
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
                '{input.Name.Replace("'", "''")}', 
                '{input.Description}',
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
            SELECT r.*, c.Id AS CompanyId, c.Name AS Company
            FROM Roles r
            LEFT JOIN Companies c
            ON r.CompanyId = c.Id
            WHERE r.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new RoleReadDto
            {
                Name = row.Field<string>("Name"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                Description = row.Field<string>("Description"),
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
                FROM Roles
                WHERE Name = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Role not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Roles
                SET IsDeleted = False
                WHERE Name = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<IQueryable<SelectList2Dto>> GetAllSelectList()
    {
        try
        {
            string query = @"
            SELECT Id, Name
            FROM Roles
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY Id";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("Id"),
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


    public async Task<PaginationResult<RoleReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE r.IsDeleted = False 
                  AND r.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(r.Name) LIKE '%{search}%'
                    OR UPPER(r.Name) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "r.Name",
                "DESCRIPTION" => "r.Description",
                "ISACTIVE" => "r.IsActive",
                _ => "r.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT r.*, c.Id AS CompanyId, c.Name AS Company
                        FROM Roles r
                        LEFT JOIN Companies c
                        ON r.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Roles
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<RoleReadDto>
                {
                    Items = new List<RoleReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new RoleReadDto
                {
                    Id = row.Field<int>("id"),
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Description = row.Table.Columns.Contains("Description") ? row.Field<string>("Description") : string.Empty,
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

            return new PaginationResult<RoleReadDto>
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

    public async Task<RoleReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT r.*, c.Id AS CompanyId, c.Name AS Company
                    FROM Roles r
                    LEFT JOIN Companies c
                    ON r.CompanyId = c.Id
                WHERE r.Name = {code}
                  AND r.IsActive = True
                  AND r.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Role not found", 200);

            DataRow row = dt.Rows[0];

            return new RoleReadDto
            {
                Id = row.Field<int>("Id"),
                Name = row.Field<string>("Name"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                Description = row.Field<string>("Description"),
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

     
    public async Task<RoleReadDto> UpdateAsync(RoleUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Name is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Roles
            WHERE Name = '{input.Name.Replace("'", "''")}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Role not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Roles
            SET 
                Name = '{input.Name.Replace("'", "''")}',
                Description = '{input.Description.Replace("'", "''")}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Name = '{input.Name.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                SELECT r.*, c.Id AS CompanyId, c.Name AS Company
                FROM Roles r
                LEFT JOIN Companies c
                ON r.CompanyId = c.Id
            WHERE r.Name = '{input.Name.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new RoleReadDto
            {
                Id = row.Field<int>("Id"),
                Name = row.Field<string>("Name"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                Description = row.Field<string>("Description"),
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
