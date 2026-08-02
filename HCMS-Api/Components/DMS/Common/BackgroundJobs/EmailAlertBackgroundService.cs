using HCMS_Api.Components.DMS.ESS;

namespace HCMS_Api.Components.DMS.Common.BackgroundJobs
{
    public class EmailAlertBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailAlertBackgroundService> _logger;

        public EmailAlertBackgroundService(IServiceProvider serviceProvider, ILogger<EmailAlertBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var emailAlertsComponent = scope.ServiceProvider.GetRequiredService<CustomizeEmailAlertsComponent>();
                    await emailAlertsComponent.ProcessScheduledEmailAlertsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred executing Email Alert Background Service.");
                }

                // Delay for 10 minutes to frequently check time conditions for exactly the right hour slot
                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}