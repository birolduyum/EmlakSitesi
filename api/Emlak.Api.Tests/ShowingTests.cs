using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class ShowingTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ShowingTests(ApiFactory factory) => _factory = factory;

    private sealed record ShowingBody(int Id, int ListingId, string ListingTitle, string Username, DateTimeOffset TakenAt, DateTimeOffset? ReturnedAt, string? Note);

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

    private async Task<ShowingBody> TakeAsync(HttpClient client, int listingId)
    {
        var res = await client.PostAsJsonAsync("/api/showings", new { listingId });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ShowingBody>())!;
    }

    private static async Task<ShowingBody[]> ReadListAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<ShowingBody[]>())!;
    }

    [Fact]
    public async Task Take_AsField_CreatesRecordWithUsernameAndTime()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder().Titled("anahtar-ilan"));
        var field = await _factory.LoginAsync("saha1");
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);

        var dto = await TakeAsync(field, listing.Id);

        Assert.Equal("saha1", dto.Username);
        Assert.Equal(listing.Id, dto.ListingId);
        Assert.Equal("anahtar-ilan", dto.ListingTitle);
        Assert.Null(dto.ReturnedAt);
        Assert.InRange(dto.TakenAt, before, DateTimeOffset.UtcNow.AddMinutes(1));
        var stored = await WithDbAsync(db => db.Showings.SingleAsync());
        Assert.Equal("saha1", stored.Username);
    }

    [Fact]
    public async Task Take_AsAdmin_Works()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");

        var dto = await TakeAsync(admin, listing.Id);

        Assert.Equal("admin1", dto.Username);
    }

    [Fact]
    public async Task Take_SameListingTwiceWhileOpen_Returns409()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");
        await TakeAsync(field, listing.Id);

        var second = await field.PostAsJsonAsync("/api/showings", new { listingId = listing.Id });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, await WithDbAsync(db => db.Showings.CountAsync()));
    }

    [Fact]
    public async Task Take_SameListingByDifferentUsers_BothAllowed()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        await TakeAsync(await _factory.LoginAsync("saha1"), listing.Id);
        await TakeAsync(await _factory.LoginAsync("admin1"), listing.Id);

        Assert.Equal(2, await WithDbAsync(db => db.Showings.CountAsync()));
    }

    [Fact]
    public async Task Take_AgainAfterClose_IsAllowed()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");
        var first = await TakeAsync(field, listing.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await field.PostAsJsonAsync($"/api/showings/{first.Id}/close", new { })).StatusCode);

        await TakeAsync(field, listing.Id);
    }

    [Fact]
    public async Task Take_DeletedListing_Returns404()
    {
        await ResetAsync();
        var deleted = await SeedAsync(new ListingBuilder().Deleted());
        var field = await _factory.LoginAsync("saha1");

        Assert.Equal(HttpStatusCode.NotFound, (await field.PostAsJsonAsync("/api/showings", new { listingId = deleted.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await field.PostAsJsonAsync("/api/showings", new { listingId = 999_999 })).StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Showings.CountAsync()));
    }

    [Fact]
    public async Task Close_SetsReturnedAtAndNote()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");
        var dto = await TakeAsync(field, listing.Id);

        var res = await field.PostAsJsonAsync($"/api/showings/{dto.Id}/close", new { note = "  Anahtar teslim edildi  " });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var stored = await WithDbAsync(db => db.Showings.SingleAsync());
        Assert.NotNull(stored.ReturnedAt);
        Assert.Equal("Anahtar teslim edildi", stored.Note);
    }

    [Fact]
    public async Task Close_EmptyNote_StoresNull()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");
        var dto = await TakeAsync(field, listing.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await field.PostAsJsonAsync($"/api/showings/{dto.Id}/close", new { note = "   " })).StatusCode);

        Assert.Null((await WithDbAsync(db => db.Showings.SingleAsync())).Note);
    }

    [Fact]
    public async Task Close_NoteTooLong_Returns400AndStaysOpen()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");
        var dto = await TakeAsync(field, listing.Id);

        var res = await field.PostAsJsonAsync($"/api/showings/{dto.Id}/close", new { note = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Null((await WithDbAsync(db => db.Showings.SingleAsync())).ReturnedAt);
    }

    [Fact]
    public async Task Close_ByOtherField_Returns403()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var dto = await TakeAsync(await _factory.LoginAsync("admin1"), listing.Id);
        var field = await _factory.LoginAsync("saha1");

        var res = await field.PostAsJsonAsync($"/api/showings/{dto.Id}/close", new { });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Null((await WithDbAsync(db => db.Showings.SingleAsync())).ReturnedAt);
    }

    [Fact]
    public async Task Close_ByAdminOnFieldRecord_Works()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var dto = await TakeAsync(await _factory.LoginAsync("saha1"), listing.Id);
        var admin = await _factory.LoginAsync("admin2");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/showings/{dto.Id}/close", new { })).StatusCode);
    }

    [Fact]
    public async Task Close_AlreadyClosed_Returns409()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");
        var dto = await TakeAsync(field, listing.Id);
        await field.PostAsJsonAsync($"/api/showings/{dto.Id}/close", new { note = "ilk" });

        var res = await field.PostAsJsonAsync($"/api/showings/{dto.Id}/close", new { note = "ikinci" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("ilk", (await WithDbAsync(db => db.Showings.SingleAsync())).Note);
    }

    [Fact]
    public async Task Close_MissingId_Returns404()
    {
        await ResetAsync();
        var field = await _factory.LoginAsync("saha1");

        Assert.Equal(HttpStatusCode.NotFound, (await field.PostAsJsonAsync("/api/showings/999999/close", new { })).StatusCode);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyOwnOpenRecords()
    {
        await ResetAsync();
        var a = await SeedAsync(new ListingBuilder().Titled("a"));
        var b = await SeedAsync(new ListingBuilder().Titled("b"));
        var c = await SeedAsync(new ListingBuilder().Titled("c"));
        var field = await _factory.LoginAsync("saha1");
        var admin = await _factory.LoginAsync("admin1");
        var closed = await TakeAsync(field, a.Id);
        await field.PostAsJsonAsync($"/api/showings/{closed.Id}/close", new { });
        await TakeAsync(field, b.Id);
        await Task.Delay(20);
        await TakeAsync(field, c.Id);
        await TakeAsync(admin, a.Id);

        var mine = await ReadListAsync(await field.GetAsync("/api/showings/mine"));

        Assert.Equal(["c", "b"], mine.Select(s => s.ListingTitle).ToArray());
        Assert.All(mine, s => Assert.Equal("saha1", s.Username));
    }

    [Fact]
    public async Task Showings_Anonymous_Returns401()
    {
        await ResetAsync();
        var anon = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/showings", new { listingId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/showings/1/close", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/showings/mine")).StatusCode);
    }

    [Fact]
    public async Task Report_FiltersByListingUserAndDateRange()
    {
        await ResetAsync();
        var a = await SeedAsync(new ListingBuilder().Titled("a"));
        var b = await SeedAsync(new ListingBuilder().Titled("b"));
        await WithDbAsync(async db =>
        {
            db.Showings.AddRange(
                new Showing { ListingId = a.Id, Username = "saha1", TakenAt = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero) },
                new Showing { ListingId = a.Id, Username = "admin1", TakenAt = new DateTimeOffset(2026, 11, 2, 23, 30, 0, TimeSpan.Zero) },
                new Showing { ListingId = b.Id, Username = "saha1", TakenAt = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero) },
                new Showing { ListingId = b.Id, Username = "saha1", TakenAt = new DateTimeOffset(2026, 11, 4, 8, 0, 0, TimeSpan.Zero) });
            await db.SaveChangesAsync();
            return 0;
        });
        var admin = await _factory.LoginAsync("admin1");

        var all = await ReadListAsync(await admin.GetAsync("/api/admin/showings"));
        Assert.Equal(4, all.Length);
        Assert.Equal(all.OrderByDescending(s => s.TakenAt).Select(s => s.Id), all.Select(s => s.Id));

        var byListing = await ReadListAsync(await admin.GetAsync($"/api/admin/showings?listingId={a.Id}"));
        Assert.Equal(2, byListing.Length);

        var byUser = await ReadListAsync(await admin.GetAsync("/api/admin/showings?username=saha1"));
        Assert.Equal(3, byUser.Length);

        // Yalnızca tarih verilen sınırlar: from günün başı, to günün sonu (UTC), ikisi de dahil.
        var day = await ReadListAsync(await admin.GetAsync("/api/admin/showings?from=2026-11-02&to=2026-11-02"));
        Assert.Single(day);
        Assert.Equal("admin1", day[0].Username);

        var range = await ReadListAsync(await admin.GetAsync("/api/admin/showings?from=2026-11-02&to=2026-11-03"));
        Assert.Equal(2, range.Length);

        var combined = await ReadListAsync(await admin.GetAsync($"/api/admin/showings?listingId={b.Id}&username=saha1&from=2026-11-04T00:00:00Z"));
        Assert.Single(combined);

        // Tam zamanlı sınır tam eşleşmede dahil.
        var exact = await ReadListAsync(await admin.GetAsync("/api/admin/showings?from=2026-11-03T00:00:00Z&to=2026-11-03T00:00:00Z"));
        Assert.Single(exact);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/showings?from=dun")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/showings?to=2026-13-45")).StatusCode);
    }

    [Fact]
    public async Task Report_AcceptsOffsetBounds()
    {
        await ResetAsync();
        var a = await SeedAsync(new ListingBuilder().Titled("a"));
        await WithDbAsync(async db =>
        {
            db.Showings.AddRange(
                new Showing { ListingId = a.Id, Username = "saha1", TakenAt = new DateTimeOffset(2026, 11, 2, 20, 59, 0, TimeSpan.Zero) },
                new Showing { ListingId = a.Id, Username = "saha1", TakenAt = new DateTimeOffset(2026, 11, 2, 21, 0, 0, TimeSpan.Zero) },
                new Showing { ListingId = a.Id, Username = "saha1", TakenAt = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero) },
                new Showing { ListingId = a.Id, Username = "saha1", TakenAt = new DateTimeOffset(2026, 11, 3, 0, 1, 0, TimeSpan.Zero) });
            await db.SaveChangesAsync();
            return 0;
        });
        var admin = await _factory.LoginAsync("admin1");

        // +03:00: 2026-11-03T00:00+03:00 = 2026-11-02T21:00Z (dahil); 2026-11-03T03:00+03:00 = 2026-11-03T00:00Z (tam eşleşme dahil).
        var plus3 = await ReadListAsync(await admin.GetAsync("/api/admin/showings?from=2026-11-03T00:00:00%2B03:00&to=2026-11-03T03:00:00%2B03:00"));
        var sameInstantUtc = await ReadListAsync(await admin.GetAsync("/api/admin/showings?from=2026-11-02T21:00:00Z&to=2026-11-03T00:00:00Z"));
        var minus = await ReadListAsync(await admin.GetAsync("/api/admin/showings?from=2026-11-02T16:00:00-05:00"));

        Assert.Equal(2, plus3.Length);
        Assert.Equal(plus3.Select(s => s.Id).Order(), sameInstantUtc.Select(s => s.Id).Order());
        Assert.Equal(3, minus.Length); // 16:00-05:00 = 21:00Z
        Assert.Equal(
            [new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 2, 21, 0, 0, TimeSpan.Zero)],
            plus3.Select(s => s.TakenAt.ToUniversalTime()).ToArray()); // en yeniden eskiye; 20:59 ve 00:01 dışarıda
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/showings?from=0001-01-01T00:00:00%2B03:00")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/showings?to=9999-12-31T23:59:59%2B03:00")).StatusCode);
    }

    [Theory]
    [InlineData("to=9999-12-31")]
    [InlineData("to=9999-12-31T23:59:59.9999999Z")]
    [InlineData("from=0001-01-01")]
    [InlineData("from=0001-01-01T00:00:00Z")]
    [InlineData("to=1800-01-01")]
    public async Task Report_ExtremeBounds_Return400NotServerError(string query)
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.GetAsync($"/api/admin/showings?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Report_UsernameFilter_IsCaseInsensitive()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");
        await TakeAsync(field, listing.Id);
        var admin = await _factory.LoginAsync("admin1");

        var rows = await ReadListAsync(await admin.GetAsync("/api/admin/showings?username=SAHA1"));

        Assert.Single(rows);
    }

    [Fact]
    public async Task Report_IncludesDeletedListingsHistory()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder().Titled("silinecek"));
        var field = await _factory.LoginAsync("saha1");
        await TakeAsync(field, listing.Id);
        var admin = await _factory.LoginAsync("admin1");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/listings/{listing.Id}")).StatusCode);

        var rows = await ReadListAsync(await admin.GetAsync("/api/admin/showings"));

        Assert.Single(rows);
        Assert.Equal("silinecek", rows[0].ListingTitle);
    }

    [Fact]
    public async Task Report_AsField_Returns403()
    {
        await ResetAsync();
        var field = await _factory.LoginAsync("saha1");

        Assert.Equal(HttpStatusCode.Forbidden, (await field.GetAsync("/api/admin/showings")).StatusCode);
    }

    [Fact]
    public async Task PublicDetail_ShowingCount_IncreasesAfterTake()
    {
        await ResetAsync();
        var listing = await SeedAsync(new ListingBuilder());
        var anon = _factory.CreateClient();

        async Task<int> CountAsync()
        {
            using var doc = JsonDocument.Parse(await anon.GetStringAsync($"/api/listings/{listing.Id}"));
            return doc.RootElement.GetProperty("showingCount").GetInt32();
        }

        Assert.Equal(0, await CountAsync());
        await TakeAsync(await _factory.LoginAsync("saha1"), listing.Id);
        Assert.Equal(1, await CountAsync());
    }
}
