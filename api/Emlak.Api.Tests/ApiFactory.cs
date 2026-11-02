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

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
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
            END $$;
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
