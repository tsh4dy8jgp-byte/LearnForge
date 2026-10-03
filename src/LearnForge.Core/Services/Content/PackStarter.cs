namespace LearnForge.Core;

public static class PackStarter
{
    public static Pack Create(ProductProfile profile)
    {
        var features = PackCapabilities.For(profile);
        var question = new Question("starter-q", "starter-family", QuestionKind.Single, "Which value is even?", ["parity"],
            [new("a", "2"), new("b", "3")], [], 1, false, new(ScoringPolicy.Exact, ["a"]), "An even integer is divisible by two.");
        return new(1, "my-" + profile.ToString().ToLowerInvariant(), "1.0.0", "My first " + profile.ToString().ToLowerInvariant(),
            "Replace this original demonstration with your subject. Expand the bank before using readiness.", "CC0-1.0",
            [new("parity", "Recognize even integers", [])],
            features.Lessons ? [new("introduction", "Even integers", "A short introduction.", ["parity"],
                [new(ContentBlockKind.Text, "An integer is even when it is divisible by two.")])] : [],
            features.Practice || features.Assessments ? [question] : [], [],
            features.Practice || features.Assessments ? [new("short", "Quick check", 1, 5, AssessmentSize.Short, ["parity"], [QuestionKind.Single])] : [], [],
            Goal: profile == ProductProfile.Course ? CourseGoal.Completion : CourseGoal.Readiness,
            Profile: profile, Capabilities: features);
    }
}
