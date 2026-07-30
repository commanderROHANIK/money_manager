using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;

namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly TokenProvider _tokenProvider;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ICurrentUser _currentUser;

        /// <summary>
        /// Verified against when no user matches, so a failed login costs the same whether or
        /// not the username exists and the response time stops being an account oracle.
        /// </summary>
        private static readonly string DecoyHash =
            new PasswordHasher<User>().HashPassword(new User(), "decoy-password");

        public AuthController(
            MoneyManagerDbContext context,
            TokenProvider tokenProvider,
            IPasswordHasher<User> passwordHasher,
            ICurrentUser currentUser)
        {
            _context = context;
            _tokenProvider = tokenProvider;
            _passwordHasher = passwordHasher;
            _currentUser = currentUser;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Username and password are required" });

            if (request.Password.Length < 8)
                return BadRequest(new { message = "Password must be at least 8 characters" });

            var user = new User
            {
                Username = request.Username.Trim(),
                Email = request.Email.Trim(),
                NormalizedUsername = request.Username.Trim().ToUpperInvariant(),
                NormalizedEmail = request.Email.Trim().ToUpperInvariant(),
                BaseCurrency = string.IsNullOrWhiteSpace(request.BaseCurrency)
                    ? "EUR"
                    : request.BaseCurrency.Trim().ToUpperInvariant(),
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            // The first account on an instance administers it, so a fresh deployment can
            // maintain exchange rates without a separate provisioning step. Racing
            // registrations could in principle both see an empty table; the loser is a
            // second admin on a brand-new instance, which is not worth a transaction.
            user.IsAdmin = !await _context.Users.AnyAsync();

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The unique indexes on the normalized columns are the real guard; checking
                // first and inserting after would race two concurrent registrations.
                return Conflict(new { message = "That username or email address is already registered" });
            }

            return Ok(new { message = "User registered successfully" });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var normalized = (request.Username ?? string.Empty).Trim().ToUpperInvariant();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.NormalizedUsername == normalized);

            if (user is null)
            {
                _passwordHasher.VerifyHashedPassword(new User(), DecoyHash, request.Password ?? string.Empty);
                return Unauthorized(new { message = "Invalid username or password" });
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? string.Empty);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Invalid username or password" });

            if (result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password!);
                await _context.SaveChangesAsync();
            }

            return Ok(new { token = _tokenProvider.Create(user) });
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = _currentUser.UserId;
            if (userId is null)
                return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                return Unauthorized();

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                baseCurrency = user.BaseCurrency,
                isAdmin = user.IsAdmin
            });
        }

        /// <summary>
        /// Changes the currency consolidated portfolio totals are reported in. Individual
        /// properties keep their own currency — only the rollup is affected.
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
        {
            var userId = _currentUser.UserId;
            if (userId is null)
                return Unauthorized();

            var currency = (request.BaseCurrency ?? string.Empty).Trim().ToUpperInvariant();
            if (currency.Length != 3)
                return BadRequest(new { message = "Base currency must be a three-letter ISO 4217 code." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
                return Unauthorized();

            user.BaseCurrency = currency;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                baseCurrency = user.BaseCurrency,
                isAdmin = user.IsAdmin
            });
        }
    }

    public record UpdateProfileRequest(string BaseCurrency);

    public record RegisterRequest(string Username, string Email, string Password, string? BaseCurrency = null);
    public record LoginRequest(string Username, string Password);
}
