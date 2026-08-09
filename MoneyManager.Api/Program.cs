using System.Text;
using System.Threading.RateLimiting;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MoneyManager.Api.Data;
using MoneyManager.Api.Infrastructure;
using MoneyManager.Api.Models;
using MoneyManager.Api.Services.Analytics;
using MoneyManager.Api.Services.Currency;
using MoneyManager.Api.Services.Rent;

DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// [ApiController] already turns an invalid ModelState into an RFC 7807 ValidationProblemDetails,
// which is why the request records carry DataAnnotations rather than hand-written checks. This
// registers the same shape for everything else — a 404 from a route that matched nothing, and the
// 500 produced by the exception handler below — so a caller parses one envelope for every failure
// rather than four.
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? "Data Source=moneymanager.db";

// appsettings.json ships a relative path so a fresh clone runs with no setup. That same default
// is a silent data-loss bug in a container: it resolves against the working directory, which is
// the image's own writable layer, so the app boots, migrates, serves traffic — and loses
// everything on the next deploy. Nothing warns, because nothing is wrong until the redeploy.
//
// Outside Development the database therefore has to be an absolute path, which on a deployment
// means the mounted volume, the only storage that outlives the container.
if (!builder.Environment.IsDevelopment())
{
    var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

    // In-memory databases are addressed rather than stored, so "does this survive a redeploy"
    // is not a question that applies to them.
    if (!dataSource.Contains(":memory:", StringComparison.Ordinal) && !Path.IsPathRooted(dataSource))
    {
        throw new InvalidOperationException(
            $"ConnectionStrings:Default points at the relative path '{dataSource}', which resolves " +
            "inside the container and is discarded on the next deploy. Point it at a mounted " +
            "volume, for example ConnectionStrings__Default=\"Data Source=/data/moneymanager.db\".");
    }
}

builder.Services.AddDbContext<MoneyManagerDbContext>(options => options.UseSqlite(connectionString));

// ---------------------------------------------------------------------------
// Authentication
// ---------------------------------------------------------------------------
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));

var seed = builder.Configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();

// Refusing to start beats seeding a default. With registration disabled the seeded account is
// the only way into the deployment, and preview URLs are public with nothing in front of them —
// so a built-in password would mean one known credential opening every environment built from
// this image. A container that will not boot is a much cheaper failure than that.
if (seed.Enabled && seed.Password.Length < SeedOptions.MinimumPasswordLength)
{
    throw new InvalidOperationException(
        $"{SeedOptions.SectionName}:Password must be at least {SeedOptions.MinimumPasswordLength} characters " +
        $"when {SeedOptions.SectionName}:Enabled is true. Set it via the {SeedOptions.SectionName}__Password " +
        "environment variable; there is deliberately no default.");
}

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
            NameClaimType = "username"
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
builder.Services.AddScoped<CurrencyRollupService>();
builder.Services.AddScoped<RentScheduleService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Deployed behind an edge proxy, every request reaches Kestrel from the proxy — so
// Connection.RemoteIpAddress is the same value for all of them and the partition key below
// collapses to a constant. That is not a weakened mitigation, it is precisely the one-shared-
// bucket outcome the limiter's own comment rules out, arrived at by deploying rather than by
// editing. Nothing fails and nothing logs.
//
// It is an availability bug rather than only a security one: [EnableRateLimiting("auth")] sits
// on the whole AuthController, which includes the /api/auth/me the SPA calls on every page
// load. One bucket means the entire deployment shares ten requests a minute, and two people
// browsing at once throttle each other out of the app.
//
// KnownNetworks and KnownProxies are cleared because the edge's address is neither loopback nor
// knowable in advance. That switches off the middleware's per-hop verification altogether — it
// no longer stops at the first unrecognised peer — so what keeps this honest is ForwardLimit
// staying at 1: only the rightmost X-Forwarded-For entry is read, which is the one the edge
// itself appended rather than anything a caller wrote. That entry is the real client only if
// exactly one hop appends to the header, and there is no signal if that assumption is wrong.
// It has to be confirmed by observing the resolved address vary per client on a deployment.
//
// XForwardedProto is included so a Location header from CreatedAtAction carries the scheme the
// caller actually used rather than the http:// the proxy forwards over. With KnownProxies
// cleared it is caller-controlled, which is harmless while nothing branches on the scheme —
// there is no UseHttpsRedirection here — but it would become an open redirect the day one is
// added.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
    scope.ServiceProvider.GetRequiredService<MoneyManagerDbContext>().Database.Migrate();

    // After Migrate, because a fresh volume has no schema to seed into. Deliberately not
    // wrapped in a try/catch: a seeder that failed leaves the environment in a state nobody
    // asked for, and the unhandled exception on stdout is the only diagnosis available.
    await DemoDataSeeder.SeedAsync(scope.ServiceProvider);
}

// Ahead of everything, so it wraps the whole pipeline rather than only the endpoints. Outside
// Development this writes a ProblemDetails 500 carrying no stack trace and no exception message:
// the details go to the log, where they belong, and not to a public URL. It is also what makes an
// unhandled exception answer in the same shape as every other failure — before this, one would
// have produced an empty body the SPA had no way to interpret.
app.UseExceptionHandler();

// Then forwarded headers, ahead of everything that reads the client address or the scheme —
// which here means the rate limiter. Registered without the options above it is a silent no-op,
// the same class of quiet failure it exists to prevent.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// The SPA is served from this same origin in a deployed image: the Dockerfile copies the Vite
// bundle into wwwroot. In a dev checkout wwwroot does not exist, these are no-ops, and the Vite
// dev server on :5173 serves the UI as before.
//
// Both must stay ahead of UseAuthorization. MapFallbackToFile registers the pattern
// "{*path:nonfile}", which by design does not match a path whose last segment has a file
// extension — so /assets/index-abc123.js matches no endpoint at all, and the FallbackPolicy
// below is applied to endpoint-less requests too. Served after authorization, every script and
// stylesheet would come back 401: a blank page behind a working index.html, which reads like a
// CORS or build problem and not like an authorization one.
app.UseDefaultFiles();
app.UseStaticFiles();

// Order matters: CORS has to run before endpoint routing terminates the request, and
// authentication has to populate the principal before authorization inspects it.
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// The three endpoints below are the only anonymous ones outside register and login, and each is
// a deliberate exception to the deny-by-default FallbackPolicy rather than an oversight.
//
// None of them exposes data. Requiring a token for the SPA shell would be circular anyway — the
// token lives in localStorage, which the browser can only read once the shell it is gating has
// already loaded.

// Liveness for the platform healthcheck, which has no credentials to present.
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Without this, the SPA fallback below swallows an unmatched /api path and answers 200 HTML
// where every caller expects JSON, turning "no such endpoint" into a parse error. Both
// fallbacks sit at the same route order, so the more specific pattern wins.
app.MapFallback("/api/{**slug}", () => Results.NotFound()).AllowAnonymous();

// vue-router runs in history mode, so a deep link such as /properties/3 reaches the server as a
// real navigation and has to be answered with the shell for the client-side router to take over.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

/// <summary>Exposed so integration tests can host the app with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
