using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data; 

namespace HCMS_Api.Components.DMS.ESS;

public class DocumentTypeComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public DocumentTypeComponent(
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


    public async Task<DocumentTypeReadDto> CreateAsync(DocumentTypeCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Document type name is required.", 400);

            // 🔍 Check duplicate by NAME only
            string duplicateCheckQuery = $@"
                            SELECT COUNT(1)
                            FROM DocumentTypes
                            WHERE Name = '{input.Name.Replace("'", "''")}'
                              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(duplicateCheckQuery));

            if (exists > 0)
                throw new CustomException("Document Type already exists", 409);

            // 🔢 Generate next Division Code
            string getLastCodeQuery = @"
                            SELECT Code
                            FROM DocumentTypes
                            WHERE Code IS NOT NULL
                            ORDER BY Id DESC
                            LIMIT 1";

            var lastCodeObj = _common.ExecuteScalarQuery(getLastCodeQuery);

            int nextNumber = 1;

            if (lastCodeObj != null)
            {
                var lastCode = lastCodeObj.ToString(); // e.g. DIV-0012
                var numericPart = lastCode.Replace("DT-", "");

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            string generatedCode = $"DT-{nextNumber:D4}";

            // 🧾 Insert
            string insertQuery = $@"
                    INSERT INTO DocumentTypes
                    (   CompanyId,
                        Code,
                        Name,
                        Description,
                        IsActive,
                        IsDeleted,
                        CreatedAt,
                        CreatedBy,
                        LastModifiedAt,
                        LastModifiedBy
                    )
                    VALUES
                    (
                        {CompanyId},
                        '{generatedCode}',
                        '{input.Name.Replace("'", "''")}',
                        '{input.Description?.Replace("'", "''")}',
                        TRUE,
                        FALSE,
                        NOW(),
                        '{empCode.Replace("'", "''")}',
                        NOW(),
                        '{empCode.Replace("'", "''")}'
                    )
                    RETURNING Id;";

            int newId =
                Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // 📥 Fetch inserted record
            string selectQuery = $@"SELECT d.*, c.Name AS Company,
                -- 🔹 Audit Fields
                 COALESCE(e.EmployeeName, d.CreatedBy::text) AS CreatedByName, 
                 COALESCE(m.EmployeeName, d.LastModifiedBy::text) AS LastModifiedByName 
    
                FROM DocumentTypes d
                LEFT JOIN Companies c
                ON d.CompanyId = c.Id
 
                  -- 🔹 Created By Employee
                LEFT JOIN Vw_EmployeeNames e
                    ON e.CleanEmpCode = LTRIM(d.CreatedBy::text, '0')

                -- 🔹 Last Modified By Employee
                LEFT JOIN Vw_EmployeeNames m 
                    ON m.CleanEmpCode = LTRIM(d.LastModifiedBy::text, '0')
                WHERE d.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created document type");

            DataRow row = dt.Rows[0];

            return new DocumentTypeReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),

                Code = row.Field<string>("Code"), // 🔒 immutable
                Name = row.Field<string>("Name"),
                Description = row.Field<string>("Description"),
                IsActive = row.Field<bool>("IsActive"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                CreatedAt = row.Field<DateTime>("CreatedAt")
                                .ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt")
                                .ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy"),
                CreatedByName = row.Field<string>("CreatedByName"),
                LastModifiedByName = row.Field<string>("LastModifiedByName")
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
                FROM DocumentTypes
                WHERE Code = '{code}'
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentType not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE DocumentTypes
                SET IsDeleted = True,
                    LastModifiedAt = NOW(),
                    LastModifiedBy = '{empCode.Replace("'", "''")}'
                WHERE Code = '{code}'";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<DocumentTypeReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            var whereClause = @"
                WHERE d.IsDeleted = False 
                  AND d.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(d.Name) LIKE '%{search}%'
                    OR UPPER(d.Code) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "d.Name",
                "CODE" => "d.Code",
                "ISACTIVE" => "d.IsActive",
                _ => "d.Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"SELECT d.*, c.Name AS Company,
                        -- 🔹 Audit Fields
                         COALESCE(e.EmployeeName, d.CreatedBy::text) AS CreatedByName, 
                         COALESCE(m.EmployeeName, d.LastModifiedBy::text) AS LastModifiedByName 
    
                        FROM DocumentTypes d
                        LEFT JOIN Companies c
                        ON d.CompanyId = c.Id
 
                          -- 🔹 Created By Employee
                        LEFT JOIN Vw_EmployeeNames e
                            ON e.CleanEmpCode = LTRIM(d.CreatedBy::text, '0')

                        -- 🔹 Last Modified By Employee
                        LEFT JOIN Vw_EmployeeNames m 
                            ON m.CleanEmpCode = LTRIM(d.LastModifiedBy::text, '0')
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM DocumentTypes d
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<DocumentTypeReadDto>
                {
                    Items = new List<DocumentTypeReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new DocumentTypeReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Code = row.Table.Columns.Contains("Code") ? row.Field<string>("Code") : string.Empty,
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
                    Description = row.Table.Columns.Contains("Description") ? row.Field<string>("Description") : string.Empty,
                    IsActive = row.Table.Columns.Contains("IsActive") && row.Field<bool?>("IsActive") == true,
                    IsDeleted = row.Table.Columns.Contains("IsDeleted") && row.Field<bool?>("IsDeleted") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    CreatedBy = row.Table.Columns.Contains("CreatedBy") ? row.Field<string>("CreatedBy") : string.Empty,
                    LastModifiedAt = (row.Table.Columns.Contains("LastModifiedAt") && !row.IsNull("LastModifiedAt"))
                                     ? row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    LastModifiedBy = row.Table.Columns.Contains("LastModifiedBy") ? row.Field<string>("LastModifiedBy") : string.Empty,
                    CreatedByName = row.Field<string>("CreatedByName"),
                    LastModifiedByName = row.Field<string>("LastModifiedByName")
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<DocumentTypeReadDto>
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
            SELECT Code, Name
            FROM DocumentTypes
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY Code";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectListDto
                {
                    Code = row.Field<string>("Code"),
                    Value = row.Field<string>("Name")
                })
                .ToList();

            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<DocumentTypeReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"SELECT d.*, c.Name AS Company,
                -- 🔹 Audit Fields
                 COALESCE(e.EmployeeName, d.CreatedBy::text) AS CreatedByName, 
                 COALESCE(m.EmployeeName, d.LastModifiedBy::text) AS LastModifiedByName 
    
                FROM DocumentTypes d
                LEFT JOIN Companies c
                ON d.CompanyId = c.Id
 
                  -- 🔹 Created By Employee
                LEFT JOIN Vw_EmployeeNames e
                    ON e.CleanEmpCode = LTRIM(d.CreatedBy::text, '0')

                -- 🔹 Last Modified By Employee
                LEFT JOIN Vw_EmployeeNames m 
                    ON m.CleanEmpCode = LTRIM(d.LastModifiedBy::text, '0')
                WHERE d.Code = '{code}'
                  AND d.IsActive = True
                  AND d.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("DocumentType not found", 200);

            DataRow row = dt.Rows[0];

            return new DocumentTypeReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Description = row.Field<string>("Description"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy"),
                CreatedByName = row.Field<string>("CreatedByName"),
                LastModifiedByName = row.Field<string>("LastModifiedByName")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }
 

    public async Task<DocumentTypeReadDto> UpdateAsync(DocumentTypeUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP(); 
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Code is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM DocumentTypes
            WHERE Code = '{input.Code.Replace("'", "''")}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("DocumentType not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE DocumentTypes
            SET 
                Name = '{input.Name.Replace("'", "''")}',
                Description = '{input.Description!.Replace("'", "''")}',
                IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
                LastModifiedAt = NOW(),
                LastModifiedBy = '{empCode.Replace("'", "''")}'
            WHERE Code = '{input.Code.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"SELECT d.*, c.Name AS Company,
                -- 🔹 Audit Fields
                 COALESCE(e.EmployeeName, d.CreatedBy::text) AS CreatedByName, 
                 COALESCE(m.EmployeeName, d.LastModifiedBy::text) AS LastModifiedByName 
    
                FROM DocumentTypes d
                LEFT JOIN Companies c
                ON d.CompanyId = c.Id
 
                  -- 🔹 Created By Employee
                LEFT JOIN Vw_EmployeeNames e
                    ON e.CleanEmpCode = LTRIM(d.CreatedBy::text, '0')

                -- 🔹 Last Modified By Employee
                LEFT JOIN Vw_EmployeeNames m 
                    ON m.CleanEmpCode = LTRIM(d.LastModifiedBy::text, '0')
            WHERE Code = '{input.Code.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new DocumentTypeReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Description = row.Field<string>("Description"),
                IsDeleted = row.Field<bool>("IsDeleted"),
                IsActive = row.Field<bool>("IsActive"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedBy = row.Field<string>("CreatedBy"),
                LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
                LastModifiedBy = row.Field<string>("LastModifiedBy"),
                CreatedByName = row.Field<string>("CreatedByName"),
                LastModifiedByName = row.Field<string>("LastModifiedByName")
            };
        }
        catch
        {
            throw;
        }
    }


    public async Task<int> GetCount()
    {
        try
        {
            string query = $@"
                SELECT COUNT(1)
                FROM DocumentTypes 
                  WHERE IsDeleted = FALSE";
            int count = Convert.ToInt32(_common.ExecuteScalarQuery(query));
            return count;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
