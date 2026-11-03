using Emlak.Api.Domain;
using Emlak.Api.Photos;

namespace Emlak.Api.Listings;

// Yalnızca Admin rolüne giden cevaplar. Saha (Field) rolü bu türleri hiç görmez: onun cevapları PublicListingSummary/
// PublicListingDetail ile üretilir; ev sahibi sızıntısı bu yüzden tasarım gereği imkânsızdır. Listing varlığı hiçbir DTO'ya gömülmez.

public record AdminOwner(string Name, string Phone, string? Note);

public record AdminListingSummary(
    int Id, string Title, string? Badge, ListingType Type, decimal Price, Currency Currency,
    string City, string District, string Rooms, int GrossM2, int Floor, DateTime CreatedAt, string[] PhotoUrls,
    string CreatedBy, bool IsDeleted, DateTime? DeletedAt, string? DeletedBy, AdminOwner Owner);

public record AdminListingDetail(
    int Id, string Title, string? Badge, ListingType Type, decimal Price, Currency Currency,
    string City, string District, string Rooms, int GrossM2, int Floor, DateTime CreatedAt, string[] PhotoUrls,
    decimal? Deposit, decimal? Aidat, string Neighborhood, int Bathrooms, int NetM2, int TotalFloors, int BuildingAge,
    HeatingType Heating, FurnishingStatus Furnishing, UsageStatus Usage, DeedStatus Deed, KitchenType Kitchen,
    bool Balcony, bool Elevator, bool Parking, bool InSite, bool LoanEligible, bool Swap,
    PublicTag[] Tags, string Description, int ShowingCount,
    AdminOwner Owner,
    string CreatedBy, DateTime? UpdatedAt, string? UpdatedBy,
    bool IsDeleted, DateTime? DeletedAt, string? DeletedBy, DateTime? RestoredAt, string? RestoredBy,
    PhotoDto[] Photos);

/// <summary>Oluşturma/güncelleme gövdesi. Alanlar boş bırakılabilir; eksikler <see cref="ListingValidator"/> ile 400 olur.</summary>
public record AdminListingInput
{
    public string? Title { get; init; }
    public string? Badge { get; init; }
    public ListingType Type { get; init; }
    public decimal Price { get; init; }
    public Currency Currency { get; init; }
    public decimal? Deposit { get; init; }
    public decimal? Aidat { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? Neighborhood { get; init; }
    public string? Rooms { get; init; }
    public int Bathrooms { get; init; }
    public int GrossM2 { get; init; }
    public int NetM2 { get; init; }
    public int Floor { get; init; }
    public int TotalFloors { get; init; }
    public int BuildingAge { get; init; }
    public HeatingType Heating { get; init; }
    public FurnishingStatus Furnishing { get; init; }
    public UsageStatus Usage { get; init; }
    public DeedStatus Deed { get; init; }
    public KitchenType Kitchen { get; init; }
    public bool Balcony { get; init; }
    public bool Elevator { get; init; }
    public bool Parking { get; init; }
    public bool InSite { get; init; }
    public bool LoanEligible { get; init; }
    public bool Swap { get; init; }
    public string? Description { get; init; }
    public string? OwnerName { get; init; }
    public string? OwnerPhone { get; init; }
    public string? OwnerNote { get; init; }
    public int[]? TagIds { get; init; }
}

public record CreateTagRequest(string? Name, TagGroup? Group);

public static class AdminMapper
{
    public static AdminOwner ToOwner(Owner o) => new(o.Name, o.Phone, o.Note);

    public static AdminListingSummary ToSummary(Listing l) => new(
        l.Id, l.Title, l.Badge, l.Type, l.Price, l.Currency, l.City, l.District, l.Rooms, l.GrossM2, l.Floor, l.CreatedAt,
        PublicMapper.PhotoUrls(l),
        l.CreatedBy, l.IsDeleted, l.DeletedAt, l.DeletedBy, ToOwner(l.Owner));

    public static AdminListingDetail ToDetail(Listing l, int showingCount)
    {
        var p = PublicMapper.ToDetail(l, showingCount);
        return new AdminListingDetail(
            p.Id, p.Title, p.Badge, p.Type, p.Price, p.Currency, p.City, p.District, p.Rooms, p.GrossM2, p.Floor, p.CreatedAt, p.PhotoUrls,
            p.Deposit, p.Aidat, p.Neighborhood, p.Bathrooms, p.NetM2, p.TotalFloors, p.BuildingAge,
            p.Heating, p.Furnishing, p.Usage, p.Deed, p.Kitchen,
            p.Balcony, p.Elevator, p.Parking, p.InSite, p.LoanEligible, p.Swap,
            p.Tags, p.Description, p.ShowingCount,
            ToOwner(l.Owner),
            l.CreatedBy, l.UpdatedAt, l.UpdatedBy,
            l.IsDeleted, l.DeletedAt, l.DeletedBy, l.RestoredAt, l.RestoredBy,
            // Yönetici fotoğraf yönetimi için kimlikli liste (ziyaretçi cevabında yalnızca adresler vardır).
            l.Photos.OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new PhotoDto(x.Id, PhotoUrl.For(x.FilePath), x.SortOrder)).ToArray());
    }
}
