using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DistributionListComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DistributionListComponent(
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


    public async Task<DistributionListReadDto> CreateAsync(DistributionListCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("DistributionList code is required.", 200);

            // Check duplicate by Code OR DivisionCode
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DistributionLists
            WHERE (DocumentRequestId = '{input.DocumentRequestId}'
                   OR DivisionCode = '{input.DivisionCode.Replace("'", "''")}')
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DistributionList already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO DistributionLists
            (
                DocumentRequestId,
                DivisionCode,
                DepartmentCode,
                RoleId,
                DistributionType,
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                '{input.DocumentRequestId}',
                '{input.DivisionCode.Replace("'", "''")}',
                '{input.DepartmentCode.Replace("'", "''")}',
                '{input.RoleId}',
                '{input.DistributionType}',
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
            SELECT Id, Code, DivisionCode, IsActive
            FROM DistributionLists
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DistributionListReadDto
            {
                DocumentRequestId = row.Field<Guid>("DocumentRequestId"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                RoleId = row.Field<int>("RoleId"),
                DistributionType = row.Field<int>("DistributionType"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }


    public async Task<bool> DeleteAsync(string GId)
    {
        try
        {
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM DistributionLists
                WHERE Id = {GId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DistributionList not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DistributionLists
                SET IsDeleted = False
                WHERE Id = {GId}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DistributionListReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE dl.IsDeleted = False 
                  AND dl.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(dl.DivisionCode) LIKE '%{search}%'
                    OR UPPER(dep.DepartmentCode) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "dl.DivisionCode",
                "DepartmentCode" => "dep.DepartmentCode",
                "ISACTIVE" => "dl.IsActive",
                _ => "dl.DivisionCode"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT *
                        FROM DistributionLists dl
                        LEFT JOIN Divisions div
						ON dl.DivisionCode = dl.Code
                        LEFT JOIN Department dep
						ON dl.DepartmentCode = dep.Code
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DistributionLists dl
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DistributionListReadDto>
                {
                    Items = new List<DistributionListReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DistributionListReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<Guid>("Id") : Guid.Empty,
                    DocumentRequestId = row.Table.Columns.Contains("DocumentRequestId") ? row.Field<Guid>("DocumentRequestId") : Guid.Empty,
                    DivisionCode = row.Table.Columns.Contains("Code1") ? row.Field<string>("Code1") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,
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

            return new PaginationResult<DistributionListReadDto>
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
            SELECT Code, DivisionCode
            FROM DistributionLists
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY DivisionCode";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Code"),
                    Value = row.Field<string>("DivisionCode")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DistributionListReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT Id, DivisionCode, Code,DivisionCode, IsActive
                FROM DistributionLists
                WHERE Code = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DistributionList not found", 200);

            DataRow row = dt.Rows[0];

            return new DistributionListReadDto
            {
                DocumentRequestId = row.Field<Guid>("DocumentRequestId"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                RoleId = row.Field<int>("RoleId"),
                DistributionType = row.Field<int>("DistributionType"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DistributionListReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT Id, DocumentRequestId,
                DivisionCode,
                DepartmentCode,
                RoleId,
                DistributionType, IsActive
                FROM DistributionLists
                WHERE Division = {dCode}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DistributionList not found", 200);

            DataRow row = dt.Rows[0];

            return new DistributionListReadDto
            {
                DocumentRequestId = row.Field<Guid>("DocumentRequestId"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                RoleId = row.Field<int>("RoleId"),
                DistributionType = row.Field<int>("DistributionType"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DistributionListReadDto> UpdateAsync(DistributionListUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Code is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DistributionLists
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DistributionList not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DistributionLists
            SET 
                DivisionCode = '{input.DivisionCode.Replace("'", "''")}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT DocumentRequestId,
                   DivisionCode,
                   DepartmentCode,
                   RoleId,
                   DistributionType, IsActive
            FROM DistributionLists
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DistributionListReadDto
            {
                DocumentRequestId = row.Field<Guid>("DocumentRequestId"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                RoleId = row.Field<int>("RoleId"),
                DistributionType = row.Field<int>("DistributionType"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }

}
