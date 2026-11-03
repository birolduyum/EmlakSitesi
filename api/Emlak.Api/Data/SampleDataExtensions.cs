using Emlak.Api.Photos;

namespace Emlak.Api.Data;

public static class SampleDataExtensions
{
    /// <summary>Seed:Sample=true ise ve ilan yoksa örnek veriyi ekler (göç ve kullanıcı tohumundan sonra çağrılır).</summary>
    public static async Task SeedSampleDataAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        if (!sp.GetRequiredService<IConfiguration>().GetValue<bool>("Seed:Sample")) { return; }

        await SampleDataSeeder.SeedAsync(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<PhotoStorage>());
    }
}
