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
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            // Check duplicate by ID OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM CabinetStructureTabsConfig
            WHERE Name = '{input.Name.Replace("'", "''")}')
              AND IsDeleted = FALSE";

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
                '{userId.Replace("'", "''")}',
                NOW(),
                '{userId.Replace("'", "''")}'
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"
            SELECT cst.*, c.Id AS CompanyId, c.Name AS Company
                   FROM CabinetStructureTabsConfig cst
                   LEFT JOIN Companies c
                   ON cst.CompanyId = c.Id
            WHERE cst.Id = {newId}";

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
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

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
                WHERE ID = {id}";

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
            var whereClause = @"
                WHERE cst.IsDeleted = False 
                  AND cst.IsActive = " + (input.IsActive ? "True" : "False");

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
                "ISACTIVE" => "cst.IsActive",
                _ => "cst.ID"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT cst.*, c.Id AS CompanyId, c.Name AS Company
                        FROM CabinetStructureTabsConfig cst
                        LEFT JOIN Companies c
                               ON cst.CompanyId = c.Id
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
            string query = @"
            SELECT ID, Name
            FROM CabinetStructureTabsConfig
            WHERE IsActive = True
              AND IsDeleted = False
            ORDER BY Id";

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
            string query = $@"
                SELECT cst.*, c.Id AS CompanyId, c.Name AS Company
                        FROM CabinetStructureTabsConfig cst
                        LEFT JOIN Companies c
                        ON cst.CompanyId = c.Id
                WHERE cst.ID = {id}
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
            string CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);

            int finalId;
            // 1️ Check existence ONLY if Id > 0
            int exists = 0;

            if (input.Id > 0)
            {
                string existsQuery = $@"
                    SELECT COUNT(1)
                    FROM CabinetStructureTabsConfig
                    WHERE Id = {input.Id} AND IsDeleted = FALSE";

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
                            '{userId.Replace("'", "''")}',
                            NOW(),
                            '{userId.Replace("'", "''")}'
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
                            LastModifiedBy = '{userId.Replace("'", "''")}' 
                        WHERE ID = '{input.Id}'";

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
                        WHERE cst.Id = {finalId}";

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


            //if (input.Id < 0)
            //    throw new CustomException("Invalid Id.", 200);

            //// Check existence (ID is VARCHAR → must be quoted)
            //string checkQuery = $@"
            //SELECT COUNT(1)
            //FROM CabinetStructureTabsConfig
            //WHERE ID = '{input.Id}'
            //  AND IsDeleted = FALSE";

            //int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            //if (exists == 0)
            //{
            //    string insertQuery = $@"
            //        INSERT INTO CabinetStructureTabsConfig
            //        (   
            //            CompanyId,
            //            Name,
            //            IsActive,
            //            IsDeleted,
            //            CreatedAt,
            //            CreatedBy,
            //            LastModifiedAt,
            //            LastModifiedBy
            //        )
            //        VALUES
            //        ( 
            //            '{input.CompanyId}',
            //            '{input.Name.Replace("'", "''")}',
            //            TRUE,
            //            FALSE,
            //            NOW(),
            //            '{userId.Replace("'", "''")}',
            //            NOW(),
            //            '{userId.Replace("'", "''")}'
            //        )
            //        RETURNING Id;";

            //    int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            //    // Fetch inserted record
            //    selectQuery = $@"
            //            SELECT cst.*, c.Id AS CompanyId, c.Name AS Company
            //                   FROM CabinetStructureTabsConfig cst
            //                   LEFT JOIN Companies c
            //                   ON cst.CompanyId = c.Id
            //            WHERE cst.Id = {newId}";

            //    DataTable dt = await _common.ExecuteSqlQuery(selectQuery);
            //}

            //// Update (PostgreSQL boolean + timestamp)
            //string updateQuery = $@"
            //UPDATE CabinetStructureTabsConfig
            //SET 
            //    Name = '{input.Name.Replace("'", "''")}',
            //    IsActive = {(input.IsActive ? "TRUE" : "FALSE")},
            //    LastModifiedAt = NOW(),
            //    LastModifiedBy = '{userId.Replace("'", "''")}'
            //WHERE ID = '{input.Id}'";

            //bool updated = _common.ExecuteNonQuery(updateQuery);

            //if (!updated)
            //    throw new Exception("Update failed");

            //// Return updated record
            //string selectQuery = $@"
            //SELECT cst.*, c.Id AS CompanyId, c.Name AS Company
            //            FROM CabinetStructureTabsConfig cst
            //            LEFT JOIN Companies c
            //                   ON cst.CompanyId = c.Id
            //WHERE cst.ID = '{input.Id}'";

            //DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            //if (dt == null || dt.Rows.Count == 0)
            //    throw new Exception("Failed to fetch updated Id");

            //DataRow row = dt.Rows[0];

            //return new CabinetStructureTabsConfigReadDto
            //{
            //    Id = row.Field<int>("ID"),
            //    CompanyId = row.Field<Int64>("CompanyId"),
            //    Company = row.Field<string>("Company"),
            //    Name = row.Field<string>("Name"),
            //    IsDeleted = row.Field<bool>("IsDeleted"),
            //    IsActive = row.Field<bool>("IsActive"),
            //    CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss"),
            //    CreatedBy = row.Field<string>("CreatedBy"),
            //    LastModifiedAt = row.Field<DateTime>("LastModifiedAt").ToString("yyyy-MM-dd HH:mm:ss"),
            //    LastModifiedBy = row.Field<string>("LastModifiedBy")
            //};
        }
        catch
        {
            throw;
        }
    }

}
