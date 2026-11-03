using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class AdvisorTests : IClassFixture<ApiFactory>
{
    // Ev sahibi değerleri; danışman numaralarından bilerek farklıdır (gizlilik denetimi değer bazlıdır).
    private const string OwnerName = "Gizli Sahip";
    private const string OwnerPhone = "0532 999 88 77";
    private const string OwnerNote = "sadece sabah ara";

    private readonly ApiFactory _factory;

    public AdvisorTests(ApiFactory factory) => _factory = factory;

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

    private async Task<Advisor> SeedAdvisorAsync(string name = "Ayşe Kaya", string phone = "+90 532 111 22 33", string? whatsapp = null, bool active = true)
    {
        var a = new Advisor { Name = name, Phone = phone, WhatsApp = whatsapp, IsActive = active, CreatedAt = DateTimeOffset.UtcNow, CreatedBy = "admin1" };
        await WithDbAsync(async db => { db.Advisors.Add(a); await db.SaveChangesAsync(); return 0; });
        return a;
    }

    /// <summary>İlanı danışmanıyla ekler; danışman yalnızca kimlikle bağlanır (izlenmeyen varlık yeniden eklenmesin).</summary>
    private async Task<Listing> SeedListingAsync(ListingBuilder b, Advisor? advisor = null)
    {
        var l = b.Build();
        await WithDbAsync(async db =>
        {
            l.AdvisorId = advisor?.Id;
            db.Listings.Add(l);
            await db.SaveChangesAsync();
            return 0;
        });
        return l;
    }

    private static Dictionary<string, object?> ListingInput(int? advisorId)
        => new()
        {
            ["title"] = "Yeni ilan", ["type"] = "Satilik", ["price"] = 2_500_000, ["currency"] = "TRY",
            ["city"] = "Istanbul", ["district"] = "Kadikoy", ["neighborhood"] = "Moda", ["rooms"] = "3+1",
            ["bathrooms"] = 1, ["grossM2"] = 120, ["netM2"] = 100, ["floor"] = 2, ["totalFloors"] = 5, ["buildingAge"] = 5,
            ["heating"] = "Kombi", ["furnishing"] = "Esyasiz", ["usage"] = "Bos", ["deed"] = "KatMulkiyeti", ["kitchen"] = "Kapali",
            ["description"] = "Açıklama", ["ownerName"] = OwnerName, ["ownerPhone"] = OwnerPhone, ["ownerNote"] = OwnerNote,
            ["tagIds"] = Array.Empty<int>(), ["advisorId"] = advisorId,
        };

    // ---- Danışman uç noktaları ----

    [Fact]
    public async Task Create_Returns201_TrimsAndStoresAudit_EmptyWhatsappBecomesNull()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PostAsJsonAsync("/api/admin/advisors", new { name = "  Ayşe Kaya ", phone = " 0 532 444 55 66 ", whatsApp = "  " });
        var dto = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.EndsWith($"/api/admin/advisors/{dto.GetProperty("id").GetInt32()}", res.Headers.Location!.ToString());
        Assert.Equal("Ayşe Kaya", dto.GetProperty("name").GetString());
        Assert.Equal("0 532 444 55 66", dto.GetProperty("phone").GetString());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("whatsApp").ValueKind);
        Assert.True(dto.GetProperty("isActive").GetBoolean());
        Assert.Equal(0, dto.GetProperty("listingCount").GetInt32());
        Assert.Equal("admin1", dto.GetProperty("createdBy").GetString());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("updatedAt").ValueKind);
    }

    [Theory]
    [InlineData("", "0532 111 22 33", null)]
    [InlineData("  ", "0532 111 22 33", null)]
    [InlineData("Ad", "", null)]
    [InlineData("Ad", "123456", null)]
    [InlineData("Ad", "1234567890123456", null)]
    [InlineData("Ad", "0532 abc 22 33", null)]
    [InlineData("Ad", "0532 111 22 33", "abc")]
    [InlineData("Ad", "0532 111 22 33", "javascript:alert(1)")]
    [InlineData("Ad", "0532 111 22 33", "12345")]
    public async Task Create_InvalidInput_Returns400WithErrors(string name, string phone, string? whatsapp)
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PostAsJsonAsync("/api/admin/advisors", new { name, phone, whatsApp = whatsapp });
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.True(body.GetProperty("errors").GetArrayLength() > 0);
        Assert.Empty(await WithDbAsync(db => db.Advisors.ToListAsync()));
    }

    [Fact]
    public async Task Create_TooLongValues_Return400()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var longName = await admin.PostAsJsonAsync("/api/admin/advisors", new { name = new string('a', 101), phone = "0532 111 22 33" });
        var longPhone = await admin.PostAsJsonAsync("/api/admin/advisors", new { name = "Ad", phone = "0532 111 22 33" + new string(' ', 30) + "1" });
        var longWa = await admin.PostAsJsonAsync("/api/admin/advisors", new { name = "Ad", phone = "0532 111 22 33", whatsApp = "https://wa.me/" + new string('1', 200) });
        var okWa = await admin.PostAsJsonAsync("/api/admin/advisors", new { name = "Ad", phone = "0532 111 22 33", whatsApp = "https://wa.me/905321112233" });

        Assert.Equal(HttpStatusCode.BadRequest, longName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longPhone.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longWa.StatusCode);
        Assert.Equal(HttpStatusCode.Created, okWa.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsActiveAndInactive_OrderedByName_WithNonDeletedListingCount()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var b = await SeedAdvisorAsync("Bora");
        var a = await SeedAdvisorAsync("Ali", "0532 222 33 44", active: false);
        await SeedListingAsync(new ListingBuilder(), a);
        await SeedListingAsync(new ListingBuilder(), a);
        await SeedListingAsync(new ListingBuilder().Deleted(), a);

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/admin/advisors");

        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal("Ali", list[0].GetProperty("name").GetString());
        Assert.False(list[0].GetProperty("isActive").GetBoolean());
        Assert.Equal(2, list[0].GetProperty("listingCount").GetInt32()); // silinen sayılmaz
        Assert.Equal("Bora", list[1].GetProperty("name").GetString());
        Assert.Equal(0, list[1].GetProperty("listingCount").GetInt32());
        Assert.Equal(b.Id, list[1].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GetById_Put_Deactivate_Activate_WorkAndRecordAudit()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin2");
        var a = await SeedAdvisorAsync();

        var one = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/advisors/{a.Id}");
        var put = await admin.PutAsJsonAsync($"/api/admin/advisors/{a.Id}", new { name = "Yeni Ad", phone = "+90 532 555 66 77", whatsApp = "905325556677" });
        var putBody = await put.Content.ReadFromJsonAsync<JsonElement>();
        var off = await admin.PostAsync($"/api/admin/advisors/{a.Id}/deactivate", null);
        var offDto = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/advisors/{a.Id}");
        var on = await admin.PostAsync($"/api/admin/advisors/{a.Id}/activate", null);
        var onAgain = await admin.PostAsync($"/api/admin/advisors/{a.Id}/activate", null);
        var onDto = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/advisors/{a.Id}");

        Assert.Equal("Ayşe Kaya", one.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("Yeni Ad", putBody.GetProperty("name").GetString());
        Assert.Equal("905325556677", putBody.GetProperty("whatsApp").GetString());
        Assert.Equal("admin2", putBody.GetProperty("updatedBy").GetString());
        Assert.Equal("admin1", putBody.GetProperty("createdBy").GetString());
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.False(offDto.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, on.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, onAgain.StatusCode);
        Assert.True(onDto.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task UnknownId_Returns404_OnEveryIdRoute()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var input = new { name = "Ad", phone = "0532 111 22 33" };

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/advisors/9999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync("/api/admin/advisors/9999", input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/advisors/9999/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/advisors/9999/activate", null)).StatusCode);
    }

    [Fact]
    public async Task Put_InvalidInput_Returns400_AndNoHardDeleteRouteExists()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var a = await SeedAdvisorAsync();

        var bad = await admin.PutAsJsonAsync($"/api/admin/advisors/{a.Id}", new { name = "", phone = "1" });
        var del = await admin.DeleteAsync($"/api/admin/advisors/{a.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, del.StatusCode);
        Assert.Equal(1, await WithDbAsync(db => db.Advisors.CountAsync()));
    }

    [Fact]
    public async Task Field_And_Anonymous_AreRejected_OnAdvisorRoutes()
    {
        await ResetAsync();
        var field = await _factory.LoginAsync("saha1");

        Assert.Equal(HttpStatusCode.Forbidden, (await field.GetAsync("/api/admin/advisors")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await field.PostAsJsonAsync("/api/admin/advisors", new { name = "Ad", phone = "0532 111 22 33" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().GetAsync("/api/admin/advisors")).StatusCode);
    }

    // ---- İlana atama ----

    [Fact]
    public async Task Listing_AssignActiveAdvisor_ThenClearWithNull()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var a = await SeedAdvisorAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/listings", ListingInput(a.Id));
        var dto = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = dto.GetProperty("id").GetInt32();
        var cleared = await admin.PutAsJsonAsync($"/api/admin/listings/{id}", ListingInput(null));
        var clearedDto = await cleared.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(a.Id, dto.GetProperty("advisorId").GetInt32());
        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Equal(JsonValueKind.Null, clearedDto.GetProperty("advisorId").ValueKind);
    }

    [Fact]
    public async Task Listing_AssignUnknownOrInactiveAdvisor_Returns400()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var off = await SeedAdvisorAsync("Pasif", active: false);

        var unknown = await admin.PostAsJsonAsync("/api/admin/listings", ListingInput(9999));
        var inactive = await admin.PostAsJsonAsync("/api/admin/listings", ListingInput(off.Id));
        var body = await inactive.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Contains("pasif", body.GetProperty("errors")[0].GetString());
        Assert.Equal(0, await WithDbAsync(db => db.Listings.CountAsync()));
    }

    [Fact]
    public async Task DeactivatedAdvisor_StillShowsOnItsListing_CanKeepIt_ButCannotBeNewlyAssigned()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var a = await SeedAdvisorAsync("Eski Danışman", "+90 532 111 22 33");
        var other = await SeedAdvisorAsync("Başka", "+90 532 777 88 99");
        var l = await SeedListingAsync(new ListingBuilder().Titled("bağlı ilan"), a);
        var free = await SeedListingAsync(new ListingBuilder().Titled("serbest ilan"));
        await admin.PostAsync($"/api/admin/advisors/{a.Id}/deactivate", null);

        var detail = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/listings/{l.Id}");
        var keep = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}", ListingInput(a.Id)); // aynı (pasif) danışman: serbest
        var assignNew = await admin.PutAsJsonAsync($"/api/admin/listings/{free.Id}", ListingInput(a.Id));
        var swapAway = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}", ListingInput(other.Id));
        var back = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}", ListingInput(a.Id)); // artık bağlı değil: reddedilir

        Assert.Equal("Eski Danışman", detail.GetProperty("advisor").GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, assignNew.StatusCode);
        Assert.Equal(HttpStatusCode.OK, swapAway.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, back.StatusCode);
    }

    // ---- Ziyaretçi cevabı ----

    [Fact]
    public async Task Visitor_Json_ContainsAdvisorBlock_ButNoOwnerValues_AndNoAdvisorInternals()
    {
        await ResetAsync();
        var a = await SeedAdvisorAsync("Ayşe Kaya", "0 532 444 55 66", "https://wa.me/905320001234");
        var l = await SeedListingAsync(new ListingBuilder().WithOwner(OwnerName, OwnerPhone, OwnerNote), a);
        var client = _factory.CreateClient();

        var detailJson = await client.GetStringAsync($"/api/listings/{l.Id}");
        var listJson = await client.GetStringAsync("/api/listings");

        foreach (var json in new[] { detailJson, listJson })
        {
            using var doc = JsonDocument.Parse(json);
            var node = doc.RootElement.TryGetProperty("items", out var items) ? items[0] : doc.RootElement;
            var advisor = node.GetProperty("advisor");
            Assert.Equal("Ayşe Kaya", advisor.GetProperty("name").GetString());
            Assert.Equal("0 532 444 55 66", advisor.GetProperty("phone").GetString());
            Assert.Equal("https://wa.me/905320001234", advisor.GetProperty("whatsappUrl").GetString());
            Assert.Equal(["name", "phone", "whatsappUrl"], advisor.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
            foreach (var forbidden in new[] { OwnerName, OwnerPhone, "999 88 77", OwnerNote, "ownerPhone", "owner", "createdBy", "isActive" })
            {
                Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task Listing_WithoutAdvisor_ShowsConfiguredDefault_AndNullWhenNoneConfigured()
    {
        await ResetAsync();
        var l = await SeedListingAsync(new ListingBuilder());
        var withDefault = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Advisor:Name", "Varsayılan Kişi");
            b.UseSetting("Advisor:Phone", "+90 555 123 45 67");
            b.UseSetting("Advisor:WhatsApp", "");
        }).CreateClient();
        var withoutDefault = _factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Advisor:Name", "");
            b.UseSetting("Advisor:Phone", "");
            b.UseSetting("Advisor:WhatsApp", "");
        }).CreateClient();

        var detail = await withDefault.GetFromJsonAsync<JsonElement>($"/api/listings/{l.Id}");
        var list = await withDefault.GetFromJsonAsync<JsonElement>("/api/listings");
        var none = await withoutDefault.GetFromJsonAsync<JsonElement>($"/api/listings/{l.Id}");

        Assert.Equal("Varsayılan Kişi", detail.GetProperty("advisor").GetProperty("name").GetString());
        Assert.Equal("https://wa.me/905551234567", detail.GetProperty("advisor").GetProperty("whatsappUrl").GetString());
        Assert.Equal("Varsayılan Kişi", list.GetProperty("items")[0].GetProperty("advisor").GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, none.GetProperty("advisor").ValueKind);
    }

    [Fact]
    public async Task Advisor_WithLocalPhone_ProducesInternationalWhatsappUrl_InListingJson()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var created = await admin.PostAsJsonAsync("/api/admin/advisors", new { name = "Yerel", phone = "0 532 444 55 66" });
        var advisorId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var l = await SeedListingAsync(new ListingBuilder());
        var put = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}", ListingInput(advisorId));

        var detail = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/listings/{l.Id}");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("https://wa.me/905324445566", detail.GetProperty("advisor").GetProperty("whatsappUrl").GetString());
        Assert.Equal("0 532 444 55 66", detail.GetProperty("advisor").GetProperty("phone").GetString()); // görünen telefon yazıldığı gibi
    }

    [Fact]
    public async Task Field_Gets_PublicShape_WithAdvisor_AndNoOwnerValues()
    {
        await ResetAsync();
        var field = await _factory.LoginAsync("saha1");
        var a = await SeedAdvisorAsync("Ayşe Kaya", "+90 532 111 22 33");
        var l = await SeedListingAsync(new ListingBuilder().WithOwner(OwnerName, OwnerPhone, OwnerNote), a);

        var detail = await field.GetStringAsync($"/api/admin/listings/{l.Id}");
        var list = await field.GetStringAsync("/api/admin/listings");

        foreach (var json in new[] { detail, list })
        {
            Assert.Contains("Ayşe Kaya", json);
            foreach (var forbidden in new[] { OwnerName, OwnerPhone, "999 88 77", OwnerNote, "ownerPhone", "owner", "createdBy" })
            {
                Assert.DoesNotContain(forbidden, json, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    // ---- Filtre ----

    [Fact]
    public async Task AdminList_FiltersByAdvisor_AndShowsAdvisorName_VisitorIgnoresAdvisorId()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");
        var a = await SeedAdvisorAsync("Ayşe Kaya");
        var b = await SeedAdvisorAsync("Mehmet Demir", "+90 532 222 33 44");
        await SeedListingAsync(new ListingBuilder().Titled("ayşe-ilanı"), a);
        await SeedListingAsync(new ListingBuilder().Titled("mehmet-ilanı"), b);
        await SeedListingAsync(new ListingBuilder().Titled("danışmansız"));

        var byA = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/listings?advisorId={a.Id}");
        var all = await admin.GetFromJsonAsync<JsonElement>("/api/admin/listings");
        var visitor = await _factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/listings?advisorId={a.Id}");

        Assert.Equal(1, byA.GetProperty("total").GetInt32());
        var row = byA.GetProperty("items")[0];
        Assert.Equal("ayşe-ilanı", row.GetProperty("title").GetString());
        Assert.Equal(a.Id, row.GetProperty("advisorId").GetInt32());
        Assert.Equal("Ayşe Kaya", row.GetProperty("advisorName").GetString());
        Assert.Equal(3, all.GetProperty("total").GetInt32());
        Assert.Contains(all.GetProperty("items").EnumerateArray(), i => i.GetProperty("advisorName").ValueKind == JsonValueKind.Null);
        Assert.Equal(3, visitor.GetProperty("total").GetInt32()); // ziyaretçi advisorId'yi yok sayar
    }
}
