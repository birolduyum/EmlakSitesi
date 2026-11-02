using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Emlak.Api.Tests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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
    }
}
