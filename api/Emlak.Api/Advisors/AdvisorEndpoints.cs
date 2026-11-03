using System.Security.Claims;
using Emlak.Api.Auth;
using Emlak.Api.Data;
using Emlak.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Emlak.Api.Advisors;

/// <summary>Danışman yönetimi (yalnızca Yönetici). Kalıcı silme yoktur: ilanlar danışmana bağlıdır, yalnızca pasifleştirilir.</summary>
public static class AdvisorEndpoints
{
    public static IEndpointRouteBuilder MapAdvisorEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/advisors");

        g.MapGet("", async (AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.Advisors.AsNoTracking()
                .OrderBy(a => a.Name).ThenBy(a => a.Id)
                .Select(a => new { Advisor = a, Count = db.Listings.Count(l => l.AdvisorId == a.Id && !l.IsDeleted) })
                .ToListAsync(ct);
            return Results.Ok(rows.Select(r => AdvisorMapper.ToDto(r.Advisor, r.Count)).ToList());
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var advisor = await db.Advisors.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
            return advisor is null ? Results.NotFound() : Results.Ok(AdvisorMapper.ToDto(advisor, await CountAsync(db, id, ct)));
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        g.MapPost("", async (AdvisorInput input, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var errors = AdvisorValidator.Validate(input);
            if (errors.Length > 0) { return BadRequest(errors); }

            var advisor = new Advisor { CreatedAt = DateTimeOffset.UtcNow, CreatedBy = UserName(user), IsActive = true };
            Apply(advisor, input);
            db.Advisors.Add(advisor);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/advisors/{advisor.Id}", AdvisorMapper.ToDto(advisor, 0));
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        g.MapPut("/{id:int}", async (int id, AdvisorInput input, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var errors = AdvisorValidator.Validate(input);
            if (errors.Length > 0) { return BadRequest(errors); }

            var advisor = await db.Advisors.SingleOrDefaultAsync(a => a.Id == id, ct);
            if (advisor is null) { return Results.NotFound(); }

            Apply(advisor, input);
            Touch(advisor, user);
            await db.SaveChangesAsync(ct);
            return Results.Ok(AdvisorMapper.ToDto(advisor, await CountAsync(db, id, ct)));
        }).RequireAuthorization(AuthExtensions.AdminOnly);

        g.MapPost("/{id:int}/deactivate", (int id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => SetActiveAsync(id, false, user, db, ct))
            .RequireAuthorization(AuthExtensions.AdminOnly);
        g.MapPost("/{id:int}/activate", (int id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => SetActiveAsync(id, true, user, db, ct))
            .RequireAuthorization(AuthExtensions.AdminOnly);

        return app;
    }

    // Durum zaten istenen gibiyse idempotent 204 (kayıt değişmez). Mevcut ilanlardan kaldırılmaz; yalnızca yeni atamayı kapatır.
    private static async Task<IResult> SetActiveAsync(int id, bool active, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var advisor = await db.Advisors.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (advisor is null) { return Results.NotFound(); }

        if (advisor.IsActive != active)
        {
            advisor.IsActive = active;
            Touch(advisor, user);
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    private static Task<int> CountAsync(AppDbContext db, int id, CancellationToken ct)
        => db.Listings.CountAsync(l => l.AdvisorId == id && !l.IsDeleted, ct);

    private static void Apply(Advisor a, AdvisorInput i)
    {
        a.Name = i.Name!.Trim();
        a.Phone = i.Phone!.Trim();
        a.WhatsApp = string.IsNullOrWhiteSpace(i.WhatsApp) ? null : i.WhatsApp.Trim();
    }

    private static void Touch(Advisor a, ClaimsPrincipal user)
    {
        a.UpdatedAt = DateTimeOffset.UtcNow;
        a.UpdatedBy = UserName(user);
    }

    private static string UserName(ClaimsPrincipal user) => user.Identity?.Name ?? "";

    private static IResult BadRequest(string[] errors) => Results.BadRequest(new { errors });
}
