using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Listings;

public static class PublicListingEndpoints
{
    private const int MaxPage = 10_000;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapPublicListingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/listings", async ([AsParameters] ListingQuery q, ListingSearchService search, CancellationToken ct) =>
        {
            // Tür verilmezse iki tür birden listelenir. Ziyaretçi yalnızca aktif ilanı görür, sahip aramasını kullanamaz; sayfa sınırları serviste taşmadan önce kıstırılır.
            var safe = q with
            {
                Status = ListingStatus.Active,
                OwnerText = null,
                Page = Math.Clamp(q.Page, 1, MaxPage),
                PageSize = Math.Clamp(q.PageSize, 1, MaxPageSize),
            };
            var (items, total) = await search.SearchAsync(safe, includeOwnerSearch: false, ct);
            return Results.Ok(new PagedResult<PublicListingSummary>(
                items.Select(PublicMapper.ToSummary).ToList(), total, safe.Page, safe.PageSize));
        }).AllowAnonymous();

        app.MapGet("/api/listings/filters", async ([FromQuery] ListingType? type, ListingSearchService search, CancellationToken ct)
            => Results.Ok(await search.GetFilterOptionsAsync(type, ct))).AllowAnonymous();

        app.MapGet("/api/listings/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var listing = await db.Listings.AsNoTracking()
                .Include(l => l.Photos)
                .Include(l => l.Tags)
                .AsSplitQuery()
                .SingleOrDefaultAsync(l => l.Id == id && !l.IsDeleted, ct);
            if (listing is null)
            {
                return Results.NotFound();
            }

            var showingCount = await db.Showings.CountAsync(s => s.ListingId == id, ct);
            return Results.Ok(PublicMapper.ToDetail(listing, showingCount));
        }).AllowAnonymous();

        app.MapGet("/api/tags", async (AppDbContext db, CancellationToken ct) =>
        {
            var tags = await db.FeatureTags.AsNoTracking().OrderBy(t => t.Group).ThenBy(t => t.Name).ToListAsync(ct);
            return Results.Ok(tags.Select(PublicMapper.ToTag));
        }).AllowAnonymous();

        app.MapGet("/api/site", (IConfiguration config) =>
        {
            return Results.Ok(SiteInfoFactory.Create(config["Advisor:Name"], config["Advisor:Phone"], config["Advisor:WhatsApp"]));
        }).AllowAnonymous();

        return app;
    }
}
