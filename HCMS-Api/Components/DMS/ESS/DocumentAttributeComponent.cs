using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentAttributeComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DocumentAttributeComponent(
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


    public async Task<DocumentAttributeReadDto> CreateAsync(DocumentAttributeCreateDto input)
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
            FROM DocumentAttributes
            WHERE ControlLabel = '{input.ControlLabel}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("DocumentAttribute already exists", 409);
 
            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO DocumentAttributes
            (   CompanyId,
                DocumentTypeCode,
                ControlLabel,
                ControlTypeId,
                ListValues,
                IsMandatory, 
                IsActive,
                IsDeleted,
                CreatedBy,
                CreatedAt,
                LastModifiedBy,
                LastModifiedAt
            )
            VALUES
            (
                {CompanyId},
                '{input.DocumentTypeCode}',
                '{input.ControlLabel.Trim()}',
                '{input.ControlTypeId}',
                '{input.ListValues!.Trim()}',
                {input.IsMandatory}, 
                TRUE,
                FALSE,
                '{empCode.Replace("'", "''")}',
                NOW(),
                '{empCode.Replace("'", "''")}',
                NOW()
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"
            SELECT da.*, dt.Code AS DocumentTypeCode, dt.Name AS DocumentType ,c.Id AS CompanyId, c.Name Company,
                         ct.Name AS ControlType
                         FROM DocumentAttributes da
                              LEFT JOIN DocumentTypes dt
                              ON da.DocumentTypeCode = dt.Code
                              LEFT JOIN Companies c
                              ON da.CompanyId = c.Id
	                          LEFT JOIN ControlTypes ct
	                          ON da.ControlTypeId = ct.Id
            WHERE da.Id = '{newId}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new DocumentAttributeReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                ControlLabel = row.Table.Columns.Contains("ControlLabel") ? row.Field<string>("ControlLabel") : string.Empty,
                ControlType = row.Table.Columns.Contains("ControlType") ? row.Field<string>("ControlType") : string.Empty,
                ControlTypeId = row.Table.Columns.Contains("ControlTypeId") ? row.Field<int>("ControlTypeId") : 0,
                ListValues = row.Table.Columns.Contains("ListValues") ? row.Field<string>("ListValues") : string.Empty,
                IsMandatory = row.Table.Columns.Contains("IsMandatory") ? row.Field<bool>("IsMandatory") : false,
                IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty
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
                FROM DocumentAttributes
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentAttribute not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentAttributes
                SET IsDeleted = True,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentAttributeReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE da.IsDeleted = False 
                  AND da.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(da.DocumentTypeCode) LIKE '%{search}%'
                    OR UPPER(da.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "da.DocumentTypeCode",
                "CODE" => "da.Id",
                "ISACTIVE" => "da.IsActive",
                _ => "da.DocumentTypeCode"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT da.*, dt.Code AS DocumentTypeCode, dt.Name AS DocumentType ,c.Id AS CompanyId, c.Name Company,
                         ct.Name AS ControlType
                         FROM DocumentAttributes da
                              LEFT JOIN DocumentTypes dt
                              ON da.DocumentTypeCode = dt.Code
                              LEFT JOIN Companies c
                              ON da.CompanyId = c.Id
	                          LEFT JOIN ControlTypes ct
	                          ON da.ControlTypeId = ct.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DocumentAttributes da
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentAttributeReadDto>
                {
                    Items = new List<DocumentAttributeReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentAttributeReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    ControlLabel = row.Table.Columns.Contains("ControlLabel") ? row.Field<string>("ControlLabel") : string.Empty,
                    ControlType = row.Table.Columns.Contains("ControlType") ? row.Field<string>("ControlType") : string.Empty,
                    ControlTypeId = row.Table.Columns.Contains("ControlTypeId") ? row.Field<int>("ControlTypeId") : 0,
                    ListValues = row.Table.Columns.Contains("ListValues") ? row.Field<string>("ListValues") : string.Empty,
                    IsMandatory = row.Table.Columns.Contains("IsMandatory") ? row.Field<bool>("IsMandatory") : false,
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

            return new PaginationResult<DocumentAttributeReadDto>
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


    public async Task<List<DocumentAttributeReadDto2>> GetDocumentAttributesByDocumentIdAsync(int documentId)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP()); 
            int CompanyId = int.Parse(_CompanyId); 


            var query = $@"SELECT 
                        da.Id AS DocumentAttributeId,
                        da.ControlLabel,
                        da.ControlTypeId,
    
                        dav.ValueText,
                        dav.ValueNumber,
                        dav.ValueDate,
                        dav.ValueBoolean

                    FROM DocumentAttributeValues dav
                    JOIN DocumentAttributes da
                        ON da.Id = dav.DocumentAttributeId
                    WHERE dav.CompanyId = {CompanyId}
                      AND dav.DocumentId = {documentId}
                      AND da.IsDeleted = FALSE
                      AND da.IsActive = TRUE
                    ORDER BY da.Id;";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];
          

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentAttributeReadDto2
                {
                    // Use <int?> to safely handle DBNull, then ?? 0 for the default
                    DocumentAttributeId = row.Table.Columns.Contains("DocumentAttributeId")
                        ? (row.Field<int?>("DocumentAttributeId") ?? 0)
                        : 0,

                    ControlLabel = row.Field<string>("ControlLabel"),

                    // Critical: ControlTypeId likely has a NULL in the DB
                    ControlTypeId = row.Field<int?>("ControlTypeId") ?? 0,

                    ValueText = row.Table.Columns.Contains("ValueText")
                        ? row.Field<string>("ValueText")
                        : string.Empty,

                    // ValueNumber is likely stored as decimal or int; use nullable to be safe
                    ValueNumber = row.Table.Columns.Contains("ValueNumber")
                        ? (row.Field<decimal?>("ValueNumber") ?? 0)
                        : 0,

                    // If ValueDate is a DateTime column, use row.Field<DateTime?>
                    ValueDate = row.Table.Columns.Contains("ValueDate")
                        ? row.Field<DateOnly?>("ValueDate")?.ToDateTime(TimeOnly.MinValue).ToString("yyyy-MM-dd") ?? string.Empty
                        : string.Empty,

                    ValueBoolean = row.Table.Columns.Contains("ValueBoolean") && (row.Field<bool?>("ValueBoolean") ?? false)

                })
                .ToList();

            return divisions;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<DocumentAttributeReadDto>> GetAllByDocumentTypeAsync(string documentTypeCode)
    {
        try
        { 
            string query = $@"
                        SELECT da.*, dt.Code AS DocumentTypeCode, dt.Name AS DocumentType ,c.Id AS CompanyId, c.Name Company,
                         ct.Name AS ControlType
                         FROM DocumentAttributes da
                              LEFT JOIN DocumentTypes dt
                              ON da.DocumentTypeCode = dt.Code
                              LEFT JOIN Companies c
                              ON da.CompanyId = c.Id
	                          LEFT JOIN ControlTypes ct
	                          ON da.ControlTypeId = ct.Id
                         WHERE da.DocumentTypeCode = '{documentTypeCode}'
                  AND da.IsActive = True
                  AND da.IsDeleted = False";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  

            var documentAttributes = divisionsTable.AsEnumerable()
                .Select(row => new DocumentAttributeReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                    DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                    ControlLabel = row.Table.Columns.Contains("ControlLabel") ? row.Field<string>("ControlLabel") : string.Empty,
                    ControlType = row.Table.Columns.Contains("ControlType") ? row.Field<string>("ControlType") : string.Empty,
                    ControlTypeId = row.Table.Columns.Contains("ControlTypeId") ? row.Field<int>("ControlTypeId") : 0,
                    ListValues = row.Table.Columns.Contains("ListValues") ? row.Field<string>("ListValues") : string.Empty,
                    IsMandatory = row.Table.Columns.Contains("IsMandatory") ? row.Field<bool>("IsMandatory") : false,
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


            return documentAttributes;
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
            SELECT Id, DocumentTypeCode
            FROM DocumentAttributes
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY DocumentTypeCode";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Id"),
                    Value = row.Field<string>("DocumentTypeCode")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentAttributeReadDto> GetByCodeAsync(int id)
    {
        try
        {
            string query = $@"
                SELECT da.*, dt.Code AS DocumentTypeCode, dt.Name AS DocumentType ,c.Id AS CompanyId, c.Name Company,
                         ct.Name AS ControlType
                         FROM DocumentAttributes da
                              LEFT JOIN DocumentTypes dt
                              ON da.DocumentTypeCode = dt.Code
                              LEFT JOIN Companies c
                              ON da.CompanyId = c.Id
	                          LEFT JOIN ControlTypes ct
	                          ON da.ControlTypeId = ct.Id
                WHERE da.Id = {id}
                  AND da.IsActive = True
                  AND da.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentAttribute not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentAttributeReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                ControlLabel = row.Table.Columns.Contains("ControlLabel") ? row.Field<string>("ControlLabel") : string.Empty,
                ControlTypeId = row.Table.Columns.Contains("ControlTypeId") ? row.Field<int>("ControlTypeId") : 0,
                ControlType = row.Table.Columns.Contains("ControlType") ? row.Field<string>("ControlType") : string.Empty,
                ListValues = row.Table.Columns.Contains("ListValues") ? row.Field<string>("ListValues") : string.Empty,
                IsMandatory = row.Table.Columns.Contains("IsMandatory") ? row.Field<bool>("IsMandatory") : false,
                IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentAttributeReadDto> GetByDocumentTypeCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT da.*, dt.Code AS DocumentTypeCode, dt.Name AS DocumentType ,c.Id AS CompanyId, c.Name Company,
                         ct.Name AS ControlType
                         FROM DocumentAttributes da
                              LEFT JOIN DocumentTypes dt
                              ON da.DocumentTypeCode = dt.Code
                              LEFT JOIN Companies c
                              ON da.CompanyId = c.Id
	                          LEFT JOIN ControlTypes ct
	                          ON da.ControlTypeId = ct.Id
                WHERE DocumentTypeCode = '{dCode}'
                  AND da.IsActive = True
                  AND da.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentAttribute not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentAttributeReadDto
            {
                Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentType = row.Table.Columns.Contains("DocumentType") ? row.Field<string>("DocumentType") : string.Empty,
                DocumentTypeCode = row.Table.Columns.Contains("DocumentTypeCode") ? row.Field<string>("DocumentTypeCode") : string.Empty,
                ControlLabel = row.Table.Columns.Contains("ControlLabel") ? row.Field<string>("ControlLabel") : string.Empty,
                ControlTypeId = row.Table.Columns.Contains("ControlTypeId") ? row.Field<int>("ControlTypeId") : 0,
                ControlType = row.Table.Columns.Contains("ControlType") ? row.Field<string>("ControlType") : string.Empty,
                ListValues = row.Table.Columns.Contains("ListValues") ? row.Field<string>("ListValues") : string.Empty,
                IsMandatory = row.Table.Columns.Contains("IsMandatory") ? row.Field<bool>("IsMandatory") : false,
                IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentAttributeReadDto> UpdateAsync(DocumentAttributeUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentAttributes
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentAttribute not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DocumentAttributes
            SET 
                ControlLabel = '{input.ControlLabel}',
                ControlTypeId = {input.ControlTypeId},
                ListValues = '{input.ListValues}',
                IsMandatory = '{input.IsMandatory}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
                    SELECT da.*, dt.Code AS DocumentTypeCode, dt.Name AS DocumentType ,c.Id AS CompanyId, c.Name Company,
                         ct.Name AS ControlType
                         FROM DocumentAttributes da
                              LEFT JOIN DocumentTypes dt
                              ON da.DocumentTypeCode = dt.Code
                              LEFT JOIN Companies c
                              ON da.CompanyId = c.Id
	                          LEFT JOIN ControlTypes ct
	                          ON da.ControlTypeId = ct.Id
            WHERE da.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentAttributeReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                DocumentTypeCode = row.Field<string>("DocumentTypeCode"),
                ControlLabel = row.Field<string>("ControlLabel"),
                ControlTypeId = row.Field<int>("ControlTypeId"),
                ControlType = row.Field<string>("ControlType"),
                ListValues = row.Field<string>("ListValues"),
                IsMandatory = row.Field<bool>("IsMandatory"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy")
            };
        }
        catch (Exception ex)
        {
            throw ex;
        }
    }

}
