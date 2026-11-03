namespace Emlak.Api.Domain;

public class Listing
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Badge { get; set; }
    public ListingType Type { get; set; }
    public decimal Price { get; set; }
    public Currency Currency { get; set; }
    public decimal? Deposit { get; set; }
    public decimal? Aidat { get; set; }
    public string City { get; set; } = "";
    public string District { get; set; } = "";
    public string Neighborhood { get; set; } = "";
    public string Rooms { get; set; } = "";
    public int Bathrooms { get; set; }
    public int GrossM2 { get; set; }
    public int NetM2 { get; set; }
    public int Floor { get; set; }
    public int TotalFloors { get; set; }
    public int BuildingAge { get; set; }
    public HeatingType Heating { get; set; }
    public FurnishingStatus Furnishing { get; set; }
    public UsageStatus Usage { get; set; }
    public DeedStatus Deed { get; set; }
    public KitchenType Kitchen { get; set; }
    public bool Balcony { get; set; }
    public bool Elevator { get; set; }
    public bool Parking { get; set; }
    public bool InSite { get; set; }
    public bool LoanEligible { get; set; }
    public bool Swap { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public DateTime? RestoredAt { get; set; }
    public string? RestoredBy { get; set; }
    public int? AdvisorId { get; set; }

    public Advisor? Advisor { get; set; }
    public Owner Owner { get; set; } = null!;
    public List<Photo> Photos { get; set; } = new();
    public List<FeatureTag> Tags { get; set; } = new();
}
