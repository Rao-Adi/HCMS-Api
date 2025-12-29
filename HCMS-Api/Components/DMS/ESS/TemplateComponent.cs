using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class TemplateComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public TemplateComponent(
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


    public async Task<Template> CreateAsync(Template input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("Template code is required.", 200);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Templates
            WHERE (Id = '{input.Id}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Template already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Templates
            (
                DocumentTypeCode,
                TemplateName,
                TemplateFileURL,
                TemplateType,
                DivisionCode,
                DepartmentCode,
                SubDepartmentCode,
                IsDefault, 
                ApprovedBy, 
                ApprovedAt,
                IsActive,
                IsDeleted,
                CreatedAt,
                CreatedBy,
                LastModifiedAt,
                LastModifiedBy
            )
            VALUES
            (
                '{input.DocumentTypeCode}',
                '{input.TemplateName}',
                '{input.TemplateFileURL}',
                '{input.TemplateType}',
                '{input.DivisionCode}', 
                '{input.DepartmentCode}', 
                '{input.SubDepartmentCode}', 
                '{input.IsDefault}',
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
                    DocumentTypeCode,
                    TemplateName,
                    TemplateFileURL,
                    TemplateType,
                    DivisionCode,
                    DepartmentCode,
                    SubDepartmentCode,
                    IsDefault, 
                    ApprovedBy, 
                    ApprovedAt,
                    IsActive
            FROM Templates
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new Template
            {
                Id = row.Field<Guid>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileURL = row.Field<string>("TemplateFileURL"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                IsDefault = row.Field<bool>("IsDefault"),
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
                FROM Templates
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Templates not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Templates
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<Template>> GetAllAsync(TableFiltersDto input)
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
                        FROM Templates
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.pageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Templates
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<Template>
                {
                    Items = new List<Template>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new Template
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<Guid>("Id") : Guid.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    TemplateName = row.Table.Columns.Contains("TemplateName") ? row.Field<string>("TemplateName") : string.Empty,
                    TemplateFileURL = row.Table.Columns.Contains("TemplateFileURL") ? row.Field<string>("TemplateFileURL") : string.Empty,
                    TemplateType = row.Table.Columns.Contains("TemplateType") ? row.Field<int>("TemplateType") : 0,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,
                    IsDefault = row.Table.Columns.Contains("IsDefault") ? row.Field<bool>("IsDefault") : false,
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

            return new PaginationResult<Template>
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

    public async Task<Template> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT  Id,
                        DocumentTypeCode,
                        TemplateName,
                        TemplateFileURL,
                        TemplateType,
                        DivisionCode,
                        DepartmentCode,
                        SubDepartmentCode,
                        IsDefault, 
                        ApprovedBy, 
                        ApprovedAt,
                        IsActive
                FROM Templates
                WHERE Id = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Templates not found", 200);

            DataRow row = dt.Rows[0];

            return new Template
            {
                Id = row.Field<Guid>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileURL = row.Field<string>("TemplateFileURL"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                IsDefault = row.Field<bool>("IsDefault"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<Template> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT  Id,
                        DocumentTypeCode,
                        TemplateName,
                        TemplateFileURL,
                        TemplateType,
                        DivisionCode,
                        DepartmentCode,
                        SubDepartmentCode,
                        IsDefault, 
                        ApprovedBy, 
                        ApprovedAt,
                        IsActive
                FROM Templates
                WHERE Division = {dCode}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Templates not found", 200);

            DataRow row = dt.Rows[0];

            return new Template
            {
                Id = row.Field<Guid>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileURL = row.Field<string>("TemplateFileURL"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                IsDefault = row.Field<bool>("IsDefault"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<Template> UpdateAsync(Template input)
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
            FROM Templates
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Templates not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Templates
            SET 
                DocumentTypeCode = '{input.DocumentTypeCode}',
                TemplateName = '{input.TemplateName}',
                TemplateFileURL = '{input.TemplateFileURL}',
                TemplateType = '{input.TemplateType}',
                DivisionCode = '{input.DivisionCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}',
                IsDefault = '{input.IsDefault}',
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
            FROM Templates
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new Template
            {
                Id = row.Field<Guid>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileURL = row.Field<string>("TemplateFileURL"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                IsDefault = row.Field<bool>("IsDefault"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }
}
