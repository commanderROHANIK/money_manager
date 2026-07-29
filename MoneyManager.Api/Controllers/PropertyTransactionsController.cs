using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/RentalProperties/{propertyId:int}/transactions")]
    public class PropertyTransactionsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public PropertyTransactionsController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PropertyTransaction>>> GetAll(
            int propertyId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            if (!await OwnsProperty(propertyId))
                return NotFound();

            var query = _context.PropertyTransactions.Where(t => t.RentalPropertyId == propertyId);

            if (from is not null) query = query.Where(t => t.Date >= from);
            if (to is not null) query = query.Where(t => t.Date <= to);

            return await query.OrderByDescending(t => t.Date).ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<PropertyTransaction>> Create(
            int propertyId, [FromBody] PropertyTransactionRequest request)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property is null)
                return NotFound();

            if (request.Amount <= 0)
            {
                return BadRequest(new
                {
                    message = "Amount must be positive. Whether it is money in or money out "
                              + "is determined by the category."
                });
            }

            // One property, one currency. Accepting a foreign-currency line here would make
            // every total for this property silently wrong.
            var currency = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? property.CurrencyCode
                : request.CurrencyCode.ToUpperInvariant();

            if (currency != property.CurrencyCode)
            {
                return BadRequest(new
                {
                    message = $"This property is denominated in {property.CurrencyCode}; "
                              + $"a {currency} transaction cannot be recorded against it."
                });
            }

            if (!await LeaseBelongsToProperty(request.LeaseId, propertyId))
                return BadRequest(new { message = "The given lease does not belong to this property." });

            var transaction = new PropertyTransaction
            {
                RentalPropertyId = propertyId,
                LeaseId = request.LeaseId,
                Date = request.Date,
                Amount = request.Amount,
                CurrencyCode = currency,
                Category = request.Category,
                Description = request.Description ?? string.Empty,
            };

            _context.PropertyTransactions.Add(transaction);

            // Capital spend changes what the property cost, which is worth seeing on the
            // timeline next to everything else that happened.
            if (TransactionCategoryInfo.IsCapital(request.Category))
            {
                _context.PropertyEvents.Add(new PropertyEvent
                {
                    RentalPropertyId = propertyId,
                    OccurredOn = request.Date,
                    Type = request.Category == TransactionCategory.CapitalImprovement
                        ? PropertyEventType.Renovation
                        : PropertyEventType.Purchase,
                    Title = request.Category == TransactionCategory.CapitalImprovement
                        ? "Capital improvement"
                        : "Acquisition cost",
                    Description = $"{request.Amount:N0} {currency}"
                                  + (string.IsNullOrWhiteSpace(request.Description) ? "" : $" — {request.Description}"),
                    IsSystemGenerated = true,
                });
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { propertyId }, transaction);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int propertyId, int id, [FromBody] PropertyTransactionRequest request)
        {
            var transaction = await _context.PropertyTransactions
                .FirstOrDefaultAsync(t => t.Id == id && t.RentalPropertyId == propertyId);

            if (transaction is null)
                return NotFound();

            if (request.Amount <= 0)
                return BadRequest(new { message = "Amount must be positive." });

            if (!await LeaseBelongsToProperty(request.LeaseId, propertyId))
                return BadRequest(new { message = "The given lease does not belong to this property." });

            transaction.Date = request.Date;
            transaction.Amount = request.Amount;
            transaction.Category = request.Category;
            transaction.Description = request.Description ?? string.Empty;
            transaction.LeaseId = request.LeaseId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int propertyId, int id)
        {
            var transaction = await _context.PropertyTransactions
                .FirstOrDefaultAsync(t => t.Id == id && t.RentalPropertyId == propertyId);

            if (transaction is null)
                return NotFound();

            _context.PropertyTransactions.Remove(transaction);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private Task<bool> OwnsProperty(int propertyId) =>
            _context.RentalProperties.AnyAsync(p => p.Id == propertyId);

        // A null LeaseId is valid (not every transaction is tied to a tenancy); a non-null one
        // must reference a lease on this property, otherwise a transaction could be tagged
        // against a lease belonging to a different property the caller owns.
        private Task<bool> LeaseBelongsToProperty(int? leaseId, int propertyId) =>
            leaseId is null
                ? Task.FromResult(true)
                : _context.Leases.AnyAsync(l => l.Id == leaseId && l.RentalPropertyId == propertyId);
    }

    public record PropertyTransactionRequest(
        DateTime Date,
        decimal Amount,
        TransactionCategory Category,
        string? Description = null,
        string? CurrencyCode = null,
        int? LeaseId = null);
}
