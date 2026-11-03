using Emlak.Api.Domain;

namespace Emlak.Api.Listings;

public enum ListingSort { Newest, PriceAsc, PriceDesc }

public enum ListingStatus { Active, Deleted, All }

/// <summary>
/// İlan arama kriterleri. Fiyat sınırları ₺ cinsindendir; ilanın para birimi kurla ₺'ye çevrilerek karşılaştırılır.
/// <c>Status</c>, <c>OwnerText</c> ve <c>AdvisorId</c> yalnızca yönetici uç noktalarının geçirmesi içindir; ziyaretçi uç noktaları Active'e zorlar.
/// </summary>
public record ListingQuery(
    ListingType? Type = null, // null = tür filtresi yok (yalnızca yönetici/saha); ziyaretçi uç noktalarında da iki tür birden gelir
    string? City = null,
    string? District = null,
    string? Neighborhood = null,
    Currency? Currency = null,
    decimal? MinPriceTry = null,
    decimal? MaxPriceTry = null,
    int? MinM2 = null,
    int? MaxM2 = null,
    string[]? Rooms = null,
    int? MaxBuildingAge = null,
    int? Floor = null,
    int? MinBathrooms = null,
    int? MinTotalFloors = null,
    int? MaxTotalFloors = null,
    HeatingType? Heating = null,
    FurnishingStatus? Furnishing = null,
    bool? Balcony = null,
    bool? Elevator = null,
    bool? Parking = null,
    bool? InSite = null,
    bool? LoanEligible = null,
    bool? Swap = null,
    string? Text = null,
    ListingSort Sort = ListingSort.Newest,
    int Page = 1,
    int PageSize = 20,
    ListingStatus Status = ListingStatus.Active,
    string? OwnerText = null,
    int? AdvisorId = null); // yalnızca yönetici/saha; ziyaretçi uç noktaları yok sayar

public record FilterOptions(
    string[] Cities,
    Dictionary<string, string[]> DistrictsByCity,
    Dictionary<string, int> RoomCounts);
