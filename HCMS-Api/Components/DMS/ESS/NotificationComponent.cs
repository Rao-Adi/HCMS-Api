﻿﻿﻿using HCMS_Api.Common;
﻿using HCMS_Api.Common;
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
        //string connectionString = _configuration.GetRequiredConnectionString("DMSConnectionString");
        //_dataservice.BeginProcess(connectionString);

    }

    /// <summary>
    /// Triggers a system notification aligned with the predefined Notification Matrix.
    /// </summary>
    public async Task<bool> TriggerNotificationAsync(NotificationScenario scenario, int companyId, int relatedEntityId, string recipientUserId, Dictionary<string, string> placeholders)
    {
        try
        {
            var (title, message, relatedEntityType, redirectionUrl) = GetNotificationDetails(scenario, placeholders);

            string insertQuery = $@"
            INSERT INTO Notifications
            (   CompanyId,
                EmployeeCode,
                Title,
                Message,
                NotificationType,
                RelatedEntityType,
                RelatedEntityId,
                RedirectionUrl,
                IsRead, 
                CreatedAt
            )
            VALUES
            (
                {companyId},
                '{recipientUserId.ToString()}',
                '{title.Replace("'", "''")}',
                '{message.Replace("'", "''")}',
                {(int)scenario},
                '{relatedEntityType}',
                {relatedEntityId},
                '{redirectionUrl}',
                FALSE,
                NOW()
            )
            RETURNING Id;";

            var newIdObj = _common.ExecuteScalarQuery(insertQuery);
            int newId = newIdObj != null ? Convert.ToInt32(newIdObj) : 0;

            // Dispatch real-time event to specific User and Group (Secure Targeting)
            var payload = new
            {
                Id = newId,
                RecipientUserId = recipientUserId,
                Title = title,
                Message = message,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                RedirectionUrl = redirectionUrl,
                CreatedAt = DateTime.UtcNow
            };

            await _hubContext.Clients.User(recipientUserId).SendAsync("ReceiveNotification", payload);
            await _hubContext.Clients.Group(recipientUserId).SendAsync("ReceiveNotification", payload);

            // Dispatch Email
            await DispatchEmailNotificationAsync(companyId, recipientUserId, title, message, redirectionUrl);

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
                "/dms/approvals/requests"
            ),
            NotificationScenario.RequestApprovedForwarded => (
                "Request Approved - Forwarded",
                $"Request ID: {Get("ID")} has been approved and requires your action.",
                "Request",
                "/dms/approvals/requests"
            ),
            NotificationScenario.RequestRejected => (
                "Request Rejected",
                $"Your request (Request ID: {Get("ID")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Request",
                "/dms/my-requests"
            ),
            NotificationScenario.RequestRevertedForRework => (
                "Request Rework Required",
                $"Your request (Request ID: {Get("ID")}) has been reverted by {Get("Approver")} for rework. Reason: {Get("Observation")}.",
                "Request",
                $"/dms/requests/edit/{Get("ID")}"
            ),
            NotificationScenario.OverdueRequestReminder => (
                "Overdue Action Required",
                $"ACTION REQUIRED: Request ID: {Get("ID")} is overdue. Please process immediately.",
                "Request",
                "/dms/approvals/requests"
            ),
            NotificationScenario.PendingDocumentApproval => (
                "New Document Pending Review",
                $"A new document ({Get("Doc Name")}, Version: {Get("V#")}) is pending your technical review.",
                "Document",
                "/dms/approvals/documents"
            ),
            NotificationScenario.DocumentApprovedForwarded => (
                "Document Approved - Forwarded",
                $"Document {Get("Doc Name")} has been approved and requires your action/authorization.",
                "Document",
                "/dms/approvals/documents"
            ),
            NotificationScenario.DocumentRejected => (
                "Document Rejected",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been rejected by {Get("Approver")}. Reason: {Get("Observation")}.",
                "Document",
                "/dms/my-requests"
            ),
            NotificationScenario.DocumentRevertedForRework => (
                "Document Rework Required",
                $"Your document ({Get("Doc Name")}, Version: {Get("V#")}) has been reverted by {Get("Approver")}. Please modify.",
                "Document",
                $"/dms/documents/edit/{Get("ID")}"
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
                "/dms/my-documents"
            ),
            NotificationScenario.PeriodicReviewDue => (
                "Document Review Due Soon",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) is due for review on {Get("Date")}. Please initiate a Revision Request.",
                "Review",
                "/dms/my-documents"
            ),
            NotificationScenario.DocumentObsoleted => (
                "Document Obsoleted",
                $"Document {Get("Doc Name")} (V:{Get("V#")}) has been officially obsoleted as of {Get("Date")}.",
                "Obsoletion",
                "/dms/documents/obsoleted"
            ),
            NotificationScenario.PhysicalCopyRetrievalTask => (
                "Task: Retrieve Physical Copies",
                $"ACTION REQUIRED: Retrieve and destroy physical copies for Obsoleted Document {Get("Doc Name")} (V:{Get("V#")}).",
                "Obsoletion",
                "/dms/tasks/physical-copies"
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
                "/dms/my-documents"
            ),
            _ => ("Notification", "You have a new notification.", "General", "/")
        };
    }

     
    public async Task<NotificationReadDto> CreateAsync(NotificationCreateDto input)
    {
        try
        {  

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
                '{input.CompanyId}',
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
            WHERE n.Id = {newId}";

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
            var empDetail = await _peoplePartnersComponent.GetEmployeeByEmpIdAsync(code);
            // Check existence
            string checkQuery = $@"
                SELECT COUNT(1)
                FROM Notifications
                WHERE EmployeeCode = '{empDetail.empcode}'
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
                WHERE n.IsRead = " + (input.IsActive ? "True" : "False");

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
                throw new CustomException("Notifications not found", 200);
             

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
                EmployeeCode = '{input.EmployeeCode}', 
                LastModifiedAt = NOW(),
                LastModifiedBy = '{input.EmployeeCode.Replace("'", "''")}'
            WHERE Id = '{input.Id}'";

            bool updated = _common.ExecuteNonQuery(updateQuery);

            if (!updated)
                throw new Exception("Update failed");

            // Return updated record
            string selectQuery = $@" 
                SELECT n.*,c.Name AS Company
                    FROM Notifications n
	                LEFT JOIN Companies c ON n.CompanyId = c.Id
            WHERE n.Id = '{input.Id}'";

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
                WHERE Id = {notificationId} AND CompanyID = {CompanyId} AND EmployeeCode = '{empCode}'";

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

            string updateQuery = $"UPDATE Notifications SET IsRead = TRUE WHERE EmployeeCode = {empCode} AND CompanyID = {CompanyId} AND IsRead = FALSE";
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


    private async Task DispatchEmailNotificationAsync(int companyId, string recipientUserId, string title, string message, string redirectionUrl)
    {
        try
        {
            string emailQuery = "SELECT Email FROM tblEmployee WHERE TRIM(empCode) = @EmpCode AND CompanyId = @CompanyId AND COALESCE(Active, 1) = 1 LIMIT 1;";
            var recipientEmail = await _common.QueryFirstOrDefaultAsync<string>(emailQuery, new { EmpCode = recipientUserId.Trim(), CompanyId = companyId });

            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e1e1e1; border-radius: 8px;'>
                        <h2 style='color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px;'>{title}</h2>
                        <p style='font-size: 16px; color: #333; line-height: 1.5;'>{message}</p>
                        <p style='font-size: 14px; color: #555; margin-top: 20px;'><strong>Action Required At:</strong> {redirectionUrl}</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin-top: 30px;' />
                        <p style='font-size: 12px; color: #999; text-align: center;'>This is an automated notification from the Document Management System. Please do not reply.</p>
                    </div>";

                await _utilities.SendEmailAsync(recipientEmail, title, emailBody);
            }
        }
        catch (Exception ex)
        {
            // Catching to ensure SMTP/Email failures do NOT crash the primary Workflow/DB transactions
            Console.WriteLine($"[Email Notification Failed] User: {recipientUserId} | Error: {ex.Message}");
        }
    }

}
