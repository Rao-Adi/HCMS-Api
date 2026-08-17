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
    private readonly PeoplePartnersComponent _peoplePartnersComponent;
    public NotificationComponent(
        DMSUtilities utilities
        , DMSDataServices dataservice
        , IConfiguration configuration
        , ClientContextService clientContextService
        , IDMSDapperDataService dapper
        //, ILogger<UtilitiesController> logger
        , IHttpContextAccessor http,
        DMSCommon common,
        IHubContext<NotificationHub> hubContext,
        PeoplePartnersComponent peoplePartnersComponent
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
        _peoplePartnersComponent = peoplePartnersComponent;
    }

    /// <summary>
    /// Triggers a system notification aligned with the predefined Notification Matrix.
    /// </summary>
    public async Task<bool> TriggerNotificationAsync(NotificationScenario scenario, int companyId, int relatedEntityId, string recipientUserId, Dictionary<string, string> placeholders, IDbTransaction transaction = null)
    {
        try
        {
            string targetUserId = recipientUserId?.Trim() ?? string.Empty;

            var (title, message, relatedEntityType, redirectionUrl) = GetNotificationDetails(scenario, placeholders);

            string insertQuery = @"
            INSERT INTO Notifications
            (CompanyId, EmployeeCode, Title, Message, NotificationType, RelatedEntityType, RelatedEntityId, RedirectionUrl, IsRead, CreatedAt)
            VALUES
            (@CompanyId, @EmployeeCode, @Title, @Message, @NotificationType, @RelatedEntityType, @RelatedEntityId, @RedirectionUrl, FALSE, NOW())
            RETURNING Id;";

            int newId = await _common.ExecuteScalarAsync<int>(insertQuery, new
            {
                CompanyId = companyId,
                EmployeeCode = targetUserId,
                Title = title,
                Message = message,
                NotificationType = (int)scenario,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                RedirectionUrl = redirectionUrl
            }, transaction);

            // Dispatch real-time event to specific User and Group (Secure Targeting)
            var payload = new
            {
                id = newId,
                recipientUserId = targetUserId,
                title = title,
                message = message,
                type = "info", // Set to string so frontend toastr UI renders it correctly like SendTestNotification
                notificationType = (int)scenario,
                relatedEntityType = relatedEntityType,
                relatedEntityId = relatedEntityId,
                redirectionUrl = redirectionUrl,
                CreatedAt = DateTime.Now // Match SendTestNotification casing
            };

            // Broadcast to ALL connected clients to guarantee real-time delivery, mirroring SendTestNotification.
            // IMPORTANT: Ensure your frontend Angular code filters incoming messages by checking:
            // if (notification.recipientUserId === currentUser.employeeCode) { showToastr(); }
            //await _hubContext.Clients.All.SendAsync("ReceiveNotification", payload);
            await _hubContext.Clients.Group($"user_{targetUserId}").SendAsync("ReceiveNotification", payload);

            // Dispatch Email
            await DispatchEmailNotificationAsync(companyId, targetUserId, title, message, redirectionUrl, transaction);

            
            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private (string Title, string Message, string RelatedEntityType, string RedirectionUrl) GetNotificationDetails(NotificationScenario scenario, Dictionary<string, string> p)
    {
        string Get(string key) => p != null && p.TryGetValue(key, out var val) ? val : $"[{key}]";

        return scenario switch
        {
            NotificationScenario.PendingRequest => (
                "New Request Pending",
                $"A new request (Request ID: {Get("ID")}) is pending your approval.",
                "Request",
                "/documents/my-approvals-request"
            ),
            NotificationScenario.RequestApprovedForwarded => (
                "Request Approved - Forwarded",
                $"Request ID: {Get("ID")} has been approved and requires your action.",
                "Request",
                "/documents/my-approvals-request"
            ),
            NotificationScenario.RequestRejected => (
                "Request Rejected",
                $"Your request (Request ID: {Get("ID")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Request",
                "/documents/my-approvals-request"
            ),
            NotificationScenario.RequestRevertedForRework => (
                "Request Rework Required",
                $"Your request (Request ID: {Get("ID")}) has been reverted by {Get("Approver")} for rework. Reason: {Get("Observation")}.",
                "Request",
                $"/documents/my-approvals-request"
            ),
            NotificationScenario.OverdueRequestReminder => (
                "Overdue Action Required",
                $"ACTION REQUIRED: Request ID: {Get("ID")} is overdue. Please process immediately.",
                "Request",
                "/documents/my-approvals-request"
            ),
            NotificationScenario.PendingDocumentApproval => (
                "New Document Pending Review",
                $"A new document ({Get("Doc Name")}, Version: {Get("V#")}) pending for your technical review.",
                "Document",
                "/documents/my-approvals-documents"
            ),
            NotificationScenario.DocumentApprovedForwarded => (
                "Document Approved - Forwarded",
                $"Document {Get("Doc Name")} has been approved and requires your action/authorization.",
                "Document",
                "/documents/my-approvals-documents"
            ),
            NotificationScenario.DocumentRejected => (
                "Document Rejected",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Document",
                "/documents/my-approvals-request"
            ),
            NotificationScenario.DocumentRevertedForRework => (
                "Document Rework Required",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been reverted by {Get("Approver")}. Please modify.",
                "Document",
                $"/documents/my-approvals-documents"
            ),
            NotificationScenario.TrainingProofRequired => (
                "Training Proof Required",
                $"Action needed: Upload Training Proof for document {Get("Doc Name")} (V:{Get("V#")}) for final authorization.",
                "Authorization",
                "/dms/authorization/post-training"
            ),
            NotificationScenario.TrainingProofSubmitted => (
                "Proof Submitted - Final Authorization",
                $"Training proof has been submitted for {Get("Doc Name")} (V:{Get("V#")}). Final authorization is now pending.",
                "Authorization",
                "/dms/authorization/post-training"
            ),
            NotificationScenario.DocumentAuthorizedEffective => (
                "Document Authorized & Effective",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) is now authorized and effective as of {Get("Date")}.",
                "Authorization",
                "/documents/trainingauthorization"
            ),
            NotificationScenario.PeriodicReviewDue => (
                "Document Review Due Soon",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) is due for review on {Get("Date")}. Please initiate a Revision Request.",
                "Review",
                "/documents/trainingauthorization"
            ),
            NotificationScenario.DocumentObsoleted => (
                "Document Obsoleted",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) has been officially obsoleted as of {Get("Date")}.",
                "Obsoletion",
                "/documents/my-approvals-documents"
            ),
            NotificationScenario.PhysicalCopyRetrievalTask => (
                "Task: Retrieve Physical Copies",
                $"ACTION REQUIRED: Retrieve and destroy physical copies for Obsoleted Document {Get("Doc Name")} (V:{Get("V#")}).",
                "Obsoletion",
                "/documents/my-approvals-documents"
            ),
            NotificationScenario.NewUserAccountCreated => (
                "Welcome to DMS",
                "Your DMS account has been created. Use your credentials to log in.",
                "Setup",
                "/dashboard"
            ),
            NotificationScenario.TransferRequestApproval => (
                "Responsibility Transfer Effective",
                $"Your responsibility transfer from {Get("Emp From")} to {Get("Emp To")} is now effective from {Get("Date From")} to {Get("Date To")}.",
                "Setup",
                "/documents/my-approvals-documents"
            ),
            _ => ("Notification", "You have a new notification.", "General", "/")
        };
    }


    public async Task<NotificationReadDto> CreateAsync(NotificationCreateDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);
            // Insert (PostgreSQL syntax)
            string insertQuery = $@"
            INSERT INTO Notifications
            (   CompanyId,
                EmployeeCode,
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
                '{CompanyId}',
                '{input.EmployeeCode}',
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
             SELECT n.*, c.Name AS Company FROM Notifications n
             LEFT JOIN Companies c ON n.CompanyId = c.Id
            WHERE n.Id = {newId} AND n.CompanyId ={CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch created division");

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<int>("RelatedEntityId"),
                RedirectionUrl = row.Field<string>("RedirectionUrl"),
                IsRead = row.Field<bool>("IsRead"),
                CreatedAt = row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss")
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
            int CompanyId = int.Parse(_CompanyId);

            var empDetail = await _peoplePartnersComponent.GetEmployeeByEmpIdAsync(code);
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM Notifications
                WHERE EmployeeCode = '{empDetail.empcode}'
                  AND CompanyId = {CompanyId}
                  AND IsDeleted = False";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Notifications not found", 404);

            // Soft delete
            string deleteQuery = $@"
                UPDATE Notifications
                SET IsDeleted = False
                WHERE Id = {code} AND CompanyId ={CompanyId}";

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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            JwtArray loginUser = await _common.GetJwtUser();
            // Optionally enforce that a user only queries their own notifications
            input.SearchText = string.IsNullOrWhiteSpace(input.SearchText)
                ? loginUser.UserEmpID.ToString()
                : input.SearchText;

            var whereClause = @"WHERE n.CompanyId = " + CompanyId + " AND  n.IsRead = " + (input.IsActive ? "True" : "False");

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(n.Title) LIKE '%{search}%'
                    OR UPPER(n.Title) LIKE '%{search}%'
                )";
            }

            // Sorting (whitelisted to avoid SQL Injection)
            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "TITLE" => "n.Title",
                "CREATEDAT" => "n.CreatedAt",
                "CREATEDBY" => "n.CreatedBy",
                "LASTMODIFIEDAT" => "n.LastModifiedAt",
                "LASTMODIFIEDBY" => "n.LastModifiedBy",
                _ => "n.EmployeeCode"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";

            int offset = (input.PageNumber - 1) * input.PageSize;

            string query = $@"SELECT n.*,c.Name AS Company
                        FROM Notifications n
	                    LEFT JOIN Companies c ON n.CompanyId = c.Id
                        {whereClause}
                        ORDER BY {sortColumn} {sortDirection}
                        OFFSET {offset} ROWS FETCH NEXT {input.PageSize} ROWS ONLY;

                        SELECT COUNT(1)
                        FROM Notifications n
                        {whereClause}; ";

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
                    CompanyId = row.Field<int>("CompanyId"),
                    Company = row.Field<string>("Company"),
                    EmployeeCode = row.Table.Columns.Contains("EmployeeCode") ? row.Field<string>("EmployeeCode") : string.Empty,
                    Title = row.Table.Columns.Contains("Title") ? row.Field<string>("Title") : string.Empty,
                    Message = row.Table.Columns.Contains("Message") ? row.Field<string>("Message") : string.Empty,
                    NotificationType = row.Table.Columns.Contains("NotificationType") ? row.Field<int>("NotificationType") : 0,
                    RelatedEntityType = row.Table.Columns.Contains("RelatedEntityType") ? row.Field<string>("RelatedEntityType") : string.Empty,
                    RelatedEntityId = row.Table.Columns.Contains("RelatedEntityId") ? row.Field<int>("RelatedEntityId") : 0,
                    RedirectionUrl = row.Table.Columns.Contains("RedirectionUrl") ? row.Field<string>("RedirectionUrl") : string.Empty,
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

    public async Task<List<NotificationReadDto>> GetByIdAsync(bool isRead)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            string query = $@"
                 SELECT n.*,c.Name AS Company
                    FROM Notifications n
	                LEFT JOIN Companies c ON n.CompanyId = c.Id
                WHERE n.EmployeeCode = '{empCode}'
                  AND n.CompanyId = {CompanyId}
                  AND n.IsRead = {isRead}
                  ORDER BY n.CreatedAt DESC";

            DataTable divisionsTable = await _common.ExecuteSqlQuery(query);

            if (divisionsTable.Rows.Count == 0)
                throw new CustomException("Notifications not found", 404);


            var divisions = divisionsTable.AsEnumerable()
               .Select(row => new NotificationReadDto
               {
                   Id = row.Table.Columns.Contains("Id") ? row.Field<int>("Id") : 0,
                   CompanyId = row.Field<int>("CompanyId"),
                   Company = row.Field<string>("Company"),
                   EmployeeCode = row.Table.Columns.Contains("EmployeeCode") ? row.Field<string>("EmployeeCode") : string.Empty,
                   Title = row.Table.Columns.Contains("Title") ? row.Field<string>("Title") : string.Empty,
                   Message = row.Table.Columns.Contains("Message") ? row.Field<string>("Message") : string.Empty,
                   NotificationType = row.Table.Columns.Contains("NotificationType") ? row.Field<int>("NotificationType") : 0,
                   RelatedEntityType = row.Table.Columns.Contains("RelatedEntityType") ? row.Field<string>("RelatedEntityType") : string.Empty,
                   RelatedEntityId = row.Table.Columns.Contains("RelatedEntityId") ? row.Field<int>("RelatedEntityId") : 0,
                   RedirectionUrl = row.Table.Columns.Contains("RedirectionUrl") ? row.Field<string>("RedirectionUrl") : string.Empty,
                   IsRead = row.Table.Columns.Contains("IsRead") && row.Field<bool?>("IsRead") == true,
                   CreatedAt = (row.Table.Columns.Contains("CreatedAt") && !row.IsNull("CreatedAt"))
                               ? row.Field<DateTime>("CreatedAt").ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
               })
               .ToList();

            return divisions;
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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            if (input.Id < 0)
                throw new CustomException("Invalid division code.", 200);

            // Check existence (Id is VARCHAR → must be quoted)
            string checkQuery = $@"
            SELECT COUNT(1)
            FROM Notifications
            WHERE Id = {input.Id}
              AND CompanyId = {CompanyId}
              AND IsDeleted = FALSE";

            int exists = Convert.ToInt32(_common.ExecuteScalarQuery(checkQuery));

            if (exists == 0)
                throw new CustomException("Notifications not found", 404);

            // Update (PostgreSQL boolean + timestamp)
            string updateQuery = $@"
            UPDATE Notifications
            SET 
                EmployeeCode = '{input.EmployeeCode}', 
                LastModifiedAt = NOW(),
                LastModifiedBy = '{input.EmployeeCode.Replace("'", "''")}'
            WHERE Id = {input.Id} AND CompanyId = {CompanyId}";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@" 
                SELECT n.*,c.Name AS Company
                    FROM Notifications n
	                LEFT JOIN Companies c ON n.CompanyId = c.Id
            WHERE n.Id = {input.Id} AND n.CompanyId = {CompanyId}";

            DataTable dt = await _common.ExecuteSqlQuery(selectQuery);

            if (dt == null || dt.Rows.Count == 0)
                throw new Exception("Failed to fetch updated division");

            DataRow row = dt.Rows[0];

            return new NotificationReadDto
            {
                Id = row.Field<int>("Id"),
                CompanyId = row.Field<int>("CompanyId"),
                Company = row.Field<string>("Company"),
                EmployeeCode = row.Field<string>("EmployeeCode"),
                Title = row.Field<string>("Title"),
                Message = row.Field<string>("Message"),
                NotificationType = row.Field<int>("NotificationType"),
                RelatedEntityType = row.Field<string>("RelatedEntityType"),
                RelatedEntityId = row.Field<int>("RelatedEntityId"),
                RedirectionUrl = row.Field<string>("RedirectionUrl"),
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

            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            string updateQuery = $@"
                UPDATE Notifications 
                SET IsRead = TRUE 
                WHERE Id = {notificationId} AND CompanyId = {CompanyId} AND EmployeeCode = '{empCode}'";

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
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            string updateQuery = $"UPDATE Notifications SET IsRead = TRUE WHERE EmployeeCode = '{empCode}' AND CompanyID = {CompanyId} AND IsRead = FALSE";
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
                CreatedAt = DateTime.Now
            });

            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }


    private async Task DispatchEmailNotificationAsync(int companyId, string recipientUserId, string title, string message, string redirectionUrl, IDbTransaction transaction = null)
    {
        try
        {
            string emailQuery = "SELECT Email FROM tblEmployee WHERE LTRIM(RTRIM(empCode::text), '0') = LTRIM(RTRIM(@EmpCode::text), '0') AND CompanyId = @CompanyId AND COALESCE(Active, 1) = 1 LIMIT 1;";
            var recipientEmail = await _common.QueryFirstOrDefaultAsync<string>(emailQuery, new { EmpCode = recipientUserId, CompanyId = companyId }, transaction);

            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                string emailBody = BuildNotificationEmailHtml(title, message, redirectionUrl);
                await _utilities.SendEmailAsync(recipientEmail, title, emailBody);
            }
        }
        catch (Exception ex)
        {
            // Catching to ensure SMTP/Email failures do NOT crash the primary Workflow/DB transactions
            Console.WriteLine($"[Email Notification Failed] User: {recipientUserId} | Error: {ex.Message}");
        }
    }

    private string BuildNotificationEmailHtml(string title, string message, string redirectionUrl)
    {
        string actionUrl = redirectionUrl ?? "";
        var baseUrl = _configuration["DmsFrontendUrl"] ?? "https://testerp.atcolab.com/";
        if (!string.IsNullOrEmpty(actionUrl) && actionUrl != "#")
        {
            actionUrl = baseUrl.TrimEnd('/') + "/" + actionUrl.TrimStart('/');
        }
        else
        {
            actionUrl = baseUrl;
        }

        return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>{title}</title>
            </head>
            <body style='margin: 0; padding: 0; background-color: #f1f5f9; font-family: ""Segoe UI"", Tahoma, Geneva, Verdana, sans-serif; -webkit-font-smoothing: antialiased;'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='background-color: #f1f5f9; padding: 30px 10px;'>
                    <tr>
                        <td align='center'>
                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='max-width: 600px; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0, 0, 0, 0.05); border: 1px solid #e2e8f0;'>
                    
                                <!-- Header Banner -->
                                <tr>
                                    <td style='background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 100%); padding: 28px 32px; text-align: left;'>
                                        <table width='100%' cellspacing='0' cellpadding='0' border='0'>
                                            <tr>
                                                <td>
                                                    <div style='display: inline-block; background-color: rgba(255, 255, 255, 0.15); border-radius: 6px; padding: 5px 12px; color: #ffffff; font-size: 11px; font-weight: 600; letter-spacing: 0.5px; text-transform: uppercase;'>
                                                        Document Management System
                                                    </div>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='padding-top: 12px;'>
                                                    <h1 style='color: #ffffff; font-size: 22px; font-weight: 700; margin: 0; line-height: 1.3;'>{title}</h1>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>

                                <!-- Body Content -->
                                <tr>
                                    <td style='padding: 32px; color: #334155;'>
                            
                                        <!-- Status Badge -->
                                        <div style='margin-bottom: 20px;'>
                                            <span style='background-color: #eff6ff; color: #1d4ed8; border: 1px solid #bfdbfe; padding: 5px 14px; border-radius: 20px; font-size: 12px; font-weight: 600; display: inline-block;'>
                                                Action Required
                                            </span>
                                        </div>

                                        <!-- Notification Message Box -->
                                        <div style='background-color: #f8fafc; border-left: 4px solid #2563eb; border-radius: 0 8px 8px 0; padding: 18px 20px; margin-bottom: 24px;'>
                                            <p style='margin: 0; font-size: 15px; color: #1e293b; line-height: 1.6; font-weight: 500;'>
                                                {message}
                                            </p>
                                        </div>

                                        <!-- Action Button Section -->
                                        {(!string.IsNullOrEmpty(redirectionUrl) ? $@"
                                        <div style='margin-top: 28px; text-align: center; background-color: #ffffff; border: 1px solid #f1f5f9; padding: 20px; border-radius: 10px;'>
                                            <p style='font-size: 13px; color: #64748b; margin-top: 0; margin-bottom: 16px; font-weight: 500;'>
                                                Click the button below to review and process this request:
                                            </p>
                                            <a href='{actionUrl}' target='_blank' style='display: inline-block; background-color: #2563eb; background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); color: #ffffff; text-decoration: none; font-weight: 600; font-size: 14px; padding: 12px 30px; border-radius: 8px; box-shadow: 0 4px 12px rgba(37, 99, 235, 0.25); border: 1px solid #1d4ed8;'>
                                                View Request Details &rarr;
                                            </a>
                                            <p style='font-size: 11px; color: #94a3b8; margin-top: 14px; margin-bottom: 0; word-break: break-all;'>
                                                Target path: <span style='font-family: monospace; color: #475569;'>{redirectionUrl}</span>
                                            </p>
                                        </div>" : "")}

                                    </td>
                                </tr>

                                <!-- Footer -->
                                <tr>
                                    <td style='background-color: #f8fafc; padding: 20px 32px; border-top: 1px solid #e2e8f0; text-align: center;'>
                                        <p style='font-size: 12px; color: #64748b; margin: 0 0 6px 0; font-weight: 500;'>
                                            This is an automated notification from the <strong>Document Management System (DMS)</strong>.
                                        </p>
                                        <p style='font-size: 11px; color: #94a3b8; margin: 0;'>
                                            Please do not reply directly to this email.
                                        </p>
                                    </td>
                                </tr>

                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";
    }

}
