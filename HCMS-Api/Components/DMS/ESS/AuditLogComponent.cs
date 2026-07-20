using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class AuditLogComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public AuditLogComponent(
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


    public async Task<AuditLogReadDto> CreateAsync(AuditLogCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check duplicate by EmployeeCode OR Action
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM AuditLogs
            WHERE (EmployeeCode = '{input.EmployeeCode}'
                   OR Action = '{input.Action.Replace("'", "''")}')
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("AuditLog already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO AuditLogs
            (
                CompanyId,
                EmployeeCode,
                Action,
                EntityType,
                EntityTid,
                OldValues,
                NewValues,
                Timestamp,
                ipaddress 
            )
            VALUES
            (
                '{CompanyId}',
                '{input.EmployeeCode}',
                '{input.Action.Replace("'", "''")}',
                '{input.EntityId}',
                '{input.EntityType}',
                '{input.OldValues}',
                '{input.NewValues}', 
                NOW(),
                '{input.IPAddress}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"
            SELECT *
            FROM AuditLogs
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new AuditLogReadDto
            {
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                Action = row.Field<string>("Action"),
                EntityType = row.Field<string>("EntityType"),
                EntityId = row.Field<int>("EntityId"),
                OldValues = row.Field<string>("OldValues"),
                NewValues = row.Field<string>("NewValues"),
                Timestamp = row.Field<DateTime>("Timestamp"),
                IPAddress = row.Field<string>("IPAddress")
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
                FROM AuditLogs
                WHERE EmployeeCode = '{code}'
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("AuditLog not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE AuditLogs
                SET IsDeleted = True
                WHERE EmployeeCode = '{code}'";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<AuditLogReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"";

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(Action) LIKE '%{search}%'
                    OR UPPER(EmployeeCode) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "Action" => "Action",
                "EmployeeCode" => "EmployeeCode",
                "CREATEDAT" => "CreatedAt",
                "CREATEDBY" => "CreatedBy",
                "LASTMODIFIEDAT" => "LastModifiedAt",
                "LASTMODIFIEDBY" => "LastModifiedBy",
                _ => "Action"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT *
                        FROM AuditLogs
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM AuditLogs
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<AuditLogReadDto>
                {
                    Items = new List<AuditLogReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new AuditLogReadDto
                {
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    EmployeeCode = row.Table.Columns.Contains("EmployeeCode") ? row.Field<string>("EmployeeCode") : string.Empty,
                    Action = row.Table.Columns.Contains("Action") ? row.Field<string>("Action") : string.Empty,
                    EntityId = row.Table.Columns.Contains("EntityId") ? row.Field<int>("EntityId") : 0,
                    EntityType = row.Table.Columns.Contains("EntityType") ? row.Field<string>("EntityType") : string.Empty,
                    OldValues = row.Table.Columns.Contains("OldValues") ? row.Field<string>("OldValues") : string.Empty,
                    NewValues = row.Table.Columns.Contains("NewValues") ? row.Field<string>("NewValues") : string.Empty,
                    Timestamp = row.Table.Columns.Contains("Timestamp") ? row.Field<DateTime>("Timestamp") : DateTime.Now,
                    IPAddress = row.Table.Columns.Contains("IPAddress") ? row.Field<string>("IPAddress") : string.Empty
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<AuditLogReadDto>
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
     
    public async Task<AuditLogReadDto> GetByEmployeeCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT *
                FROM AuditLogs
                WHERE EmployeeCode = '{code}'";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("AuditLog not found", 404);

            DataRow row = dt.Rows[0];

            return new AuditLogReadDto
            {
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                Action = row.Field<string>("Action"),
                EntityId = row.Field<int>("EntityId"),
                EntityType = row.Table.Columns.Contains("EntityType") ? row.Field<string>("EntityType") : string.Empty,
                OldValues = row.Table.Columns.Contains("OldValues") ? row.Field<string>("OldValues") : string.Empty,
                NewValues = row.Table.Columns.Contains("NewValues") ? row.Field<string>("NewValues") : string.Empty,
                Timestamp = row.Table.Columns.Contains("Timestamp") ? row.Field<DateTime>("Timestamp") : DateTime.Now,
                IPAddress = row.Table.Columns.Contains("IPAddress") ? row.Field<string>("IPAddress") : string.Empty
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

      
}
