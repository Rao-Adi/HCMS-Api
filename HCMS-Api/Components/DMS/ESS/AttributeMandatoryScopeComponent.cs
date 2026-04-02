using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class AttributeMandatoryScopeComponent
{

    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public AttributeMandatoryScopeComponent(
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

    public async Task<AttributeMandatoryScopeReadDto> CreateAsync(AttributeMandatoryScopeCreateDto input)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            // Check duplicate by DocumentAttributeId OR DivisionCode
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM AttributeMandatoryScopes
            WHERE (Id = '{input.DocumentAttributeId}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("AttributeMandatoryScope already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO AttributeMandatoryScopes
            (
                CompanyId,
                DocumentAttributeId,
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                BusinessDomainCode,
                IsMandatory,
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
                '{input.DocumentAttributeId}',
                '{input.DivisionCode}',
                '{input.DepartmentCode}',
                '{input.SubDepartmentCode}',
                '{input.BusinessDomainCode}',
                '{input.IsMandatory}',
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
            SELECT doc.*, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain, c.Id AS CompanyId, c.Name AS Company
                        FROM AttributeMandatoryScopes doc
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c 
                        ON doc.CompanyId = c.Id
            WHERE doc.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created Attribute Mandatory");

            DataRow row = dt.Rows[0];

            return new AttributeMandatoryScopeReadDto
            {
                DocumentAttributeId = row.Field<int>("DocumentAttributeId"),

                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                IsMandatory = row.Field<bool>("IsMandatory"),
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
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM AttributeMandatoryScopes
                WHERE DocumentAttributeId = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("AttributeMandatoryScope not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE AttributeMandatoryScopes
                SET IsDeleted = False
                WHERE DocumentAttributeId = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<AttributeMandatoryScopeReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            var whereClause = @"
                WHERE doc.IsDeleted = False 
                  AND doc.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(doc.Name) LIKE '%{search}%'
                    OR UPPER(doc.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "DocumentAttributeId" => "doc.DocumentAttributeId",
                "DivisionCode" => "doc.DivisionCode",
                "DepartmentCode" => "doc.DepartmentCode",
                "SubDepartmentCode" => "doc.SubDepartmentCode",
                "BusinessDomainCode" => "bd.BusinessDomainCode",
                "ISACTIVE" => "doc.IsActive",
                _ => "doc.DocumentAttributeId"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                       SELECT doc.*, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain, c.Id AS CompanyId, c.Name AS Company
                        FROM AttributeMandatoryScopes doc
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c 
                        ON doc.CompanyId = c.Id

                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM AttributeMandatoryScopes doc
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<AttributeMandatoryScopeReadDto>
                {
                    Items = new List<AttributeMandatoryScopeReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new AttributeMandatoryScopeReadDto
                {
                    DocumentAttributeId = row.Table.Columns.Contains("DocumentAttributeId") ? row.Field<int>("DocumentAttributeId") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    Division = row.Table.Columns.Contains("DivisionName") ? row.Field<string>("DivisionName") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,

                    Department = row.Table.Columns.Contains("DepartmentName") ? row.Field<string>("DepartmentName") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,

                    SubDepartment = row.Table.Columns.Contains("SubDepartmentName") ? row.Field<string>("SubDepartmentName") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,

                    BusinessDomain = row.Table.Columns.Contains("BusinessDomain") ? row.Field<string>("BusinessDomain") : string.Empty,
                    BusinessDomainCode = row.Table.Columns.Contains("BusinessDomainCode") ? row.Field<string>("BusinessDomainCode") : string.Empty,

                    IsMandatory = row.Table.Columns.Contains("IsMandatory") && row.Field<bool?>("IsMandatory") == true,
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

            return new PaginationResult<AttributeMandatoryScopeReadDto>
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
            SELECT DocumentAttributeId, DivisionCode
            FROM AttributeMandatoryScopes
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY DivisionCode";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<int>("DocumentAttributeId").ToString(),
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


    public async Task<PaginationResult<AttributeMandatoryScopeReadDto>> GetByCodeAsync(int id)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            string query = $@"
                SELECT doc.*, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain, c.Id AS CompanyId, c.Name AS Company
                        FROM AttributeMandatoryScopes doc
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c 
                        ON doc.CompanyId = c.Id
                WHERE DocumentAttributeId = {id}
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("AttributeMandatoryScope not found", 200);
             

            if (dt == null || dt.Rows.Count == 0)
            {
                return new PaginationResult<AttributeMandatoryScopeReadDto>
                {
                    Items = new List<AttributeMandatoryScopeReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = dt.AsEnumerable()
                .Select(row => new AttributeMandatoryScopeReadDto
                {
                    DocumentAttributeId = row.Table.Columns.Contains("DocumentAttributeId") ? row.Field<int>("DocumentAttributeId") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    Division = row.Table.Columns.Contains("DivisionName") ? row.Field<string>("DivisionName") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,

                    Department = row.Table.Columns.Contains("DepartmentName") ? row.Field<string>("DepartmentName") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,

                    SubDepartment = row.Table.Columns.Contains("SubDepartmentName") ? row.Field<string>("SubDepartmentName") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,

                    BusinessDomain = row.Table.Columns.Contains("BusinessDomain") ? row.Field<string>("BusinessDomain") : string.Empty,
                    BusinessDomainCode = row.Table.Columns.Contains("BusinessDomainCode") ? row.Field<string>("BusinessDomainCode") : string.Empty,

                    IsMandatory = row.Table.Columns.Contains("IsMandatory") && row.Field<bool?>("IsMandatory") == true,
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
            if (dt != null && dt.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(dt.Rows[0][0]);
            }

            return new PaginationResult<AttributeMandatoryScopeReadDto>
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


    public async Task<PaginationResult<AttributeMandatoryScopeReadDto>> GetAttributeByDocumenTypeId(int id)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            string query = $@"
                SELECT doc.*, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain, c.Id AS CompanyId, c.Name AS Company
                        FROM AttributeMandatoryScopes doc
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c 
                        ON doc.CompanyId = c.Id
                WHERE DocumentAttributeId = {id}
                  AND doc.IsActive = True
                  AND doc.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("AttributeMandatoryScope not found", 200);


            if (dt == null || dt.Rows.Count == 0)
            {
                return new PaginationResult<AttributeMandatoryScopeReadDto>
                {
                    Items = new List<AttributeMandatoryScopeReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = dt.AsEnumerable()
                .Select(row => new AttributeMandatoryScopeReadDto
                {
                    DocumentAttributeId = row.Table.Columns.Contains("DocumentAttributeId") ? row.Field<int>("DocumentAttributeId") : 0,

                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),

                    Division = row.Table.Columns.Contains("DivisionName") ? row.Field<string>("DivisionName") : string.Empty,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,

                    Department = row.Table.Columns.Contains("DepartmentName") ? row.Field<string>("DepartmentName") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,

                    SubDepartment = row.Table.Columns.Contains("SubDepartmentName") ? row.Field<string>("SubDepartmentName") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,

                    BusinessDomain = row.Table.Columns.Contains("BusinessDomain") ? row.Field<string>("BusinessDomain") : string.Empty,
                    BusinessDomainCode = row.Table.Columns.Contains("BusinessDomainCode") ? row.Field<string>("BusinessDomainCode") : string.Empty,

                    IsMandatory = row.Table.Columns.Contains("IsMandatory") && row.Field<bool?>("IsMandatory") == true,
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
            if (dt != null && dt.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(dt.Rows[0][0]);
            }

            return new PaginationResult<AttributeMandatoryScopeReadDto>
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




    public async Task<AttributeMandatoryScopeReadDto> UpdateAsync(AttributeMandatoryScopeUpdateDto input)
    {
        try
        {
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            // Check existence (DocumentAttributeId is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM AttributeMandatoryScopes
            WHERE DocumentAttributeId = '{input.DocumentAttributeId}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("AttributeMandatoryScope not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE AttributeMandatoryScopes
            SET 
                DivisionCode = '{input.DivisionCode.Replace("'", "''")}',
                DepartmentCode = '{input.DepartmentCode.Replace("'", "''")}',
                SubDepartmentCode = '{input.SubDepartmentCode.Replace("'", "''")}',
                IsMandatory = {(input.IsMandatory ? "TRUE" : "FALSE")},
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE DocumentAttributeId = '{input.DocumentAttributeId}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                        SELECT doc.*, div.Name AS DivisionName,
                        dep.Name AS DepartmentName, subd.Name AS SubDepartmentName, bd.Name AS BusinessDomain, c.Id AS CompanyId, c.Name AS Company
                        FROM AttributeMandatoryScopes doc
                        LEFT JOIN Divisions div
                        ON doc.DivisionCode = div.Code
                        LEFT JOIN Departments dep
                        ON doc.DepartmentCode = dep.Code
                        LEFT JOIN SubDepartments subd
                        ON doc.SubDepartmentCode = subd.Code
                        LEFT JOIN BusinessDomains bd
                        ON doc.BusinessDomainCode = bd.Code
                        LEFT JOIN Companies c 
                        ON doc.CompanyId = c.Id
            WHERE id = {updated}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new AttributeMandatoryScopeReadDto
            {
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                DocumentAttributeId = row.Field<int>("DocumentAttributeId"),

                Division = row.Field<string>("DivisionName"),
                DivisionCode = row.Field<string>("DivisionCode"),

                Department = row.Field<string>("DepartmentName"),
                DepartmentCode = row.Field<string>("DepartmentCode"),

                SubDepartment = row.Field<string>("SubDepartmentName"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),

                BusinessDomain = row.Field<string>("BusinessDomain"),
                BusinessDomainCode = row.Field<string>("BusinessDomainCode"),

                IsMandatory = row.Field<bool>("IsMandatory"),
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
