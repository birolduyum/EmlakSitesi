using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Listings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddAppAuth(builder.Configuration);
builder.Services.AddListingSearch();

var app = builder.Build();

app.Services.MigrateDatabase();
await app.Services.SeedUsersAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => "ok");
app.MapAuthEndpoints();
app.MapPublicListingEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapTestAuthEndpoints();
}

app.Run();

public partial class Program { }
