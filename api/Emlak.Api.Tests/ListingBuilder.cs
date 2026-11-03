using Emlak.Api.Domain;

namespace Emlak.Api.Tests;

/// <summary>Test verisi için akıcı ilan oluşturucu; varsayılanlar geçerli bir satılık ilandır (ev sahibiyle).</summary>
public class ListingBuilder
{
    private static int _seq;
    private readonly Listing _l;

    public ListingBuilder()
    {
        var n = Interlocked.Increment(ref _seq);
        _l = new Listing
        {
            Title = $"Test ilanı {n}",
            Type = ListingType.Satilik,
            Price = 1_000_000m,
            Currency = Currency.TRY,
            City = "Istanbul",
            District = "Kadikoy",
            Neighborhood = "Caddebostan",
            Rooms = "3+1",
            Bathrooms = 1,
            GrossM2 = 120,
            NetM2 = 100,
            Floor = 2,
            TotalFloors = 5,
            BuildingAge = 5,
            Heating = HeatingType.Kombi,
            Furnishing = FurnishingStatus.Esyasiz,
            Usage = UsageStatus.Bos,
            Deed = DeedStatus.KatMulkiyeti,
            Kitchen = KitchenType.Kapali,
            Description = "Açıklama",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "admin1",
            Owner = new Owner { Name = "Ev Sahibi", Phone = "0555 000 00 00" },
        };
    }

    public ListingBuilder Titled(string title) { _l.Title = title; return this; }
    public ListingBuilder OfType(ListingType type) { _l.Type = type; return this; }
    public ListingBuilder Priced(decimal price, Currency currency = Currency.TRY) { _l.Price = price; _l.Currency = currency; return this; }
    public ListingBuilder In(string city, string district = "Kadikoy", string neighborhood = "Caddebostan")
    {
        _l.City = city; _l.District = district; _l.Neighborhood = neighborhood; return this;
    }
    public ListingBuilder WithRooms(string rooms) { _l.Rooms = rooms; return this; }
    public ListingBuilder WithOwner(string name, string phone, string? note = null) { _l.Owner = new Owner { Name = name, Phone = phone, Note = note }; return this; }
    public ListingBuilder WithTags(params FeatureTag[] tags) { _l.Tags = tags.ToList(); return this; }
    public ListingBuilder WithAdvisor(Advisor advisor) { _l.Advisor = advisor; return this; }
    public ListingBuilder CreatedAt(DateTimeOffset utc) { _l.CreatedAt = utc; return this; }
    public ListingBuilder UpdatedAt(DateTimeOffset utc) { _l.UpdatedAt = utc; return this; }
    public ListingBuilder Deleted() { _l.IsDeleted = true; _l.DeletedAt = DateTimeOffset.UtcNow; _l.DeletedBy = "admin1"; return this; }
    public ListingBuilder WithPhotos(params string[] paths)
    {
        _l.Photos = paths.Select((p, i) => new Photo { FilePath = p, SortOrder = i }).ToList();
        return this;
    }

    public Listing Build() => _l;
}
