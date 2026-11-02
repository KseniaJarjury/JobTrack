using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JobTrack.API.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace JobTrack.API.Api;

public sealed class InitialSetupKey
{
    public string? Value { get; private set; }

    public string Generate()
    {
        Value = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return Value;
    }

    public void Clear() => Value = null;
}

public static class AuthenticationEndpoints
{
    public static RouteGroupBuilder MapAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints,
        InitialSetupKey setupKey)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapGet("/status", async (UserManager<JobTrackUser> userManager, CancellationToken cancellationToken) =>
        {
            var setupRequired = !await userManager.Users.AnyAsync(cancellationToken);
            return Results.Ok(new { setupRequired });
        }).AllowAnonymous();

        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { requestToken = tokens.RequestToken });
        }).AllowAnonymous();

        group.MapPost("/setup", async (
            SetupRequest request,
            UserManager<JobTrackUser> userManager,
            JobTrackDbContext db,
            HttpContext context,
            InitialSetupKey initialSetupKey,
            CancellationToken cancellationToken) =>
        {
            if (await userManager.Users.AnyAsync(cancellationToken))
                return Results.Conflict(new { message = "La configuración inicial ya se completó." });

            if (!IsValidSetupKey(initialSetupKey.Value, request.SetupKey))
                return Results.Unauthorized();

            var email = request.Email?.Trim();
            if (string.IsNullOrEmpty(email) || email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Ingresá un correo electrónico válido."] });

            if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 120)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = ["El nombre es obligatorio y no puede superar los 120 caracteres."] });

            if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["La contraseña es obligatoria y no puede superar los 256 caracteres."] });

            var user = new JobTrackUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                AccountSlot = 1
            };

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                var errors = result.Errors
                    .GroupBy(error => string.Equals(error.Code, "DuplicateUserName", StringComparison.Ordinal)
                        || string.Equals(error.Code, "DuplicateEmail", StringComparison.Ordinal)
                        ? "email"
                        : "password")
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(GetIdentityErrorMessage).ToArray());
                return Results.ValidationProblem(errors);
            }

            var profile = await db.UserProfiles.SingleAsync(
                item => item.Id == JobTrackDbContext.LocalUserId,
                cancellationToken);
            profile.DisplayName = request.DisplayName.Trim();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            initialSetupKey.Clear();
            await SignInAsync(context, user);
            return Results.Ok(new { email = user.Email, displayName = profile.DisplayName });
        })
        .AllowAnonymous()
        .ValidateAntiforgery()
        .RequireRateLimiting("authentication");

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<JobTrackUser> userManager,
            HttpContext context) =>
        {
            var email = request.Email?.Trim();
            if (string.IsNullOrEmpty(email) || email.Length > 254
                || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
                return Results.Unauthorized();

            var user = await userManager.FindByEmailAsync(email);
            if (user is null || await userManager.IsLockedOutAsync(user))
                return Results.Unauthorized();

            if (!await userManager.CheckPasswordAsync(user, request.Password))
            {
                await userManager.AccessFailedAsync(user);
                return Results.Unauthorized();
            }

            await userManager.ResetAccessFailedCountAsync(user);
            await SignInAsync(context, user);
            return Results.Ok(new { email = user.Email });
        })
        .AllowAnonymous()
        .ValidateAntiforgery()
        .RequireRateLimiting("authentication");

        group.MapGet("/me", (ClaimsPrincipal user) =>
            Results.Ok(new { email = user.Identity?.Name }))
            .RequireAuthorization();

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .ValidateAntiforgery();

        return group;
    }

    private static bool IsValidSetupKey(string? expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided) || provided.Length > 128)
            return false;

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static string GetIdentityErrorMessage(IdentityError error) =>
        error.Code switch
        {
            "PasswordTooShort" => "La contraseña debe tener al menos 12 caracteres.",
            "PasswordRequiresDigit" => "La contraseña debe incluir un número.",
            "PasswordRequiresLower" => "La contraseña debe incluir una minúscula.",
            "PasswordRequiresUpper" => "La contraseña debe incluir una mayúscula.",
            "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir un símbolo.",
            "DuplicateEmail" or "DuplicateUserName" => "Ese correo ya está registrado.",
            _ => error.Description
        };

    private static Task SignInAsync(HttpContext context, JobTrackUser user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Email ?? user.UserName ?? "")
        ],
        CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties { IsPersistent = true };
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }

    public sealed record SetupRequest(string? SetupKey, string? Email, string? Password, string? DisplayName);
    public sealed record LoginRequest(string? Email, string? Password);
}
