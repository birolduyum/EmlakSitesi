namespace Emlak.Api.Listings;

public static class ListingExtensions
{
    public static IServiceCollection AddListingSearch(this IServiceCollection services)
        => services.AddScoped<ListingSearchService>();
}
