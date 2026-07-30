using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;

namespace MoneyManager.Api.Services.Currency
{
    /// <summary>
    /// Loads the stored rates and hands back an immutable converter for the current request.
    /// </summary>
    public sealed class ExchangeRateService(MoneyManagerDbContext context)
    {
        public async Task<CurrencyConverter> GetConverterAsync(CancellationToken ct = default)
        {
            var rates = await context.ExchangeRates.AsNoTracking().ToListAsync(ct);
            return CurrencyConverter.FromRates(rates);
        }
    }
}
