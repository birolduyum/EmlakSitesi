using System.Net.Http.Headers;
using System.Net.Http.Json;
using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Emlak.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Test hesaplarının ortak parolası (yalnızca test fabrikasında; gerçek parola .env'den gelir).</summary>
    public const string TestPassword = "Test-Parola-123!";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16")
        .Build();

    public async Task InitializeAsync() => await _db.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    public override async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:Key", "test-only-signing-key-at-least-32-chars-long!!");
        (string Name, string Role)[] users = [("admin1", "Admin"), ("admin2", "Admin"), ("saha1", "Field")];
        for (var i = 0; i < users.Length; i++)
        {
            builder.UseSetting($"Users:{i}:Username", users[i].Name);
            builder.UseSetting($"Users:{i}:Password", TestPassword);
            builder.UseSetting($"Users:{i}:Role", users[i].Role);
        }
    }

    /// <summary>Public şemadaki tüm tabloları temizler (tablo yoksa hiçbir şey yapmaz).</summary>
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();
        const string sql = """
            DO $$
            DECLARE r record;
            BEGIN
              FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory' LOOP
                EXECUTE 'TRUNCATE TABLE ' || quote_ident(r.tablename) || ' RESTART IDENTITY CASCADE';
              END LOOP;
              -- Tohum (HasData) kurları temizlikle silinir; testlerin varsayımı "kur her zaman vardır" olduğu için geri konur.
              IF to_regclass('public."CurrencyRates"') IS NOT NULL THEN
                -- USD=35 / EUR=38 değerleri AppDbContext HasData ile aynı olmalı.
                INSERT INTO "CurrencyRates" ("Currency", "TryPerUnit", "UpdatedAt", "UpdatedBy")
                VALUES ('USD', 35, now(), 'seed'), ('EUR', 38, now(), 'seed');
              END IF;
            END $$;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();

        // Temizlik AppUsers'ı da boşalttığı için test hesapları yapılandırmadan yeniden tohumlanır.
        _ = Server; // ana makine (göç + ilk tohum) hazır olsun
        await using var scope = Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await UserSeeder.SeedAsync(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<IPasswordHasher<AppUser>>());
    }

    /// <summary>Verilen test hesabıyla giriş yapar; Bearer belirteci ayarlı bir HttpClient döner.</summary>
    public async Task<HttpClient> LoginAsync(string username)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { username, password = TestPassword });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<LoginBody>()
                   ?? throw new InvalidOperationException("Giriş cevabı boş.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.Token);
        return client;
    }

    private sealed record LoginBody(string Token, string Username, string Role);
}
