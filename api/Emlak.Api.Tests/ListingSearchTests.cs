using Emlak.Api.Data;
using Emlak.Api.Domain;
using Emlak.Api.Listings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Emlak.Api.Tests;

public class ListingSearchTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public ListingSearchTests(ApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private async Task<AsyncServiceScope> FreshScopeAsync()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        return _factory.Services.CreateAsyncScope();
    }

    private static async Task SeedAsync(AppDbContext db, params Listing[] listings)
    {
        db.Listings.AddRange(listings);
        await db.SaveChangesAsync();
    }

    private static async Task SetRateAsync(AppDbContext db, Currency currency, decimal rate)
    {
        var row = await db.CurrencyRates.SingleAsync(r => r.Currency == currency);
        row.TryPerUnit = rate;
        await db.SaveChangesAsync();
    }

    private static ListingSearchService Svc(AsyncServiceScope scope)
        => new(scope.ServiceProvider.GetRequiredService<AppDbContext>());

    private static string[] Titles(IEnumerable<Listing> items) => items.Select(i => i.Title).OrderBy(t => t, StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task PriceFilter_ConvertsUsdAndEurWithConfiguredRate()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetRateAsync(db, Currency.USD, 40m);
        await SetRateAsync(db, Currency.EUR, 41m);
        await SeedAsync(db,
            new ListingBuilder().Titled("usd-4m").Priced(100_000m, Currency.USD).Build(),     // 4.000.000 ₺
            new ListingBuilder().Titled("try-4m").Priced(4_000_000m).Build(),
            new ListingBuilder().Titled("eur-4.1m").Priced(100_000m, Currency.EUR).Build(),   // 4.100.000 ₺ (sınır dahil)
            new ListingBuilder().Titled("try-1m").Priced(1_000_000m).Build(),
            new ListingBuilder().Titled("usd-too-cheap").Priced(90_000m, Currency.USD).Build(), // 3.600.000 ₺
            new ListingBuilder().Titled("usd-too-dear").Priced(110_000m, Currency.USD).Build()); // 4.400.000 ₺

        var (items, total) = await Svc(scope).SearchAsync(
            new ListingQuery { MinPriceTry = 3_900_000m, MaxPriceTry = 4_100_000m }, false, default);

        Assert.Equal(["eur-4.1m", "try-4m", "usd-4m"], Titles(items));
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task PriceFilter_DoesNotMixSaleAndRent()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db,
            new ListingBuilder().Titled("satilik").OfType(ListingType.Satilik).Priced(50_000m).Build(),
            new ListingBuilder().Titled("kiralik").OfType(ListingType.Kiralik).Priced(50_000m).Build());

        var query = new ListingQuery { Type = ListingType.Satilik, MinPriceTry = 40_000m, MaxPriceTry = 60_000m };
        var sale = await Svc(scope).SearchAsync(query, false, default);
        var rent = await Svc(scope).SearchAsync(query with { Type = ListingType.Kiralik }, false, default);

        Assert.Equal(["satilik"], Titles(sale.Items));
        Assert.Equal(["kiralik"], Titles(rent.Items));
    }

    [Fact]
    public async Task LocationFilter_ReturnsAllCurrencies()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db,
            new ListingBuilder().Titled("try").In("Istanbul", "Besiktas").Priced(5_000_000m).Build(),
            new ListingBuilder().Titled("usd").In("Istanbul", "Besiktas").Priced(100_000m, Currency.USD).Build(),
            new ListingBuilder().Titled("eur").In("Istanbul", "Besiktas").Priced(90_000m, Currency.EUR).Build(),
            new ListingBuilder().Titled("baska-ilce").In("Istanbul", "Kadikoy").Build(),
            new ListingBuilder().Titled("baska-il").In("Ankara", "Besiktas").Build());

        var (items, total) = await Svc(scope).SearchAsync(
            new ListingQuery { City = "Istanbul", District = "Besiktas" }, false, default);

        Assert.Equal(["eur", "try", "usd"], Titles(items));
        Assert.Equal(3, total);
    }

    [Fact]
    public async Task CurrencyFilter_ReturnsOnlyThatCurrency()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db,
            new ListingBuilder().Titled("try").Priced(5_000_000m).Build(),
            new ListingBuilder().Titled("usd").Priced(100_000m, Currency.USD).Build(),
            new ListingBuilder().Titled("eur").Priced(90_000m, Currency.EUR).Build());

        var (items, _) = await Svc(scope).SearchAsync(new ListingQuery { Currency = Currency.USD }, false, default);

        Assert.Equal(["usd"], Titles(items));
    }

    [Fact]
    public async Task Sort_PriceAsc_UsesTryConvertedPrice()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetRateAsync(db, Currency.USD, 40m);
        await SetRateAsync(db, Currency.EUR, 50m);
        await SeedAsync(db,
            new ListingBuilder().Titled("usd-100k").Priced(100_000m, Currency.USD).Build(),   // 4.000.000
            new ListingBuilder().Titled("try-3m").Priced(3_000_000m).Build(),
            new ListingBuilder().Titled("eur-100k").Priced(100_000m, Currency.EUR).Build(),   // 5.000.000
            new ListingBuilder().Titled("try-4.5m").Priced(4_500_000m).Build());

        var asc = await Svc(scope).SearchAsync(new ListingQuery { Sort = ListingSort.PriceAsc }, false, default);
        var desc = await Svc(scope).SearchAsync(new ListingQuery { Sort = ListingSort.PriceDesc }, false, default);

        Assert.Equal(["try-3m", "usd-100k", "try-4.5m", "eur-100k"], asc.Items.Select(i => i.Title));
        Assert.Equal(["eur-100k", "try-4.5m", "usd-100k", "try-3m"], desc.Items.Select(i => i.Title));
    }

    [Fact]
    public async Task Text_WithPercentAndQuote_DoesNotActAsWildcardOrThrow()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db,
            new ListingBuilder().Titled("A%B").Build(),
            new ListingBuilder().Titled("AxB").Build(),
            new ListingBuilder().Titled("A_B").Build(),
            new ListingBuilder().Titled(@"C\D").Build(),
            new ListingBuilder().Titled("CxD").Build());
        var svc = Svc(scope);

        var percent = await svc.SearchAsync(new ListingQuery { Text = "A%B" }, false, default);
        var underscore = await svc.SearchAsync(new ListingQuery { Text = "A_B" }, false, default);
        var backslash = await svc.SearchAsync(new ListingQuery { Text = @"C\D" }, false, default);
        var quote = await svc.SearchAsync(new ListingQuery { Text = "'" }, false, default);

        Assert.Equal(["A%B"], Titles(percent.Items));
        Assert.Equal(["A_B"], Titles(underscore.Items));
        Assert.Equal([@"C\D"], Titles(backslash.Items));
        Assert.Empty(quote.Items);
    }

    [Fact]
    public async Task Text_Digits_MatchesListingNumberExactly()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = new ListingBuilder().Titled("hedef").Build();
        await SeedAsync(db, target, new ListingBuilder().Titled("diger").Build());

        var (items, _) = await Svc(scope).SearchAsync(new ListingQuery { Text = target.Id.ToString() }, false, default);

        Assert.Equal(["hedef"], Titles(items));
    }

    [Fact]
    public async Task Status_Active_ExcludesDeleted_DeletedShowsOnlyDeleted_AllShowsBoth()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db,
            new ListingBuilder().Titled("aktif").Build(),
            new ListingBuilder().Titled("silinmis").Deleted().Build());
        var svc = Svc(scope);

        var active = await svc.SearchAsync(new ListingQuery { Status = ListingStatus.Active }, false, default);
        var deleted = await svc.SearchAsync(new ListingQuery { Status = ListingStatus.Deleted }, false, default);
        var all = await svc.SearchAsync(new ListingQuery { Status = ListingStatus.All }, false, default);

        Assert.Equal(["aktif"], Titles(active.Items));
        Assert.Equal(["silinmis"], Titles(deleted.Items));
        Assert.Equal(["aktif", "silinmis"], Titles(all.Items));
        Assert.Equal(2, all.Total);
    }

    [Fact]
    public async Task OwnerText_OnlyWorksWhenAllowed()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db,
            new ListingBuilder().Titled("ayse").WithOwner("Ayşe Yılmaz", "0532 111 22 33").Build(),
            new ListingBuilder().Titled("mehmet").WithOwner("Mehmet Kaya", "0533 444 55 66").Build());
        var svc = Svc(scope);

        var byName = await svc.SearchAsync(new ListingQuery { OwnerText = "yılmaz" }, true, default);
        var byPhone = await svc.SearchAsync(new ListingQuery { OwnerText = "444 55" }, true, default);
        var denied = await svc.SearchAsync(new ListingQuery { OwnerText = "yılmaz" }, false, default);

        Assert.Equal(["ayse"], Titles(byName.Items));
        Assert.Equal(["mehmet"], Titles(byPhone.Items));
        Assert.Equal(2, denied.Items.Count); // yetkisiz çağrıda OwnerText yok sayılır, süzmez ve sızdırmaz
    }

    [Fact]
    public async Task FilterOptions_CountsActiveOnly()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db,
            new ListingBuilder().In("Istanbul", "Kadikoy").WithRooms("3+1").Build(),
            new ListingBuilder().In("Istanbul", "Besiktas").WithRooms("3+1").Build(),
            new ListingBuilder().In("Ankara", "Cankaya").WithRooms("2+1").Build(),
            new ListingBuilder().In("Izmir", "Konak").WithRooms("10+1").Deleted().Build(),                       // silinmiş
            new ListingBuilder().In("Bursa", "Nilufer").WithRooms("4+1").OfType(ListingType.Kiralik).Build());     // başka tür

        var options = await Svc(scope).GetFilterOptionsAsync(ListingType.Satilik, default);

        Assert.Equal(["Ankara", "Istanbul"], options.Cities);
        Assert.Equal(["Besiktas", "Kadikoy"], options.DistrictsByCity["Istanbul"]);
        Assert.Equal(["Cankaya"], options.DistrictsByCity["Ankara"]);
        Assert.Equal(2, options.DistrictsByCity.Count);
        Assert.Equal(2, options.RoomCounts["3+1"]);
        Assert.Equal(1, options.RoomCounts["2+1"]);
        Assert.Equal(2, options.RoomCounts.Count);
        Assert.Equal(["2+1", "3+1"], options.RoomCounts.Keys);
    }

    [Fact]
    public async Task Paging_AndPhotosOwnerAreLoaded()
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = DateTime.UtcNow;
        await SeedAsync(db,
            new ListingBuilder().Titled("eski").CreatedAt(t.AddDays(-2)).Build(),
            new ListingBuilder().Titled("orta").CreatedAt(t.AddDays(-1)).WithPhotos("b.jpg", "c.jpg").Build(),
            new ListingBuilder().Titled("yeni").CreatedAt(t).Build());

        var page1 = await Svc(scope).SearchAsync(new ListingQuery { PageSize = 2 }, false, default);
        var page2 = await Svc(scope).SearchAsync(new ListingQuery { PageSize = 2, Page = 2 }, false, default);

        Assert.Equal(3, page1.Total);
        Assert.Equal(["yeni", "orta"], page1.Items.Select(i => i.Title));
        Assert.Equal(["eski"], page2.Items.Select(i => i.Title));
        Assert.Equal(["b.jpg", "c.jpg"], page1.Items[1].Photos.Select(p => p.FilePath));
        Assert.All(page1.Items, i => Assert.NotNull(i.Owner));
    }

    [Fact]
    public async Task PriceFilter_IsTranslatedToSql_NotEvaluatedOnClient()
    {
        await using var scope = await FreshScopeAsync();
        var sql = Svc(scope)
            .Filter(new ListingQuery { MinPriceTry = 3_900_000m, MaxPriceTry = 4_100_000m }, false, 40m, 41m)
            .ToQueryString();
        _output.WriteLine(sql);

        Assert.Contains("CASE", sql);
        Assert.Contains("\"Price\" *", sql);
        Assert.Contains(">= 3900000", sql);
        Assert.Contains("<= 4100000", sql);
    }

    public static TheoryData<string, Action<Listing>, ListingQuery> FilterCases => new()
    {
        { "Rooms", l => l.Rooms = "2+1", new() { Rooms = ["2+1", "4+1"] } },
        { "Floor", l => l.Floor = 7, new() { Floor = 7 } },
        { "MaxBuildingAge", l => l.BuildingAge = 1, new() { MaxBuildingAge = 2 } },
        { "MinM2 (sınır dahil)", l => l.GrossM2 = 200, new() { MinM2 = 200 } },
        { "MaxM2 (sınır dahil)", l => l.GrossM2 = 50, new() { MaxM2 = 50 } },
        { "Heating", l => l.Heating = HeatingType.Dogalgaz, new() { Heating = HeatingType.Dogalgaz } },
        { "Furnishing", l => l.Furnishing = FurnishingStatus.Esyali, new() { Furnishing = FurnishingStatus.Esyali } },
        { "Balcony", l => l.Balcony = true, new() { Balcony = true } },
        { "Elevator", l => l.Elevator = true, new() { Elevator = true } },
        { "Parking", l => l.Parking = true, new() { Parking = true } },
        { "InSite", l => l.InSite = true, new() { InSite = true } },
        { "LoanEligible", l => l.LoanEligible = true, new() { LoanEligible = true } },
        { "Swap", l => l.Swap = true, new() { Swap = true } },
        { "Neighborhood", l => l.Neighborhood = "Moda", new() { Neighborhood = "Moda" } },
        { "MinPriceTry (sınır dahil)", l => l.Price = 2_000_000m, new() { MinPriceTry = 2_000_000m } },
    };

    /// <summary>Varsayılan ilan eşleşmez; yalnızca ilgili alanı değiştirilmiş ilan, ilgili süzgeçle gelir.</summary>
    [Theory]
    [MemberData(nameof(FilterCases))]
    public async Task SingleFilter_ReturnsOnlyTheMatchingListing(string name, Action<Listing> makeMatch, ListingQuery query)
    {
        await using var scope = await FreshScopeAsync();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = new ListingBuilder().Titled("eslesen").Build();
        makeMatch(match);
        await SeedAsync(db, match, new ListingBuilder().Titled("varsayilan").Build());

        var (items, total) = await Svc(scope).SearchAsync(query, false, default);

        Assert.True(items.Select(i => i.Title).SequenceEqual(["eslesen"]), $"{name}: {string.Join(",", items.Select(i => i.Title))}");
        Assert.Equal(1, total);
    }
}
