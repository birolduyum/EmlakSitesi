namespace Emlak.Api.Domain;

public class CurrencyRate
{
    public Currency Currency { get; set; }
    public decimal TryPerUnit { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "";
}
