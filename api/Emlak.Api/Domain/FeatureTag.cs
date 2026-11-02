namespace Emlak.Api.Domain;

public class FeatureTag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TagGroup Group { get; set; }
}
