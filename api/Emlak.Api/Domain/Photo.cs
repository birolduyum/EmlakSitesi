namespace Emlak.Api.Domain;

public class Photo
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string FilePath { get; set; } = "";
    public int SortOrder { get; set; }
}
