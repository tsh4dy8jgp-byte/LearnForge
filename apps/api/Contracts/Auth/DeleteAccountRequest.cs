using System.ComponentModel.DataAnnotations;

namespace LearnForge.Api.Contracts.Auth;

public sealed record DeleteAccountRequest([property: Required, MaxLength(128)] string Password);
