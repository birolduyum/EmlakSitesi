using System.Globalization;
using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Showings;

public static class ShowingEndpoints
{
    private const int MaxNote = 500;
    private const int MaxReportRows = 500;

    public static IEndpointRouteBuilder MapShowingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/showings");

        // "Anahtarı aldım": kayıt açar. Silinmiş/olmayan ilan 404; aynı kullanıcının aynı ilanda açık kaydı varsa 409.
        g.MapPost("", async (TakeShowingRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (UserName(http) is not { } user) { return Results.Unauthorized(); }
            if (req.ListingId is not { } listingId) { return Results.BadRequest(new { errors = new[] { "İlan seçilmedi." } }); }

            var listing = await db.Listings.AsNoTracking()
                .Where(l => l.Id == listingId && !l.IsDeleted)
                .Select(l => new { l.Id, l.Title })
                .SingleOrDefaultAsync(ct);
            if (listing is null) { return Results.NotFound(); }

            if (await db.Showings.AnyAsync(s => s.ListingId == listingId && s.Username == user && s.ReturnedAt == null, ct))
            {
                return Results.Conflict(new { error = "Bu ilan için zaten açık bir gösterme kaydınız var." });
            }

            var showing = new Showing { ListingId = listingId, Username = user, TakenAt = DateTime.UtcNow };
            db.Showings.Add(showing);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/showings/{showing.Id}", ToDto(showing, listing.Title));
        }).RequireAuthorization(AuthExtensions.Staff);

        // "Anahtarı getirdim": yalnızca kaydı açan kullanıcı ya da Admin kapatır.
        g.MapPost("/{id:int}/close", async (int id, CloseShowingRequest? req, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (UserName(http) is not { } user) { return Results.Unauthorized(); }

            var note = string.IsNullOrWhiteSpace(req?.Note) ? null : req.Note.Trim();
            if (note is { Length: > MaxNote })
            {
                return Results.BadRequest(new { errors = new[] { $"Not en çok {MaxNote} karakter olabilir." } });
            }

            var showing = await db.Showings.SingleOrDefaultAsync(s => s.Id == id, ct);
            if (showing is null) { return Results.NotFound(); }
            if (showing.Username != user && !http.User.IsInRole(nameof(UserRole.Admin))) { return Results.Forbid(); }
            if (showing.ReturnedAt is not null) { return Results.Conflict(new { error = "Bu kayıt zaten kapatılmış." }); }

            showing.ReturnedAt = DateTime.UtcNow;
            showing.Note = note;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthExtensions.Staff);

        g.MapGet("/mine", async (HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            if (UserName(http) is not { } user) { return Results.Unauthorized(); }

            var rows = await Query(db)
                .Where(r => r.Showing.Username == user && r.Showing.ReturnedAt == null)
                .OrderByDescending(r => r.Showing.TakenAt).ThenByDescending(r => r.Showing.Id)
                .ToListAsync(ct);
            return Results.Ok(rows.Select(r => ToDto(r.Showing, r.Title)).ToList());
        }).RequireAuthorization(AuthExtensions.Staff);

        // Rapor: silinmiş ilanların kayıtları da (geçmiş) başlığıyla gelir. from/to TakenAt'e dahil sınırdır.
        app.MapGet("/api/admin/showings", async (int? listingId, string? username, string? from, string? to, AppDbContext db, CancellationToken ct) =>
        {
            if (!TryParseBound(from, endOfDay: false, out var fromUtc)) { return BadBound("from"); }
            if (!TryParseBound(to, endOfDay: true, out var toUtc)) { return BadBound("to"); }

            var q = Query(db);
            if (listingId is { } lid) { q = q.Where(r => r.Showing.ListingId == lid); }
            if (!string.IsNullOrWhiteSpace(username))
            {
                var u = username.Trim().ToLower();
                q = q.Where(r => r.Showing.Username.ToLower() == u);
            }
            if (fromUtc is { } f) { q = q.Where(r => r.Showing.TakenAt >= f); }
            if (toUtc is { } t) { q = q.Where(r => r.Showing.TakenAt < t); }

            var rows = await q.OrderByDescending(r => r.Showing.TakenAt).ThenByDescending(r => r.Showing.Id)
                .Take(MaxReportRows).ToListAsync(ct);
            return Results.Ok(rows.Select(r => ToDto(r.Showing, r.Title)).ToList());
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        return app;
    }

    private sealed class Row
    {
        public required Showing Showing { get; init; }
        public required string Title { get; init; }
    }

    // Listing üzerinde global filtre yok: silinmiş ilanın başlığı da gelir.
    private static IQueryable<Row> Query(AppDbContext db)
        => db.Showings.AsNoTracking().Join(db.Listings.AsNoTracking(), s => s.ListingId, l => l.Id, (s, l) => new Row { Showing = s, Title = l.Title });

    private static ShowingDto ToDto(Showing s, string title)
        => new(s.Id, s.ListingId, title, s.Username, s.TakenAt, s.ReturnedAt, s.Note);

    private static string? UserName(HttpContext http)
        => string.IsNullOrWhiteSpace(http.User.Identity?.Name) ? null : http.User.Identity!.Name;

    private static IResult BadBound(string name)
        => Results.BadRequest(new { errors = new[] { $"'{name}' geçerli bir tarih değil (örn. 2026-11-02 ya da 2026-11-02T10:30:00Z)." } });

    /// <summary>
    /// Boş değer = sınır yok. Yalnızca tarih (yyyy-MM-dd) verilirse UTC gün başı; endOfDay ise ertesi günün başı (dışlayıcı üst sınır,
    /// yani o günün tamamı dahil). Saatli değer verilirse olduğu gibi kullanılır; üst sınırda tam eşleşme dahildir (+1 mikrosaniye; PostgreSQL mikrosaniye hassasiyetindedir).
    /// </summary>
    private static bool TryParseBound(string? raw, bool endOfDay, out DateTime? utc)
    {
        utc = null;
        if (string.IsNullOrWhiteSpace(raw)) { return true; }

        var s = raw.Trim();
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            var start = DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            utc = endOfDay ? start.AddDays(1) : start;
            return true;
        }

        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            var t = dto.UtcDateTime;
            utc = endOfDay ? t.AddTicks(TimeSpan.TicksPerMicrosecond) : t;
            return true;
        }

        return false;
    }
}
