using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Emlak.Api.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Emlak.Api.Tests;

public class PhotoTests : IClassFixture<ApiFactory>
{
    private const int TenMb = 10 * 1024 * 1024;

    private readonly ApiFactory _factory;

    public PhotoTests(ApiFactory factory) => _factory = factory;

    private async Task ResetAsync()
    {
        _ = _factory.Server;
        await _factory.ResetAsync();
        // Önceki testlerden kalan dosyalar sonraki testi etkilemesin.
        foreach (var dir in Directory.GetDirectories(_factory.PhotoRoot))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<Listing> SeedAsync(ListingBuilder b)
    {
        var l = b.Build();
        await WithDbAsync(async db => { db.Listings.Add(l); await db.SaveChangesAsync(); return 0; });
        return l;
    }

    private static byte[] Jpeg(int size = 64, byte fill = 1)
    {
        var b = new byte[size];
        Array.Fill(b, fill);
        b[0] = 0xFF; b[1] = 0xD8; b[2] = 0xFF; b[3] = 0xE0;
        return b;
    }

    private static byte[] Png(int size = 64)
    {
        var b = new byte[size];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0);
        return b;
    }

    private static byte[] WebP(int size = 64)
    {
        var b = new byte[size];
        "RIFF"u8.ToArray().CopyTo(b, 0);
        "WEBP"u8.ToArray().CopyTo(b, 8);
        return b;
    }

    private static MultipartFormDataContent Form(params (string FileName, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, bytes) in files)
        {
            var part = new ByteArrayContent(bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(part, "files", name);
        }
        return form;
    }

    private static async Task<List<int>> PhotoIdsAsync(HttpResponseMessage res)
    {
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        return json.EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).ToList();
    }

    private Task<List<Photo>> DbPhotosAsync(int listingId)
        => WithDbAsync(db => db.Photos.AsNoTracking().Where(p => p.ListingId == listingId).OrderBy(p => p.SortOrder).ToListAsync());

    private async Task<string[]> PublicUrlsAsync(HttpClient client, int listingId)
    {
        var json = await client.GetFromJsonAsync<JsonElement>($"/api/listings/{listingId}");
        return json.GetProperty("photoUrls").EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    [Fact]
    public async Task Upload_ValidJpeg_AppearsInPublicDetailInOrder()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");

        var first = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("a.jpg", Jpeg()), ("b.png", Png())));
        var second = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("c.webp", WebP())));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var urls = await PublicUrlsAsync(_factory.CreateClient(), l.Id);
        Assert.Equal(3, urls.Length);
        Assert.EndsWith(".jpg", urls[0]);
        Assert.EndsWith(".png", urls[1]);
        Assert.EndsWith(".webp", urls[2]);
        Assert.All(urls, u => Assert.StartsWith($"/photos/{l.Id}/", u));
        var photos = await DbPhotosAsync(l.Id);
        Assert.Equal(new[] { 0, 1, 2 }, photos.Select(p => p.SortOrder).ToArray());
        Assert.All(photos, p => Assert.True(File.Exists(Path.Combine(_factory.PhotoRoot, p.FilePath))));
    }

    [Fact]
    public async Task Upload_TextFileRenamedToJpg_Returns400()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos",
            Form(("fake.jpg", "bu bir metin dosyasıdır, resim değil"u8.ToArray())));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("errors", await res.Content.ReadAsStringAsync());
        Assert.Empty(await DbPhotosAsync(l.Id));
        Assert.False(Directory.Exists(Path.Combine(_factory.PhotoRoot, l.Id.ToString())) &&
                     Directory.GetFiles(Path.Combine(_factory.PhotoRoot, l.Id.ToString())).Length > 0);
    }

    [Fact]
    public async Task Upload_Over10MB_Returns400()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("big.jpg", Jpeg(TenMb + 1))));
        var exact = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("max.jpg", Jpeg(TenMb))));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("errors", await res.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, exact.StatusCode);
        Assert.Single(await DbPhotosAsync(l.Id));
    }

    [Fact]
    public async Task Upload_41stPhoto_Returns400AndListingUnchanged()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder().WithPhotos(Enumerable.Range(0, 40).Select(i => $"{i}/seed.jpg").ToArray()));
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("x.jpg", Jpeg())));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("errors", await res.Content.ReadAsStringAsync());
        Assert.Equal(40, (await DbPhotosAsync(l.Id)).Count);
    }

    [Fact]
    public async Task Reorder_ChangesPublicOrder()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");
        var up = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("a.jpg", Jpeg()), ("b.png", Png()), ("c.webp", WebP())));
        var ids = await PhotoIdsAsync(up);
        var before = await PublicUrlsAsync(_factory.CreateClient(), l.Id);

        var res = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}/photos/order", new[] { ids[2], ids[0], ids[1] });
        var after = await PublicUrlsAsync(_factory.CreateClient(), l.Id);
        var missing = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}/photos/order", new[] { ids[2], ids[0] });
        var dup = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}/photos/order", new[] { ids[2], ids[0], ids[0] });
        var foreign = await admin.PutAsJsonAsync($"/api/admin/listings/{l.Id}/photos/order", new[] { ids[2], ids[0], 99999 });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(new[] { before[2], before[0], before[1] }, after);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, dup.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal(after, await PublicUrlsAsync(_factory.CreateClient(), l.Id));
    }

    [Fact]
    public async Task Delete_RemovesFileAndRecord()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");
        var up = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("a.jpg", Jpeg()), ("b.png", Png()), ("c.webp", WebP())));
        var ids = await PhotoIdsAsync(up);
        var photos = await DbPhotosAsync(l.Id);
        var middle = photos[1];
        var filePath = Path.Combine(_factory.PhotoRoot, middle.FilePath);
        Assert.True(File.Exists(filePath));

        var res = await admin.DeleteAsync($"/api/admin/listings/{l.Id}/photos/{middle.Id}");
        var again = await admin.DeleteAsync($"/api/admin/listings/{l.Id}/photos/{middle.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.False(File.Exists(filePath));
        var left = await DbPhotosAsync(l.Id);
        Assert.Equal(new[] { ids[0], ids[2] }, left.Select(p => p.Id).ToArray());
        Assert.Equal(new[] { 0, 1 }, left.Select(p => p.SortOrder).ToArray());
    }

    [Fact]
    public async Task Delete_WhenFileAlreadyMissing_StillSucceeds()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder().WithPhotos("9/yok.jpg"));
        var admin = await _factory.LoginAsync("admin1");
        var photo = (await DbPhotosAsync(l.Id)).Single();

        var res = await admin.DeleteAsync($"/api/admin/listings/{l.Id}/photos/{photo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Empty(await DbPhotosAsync(l.Id));
    }

    [Fact]
    public async Task Upload_AsField_Returns403()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var field = await _factory.LoginAsync("saha1");

        var upload = await field.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("a.jpg", Jpeg())));
        var order = await field.PutAsJsonAsync($"/api/admin/listings/{l.Id}/photos/order", new int[0]);
        var del = await field.DeleteAsync($"/api/admin/listings/{l.Id}/photos/1");
        var anon = await _factory.CreateClient().PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("a.jpg", Jpeg())));

        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, order.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, del.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
        Assert.Empty(await DbPhotosAsync(l.Id));
    }

    [Fact]
    public async Task Upload_PathTraversalFileName_IsIgnoredAndStoredUnderGeneratedName()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");
        var evilTarget = Path.GetFullPath(Path.Combine(_factory.PhotoRoot, "..", "..", "evil.jpg"));

        var res = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("../../evil.jpg", Jpeg())));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var photo = (await DbPhotosAsync(l.Id)).Single();
        Assert.Matches($@"^{l.Id}/[0-9a-f]{{32}}\.jpg$", photo.FilePath);
        var full = Path.GetFullPath(Path.Combine(_factory.PhotoRoot, photo.FilePath));
        Assert.StartsWith(Path.GetFullPath(_factory.PhotoRoot), full);
        Assert.True(File.Exists(full));
        Assert.False(File.Exists(evilTarget));
    }

    [Fact]
    public async Task Upload_OneBadFileOfTwo_SavesNothing()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");

        var res = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos",
            Form(("good.jpg", Jpeg()), ("bad.jpg", "metin"u8.ToArray())));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Empty(await DbPhotosAsync(l.Id));
        var dir = Path.Combine(_factory.PhotoRoot, l.Id.ToString());
        Assert.True(!Directory.Exists(dir) || Directory.GetFiles(dir).Length == 0);
    }

    [Fact]
    public async Task Upload_NoFiles_Returns400()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");

        var emptyForm = await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", new MultipartFormDataContent { { new StringContent("x"), "note" } });
        var notForm = await admin.PostAsJsonAsync($"/api/admin/listings/{l.Id}/photos", new { });

        Assert.Equal(HttpStatusCode.BadRequest, emptyForm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notForm.StatusCode);
    }

    [Fact]
    public async Task Upload_ToDeletedListing_Returns404()
    {
        await ResetAsync();
        var deleted = await SeedAsync(new ListingBuilder().Deleted());
        var live = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");

        var toDeleted = await admin.PostAsync($"/api/admin/listings/{deleted.Id}/photos", Form(("a.jpg", Jpeg())));
        var toMissing = await admin.PostAsync("/api/admin/listings/99999/photos", Form(("a.jpg", Jpeg())));
        var orderDeleted = await admin.PutAsJsonAsync($"/api/admin/listings/{deleted.Id}/photos/order", new int[0]);
        var deleteDeleted = await admin.DeleteAsync($"/api/admin/listings/{deleted.Id}/photos/1");
        var wrongListing = await admin.DeleteAsync($"/api/admin/listings/{live.Id}/photos/1");

        Assert.Equal(HttpStatusCode.NotFound, toDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, toMissing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, orderDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteDeleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, wrongListing.StatusCode);
        Assert.Empty(await DbPhotosAsync(deleted.Id));
    }

    [Fact]
    public async Task Photos_AreServedWithNosniffAndNoDirectoryListing()
    {
        await ResetAsync();
        var l = await SeedAsync(new ListingBuilder());
        var admin = await _factory.LoginAsync("admin1");
        var bytes = Jpeg(100, 7);
        await admin.PostAsync($"/api/admin/listings/{l.Id}/photos", Form(("a.jpg", bytes)));
        var url = (await PublicUrlsAsync(_factory.CreateClient(), l.Id)).Single();
        var anon = _factory.CreateClient();

        var file = await anon.GetAsync(url);
        var root = await anon.GetAsync("/photos/");
        var dir = await anon.GetAsync($"/photos/{l.Id}/");
        var dirNoSlash = await anon.GetAsync($"/photos/{l.Id}");

        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(bytes, await file.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", file.Content.Headers.ContentType?.MediaType);
        Assert.Contains("nosniff", file.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, dir.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, dirNoSlash.StatusCode);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "webp")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x41, 0x56, 0x45 }, null)] // WAV
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, null)] // kısa PNG
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, null)] // GIF
    [InlineData(new byte[0], null)]
    public void PhotoValidator_DetectsTypeFromMagicBytes(byte[] header, string? expectedExt)
    {
        Assert.Equal(expectedExt, PhotoValidator.DetectExtension(header));
    }
}
