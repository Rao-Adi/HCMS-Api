using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class ResponsibilityTransferComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public ResponsibilityTransferComponent(
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


    public async Task<ResponsibilityTransferReadDto> CreateAsync(ResponsibilityTransferCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("ResponsibilityTransfer code is required.", 200);

            // Check duplicate by Id OR Name
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM ResponsibilityTransfer
            WHERE (Id = '{input.Id}' 
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("ResponsibilityTransfer already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO ResponsibilityTransfer
            (
                EmployeeFromId,
                EmployeeToId,
                Reason,
                EffectiveDateFrom,
                EffectiveDateTo,
                IsPermanent,
                Remarks,
                Status, 
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
                '{input.Reason}',
                '{input.EmployeeToId}',
                '{input.Reason}',
                '{input.EffectiveDateFrom}',
                '{input.EffectiveDateTo}', 
                '{input.IsPermanent}', 
                '{input.Remarks}', 
                '{input.ApprovedBy}', 
                '{input.ApprovedAt}', 
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
                    EmployeeFromId,
                    EmployeeToId,
                    Reason,
                    EffectiveDateFrom,
                    EffectiveDateTo,
                    IsPermanent,
                    Remarks,
                    Status, 
                    ApprovedBy, 
                    ApprovedAt,
                    IsActive
            FROM ResponsibilityTransfers
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new ResponsibilityTransferReadDto
            {
                Id = row.Field<Guid>("Id"),
                EmployeeFromId = row.Field<Guid>("EmployeeFromId"),
                EmployeeToId = row.Field<Guid>("EmployeeToId"),
                Reason = row.Field<int>("Reason"),
                EffectiveDateFrom = row.Field<DateTime>("EffectiveDateFrom"),
                EffectiveDateTo = row.Field<DateTime>("EffectiveDateTo"),
                IsPermanent = row.Field<bool>("IsPermanent"),
                Remarks = row.Field<string>("Remarks"),
                Status = row.Field<int>("Status"),
                ApprovedBy = row.Field<Guid>("ApprovedBy"),
                ApprovedAt = row.Field<DateTime>("ApprovedAt"),
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
                FROM ResponsibilityTransfers
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("ResponsibilityTransfers not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE ResponsibilityTransfers
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<ResponsibilityTransferReadDto>> GetAllAsync(TableFiltersDto input)
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
                        FROM ResponsibilityTransfers
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM ResponsibilityTransfers
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
                                                      // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<ResponsibilityTransferReadDto>
                {
                    Items = new List<ResponsibilityTransferReadDto>(),
                    TotalCount = 0
                };
            }

            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new ResponsibilityTransferReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<Guid>("Id") : Guid.Empty,
                    EmployeeFromId = row.Table.Columns.Contains("EmployeeFromId") ? row.Field<Guid>("EmployeeFromId") : Guid.Empty,
                    EmployeeToId = row.Table.Columns.Contains("EmployeeToId") ? row.Field<Guid>("EmployeeToId") : Guid.Empty,
                    Reason = row.Table.Columns.Contains("Reason") ? row.Field<int>("Reason") : 0,
                    EffectiveDateFrom = row.Table.Columns.Contains("EffectiveDateFrom") ? row.Field<DateTime>("EffectiveDateFrom") : DateTime.Now,
                    EffectiveDateTo = row.Table.Columns.Contains("EffectiveDateTo") ? row.Field<DateTime>("EffectiveDateTo") : DateTime.Now,
                    IsPermanent = row.Table.Columns.Contains("IsPermanent") ? row.Field<bool>("IsPermanent") : false,
                    Remarks = row.Table.Columns.Contains("Remarks") ? row.Field<string>("Remarks") : string.Empty,
                    Status = row.Table.Columns.Contains("Status") ? row.Field<int>("Status") : 0,
                    ApprovedBy = row.Table.Columns.Contains("ApprovedBy") ? row.Field<Guid>("ApprovedBy") : Guid.Empty,
                    ApprovedAt = row.Table.Columns.Contains("ApprovedAt") ? row.Field<DateTime>("ApprovedAt") : DateTime.Now,
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

            return new PaginationResult<ResponsibilityTransferReadDto>
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
     
    public async Task<ResponsibilityTransferReadDto> GetByCodeAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT  Id,
                        EmployeeFromId,
                        EmployeeToId,
                        Reason,
                        EffectiveDateFrom,
                        EffectiveDateTo,
                        IsPermanent,
                        Remarks,
                        Status, 
                        ApprovedBy, 
                        ApprovedAt,
                        IsActive
                FROM ResponsibilityTransfers
                WHERE Id = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("ResponsibilityTransfers not found", 200);

            DataRow row = dt.Rows[0];

            return new ResponsibilityTransferReadDto
            {
                Id = row.Field<Guid>("Id"),
                EmployeeFromId = row.Field<Guid>("EmployeeFromId"),
                EmployeeToId = row.Field<Guid>("EmployeeToId"),
                Reason = row.Field<int>("Reason"),
                EffectiveDateFrom = row.Field<DateTime>("EffectiveDateFrom"),
                EffectiveDateTo = row.Field<DateTime>("EffectiveDateTo"),
                IsPermanent = row.Field<bool>("IsPermanent"),
                Remarks = row.Field<string>("Remarks"),
                Status = row.Field<int>("Status"),
                ApprovedBy = row.Field<Guid>("ApprovedBy"),
                ApprovedAt = row.Field<DateTime>("ApprovedAt"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<ResponsibilityTransferReadDto> GetByDivisionCodeAsync(string dCode)
    {
        try
        {
            string query = $@"
                SELECT  Id,
                        EmployeeFromId,
                        EmployeeToId,
                        Reason,
                        EffectiveDateFrom,
                        EffectiveDateTo,
                        IsPermanent,
                        Remarks,
                        Status, 
                        ApprovedBy, 
                        ApprovedAt,
                        IsActive
                FROM ResponsibilityTransfers
                WHERE Division = {dCode}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("ResponsibilityTransfers not found", 200);

            DataRow row = dt.Rows[0];

            return new ResponsibilityTransferReadDto
            {
                Id = row.Field<Guid>("Id"),
                EmployeeFromId = row.Field<Guid>("EmployeeFromId"),
                EmployeeToId = row.Field<Guid>("EmployeeToId"),
                Reason = row.Field<int>("Reason"),
                EffectiveDateFrom = row.Field<DateTime>("EffectiveDateFrom"),
                EffectiveDateTo = row.Field<DateTime>("EffectiveDateTo"),
                IsPermanent = row.Field<bool>("IsPermanent"),
                Remarks = row.Field<string>("Remarks"),
                Status = row.Field<int>("Status"),
                ApprovedBy = row.Field<Guid>("ApprovedBy"),
                ApprovedAt = row.Field<DateTime>("ApprovedAt"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<ResponsibilityTransferReadDto> UpdateAsync(ResponsibilityTransferUpdateDto input)
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
            FROM ResponsibilityTransfers
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("ResponsibilityTransfers not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE ResponsibilityTransfers
            SET 
                EmployeeFromId = '{input.EmployeeFromId}',
                EmployeeToId = '{input.EmployeeToId}',
                Reason = '{input.Reason}',
                EffectiveDateFrom = '{input.EffectiveDateFrom}',
                EffectiveDateTo = '{input.EffectiveDateTo}',
                IsPermanent = '{input.IsPermanent}',
                Remarks = '{input.Remarks}',
                Status = '{input.Status}',
                ApprovedBy = '{input.ApprovedBy}',
                ApprovedAt = '{input.ApprovedAt}',
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
            FROM ResponsibilityTransfers
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new ResponsibilityTransferReadDto
            {
                Id = row.Field<Guid>("Id"),
                EmployeeFromId = row.Field<Guid>("EmployeeFromId"),
                EmployeeToId = row.Field<Guid>("EmployeeToId"),
                Reason = row.Field<int>("Reason"),
                EffectiveDateFrom = row.Field<DateTime>("EffectiveDateFrom"),
                EffectiveDateTo = row.Field<DateTime>("EffectiveDateTo"),
                IsPermanent = row.Field<bool>("IsPermanent"),
                Remarks = row.Field<string>("Remarks"),
                Status = row.Field<int>("Status"),
                ApprovedBy = row.Field<Guid>("ApprovedBy"),
                ApprovedAt = row.Field<DateTime>("ApprovedAt"),
                IsActive = row.Field<bool>("IsActive")
            };
        }
        catch
        {
            throw;
        }
    }

}
