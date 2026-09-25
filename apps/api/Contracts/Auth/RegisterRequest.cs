using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record RegisterRequest(
    [property: Required, EmailAddress, MaxLength(254)] string Email,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password,
    [property: Required, StringLength(80, MinimumLength = 1)] string DisplayName);
