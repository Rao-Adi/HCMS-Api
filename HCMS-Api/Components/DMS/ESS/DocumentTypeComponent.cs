using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentTypeComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DocumentTypeComponent(
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


    public async Task<DocumentTypeReadDto> CreateAsync(DocumentTypeCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("DocumentType code is required.", 200);

            // Check duplicate by Code OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTypes
            WHERE (Code = '{input.Code.Replace("'", "''")}'
                   OR Name = '{input.Name.Replace("'", "''")}')
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DocumentType already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO DocumentTypes
            (
                Code,
                Name,
                description,
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
            SELECT Id, Code, Name, IsActive
            FROM DocumentTypes
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DocumentTypeReadDto
            {
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Description = row.Field<string>("Description"),
                IsActive = row.Field<bool>("IsActive")
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
                FROM DocumentTypes
                WHERE Code = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentType not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentTypes
                SET IsDeleted = False
                WHERE Code = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentTypeReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE IsDeleted = False 
                  AND IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(Name) LIKE '%{search}%'
                    OR UPPER(Code) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "Name",
                "CODE" => "Code",
                "ISACTIVE" => "IsActive",
                _ => "Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.pageNo - 1) * input.pageSize;

            string query = $@"
                        SELECT *
                        FROM DocumentTypes
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.pageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DocumentTypes
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentTypeReadDto>
                {
                    Items = new List<DocumentTypeReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentTypeReadDto
                {
                    Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
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

            return new PaginationResult<DocumentTypeReadDto>
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
            FROM DocumentTypes
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


    public async Task<DocumentTypeReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT Id, Name, Code,Description, IsActive
                FROM DocumentTypes
                WHERE Code = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentType not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentTypeReadDto
            {
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Description = row.Field<string>("Description"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentTypeReadDto> GetByDescriptionAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT Id, Name, Code,Description, IsActive
                FROM DocumentTypes
                WHERE Division = {dCode}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentType not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentTypeReadDto
            {
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Description = row.Field<string>("Description"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentTypeReadDto> UpdateAsync(DocumentTypeUpdateDto input)
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
            FROM DocumentTypes
            WHERE Code = '{input.Code.Replace("'", "''")}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentType not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DocumentTypes
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
            SELECT Code, Name, IsActive
            FROM DocumentTypes
            WHERE Code = '{input.Code.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentTypeReadDto
            {
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }

}
