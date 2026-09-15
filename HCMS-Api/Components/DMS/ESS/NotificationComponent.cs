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
        // Some placeholders (e.g. Observation, the reason typed into a Reject/Revert-for-rework
        // action) come from a rich-text editor and arrive here as raw HTML, e.g.
        // "<p>reverted test</p>" -- confirmed directly against a live notification's stored
        // Message. These messages are rendered as plain text, so that HTML was never being
        // interpreted as markup by whatever built the message string; the *symptom* users saw
        // (a blank line then a lone ".") came from wherever the notification bell/toast DOES
        // render it as HTML, where </p> reads as a real paragraph break, stranding the message
        // template's own trailing "." on its own line below it. Stripping tags here (not at
        // every call site that builds a placeholders dict) fixes every scenario/placeholder in
        // one place, including any future one that happens to carry HTML through the same way.
        string Get(string key)
        {
            if (p == null || !p.TryGetValue(key, out var val)) return $"[{key}]";
            val = System.Text.RegularExpressions.Regex.Replace(val, "<[^>]*>", " ");
            return System.Text.RegularExpressions.Regex.Replace(val, @"\s+", " ").Trim();
        }

        return scenario switch
        {
            // "?tab=" matches the Pending/Approved/Rejected tab keys both my-approval-request.ts
            // and my-approval-document.ts already read via ActivatedRoute.queryParams -- without
            // it, "View Request Details" landed on whatever tab happened to be the page's
            // default rather than the one the notification is actually about.
            NotificationScenario.PendingRequest => (
                $"DMS – New Request Received (from {Get("Employee Name")})",
                $"A new request (Request ID: {Get("ID")}) is pending your approval.",
                "Request",
                "/documents/my-approvals-request?tab=Pending"
            ),
            NotificationScenario.RequestApprovedForwarded => (
                "DMS – Request Approved - Forwarded",
                $"Request ID: {Get("ID")} has been approved and requires your action.",
                "Request",
                "/documents/my-approvals-request?tab=Pending"
            ),
            NotificationScenario.RequestRejected => (
                "DMS – Request Rejected",
                $"Your request (Request ID: {Get("ID")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Request",
                "/documents/my-approvals-request?tab=Rejected"
            ),
            NotificationScenario.RequestRevertedForRework => (
                "DMS – Request Rework Required",
                $"Your request (Request ID: {Get("ID")}) has been reverted by {Get("Approver")} for rework. Reason: {Get("Observation")}.",
                "Request",
                $"/documents/my-approvals-request?tab=Rejected"
            ),
            NotificationScenario.OverdueRequestReminder => (
                "DMS – Overdue Action Required",
                $"ACTION REQUIRED: Request ID: {Get("ID")} is overdue. Please process immediately.",
                "Request",
                "/documents/my-approvals-request?tab=Pending"
            ),
            NotificationScenario.PendingDocumentApproval => (
                "DMS – New Document Pending Review",
                $"A new document ({Get("Doc Name")}, Version: {Get("V#")}) pending for your technical review.",
                "Document",
                "/documents/my-approvals-documents?tab=Pending"
            ),
            NotificationScenario.DocumentApprovedForwarded => (
                "DMS – Document Approved - Forwarded",
                $"Document {Get("Doc Name")} has been approved and requires your action/authorization.",
                "Document",
                "/documents/my-approvals-documents?tab=Pending"
            ),
            NotificationScenario.DocumentRejected => (
                "DMS – Document Rejected",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Document",
                // Was pointed at my-approvals-request -- wrong page entirely for a Document-type
                // notification, and inconsistent with its three siblings below which all
                // correctly point at my-approvals-documents.
                "/documents/my-approvals-documents?tab=Rejected"
            ),
            NotificationScenario.DocumentRevertedForRework => (
                "DMS – Document Rework Required",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been reverted by {Get("Approver")}. Please modify.",
                "Document",
                $"/documents/my-approvals-documents?tab=Rejected"
            ),
            NotificationScenario.TrainingProofRequired => (
                "DMS – Training Proof Required",
                $"Action needed: Upload Training Proof for document {Get("Doc Name")} (V:{Get("V#")}) for final authorization.",
                "Authorization",
                "/dms/authorization/post-training"
            ),
            NotificationScenario.TrainingProofSubmitted => (
                "DMS – Proof Submitted - Final Authorization",
                $"Training proof has been submitted for {Get("Doc Name")} (V:{Get("V#")}). Final authorization is now pending.",
                "Authorization",
                "/dms/authorization/post-training"
            ),
            NotificationScenario.DocumentAuthorizedEffective => (
                "DMS – Document Authorized & Effective",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) is now authorized and effective as of {Get("Date")}.",
                "Authorization",
                "/documents/trainingauthorization"
            ),
            NotificationScenario.PeriodicReviewDue => (
                "DMS – Document Review Due Soon",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) is due for review on {Get("Date")}. Please initiate a Revision Request.",
                "Review",
                "/documents/trainingauthorization"
            ),
            NotificationScenario.DocumentObsoleted => (
                "DMS – Document Obsoleted",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) has been officially obsoleted as of {Get("Date")}.",
                "Obsoletion",
                "/documents/my-approvals-documents"
            ),
            NotificationScenario.PhysicalCopyRetrievalTask => (
                "DMS – Task: Retrieve Physical Copies",
                $"ACTION REQUIRED: Retrieve and destroy physical copies for Obsoleted Document {Get("Doc Name")} (V:{Get("V#")}).",
                "Obsoletion",
                "/documents/my-approvals-documents"
            ),
            NotificationScenario.NewUserAccountCreated => (
                "DMS – Welcome to DMS",
                "Your DMS account has been created. Use your credentials to log in.",
                "Setup",
                "/dashboard"
            ),
            NotificationScenario.TransferRequestApproval => (
                "DMS – Responsibility Transfer Effective",
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

            //if (!string.IsNullOrWhiteSpace(recipientEmail))
            //{
            //    string emailBody = BuildNotificationEmailHtml(title, message, redirectionUrl);
            //    await _utilities.SendEmailAsync(recipientEmail, title, emailBody);
            //}
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
        var baseUrl = _configuration["DmsFrontendUrl"] ?? "https://testerp.atcolab.com/DMSUI/";
        if (!string.IsNullOrEmpty(actionUrl) && actionUrl != "#")
        {
            actionUrl = baseUrl.TrimEnd('/') + "/" + actionUrl.TrimStart('/');
        }
        else
        {
            actionUrl = baseUrl;
        }

        // Built for Outlook on Windows, which renders mail through Word -- not a browser engine.
        // Word ignores max-width, border-radius, linear-gradient, and padding on inline elements,
        // so anything the layout depends on is expressed the way Word understands it: table cells
        // for every padded box, bgcolor attributes alongside the CSS, explicit pixel widths, and an
        // mso-only fixed-width wrapper. Modern clients still get the rounded corners and gradient --
        // they simply read the extra properties Word skips.
        return $@"
            <!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
            <html xmlns='http://www.w3.org/1999/xhtml'>
            <head>
                <meta charset='utf-8'>
                <meta http-equiv='X-UA-Compatible' content='IE=edge'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>{title}</title>
            </head>
            <body style='margin: 0; padding: 0; background-color: #f1f5f9; -webkit-font-smoothing: antialiased;'>
                <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' bgcolor='#f1f5f9' style='border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; background-color: #f1f5f9;'>
                    <tr>
                        <td align='center' style='padding: 30px 10px;'>

                            <!--[if mso]>
                            <table role='presentation' width='600' cellspacing='0' cellpadding='0' border='0'><tr><td>
                            <![endif]-->

                            <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' bgcolor='#ffffff' style='border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; width: 100%; max-width: 600px; background-color: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0;'>

                                <!-- Header banner. bgcolor carries the dark background for Word and the
                                     gradient is layered on top for clients that support it. Without the
                                     solid fallback this header rendered white in Outlook, which left the
                                     white title and label invisible. -->
                                <tr>
                                    <td bgcolor='#0f172a' style='background-color: #0f172a; background-image: linear-gradient(135deg, #0f172a 0%, #1e3a8a 100%); padding: 28px 32px;'>
                                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='border-collapse: collapse;'>
                                            <tr>
                                                <td style='padding-bottom: 14px;'>
                                                    <img src='cid:{DMSUtilities.DmsLogoContentId}' alt='DMS' width='200' style='display: block; border: 0; outline: none; text-decoration: none; width: 200px; max-width: 200px; height: auto;' />
                                                </td>
                                            </tr>
                                            <tr>
                                                <td>
                                                    <table role='presentation' cellspacing='0' cellpadding='0' border='0' style='border-collapse: collapse;'>
                                                        <tr>
                                                            <td bgcolor='#1e40af' style='background-color: #1e40af; border-radius: 6px; padding: 6px 12px; font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif; font-size: 11px; font-weight: 600; letter-spacing: 0.5px; text-transform: uppercase; color: #ffffff; mso-line-height-rule: exactly; line-height: 14px;'>
                                                                Document Management System
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style='padding-top: 12px; font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif; font-size: 22px; font-weight: 700; color: #ffffff; mso-line-height-rule: exactly; line-height: 29px;'>
                                                    {title}
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>

                                <!-- Body -->
                                <tr>
                                    <td style='padding: 32px;'>

                                        <!-- Status badge -->
                                        <table role='presentation' cellspacing='0' cellpadding='0' border='0' style='border-collapse: collapse; margin-bottom: 20px;'>
                                            <tr>
                                                <td bgcolor='#eff6ff' style='background-color: #eff6ff; border: 1px solid #bfdbfe; border-radius: 20px; padding: 6px 14px; font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif; font-size: 12px; font-weight: 600; color: #1d4ed8; mso-line-height-rule: exactly; line-height: 15px;'>
                                                    Action Required
                                                </td>
                                            </tr>
                                        </table>

                                        <!-- Message. The blue accent is a 4px table cell rather than a
                                             border-left, which Word renders unreliably. -->
                                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='border-collapse: collapse; margin-bottom: 24px;'>
                                            <tr>
                                                <td width='4' bgcolor='#2563eb' style='width: 4px; background-color: #2563eb; font-size: 0; line-height: 0;'>&nbsp;</td>
                                                <td bgcolor='#f8fafc' style='background-color: #f8fafc; padding: 18px 20px; font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif; font-size: 15px; font-weight: 500; color: #1e293b; mso-line-height-rule: exactly; line-height: 24px;'>
                                                    {message}
                                                </td>
                                            </tr>
                                        </table>

                                        <!-- Action button -->
                                        {(!string.IsNullOrEmpty(redirectionUrl) ? $@"
                                        <table role='presentation' width='100%' cellspacing='0' cellpadding='0' border='0' style='border-collapse: collapse; margin-top: 28px;'>
                                            <tr>
                                                <td align='center' style='border: 1px solid #f1f5f9; border-radius: 10px; padding: 20px;'>
                                                    <table role='presentation' cellspacing='0' cellpadding='0' border='0' style='border-collapse: collapse;'>
                                                        <tr>
                                                            <td align='center' style='padding-bottom: 16px; font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif; font-size: 13px; font-weight: 500; color: #64748b; mso-line-height-rule: exactly; line-height: 18px;'>
                                                                Click the button below to review and process this request:
                                                            </td>
                                                        </tr>
                                                        <tr>
                                                            <td align='center' bgcolor='#2563eb' style='background-color: #2563eb; border: 1px solid #1d4ed8; border-radius: 8px; mso-padding-alt: 12px 30px;'>
                                                                <a href='{actionUrl}' target='_blank' style='display: inline-block; padding: 12px 30px; font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif; font-size: 14px; font-weight: 600; color: #ffffff; text-decoration: none; mso-line-height-rule: exactly; line-height: 18px;'>
                                                                    View Request Details &rarr;
                                                                </a>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                        </table>" : "")}

                                    </td>
                                </tr>

                                <!-- Footer -->
                                <tr>
                                    <td bgcolor='#f8fafc' align='center' style='background-color: #f8fafc; padding: 20px 32px; border-top: 1px solid #e2e8f0; font-family: Segoe UI, Tahoma, Geneva, Verdana, sans-serif;'>
                                        <p style='font-size: 12px; color: #64748b; margin: 0 0 6px 0; font-weight: 500; mso-line-height-rule: exactly; line-height: 16px;'>
                                            This is an automated notification from the <strong>DMS.Partners</strong>.
                                        </p>
                                        <p style='font-size: 11px; color: #94a3b8; margin: 0; mso-line-height-rule: exactly; line-height: 15px;'>
                                            Please do not reply directly to this email.
                                        </p>
                                    </td>
                                </tr>

                            </table>

                            <!--[if mso]>
                            </td></tr></table>
                            <![endif]-->

                        </td>
                    </tr>
                </table>
            </body>
            </html>";
    }

}
