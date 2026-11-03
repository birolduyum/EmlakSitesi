using System.Net;
using System.Net.Http.Json;
using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

/// <summary>Örnek sırlar, geçersiz roller ve yapılandırmadan kaldırılan hesaplar.</summary>
public class StartupGuardTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public StartupGuardTests(ApiFactory factory) => _factory = factory;

    private static IConfiguration Config(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    private static IConfiguration UsersConfig(params (string Name, string Password, string Role)[] users)
        => Config(users.SelectMany((u, i) => new (string, string?)[]
        {
            ($"Users:{i}:Username", u.Name), ($"Users:{i}:Password", u.Password), ($"Users:{i}:Role", u.Role),
        }).ToArray());

    private static string AllMessages(Exception? e)
    {
        var parts = new List<string>();
        for (; e is not null; e = e.InnerException) { parts.Add(e.Message); }
        return string.Join(" | ", parts);
    }

    [Theory]
    [InlineData("degistir-beni-en-az-32-karakterlik-gizli-anahtar")]
    [InlineData("DEGISTIR-BENI-en-az-32-karakterlik-gizli-anahtar")]
    public void JwtKey_Placeholder_IsRejectedWithTurkishMessage(string key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SecretGuard.ValidateJwtKey(key));
        Assert.Equal("Jwt:Key hâlâ örnek değer; .env dosyasında değiştirin.", ex.Message);
        Assert.DoesNotContain(key, ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kisa-anahtar")]
    [InlineData("12345678901234567890123456789ab")] // 31 karakter
    public void JwtKey_MissingOrShort_IsRejected(string? key)
        => Assert.Throws<InvalidOperationException>(() => SecretGuard.ValidateJwtKey(key));

    [Fact]
    public void JwtKey_Valid_IsAccepted()
        => SecretGuard.ValidateJwtKey("test-only-signing-key-at-least-32-chars-long!!");

    [Fact]
    public void UserPassword_Placeholder_IsRejected_NamingTheKeyNotTheValue()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            UserSeeder.ReadConfiguredUsers(UsersConfig(("admin1", "Degistir-Beni", "Admin"))));

        Assert.Contains("Users:0:Password", ex.Message);
        Assert.DoesNotContain("Degistir-Beni", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("5")]
    [InlineData("99")]
    [InlineData("Admin,Field")]
    [InlineData("Root")]
    [InlineData("")]
    public void User_InvalidRole_Throws(string role)
        => Assert.Throws<InvalidOperationException>(() =>
            UserSeeder.ReadConfiguredUsers(UsersConfig(("u", "guclu-parola-1", role))));

    [Theory]
    [InlineData("admin", UserRole.Admin)]
    [InlineData("FIELD", UserRole.Field)]
    [InlineData("Field", UserRole.Field)]
    public void User_RoleNames_AreCaseInsensitive(string role, UserRole expected)
        => Assert.Equal(expected, Assert.Single(UserSeeder.ReadConfiguredUsers(UsersConfig(("u", "guclu-parola-1", role)))).Role);

    [Fact]
    public void Startup_WithPlaceholderJwtKey_IsRefused()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Database=yok;Username=yok;Password=yok"); // bağlanılmaz: başlangıç daha önce reddedilir
            b.UseSetting("Jwt:Key", "degistir-beni-en-az-32-karakterlik-gizli-anahtar");
        });

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.True(AllMessages(ex).Contains("Jwt:Key hâlâ örnek değer; .env dosyasında değiştirin"), AllMessages(ex));
    }

    [Fact]
    public void Startup_WithPlaceholderUserPassword_IsRefused()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Database=yok;Username=yok;Password=yok"); // bağlanılmaz: başlangıç daha önce reddedilir
            b.UseSetting("Jwt:Key", "test-only-signing-key-at-least-32-chars-long!!");
            b.UseSetting("Users:0:Username", "admin1");
            b.UseSetting("Users:0:Password", "degistir-beni");
            b.UseSetting("Users:0:Role", "Admin");
        });

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains("Users:0:Password", AllMessages(ex));
    }

    [Fact]
    public async Task Seeder_RemovesUsersMissingFromConfig_AndTheirLoginFails()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        var config = UsersConfig(("admin1", ApiFactory.TestPassword, "Admin"), ("admin2", ApiFactory.TestPassword, "Admin"));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            await UserSeeder.SeedAsync(sp.GetRequiredService<AppDbContext>(), config, sp.GetRequiredService<IPasswordHasher<AppUser>>());
            var names = await sp.GetRequiredService<AppDbContext>().Users.Select(u => u.Username).OrderBy(n => n).ToListAsync();
            Assert.Equal(["admin1", "admin2"], names); // saha1 yapılandırmadan çıktı
        }

        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { username = "saha1", password = ApiFactory.TestPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        await _factory.ResetAsync(); // diğer testler için üç hesabı geri yükle
    }

    [Fact]
    public async Task Seeder_WithNoUsersConfigured_DeletesNothing()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();

        await UserSeeder.SeedAsync(db, Config(), sp.GetRequiredService<IPasswordHasher<AppUser>>());

        Assert.Equal(3, await db.Users.CountAsync());
    }
}
