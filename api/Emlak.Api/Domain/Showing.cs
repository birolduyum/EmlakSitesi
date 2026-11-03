namespace Emlak.Api.Domain;

public class Showing
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string Username { get; set; } = "";
    public DateTimeOffset TakenAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    public string? Note { get; set; }
}
