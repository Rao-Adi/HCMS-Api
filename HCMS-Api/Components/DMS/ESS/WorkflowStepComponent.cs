using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class WorkflowStepComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public WorkflowStepComponent(
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


    public async Task<WorkflowStep> CreateAsync(WorkflowStep input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("WorkflowStep ID is required.", 200);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM WorkflowPolicies
            WHERE (Id = '{input.Id}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("WorkflowStep already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO WorkflowSteps
            (
                WorkflowPolicyId, 
                Sequence,
                ApproverRoleId,
                ApproverUserId,
                ApprovalLevel, 
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                '{input.WorkflowPolicyId}', 
                '{input.Sequence}', 
                '{input.ApproverRoleId}', 
                '{input.ApproverUserId}', 
                '{input.ApprovalLevel}', 
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
                    WorkflowPolicyId, 
                    Sequence,
                    ApproverRoleId,
                    ApproverUserId,
                    ApprovalLevel, 
                    IsActive
            FROM WorkflowSteps
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new WorkflowStep
            {
                Id = row.Field<Guid>("Id"),
                WorkflowPolicyId = row.Field<Guid>("WorkflowPolicyId"),
                Sequence = row.Field<int>("Sequence"),
                ApproverRoleId = row.Field<int>("ApproverRoleId"),
                ApproverUserId = row.Field<Guid>("ApproverUserId"),
                ApprovalLevel = row.Field<int>("ApprovalLevel"),
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
                FROM WorkflowSteps
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("WorkflowSteps not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE WorkflowSteps
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<WorkflowStep>> GetAllAsync(TableFiltersDto input)
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
                    OR UPPER(Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "Name",
                "CODE" => "Id",
                "ISACTIVE" => "IsActive",
                _ => "Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.pageNo - 1) * input.pageSize;

            string query = $@"
                        SELECT *
                        FROM WorkflowSteps
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.pageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM WorkflowSteps
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<WorkflowStep>
                {
                    Items = new List<WorkflowStep>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new WorkflowStep
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<Guid>("Id") : Guid.Empty,
                    WorkflowPolicyId = row.Table.Columns.Contains("WorkflowPolicyId") ? row.Field<Guid>("WorkflowPolicyId") : Guid.Empty,
                    Sequence = row.Table.Columns.Contains("Sequence") ? row.Field<int>("Sequence") : 0,
                    ApproverRoleId = row.Table.Columns.Contains("ApproverRoleId") ? row.Field<int>("ApproverRoleId") : 0,
                    ApproverUserId = row.Table.Columns.Contains("ApproverUserId") ? row.Field<Guid>("ApproverUserId") : Guid.Empty,
                    ApprovalLevel = row.Table.Columns.Contains("ApprovalLevel") ? row.Field<int>("ApprovalLevel") : 0,
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

            return new PaginationResult<WorkflowStep>
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

    public async Task<WorkflowStep> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT  Id,
                        WorkflowPolicyId, 
                        Sequence,
                        ApproverRoleId,
                        ApproverUserId,
                        ApprovalLevel, 
                        IsActive
                FROM WorkflowSteps
                WHERE Id = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("WorkflowSteps not found", 200);

            DataRow row = dt.Rows[0];

            return new WorkflowStep
            {
                Id = row.Field<Guid>("Id"),
                WorkflowPolicyId = row.Field<Guid>("WorkflowPolicyId"),
                Sequence = row.Field<int>("Sequence"),
                ApproverRoleId = row.Field<int>("ApproverRoleId"),
                ApproverUserId = row.Field<Guid>("ApproverUserId"),
                ApprovalLevel = row.Field<int>("ApprovalLevel"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<WorkflowStep> GetBySequenceAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT  Id,
                        WorkflowPolicyId, 
                        Sequence,
                        ApproverRoleId,
                        ApproverUserId,
                        ApprovalLevel, 
                        IsActive
                FROM WorkflowSteps
                WHERE Division = {dCode}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("WorkflowSteps not found", 200);

            DataRow row = dt.Rows[0];

            return new WorkflowStep
            {
                Id = row.Field<Guid>("Id"),
                WorkflowPolicyId = row.Field<Guid>("WorkflowPolicyId"),
                Sequence = row.Field<int>("Sequence"),
                ApproverRoleId = row.Field<int>("ApproverRoleId"),
                ApproverUserId = row.Field<Guid>("ApproverUserId"),
                ApprovalLevel = row.Field<int>("ApprovalLevel"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<WorkflowStep> UpdateAsync(WorkflowStep input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM WorkflowSteps
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("WorkflowSteps not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE WorkflowSteps
            SET 
                WorkflowPolicyId = '{input.WorkflowPolicyId}', 
                Sequence = '{input.Sequence}',
                ApproverRoleId = '{input.ApproverRoleId}',
                ApproverUserId = '{input.ApproverUserId}',
                ApprovalLevel = '{input.ApprovalLevel}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT Id
            FROM WorkflowSteps
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new WorkflowStep
            {
                Id = row.Field<Guid>("Id"),
                WorkflowPolicyId = row.Field<Guid>("WorkflowPolicyId"),
                Sequence = row.Field<int>("Sequence"),
                ApproverRoleId = row.Field<int>("ApproverRoleId"),
                ApproverUserId = row.Field<Guid>("ApproverUserId"),
                ApprovalLevel = row.Field<int>("ApprovalLevel"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }
}
