using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Listings;

public class ListingSearchService(AppDbContext db)
{
    private const int MaxPageSize = 100;

    public async Task<(List<Listing> Items, int Total)> SearchAsync(ListingQuery q, bool includeOwnerSearch, CancellationToken ct)
    {
        var (usd, eur) = await ReadRatesAsync(ct);
        var priceTry = PriceTry(usd, eur);
        var query = Filter(q, includeOwnerSearch, usd, eur);
        var total = await query.CountAsync(ct);

        var ordered = q.Sort switch
        {
            ListingSort.PriceAsc => query.OrderBy(priceTry).ThenBy(l => l.Id),
            ListingSort.PriceDesc => query.OrderByDescending(priceTry).ThenByDescending(l => l.Id),
            _ => query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id),
        };

        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var page = Math.Max(q.Page, 1);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(l => l.Owner)
            .Include(l => l.Photos.OrderBy(p => p.SortOrder))
            .AsSplitQuery()
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>Verilen türün yalnızca aktif ilanlarından il, il→ilçeler ve oda sayısı seçenekleri.</summary>
    public async Task<FilterOptions> GetFilterOptionsAsync(ListingType type, CancellationToken ct)
    {
        var active = db.Listings.AsNoTracking().Where(l => l.Type == type && !l.IsDeleted);

        var cityDistricts = await active.Select(l => new { l.City, l.District }).Distinct().ToListAsync(ct);
        var cities = cityDistricts.Select(x => x.City).Distinct().OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray();
        var districts = cityDistricts
            .GroupBy(x => x.City)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.District).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToArray());

        var roomRows = await active.GroupBy(l => l.Rooms).Select(g => new { Rooms = g.Key, Count = g.Count() }).ToListAsync(ct);
        var roomCounts = roomRows
            .OrderBy(r => FirstNumber(r.Rooms)).ThenBy(r => r.Rooms, StringComparer.Ordinal)
            .ToDictionary(r => r.Rooms, r => r.Count);

        return new FilterOptions(cities, districts, roomCounts);
    }

    /// <summary>Filtrelenmiş sorgu (sayfalama/Include yok). Kurlar dışarıdan verilir; ₺ çevirisi SQL'e çevrilir.</summary>
    public IQueryable<Listing> Filter(ListingQuery q, bool includeOwnerSearch, decimal usdRate, decimal eurRate)
        => ApplyFilters(db.Listings.AsNoTracking(), q, includeOwnerSearch, PriceTry(usdRate, eurRate));

    private static int FirstNumber(string rooms)
    {
        var m = Regex.Match(rooms, @"\d+");
        return m.Success && int.TryParse(m.Value, out var n) ? n : int.MaxValue;
    }

    /// <summary>İki kuru tek seferde okur (2 küçük satır); eksik/sıfır kur sessizce yanlış sonuç yerine hata verir.</summary>
    private async Task<(decimal Usd, decimal Eur)> ReadRatesAsync(CancellationToken ct)
    {
        var rates = await db.CurrencyRates.AsNoTracking().ToDictionaryAsync(r => r.Currency, r => r.TryPerUnit, ct);
        if (!rates.TryGetValue(Currency.USD, out var usd) || usd <= 0 ||
            !rates.TryGetValue(Currency.EUR, out var eur) || eur <= 0)
        {
            throw new InvalidOperationException("USD/EUR kuru tanımlı değil ya da geçersiz.");
        }

        return (usd, eur);
    }

    /// <summary>₺ karşılığı; EF bunu SQL'de CASE ifadesine çevirir (istemci tarafı değerlendirme yok).</summary>
    private static Expression<Func<Listing, decimal>> PriceTry(decimal usd, decimal eur)
        => l => l.Price * (l.Currency == Currency.TRY ? 1m : l.Currency == Currency.USD ? usd : eur);

    private static IQueryable<Listing> ApplyFilters(
        IQueryable<Listing> src, ListingQuery q, bool includeOwnerSearch, Expression<Func<Listing, decimal>> priceTry)
    {
        var query = src.Where(l => l.Type == q.Type);

        query = q.Status switch
        {
            ListingStatus.Active => query.Where(l => !l.IsDeleted),
            ListingStatus.Deleted => query.Where(l => l.IsDeleted),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(q.City)) { var v = q.City.Trim(); query = query.Where(l => l.City == v); }
        if (!string.IsNullOrWhiteSpace(q.District)) { var v = q.District.Trim(); query = query.Where(l => l.District == v); }
        if (!string.IsNullOrWhiteSpace(q.Neighborhood)) { var v = q.Neighborhood.Trim(); query = query.Where(l => l.Neighborhood == v); }

        if (q.Currency is { } cur) { query = query.Where(l => l.Currency == cur); }

        if (q.MinPriceTry is { } min) { query = query.Where(Compare(priceTry, min, Expression.GreaterThanOrEqual)); }
        if (q.MaxPriceTry is { } max) { query = query.Where(Compare(priceTry, max, Expression.LessThanOrEqual)); }

        if (q.MinM2 is { } minM2) { query = query.Where(l => l.GrossM2 >= minM2); }
        if (q.MaxM2 is { } maxM2) { query = query.Where(l => l.GrossM2 <= maxM2); }

        if (q.Rooms is { Length: > 0 } rooms) { query = query.Where(l => rooms.Contains(l.Rooms)); }
        if (q.MaxBuildingAge is { } age) { query = query.Where(l => l.BuildingAge <= age); }
        if (q.Floor is { } floor) { query = query.Where(l => l.Floor == floor); }
        if (q.Heating is { } heating) { query = query.Where(l => l.Heating == heating); }
        if (q.Furnishing is { } furnishing) { query = query.Where(l => l.Furnishing == furnishing); }
        if (q.Balcony is { } balcony) { query = query.Where(l => l.Balcony == balcony); }
        if (q.Elevator is { } elevator) { query = query.Where(l => l.Elevator == elevator); }
        if (q.Parking is { } parking) { query = query.Where(l => l.Parking == parking); }
        if (q.InSite is { } inSite) { query = query.Where(l => l.InSite == inSite); }
        if (q.LoanEligible is { } loan) { query = query.Where(l => l.LoanEligible == loan); }
        if (q.Swap is { } swap) { query = query.Where(l => l.Swap == swap); }

        if (!string.IsNullOrWhiteSpace(q.Text))
        {
            var text = q.Text.Trim();
            var pattern = $"%{EscapeLike(text)}%";
            // Yalnızca rakamlardan oluşan metin ilan numarasıyla birebir de eşleşir; id'si olmayan metinde -1 hiçbir ilanla eşleşmez.
            var id = int.TryParse(text, out var parsed) ? parsed : -1;
            query = query.Where(l => l.Id == id
                || EF.Functions.ILike(l.Title, pattern, "\\")
                || EF.Functions.ILike(l.City, pattern, "\\")
                || EF.Functions.ILike(l.District, pattern, "\\")
                || EF.Functions.ILike(l.Neighborhood, pattern, "\\"));
        }

        if (includeOwnerSearch && !string.IsNullOrWhiteSpace(q.OwnerText))
        {
            var pattern = $"%{EscapeLike(q.OwnerText.Trim())}%";
            query = query.Where(l => EF.Functions.ILike(l.Owner.Name, pattern, "\\") || EF.Functions.ILike(l.Owner.Phone, pattern, "\\"));
        }

        return query;
    }

    private static string EscapeLike(string s)
        => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static Expression<Func<Listing, bool>> Compare(
        Expression<Func<Listing, decimal>> price, decimal bound, Func<Expression, Expression, BinaryExpression> op)
        => Expression.Lambda<Func<Listing, bool>>(op(price.Body, Expression.Constant(bound)), price.Parameters);
}
