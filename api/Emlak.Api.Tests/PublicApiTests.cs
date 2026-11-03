using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class PublicApiTests : IClassFixture<ApiFactory>
{
    private const string OwnerName = "Gizli Sahip";
    private const string OwnerPhone = "0532 999 88 77";
    private const string OwnerNote = "sadece sabah ara";

    private readonly ApiFactory _factory;

    public PublicApiTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> FreshClientAsync()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        return _factory.CreateClient();
    }

    private async Task SeedAsync(Action<AppDbContext> add)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        add(db);
        await db.SaveChangesAsync();
    }

    private async Task<Listing> SeedListingAsync(ListingBuilder b)
    {
        var l = b.Build();
        await SeedAsync(db => db.Listings.Add(l));
        return l;
    }

    private static ListingBuilder WithSecretOwner(ListingBuilder b) => b.WithOwner(OwnerName, OwnerPhone, OwnerNote);

    private static void AssertNoOwnerData(string json)
    {
        foreach (var forbidden in new[] { OwnerName, OwnerPhone, "999 88 77", OwnerNote, "owner", "phone", "createdBy", "deletedBy", "isDeleted" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Detail_Json_ContainsNoOwnerFields()
    {
        var client = await FreshClientAsync();
        var tag = new FeatureTag { Name = "Deniz manzarası", Group = TagGroup.Dis };
        var l = await SeedListingAsync(WithSecretOwner(new ListingBuilder().WithPhotos("a/1.jpg").WithTags(tag)));

        var res = await client.GetAsync($"/api/listings/{l.Id}");
        var json = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        AssertNoOwnerData(json);
        Assert.Contains("Deniz manzarası", json);
        Assert.Contains("\"type\":\"Satilik\"", json);
    }

    [Fact]
    public async Task Detail_ExposesUpdatedAt_FallingBackToCreatedAt()
    {
        var client = await FreshClientAsync();
        var created = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var updated = new DateTime(2026, 4, 2, 11, 30, 0, DateTimeKind.Utc);
        var untouched = await SeedListingAsync(new ListingBuilder().CreatedAt(created));
        var edited = await SeedListingAsync(new ListingBuilder().CreatedAt(created).UpdatedAt(updated));

        var a = await client.GetFromJsonAsync<JsonElement>($"/api/listings/{untouched.Id}");
        var b = await client.GetFromJsonAsync<JsonElement>($"/api/listings/{edited.Id}");

        Assert.Equal(created, a.GetProperty("updatedAt").GetDateTime().ToUniversalTime());
        Assert.Equal(updated, b.GetProperty("updatedAt").GetDateTime().ToUniversalTime());
        Assert.False(b.TryGetProperty("updatedBy", out _));
    }

    [Fact]
    public async Task List_Json_ContainsNoOwnerFields()
    {
        var client = await FreshClientAsync();
        await SeedListingAsync(WithSecretOwner(new ListingBuilder().Titled("görünür")));

        var res = await client.GetAsync("/api/listings");
        var json = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("görünür", json);
        AssertNoOwnerData(json);
    }

    [Fact]
    public async Task DeletedListing_ById_Returns404()
    {
        var client = await FreshClientAsync();
        var l = await SeedListingAsync(new ListingBuilder().Deleted());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/listings/{l.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/listings/999999")).StatusCode);
    }

    [Fact]
    public async Task DeletedListing_NotInList()
    {
        var client = await FreshClientAsync();
        await SeedListingAsync(new ListingBuilder().Titled("aktif"));
        await SeedListingAsync(new ListingBuilder().Titled("silinmis").Deleted());

        var page = await client.GetFromJsonAsync<JsonElement>("/api/listings");

        Assert.Equal(1, page.GetProperty("total").GetInt32());
        Assert.Equal("aktif", page.GetProperty("items")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Detail_ShowsShowingCount()
    {
        var client = await FreshClientAsync();
        var l = await SeedListingAsync(new ListingBuilder());
        var gone = await SeedListingAsync(new ListingBuilder().Deleted());
        await SeedAsync(db => db.Showings.AddRange(
            new Showing { ListingId = l.Id, Username = "saha1", TakenAt = DateTime.UtcNow },
            new Showing { ListingId = l.Id, Username = "saha1", TakenAt = DateTime.UtcNow.AddDays(-1), ReturnedAt = DateTime.UtcNow },
            new Showing { ListingId = gone.Id, Username = "saha1", TakenAt = DateTime.UtcNow }));

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/listings/{l.Id}");
        var deleted = await client.GetAsync($"/api/listings/{gone.Id}");

        Assert.Equal(2, detail.GetProperty("showingCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.DoesNotContain("showingCount", await deleted.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ListWithoutPhotos_HasEmptyPhotoUrls()
    {
        var client = await FreshClientAsync();
        await SeedListingAsync(new ListingBuilder().Titled("fotosuz"));
        await SeedListingAsync(new ListingBuilder().Titled("fotolu").WithPhotos("7/a.jpg", "7/b.webp").CreatedAt(DateTime.UtcNow.AddDays(1)));

        var page = await client.GetFromJsonAsync<JsonElement>("/api/listings");
        var items = page.GetProperty("items");

        Assert.Equal("fotolu", items[0].GetProperty("title").GetString());
        Assert.Equal(["/photos/7/a.jpg", "/photos/7/b.webp"], items[0].GetProperty("photoUrls").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(JsonValueKind.Array, items[1].GetProperty("photoUrls").ValueKind);
        Assert.Equal(0, items[1].GetProperty("photoUrls").GetArrayLength());
    }

    [Fact]
    public async Task Filters_Endpoint_ReturnsCityAndRoomCounts()
    {
        var client = await FreshClientAsync();
        await SeedListingAsync(new ListingBuilder().In("Istanbul", "Kadikoy").WithRooms("3+1"));
        await SeedListingAsync(new ListingBuilder().In("Ankara", "Cankaya").WithRooms("3+1"));
        await SeedListingAsync(new ListingBuilder().In("Izmir", "Konak").OfType(ListingType.Kiralik).WithRooms("1+1"));

        var f = await client.GetFromJsonAsync<JsonElement>("/api/listings/filters?type=Satilik");

        Assert.Equal(["Ankara", "Istanbul"], f.GetProperty("cities").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(2, f.GetProperty("roomCounts").GetProperty("3+1").GetInt32());
        Assert.False(f.GetProperty("roomCounts").TryGetProperty("1+1", out _));
    }

    [Fact]
    public async Task List_HugePage_DoesNotError()
    {
        var client = await FreshClientAsync();
        await SeedListingAsync(new ListingBuilder());

        var res = await client.GetAsync("/api/listings?page=2147483647&pageSize=2147483647");
        var page = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(0, page.GetProperty("items").GetArrayLength());
        Assert.Equal(1, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_InvalidEnum_Returns400()
    {
        var client = await FreshClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/listings?type=Bogus")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/listings?sort=Bogus")).StatusCode);
    }

    [Fact]
    public async Task List_IgnoresOwnerTextAndStatusParams()
    {
        var client = await FreshClientAsync();
        await SeedListingAsync(WithSecretOwner(new ListingBuilder().Titled("aktif-sahipli")));
        await SeedListingAsync(new ListingBuilder().Titled("silinmis").Deleted());

        var res = await client.GetAsync($"/api/listings?status=Deleted&ownerText={Uri.EscapeDataString(OwnerPhone)}");
        var json = await res.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("aktif-sahipli", json);
        Assert.DoesNotContain("silinmis", json);
        AssertNoOwnerData(json);

        // OwnerText süzmez: eşleşmeyen bir değer de aktif ilanı getirir.
        var other = await client.GetFromJsonAsync<JsonElement>("/api/listings?ownerText=yokboyleBirSey");
        Assert.Equal(1, other.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Rooms_RepeatedQueryValues_AreBound()
    {
        var client = await FreshClientAsync();
        await SeedListingAsync(new ListingBuilder().Titled("a").WithRooms("3+1"));
        await SeedListingAsync(new ListingBuilder().Titled("b").WithRooms("2+1"));
        await SeedListingAsync(new ListingBuilder().Titled("c").WithRooms("4+1"));

        var page = await client.GetFromJsonAsync<JsonElement>("/api/listings?rooms=3%2B1&rooms=2%2B1");

        Assert.Equal(2, page.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Tags_And_Site_Endpoints_AreAnonymous()
    {
        var client = await FreshClientAsync();
        await SeedAsync(db => db.FeatureTags.Add(new FeatureTag { Name = "Asansör", Group = TagGroup.Ic }));

        var tags = await client.GetFromJsonAsync<JsonElement>("/api/tags");
        var site = await client.GetFromJsonAsync<JsonElement>("/api/site");

        Assert.Equal("Asansör", tags[0].GetProperty("name").GetString());
        Assert.True(site.TryGetProperty("advisorName", out _));
        Assert.True(site.TryGetProperty("advisorPhone", out _));
        Assert.True(site.TryGetProperty("whatsappUrl", out _));
    }
}
