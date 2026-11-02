using Emlak.Api.Auth;
using Emlak.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);
builder.Services.AddAppAuth(builder.Configuration);

var app = builder.Build();

app.Services.MigrateDatabase();
await app.Services.SeedUsersAsync();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => "ok");
app.MapAuthEndpoints();

if (app.Environment.IsEnvironment("Testing"))
{
    app.MapTestAuthEndpoints();
}

app.Run();

public partial class Program { }
