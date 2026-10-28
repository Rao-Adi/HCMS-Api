using Dapper;
using Npgsql;

namespace HCMS_Api.Components.DMS.Common.BackgroundJobs
{
    /// <summary>"EmailQueue" section of appsettings. Every value has a working default, so the section may be absent.</summary>
    public sealed class EmailQueueOptions
    {
        /// <summary>Off = notification emails are sent straight away as before, and the background job does nothing.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>The most mails the job will hand to the SMTP server in any 60 seconds.</summary>
        public int MaxPerMinute { get; set; } = 200;

        /// <summary>Tries before a mail is given up on (Status = Failed).</summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>Wait before the next try; multiplied by the number of tries so far.</summary>
        public int RetryDelayMinutes { get; set; } = 5;

        /// <summary>How often the job looks for work when it is idle.</summary>
        public int PollSeconds { get; set; } = 5;

        public static EmailQueueOptions From(IConfiguration configuration)
        {
            var options = configuration.GetSection("EmailQueue").Get<EmailQueueOptions>() ?? new EmailQueueOptions();
            options.MaxPerMinute = Math.Max(1, options.MaxPerMinute);
            options.MaxAttempts = Math.Max(1, options.MaxAttempts);
            options.RetryDelayMinutes = Math.Max(1, options.RetryDelayMinutes);
            options.PollSeconds = Math.Max(1, options.PollSeconds);
            return options;
        }
    }

    public sealed class OutboxEmail
    {
        public long Id { get; set; }
        public string RecipientEmail { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public int AttemptCount { get; set; }
    }

    /// <summary>
    /// The EmailOutbox table (db-changes-2026-10-28-email-outbox.sql). Every call uses its own short connection, on purpose: queuing a
    /// mail must never join, or be broken by, whatever transaction the workflow action that triggered it has open.
    /// </summary>
    public sealed class EmailOutboxStore
    {
        // How long a mail the job has picked up is kept away from other pickers. A job that dies between picking a mail and
        // recording the result leaves the mail Pending, and it is tried again once this has passed.
        private const int LeaseMinutes = 10;

        private readonly string _connectionString;

        public EmailOutboxStore(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DMSConnectionString") ?? "";
        }

        public async Task EnqueueAsync(int companyId, string employeeCode, string email, string subject, string body, int notificationType, int relatedEntityId)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(@"
                INSERT INTO EmailOutbox
                    (CompanyId, RecipientEmployeeCode, RecipientEmail, Subject, Body, NotificationType, RelatedEntityId)
                VALUES
                    (@CompanyId, @EmployeeCode, @Email, @Subject, @Body, @NotificationType, @RelatedEntityId);",
                new
                {
                    CompanyId = companyId,
                    EmployeeCode = employeeCode,
                    Email = email,
                    // The columns are bounded; a subject or address longer than that is a bug upstream, not a reason to lose the mail.
                    Subject = (subject ?? "").Length > 500 ? subject![..500] : subject ?? "",
                    Body = body ?? "",
                    NotificationType = notificationType,
                    RelatedEntityId = relatedEntityId
                });
        }

        /// <summary>Attempts started in the last 60 seconds, which is what the per-minute limit counts.</summary>
        public async Task<int> CountAttemptsInLastMinuteAsync()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM EmailOutbox
                WHERE LastAttemptAt > (NOW() AT TIME ZONE 'UTC') - INTERVAL '60 seconds';");
        }

        /// <summary>
        /// Takes up to <paramref name="limit"/> due mails and counts the attempt in the same statement, so the per-minute count
        /// sees them at once. SKIP LOCKED lets a second API instance work beside this one without sending anything twice.
        /// </summary>
        public async Task<List<OutboxEmail>> ClaimDueAsync(int limit)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var rows = await connection.QueryAsync<OutboxEmail>($@"
                UPDATE EmailOutbox o
                SET AttemptCount = o.AttemptCount + 1,
                    LastAttemptAt = (NOW() AT TIME ZONE 'UTC'),
                    NextAttemptAt = (NOW() AT TIME ZONE 'UTC') + INTERVAL '{LeaseMinutes} minutes'
                FROM (
                    SELECT Id FROM EmailOutbox
                    WHERE Status = 'Pending' AND NextAttemptAt <= (NOW() AT TIME ZONE 'UTC')
                    ORDER BY NextAttemptAt, Id
                    LIMIT @Limit
                    FOR UPDATE SKIP LOCKED
                ) due
                WHERE o.Id = due.Id
                RETURNING o.Id, o.RecipientEmail, o.Subject, o.Body, o.AttemptCount;",
                new { Limit = limit });
            return rows.OrderBy(r => r.Id).ToList();
        }

        public async Task MarkSentAsync(long id)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(@"
                UPDATE EmailOutbox
                SET Status = 'Sent', IsDelivered = TRUE, DeliveredAt = (NOW() AT TIME ZONE 'UTC'), LastError = NULL
                WHERE Id = @Id;", new { Id = id });
        }

        /// <summary>Put back for another try after <paramref name="delayMinutes"/>.</summary>
        public async Task MarkRetryAsync(long id, int delayMinutes, string error)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(@"
                UPDATE EmailOutbox
                SET NextAttemptAt = (NOW() AT TIME ZONE 'UTC') + make_interval(mins => @Delay), LastError = @Error
                WHERE Id = @Id;", new { Id = id, Delay = delayMinutes, Error = Truncate(error) });
        }

        public async Task MarkFailedAsync(long id, string error)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.ExecuteAsync(@"
                UPDATE EmailOutbox
                SET Status = 'Failed', IsDelivered = FALSE, LastError = @Error
                WHERE Id = @Id;", new { Id = id, Error = Truncate(error) });
        }

        private static string Truncate(string? text) =>
            string.IsNullOrEmpty(text) ? "" : text.Length <= 1000 ? text : text[..1000];
    }
}
