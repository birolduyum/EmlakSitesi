namespace Emlak.Api.Domain;

/// <summary>İlanda görünen danışman. İlanlar danışmana bağlı olduğu için silinmez, yalnızca pasifleştirilir.</summary>
public class Advisor
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? WhatsApp { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
