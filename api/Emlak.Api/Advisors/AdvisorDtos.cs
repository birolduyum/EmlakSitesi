using Emlak.Api.Domain;

namespace Emlak.Api.Advisors;

/// <summary>Yönetici ekranı için danışman; ziyaretçiye giden <c>PublicAdvisor</c> değildir (denetim alanları ve ilan sayısı içerir).</summary>
public record AdminAdvisorDto(
    int Id, string Name, string Phone, string? WhatsApp, bool IsActive, int ListingCount,
    DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset? UpdatedAt, string? UpdatedBy);

public record AdvisorInput(string? Name, string? Phone, string? WhatsApp);

public static class AdvisorMapper
{
    public static AdminAdvisorDto ToDto(Advisor a, int listingCount) => new(
        a.Id, a.Name, a.Phone, a.WhatsApp, a.IsActive, listingCount, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy);
}
