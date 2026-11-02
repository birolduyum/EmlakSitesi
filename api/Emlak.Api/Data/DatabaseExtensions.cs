using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Data;

public static class DatabaseExtensions
{
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Default")
                 ?? throw new InvalidOperationException("ConnectionStrings:Default ayarlı değil.");
        return services.AddDbContext<AppDbContext>(o => o.UseNpgsql(cs));
    }

    public static void MigrateDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
    }
}
