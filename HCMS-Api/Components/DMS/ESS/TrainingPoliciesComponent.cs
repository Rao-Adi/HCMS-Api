using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class TrainingPolicyComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public TrainingPolicyComponent(
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




    public async Task<TrainingPolicy> CreateAsync(TrainingPolicy input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
             
            // Check duplicate by Id OR DocumentTypeId
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TrainingPolicies
            WHERE (Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("TrainingPolicy already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO TrainingPolicies
            (
                DocumentTypeId,
                TrainingRequired,
                MinimumScore,
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                '{input.DocumentTypeId}',
                '{input.TrainingRequired}',
                '{input.MinimumScore}',
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
            SELECT  Id, 
                    DocumentTypeId,
                    TrainingRequired,
                    MinimumScore,
                    IsActive,
            FROM TrainingPolicies
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new TrainingPolicy
            {
                Id = row.Field<Guid>("Id"),
                DocumentTypeId = row.Field<int>("DocumentTypeId"),
                TrainingRequired = row.Field<bool>("TrainingRequired"),
                MinimumScore = row.Field<int>("MinimumScore"),
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
                FROM TrainingPolicies
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TrainingPolicy not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE TrainingPolicies
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<TrainingPolicy>> GetAllAsync(TableFiltersDto input)
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
                    UPPER(DocumentTypeId) LIKE '%{search}%'
                    OR UPPER(Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DocumentTypeId" => "DocumentTypeId",
                "Id" => "Id",
                "ISACTIVE" => "IsActive",
                _ => "Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.pageNo - 1) * input.pageSize;

            string query = $@"
                        SELECT *
                        FROM TrainingPolicies
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.pageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM TrainingPolicies
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
            // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<TrainingPolicy>
                {
                    Items = new List<TrainingPolicy>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new TrainingPolicy
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<Guid>("Id") : Guid.Empty,
                    DocumentTypeId = row.Table.Columns.Contains("DocumentTypeId") ? row.Field<int>("DocumentTypeId") : 0,
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

            return new PaginationResult<TrainingPolicy>
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

    public async Task<TrainingPolicy> GetByIdAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT  Id, 
                        DocumentTypeId,
                        TrainingRequired,
                        MinimumScore,
                        IsActive,
                FROM TrainingPolicies
                WHERE Id = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("TrainingPolicy not found", 200);

            DataRow row = dt.Rows[0];

            return new TrainingPolicy
            {
                Id = row.Field<Guid>("Id"),
                DocumentTypeId = row.Field<int>("DocumentTypeId"),
                TrainingRequired = row.Field<bool>("TrainingRequired"),
                MinimumScore = row.Field<int>("MinimumScore"), 
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<TrainingPolicy> UpdateAsync(TrainingPolicy input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("Invalid Id.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TrainingPolicies
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TrainingPolicy not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE TrainingPolicies
            SET 
                DocumentTypeId = '{input.DocumentTypeId}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT Id, DocumentTypeId, IsActive
            FROM TrainingPolicies
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new TrainingPolicy
            {
                Id = row.Field<Guid>("Id"), 
                DocumentTypeId = row.Field<int>("DocumentTypeId"),
                TrainingRequired = row.Field<bool>("TrainingRequired"),
                MinimumScore = row.Field<int>("MinimumScore"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }

}
