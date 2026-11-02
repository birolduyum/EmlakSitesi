using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Photos;

public record PhotoDto(int Id, string Url, int SortOrder);

public static class PhotoEndpoints
{
    /// <summary>Tek istekte (40 × 10 MB'a yetecek, ama sınırsız olmayan) toplam gövde üst sınırı.</summary>
    private const long MaxRequestBytes = 100L * 1024 * 1024;

    public static IEndpointRouteBuilder MapPhotoEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/listings/{id:int}/photos");

        // Hepsi ya da hiçbiri: önce her şey doğrulanır, sonra dosyalar yazılır, sonra kayıt eklenir; kayıt başarısızsa dosyalar silinir.
        // 10 MB'ı aşan dosya çerçeve tarafından değil burada reddedilir (toplam sınır 100 MB, aşılırsa yine 400 döner).
        g.MapPost("", async (int id, HttpContext http, AppDbContext db, PhotoStorage storage, CancellationToken ct) =>
        {
            if (await ListingStateAsync(db, id, ct) is not false) { return Results.NotFound(); }

            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            {
                limit.MaxRequestBodySize = MaxRequestBytes;
            }

            if (!http.Request.HasFormContentType)
            {
                return BadRequest(["İstek çok parçalı (multipart/form-data) olmalıdır."]);
            }

            IFormFileCollection files;
            try
            {
                files = (await http.Request.ReadFormAsync(ct)).Files;
            }
            catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
            {
                return BadRequest(["İstek okunamadı veya çok büyük (en çok 100 MB)."]);
            }

            if (files.Count == 0) { return BadRequest(["Yüklenecek dosya seçilmedi."]); }

            var existing = await db.Photos.Where(p => p.ListingId == id).ToListAsync(ct);
            if (existing.Count + files.Count > PhotoValidator.MaxPhotosPerListing)
            {
                return BadRequest([$"Bir ilana en çok {PhotoValidator.MaxPhotosPerListing} fotoğraf eklenebilir (şu an {existing.Count}, eklenmek istenen {files.Count})."]);
            }

            var errors = new List<string>();
            foreach (var file in files)
            {
                if (await PhotoValidator.ValidateAsync(file, ct) is { } error) { errors.Add(error); }
            }

            if (errors.Count > 0) { return BadRequest(errors.ToArray()); }

            var saved = new List<string>();
            try
            {
                var next = existing.Count == 0 ? 0 : existing.Max(p => p.SortOrder) + 1;
                var added = new List<Photo>();
                foreach (var file in files)
                {
                    var path = await storage.SaveAsync(id, file, ct);
                    saved.Add(path);
                    added.Add(new Photo { ListingId = id, FilePath = path, SortOrder = next++ });
                }

                db.Photos.AddRange(added);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/admin/listings/{id}", added.Select(ToDto).ToArray());
            }
            catch
            {
                foreach (var path in saved) { storage.Delete(path); }
                throw;
            }
        }).RequireAuthorization(AuthExtensions.AdminOnly).DisableAntiforgery();

        // Fotoğraf için kalıcı silme serbesttir (yumuşak silme yalnızca ilan içindir). Önce kayıt, sonra dosya; dosya yoksa hata değildir.
        g.MapDelete("/{photoId:int}", async (int id, int photoId, AppDbContext db, PhotoStorage storage, CancellationToken ct) =>
        {
            if (await ListingStateAsync(db, id, ct) is not false) { return Results.NotFound(); }

            var photos = await db.Photos.Where(p => p.ListingId == id).ToListAsync(ct);
            var target = photos.SingleOrDefault(p => p.Id == photoId);
            if (target is null) { return Results.NotFound(); }

            db.Photos.Remove(target);
            Renumber(photos.Where(p => p.Id != photoId));
            await db.SaveChangesAsync(ct);

            storage.Delete(target.FilePath);
            return Results.NoContent();
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        // Gövde, ilanın mevcut fotoğraf kimliklerinin tam kümesi olmalı (eksik/fazla/yinelenen kimlik 400). İlk fotoğraf kapaktır.
        g.MapPut("/order", async (int id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] int[]? photoIds, AppDbContext db, CancellationToken ct) =>
        {
            if (await ListingStateAsync(db, id, ct) is not false) { return Results.NotFound(); }

            var photos = await db.Photos.Where(p => p.ListingId == id).ToListAsync(ct);
            var wanted = photoIds ?? [];
            if (wanted.Length != photos.Count || wanted.Distinct().Count() != wanted.Length
                || !photos.Select(p => p.Id).ToHashSet().SetEquals(wanted))
            {
                return BadRequest(["Sıralama, ilanın tüm fotoğraf kimliklerini birer kez içermelidir."]);
            }

            var byId = photos.ToDictionary(p => p.Id);
            for (var i = 0; i < wanted.Length; i++) { byId[wanted[i]].SortOrder = i; }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        return app;
    }

    /// <summary>false: ilan var ve silinmemiş; true: yumuşak silinmiş; null: yok.</summary>
    private static Task<bool?> ListingStateAsync(AppDbContext db, int id, CancellationToken ct)
        => db.Listings.Where(l => l.Id == id).Select(l => (bool?)l.IsDeleted).SingleOrDefaultAsync(ct);

    private static void Renumber(IEnumerable<Photo> photos)
    {
        var i = 0;
        foreach (var p in photos.OrderBy(p => p.SortOrder).ThenBy(p => p.Id)) { p.SortOrder = i++; }
    }

    private static PhotoDto ToDto(Photo p) => new(p.Id, "/photos/" + p.FilePath, p.SortOrder);

    private static IResult BadRequest(string[] errors) => Results.BadRequest(new { errors });
}
