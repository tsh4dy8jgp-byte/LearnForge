using Microsoft.AspNetCore.Identity;

namespace LearnForge.Api;

public sealed class User : IdentityUser
{
    public string DisplayName { get; set; } = "Learner";
}
