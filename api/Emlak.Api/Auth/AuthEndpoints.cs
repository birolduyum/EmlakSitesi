using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Auth;

public static class AuthEndpoints
{
    public sealed record LoginRequest(string? Username, string? Password);

    public sealed record LoginResponse(string Token, string Username, string Role);

    // Bilinmeyen kullanıcıda da hash doğrulaması çalışsın diye (süre farkı kullanıcının varlığını sızdırmasın).
    private static readonly AppUser DummyUser = new();
    private static string? _dummyHash;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest req, AppDbContext db, IPasswordHasher<AppUser> hasher, JwtTokenService tokens) =>
        {
            var user = string.IsNullOrWhiteSpace(req.Username)
                ? null
                : await db.Users.SingleOrDefaultAsync(u => u.Username == req.Username);

            _dummyHash ??= hasher.HashPassword(DummyUser, Guid.NewGuid().ToString("N"));
            var result = hasher.VerifyHashedPassword(user ?? DummyUser, user?.PasswordHash ?? _dummyHash, req.Password ?? "");

            if (user is null || result == PasswordVerificationResult.Failed)
            {
                return Results.Json(new { error = "Kullanıcı adı veya parola hatalı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(new LoginResponse(tokens.CreateToken(user), user.Username, user.Role.ToString()));
        }).AllowAnonymous();

        return app;
    }

    /// <summary>Yalnızca Testing ortamında: rol politikalarını sınamak için geçici uç noktalar.</summary>
    public static IEndpointRouteBuilder MapTestAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/_test/staff", () => Results.Ok()).RequireAuthorization(AuthExtensions.Staff);
        app.MapGet("/api/_test/admin", () => Results.Ok()).RequireAuthorization(AuthExtensions.AdminOnly);
        return app;
    }
}
