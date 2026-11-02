using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Rates;

public static class RateEndpoints
{
    private const decimal MaxRate = 1_000_000m;

    public static IEndpointRouteBuilder MapRateEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/rates");

        g.MapGet("", async (AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.CurrencyRates.AsNoTracking()
                .Where(r => r.Currency == Currency.USD || r.Currency == Currency.EUR)
                .OrderBy(r => r.Currency)
                .ToListAsync(ct);
            return Results.Ok(rows.Select(r => new RateDto(r.Currency, r.TryPerUnit, r.UpdatedAt, r.UpdatedBy)).ToList());
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        // İkisi de (0, 1.000.000] aralığında olmalı; aksi halde hiçbir şey yazılmaz. Tohum kayıtları her zaman vardır, yoksa oluşturulur.
        g.MapPut("", async (UpdateRatesRequest req, HttpContext http, AppDbContext db, CancellationToken ct) =>
        {
            var errors = new List<string>();
            Check(req.Usd, "USD", errors);
            Check(req.Eur, "EUR", errors);
            if (errors.Count > 0) { return Results.BadRequest(new { errors }); }

            var user = http.User.Identity?.Name;
            if (string.IsNullOrEmpty(user)) { return Results.Unauthorized(); }

            var now = DateTime.UtcNow;
            var rows = await db.CurrencyRates.ToDictionaryAsync(r => r.Currency, ct);
            Upsert(db, rows, Currency.USD, req.Usd!.Value, now, user);
            Upsert(db, rows, Currency.EUR, req.Eur!.Value, now, user);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        return app;
    }

    private static void Check(decimal? value, string name, List<string> errors)
    {
        if (value is not { } v || v <= 0 || v > MaxRate)
        {
            errors.Add($"{name} kuru 0'dan büyük ve en çok 1.000.000 olmalıdır.");
        }
    }

    private static void Upsert(AppDbContext db, Dictionary<Currency, CurrencyRate> rows, Currency c, decimal value, DateTime now, string user)
    {
        if (rows.TryGetValue(c, out var row))
        {
            row.TryPerUnit = value;
            row.UpdatedAt = now;
            row.UpdatedBy = user;
        }
        else
        {
            db.CurrencyRates.Add(new CurrencyRate { Currency = c, TryPerUnit = value, UpdatedAt = now, UpdatedBy = user });
        }
    }
}
