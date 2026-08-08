using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    /// <summary>
    /// The requesting user's own preferences. <c>BaseCurrency</c> was captured at registration
    /// and then unreachable, which meant the one setting that decides what unit a consolidated
    /// total is reported in could never be corrected.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly ICurrentUser _currentUser;

        public SettingsController(MoneyManagerDbContext context, ICurrentUser currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        [HttpGet]
        public async Task<ActionResult<SettingsDto>> Get()
        {
            var user = await CurrentUserOrNull();
            if (user is null)
                return Unauthorized();

            return SettingsDto.From(user);
        }

        [HttpPut]
        public async Task<ActionResult<SettingsDto>> Update([FromBody] SettingsRequest request)
        {
            if (SupportedCurrencies.Normalize(request.BaseCurrency) is not { } baseCurrency)
            {
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    ["baseCurrency"] =
                    [
                        $"'{request.BaseCurrency}' is not a supported currency. " +
                        $"Supported: {string.Join(", ", SupportedCurrencies.All)}.",
                    ],
                }));
            }

            var user = await CurrentUserOrNull();
            if (user is null)
                return Unauthorized();

            user.BaseCurrency = baseCurrency;
            user.AlwaysConvertToBaseCurrency = request.AlwaysConvertToBaseCurrency;

            await _context.SaveChangesAsync();

            return SettingsDto.From(user);
        }

        /// <summary>
        /// <c>User</c> is not an <see cref="IOwnedByUser"/> entity, so it carries no global query
        /// filter and the row has to be pinned to the authenticated id here. The id comes from
        /// the token via <see cref="ICurrentUser"/> and never from the request body — the same
        /// rule the filtered entities get for free.
        /// </summary>
        private async Task<User?> CurrentUserOrNull()
        {
            if (_currentUser.UserId is not { } userId)
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }
    }

    public record SettingsRequest(string BaseCurrency, bool AlwaysConvertToBaseCurrency);

    public record SettingsDto(string BaseCurrency, bool AlwaysConvertToBaseCurrency)
    {
        public static SettingsDto From(User user) =>
            new(user.BaseCurrency, user.AlwaysConvertToBaseCurrency);
    }
}
