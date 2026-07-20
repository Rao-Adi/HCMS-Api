using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;
 
public class CompanyComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public CompanyComponent(
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
     
    public async Task<CompanyReadDto> CreateAsync(CompanyCreateDto input)
    {
        try
        { 

            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Company name is required.", 400);

            // 🔍 Check duplicate by NAME only
            string duplicateCheckQuery = $@"
                            SELECT COUNT(1)
                            FROM Companies
                            WHERE Name = '{input.Name.Replace("'", "''")}' ";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(duplicateCheckQuery));

            if (exists > 0)
                throw new CustomException("Company already exists", 409);

            // 🔢 Generate next Company Code
            string getLastCodeQuery = @"
                            SELECT Code
                            FROM Companies
                            WHERE Code IS NOT NULL
                            ORDER BY Id DESC
                            LIMIT 1";

            var lastCodeObj = _common.ExecuteScalarQuery(getLastCodeQuery);

            int nextNumber = 1;

            if (lastCodeObj != null)
            {
                var lastCode = lastCodeObj.ToString(); // e.g. DIV-0012
                var numericPart = lastCode.Replace("COM-", "");

                if (int.TryParse(numericPart, out int lastNumber))
                    nextNumber = lastNumber + 1;
            }

            string generatedCode = $"COM-{nextNumber:D4}";

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Companies
            (
                Code,
                Name,
                Status,
                SubscriptionPlan,
                StorageQuotaGB,
                CreatedAt
            )
            VALUES
            (
                '{generatedCode}',
                '{input.Name.Replace("'", "''")}',
                '{input.Status!.Replace("'", "''")}',
                '{input.SubscriptionPlan!.Replace("'", "''")}',
                {input.StorageQuotaGB}, 
                NOW()
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"
            SELECT *
            FROM Companies
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new CompanyReadDto
            {
                Id = row.Field<Int64>("Id"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Status = row.Field<string>("Status"),
                SubscriptionPlan = row.Field<string>("SubscriptionPlan"),
                StorageQuotaGB = row.Field<int>("StorageQuotaGB"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") 
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
                FROM Companies
                WHERE Code = {code} ";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Company not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Companies 
                WHERE Code = '{code}'";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<CompanyReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {

            var whereClause = @"";

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(Name) LIKE '%{search}%'
                    OR UPPER(Code) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "Name",
                "CODE" => "Code",
                "CREATEDAT" => "CreatedAt",
                "CREATEDBY" => "CreatedBy",
                "LASTMODIFIEDAT" => "LastModifiedAt",
                "LASTMODIFIEDBY" => "LastModifiedBy",
                _ => "Name"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT *
                        FROM Companies
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Companies
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
            // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<CompanyReadDto>
                {
                    Items = new List<CompanyReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new CompanyReadDto
                {
                    Id = row.Field<Int64>("Id"),
                    Code = row.Field<string>("Code"),
                    Name = row.Field<string>("Name"),
                    Status = row.Field<string>("Status"),
                    SubscriptionPlan = row.Field<string>("SubscriptionPlan"),
                    StorageQuotaGB = row.Field<int>("StorageQuotaGB"),
                    CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<CompanyReadDto>
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
            SELECT *
            FROM Companies 
            ORDER BY Name";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            var list = dt.AsEnumerable()
                .Select(row => new SelectList2Dto
                {
                    Id = row.Field<Int64>("Id"),
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


    public async Task<CompanyReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT *
                    FROM Companies
                WHERE Code = '{code}'";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Company not found", 404);

            DataRow row = dt.Rows[0];

            return new CompanyReadDto
            {
                Id = row.Field<Int64>("Id"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Status = row.Field<string>("Status"),
                SubscriptionPlan = row.Field<string>("SubscriptionPlan"),
                StorageQuotaGB = row.Field<int>("StorageQuotaGB"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<CompanyReadDto> UpdateAsync(CompanyUpdateDto input)
    {
        try
        {
            var clientIp = _clientContextService.GetClientIP();
            var prefix = _utilities.GetPrefix(clientIp);
            var userId = _utilities.GetUserid(prefix);
            if (string.IsNullOrWhiteSpace(input.Code))
                throw new CustomException("Company code is required.", 400);

            if (string.IsNullOrWhiteSpace(input.Name))
                throw new CustomException("Company name is required.", 400);

            // 🔍 Check division exists
            string existsQuery = $@"
                    SELECT COUNT(1)
                    FROM Companies
                    WHERE Code = '{input.Code.Replace("'", "''")}'";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(existsQuery));

            if (exists == 0)
                throw new CustomException("Company not found", 404);

            // 🚫 Prevent duplicate NAME (excluding current division)
            string duplicateNameQuery = $@"
                        SELECT COUNT(1)
                        FROM Companies
                        WHERE Name = '{input.Name.Replace("'", "''")}'
                          AND Code <> '{input.Code.Replace("'", "''")}'";

            int duplicate = Convert.ToInt32(_common.ExecuteScalarQuery(duplicateNameQuery));

            if (duplicate > 0)
                throw new CustomException("Company name already exists", 409);

            // ✏️ Update mutable fields ONLY
            string updateQuery = $@"
                        UPDATE Companies
                        SET 
                            Name = '{input.Name.Replace("'", "''")}', 
                            Status = '{input.Status!.Replace("'", "''")}', 
                            SubscriptionPlan = '{input.SubscriptionPlan!.Replace("'", "''")}', 
                            StorageQuotaGB = {input.StorageQuotaGB},  
                            CreatedAt = NOW(), 
                        WHERE Code = '{input.Code.Replace("'", "''")}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // 📥 Fetch updated record
            string selectQuery = $@"
                        SELECT d.*, c.Id AS CompanyId, c.Name AS Company
                        FROM Companies d
                        LEFT JOIN Company 
                        ON d.CompanyId = c.Id
                        WHERE d.Code = '{input.Code.Replace("'", "''")}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new CompanyReadDto
            {
                Id = row.Field<Int64>("Id"),
                Code = row.Field<string>("Code"),
                Name = row.Field<string>("Name"),
                Status = row.Field<string>("Status"),
                SubscriptionPlan = row.Field<string>("SubscriptionPlan"),
                StorageQuotaGB = row.Field<int>("StorageQuotaGB"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
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
                FROM Companies";
            int count = Convert.ToInt32(_common.ExecuteScalarQuery(query));
            return count;
        }
        catch (Exception)
        {
            throw;
        }
    }

}
