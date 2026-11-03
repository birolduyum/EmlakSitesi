using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class DataTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DataTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Listing_WithOwnerPhotosAndTags_RoundTrips()
    {
        _ = _factory.Server; // ana makineyi başlatır (göç çalışır)
        await _factory.ResetAsync();

        int id;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var listing = new Listing
            {
                Title = "Deniz manzaralı 3+2",
                Type = ListingType.Satilik,
                Price = 22_000_000m,
                Currency = Currency.TRY,
                City = "İstanbul",
                District = "Kadıköy",
                Neighborhood = "Caddebostan",
                Rooms = "3+2",
                Description = "Açıklama",
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "admin1",
                Owner = new Owner { Name = "Ayşe Yılmaz", Phone = "0555 000 00 00" },
                Photos =
                {
                    new Photo { FilePath = "a.jpg", SortOrder = 0 },
                    new Photo { FilePath = "b.jpg", SortOrder = 1 },
                },
                Tags =
                {
                    new FeatureTag { Name = "Asansör", Group = TagGroup.Ic },
                    new FeatureTag { Name = "Otopark", Group = TagGroup.Dis },
                    new FeatureTag { Name = "Metroya yakın", Group = TagGroup.Konum },
                },
            };
            db.Listings.Add(listing);
            await db.SaveChangesAsync();
            id = listing.Id;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var loaded = await db.Listings
                .Include(l => l.Owner).Include(l => l.Photos).Include(l => l.Tags)
                .SingleAsync(l => l.Id == id);

            Assert.Equal("Ayşe Yılmaz", loaded.Owner.Name);
            Assert.Equal(2, loaded.Photos.Count);
            Assert.Equal(3, loaded.Tags.Count);
            Assert.Equal(22_000_000m, loaded.Price);
            Assert.Equal(ListingType.Satilik, loaded.Type);
        }
    }

    [Fact]
    public async Task CurrencyRates_AreSeeded()
    {
        // Kendi taze fabrikası/veritabanı: ResetAsync hiç çağrılmaz, satırlar yalnızca göçün tohumundan gelebilir.
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        _ = factory.Server; // göç + tohum
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rates = await db.CurrencyRates.ToListAsync();

        Assert.Contains(rates, r => r.Currency == Currency.USD && r.TryPerUnit > 0);
        Assert.Contains(rates, r => r.Currency == Currency.EUR && r.TryPerUnit > 0);
    }

    [Fact]
    public async Task Migrations_Apply_OnFreshDatabase_AndKeepRatesUpdatedAt()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        _ = factory.Server; // tüm göç zinciri taze veritabanına uygulanır
        var custom = new DateTimeOffset(2026, 12, 24, 9, 30, 0, TimeSpan.Zero);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usd = await db.CurrencyRates.SingleAsync(r => r.Currency == Currency.USD);
            usd.UpdatedAt = custom;
            usd.UpdatedBy = "admin1";
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync(); // ikinci çalıştırma: bekleyen göç yok, tohum satırlarına dokunulmaz
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var usd = await db.CurrencyRates.AsNoTracking().SingleAsync(r => r.Currency == Currency.USD);
            Assert.Equal(custom, usd.UpdatedAt);
            Assert.Equal("admin1", usd.UpdatedBy);
        }
    }
}
