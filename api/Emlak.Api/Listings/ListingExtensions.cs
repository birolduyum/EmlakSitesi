using System.Text.Json.Serialization;

namespace Emlak.Api.Listings;

public static class ListingExtensions
{
    public static IServiceCollection AddListingSearch(this IServiceCollection services)
    {
        // Enum'lar JSON'da adlarıyla gider ("Satilik"); sorgu dizesindeki adlarla aynı biçim.
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services.AddScoped<ListingSearchService>();
    }
}
