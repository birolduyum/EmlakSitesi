using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Auth;

public static class UserSeeder
{
    public sealed record ConfiguredUser(string Username, string Password, UserRole Role);

    /// <summary>
    /// Users:{i}:Username/Password/Role yapılandırmasını doğrular ve okur. Rol yalnızca ad olabilir (Admin/Field, büyük-küçük harf önemsiz);
    /// "5" gibi sayısal ya da tanımsız değerler reddedilir. Örnek parola (degistir-beni) reddedilir; hata iletisi değeri değil anahtarı söyler.
    /// </summary>
    public static List<ConfiguredUser> ReadConfiguredUsers(IConfiguration config)
    {
        var users = new List<ConfiguredUser>();
        foreach (var entry in config.GetSection("Users").GetChildren())
        {
            var username = entry["Username"];
            var password = entry["Password"];
            var rawRole = entry["Role"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)
                || string.IsNullOrWhiteSpace(rawRole)
                || !TryParseRole(rawRole, out var role))
            {
                throw new InvalidOperationException($"Users:{entry.Key} eksik ya da geçersiz (Username/Password/Role; Role yalnızca Admin ya da Field olabilir).");
            }

            SecretGuard.ValidateUserPassword($"Users:{entry.Key}:Password", password);
            users.Add(new ConfiguredUser(username, password, role));
        }

        return users;
    }

    // Yalnızca tanımlı rol adları: "5" (sayısal) ve "Admin,Field" (virgüllü bayrak sözdizimi) Enum.TryParse ile geçerdi, burada reddedilir.
    private static bool TryParseRole(string raw, out UserRole role)
        => Enum.TryParse(raw.Trim(), ignoreCase: true, out role) && Enum.IsDefined(role)
           && Enum.GetNames<UserRole>().Contains(raw.Trim(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Yapılandırmadaki kullanıcıları oluşturur ya da günceller; yapılandırmada artık olmayan hesapları siler
    /// (ilan/gösterme kayıtları kullanıcı adını düz metin tutar, yabancı anahtar yoktur). Parola yalnızca hash olarak yazılır;
    /// hiçbir yere günlüklenmez. Users hiç tanımlı değilse hiçbir şey yapmaz (hesapları yanlışlıkla topluca silmemek için).
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, IPasswordHasher<AppUser> hasher)
    {
        var configured = ReadConfiguredUsers(config);

        foreach (var (username, password, role) in configured)
        {
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

        if (configured.Count > 0)
        {
            var names = configured.Select(c => c.Username).ToArray();
            var stale = await db.Users.Where(u => !names.Contains(u.Username)).ToListAsync();
            db.Users.RemoveRange(stale);
        }

        await db.SaveChangesAsync();
    }
}
