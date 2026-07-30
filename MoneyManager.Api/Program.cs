using System.Text;
using System.Threading.RateLimiting;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Analytics;
using MoneyManager.Api.Services.Currency;
using MoneyManager.Api.Services.MarketRent;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<MoneyManagerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=moneymanager.db"));

// ---------------------------------------------------------------------------
// Authentication
// ---------------------------------------------------------------------------
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwt.SecretKey) && builder.Environment.IsDevelopment())
{
    // Keeps a fresh clone runnable without setup. Never reached outside Development.
    jwt.SecretKey = "development-only-signing-key-do-not-use-in-production";
    builder.Configuration[$"{JwtSettings.SectionName}:SecretKey"] = jwt.SecretKey;
}

if (Encoding.UTF8.GetByteCount(jwt.SecretKey) < JwtSettings.MinimumSecretKeyBytes)
{
    throw new InvalidOperationException(
        $"{JwtSettings.SectionName}:SecretKey must be at least {JwtSettings.MinimumSecretKeyBytes} bytes for HS256. " +
        "Set it via configuration or the JwtSettings__SecretKey environment variable (see .env.example).");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "username",
            RoleClaimType = TokenProvider.RoleClaimType
        };
    });

// Deny by default: an endpoint that forgets [Authorize] is still protected. Only the
// endpoints explicitly marked [AllowAnonymous] (register, login) are reachable without a token.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<PropertyAnalyticsService>();
builder.Services.AddScoped<ExchangeRateService>();

// Market rent. Providers are resolved as a set and asked in priority order, so adding a
// paid HTTP-backed source later is a registration rather than a change at any call site.
// Validated at startup so a mistyped interval fails immediately, and by name. Left
// unvalidated it surfaced as an ArgumentOutOfRangeException thrown out of the hosted
// service, which the default BackgroundServiceExceptionBehavior turns into the whole API
// refusing to run for no stated reason.
builder.Services.AddOptions<MarketRentOptions>()
    .Bind(builder.Configuration.GetSection(MarketRentOptions.SectionName))
    .Validate(o => o.RefreshIntervalHours is > 0 and <= MarketRentRefreshService.MaxRefreshIntervalHours,
        $"{MarketRentOptions.SectionName}:RefreshIntervalHours must be between 1 and "
        + $"{MarketRentRefreshService.MaxRefreshIntervalHours}.")
    .Validate(o => o.MaxAgeDays > 0,
        $"{MarketRentOptions.SectionName}:MaxAgeDays must be greater than zero.")
    .Validate(o => o.StartupDelaySeconds >= 0,
        $"{MarketRentOptions.SectionName}:StartupDelaySeconds cannot be negative.")
    .ValidateOnStart();
builder.Services.AddScoped<IMarketRentProvider, PeerComparableRentProvider>();
builder.Services.AddScoped<MarketRentService>();
builder.Services.AddHostedService<MarketRentRefreshService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Credential stuffing is the obvious attack on a login form. A fixed window on the auth
// endpoints costs nothing and is table stakes for anything sold as a service. Partitioned
// by client IP so one caller hitting the limit can't lock everyone else out of login —
// AddFixedWindowLimiter's overload without a partition key hands out one shared bucket for
// the whole app, which is a self-inflicted denial of service rather than a mitigation.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    // The market rent refresh reads across every tenant and writes a row. Cheap for a user
    // clicking "refresh"; a way to hammer an unindexed cross-tenant scan, and to probe the
    // comparables set repeatedly, if left unmetered. Partitioned by user rather than IP so
    // one account cannot spend everyone else's budget.
    options.AddPolicy("market-rent", context => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? context.Connection.RemoteIpAddress?.ToString()
                      ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "MoneyManager API", Version = "v1" });

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    options.AddSecurityDefinition("Bearer", scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MoneyManagerDbContext>();
    db.Database.Migrate();

    // Backfills RentalProperty.NormalizedCity for rows that predate the column. Done here
    // rather than in the migration because SQLite's UPPER() only folds ASCII: it would
    // write "GYőR" where the application writes "GYŐR", leaving old and new rows in
    // different markets — the very defect the column exists to fix. Reassigning City runs
    // the setter, which normalises in C#. No current user at startup, so the tenant filter
    // has to be off; these are updates, so no owner is assigned.
    var unnormalized = db.RentalProperties
        .IgnoreQueryFilters()
        .Where(p => p.NormalizedCity == null && p.City != null)
        .ToList();

    if (unnormalized.Count > 0)
    {
        foreach (var property in unnormalized)
        {
            var city = property.City;
            property.City = city;
        }

        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Order matters: CORS has to run before endpoint routing terminates the request, and
// authentication has to populate the principal before authorization inspects it.
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can host the app with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
