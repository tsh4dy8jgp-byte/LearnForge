using System.Security.Cryptography;
using System.Text;

namespace LearnForge.Core;

public static class ExamComposer
{
    // Bounded backtracking keeps scenario sections atomic and fails explicitly on unsatisfied constraints.
    public static Question[] Compose(Pack pack, Blueprint blueprint, HashSet<string> seenFamilies, string seed)
    {
        var groups = pack.Questions.GroupBy(q => q.ScenarioId is { } id ? "case:" + id : "item:" + q.Id)
            .Select(g => g.ToArray()).Where(g => g.All(q => q.ObjectiveIds.Any(blueprint.ObjectiveIds.Contains)))
            .Where(g => g.Select(q => q.FamilyId).Distinct().Count() == g.Length)
            .OrderBy(g => g.Count(q => seenFamilies.Contains(q.FamilyId)))
            .ThenBy(g => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed + g[0].Id))))
            .ToArray();
        var chosen = new List<Question>();
        var steps = 0;
        bool Search(int start)
        {
            if (++steps > 100_000) return false;
            if (chosen.Count == blueprint.Count) return blueprint.ObjectiveIds.All(id => chosen.Any(q => q.ObjectiveIds.Contains(id)))
                && blueprint.RequiredKinds.All(kind => chosen.Any(q => q.Kind == kind));
            if (chosen.Count > blueprint.Count || groups.Skip(start).Sum(g => g.Length) + chosen.Count < blueprint.Count) return false;
            for (var i = start; i < groups.Length; i++)
            {
                var group = groups[i];
                if (chosen.Count + group.Length > blueprint.Count || group.Any(q => chosen.Any(c => c.FamilyId == q.FamilyId))) continue;
                chosen.AddRange(group);
                if (Search(i + 1)) return true;
                chosen.RemoveRange(chosen.Count - group.Length, group.Length);
            }
            return false;
        }
        if (!Search(0)) throw new InvalidOperationException("Cannot compose this blueprint within the selection budget. Add eligible items or simplify its count, objective, format or scenario constraints.");
        return chosen.ToArray();
    }
}
