namespace LearnForge.Core;

// Mock protection: a pack with reserveFixedPapers keeps every question family used by a fixed paper out of practice,
// so an unopened paper stays fully fresh. A family returns to practice once the learner has seen it.
public static class PaperReservation
{
    public static HashSet<string> HiddenFamilies(Pack pack, IReadOnlySet<string> seenFamilies)
    {
        if (!pack.ReserveFixedPapers) return [];
        var paperIds = pack.Blueprints.Where(b => b.QuestionIds is not null).SelectMany(b => b.QuestionIds!).ToHashSet();
        return pack.Questions.Where(q => paperIds.Contains(q.Id) && !seenFamilies.Contains(q.FamilyId)).Select(q => q.FamilyId).ToHashSet();
    }
}
