using Emlak.Api.Domain;

namespace Emlak.Api.Listings;

// Ziyaretçiye giden cevaplar. Bilerek ev sahibi, oluşturan/silen/güncelleyen alanları ve Listing varlığının kendisi yoktur.

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record PublicListingSummary(
    int Id, string Title, string? Badge, ListingType Type, decimal Price, Currency Currency,
    string City, string District, string Rooms, int GrossM2, int Floor, DateTime CreatedAt, string[] PhotoUrls);

public record PublicTag(int Id, string Name, TagGroup Group);

public record PublicListingDetail(
    int Id, string Title, string? Badge, ListingType Type, decimal Price, Currency Currency,
    string City, string District, string Rooms, int GrossM2, int Floor, DateTime CreatedAt, string[] PhotoUrls,
    decimal? Deposit, decimal? Aidat, string Neighborhood, int Bathrooms, int NetM2, int TotalFloors, int BuildingAge,
    HeatingType Heating, FurnishingStatus Furnishing, UsageStatus Usage, DeedStatus Deed, KitchenType Kitchen,
    bool Balcony, bool Elevator, bool Parking, bool InSite, bool LoanEligible, bool Swap,
    PublicTag[] Tags, string Description, int ShowingCount, DateTime UpdatedAt);

public record SiteInfo(string AdvisorName, string AdvisorPhone, string? WhatsappUrl);

/// <summary>Danışman ayarlarından <see cref="SiteInfo"/> üretir. WhatsApp boşsa telefon rakamlarından wa.me bağlantısı türetilir.</summary>
public static class SiteInfoFactory
{
    public static SiteInfo Create(string? name, string? phone, string? whatsapp)
        => new(name ?? "", phone ?? "", WhatsappUrl(phone, whatsapp));

    public static string? WhatsappUrl(string? phone, string? whatsapp)
    {
        var configured = whatsapp?.Trim();
        if (!string.IsNullOrEmpty(configured))
        {
            if (configured.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return configured;
            var digits = Digits(configured);
            if (digits.Length > 0) return $"https://wa.me/{digits}";
        }

        var phoneDigits = Digits(phone);
        return phoneDigits.Length is >= 7 and <= 15 ? $"https://wa.me/{phoneDigits}" : null;
    }

    private static string Digits(string? value) => new(value?.Where(char.IsAsciiDigit).ToArray() ?? []);
}

/// <summary>Listing → ziyaretçi DTO'ları için tek eşleme yeri; yeni bir alan buraya eklenmedikçe ziyaretçiye çıkmaz.</summary>
public static class PublicMapper
{
    public static PublicListingSummary ToSummary(Listing l) => new(
        l.Id, l.Title, l.Badge, l.Type, l.Price, l.Currency, l.City, l.District, l.Rooms, l.GrossM2, l.Floor, l.CreatedAt,
        PhotoUrls(l));

    public static PublicListingDetail ToDetail(Listing l, int showingCount) => new(
        l.Id, l.Title, l.Badge, l.Type, l.Price, l.Currency, l.City, l.District, l.Rooms, l.GrossM2, l.Floor, l.CreatedAt,
        PhotoUrls(l),
        l.Deposit, l.Aidat, l.Neighborhood, l.Bathrooms, l.NetM2, l.TotalFloors, l.BuildingAge,
        l.Heating, l.Furnishing, l.Usage, l.Deed, l.Kitchen,
        l.Balcony, l.Elevator, l.Parking, l.InSite, l.LoanEligible, l.Swap,
        l.Tags.OrderBy(t => t.Group).ThenBy(t => t.Name, StringComparer.CurrentCulture).Select(ToTag).ToArray(),
        l.Description, showingCount, l.UpdatedAt ?? l.CreatedAt);

    public static PublicTag ToTag(FeatureTag t) => new(t.Id, t.Name, t.Group);

    public static string[] PhotoUrls(Listing l)
        => l.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => Emlak.Api.Photos.PhotoUrl.For(p.FilePath))
            .ToArray();
}
