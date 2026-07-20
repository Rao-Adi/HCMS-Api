using Dapper;
using HCMS_Api.Common;
using HCMS_Api.Common.DMS;
using HCMS_Api.Common.Misc;
using HCMS_Api.Components.DMS.Common;
using HCMS_Api.Components.DMS.Common.Dapper;
using HCMS_Api.Components.DMS.Common.DataAccess;
using HCMS_Api.Components.DMS.Common.Models;
using System.Data;

namespace HCMS_Api.Components.DMS.ESS;
 
public class CustomizeEmailAlertsComponent
{

    private readonly DMSUtilities _utilities;
    private readonly DMSDataServices _dataservice;
    private readonly IConfiguration _configuration;
    private readonly ClientContextService _clientContextService;
    private readonly IDMSDapperDataService _dapperService;
    //private readonly ILogger<UtilitiesController> _logger;
    private readonly IHttpContextAccessor _http;
    private readonly DMSCommon _common;
    private static readonly Dictionary<int, DateTime> _lastRunTimes = new Dictionary<int, DateTime>();

    public CustomizeEmailAlertsComponent(
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


    public async Task<CustomizeEmailAlertReadDto> CreateAsync(CustomizeEmailAlertCreateDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            var clientIp = _clientContextService.GetClientIP();
            int CompanyId = int.Parse(_CompanyId);
            var empId = _utilities.GetEmpid(clientIp);
            var empCode = _utilities.GetEmpCodeForHCMS(empId.ToString());

            // Check duplicate by Name
            string checkQuery = @"
            SELECT COUNT(1)
            FROM alert_configs
            WHERE name = @Name
              AND company_id = @CompanyId";

            int exists = await _common.ExecuteScalarAsync<int>(checkQuery, new { Name = input.Name, CompanyId }, tx);

            if (exists > 0)
                throw new CustomException("Email Alert Configuration already exists", 409);

            // Insert into alert_configs
            string insertQuery = @"
            INSERT INTO alert_configs
            (
                company_id, name, is_active, pending_state, pending_days, review_within_days, inactive_days,
                status_changed_to, lookback_days, frequency, send_at_time, delivery_format, email_subject, email_body,
                created_by, created_at, updated_at
            )
            VALUES
            (
                @CompanyId, @Name, @IsActive, @PendingState, @PendingDays, @ReviewWithinDays, @InactiveDays,
                @StatusChangedTo, @LookbackDays, @Frequency, @SendAtTime::time, @DeliveryFormat, @EmailSubject, @EmailBody,
                @CreatedBy, NOW(), NOW()
            )
            RETURNING Id;";

            int newId = await _common.ExecuteScalarAsync<int>(insertQuery, new
            {
                CompanyId,
                input.Name,
                IsActive = input.IsActive,
                input.PendingState,
                input.PendingDays,
                input.ReviewWithinDays,
                input.InactiveDays,
                input.StatusChangedTo,
                input.LookbackDays,
                input.Frequency,
                SendAtTime = input.SendAtTime,
                input.DeliveryFormat,
                input.EmailSubject,
                input.EmailBody,
                CreatedBy = empCode
            }, tx);

            // Insert Scopes
            if (input.Scopes != null && input.Scopes.Any())
            {
                string scopeQuery = @"
                INSERT INTO alert_document_scopes (alert_config_id, company_id, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode)
                VALUES (@AlertConfigId, @CompanyId, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode);";

                foreach (var scope in input.Scopes)
                {
                    await _common.ExecuteAsync(scopeQuery, new {
                        AlertConfigId = newId,
                        CompanyId,
                        scope.DivisionCode,
                        scope.DepartmentCode,
                        scope.SubDepartmentCode,
                        scope.BusinessDomainCode
                    }, tx);
                }
            }

            // Insert Recipients
            if (input.Recipients != null && input.Recipients.Any())
            {
                string recipientQuery = @"
                INSERT INTO alert_recipients (alert_config_id, email_address, recipient_type, is_self)
                VALUES (@AlertConfigId, @EmailAddress, @RecipientType, @IsSelf);";

                foreach (var recipient in input.Recipients)
                {
                    await _common.ExecuteAsync(recipientQuery, new {
                        AlertConfigId = newId,
                        recipient.EmailAddress,
                        recipient.RecipientType,
                        recipient.IsSelf
                    }, tx);
                }
            }

            await tx.CommitAsync();

            return await GetByIdAsync(newId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }


    public async Task<bool> DeleteAsync(int id)
    {
        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            // Check existence
            string checkQuery = @"
                SELECT COUNT(1)
                FROM alert_configs
                WHERE Id = @Id
                  AND company_id = @CompanyId";

            int exists = await _common.ExecuteScalarAsync<int>(checkQuery, new { Id = id, CompanyId }, tx);

            if (exists == 0)
                throw new CustomException("Email Alert Configuration not found", 404);

            // Explicitly delete from cascade tables
            await _common.ExecuteAsync("DELETE FROM alert_document_scopes WHERE alert_config_id = @Id", new { Id = id }, tx);
            await _common.ExecuteAsync("DELETE FROM alert_recipients WHERE alert_config_id = @Id", new { Id = id }, tx);

            // Delete main config
            string deleteQuery = @"
                DELETE FROM alert_configs
                WHERE Id = @Id AND company_id = @CompanyId";

            await _common.ExecuteAsync(deleteQuery, new { Id = id, CompanyId }, tx);
            
            await tx.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
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
            SELECT Id, name AS Name
            FROM alert_configs
            WHERE is_active = True
              AND company_id = @CompanyId
            ORDER BY Id";

            var list = await _common.QueryAsync<SelectList2Dto>(query, new { CompanyId });
            return list.AsQueryable();
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<PaginationResult<CustomizeEmailAlertReadDto>> GetAllAsync(TableFiltersDto input)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            var whereClause = @"
                WHERE ac.company_id = @CompanyId
                  AND ac.is_active = @IsActive";

            // Search
            if (!string.IsNullOrWhiteSpace(input.SearchText))
            {
                var search = input.SearchText.Replace("'", "''").ToUpper();
                whereClause += $@"
                AND (
                    UPPER(ac.name) LIKE '%{search}%'
                )";
            }

            string sortColumn = input.SortColumn?.ToUpper() switch
            {
                "NAME" => "ac.name",
                "FREQUENCY" => "ac.frequency",
                "CREATEDAT" => "ac.CreatedAt",
                "CREATEDBY" => "ac.CreatedBy",
                "LASTMODIFIEDAT" => "ac.LastModifiedAt",
                "LASTMODIFIEDBY" => "ac.LastModifiedBy",
                "ISACTIVE" => "ac.is_active",
                _ => "ac.id"
            };

            string sortDirection = input.SortBy?.ToUpper() == "DESC" ? "DESC" : "ASC";
            int offset = (input.PageNumber - 1) * input.PageSize;

            string dataSql = $@"
                SELECT ac.id, ac.company_id AS CompanyId, ac.name, ac.is_active AS IsActive,
                       ac.pending_state AS PendingState, ac.pending_days AS PendingDays,
                       ac.review_within_days AS ReviewWithinDays, ac.inactive_days AS InactiveDays,
                       ac.status_changed_to AS StatusChangedTo, ac.lookback_days AS LookbackDays,
                       ac.frequency, ac.send_at_time::text AS SendAtTime, ac.delivery_format AS DeliveryFormat,
                       ac.email_subject AS EmailSubject, ac.email_body AS EmailBody,
                       ac.created_by AS CreatedBy, ac.created_at AS CreatedAt, ac.updated_at AS UpdatedAt
                FROM alert_configs ac
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            string countSql = $@"
                SELECT COUNT(1)
                FROM alert_configs ac
                {whereClause};";

            var queryParams = new { CompanyId, IsActive = input.IsActive, Offset = offset, PageSize = input.PageSize };

            var items = (await _common.QueryAsync<CustomizeEmailAlertReadDto>(dataSql, queryParams)).ToList();
            var totalCount = await _common.ExecuteScalarAsync<int>(countSql, queryParams);

            return new PaginationResult<CustomizeEmailAlertReadDto>
            {
                Items = items,
                TotalCount = totalCount
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<CustomizeEmailAlertReadDto> GetByIdAsync(int id)
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = @"
                SELECT ac.id, ac.company_id AS CompanyId, ac.name, ac.is_active AS IsActive,
                       ac.pending_state AS PendingState, ac.pending_days AS PendingDays,
                       ac.review_within_days AS ReviewWithinDays, ac.inactive_days AS InactiveDays,
                       ac.status_changed_to AS StatusChangedTo, ac.lookback_days AS LookbackDays,
                       ac.frequency, ac.send_at_time::text AS SendAtTime, ac.delivery_format AS DeliveryFormat,
                       ac.email_subject AS EmailSubject, ac.email_body AS EmailBody,
                       ac.created_by AS CreatedBy, ac.created_at AS CreatedAt, ac.updated_at AS UpdatedAt
                FROM alert_configs ac
                WHERE ac.Id = @Id AND ac.company_id = @CompanyId";

            var alert = await _common.QueryFirstOrDefaultAsync<CustomizeEmailAlertReadDto>(query, new { Id = id, CompanyId });

            if (alert == null)
                throw new CustomException("Email Alert Configuration not found", 404);

            // Fetch Scopes
            string scopeQuery = "SELECT DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode FROM alert_document_scopes WHERE alert_config_id = @Id";
            alert.Scopes = (await _common.QueryAsync<AlertDocumentScopeDto>(scopeQuery, new { Id = id })).ToList();

            // Fetch Recipients
            string recipientQuery = "SELECT email_address AS EmailAddress, recipient_type AS RecipientType, is_self AS IsSelf FROM alert_recipients WHERE alert_config_id = @Id";
            alert.Recipients = (await _common.QueryAsync<AlertRecipientDto>(recipientQuery, new { Id = id })).ToList();

            return alert;
        }
        catch (Exception)
        {
            throw;
        }
    }


    public async Task<CustomizeEmailAlertReadDto> UpdateAsync(CustomizeEmailAlertUpdateDto input)
    {
        await using var tx = await _common.BeginTransactionAsync();
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            // Check existence
            string checkQuery = @"
            SELECT COUNT(1)
            FROM alert_configs
            WHERE Id = @Id AND company_id = @CompanyId";

            int exists = await _common.ExecuteScalarAsync<int>(checkQuery, new { input.Id, CompanyId }, tx);

            if (exists == 0)
                throw new CustomException("Email Alert Configuration not found", 404);

            // Update alert_configs
            string updateQuery = @"
            UPDATE alert_configs
            SET 
                name = @Name,
                is_active = @IsActive,
                pending_state = @PendingState,
                pending_days = @PendingDays,
                review_within_days = @ReviewWithinDays,
                inactive_days = @InactiveDays,
                status_changed_to = @StatusChangedTo,
                lookback_days = @LookbackDays,
                frequency = @Frequency,
                send_at_time = @SendAtTime::time,
                delivery_format = @DeliveryFormat,
                email_subject = @EmailSubject,
                email_body = @EmailBody,
                updated_at = NOW()
            WHERE Id = @Id AND company_id = @CompanyId";

            await _common.ExecuteAsync(updateQuery, new
            {
                input.Name,
                input.IsActive,
                input.PendingState,
                input.PendingDays,
                input.ReviewWithinDays,
                input.InactiveDays,
                input.StatusChangedTo,
                input.LookbackDays,
                input.Frequency,
                input.SendAtTime,
                input.DeliveryFormat,
                input.EmailSubject,
                input.EmailBody,
                input.Id,
                CompanyId
            }, tx);

            // Delete old scopes and recipients
            await _common.ExecuteAsync("DELETE FROM alert_document_scopes WHERE alert_config_id = @Id", new { input.Id }, tx);
            await _common.ExecuteAsync("DELETE FROM alert_recipients WHERE alert_config_id = @Id", new { input.Id }, tx);

            // Insert Scopes
            if (input.Scopes != null && input.Scopes.Any())
            {
                string scopeQuery = @"
                INSERT INTO alert_document_scopes (alert_config_id, company_id, DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode)
                VALUES (@AlertConfigId, @CompanyId, @DivisionCode, @DepartmentCode, @SubDepartmentCode, @BusinessDomainCode);";

                foreach (var scope in input.Scopes)
                {
                    await _common.ExecuteAsync(scopeQuery, new {
                        AlertConfigId = input.Id,
                        CompanyId,
                        scope.DivisionCode,
                        scope.DepartmentCode,
                        scope.SubDepartmentCode,
                        scope.BusinessDomainCode
                    }, tx);
                }
            }

            // Insert Recipients
            if (input.Recipients != null && input.Recipients.Any())
            {
                string recipientQuery = @"
                INSERT INTO alert_recipients (alert_config_id, email_address, recipient_type, is_self)
                VALUES (@AlertConfigId, @EmailAddress, @RecipientType, @IsSelf);";

                foreach (var recipient in input.Recipients)
                {
                    await _common.ExecuteAsync(recipientQuery, new {
                        AlertConfigId = input.Id,
                        recipient.EmailAddress,
                        recipient.RecipientType,
                        recipient.IsSelf
                    }, tx);
                }
            }

            await tx.CommitAsync();

            return await GetByIdAsync(input.Id);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<int> GetCount()
    {
        try
        {
            string _CompanyId = _utilities.GetCompanyId(_clientContextService.GetClientIP());
            int CompanyId = int.Parse(_CompanyId);

            string query = "SELECT COUNT(1) FROM alert_configs WHERE company_id = @CompanyId";
            return await _common.ExecuteScalarAsync<int>(query, new { CompanyId });
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task ProcessScheduledEmailAlertsAsync()
    {
        string query = @"
            SELECT ac.id, ac.company_id AS CompanyId, ac.name, ac.is_active AS IsActive,
                   ac.pending_state AS PendingState, ac.pending_days AS PendingDays,
                   ac.review_within_days AS ReviewWithinDays, ac.inactive_days AS InactiveDays,
                   ac.status_changed_to AS StatusChangedTo, ac.lookback_days AS LookbackDays,
                   ac.frequency, ac.send_at_time::text AS SendAtTime, ac.delivery_format AS DeliveryFormat,
                   ac.email_subject AS EmailSubject, ac.email_body AS EmailBody
            FROM alert_configs ac
            WHERE ac.is_active = TRUE";

        var alerts = await _common.QueryAsync<CustomizeEmailAlertReadDto>(query, new { });
        var currentTime = DateTime.Now;

        foreach (var alert in alerts)
        {
            try
            {
                if (!ShouldRunAlert(alert, currentTime)) continue;

                var scopes = await _common.QueryAsync<AlertDocumentScopeDto>(
                    "SELECT DivisionCode, DepartmentCode, SubDepartmentCode, BusinessDomainCode FROM alert_document_scopes WHERE alert_config_id = @Id",
                    new { Id = alert.Id });

                var recipients = await _common.QueryAsync<AlertRecipientDto>(
                    "SELECT email_address AS EmailAddress, recipient_type AS RecipientType, is_self AS IsSelf FROM alert_recipients WHERE alert_config_id = @Id",
                    new { Id = alert.Id });

                var matchingDocuments = await FetchMatchingDocumentsAsync(alert, scopes);

                if (matchingDocuments.Any())
                {
                    await DispatchAlertEmailsAsync(alert, matchingDocuments, recipients);
                }

                // Record the run time to prevent duplicates today
                _lastRunTimes[alert.Id] = currentTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailAlert] Error processing alert {alert.Id}: {ex.Message}");
            }
        }
    }

    private bool ShouldRunAlert(CustomizeEmailAlertReadDto alert, DateTime currentTime)
    {
        if (string.IsNullOrWhiteSpace(alert.SendAtTime)) return false;
        if (!TimeSpan.TryParse(alert.SendAtTime, out TimeSpan sendTime)) return false;

        // Ensure we match the hour
        if (currentTime.Hour != sendTime.Hours) return false;

        if (_lastRunTimes.TryGetValue(alert.Id, out var lastRun))
        {
            // Prevent running multiple times on the same day
            if (lastRun.Date == currentTime.Date) return false;
        }

        if (!string.IsNullOrWhiteSpace(alert.Frequency))
        {
            if (alert.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase) && currentTime.DayOfWeek != DayOfWeek.Monday) return false;
            if (alert.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase) && currentTime.Day != 1) return false;
        }

        return true;
    }

    private async Task<List<dynamic>> FetchMatchingDocumentsAsync(CustomizeEmailAlertReadDto alert, IEnumerable<AlertDocumentScopeDto> scopes)
    {
        string baseQuery = @"
            SELECT d.Id, d.CompanyId, d.DocumentNumber, d.Title, d.CreatedBy, d.NextReviewDate, dt.Name as DocumentType,
                   (SELECT ds.Code FROM DocumentStateHistory dsh JOIN DocumentStates ds ON ds.Id = dsh.ToStateId WHERE dsh.DocumentId = d.Id ORDER BY dsh.ChangedAt DESC LIMIT 1) AS CurrentState,
                   (SELECT dsh.ChangedAt FROM DocumentStateHistory dsh WHERE dsh.DocumentId = d.Id ORDER BY dsh.ChangedAt DESC LIMIT 1) AS StateChangedAt
            FROM Documents d
            LEFT JOIN DocumentTypes dt ON d.DocumentTypeCode = dt.Code
            WHERE d.CompanyId = @CompanyId AND d.IsDeleted = FALSE";

        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", alert.CompanyId);

        if (scopes != null && scopes.Any())
        {
            var scopeConditions = new List<string>();
            int i = 0;
            foreach (var scope in scopes)
            {
                var conds = new List<string>();
                if (!string.IsNullOrWhiteSpace(scope.DivisionCode)) { conds.Add($"d.DivisionCode = @Div{i}"); parameters.Add($"Div{i}", scope.DivisionCode); }
                if (!string.IsNullOrWhiteSpace(scope.DepartmentCode)) { conds.Add($"d.DepartmentCode = @Dept{i}"); parameters.Add($"Dept{i}", scope.DepartmentCode); }
                if (!string.IsNullOrWhiteSpace(scope.SubDepartmentCode)) { conds.Add($"d.SubDepartmentCode = @SubDept{i}"); parameters.Add($"SubDept{i}", scope.SubDepartmentCode); }
                if (!string.IsNullOrWhiteSpace(scope.BusinessDomainCode)) { conds.Add($"d.BusinessDomainCode = @Biz{i}"); parameters.Add($"Biz{i}", scope.BusinessDomainCode); }

                if (conds.Any()) scopeConditions.Add("(" + string.Join(" AND ", conds) + ")");
                i++;
            }
            if (scopeConditions.Any())
            {
                baseQuery += " AND (" + string.Join(" OR ", scopeConditions) + ")";
            }
        }

        var ruleConditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(alert.PendingState) && alert.PendingDays.HasValue)
        {
            ruleConditions.Add($@"(
                (SELECT ds.Code FROM DocumentStateHistory dsh JOIN DocumentStates ds ON ds.Id = dsh.ToStateId WHERE dsh.DocumentId = d.Id ORDER BY dsh.ChangedAt DESC LIMIT 1) = @PendingState
                AND (SELECT dsh.ChangedAt FROM DocumentStateHistory dsh WHERE dsh.DocumentId = d.Id ORDER BY dsh.ChangedAt DESC LIMIT 1) <= NOW() - INTERVAL '{alert.PendingDays.Value} days'
            )");
            parameters.Add("PendingState", alert.PendingState);
        }

        if (alert.ReviewWithinDays.HasValue)
        {
            ruleConditions.Add($@"(d.NextReviewDate IS NOT NULL AND d.NextReviewDate <= NOW() + INTERVAL '{alert.ReviewWithinDays.Value} days')");
        }

        if (alert.InactiveDays.HasValue)
        {
            ruleConditions.Add($@"(d.LastModifiedAt <= NOW() - INTERVAL '{alert.InactiveDays.Value} days')");
        }

        if (!string.IsNullOrWhiteSpace(alert.StatusChangedTo) && alert.LookbackDays.HasValue)
        {
            ruleConditions.Add($@"(
                EXISTS (
                    SELECT 1 FROM DocumentStateHistory dsh
                    JOIN DocumentStates ds ON ds.Id = dsh.ToStateId
                    WHERE dsh.DocumentId = d.Id AND ds.Code = @StatusChangedTo
                      AND dsh.ChangedAt >= NOW() - INTERVAL '{alert.LookbackDays.Value} days'
                )
            )");
            parameters.Add("StatusChangedTo", alert.StatusChangedTo);
        }

        if (ruleConditions.Any())
        {
            baseQuery += " AND (" + string.Join(" OR ", ruleConditions) + ")";
        }
        else
        {
            return new List<dynamic>();
        }

        var documents = await _common.QueryAsync<dynamic>(baseQuery, parameters);
        return documents.ToList();
    }

    private async Task DispatchAlertEmailsAsync(CustomizeEmailAlertReadDto alert, List<dynamic> documents, IEnumerable<AlertRecipientDto> recipients)
    {
        var toEmails = recipients.Where(r => !r.IsSelf && r.RecipientType?.Equals("To", StringComparison.OrdinalIgnoreCase) == true && !string.IsNullOrWhiteSpace(r.EmailAddress)).Select(r => r.EmailAddress).ToList();
        var ccEmails = recipients.Where(r => !r.IsSelf && r.RecipientType?.Equals("Cc", StringComparison.OrdinalIgnoreCase) == true && !string.IsNullOrWhiteSpace(r.EmailAddress)).Select(r => r.EmailAddress).ToList();
        var bccEmails = recipients.Where(r => !r.IsSelf && r.RecipientType?.Equals("Bcc", StringComparison.OrdinalIgnoreCase) == true && !string.IsNullOrWhiteSpace(r.EmailAddress)).Select(r => r.EmailAddress).ToList();

        // Fallback for missing recipient type in older data
        var unassignedEmails = recipients.Where(r => !r.IsSelf && string.IsNullOrWhiteSpace(r.RecipientType) && !string.IsNullOrWhiteSpace(r.EmailAddress)).Select(r => r.EmailAddress).ToList();
        toEmails.AddRange(unassignedEmails);

        bool notifySelf = recipients.Any(r => r.IsSelf);

        string deliveryFormat = string.IsNullOrWhiteSpace(alert.DeliveryFormat) ? "Embedded in Email Body" : alert.DeliveryFormat;
        string safeSubject = string.IsNullOrWhiteSpace(alert.EmailSubject) ? "DMS Document Alert" : alert.EmailSubject;
        string safeBody = string.IsNullOrWhiteSpace(alert.EmailBody) ? "Please check the DMS system for pending documents." : alert.EmailBody;

        if (deliveryFormat.Equals("Consolidated", StringComparison.OrdinalIgnoreCase))
        {
            var tableRows = documents.Select(d => {
                int pDays = d.statechangedat != null ? (DateTime.Now - (DateTime)d.statechangedat).Days : 0;
                int rDays = d.nextreviewdate != null ? ((DateTime)d.nextreviewdate - DateTime.Now).Days : 0;
                return $"<tr><td style='padding:8px; border:1px solid #ddd;'>{d.documentnumber}</td><td style='padding:8px; border:1px solid #ddd;'>{d.title}</td><td style='padding:8px; border:1px solid #ddd;'>{d.documenttype}</td><td style='padding:8px; border:1px solid #ddd;'>{d.currentstate}</td><td style='padding:8px; border:1px solid #ddd;'>{pDays}</td><td style='padding:8px; border:1px solid #ddd;'>{rDays}</td></tr>";
            });
            var table = $"<table style='border-collapse:collapse; width:100%; border:1px solid #ddd;'><tr><th style='padding:8px; border:1px solid #ddd; text-align:left;'>Document Number</th><th style='padding:8px; border:1px solid #ddd; text-align:left;'>Title</th><th style='padding:8px; border:1px solid #ddd; text-align:left;'>Type</th><th style='padding:8px; border:1px solid #ddd; text-align:left;'>Status</th><th style='padding:8px; border:1px solid #ddd; text-align:left;'>Pending Days</th><th style='padding:8px; border:1px solid #ddd; text-align:left;'>Review Days</th></tr>{string.Join("", tableRows)}</table>";
            
            var body = safeBody.Replace("{DocumentList}", table).Replace("{RecipientName}", "Team"); 

            var allToEmails = new HashSet<string>(toEmails);
            if (notifySelf)
            {
                foreach (var doc in documents)
                {
                    if (doc.createdby != null)
                    {
                        var empInfo = await GetEmployeeInfoAsync((string)doc.createdby, alert.CompanyId);
                        if (empInfo != null && !string.IsNullOrWhiteSpace(empInfo.email)) allToEmails.Add((string)empInfo.email);
                    }
                }
            }

            if (allToEmails.Any() || ccEmails.Any() || bccEmails.Any())
            {
                await _utilities.SendEmailAsync(allToEmails.ToList(), ccEmails, bccEmails, safeSubject, body);
            }
        }
        else
        {
            foreach (var doc in documents)
            {
                int pDays = doc.statechangedat != null ? (DateTime.Now - (DateTime)doc.statechangedat).Days : 0;
                int rDays = doc.nextreviewdate != null ? ((DateTime)doc.nextreviewdate - DateTime.Now).Days : 0;

                var body = safeBody
                    .Replace("{DocumentNumber}", (string)doc.documentnumber)
                    .Replace("{DocumentName}", (string)doc.title)
                    .Replace("{DocumentLink}", $"<a href=\"/dms/documents/view/{doc.id}\">View Document</a>")
                    .Replace("{DocumentType}", (string)doc.documenttype)
                    .Replace("{PendingDays}", pDays.ToString())
                    .Replace("{ReviewDays}", rDays.ToString())
                    .Replace("{Status}", (string)doc.currentstate);
                
                var subject = safeSubject
                    .Replace("{DocumentNumber}", (string)doc.documentnumber)
                    .Replace("{DocumentName}", (string)doc.title)
                    .Replace("{DocumentType}", (string)doc.documenttype)
                    .Replace("{Status}", (string)doc.currentstate);

                var currentTo = new List<string>(toEmails);
                if (notifySelf && doc.createdby != null)
                {
                    var empInfo = await GetEmployeeInfoAsync((string)doc.createdby, alert.CompanyId);
                    if (empInfo != null && !string.IsNullOrWhiteSpace(empInfo.email))
                    {
                        currentTo.Add((string)empInfo.email);
                        body = body.Replace("{RecipientName}", (string)empInfo.name);
                    }
                    else
                    {
                        body = body.Replace("{RecipientName}", "User");
                    }
                }
                else
                {
                    body = body.Replace("{RecipientName}", "Team");
                }

                if (currentTo.Any() || ccEmails.Any() || bccEmails.Any())
                {
                    await _utilities.SendEmailAsync(currentTo.ToList(), ccEmails, bccEmails, subject, body);
                }
            }
        }
    }

    private async Task<dynamic> GetEmployeeInfoAsync(string empCode, int companyId)
    {
        string query = "SELECT Email, LTRIM(RTRIM(COALESCE(e.firstname, '') || ' ' || COALESCE(e.midname, '') || ' ' || COALESCE(e.lastname, ''))) AS Name FROM tblEmployee WHERE LTRIM(RTRIM(empCode::text), '0') = LTRIM(RTRIM(@EmpCode::text), '0') AND CompanyId = @CompanyId LIMIT 1";
        return await _common.QueryFirstOrDefaultAsync<dynamic>(query, new { EmpCode = empCode, CompanyId = companyId });
    }
}

public class CustomizeEmailAlertCreateDto
{
    public string Name { get; set; }
    public bool IsActive { get; set; }
    public string PendingState { get; set; }
    public int? PendingDays { get; set; }
    public int? ReviewWithinDays { get; set; }
    public int? InactiveDays { get; set; }
    public string StatusChangedTo { get; set; }
    public int? LookbackDays { get; set; }
    public string Frequency { get; set; }
    public string SendAtTime { get; set; }
    public string DeliveryFormat { get; set; }
    public string EmailSubject { get; set; }
    public string EmailBody { get; set; }

    public List<AlertDocumentScopeDto> Scopes { get; set; }
    public List<AlertRecipientDto> Recipients { get; set; }
}

public class CustomizeEmailAlertUpdateDto : CustomizeEmailAlertCreateDto
{
    public int Id { get; set; }
}

public class CustomizeEmailAlertReadDto : CustomizeEmailAlertUpdateDto
{
    public int CompanyId { get; set; }
    public string CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class AlertDocumentScopeDto
{
    public string DivisionCode { get; set; }
    public string DepartmentCode { get; set; }
    public string SubDepartmentCode { get; set; }
    public string BusinessDomainCode { get; set; }
}

public class AlertRecipientDto
{
    public string EmailAddress { get; set; }
    public string RecipientType { get; set; }
    public bool IsSelf { get; set; }
}