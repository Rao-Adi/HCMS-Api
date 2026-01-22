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


    public async Task<TemplateReadDto> CreateAsync(TemplateCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            //if (input.Id >0)
            //    throw new CustomException("Template code is required.", 200);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Templates
            WHERE DocumentTypeCode = '{input.DocumentTypeCode}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Template already exists", 409);

            // Insert (PostgreSQL syntax)
            string Safe(string s) => s?.Replace("'", "''") ?? "";
            int SafeInt(int s) => 0;

            string sql = @"
                        INSERT INTO Templates (
                            DocumentTypeCode,
                            TemplateName,
                            TemplateFileUrl,
                            TemplateType,
                            DivisionCode,
                            DepartmentCode,
                            SubDepartmentCode,
                            IsDefault, 
                            TemplateContent,
                            IsActive,
                            IsDeleted,
                            CreatedAt,
                            CreatedBy,
                            LastModifiedAt,
                            LastModifiedBy
                        )
                        VALUES (
                            @DocumentTypeCode,
                            @TemplateName,
                            @TemplateFileUrl,
                            @TemplateType,
                            @DivisionCode,
                            @DepartmentCode,
                            @SubDepartmentCode,
                            @IsDefault,
                            @TemplateContent,
                            TRUE,
                            FALSE,
                            NOW(),
                            @CreatedBy,
                            NOW(),
                            @LastModifiedBy
                        )
                        RETURNING Id;
                    ";

                                // Assuming _common.ExecuteScalarQuery accepts command + parameters
                                // (if not → change your helper or use NpgsqlCommand directly)

                                var parameters = new Dictionary<string, object>
                    {
                        { "@DocumentTypeCode",    input.DocumentTypeCode    ?? (object)DBNull.Value },
                        { "@TemplateName",        input.TemplateName        ?? (object)DBNull.Value },
                        { "@TemplateFileUrl",     input.TemplateFileUrl     ?? (object)DBNull.Value },
                        { "@TemplateType",        input.TemplateType  },
                        { "@DivisionCode",        input.DivisionCode        ?? (object)DBNull.Value },
                        { "@DepartmentCode",      input.DepartmentCode      ?? (object)DBNull.Value },
                        { "@SubDepartmentCode",   input.SubDepartmentCode   ?? (object)DBNull.Value },
                        { "@IsDefault",           input.IsDefault           },  // bool → true/false (no quotes)
                        { "@TemplateContent",     input.TemplateContent     ?? (object)DBNull.Value },
                        { "@CreatedBy",           userId                    },
                        { "@LastModifiedBy",      userId                    }
                    };

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(sql, parameters));

            // Fetch inserted record
            string selectQuery = $@"
            SELECT  *
            FROM Templates
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new TemplateReadDto
            {
                Id = row.Field<int>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileUrl = row.Field<string>("TemplateFileUrl"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                IsDefault = row.Field<bool>("IsDefault"),
                TemplateContent = row.Field<string>("TemplateContent"),
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


    public async Task<PaginationResult<TemplateReadDto>> GetAllAsync(TableFiltersDto input)
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

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT *
                        FROM Templates
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

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
                return new PaginationResult<TemplateReadDto>
                {
                    Items = new List<TemplateReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new TemplateReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    TemplateName = row.Table.Columns.Contains("TemplateName") ? row.Field<string>("TemplateName") : string.Empty,
                    TemplateFileUrl = row.Table.Columns.Contains("TemplateFileUrl") ? row.Field<string>("TemplateFileUrl") : string.Empty,
                    TemplateType = row.Table.Columns.Contains("TemplateType") ? row.Field<int>("TemplateType") : 0,
                    DivisionCode = row.Table.Columns.Contains("DivisionCode") ? row.Field<string>("DivisionCode") : string.Empty,
                    DepartmentCode = row.Table.Columns.Contains("DepartmentCode") ? row.Field<string>("DepartmentCode") : string.Empty,
                    SubDepartmentCode = row.Table.Columns.Contains("SubDepartmentCode") ? row.Field<string>("SubDepartmentCode") : string.Empty,
                    TemplateContent = row.Table.Columns.Contains("TemplateContent") ? row.Field<string>("TemplateContent") : string.Empty,
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

            return new PaginationResult<TemplateReadDto>
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

    public async Task<TemplateReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT *
                FROM Templates
                WHERE Id = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Templates not found", 200);

            DataRow row = dt.Rows[0];

            return new TemplateReadDto
            {
                Id = row.Field<int>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileUrl = row.Field<string>("TemplateFileUrl"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                TemplateContent = row.Field<string>("TemplateContent"),
                IsDefault = row.Field<bool>("IsDefault"),
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


    public async Task<TemplateReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT *
                FROM Templates
                WHERE Division = {dCode}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Templates not found", 200);

            DataRow row = dt.Rows[0];

            return new TemplateReadDto
            {
                Id = row.Field<int>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileUrl = row.Field<string>("TemplateFileUrl"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                TemplateContent = row.Field<string>("TemplateContent"),
                IsDefault = row.Field<bool>("IsDefault"),
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


    public async Task<TemplateReadDto> UpdateAsync(TemplateUpdateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
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
                TemplateFileUrl = '{input.TemplateFileURL}',
                TemplateType = '{input.TemplateType}',
                DivisionCode = '{input.DivisionCode}',
                DepartmentCode = '{input.DepartmentCode}',
                SubDepartmentCode = '{input.SubDepartmentCode}',
                TemplateContent = '{input.TemplateContent}',
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
            SELECT *
            FROM Templates
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new TemplateReadDto
            {
                Id = row.Field<int>("Id"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                TemplateName = row.Field<string>("TemplateName"),
                TemplateFileUrl = row.Field<string>("TemplateFileUrl"),
                TemplateType = row.Field<int>("TemplateType"),
                DivisionCode = row.Field<string>("DivisionCode"),
                DepartmentCode = row.Field<string>("DepartmentCode"),
                SubDepartmentCode = row.Field<string>("SubDepartmentCode"),
                TemplateContent = row.Field<string>("TemplateContent"),
                IsDefault = row.Field<bool>("IsDefault"),
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
