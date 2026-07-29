using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    /// <summary>What the property has been worth over time. Drives appreciation and equity.</summary>
    [ApiController]
    [Authorize]
    [Route("api/RentalProperties/{propertyId:int}/valuations")]
    public class PropertyValuationsController(MoneyManagerDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PropertyValuation>>> GetAll(int propertyId)
        {
            if (!await context.RentalProperties.AnyAsync(p => p.Id == propertyId))
                return NotFound();

            return await context.PropertyValuations
                .Where(v => v.RentalPropertyId == propertyId)
                .OrderBy(v => v.ValuedOn)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<PropertyValuation>> Create(int propertyId, [FromBody] ValuationRequest request)
        {
            var property = await context.RentalProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property is null)
                return NotFound();

            if (request.Value <= 0)
                return BadRequest(new { message = "A valuation must be greater than zero." });

            var valuation = new PropertyValuation
            {
                RentalPropertyId = propertyId,
                ValuedOn = request.ValuedOn,
                Value = request.Value,
                CurrencyCode = property.CurrencyCode,
                Source = request.Source,
                Notes = request.Notes,
            };

            context.PropertyValuations.Add(valuation);

            context.PropertyEvents.Add(new PropertyEvent
            {
                RentalPropertyId = propertyId,
                OccurredOn = request.ValuedOn,
                Type = PropertyEventType.Valuation,
                Title = "Valuation recorded",
                Description = $"{request.Value:N0} {property.CurrencyCode}",
                IsSystemGenerated = true,
            });

            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { propertyId }, valuation);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int propertyId, int id)
        {
            var valuation = await context.PropertyValuations
                .FirstOrDefaultAsync(v => v.Id == id && v.RentalPropertyId == propertyId);

            if (valuation is null)
                return NotFound();

            context.PropertyValuations.Remove(valuation);
            await context.SaveChangesAsync();
            return NoContent();
        }
    }

    /// <summary>
    /// The rent timeline: what was charged, and what the market was estimated to pay.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/RentalProperties/{propertyId:int}/rent-history")]
    public class RentPricePointsController(MoneyManagerDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RentPricePoint>>> GetAll(int propertyId)
        {
            if (!await context.RentalProperties.AnyAsync(p => p.Id == propertyId))
                return NotFound();

            return await context.RentPricePoints
                .Where(r => r.RentalPropertyId == propertyId)
                .OrderBy(r => r.EffectiveFrom)
                .ToListAsync();
        }

        /// <summary>
        /// Records a market estimate by hand. The automatic provider writes the same rows,
        /// so a manually entered benchmark and a fetched one are treated identically.
        /// </summary>
        [HttpPost("market-estimate")]
        public async Task<ActionResult<RentPricePoint>> AddMarketEstimate(
            int propertyId, [FromBody] MarketEstimateRequest request)
        {
            var property = await context.RentalProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property is null)
                return NotFound();

            if (request.Amount <= 0)
                return BadRequest(new { message = "A market estimate must be greater than zero." });

            var point = new RentPricePoint
            {
                RentalPropertyId = propertyId,
                EffectiveFrom = request.EffectiveFrom ?? DateTime.UtcNow.Date,
                Amount = request.Amount,
                CurrencyCode = property.CurrencyCode,
                Source = RentPriceSource.MarketEstimate,
                ProviderKey = "manual",
                Notes = request.Notes,
            };

            context.RentPricePoints.Add(point);
            await context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { propertyId }, point);
        }
    }

    /// <summary>The property's timeline. Most entries are written automatically.</summary>
    [ApiController]
    [Authorize]
    [Route("api/RentalProperties/{propertyId:int}/events")]
    public class PropertyEventsController(MoneyManagerDbContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PropertyEvent>>> GetAll(int propertyId)
        {
            if (!await context.RentalProperties.AnyAsync(p => p.Id == propertyId))
                return NotFound();

            return await context.PropertyEvents
                .Where(e => e.RentalPropertyId == propertyId)
                .OrderByDescending(e => e.OccurredOn)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<PropertyEvent>> Create(int propertyId, [FromBody] PropertyEventRequest request)
        {
            if (!await context.RentalProperties.AnyAsync(p => p.Id == propertyId))
                return NotFound();

            var propertyEvent = new PropertyEvent
            {
                RentalPropertyId = propertyId,
                OccurredOn = request.OccurredOn,
                Type = request.Type,
                Title = request.Title,
                Description = request.Description,
                IsSystemGenerated = false,
            };

            context.PropertyEvents.Add(propertyEvent);
            await context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { propertyId }, propertyEvent);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int propertyId, int id)
        {
            var propertyEvent = await context.PropertyEvents
                .FirstOrDefaultAsync(e => e.Id == id && e.RentalPropertyId == propertyId);

            if (propertyEvent is null)
                return NotFound();

            context.PropertyEvents.Remove(propertyEvent);
            await context.SaveChangesAsync();
            return NoContent();
        }
    }

    public record ValuationRequest(
        DateTime ValuedOn,
        decimal Value,
        ValuationSource Source = ValuationSource.OwnerEstimate,
        string? Notes = null);

    public record MarketEstimateRequest(
        decimal Amount,
        DateTime? EffectiveFrom = null,
        string? Notes = null);

    public record PropertyEventRequest(
        DateTime OccurredOn,
        PropertyEventType Type,
        string Title,
        string? Description = null);
}
