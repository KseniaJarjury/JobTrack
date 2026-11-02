using JobTrack.API.Api;
using JobTrack.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("JobTrack");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure la cadena de conexión ConnectionStrings:JobTrack.");

builder.Services.AddDbContext<JobTrackDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddOpenApi();
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy("LocalFrontend", policy =>
        policy.WithOrigins("null", "http://localhost:5500", "http://127.0.0.1:5500")
            .AllowAnyHeader()
            .AllowAnyMethod()));
}

var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend"));
var frontendProvider = Directory.Exists(frontendPath) ? new PhysicalFileProvider(frontendPath) : null;
if (frontendProvider is not null)
    builder.WebHost.UseWebRoot(frontendPath);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("LocalFrontend");
}

if (frontendProvider is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = frontendProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = frontendProvider });
}

app.UseHttpsRedirection();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JobTrackDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
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
app.MapApplicationEndpoints();

app.Run();
