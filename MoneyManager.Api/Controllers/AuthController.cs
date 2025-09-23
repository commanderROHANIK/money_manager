using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyManager.Api.Data;
using MoneyManager.Api.Models;
using MoneyManager.Api.Infrastructure;


namespace MoneyManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly MoneyManagerDbContext _context;
        private readonly IConfiguration _config;
        private readonly TokenProvider _tokenProvider;

        public AuthController(MoneyManagerDbContext context, IConfiguration config, TokenProvider tokenProvider)
        {
            _context = context;
            _config = config;
            _tokenProvider = tokenProvider;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest(new { message = "Username already exists" });

                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                return BadRequest(new { message = "Email address already used" });

            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                Password = request.Password,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null || !string.Equals(request.Password, user.Password))
                return Unauthorized(new { message = "Invalid username or password" });

            var token = _tokenProvider.Create(user);
            return Ok(new { token });
        }

        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            return Ok(new
            {
                username = User.Identity?.Name
            });
        }
    }

    public record RegisterRequest(string Username, string Email, string Password);
    public record LoginRequest(string Username, string Password);
}
