using Emlak.Api.Domain;

namespace Emlak.Api.Rates;

public record RateDto(Currency Currency, decimal TryPerUnit, DateTimeOffset UpdatedAt, string UpdatedBy);

public record UpdateRatesRequest(decimal? Usd, decimal? Eur);
