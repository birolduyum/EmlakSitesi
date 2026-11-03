namespace Emlak.Api.Showings;

public record ShowingDto(int Id, int ListingId, string ListingTitle, string Username, DateTimeOffset TakenAt, DateTimeOffset? ReturnedAt, string? Note);

public record TakeShowingRequest(int? ListingId);

public record CloseShowingRequest(string? Note);
