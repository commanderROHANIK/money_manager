using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure.Validation;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/RentalProperties/{propertyId:int}/leases")]
    public class LeasesController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;

        public LeasesController(MoneyManagerDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Lease>>> GetAll(int propertyId)
        {
            if (!await _context.RentalProperties.AnyAsync(p => p.Id == propertyId))
                return NotFound();

            return await _context.Leases
                .Where(l => l.RentalPropertyId == propertyId)
                .OrderByDescending(l => l.StartDate)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Lease>> Create(int propertyId, [FromBody] LeaseRequest request)
        {
            var property = await _context.RentalProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
            if (property is null)
                return NotFound();

            var lease = new Lease
            {
                RentalPropertyId = propertyId,
                TenantName = request.TenantName,
                TenantEmail = request.TenantEmail,
                TenantPhone = request.TenantPhone,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                MonthlyRent = request.MonthlyRent,
                CurrencyCode = property.CurrencyCode,
                RentDueDayOfMonth = Math.Clamp(request.RentDueDayOfMonth, 1, 28),
                DepositAmount = request.DepositAmount,
                Notes = request.Notes,
            };

            _context.Leases.Add(lease);

            // A tenancy starting is both a timeline entry and a point on the rent history —
            // recording both here is what makes the rent-over-time chart populate itself.
            _context.PropertyEvents.Add(new PropertyEvent
            {
                RentalPropertyId = propertyId,
                OccurredOn = request.StartDate,
                Type = PropertyEventType.TenantMovedIn,
                Title = $"{request.TenantName} moved in",
                Description = $"Rent {request.MonthlyRent:N0} {property.CurrencyCode} per month",
                IsSystemGenerated = true,
            });

            _context.RentPricePoints.Add(new RentPricePoint
            {
                RentalPropertyId = propertyId,
                EffectiveFrom = request.StartDate,
                Amount = request.MonthlyRent,
                CurrencyCode = property.CurrencyCode,
                Source = RentPriceSource.Contracted,
            });

            if (request.EndDate is { } moveOut)
            {
                _context.PropertyEvents.Add(new PropertyEvent
                {
                    RentalPropertyId = propertyId,
                    OccurredOn = moveOut,
                    Type = PropertyEventType.TenantMovedOut,
                    Title = $"{request.TenantName} moved out",
                    IsSystemGenerated = true,
                });
            }

            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { propertyId }, lease);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int propertyId, int id, [FromBody] LeaseRequest request)
        {
            var lease = await _context.Leases
                .FirstOrDefaultAsync(l => l.Id == id && l.RentalPropertyId == propertyId);

            if (lease is null)
                return NotFound();

            var rentChanged = lease.MonthlyRent != request.MonthlyRent;
            var previousRent = lease.MonthlyRent;

            lease.TenantName = request.TenantName;
            lease.TenantEmail = request.TenantEmail;
            lease.TenantPhone = request.TenantPhone;
            lease.StartDate = request.StartDate;
            lease.EndDate = request.EndDate;
            lease.MonthlyRent = request.MonthlyRent;
            lease.RentDueDayOfMonth = Math.Clamp(request.RentDueDayOfMonth, 1, 28);
            lease.DepositAmount = request.DepositAmount;
            lease.Notes = request.Notes;

            // Every rent change becomes a new point on the timeline rather than overwriting
            // history, which is the whole point of following the rental price.
            if (rentChanged)
            {
                var effectiveFrom = DateTime.UtcNow.Date;

                _context.RentPricePoints.Add(new RentPricePoint
                {
                    RentalPropertyId = propertyId,
                    LeaseId = lease.Id,
                    EffectiveFrom = effectiveFrom,
                    Amount = request.MonthlyRent,
                    CurrencyCode = lease.CurrencyCode,
                    Source = RentPriceSource.Contracted,
                });

                _context.PropertyEvents.Add(new PropertyEvent
                {
                    RentalPropertyId = propertyId,
                    OccurredOn = effectiveFrom,
                    Type = PropertyEventType.RentChanged,
                    Title = request.MonthlyRent > previousRent ? "Rent increased" : "Rent reduced",
                    Description = $"{previousRent:N0} → {request.MonthlyRent:N0} {lease.CurrencyCode}",
                    IsSystemGenerated = true,
                });
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int propertyId, int id)
        {
            var lease = await _context.Leases
                .FirstOrDefaultAsync(l => l.Id == id && l.RentalPropertyId == propertyId);

            if (lease is null)
                return NotFound();

            _context.Leases.Remove(lease);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }

    public record LeaseRequest(
        [Required, MaxLength(120)] string TenantName,
        DateTime StartDate,
        [NonNegative] decimal MonthlyRent,
        DateTime? EndDate = null,
        [EmailAddress, MaxLength(200)] string? TenantEmail = null,
        [MaxLength(40)] string? TenantPhone = null,
        [Range(1, 31)] int RentDueDayOfMonth = 1,
        [NonNegative] decimal? DepositAmount = null,
        [MaxLength(2000)] string? Notes = null) : IValidatableObject
    {
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Was a hand-written check in Create and Update that returned { message } with no
            // field named. Here it is enforced once, on both endpoints, and the UI can put the
            // message against the input that caused it.
            if (EndDate is { } end && end < StartDate)
            {
                yield return new ValidationResult(
                    "A tenancy cannot end before it starts.", [nameof(EndDate)]);
            }
        }
    }
}
