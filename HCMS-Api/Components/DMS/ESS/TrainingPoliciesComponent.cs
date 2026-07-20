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
    }




    public async Task<TrainingPolicyReadDto> CreateAsync(TrainingPolicyCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check duplicate by Id OR DocumentTypeCode
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TrainingPolicies
            WHERE DocumentTypeCode = '{input.DocumentTypeCode}' AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("TrainingPolicy already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO TrainingPolicies
            (   CompanyId,
                DocumentTypeCode,
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
                '{CompanyId}',
                '{input.DocumentTypeCode}',
                '{input.TrainingRequired}',
                '{input.MinimumScore}',
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
             SELECT t.*, c.Id AS CompanyId, c.Name AS Company,
             -- 🔹 Audit Fields
              COALESCE(e.EmployeeName, t.CreatedBy::text) AS CreatedByName,
 
              COALESCE(m.EmployeeName, t.LastModifiedBy::text) AS LastModifiedByName
             FROM TrainingPolicies t 
            LEFT JOIN Companies c
                    ON t.CompanyId = c.Id
                -- 🔹 Created By Employee
             LEFT JOIN Vw_EmployeeNames e
                 ON e.CleanEmpCode = LTRIM(t.CreatedBy::text, '0')

             -- 🔹 Last Modified By Employee
             LEFT JOIN Vw_EmployeeNames m 
                 ON m.CleanEmpCode = LTRIM(t.LastModifiedBy::text, '0')
            WHERE t.Id = {newId} AND t.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new TrainingPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TrainingRequired = row.Field<bool>("TrainingRequired"),
                MinimumScore = row.Field<int>("MinimumScore"),
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


    public async Task<bool> DeleteAsync(int code)
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
                FROM TrainingPolicies
                WHERE Id = {code} AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TrainingPolicy not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE TrainingPolicies
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


    public async Task<PaginationResult<TrainingPolicyReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE t.IsDeleted = False AND t.CompanyId = " + CompanyId + @"
                  AND t.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(t.DocumentTypeCode) LIKE '%{search}%'
                    OR UPPER(t.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DocumentTypeCode" => "t.DocumentTypeCode",
                "Id" => "t.Id",
                "ISACTIVE" => "t.IsActive",
                "CREATEDAT" => "t.CreatedAt",
                "CREATEDBY" => "t.CreatedBy",
                "LASTMODIFIEDAT" => "t.LastModifiedAt",
                "LASTMODIFIEDBY" => "t.LastModifiedBy",
                _ => "t.Id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT t.*, c.Id AS CompanyId, c.Name AS Company,
                         -- 🔹 Audit Fields
                          COALESCE(e.EmployeeName, t.CreatedBy::text) AS CreatedByName,
 
                          COALESCE(m.EmployeeName, t.LastModifiedBy::text) AS LastModifiedByName
                         FROM TrainingPolicies t 
                        LEFT JOIN Companies c
                                ON t.CompanyId = c.Id
                            -- 🔹 Created By Employee
                         LEFT JOIN Vw_EmployeeNames e
                             ON e.CleanEmpCode = LTRIM(t.CreatedBy::text, '0')

                         -- 🔹 Last Modified By Employee
                         LEFT JOIN Vw_EmployeeNames m 
                             ON m.CleanEmpCode = LTRIM(t.LastModifiedBy::text, '0')
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM TrainingPolicies t
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
            // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<TrainingPolicyReadDto>
                {
                    Items = new List<TrainingPolicyReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new TrainingPolicyReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    MinimumScore = row.Table.Columns.Contains("MinimumScore") ? row.Field<int>("MinimumScore") : 0,
                    TrainingRequired = row.Table.Columns.Contains("TrainingRequired") && row.Field<bool?>("TrainingRequired") == true,
                    IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                    IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                    CreatedByName = row.Table.Columns.Contains("CreatedByName") ? row.Field<string>("CreatedByName") : string.Empty,
                    LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                    LastModifiedByName = row.Table.Columns.Contains("LastModifiedByName") ? row.Field<string>("LastModifiedByName") : string.Empty,
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<TrainingPolicyReadDto>
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

    public async Task<TrainingPolicyReadDto> GetByIdAsync(int id)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                 SELECT t.*, c.Id AS CompanyId, c.Name AS Company,
                     -- 🔹 Audit Fields
                      COALESCE(e.EmployeeName, t.CreatedBy::text) AS CreatedByName,
 
                      COALESCE(m.EmployeeName, t.LastModifiedBy::text) AS LastModifiedByName
                     FROM TrainingPolicies t 
                    LEFT JOIN Companies c
                            ON t.CompanyId = c.Id
                        -- 🔹 Created By Employee
                     LEFT JOIN Vw_EmployeeNames e
                         ON e.CleanEmpCode = LTRIM(t.CreatedBy::text, '0')

                     -- 🔹 Last Modified By Employee
                     LEFT JOIN Vw_EmployeeNames m 
                         ON m.CleanEmpCode = LTRIM(t.LastModifiedBy::text, '0')
                WHERE t.Id = {id} AND t.CompanyId = {CompanyId}
                  AND t.IsActive = True
                  AND t.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("TrainingPolicy not found", 404);

            DataRow row = dt.Rows[0];

            return new TrainingPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TrainingRequired = row.Field<bool>("TrainingRequired"),
                MinimumScore = row.Field<int>("MinimumScore"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                CreatedByName = row.Table.Columns.Contains("CreatedByName") ? row.Field<string>("CreatedByName") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                LastModifiedByName = row.Table.Columns.Contains("LastModifiedByName") ? row.Field<string>("LastModifiedByName") : string.Empty,
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<TrainingPolicyReadDto> GetByDocumentTypeAsync(string dtCode)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                 SELECT t.*, c.Id AS CompanyId, c.Name AS Company,
                     -- 🔹 Audit Fields
                      COALESCE(e.EmployeeName, t.CreatedBy::text) AS CreatedByName,
 
                      COALESCE(m.EmployeeName, t.LastModifiedBy::text) AS LastModifiedByName
                     FROM TrainingPolicies t 
                    LEFT JOIN Companies c
                            ON t.CompanyId = c.Id
                        -- 🔹 Created By Employee
                     LEFT JOIN Vw_EmployeeNames e
                         ON e.CleanEmpCode = LTRIM(t.CreatedBy::text, '0')

                     -- 🔹 Last Modified By Employee
                     LEFT JOIN Vw_EmployeeNames m 
                         ON m.CleanEmpCode = LTRIM(t.LastModifiedBy::text, '0')
                WHERE t.DocumentTypeCode = '{dtCode}' AND t.CompanyId = {CompanyId}
                  AND t.IsActive = True
                  AND t.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("TrainingPolicy not found", 404);

            DataRow row = dt.Rows[0];

            return new TrainingPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TrainingRequired = row.Field<bool>("TrainingRequired"),
                MinimumScore = row.Field<int>("MinimumScore"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                CreatedByName = row.Table.Columns.Contains("CreatedByName") ? row.Field<string>("CreatedByName") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                LastModifiedByName = row.Table.Columns.Contains("LastModifiedByName") ? row.Field<string>("LastModifiedByName") : string.Empty,
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<TrainingPolicyReadDto> UpdateAsync(TrainingPolicyUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id < 0)
                throw new CustomException("Invalid Id.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TrainingPolicies
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TrainingPolicy not found", 404);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE TrainingPolicies
            SET 
                DocumentTypeCode = '{input.DocumentTypeCode}',
                MinimumScore = '{input.MinimumScore}',
                TrainingRequired = {(input.TrainingRequired ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
             SELECT t.*, c.Id AS CompanyId, c.Name AS Company,
                 -- 🔹 Audit Fields
                  COALESCE(e.EmployeeName, t.CreatedBy::text) AS CreatedByName,
 
                  COALESCE(m.EmployeeName, t.LastModifiedBy::text) AS LastModifiedByName
                 FROM TrainingPolicies t 
                LEFT JOIN Companies c
                        ON t.CompanyId = c.Id
                    -- 🔹 Created By Employee
                 LEFT JOIN Vw_EmployeeNames e
                     ON e.CleanEmpCode = LTRIM(t.CreatedBy::text, '0')

                 -- 🔹 Last Modified By Employee
                 LEFT JOIN Vw_EmployeeNames m 
                     ON m.CleanEmpCode = LTRIM(t.LastModifiedBy::text, '0')
            WHERE t.Id = {input.Id} AND t.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new TrainingPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TrainingRequired = row.Field<bool>("TrainingRequired"),
                MinimumScore = row.Field<int>("MinimumScore"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                CreatedByName = row.Table.Columns.Contains("CreatedByName") ? row.Field<string>("CreatedByName") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                LastModifiedByName = row.Table.Columns.Contains("LastModifiedByName") ? row.Field<string>("LastModifiedByName") : string.Empty,
            };
        }
        catch
        {
            throw;
        }
    }

}
