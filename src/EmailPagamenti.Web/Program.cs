using System.Security.Claims;
using System.Threading.RateLimiting;
using EmailPagamenti.Infrastructure;
using EmailPagamenti.Infrastructure.Persistence;
using EmailPagamenti.Web;
using EmailPagamenti.Web.Endpoints;
using EmailPagamenti.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEmailPagamenti(builder.Configuration);

builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .Validate(
        o => o.AllowAnonymous || !string.IsNullOrWhiteSpace(o.Password),
        "Manca la password di accesso. Impostala con Security__Password, oppure accendi "
        + "Security__AllowAnonymous se sai che la stai esponendo senza protezione.")
    .ValidateOnStart();

// Le opzioni servono anche come parametro diretto degli endpoint, non solo via IOptions.
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<SecurityOptions>>().Value);

var security = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "pagamenti.sessione";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = security.RequireHttps
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = security.SessionLifetime;
        options.SlidingExpiration = true;

        // E' un'API: a sessione scaduta si risponde 401, non si reindirizza a una pagina.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// Il tentativo di indovinare la password costa: cinque prove al minuto per indirizzo.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("accesso", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

builder.Services.AddHostedService<IngestionBackgroundService>();

var app = builder.Build();

await PrepareDatabaseAsync(app).ConfigureAwait(false);

app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { stato = "ok" })).AllowAnonymous();

app.MapPost("/api/accesso", async (LoginRequest request, HttpContext context, SecurityOptions options) =>
{
    if (!options.AllowAnonymous && !PasswordGate.Verify(request.Password, options.Password))
    {
        return Results.Unauthorized();
    }

    var identity = new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "proprietario")],
        CookieAuthenticationDefaults.AuthenticationScheme);

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));

    return Results.Ok(new { entrato = true });
})
.AllowAnonymous()
.RequireRateLimiting("accesso");

app.MapPost("/api/uscita", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok(new { entrato = false });
}).AllowAnonymous();

app.MapGet("/api/sessione", (HttpContext context, SecurityOptions options) =>
    Results.Ok(new
    {
        entrato = options.AllowAnonymous || context.User.Identity?.IsAuthenticated == true,
        protetta = !options.AllowAnonymous,
    })).AllowAnonymous();

var api = app.MapGroup("/api").MapApi();
if (!security.AllowAnonymous)
{
    api.RequireAuthorization();
}

await app.RunAsync().ConfigureAwait(false);

static async Task PrepareDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();

    // Finche' non esiste una migrazione (dotnet ef migrations add Iniziale) si crea lo schema
    // direttamente, cosi' il progetto parte appena clonato. In produzione servono le migrazioni.
    if (db.Database.GetMigrations().Any())
    {
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }
    else
    {
        app.Logger.LogWarning("Nessuna migrazione trovata: lo schema viene creato con EnsureCreated.");
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }
}

/// <summary>Corpo della richiesta di accesso.</summary>
internal sealed record LoginRequest(string? Password);
