namespace Emlak.Api.Domain;

public class Owner
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Note { get; set; }
}
