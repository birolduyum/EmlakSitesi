namespace Emlak.Api.Domain;

public class Showing
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string Username { get; set; } = "";
    public DateTime TakenAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? Note { get; set; }
}
