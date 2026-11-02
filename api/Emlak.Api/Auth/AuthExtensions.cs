using System.Text;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Emlak.Api.Auth;

public static class AuthExtensions
{
    public const string AdminOnly = "AdminOnly";
    public const string Staff = "Staff";

    public static SymmetricSecurityKey GetSigningKey(IConfiguration config)
    {
        var key = config["Jwt:Key"];
        if (string.IsNullOrEmpty(key) || key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key ayarlı değil ya da 32 karakterden kısa.");
        }
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public static IServiceCollection AddAppAuth(this IServiceCollection services, IConfiguration config)
    {
        var signingKey = GetSigningKey(config);

        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddSingleton<JwtTokenService>();

        services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", o =>
            {
                o.MapInboundClaims = false; // "name" ve "role" olduğu gibi kalsın
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = signingKey,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "name",
                    RoleClaimType = "role",
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(Staff, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Field)));

        return services;
    }

    /// <summary>Başlangıçta, göçten sonra: yapılandırmadaki kullanıcıları oluşturur/günceller.</summary>
    public static async Task SeedUsersAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        await UserSeeder.SeedAsync(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IPasswordHasher<AppUser>>());
    }
}
