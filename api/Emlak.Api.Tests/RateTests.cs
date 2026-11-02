using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class RateTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RateTests(ApiFactory factory) => _factory = factory;

    private sealed record RateBody(string Currency, decimal TryPerUnit, DateTime UpdatedAt, string UpdatedBy);

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

    private static async Task<RateBody[]> GetRatesAsync(HttpClient client)
        => (await client.GetFromJsonAsync<RateBody[]>("/api/admin/rates"))!;

    [Fact]
    public async Task GetRates_AsAdmin_ReturnsUsdAndEur()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var rates = await GetRatesAsync(admin);

        Assert.Equal(["EUR", "USD"], rates.Select(r => r.Currency).Order().ToArray());
        Assert.Equal(35m, rates.Single(r => r.Currency == "USD").TryPerUnit);
    }

    [Fact]
    public async Task PutRates_AsAdmin_UpdatesAndRecordsUser()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin2");
        var before = DateTime.UtcNow.AddMinutes(-1);

        var res = await admin.PutAsJsonAsync("/api/admin/rates", new { usd = 41.25m, eur = 44.5m });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        var rates = await GetRatesAsync(admin);
        var usd = rates.Single(r => r.Currency == "USD");
        var eur = rates.Single(r => r.Currency == "EUR");
        Assert.Equal(41.25m, usd.TryPerUnit);
        Assert.Equal(44.5m, eur.TryPerUnit);
        Assert.Equal("admin2", usd.UpdatedBy);
        Assert.Equal("admin2", eur.UpdatedBy);
        Assert.True(usd.UpdatedAt >= before);
    }

    [Theory]
    [InlineData(0, 38)]
    [InlineData(35, 0)]
    [InlineData(-1, 38)]
    [InlineData(35, -5)]
    [InlineData(1_000_001, 38)]
    [InlineData(35, 1_000_001)]
    public async Task PutRates_ZeroNegativeOrHuge_Returns400AndKeepsOld(decimal usd, decimal eur)
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PutAsJsonAsync("/api/admin/rates", new { usd, eur });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").GetArrayLength() > 0);
        var rates = await GetRatesAsync(admin);
        Assert.Equal(35m, rates.Single(r => r.Currency == "USD").TryPerUnit);
        Assert.Equal(38m, rates.Single(r => r.Currency == "EUR").TryPerUnit);
        Assert.All(rates, r => Assert.Equal("seed", r.UpdatedBy));
    }

    [Fact]
    public async Task PutRates_MissingField_Returns400()
    {
        await ResetAsync();
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PutAsJsonAsync("/api/admin/rates", new { usd = 40m });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task PutRates_AsField_Returns403()
    {
        await ResetAsync();
        var field = await _factory.LoginAsync("saha1");

        Assert.Equal(HttpStatusCode.Forbidden, (await field.PutAsJsonAsync("/api/admin/rates", new { usd = 40m, eur = 41m })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await field.GetAsync("/api/admin/rates")).StatusCode);
        var db = await WithDbAsync(d => d.CurrencyRates.AsNoTracking().SingleAsync(r => r.Currency == Currency.USD));
        Assert.Equal(35m, db.TryPerUnit);
    }

    [Fact]
    public async Task NewRate_ChangesPriceFilterResult()
    {
        await ResetAsync();
        await WithDbAsync(async db =>
        {
            db.Listings.Add(new ListingBuilder().Titled("usd-100k").Priced(100_000m, Currency.USD).Build()); // 35 kurunda 3.500.000 ₺
            await db.SaveChangesAsync();
            return 0;
        });
        var anon = _factory.CreateClient();
        var admin = await _factory.LoginAsync("admin1");
        const string url = "/api/listings?minPriceTry=3900000&maxPriceTry=4100000";

        async Task<string[]> TitlesAsync(HttpClient c, string path)
        {
            using var doc = JsonDocument.Parse(await c.GetStringAsync(path));
            return doc.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!).ToArray();
        }

        Assert.Empty(await TitlesAsync(anon, url));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/admin/rates", new { usd = 40m, eur = 38m })).StatusCode);

        Assert.Equal(["usd-100k"], await TitlesAsync(anon, url));
        Assert.Equal(["usd-100k"], await TitlesAsync(admin, "/api/admin/listings?minPriceTry=3900000&maxPriceTry=4100000"));
    }
}
