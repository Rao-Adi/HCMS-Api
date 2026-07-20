using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class TransferScopePolicyComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public TransferScopePolicyComponent(
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


    public async Task<TransferScopePolicyReadDto> CreateAsync(TransferScopePolicyCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (input.Id < 0)
                throw new CustomException("TransferScopePolicy is required.", 400);

            // Check duplicate by DivisionCode OR DivisionCode
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TransferScopePolicies
            WHERE DivisionCode = '{input.DivisionCode!.Replace("'", "''")}' 
                AND CompanyId = {CompanyId}
                AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("TransferScopePolicy already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO TransferScopePolicies
            (   CompanyId,
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                BusinessDomainCode,
                ReportingToLevel,
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
                '{input.DivisionCode.Replace("'", "''")}', 
                '{input.DepartmentCode!.Replace("'", "''")}', 
                '{input.SubDepartmentCode!.Replace("'", "''")}', 
                '{input.BusinessDomainCode!.Replace("'", "''")}', 
                '{input.ReportingToLevel}',
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
            SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
            FROM TransferScopePolicies t
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
            WHERE t.Id = {newId} AND t.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new TransferScopePolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                ReportingToLevel = row.Field<int>("ReportingToLevel"),
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM TransferScopePolicies
                WHERE DivisionCode = '{code}' AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TransferScopePolicy not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE TransferScopePolicies
                SET IsDeleted = True,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE DivisionCode = '{code}' AND CompanyId = {CompanyId}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<TransferScopePolicyReadDto>> GetAllAsync(TableFiltersDto input)
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
                    UPPER(t.DivisionCode) LIKE '%{search}%'
                    OR UPPER(t.DivisionCode) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "t.DivisionCode",
                "DESCRIPTION" => "t.ReportingToLevel",
                "ISACTIVE" => "t.IsActive",
                "CREATEDAT" => "t.CreatedAt",
                "CREATEDBY" => "t.CreatedBy",
                "LASTMODIFIEDAT" => "t.LastModifiedAt",
                "LASTMODIFIEDBY" => "t.LastModifiedBy",
                _ => "t.DivisionCode"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
                        FROM TransferScopePolicies t
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
                        FROM TransferScopePolicies t
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<TransferScopePolicyReadDto>
                {
                    Items = new List<TransferScopePolicyReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new TransferScopePolicyReadDto
                {
                    Id = row.Field<int>("Id"),
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    Division = row.Field<string>("Division"),
                    DivisionCode = row.Field<string>("DivisionCode"),

                    Department = row.Field<string>("Department"),
                    DepartmentCode = row.Field<string>("DepartmentCode"),

                    SubDepartment = row.Field<string>("SubDepartment"),
                    SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                    BusinessDomain = row.Field<string>("BusinessDomain"),
                    BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                    ReportingToLevel = row.Table.Columns.Contains("ReportingToLevel") ? row.Field<int>("ReportingToLevel") : 0,
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

            return new PaginationResult<TransferScopePolicyReadDto>
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

    public async Task<TransferScopePolicyReadDto> GetByTransferScopePolicyCodeAsync(string code)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                    SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
                    FROM TransferScopePolicies t
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
                WHERE t.DivisionCode = '{code}' AND t.CompanyId = {CompanyId}
                  AND t.IsActive = True
                  AND t.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("TransferScopePolicy not found", 404);

            DataRow row = dt.Rows[0];

            return new TransferScopePolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),
                ReportingToLevel = row.Field<int>("ReportingToLevel"),
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
 
    public async Task<TransferScopePolicyReadDto> UpdateAsync(TransferScopePolicyUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (string.IsNullOrWhiteSpace(input.DivisionCode))
                throw new CustomException("Invalid division code.", 200);

            // Check existence (DivisionCode is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM TransferScopePolicies
            WHERE DivisionCode = '{input.DivisionCode.Replace("'", "''")}'
                AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("TransferScopePolicy not found", 404);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE TransferScopePolicies
            SET 
                DivisionCode = '{input.DivisionCode.Replace("'", "''")}',
                DepartmentCode = '{input.DepartmentCode!.Replace("'", "''")}',
                SubDepartmentCode = '{input.SubDepartmentCode!.Replace("'", "''")}',
                BusinessDomainCode = '{input.BusinessDomainCode!.Replace("'", "''")}',
                ReportingToLevel = '{input.ReportingToLevel}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE DivisionCode = '{input.DivisionCode.Replace("'", "''")}' AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT  t.*, div.Name AS Division, d.Name Department, sd.Name SubDepartment, c.Name AS Company, bd.Name AS BusinessDomain
            FROM TransferScopePolicies t
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
            WHERE t.DivisionCode = '{input.DivisionCode.Replace("'", "''")}' AND t.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new TransferScopePolicyReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Division = row.Field<string>("Division"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("Department"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartment"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                ReportingToLevel = row.Field<int>("ReportingToLevel"),
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
