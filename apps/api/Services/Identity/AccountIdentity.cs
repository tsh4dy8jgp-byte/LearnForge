using System.Security.Claims;
namespace LearnForge.Api.Services.Identity;
public static class AccountIdentity
{
    public static string UserId(ClaimsPrincipal p) => p.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
