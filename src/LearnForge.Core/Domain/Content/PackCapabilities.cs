namespace LearnForge.Core;

// These are supported delivery capabilities, not permissions or future feature flags.
public sealed record PackCapabilities(bool Lessons, bool Practice, bool Assessments)
{
    public static PackCapabilities For(ProductProfile profile) => profile switch
    {
        ProductProfile.Course => new(true, false, false),
        ProductProfile.Exam => new(false, true, true),
        _ => new(true, true, true)
    };
}
