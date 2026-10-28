using System.Diagnostics;
using HCMS_Api.Components.DMS.Common;

namespace HCMS_Api.Components.DMS.Common.BackgroundJobs
{
    /// <summary>
    /// Sends what is waiting in EmailOutbox, never more than EmailQueue:MaxPerMinute in any minute and evenly spaced rather than in
    /// a burst, and records the outcome of every mail. A mail the SMTP server does not accept is tried again later
    /// (RetryDelayMinutes x number of tries so far), up to MaxAttempts, then marked Failed.
    /// </summary>
    public class EmailOutboxBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly EmailOutboxStore _store;
        private readonly EmailQueueOptions _options;
        private readonly ILogger<EmailOutboxBackgroundService> _logger;

        public EmailOutboxBackgroundService(IServiceProvider serviceProvider, EmailOutboxStore store, IConfiguration configuration, ILogger<EmailOutboxBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _store = store;
            _options = EmailQueueOptions.From(configuration);
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Email queue is switched off (EmailQueue:Enabled = false); notification emails are sent directly.");
                return;
            }

            try
            {
                // Let the application finish starting before the first look at the database.
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var handled = 0;
                    try
                    {
                        handled = await ProcessBatchAsync(OpenSender, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // The table not being there yet (script not run), the database being down, ... -- wait and look again.
                        _logger.LogError(ex, "Error occurred executing the email queue.");
                    }

                    if (handled == 0)
                        await Task.Delay(TimeSpan.FromSeconds(_options.PollSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }

        // One scope for the whole batch; opened only when there is something to send, so an idle queue costs one query.
        private (Func<OutboxEmail, Task> Send, IDisposable Scope) OpenSender()
        {
            var scope = _serviceProvider.CreateScope();
            var utilities = scope.ServiceProvider.GetRequiredService<DMSUtilities>();
            return (mail => utilities.SendEmailAsync(mail.RecipientEmail, mail.Subject, mail.Body), scope);
        }

        /// <summary>Returns how many mails were attempted (0 when nothing was due or the minute's allowance is used up).</summary>
        public async Task<int> ProcessBatchAsync(Func<(Func<OutboxEmail, Task> Send, IDisposable Scope)> openSender, CancellationToken ct)
        {
            var allowance = _options.MaxPerMinute - await _store.CountAttemptsInLastMinuteAsync();
            if (allowance <= 0) return 0;

            var due = await _store.ClaimDueAsync(Math.Min(allowance, 20));
            if (due.Count == 0) return 0;

            var spacing = TimeSpan.FromMilliseconds(60_000.0 / _options.MaxPerMinute);
            var (send, scope) = openSender();
            using (scope)
            {
                foreach (var mail in due)
                {
                    var clock = Stopwatch.StartNew();
                    try
                    {
                        await send(mail);
                        await _store.MarkSentAsync(mail.Id);
                    }
                    catch (FormatException ex)
                    {
                        // The address itself cannot be read (MimeKit's ParseException). Trying again changes nothing.
                        _logger.LogWarning(ex, "Email {Id} to '{To}' has an invalid address; marked Failed.", mail.Id, mail.RecipientEmail);
                        await _store.MarkFailedAsync(mail.Id, ex.Message);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
                    {
                        if (mail.AttemptCount >= _options.MaxAttempts)
                        {
                            _logger.LogError(ex, "Email {Id} to '{To}' failed {Attempts} times; giving up.", mail.Id, mail.RecipientEmail, mail.AttemptCount);
                            await _store.MarkFailedAsync(mail.Id, ex.Message);
                        }
                        else
                        {
                            var delay = _options.RetryDelayMinutes * mail.AttemptCount;
                            _logger.LogWarning(ex, "Email {Id} to '{To}' not sent (try {Attempt} of {Max}); trying again in {Delay} min.",
                                mail.Id, mail.RecipientEmail, mail.AttemptCount, _options.MaxAttempts, delay);
                            await _store.MarkRetryAsync(mail.Id, delay, ex.Message);
                        }
                    }

                    // Even spacing: with the default 200 a minute that is one mail every 0.3 s, whatever the SMTP server's speed.
                    var wait = spacing - clock.Elapsed;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                }
            }
            return due.Count;
        }
    }
}
