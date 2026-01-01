using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;

public class NotificationComponent
{
    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    public NotificationComponent(
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

     
    public async Task<NotificationReadDto> CreateAsync(NotificationCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id != Guid.Empty)
                throw new CustomException("Notifications code is required.", 200);

            // Check duplicate by Id OR UserId
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Notifications
            WHERE (Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Notifications already exists", 200);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Notifications
            (
                UserId,
                Title,
                Message,
                NotificationType,
                RelatedEntityType,
                RelatedEntityId,
                IsRead, 
                CreatedAt
            )
            VALUES
            (
                '{input.UserId}',
                '{input.Title}',
                '{input.Message}',
                '{input.NotificationType}',
                '{input.RelatedEntityType}',
                '{input.RelatedEntityId}',
                '{input.IsRead}',
                NOW(),
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"
            SELECT *
            FROM Notifications
            WHERE Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<Guid>("Id"),
                UserId = row.Field<Guid>("UserId"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<Guid>("RelatedEntityId"),
                IsRead = row.Field<bool>("IsRead"),
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
                FROM Notifications
                WHERE Id = {code}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Notifications not found", 200);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Notifications
                SET IsDeleted = False
                WHERE Id = {code}";

            return _common.ExecuteNonQuery(deleteQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<NotificationReadDto>> GetAllAsync(TableFiltersDto input)
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
                    UPPER(UserId) LIKE '%{search}%'
                    OR UPPER(Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "UserId",
                "CODE" => "Id",
                "ISACTIVE" => "IsActive",
                _ => "UserId"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                        SELECT *
                        FROM Notifications
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Notifications
                        {whereClause};
                    ";

            DataSet ds = await _common.ExecuteSqlQueryMultiple(query);
            DataTable divisionsTable = ds.Tables[0];  // your first result set (paged data)
            DataTable countTable = ds.Tables[1];      // second result set (count)
            // ✅ SAFETY CHECKS
            if (divisionsTable == null || divisionsTable.Rows.Count == 0)
            {
                return new PaginationResult<NotificationReadDto>
                {
                    Items = new List<NotificationReadDto>(),
                    TotalCount = 0
                };
            }
             
            var divisions = divisionsTable.AsEnumerable()
                .Select(row => new NotificationReadDto
                {
                    Id = row.Table.Columns.Contains("Id") ? row.Field<Guid>("Id") : Guid.Empty,
                    UserId = row.Table.Columns.Contains("UserId") ? row.Field<Guid>("UserId") : Guid.Empty,
                    Title = row.Table.Columns.Contains("Title") ? row.Field<string>("Title") : string.Empty,
                    Message = row.Table.Columns.Contains("Message") ? row.Field<string>("Message") : string.Empty,
                    NotificationType = row.Table.Columns.Contains("NotificationType") ? row.Field<int>("NotificationType") : 0,
                    RelatedEntityType = row.Table.Columns.Contains("RelatedEntityType") ? row.Field<string>("RelatedEntityType") : string.Empty,
                    RelatedEntityId = row.Table.Columns.Contains("RelatedEntityId") ? row.Field<Guid>("RelatedEntityId") : Guid.Empty,
                    IsRead = row.Table.Columns.Contains("IsRead") && row.Field<bool?>("IsRead") == true,
                    CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                                ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                })
                .ToList();

            int totalCount = 0;
            if (countTable != null && countTable.Rows.Count > 0)
            {
                totalCount = Convert.ToInt32(countTable.Rows[0][0]);
            }

            return new PaginationResult<NotificationReadDto>
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

    public async Task<NotificationReadDto> GetByIdAsync(string code)
    {
        try
        {
            string query = $@"
                SELECT *
                FROM Notifications
                WHERE Id = {code}
                  AND IsActive = True
                  AND IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Notifications not found", 200);

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<Guid>("Id"),
                UserId = row.Field<Guid>("UserId"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<Guid>("RelatedEntityId"),
                IsRead = row.Field<bool>("IsRead"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<NotificationReadDto> UpdateAsync(NotificationUpdateDto input)
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
            FROM Notifications
            WHERE Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Notifications not found", 200);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Notifications
            SET 
                UserId = '{input.UserId}', 
                LastModifiedAt = NOW(),
                LastModifiedBy = '{userId.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@"
            SELECT *
            FROM Notifications
            WHERE Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<Guid>("Id"),
                UserId = row.Field<Guid>("UserId"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<Guid>("RelatedEntityId"),
                IsRead = row.Field<bool>("IsRead"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        catch
        {
            throw;
        }
    }

}
