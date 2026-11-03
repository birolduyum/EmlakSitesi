using Emlak.Api.Domain;

namespace Emlak.Api.Listings;

// Ziyaretçiye giden cevaplar. Bilerek ev sahibi, oluşturan/silen/güncelleyen alanları ve Listing varlığının kendisi yoktur.

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record PublicListingSummary(
    int Id, string Title, string? Badge, ListingType Type, decimal Price, Currency Currency,
    string City, string District, string Rooms, int GrossM2, int Floor, DateTimeOffset CreatedAt, string[] PhotoUrls, PublicAdvisor? Advisor);

public record PublicTag(int Id, string Name, TagGroup Group);

public record PublicListingDetail(
    int Id, string Title, string? Badge, ListingType Type, decimal Price, Currency Currency,
    string City, string District, string Rooms, int GrossM2, int Floor, DateTimeOffset CreatedAt, string[] PhotoUrls,
    decimal? Deposit, decimal? Aidat, string Neighborhood, int Bathrooms, int NetM2, int TotalFloors, int BuildingAge,
    HeatingType Heating, FurnishingStatus Furnishing, UsageStatus Usage, DeedStatus Deed, KitchenType Kitchen,
    bool Balcony, bool Elevator, bool Parking, bool InSite, bool LoanEligible, bool Swap,
    PublicTag[] Tags, string Description, int ShowingCount, DateTimeOffset UpdatedAt, PublicAdvisor? Advisor);

public record SiteInfo(string AdvisorName, string AdvisorPhone, string? WhatsappUrl);

/// <summary>Ziyaretçiye görünen danışman: yalnızca ad, telefon ve türetilmiş WhatsApp bağlantısı (kimlik, durum, denetim alanları yok).</summary>
public record PublicAdvisor(string Name, string Phone, string? WhatsappUrl);

/// <summary>Danışman ayarlarından <see cref="SiteInfo"/> üretir (varsayılan danışman). WhatsApp kuralları <see cref="WhatsappLink"/>'tedir.</summary>
public static class SiteInfoFactory
{
    public static SiteInfo Create(string? name, string? phone, string? whatsapp)
        => new(name ?? "", phone ?? "", WhatsappLink.Build(phone, whatsapp));

    public static SiteInfo FromConfig(IConfiguration config)
        => Create(config["Advisor:Name"], config["Advisor:Phone"], config["Advisor:WhatsApp"]);

    /// <summary>İlanda danışman yoksa gösterilecek varsayılan; ad ve telefon ikisi de boşsa null.</summary>
    public static PublicAdvisor? DefaultAdvisor(IConfiguration config)
    {
        var site = FromConfig(config);
        return string.IsNullOrWhiteSpace(site.AdvisorName) && string.IsNullOrWhiteSpace(site.AdvisorPhone)
            ? null
            : new PublicAdvisor(site.AdvisorName, site.AdvisorPhone, site.WhatsappUrl);
    }
}

/// <summary>Listing → ziyaretçi DTO'ları için tek eşleme yeri; yeni bir alan buraya eklenmedikçe ziyaretçiye çıkmaz.</summary>
public static class PublicMapper
{
    public static PublicListingSummary ToSummary(Listing l, PublicAdvisor? defaultAdvisor) => new(
        l.Id, l.Title, l.Badge, l.Type, l.Price, l.Currency, l.City, l.District, l.Rooms, l.GrossM2, l.Floor, l.CreatedAt,
        PhotoUrls(l), ToAdvisor(l, defaultAdvisor));

    public static PublicListingDetail ToDetail(Listing l, int showingCount, PublicAdvisor? defaultAdvisor) => new(
        l.Id, l.Title, l.Badge, l.Type, l.Price, l.Currency, l.City, l.District, l.Rooms, l.GrossM2, l.Floor, l.CreatedAt,
        PhotoUrls(l),
        l.Deposit, l.Aidat, l.Neighborhood, l.Bathrooms, l.NetM2, l.TotalFloors, l.BuildingAge,
        l.Heating, l.Furnishing, l.Usage, l.Deed, l.Kitchen,
        l.Balcony, l.Elevator, l.Parking, l.InSite, l.LoanEligible, l.Swap,
        l.Tags.OrderBy(t => t.Group).ThenBy(t => t.Name, StringComparer.CurrentCulture).Select(ToTag).ToArray(),
        l.Description, showingCount, l.UpdatedAt ?? l.CreatedAt, ToAdvisor(l, defaultAdvisor));

    /// <summary>İlanın danışmanı (pasif olsa da); yoksa varsayılan. Danışman varlığı yüklenmiş olmalıdır (Include).</summary>
    public static PublicAdvisor? ToAdvisor(Listing l, PublicAdvisor? defaultAdvisor)
        => l.Advisor is { } a ? new PublicAdvisor(a.Name, a.Phone, WhatsappLink.Build(a.Phone, a.WhatsApp)) : defaultAdvisor;

    public static PublicTag ToTag(FeatureTag t) => new(t.Id, t.Name, t.Group);

    public static string[] PhotoUrls(Listing l)
        => l.Photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => Emlak.Api.Photos.PhotoUrl.For(p.FilePath))
            .ToArray();
}
