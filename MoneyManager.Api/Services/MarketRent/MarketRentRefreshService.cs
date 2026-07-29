using Microsoft.Extensions.Options;

namespace MoneyManager.Api.Services.MarketRent
{
    public sealed class MarketRentOptions
    {
        public const string SectionName = "MarketRent";

        public bool Enabled { get; set; } = true;

        /// <summary>How stale an estimate may become before it is refreshed.</summary>
        public int MaxAgeDays { get; set; } = 30;

        public int RefreshIntervalHours { get; set; } = 24;

        /// <summary>Delays the first run so startup is never held up by it.</summary>
        public int StartupDelaySeconds { get; set; } = 30;
    }

    /// <summary>
    /// Keeps market rent estimates current in the background, so the below-market figures a
    /// landlord logs in to see are already there rather than being computed on the click.
    /// </summary>
    public sealed class MarketRentRefreshService(
        IServiceScopeFactory scopeFactory,
        IOptions<MarketRentOptions> options,
        ILogger<MarketRentRefreshService> logger) : BackgroundService
    {
        private readonly MarketRentOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                logger.LogInformation("Market rent refresh is disabled.");
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.RefreshIntervalHours));

            do
            {
                try
                {
                    await RefreshStaleEstimatesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // A failed run must not take the loop down with it.
                    logger.LogError(ex, "Market rent refresh run failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RefreshStaleEstimatesAsync(CancellationToken ct)
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<MarketRentService>();

            var stale = await service.FindStaleAsync(TimeSpan.FromDays(_options.MaxAgeDays), ct);

            if (stale.Count == 0)
            {
                logger.LogDebug("No properties need a market rent estimate.");
                return;
            }

            var written = 0;

            foreach (var property in stale)
            {
                ct.ThrowIfCancellationRequested();

                // A null result is ordinary — it means there are not yet enough comparable
                // lettings in that city to say anything honest.
                if (await service.RefreshAsync(property, ct) is not null)
                    written++;
            }

            logger.LogInformation(
                "Market rent refresh: {Written} of {Considered} properties updated.",
                written, stale.Count);
        }
    }
}
