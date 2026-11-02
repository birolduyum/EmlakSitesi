using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Auth;

public static class UserSeeder
{
    /// <summary>
    /// Users:{i}:Username/Password/Role yapılandırmasından kullanıcıları oluşturur ya da günceller.
    /// Parola yalnızca hash olarak yazılır; hiçbir yere günlüklenmez. Users boşsa hiçbir şey yapmaz.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, IPasswordHasher<AppUser> hasher)
    {
        foreach (var entry in config.GetSection("Users").GetChildren())
        {
            var username = entry["Username"];
            var password = entry["Password"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)
                || !Enum.TryParse<UserRole>(entry["Role"], ignoreCase: true, out var role))
            {
                throw new InvalidOperationException($"Users:{entry.Key} eksik ya da geçersiz (Username/Password/Role).");
            }

            var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user is null)
            {
                db.Users.Add(new AppUser { Username = username, Role = role, PasswordHash = hasher.HashPassword(null!, password) });
                continue;
            }

            user.Role = role;
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            {
                user.PasswordHash = hasher.HashPassword(user, password);
            }
        }

        await db.SaveChangesAsync();
    }
}
