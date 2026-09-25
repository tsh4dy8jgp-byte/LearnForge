using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record ChangePasswordRequest([property: Required, MaxLength(128)] string CurrentPassword, [property: Required, MaxLength(128)] string NewPassword);
