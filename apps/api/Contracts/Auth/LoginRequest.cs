using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record LoginRequest([property: Required, MaxLength(254)] string Email, [property: Required, MaxLength(128)] string Password);
