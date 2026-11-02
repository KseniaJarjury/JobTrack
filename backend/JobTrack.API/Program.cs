using JobTrack.API.Api;
using JobTrack.API.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Threading.RateLimiting;

var contentRootPath = Directory.GetCurrentDirectory();
var frontendPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "frontend"));
var frontendProvider = Directory.Exists(frontendPath) ? new PhysicalFileProvider(frontendPath) : null;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRootPath,
    WebRootPath = frontendProvider is null ? null : frontendPath
});

var connectionString = builder.Configuration.GetConnectionString("JobTrack");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure la cadena de conexión ConnectionStrings:JobTrack.");

builder.Services.AddDbContext<JobTrackDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddOpenApi();
builder.Services
    .AddIdentityCore<JobTrackUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<JobTrackDbContext>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "JobTrack.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "JobTrack.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
var initialSetupKey = new InitialSetupKey();
builder.Services.AddSingleton(initialSetupKey);

var app = builder.Build();

if (frontendProvider is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendProvider });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

app.UseHttpsRedirection();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobTrackDbContext>();
    await db.Database.MigrateAsync();
    if (!await db.Users.AnyAsync())
    {
        var setupKey = initialSetupKey.Generate();
        app.Logger.LogWarning("Cuenta inicial pendiente. Código temporal de configuración (ingresalo en JobTrack): {SetupKey}", setupKey);
    }
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/api/profile", async (JobTrackDbContext db, CancellationToken cancellationToken) =>
{
    var profile = await db.UserProfiles
        .AsNoTracking()
        .Where(user => user.Id == JobTrackDbContext.LocalUserId)
        .Select(user => new
        {
            user.Id,
            user.DisplayName,
            user.Education,
            user.ExperienceSummary,
            Skills = user.Skills.Select(skill => new { skill.Id, skill.Name, skill.Proficiency }),
            PositionPreferences = user.PositionPreferences.Select(preference =>
                new { preference.Id, preference.Title, preference.PreferenceType }),
            WorkPreferences = user.WorkPreferences.Select(preference => new { preference.Id, preference.Preference })
        })
        .SingleAsync(cancellationToken);
    return Results.Ok(profile);
});
app.MapAuthenticationEndpoints(initialSetupKey);
app.MapApplicationEndpoints();

app.Run();
