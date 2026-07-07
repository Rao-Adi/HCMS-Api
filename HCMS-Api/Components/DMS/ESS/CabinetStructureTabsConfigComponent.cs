using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class CabinetStructureTabsConfigComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public CabinetStructureTabsConfigComponent(
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


    public async Task<CabinetStructureTabsConfigReadDto> CreateAsync(CabinetStructureTabsConfigCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check duplicate by ID OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM CabinetStructureTabsConfig
            WHERE Name = '{input.Name.Replace("'", "''")}')
              AND IsDeleted = FALSE AND CompanyId = {CompanyId}";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("CabinetStructureTabsConfig already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO CabinetStructureTabsConfig
            (   
                CompanyId,
                Name,
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
                '{input.Name.Replace("'", "''")}',
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
            SELECT cst.*, c.Id AS CompanyId, c.Name AS Company
                   FROM CabinetStructureTabsConfig cst
                   LEFT JOIN Companies c
                   ON cst.CompanyId = c.Id
            WHERE cst.Id = {newId} AND cst.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created Id");

            DataRow row = dt.Rows[0];

            return new CabinetStructureTabsConfigReadDto
            {
                Id = row.Field<int>("ID"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Name = row.Field<string>("Name"),
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


    public async Task<bool> DeleteAsync(int id)
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
                FROM CabinetStructureTabsConfig
                WHERE Id = {id}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("CabinetStructureTabsConfig not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE CabinetStructureTabsConfig
                SET IsDeleted = False
                    , LastModifiedAt = NOW()
                    , LastModifiedBy = '{empCode.Replace("'", "''")}'    
                WHERE ID = {id} AND CompanyId = {CompanyId}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<CabinetStructureTabsConfigReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE cst.IsDeleted = False AND cst.CompanyId = " + CompanyId + " AND cst.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(cst.Name) LIKE '%{search}%'
                    OR UPPER(cst.ID) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "cst.Name",
                "CODE" => "cst.ID",
                "CREATEDAT" => "cst.CreatedAt",
                "CREATEDBY" => "cst.CreatedBy",
                "LASTMODIFIEDAT" => "cst.LastModifiedAt",
                "LASTMODIFIEDBY" => "cst.LastModifiedBy",
                "ISACTIVE" => "cst.IsActive",
                _ => "cst.ID"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT cst.*, c.Id AS CompanyId, c.Name AS Company,
                        -- 🔹 Audit Fields
                         COALESCE(e.EmployeeName, cst.CreatedBy::text) AS CreatedByName,
 
                         COALESCE(m.EmployeeName, cst.LastModifiedBy::text) AS LastModifiedByName
  
                        FROM CabinetStructureTabsConfig cst
                        LEFT JOIN Companies c
                               ON cst.CompanyId = c.Id
                           -- 🔹 Created By Employee
                        LEFT JOIN Vw_EmployeeNames e
                            ON e.CleanEmpCode = LTRIM(cst.CreatedBy::text, '0')

                        -- 🔹 Last Modified By Employee
                        LEFT JOIN Vw_EmployeeNames m 
                            ON m.CleanEmpCode = LTRIM(cst.LastModifiedBy::text, '0')
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM CabinetStructureTabsConfig cst
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<CabinetStructureTabsConfigReadDto>
                {
                    Items = new List<CabinetStructureTabsConfigReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new CabinetStructureTabsConfigReadDto
                {
                    Id = row.Table.Columns.Contains("ID") ? row.Field<int>("ID") : 0,
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    Name = row.Table.Columns.Contains("Name") ? row.Field<string>("Name") : string.Empty,
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

            return new PaginationResult<CabinetStructureTabsConfigReadDto>
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


    public async Task<IQueryable<SelectList2Dto>> GetAllSelectList()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = @"
            SELECT ID, Name
            FROM CabinetStructureTabsConfig
            WHERE IsActive = True
              AND IsDeleted = False
              AND CompanyId = " + CompanyId + " ORDER BY Id";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<int>("ID"),
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


    public async Task<CabinetStructureTabsConfigReadDto> GetByIDAsync(int id)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = $@"
                SELECT cst.*, c.Id AS CompanyId, c.Name AS Company
                        FROM CabinetStructureTabsConfig cst
                        LEFT JOIN Companies c
                        ON cst.CompanyId = c.Id
                WHERE cst.ID = {id}
                  AND cst.CompanyId = {CompanyId}
                  AND cst.IsActive = True
                  AND cst.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("CabinetStructureTabsConfig not found", 200);

            DataRow row = dt.Rows[0];

            return new CabinetStructureTabsConfigReadDto
            {
                Id = row.Field<int>("ID"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Name = row.Field<string>("Name"),
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

    public async Task<CabinetStructureTabsConfigReadDto> UpdateAsync(CabinetStructureTabsConfigUpdateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            int finalId;
            // 1️ Check existence ONLY if Id > 0
            int exists = 0;

            if (input.Id > 0)
            {
                string existsQuery = $@"
                    SELECT COUNT(1)
                    FROM CabinetStructureTabsConfig
                    WHERE Id = {input.Id} AND CompanyId = {CompanyId} AND IsDeleted = FALSE";

                exists = Convert.ToInt32(_common.ExecuteScalarQuery(existsQuery));
            }
            // 2️⃣ INSERT
            if (exists <= 0)
            {
                string insertQuery = $@"
                        INSERT INTO CabinetStructureTabsConfig
                        (   
                            CompanyId,
                            Name,
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
                            '{input.Name.Replace("'", "''")}',
                            TRUE,
                            FALSE,
                            NOW(),
                            '{empCode.Replace("'", "''")}',
                            NOW(),
                            '{empCode.Replace("'", "''")}'
                        )
                        RETURNING Id;";

                finalId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));
            }
            // 3️ UPDATE
            else
            {
                string updateQuery = $@" 
                        UPDATE CabinetStructureTabsConfig 
                        SET 
                            Name = '{input.Name.Replace("'", "''")}', 
                            IsActive = {(input.IsActive ? "TRUE" : "FALSE")}, 
                            LastModifiedAt = NOW(),
                            LastModifiedBy = '{empCode.Replace("'", "''")}' 
                        WHERE ID = '{input.Id}' AND CompanyId = {CompanyId}";

                bool updated = _common.ExecuteNonQuery(updateQuery);

                if (!updated)
                    throw new Exception("Update failed.");

                finalId = input.Id;
            }

            // 4️⃣ FETCH FINAL RECORD
            string selectQuery = $@"
                        SELECT
                            cst.*,
                            c.Id AS CompanyId,
                            c.Name AS Company
                        FROM CabinetStructureTabsConfig cst
                        LEFT JOIN Companies c ON cst.CompanyId = c.Id
                        WHERE cst.Id = {finalId} AND cst.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch saved record.");

            DataRow row = dt.Rows[0];

            return new CabinetStructureTabsConfigReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                Name = row.Field<string>("Name"),
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
