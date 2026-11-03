using System.Net.Http.Json;
using System.Text.Json;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Emlak.Api.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class SampleDataTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SampleDataTests(ApiFactory factory) => _factory = factory;

    private async Task SeedAsync()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        await SampleDataSeeder.SeedAsync(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<PhotoStorage>());
    }

    [Fact]
    public async Task Seed_CreatesVariedListings_AndIsIdempotent()
    {
        await SeedAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var storage = sp.GetRequiredService<PhotoStorage>();

        var count = await db.Listings.CountAsync();
        var tagCount = await db.FeatureTags.CountAsync();
        Assert.InRange(count, 8, 12);

        await SampleDataSeeder.SeedAsync(db, storage); // ikinci çalıştırma hiçbir şey eklemez
        Assert.Equal(count, await db.Listings.CountAsync());
        Assert.Equal(tagCount, await db.FeatureTags.CountAsync());

        var listings = await db.Listings.Include(l => l.Photos).Include(l => l.Owner).Include(l => l.Tags).AsNoTracking().ToListAsync();
        Assert.Contains(listings, l => l.Type == ListingType.Satilik);
        Assert.Contains(listings, l => l.Type == ListingType.Kiralik);
        Assert.Equal(3, listings.Select(l => l.Currency).Distinct().Count());
        Assert.Contains(listings, l => l.Photos.Count == 0);
        Assert.Contains(listings, l => l.Photos.Count == 1);
        Assert.Contains(listings, l => l.IsDeleted && l.DeletedBy == "admin1" && l.DeletedAt is not null);
        Assert.All(listings, l => Assert.NotNull(l.Owner));
        Assert.All(listings.Where(l => l.Type == ListingType.Kiralik), l => { Assert.NotNull(l.Deposit); Assert.NotNull(l.Aidat); });
        Assert.Contains(listings, l => l.Tags.Count > 0);
        Assert.True(listings.Select(l => l.CreatedAt.Date).Distinct().Count() > 5);

        foreach (var photo in listings.SelectMany(l => l.Photos))
        {
            Assert.True(File.Exists(storage.Resolve(photo.FilePath)), photo.FilePath);
        }

        Assert.Equal(3, await db.Showings.CountAsync());
        Assert.All(await db.Showings.ToListAsync(), s => Assert.Equal("saha1", s.Username));
        var deletedIds = listings.Where(l => l.IsDeleted).Select(l => l.Id).ToHashSet();
        Assert.DoesNotContain(await db.Showings.ToListAsync(), s => deletedIds.Contains(s.ListingId));
    }

    [Fact]
    public async Task Seed_DoesNothing_WhenOnlyASoftDeletedListingExists()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        db.Listings.Add(new ListingBuilder().Deleted().Build());
        await db.SaveChangesAsync();

        await SampleDataSeeder.SeedAsync(db, sp.GetRequiredService<PhotoStorage>());

        Assert.Equal(1, await db.Listings.CountAsync());
        Assert.Equal(0, await db.FeatureTags.CountAsync());
    }

    [Fact]
    public async Task VisitorList_AfterSeeding_HidesDeletedAndOwnerData()
    {
        await SeedAsync();
        string[] ownerStrings;
        string deletedTitle;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owners = await db.Owners.AsNoTracking().ToListAsync();
            ownerStrings = owners.SelectMany(o => new[] { o.Name, o.Phone }).ToArray();
            deletedTitle = (await db.Listings.AsNoTracking().SingleAsync(l => l.IsDeleted)).Title;
        }

        // Ziyaretçi ucu tür verilmezse varsayılan olarak satılıkları döner; iki türü de iste.
        var client = _factory.CreateClient();
        var satilik = await client.GetStringAsync("/api/listings?type=Satilik&pageSize=50");
        var kiralik = await client.GetStringAsync("/api/listings?type=Kiralik&pageSize=50");
        var json = satilik + kiralik;

        Assert.DoesNotContain(deletedTitle, json);
        foreach (var s in ownerStrings.Concat(["owner", "phone", "createdBy", "deletedBy", "isDeleted"]))
        {
            Assert.DoesNotContain(s, json, StringComparison.OrdinalIgnoreCase);
        }

        var items = new List<JsonElement>();
        foreach (var part in new[] { satilik, kiralik })
        {
            items.AddRange(JsonDocument.Parse(part).RootElement.GetProperty("items").EnumerateArray());
        }

        Assert.Equal(9, items.Count); // 10 ilandan silinmiş olan görünmez
        Assert.Contains(items, i => i.GetProperty("photoUrls").GetArrayLength() > 1);
        Assert.Contains(items, i => i.GetProperty("photoUrls").GetArrayLength() == 0);
    }
}
