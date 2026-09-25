namespace LearnForge.Api.Contracts.Auth;

public sealed record CurrentUserResponse(string Id, string DisplayName, string Email, bool Publisher);
