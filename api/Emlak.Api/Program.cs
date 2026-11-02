using Emlak.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppDatabase(builder.Configuration);

var app = builder.Build();

app.Services.MigrateDatabase();

app.MapGet("/health", () => "ok");

app.Run();

public partial class Program { }
