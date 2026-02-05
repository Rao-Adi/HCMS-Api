using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class TransferWorkflowPolicyComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public TransferWorkflowPolicyComponent(
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


    public async Task<TransferWorkflowPolicyReadDto> CreateAsync(TransferWorkflowPolicyCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("TransferWorkflowPolicy is required.", 400);

             
            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO TransferWorkflowPolicies
            (   CompanyId,
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                BusinessDomainCode,
                ApprovalRoleId,
                ApprovalUserId, 
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
                '{input.DivisionCode!.Replace("'", "''")}', 
                '{input.DepartmentCode!.Replace("'", "''")}', 
                '{input.SubDepartmentCode!.Replace("'", "''")}', 
                '{input.BusinessDomainCode!.Replace("'", "''")}', 
                '{input.ApprovalRoleId}',
                '{input.ApprovalUserId}',
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
            SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
            FROM TransferWorkflowPolicies t
            LEFT JOIN Divisions div
            ON t.DivisionCode = div.Code
            LEFT JOIN Departments d
            ON t.DepartmentCode = d.Code
            LEFT JOIN SubDepartments sd
            ON t.SubDepartmentCode = sd.Code
            LEFT JOIN Companies c
            ON d.CompanyId = c.Id
            LEFT JOIN BusinessDomains bd
            ON d.BusinessDomainCode = bd.Code
            WHERE t.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new TransferWorkflowPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                ApprovalRoleId = row.Field<int>("ApprovalRoleId"),
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
                FROM TransferWorkflowPolicies
                WHERE DivisionCode = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE TransferWorkflowPolicies
                SET IsDeleted = False
                WHERE DivisionCode = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<TransferWorkflowPolicyReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE t.IsDeleted = False 
                  AND t.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(t.DivisionCode) LIKE '%{search}%'
                    OR UPPER(t.DivisionCode) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "t.DivisionCode",
                "DESCRIPTION" => "t.ApprovalRoleId",
                "ISACTIVE" => "t.IsActive",
                _ => "t.DivisionCode"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
                        FROM TransferWorkflowPolicies t
                        LEFT JOIN Divisions div
                        ON t.DivisionCode = div.Code
                        LEFT JOIN Departments d
                        ON t.DepartmentCode = d.Code
                        LEFT JOIN SubDepartments sd
                        ON t.SubDepartmentCode = sd.Code
                        LEFT JOIN Companies c
                        ON d.CompanyId = c.Id
                        LEFT JOIN BusinessDomains bd
                        ON d.BusinessDomainCode = bd.Code
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM TransferWorkflowPolicies
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<TransferWorkflowPolicyReadDto>
                {
                    Items = new List<TransferWorkflowPolicyReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new TransferWorkflowPolicyReadDto
                {
                    Id = row.Field<int>("Id"),
                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    Division = row.Field<string>("Division"),
                    DivisionCode = row.Field<string>("DivisionCode"),

                    Department = row.Field<string>("Department"),
                    DepartmentCode = row.Field<string>("DepartmentCode"),

                    SubDepartment = row.Field<string>("SubDepartment"),
                    SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                    BusinessDomain = row.Field<string>("BusinessDomain"),
                    BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                    ApprovalRoleId = row.Table.Columns.Contains("ApprovalRoleId") ? row.Field<int>("ApprovalRoleId") : 0,
                    ApprovalUserId = row.Table.Columns.Contains("ApprovalUserId") ? row.Field<int>("ApprovalUserId") : 0,
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

            return new PaginationResult<TransferWorkflowPolicyReadDto>
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

    public async Task<TransferWorkflowPolicyReadDto> GetByTransferWorkflowPolicyCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
                    FROM TransferWorkflowPolicies t
                    LEFT JOIN Divisions div
                    ON t.DivisionCode = div.Code
                    LEFT JOIN Departments d
                    ON t.DepartmentCode = d.Code
                    LEFT JOIN SubDepartments sd
                    ON t.SubDepartmentCode = sd.Code
                    LEFT JOIN Companies c
                    ON d.CompanyId = c.Id
                    LEFT JOIN BusinessDomains bd
                    ON d.BusinessDomainCode = bd.Code
                WHERE t.DivisionCode = {code}
                  AND t.IsActive = True
                  AND t.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 200);

            DataRow row = dt.Rows[0];

            return new TransferWorkflowPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                ApprovalRoleId = row.Field<int>("ApprovalRoleId"),
                ApprovalUserId = row.Field<int>("ApprovalUserId"),
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

    public async Task<TransferWorkflowPolicyReadDto> UpdateAsync(TransferWorkflowPolicyUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.DivisionCode))
                throw new CustomException("Invalid division code.", 200);

            // Check existence (DivisionCode is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TransferWorkflowPolicies
            WHERE DivisionCode = '{input.DivisionCode.Replace("'", "''")}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TransferWorkflowPolicy not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE TransferWorkflowPolicies
            SET 
                DivisionCode = '{input.DivisionCode.Replace("'", "''")}',
                DepartmentCode = '{input.DepartmentCode!.Replace("'", "''")}',
                SubDepartmentCode = '{input.SubDepartmentCode!.Replace("'", "''")}',
                BusinessDomainCode = '{input.BusinessDomainCode!.Replace("'", "''")}',
                ApprovalRoleId = '{input.ApprovalRoleId}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE DivisionCode = '{input.DivisionCode.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
            FROM TransferWorkflowPolicies t
            LEFT JOIN Divisions div
            ON t.DivisionCode = div.Code
            LEFT JOIN Departments d
            ON t.DepartmentCode = d.Code
            LEFT JOIN SubDepartments sd
            ON t.SubDepartmentCode = sd.Code
            LEFT JOIN Companies c
            ON d.CompanyId = c.Id
            LEFT JOIN BusinessDomains bd
            ON d.BusinessDomainCode = bd.Code
            WHERE t.Id = {updated}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new TransferWorkflowPolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                ApprovalRoleId = row.Field<int>("ApprovalRoleId"),
                ApprovalUserId = row.Field<int>("ApprovalUserId"),
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
