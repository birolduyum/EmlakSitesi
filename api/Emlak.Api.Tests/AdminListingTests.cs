using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class AdminListingTests : IClassFixture<ApiFactory>
{
    private const string OwnerName = "Gizli Sahip";
    private const string OwnerPhone = "0532 999 88 77";
    private const string OwnerNote = "sadece sabah ara";

    private readonly ApiFactory _factory;

    public AdminListingTests(ApiFactory factory) => _factory = factory;

    private async Task ResetAsync()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<Listing> SeedAsync(ListingBuilder b)
    {
        var l = b.Build();
        await WithDbAsync(async db => { db.Listings.Add(l); await db.SaveChangesAsync(); return 0; });
        return l;
    }

    private static Dictionary<string, object?> ValidInput(Action<Dictionary<string, object?>>? tweak = null)
    {
        var d = new Dictionary<string, object?>
        {
            ["title"] = "Yeni ilan",
            ["type"] = "Satilik",
            ["price"] = 2_500_000,
            ["currency"] = "TRY",
            ["city"] = "Istanbul",
            ["district"] = "Kadikoy",
            ["neighborhood"] = "Moda",
            ["rooms"] = "3+1",
            ["bathrooms"] = 1,
            ["grossM2"] = 120,
            ["netM2"] = 100,
            ["floor"] = 2,
            ["totalFloors"] = 5,
            ["buildingAge"] = 5,
            ["heating"] = "Kombi",
            ["furnishing"] = "Esyasiz",
            ["usage"] = "Bos",
            ["deed"] = "KatMulkiyeti",
            ["kitchen"] = "Kapali",
            ["description"] = "Açıklama",
            ["ownerName"] = OwnerName,
            ["ownerPhone"] = OwnerPhone,
            ["ownerNote"] = OwnerNote,
            ["tagIds"] = Array.Empty<int>(),
        };
        tweak?.Invoke(d);
        return d;
    }

    private static ListingBuilder Secret(ListingBuilder b) => b.WithOwner(OwnerName, OwnerPhone, OwnerNote);

    [Fact]
    public async Task Create_AsAdmin_SetsCreatedByAndOwner()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin2");

        var res = await admin.PostAsJsonAsync("/api/admin/listings", ValidInput());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.NotNull(res.Headers.Location);
        var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        var id = json.GetProperty("id").GetInt32();
        Assert.EndsWith($"/api/admin/listings/{id}", res.Headers.Location!.ToString());

        var saved = await WithDbAsync(db => db.Listings.Include(l => l.Owner).SingleAsync(l => l.Id == id));
        Assert.Equal("admin2", saved.CreatedBy);
        Assert.True((DateTime.UtcNow - saved.CreatedAt).Duration() < TimeSpan.FromMinutes(1));
        Assert.Equal(OwnerName, saved.Owner.Name);
        Assert.Equal(OwnerPhone, saved.Owner.Phone);
        Assert.Equal(OwnerNote, saved.Owner.Note);
    }

    [Fact]
    public async Task AdminDetail_IncludesShowingCount_FieldDetailToo()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder()));
        await WithDbAsync(async db =>
        {
            db.Showings.AddRange(
                new Showing { ListingId = l.Id, Username = "saha1", TakenAt = DateTime.UtcNow },
                new Showing { ListingId = l.Id, Username = "saha1", TakenAt = DateTime.UtcNow.AddDays(-1), ReturnedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return 0;
        });
        var admin = await _factory.LoginAsync("admin1");
        var field = await _factory.LoginAsync("saha1");

        var adminJson = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/listings/{l.Id}");
        var fieldJson = await field.GetFromJsonAsync<JsonElement>($"/api/admin/listings/{l.Id}");

        Assert.Equal(2, adminJson.GetProperty("showingCount").GetInt32());
        Assert.Equal(OwnerName, adminJson.GetProperty("owner").GetProperty("name").GetString());
        Assert.Equal(2, fieldJson.GetProperty("showingCount").GetInt32());
        Assert.False(fieldJson.TryGetProperty("owner", out _));
    }

    [Fact]
    public async Task AdminList_NewFilters_AndOwnerOnlyForAdmin()
    {
        await ResetAsync();
        await SeedAsync(Secret(new ListingBuilder().Titled("kucuk")));
        var big = await SeedAsync(Secret(new ListingBuilder().Titled("buyuk")));
        await WithDbAsync(async db =>
        {
            var row = await db.Listings.SingleAsync(x => x.Id == big.Id);
            row.Bathrooms = 3; row.TotalFloors = 12;
            await db.SaveChangesAsync();
            return 0;
        });
        var admin = await _factory.LoginAsync("admin1");
        var field = await _factory.LoginAsync("saha1");

        var byBath = await admin.GetFromJsonAsync<JsonElement>("/api/admin/listings?minBathrooms=2");
        var byMinFloors = await admin.GetFromJsonAsync<JsonElement>("/api/admin/listings?minTotalFloors=10");
        var byMaxFloors = await admin.GetFromJsonAsync<JsonElement>("/api/admin/listings?maxTotalFloors=6");
        var fieldList = await field.GetStringAsync("/api/admin/listings?minBathrooms=2");

        Assert.Equal("buyuk", Assert.Single(byBath.GetProperty("items").EnumerateArray()).GetProperty("title").GetString());
        Assert.Equal(OwnerName, byBath.GetProperty("items")[0].GetProperty("owner").GetProperty("name").GetString());
        Assert.Equal(1, byMinFloors.GetProperty("total").GetInt32());
        Assert.Equal("kucuk", Assert.Single(byMaxFloors.GetProperty("items").EnumerateArray()).GetProperty("title").GetString());
        Assert.Contains("buyuk", fieldList);
        Assert.DoesNotContain(OwnerName, fieldList);
    }

    [Fact]
    public async Task Create_AsField_Returns403()
    {
        await ResetAsync();
        var field = await _factory.LoginAsync("saha1");

        var res = await field.PostAsJsonAsync("/api/admin/listings", ValidInput());

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Listings.CountAsync()));
    }

    [Fact]
    public async Task Update_SetsUpdatedBy()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder()));
        var admin = await _factory.LoginAsync("admin2");

        var res = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}",
            ValidInput(d => { d["title"] = "Güncel başlık"; d["ownerName"] = "Yeni Sahip"; }));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var saved = await WithDbAsync(db => db.Listings.Include(x => x.Owner).SingleAsync(x => x.Id == l.Id));
        Assert.Equal("Güncel başlık", saved.Title);
        Assert.Equal("Yeni Sahip", saved.Owner.Name);
        Assert.Equal("admin2", saved.UpdatedBy);
        Assert.NotNull(saved.UpdatedAt);
        Assert.Equal("admin1", saved.CreatedBy);
    }

    [Fact]
    public async Task Update_DeletedListing_Returns404()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder().Deleted());
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}", ValidInput());

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Delete_IsSoft_ListingStillInDbWithDeletedBy()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder()));
        var admin = await _factory.LoginAsync("admin2");

        var res = await admin.DeleteAsync($"/api/admin/listings/{l.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var saved = await WithDbAsync(db => db.Listings.Include(x => x.Owner).SingleAsync(x => x.Id == l.Id));
        Assert.True(saved.IsDeleted);
        Assert.Equal("admin2", saved.DeletedBy);
        Assert.NotNull(saved.DeletedAt);
        Assert.Equal(OwnerName, saved.Owner.Name);
    }

    [Fact]
    public async Task Delete_Twice_IsIdempotentAndKeepsFirstAudit()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var a1 = await _factory.LoginAsync("admin1");
        var a2 = await _factory.LoginAsync("admin2");

        Assert.Equal(HttpStatusCode.NoContent, (await a1.DeleteAsync($"/api/admin/listings/{l.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await a2.DeleteAsync($"/api/admin/listings/{l.Id}")).StatusCode);

        var saved = await WithDbAsync(db => db.Listings.SingleAsync(x => x.Id == l.Id));
        Assert.Equal("admin1", saved.DeletedBy);
        Assert.Equal(HttpStatusCode.NotFound, (await a1.DeleteAsync("/api/admin/listings/99999")).StatusCode);
    }

    [Fact]
    public async Task Delete_ThenVisitorList_DoesNotShow()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder().Titled("Silinecek ilan"));
        var admin = await _factory.LoginAsync("admin1");
        var visitor = _factory.CreateClient();
        Assert.Contains("Silinecek ilan", await visitor.GetStringAsync("/api/listings"));

        await admin.DeleteAsync($"/api/admin/listings/{l.Id}");

        Assert.DoesNotContain("Silinecek ilan", await visitor.GetStringAsync("/api/listings"));
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/listings/{l.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_ThenAdminListStatusDeleted_Shows()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder().Titled("Silinecek ilan"));
        var admin = await _factory.LoginAsync("admin1");
        await admin.DeleteAsync($"/api/admin/listings/{l.Id}");

        var deleted = await admin.GetStringAsync("/api/admin/listings?status=Deleted");
        var active = await admin.GetStringAsync("/api/admin/listings");
        var all = await admin.GetStringAsync("/api/admin/listings?status=All");

        Assert.Contains("Silinecek ilan", deleted);
        Assert.DoesNotContain("Silinecek ilan", active);
        Assert.Contains("Silinecek ilan", all);
    }

    [Fact]
    public async Task Restore_ClearsFlagAndSetsRestoredBy()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder().Deleted());
        var admin = await _factory.LoginAsync("admin2");

        var res = await admin.PostAsync($"/api/admin/listings/{l.Id}/restore", null);

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var saved = await WithDbAsync(db => db.Listings.SingleAsync(x => x.Id == l.Id));
        Assert.False(saved.IsDeleted);
        Assert.Equal("admin2", saved.RestoredBy);
        Assert.NotNull(saved.RestoredAt);
        Assert.Equal("admin1", saved.DeletedBy); // silme geçmişi korunur
        Assert.Contains(l.Title, await _factory.CreateClient().GetStringAsync("/api/listings"));
    }

    [Fact]
    public async Task AdminDetail_AsField_HasNoOwnerField()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder()));
        var field = await _factory.LoginAsync("saha1");

        var res = await field.GetAsync($"/api/admin/listings/{l.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        AssertNoOwnerData(await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AdminDetail_AsAdmin_HasOwner()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder()));
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.GetAsync($"/api/admin/listings/{l.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var owner = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("owner");
        Assert.Equal(OwnerName, owner.GetProperty("name").GetString());
        Assert.Equal(OwnerPhone, owner.GetProperty("phone").GetString());
        Assert.Equal(OwnerNote, owner.GetProperty("note").GetString());
    }

    [Fact]
    public async Task AdminDetail_AsAdmin_HasPhotosWithIds()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder().WithPhotos("9/b.jpg", "9/a.png")));
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.GetAsync($"/api/admin/listings/{l.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var photos = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("photos").EnumerateArray().ToList();
        Assert.Equal(2, photos.Count);
        Assert.Equal(new[] { 0, 1 }, photos.Select(p => p.GetProperty("sortOrder").GetInt32()));
        Assert.Equal(new[] { "/photos/9/b.jpg", "/photos/9/a.png" }, photos.Select(p => p.GetProperty("url").GetString()));
        Assert.Equal(l.Photos.OrderBy(p => p.SortOrder).Select(p => p.Id), photos.Select(p => p.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task AdminDetail_AsField_HasNoPhotoIdsOrOwner()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder().WithPhotos("9/b.jpg")));
        var field = await _factory.LoginAsync("saha1");

        var res = await field.GetAsync($"/api/admin/listings/{l.Id}");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        AssertNoOwnerData(body);
        var root = JsonDocument.Parse(body).RootElement;
        Assert.False(root.TryGetProperty("photos", out _));
        Assert.Equal("/photos/9/b.jpg", root.GetProperty("photoUrls")[0].GetString());
    }

    [Fact]
    public async Task AdminDetail_IncludesDeleted_ForAdmin_But404ForField()
    {
        await ResetAsync();
        var l = await SeedAsync(Secret(new ListingBuilder().Deleted()));
        var admin = await _factory.LoginAsync("admin1");
        var field = await _factory.LoginAsync("saha1");

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/admin/listings/{l.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await field.GetAsync($"/api/admin/listings/{l.Id}")).StatusCode);
    }

    [Fact]
    public async Task AdminList_AsField_NeverShowsDeletedOrOwner()
    {
        await ResetAsync();
        await SeedAsync(Secret(new ListingBuilder().Titled("Aktif ilan")));
        await SeedAsync(Secret(new ListingBuilder().Titled("Silinmiş ilan").Deleted()));
        var field = await _factory.LoginAsync("saha1");

        foreach (var url in new[]
        {
            "/api/admin/listings",
            "/api/admin/listings?status=Deleted",
            "/api/admin/listings?status=All",
            "/api/admin/listings?ownerText=Gizli",
            "/api/admin/listings?ownerText=999",
        })
        {
            var body = await field.GetStringAsync(url);
            Assert.Contains("Aktif ilan", body);
            Assert.DoesNotContain("Silinmiş ilan", body);
            AssertNoOwnerData(body);
        }
    }

    [Fact]
    public async Task AdminList_AsAdmin_OwnerTextFiltersAndOwnerIsShown()
    {
        await ResetAsync();
        await SeedAsync(Secret(new ListingBuilder().Titled("Sahipli ilan")));
        await SeedAsync(new ListingBuilder().Titled("Başka ilan").WithOwner("Diğer Kişi", "0500 111 22 33"));
        var admin = await _factory.LoginAsync("admin1");

        var body = await admin.GetStringAsync("/api/admin/listings?ownerText=Gizli");

        Assert.Contains("Sahipli ilan", body);
        Assert.DoesNotContain("Başka ilan", body);
        Assert.Contains(OwnerPhone, body);
    }

    [Fact]
    public async Task AdminList_WithoutType_ReturnsSatilikAndKiralik()
    {
        await ResetAsync();
        await SeedAsync(new ListingBuilder().Titled("Satılık A"));
        await SeedAsync(new ListingBuilder().Titled("Kiralık B").OfType(ListingType.Kiralik));
        var admin = await _factory.LoginAsync("admin1");
        var field = await _factory.LoginAsync("saha1");

        foreach (var c in new[] { admin, field })
        {
            var body = await c.GetStringAsync("/api/admin/listings");
            Assert.Contains("Satılık A", body);
            Assert.Contains("Kiralık B", body);
        }
    }

    [Fact]
    public async Task AdminList_WithTypeKiralik_ReturnsOnlyKiralik()
    {
        await ResetAsync();
        await SeedAsync(new ListingBuilder().Titled("Satılık A"));
        await SeedAsync(new ListingBuilder().Titled("Kiralık B").OfType(ListingType.Kiralik));
        var admin = await _factory.LoginAsync("admin1");

        var body = await admin.GetStringAsync("/api/admin/listings?type=Kiralik");

        Assert.Contains("Kiralık B", body);
        Assert.DoesNotContain("Satılık A", body);
    }

    [Fact]
    public async Task PublicList_WithoutType_ReturnsBothTypes()
    {
        await ResetAsync();
        await SeedAsync(new ListingBuilder().Titled("Satılık A"));
        await SeedAsync(new ListingBuilder().Titled("Kiralık B").OfType(ListingType.Kiralik));

        var body = await _factory.CreateClient().GetStringAsync("/api/listings");

        Assert.Contains("Satılık A", body);
        Assert.Contains("Kiralık B", body);
    }

    [Fact]
    public async Task Create_BasementFloor_IsAccepted_ButBelowMinus5Rejected()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var ok = await admin.PostAsJsonAsync("/api/admin/listings", ValidInput(d => d["floor"] = -1));
        var bad = await admin.PostAsJsonAsync("/api/admin/listings", ValidInput(d => d["floor"] = -6));

        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task AdminList_InvalidEnum_Returns400()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/listings?status=Nope")).StatusCode);
    }

    [Fact]
    public async Task Create_InvalidInput_Returns400WithErrors()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PostAsJsonAsync("/api/admin/listings",
            ValidInput(d => { d["price"] = 0; d["deposit"] = 5000; }));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        Assert.True(errors.GetArrayLength() >= 2);
        Assert.Equal(0, await WithDbAsync(db => db.Listings.CountAsync()));
    }

    [Fact]
    public async Task Create_SatilikWithDeposit_Returns400()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var bad = await admin.PostAsJsonAsync("/api/admin/listings", ValidInput(d => d["deposit"] = 5000));
        var ok = await admin.PostAsJsonAsync("/api/admin/listings",
            ValidInput(d => { d["type"] = "Kiralik"; d["deposit"] = 5000; d["aidat"] = 300; }));

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownTagId_Returns400_AndKnownTagsAreAttached()
    {
        await ResetAsync();
        var tag = new FeatureTag { Name = "Asansör", Group = TagGroup.Ic };
        await WithDbAsync(async db => { db.FeatureTags.Add(tag); await db.SaveChangesAsync(); return 0; });
        var admin = await _factory.LoginAsync("admin1");

        var bad = await admin.PostAsJsonAsync("/api/admin/listings", ValidInput(d => d["tagIds"] = new[] { tag.Id, 9999 }));
        var ok = await admin.PostAsJsonAsync("/api/admin/listings", ValidInput(d => d["tagIds"] = new[] { tag.Id }));

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Contains("Asansör", await ok.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Tags_Create_DuplicateReturns409()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var field = await _factory.LoginAsync("saha1");

        var first = await admin.PostAsJsonAsync("/api/admin/tags", new { name = "Deniz manzarası", group = "Dis" });
        var dup = await admin.PostAsJsonAsync("/api/admin/tags", new { name = "deniz manzarası ", group = "Dis" });
        var otherGroup = await admin.PostAsJsonAsync("/api/admin/tags", new { name = "Deniz manzarası", group = "Konum" });
        var empty = await admin.PostAsJsonAsync("/api/admin/tags", new { name = "  ", group = "Dis" });
        var tooLong = await admin.PostAsJsonAsync("/api/admin/tags", new { name = new string('a', 61), group = "Dis" });
        var asField = await field.PostAsJsonAsync("/api/admin/tags", new { name = "Yeni", group = "Dis" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherGroup.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asField.StatusCode);
    }

    /// <summary>Uygulamanın gerçekten kayıtlı /api/admin rotaları; sonradan eklenen rota otomatik kapsanır.</summary>
    private List<(string Method, string Path, bool AdminOnly)> AdminRoutes()
    {
        _ = _factory.Server;
        var source = _factory.Services.GetRequiredService<EndpointDataSource>();
        var routes = source.Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
            .Select(e => (
                Method: e.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.First(),
                Path: Regex.Replace(e.RoutePattern.RawText!, @"\{[^}]+\}", "1"),
                AdminOnly: e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "AdminOnly")))
            .ToList();
        Assert.True(routes.Count >= 7, $"Beklenenden az admin rotası bulundu: {routes.Count}");
        return routes;
    }

    [Fact]
    public async Task Anonymous_Returns401ForEveryAdminRoute()
    {
        await ResetAsync();
        var anon = _factory.CreateClient();
        var failures = new List<string>();

        foreach (var (method, path, _) in AdminRoutes())
        {
            var res = await anon.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = method is "GET" or "DELETE" ? null : JsonContent.Create(new { }),
            });
            if (res.StatusCode != HttpStatusCode.Unauthorized)
            {
                failures.Add($"{method} {path} -> {(int)res.StatusCode}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    [Fact]
    public async Task Field_Gets403OnEveryAdminOnlyRoute()
    {
        await ResetAsync();
        var field = await _factory.LoginAsync("saha1");
        var failures = new List<string>();
        var routes = AdminRoutes().Where(r => r.AdminOnly).ToList();
        Assert.True(routes.Count >= 5);

        foreach (var (method, path, _) in routes)
        {
            var res = await field.SendAsync(new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = method is "GET" or "DELETE" ? null : JsonContent.Create(new { }),
            });
            if (res.StatusCode != HttpStatusCode.Forbidden)
            {
                failures.Add($"{method} {path} -> {(int)res.StatusCode}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    private static void AssertNoOwnerData(string json)
    {
        foreach (var forbidden in new[] { OwnerName, OwnerPhone, "999 88 77", "9998877", OwnerNote, "owner", "ownerPhone" })
        {
            Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
        }
    }
}
