using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Listings;
using Emlak.Api.Photos;
using Emlak.Api.Rates;
using Emlak.Api.Showings;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddAppAuth(builder.Configuration);
builder.Services.AddListingSearch();
builder.Services.AddSingleton<PhotoStorage>();

var app = builder.Build();

app.Services.MigrateDatabase();
await app.Services.SeedUsersAsync();
await app.Services.SeedSampleDataAsync();

// Fotoğraflar Photos:Path klasöründen /photos altında sunulur; dizin listeleme açılmaz, tür koklama kapatılır.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(app.Services.GetRequiredService<PhotoStorage>().Root),
    RequestPath = "/photos",
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.XContentTypeOptions = "nosniff";
        ctx.Context.Response.Headers.CacheControl = "public, max-age=86400";
    },
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => "ok");
app.MapAuthEndpoints();
app.MapPublicListingEndpoints();
app.MapAdminListingEndpoints();
app.MapPhotoEndpoints();
app.MapShowingEndpoints();
app.MapRateEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapTestAuthEndpoints();
}

app.Run();

public partial class Program { }
