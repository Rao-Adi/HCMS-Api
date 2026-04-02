using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using HCMS_Api.Components.DMS.Common.Models.Enums;
using HCMS_Api.Models;
using Microsoft.AspNetCore.SignalR;
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
    private readonly IHubContext<NotificationHub> _hubContext;
    public NotificationComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        //, ILogger<UtilitiesController> logger
        , IHttpContextAccessor http,
        DMSCommon common,
        IHubContext<NotificationHub> hubContext
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
        _hubContext = hubContext;
        string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        _dataservice.BeginProcess(connectionString);

    }

    /// <summary>
    /// Triggers a system notification aligned with the predefined Notification Matrix.
    /// </summary>
    public async Task<bool> TriggerNotificationAsync(NotificationScenario scenario, string companyId, int relatedEntityId, int recipientUserId, Dictionary<string, string> placeholders)
    {
        try
        {
            var (title, message, relatedEntityType, redirectionUrl) = GetNotificationDetails(scenario, placeholders);

            string insertQuery = $@"
            INSERT INTO Notifications
            (   CompanyId,
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
                {companyId},
                {recipientUserId},
                '{title.Replace("'", "''")}',
                '{message.Replace("'", "''")}',
                {(int)scenario},
                '{relatedEntityType}',
                {relatedEntityId},
                FALSE,
                NOW()
            )
            RETURNING Id;";

            _common.ExecuteScalarQuery(insertQuery);
            var newIdObj = _common.ExecuteScalarQuery(insertQuery);
            int newId = newIdObj != null && newIdObj != null ? Convert.ToInt32(newIdObj) : 0;

            // Dispatch real-time event
            await _hubContext.Clients.User(recipientUserId.ToString()).SendAsync("ReceiveNotification", new
            {
                Id = newId,
                Title = title,
                Message = message,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                RedirectionUrl = redirectionUrl,
                CreatedAt = DateTime.UtcNow
            });

            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private (string Title, string Message, string RelatedEntityType, string RedirectionUrl) GetNotificationDetails(NotificationScenario scenario, Dictionary<string, string> p)
    {
        string Get(string key) => p.TryGetValue(key, out var val) ? val : $"[{key}]";

        return scenario switch
        {
            NotificationScenario.PendingRequest => (
                "New Request Pending",
                $"A new request (Request ID: {Get("ID")}) is pending your approval.",
                "Request",
                "My Approvals - Request for Document Creation/Update"
            ),
            NotificationScenario.RequestApprovedForwarded => (
                "Request Approved - Forwarded",
                $"Request ID: {Get("ID")} has been approved and requires your action.",
                "Request",
                "My Approvals - Request for Document Creation/Update"
            ),
            NotificationScenario.RequestRejected => (
                "Request Rejected",
                $"Your request (Request ID: {Get("ID")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Request",
                "My Request Pending Approval"
            ),
            NotificationScenario.RequestRevertedForRework => (
                "Request Rework Required",
                $"Your request (Request ID: {Get("ID")}) has been reverted by {Get("Approver")} for rework. Reason: {Get("Observation")}.",
                "Request",
                "Request for Document Creation/Update (Edit View)"
            ),
            NotificationScenario.OverdueRequestReminder => (
                "Overdue Action Required",
                $"ACTION REQUIRED: Request ID: {Get("ID")} is overdue. Please process immediately.",
                "Request",
                "My Approvals - Request for Document Creation/Update"
            ),
            NotificationScenario.PendingDocumentApproval => (
                "New Document Pending Review",
                $"A new document ({Get("Doc Name")}, Version: {Get("V#")}) is pending your technical review.",
                "Document",
                "My Approvals - Documents"
            ),
            NotificationScenario.DocumentApprovedForwarded => (
                "Document Approved - Forwarded",
                $"Document {Get("Doc Name")} has been approved and requires your action/authorization.",
                "Document",
                "My Approvals - Documents / Document Authorization - Post Training"
            ),
            NotificationScenario.DocumentRejected => (
                "Document Rejected",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Document",
                "My Request Pending Approval"
            ),
            NotificationScenario.DocumentRevertedForRework => (
                "Document Rework Required",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been reverted by {Get("Approver")}. Please modify.",
                "Document",
                "Upload or Create Document (Edit View)"
            ),
            NotificationScenario.TrainingProofRequired => (
                "Training Proof Required",
                $"Action needed: Upload Training Proof for document {Get("Doc Name")} (V:{Get("V#")}) for final authorization.",
                "Authorization",
                "Document Authorization - Post Training"
            ),
            NotificationScenario.TrainingProofSubmitted => (
                "Proof Submitted - Final Authorization",
                $"Training proof has been submitted for {Get("Doc Name")} (V:{Get("V#")}). Final authorization is now pending.",
                "Authorization",
                "Document Authorization - Post Training"
            ),
            NotificationScenario.DocumentAuthorizedEffective => (
                "Document Authorized & Effective",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) is now authorized and effective as of {Get("Date")}.",
                "Authorization",
                "My Documents"
            ),
            NotificationScenario.PeriodicReviewDue => (
                "Document Review Due Soon",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) is due for review on {Get("Date")}. Please initiate a Revision Request.",
                "Review",
                "My Documents"
            ),
            NotificationScenario.DocumentObsoleted => (
                "Document Obsoleted",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) has been officially obsoleted as of {Get("Date")}.",
                "Obsoletion",
                "N/A"
            ),
            NotificationScenario.PhysicalCopyRetrievalTask => (
                "Task: Retrieve Physical Copies",
                $"ACTION REQUIRED: Retrieve and destroy physical copies for Obsoleted Document {Get("Doc Name")} (V:{Get("V#")}).",
                "Obsoletion",
                "N/A"
            ),
            NotificationScenario.NewUserAccountCreated => (
                "Welcome to DMS",
                "Your DMS account has been created. Use your credentials to log in.",
                "Setup",
                "Login Screen/DMS Dashboard"
            ),
            NotificationScenario.TransferRequestApproval => (
                "Responsibility Transfer Effective",
                $"Your responsibility transfer from {Get("Emp From")} to {Get("Emp To")} is now effective from {Get("Date From")} to {Get("Date To")}.",
                "Setup",
                "My Documents/Tasks"
            ),
            _ => ("Notification", "You have a new notification.", "General", "")
        };
    }

     
    public async Task<NotificationReadDto> CreateAsync(NotificationCreateDto input)
    {
        try
        {
            //var clientIp = _clientContextService.GetClientIP();
            //var prefix = _utilities.GetPrefix(clientIp);
            var userId = "manual"; //_utilities.GetUserid(prefix);
            if (input.Id < 0)
                throw new CustomException("Notifications code is required.", 400);

            // Check duplicate by Id OR UserId
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Notifications
            WHERE (Id = '{input.Id}'
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists > 0)
                throw new CustomException("Notifications already exists", 409);

            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Notifications
            (   CompanyId,
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
                '{input.CompanyId}',
                '{input.UserId}',
                '{input.Title}',
                '{input.Message}',
                '{input.NotificationType}',
                '{input.RelatedEntityType}',
                '{input.RelatedEntityId}',
                '{input.IsRead}',
                NOW()
            )
            RETURNING Id;";

            int newId = Convert.ToInt32(_common.ExecuteScalarQuery(insertQuery));

            // Fetch inserted record
            string selectQuery = $@"
             SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                    FROM Notifications n
                    LEFT JOIN Companies c
                    ON d.CompanyId = c.Id
            WHERE n.Id = {newId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                UserId = row.Field<int>("UserId"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<int>("RelatedEntityId"),
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
            JwtArray loginUser = await _common.GetJwtUser();
            // Optionally enforce that a user only queries their own notifications
            input.SearchText = string.IsNullOrWhiteSpace(input.SearchText)
                ? loginUser.UserEmpID.ToString()
                : input.SearchText;

            var whereClause = @"
                WHERE n.IsDeleted = False 
                  AND n.IsActive = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(n.UserId) LIKE '%{search}%'
                    OR UPPER(n.Id) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "n.UserId",
                "CODE" => "n.Id",
                "ISACTIVE" => "n.IsActive",
                _ => "n.UserId"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"
                         SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                            FROM Notifications n
                            LEFT JOIN Companies c
                            ON d.CompanyId = c.Id
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
                    Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                    CompanyId = row.Field<Int64>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    UserId = row.Table.Columns.Contains("UserId") ? row.Field<int>("UserId") : 0,
                    Title = row.Table.Columns.Contains("Title") ? row.Field<string>("Title") : string.Empty,
                    Message = row.Table.Columns.Contains("Message") ? row.Field<string>("Message") : string.Empty,
                    NotificationType = row.Table.Columns.Contains("NotificationType") ? row.Field<int>("NotificationType") : 0,
                    RelatedEntityType = row.Table.Columns.Contains("RelatedEntityType") ? row.Field<string>("RelatedEntityType") : string.Empty,
                    RelatedEntityId = row.Table.Columns.Contains("RelatedEntityId") ? row.Field<int>("RelatedEntityId") : 0,
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
                 SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                    FROM Notifications n
                    LEFT JOIN Companies c
                    ON d.CompanyId = c.Id
                WHERE n.Id = {code}
                  AND n.IsActive = True
                  AND n.IsDeleted = False";

            DataTable dt = await _common.ExecuteSqlQuery(query);

            if (dt.Rows.Count == 0)
                throw new CustomException("Notifications not found", 200);

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                UserId = row.Field<int>("UserId"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<int>("RelatedEntityId"),
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
            if (input.Id < 0)
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
                SELECT dt.*, c.Id AS CompanyId, c.Name AS Company
                    FROM Notifications n
                    LEFT JOIN Companies c
                    ON d.CompanyId = c.Id
            WHERE n.Id = '{input.Id}'";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<Int64>("CompanyId"),
                Company = row.Field<string>("Company"),
                UserId = row.Field<int>("UserId"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<int>("RelatedEntityId"),
                IsRead = row.Field<bool>("IsRead"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
        catch
        {
            throw;
        }
    }

    public async Task<bool> MarkAsReadAsync(int notificationId)
    {
        try
        {

            JwtArray loginUser = await _common.GetJwtUser();

            string updateQuery = $@"
                UPDATE Notifications 
                SET IsRead = TRUE 
                WHERE Id = {notificationId} AND UserId = {loginUser.UserID}";

            return _common.ExecuteNonQuery(updateQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> MarkAllAsReadAsync()
    {
        try
        {
            JwtArray loginUser = await _common.GetJwtUser();
            string updateQuery = $"UPDATE Notifications SET IsRead = TRUE WHERE UserId = {loginUser.UserID} AND IsRead = FALSE";
            return _common.ExecuteNonQuery(updateQuery);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> SendTestNotification(string title, string message, string type)
    {
        try
        {
            // Dispatch real-time event
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", new
            {
                title = title,
                message = message,
                type = type,
                CreatedAt = DateTime.UtcNow
            });

            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }

}
