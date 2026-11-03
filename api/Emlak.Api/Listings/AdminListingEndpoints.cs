using System.Security.Claims;
using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Listings;

public static class AdminListingEndpoints
{
    private const int MaxPage = 10_000;
    private const int MaxPageSize = 100;
    private const int MaxTagName = 60;

    public static IEndpointRouteBuilder MapAdminListingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin");

        // Admin: Status ve OwnerText geçerli, ev sahibi dahil. Field: yalnızca aktif, OwnerText yok sayılır, cevapta ev sahibi alanı hiç yok
        // (Field cevabı yalnızca Public* DTO'larıyla üretilir; AdminListing* türlerine hiç dokunmaz).
        g.MapGet("/listings", async ([AsParameters] ListingQuery q, ClaimsPrincipal user, ListingSearchService search, CancellationToken ct) =>
        {
            var isAdmin = IsAdmin(user);
            var safe = q with
            {
                Status = isAdmin ? q.Status : ListingStatus.Active,
                OwnerText = isAdmin ? q.OwnerText : null,
                Page = Math.Clamp(q.Page, 1, MaxPage),
                PageSize = Math.Clamp(q.PageSize, 1, MaxPageSize),
            };
            var (items, total) = await search.SearchAsync(safe, includeOwnerSearch: isAdmin, ct, includeOwner: isAdmin);

            return isAdmin
                ? Results.Ok(new PagedResult<AdminListingSummary>(items.Select(AdminMapper.ToSummary).ToList(), total, safe.Page, safe.PageSize))
                : Results.Ok(new PagedResult<PublicListingSummary>(items.Select(PublicMapper.ToSummary).ToList(), total, safe.Page, safe.PageSize));
        }).RequireAuthorization(AuthExtensions.Staff);

        g.MapGet("/listings/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var isAdmin = IsAdmin(user);
            var listing = await LoadAsync(db.Listings.AsNoTracking(), id, includeOwner: isAdmin, ct);
            if (listing is null || (!isAdmin && listing.IsDeleted))
            {
                return Results.NotFound();
            }

            var showingCount = await db.Showings.CountAsync(s => s.ListingId == id, ct);
            return isAdmin
                ? Results.Ok(AdminMapper.ToDetail(listing, showingCount))
                : Results.Ok(PublicMapper.ToDetail(listing, showingCount));
        }).RequireAuthorization(AuthExtensions.Staff);

        g.MapPost("/listings", async (AdminListingInput input, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var errors = ListingValidator.Validate(input);
            if (errors.Length > 0) { return BadRequest(errors); }

            var tags = await ResolveTagsAsync(db, input.TagIds, ct);
            if (tags is null) { return BadRequest(["Bilinmeyen etiket seçildi."]); }

            var listing = new Listing { CreatedAt = DateTime.UtcNow, CreatedBy = UserName(user), Owner = new Owner() };
            Apply(listing, input, tags);
            db.Listings.Add(listing);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/admin/listings/{listing.Id}", AdminMapper.ToDetail(listing, 0));
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        // Silinmiş ilan güncellenmez (404): önce geri alınmalıdır. Fotoğraflara dokunulmaz.
        g.MapPut("/listings/{id:int}", async (int id, AdminListingInput input, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var errors = ListingValidator.Validate(input);
            if (errors.Length > 0) { return BadRequest(errors); }

            var listing = await LoadAsync(db.Listings, id, includeOwner: true, ct);
            if (listing is null || listing.IsDeleted) { return Results.NotFound(); }

            var tags = await ResolveTagsAsync(db, input.TagIds, ct);
            if (tags is null) { return BadRequest(["Bilinmeyen etiket seçildi."]); }

            Apply(listing, input, tags);
            listing.UpdatedAt = DateTime.UtcNow;
            listing.UpdatedBy = UserName(user);
            await db.SaveChangesAsync(ct);

            var showingCount = await db.Showings.CountAsync(s => s.ListingId == id, ct);
            return Results.Ok(AdminMapper.ToDetail(listing, showingCount));
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        // Yumuşak silme. Zaten silinmişse idempotent 204: ilk silmenin kaydı (kim/ne zaman) korunur. Olmayan id: 404.
        g.MapDelete("/listings/{id:int}", async (int id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var listing = await db.Listings.SingleOrDefaultAsync(l => l.Id == id, ct);
            if (listing is null) { return Results.NotFound(); }

            if (!listing.IsDeleted)
            {
                listing.IsDeleted = true;
                listing.DeletedAt = DateTime.UtcNow;
                listing.DeletedBy = UserName(user);
                await db.SaveChangesAsync(ct);
            }

            return Results.NoContent();
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        // Geri alma: silme kaydı geçmiş olarak kalır, RestoredAt/By yazılır. Silinmemiş ilanda idempotent 204.
        g.MapPost("/listings/{id:int}/restore", async (int id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var listing = await db.Listings.SingleOrDefaultAsync(l => l.Id == id, ct);
            if (listing is null) { return Results.NotFound(); }

            if (listing.IsDeleted)
            {
                listing.IsDeleted = false;
                listing.RestoredAt = DateTime.UtcNow;
                listing.RestoredBy = UserName(user);
                await db.SaveChangesAsync(ct);
            }

            return Results.NoContent();
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        g.MapPost("/tags", async (CreateTagRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var name = req.Name?.Trim() ?? "";
            if (name.Length == 0 || name.Length > MaxTagName)
            {
                return BadRequest([$"Etiket adı boş olamaz ve en çok {MaxTagName} karakter olabilir."]);
            }

            if (req.Group is not { } group || !Enum.IsDefined(group))
            {
                return BadRequest(["Etiket grubu geçersiz."]);
            }

            var lower = name.ToLowerInvariant();
            if (await db.FeatureTags.AnyAsync(t => t.Group == group && t.Name.ToLower() == lower, ct)) // SQL lower() olarak çevrilir; kültürden etkilenmez
            {
                return Results.Conflict(new { error = "Bu grupta aynı adlı etiket zaten var." });
            }

            var tag = new FeatureTag { Name = name, Group = group };
            db.FeatureTags.Add(tag);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/tags/{tag.Id}", PublicMapper.ToTag(tag));
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        return app;
    }

    private static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(nameof(UserRole.Admin));

    private static string UserName(ClaimsPrincipal user) => user.Identity?.Name ?? "";

    private static IResult BadRequest(string[] errors) => Results.BadRequest(new { errors });

    private static Task<Listing?> LoadAsync(IQueryable<Listing> src, int id, bool includeOwner, CancellationToken ct)
    {
        // Ev sahibi yalnızca yönetici için yüklenir (Saha cevabı onu hiç kullanmaz).
        var query = includeOwner ? src.Include(l => l.Owner) : src;
        return query.Include(l => l.Photos).Include(l => l.Tags).AsSplitQuery().SingleOrDefaultAsync(l => l.Id == id, ct);
    }

    /// <summary>Verilen kimliklerin etiketleri; bilinmeyen kimlik varsa null.</summary>
    private static async Task<List<FeatureTag>?> ResolveTagsAsync(AppDbContext db, int[]? ids, CancellationToken ct)
    {
        var distinct = (ids ?? []).Distinct().ToArray();
        var tags = await db.FeatureTags.Where(t => distinct.Contains(t.Id)).ToListAsync(ct);
        return tags.Count == distinct.Length ? tags : null;
    }

    private static void Apply(Listing l, AdminListingInput i, List<FeatureTag> tags)
    {
        l.Title = i.Title!.Trim();
        l.Badge = string.IsNullOrWhiteSpace(i.Badge) ? null : i.Badge.Trim();
        l.Type = i.Type;
        l.Price = i.Price;
        l.Currency = i.Currency;
        l.Deposit = i.Deposit;
        l.Aidat = i.Aidat;
        l.City = i.City!.Trim();
        l.District = i.District!.Trim();
        l.Neighborhood = i.Neighborhood?.Trim() ?? "";
        l.Rooms = i.Rooms!.Trim();
        l.Bathrooms = i.Bathrooms;
        l.GrossM2 = i.GrossM2;
        l.NetM2 = i.NetM2;
        l.Floor = i.Floor;
        l.TotalFloors = i.TotalFloors;
        l.BuildingAge = i.BuildingAge;
        l.Heating = i.Heating;
        l.Furnishing = i.Furnishing;
        l.Usage = i.Usage;
        l.Deed = i.Deed;
        l.Kitchen = i.Kitchen;
        l.Balcony = i.Balcony;
        l.Elevator = i.Elevator;
        l.Parking = i.Parking;
        l.InSite = i.InSite;
        l.LoanEligible = i.LoanEligible;
        l.Swap = i.Swap;
        l.Description = i.Description ?? "";

        l.Owner.Name = i.OwnerName!.Trim();
        l.Owner.Phone = i.OwnerPhone!.Trim();
        l.Owner.Note = string.IsNullOrWhiteSpace(i.OwnerNote) ? null : i.OwnerNote.Trim();

        l.Tags.Clear();
        l.Tags.AddRange(tags);
    }
}
