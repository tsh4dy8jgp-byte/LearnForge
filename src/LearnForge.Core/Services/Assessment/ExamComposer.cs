using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace LearnForge.Core;

public static class ExamComposer
{
    // Bounded backtracking keeps scenario sections atomic and fails explicitly on unsatisfied constraints.
    // A weighted blueprint also keeps each objective within one question of its share of Count, counting every
    // question once under its primary objective: the first of its objectives that the blueprint includes.
    public static Question[] Compose(Pack pack, Blueprint blueprint, HashSet<string> seenFamilies, string seed)
    {
        if (blueprint.QuestionIds is { } ids)
        {
            if (blueprint.ObjectiveWeights is not null)
                throw new InvalidOperationException("Fixed papers cannot also specify objectiveWeights.");
            if (ids.Length != blueprint.Count || ids.Distinct().Count() != ids.Length)
                throw new InvalidOperationException("Fixed papers need exactly count distinct questionIds.");
            var bank = pack.Questions.ToDictionary(q => q.Id);
            if (ids.Any(id => !bank.ContainsKey(id)))
                throw new InvalidOperationException("Fixed paper references an unknown question.");
            var paper = ids.Select(id => bank[id]).ToArray();
            if (paper.Select(q => q.FamilyId).Distinct().Count() != paper.Length)
                throw new InvalidOperationException("Fixed papers cannot repeat a question family.");
            if (paper.Any(q => !q.ObjectiveIds.Any(blueprint.ObjectiveIds.Contains))
                || blueprint.ObjectiveIds.Any(id => !paper.Any(q => q.ObjectiveIds.Contains(id))))
                throw new InvalidOperationException("Fixed paper does not satisfy its objective filter and coverage.");
            if (blueprint.RequiredKinds.Any(kind => !paper.Any(q => q.Kind == kind)))
                throw new InvalidOperationException("Fixed paper is missing a required question kind.");
            var selected = ids.ToHashSet();
            var scenarios = paper.Where(q => q.ScenarioId is not null).Select(q => q.ScenarioId).ToHashSet();
            if (pack.Questions.Any(q => q.ScenarioId is not null && scenarios.Contains(q.ScenarioId) && !selected.Contains(q.Id)))
                throw new InvalidOperationException("Fixed papers must include complete scenario groups.");
            return paper;
        }
        var groups = pack.Questions.GroupBy(q => q.ScenarioId is { } id ? "case:" + id : "item:" + q.Id)
            .Select(g => g.ToArray()).Where(g => g.All(q => q.ObjectiveIds.Any(blueprint.ObjectiveIds.Contains)))
            .Where(g => g.Select(q => q.FamilyId).Distinct().Count() == g.Length)
            .OrderBy(g => g.Count(q => seenFamilies.Contains(q.FamilyId)))
            .ThenBy(g => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed + g[0].Id))))
            .ToArray();
        var index = new Dictionary<string, int>();
        foreach (var id in blueprint.ObjectiveIds) index.TryAdd(id, index.Count);
        var objectives = index.Count;

        // Per-group facts, computed once: covered objectives per question, kinds, primary-objective tallies and suffix totals.
        var covers = groups.Select(g => g.Select(q => q.ObjectiveIds.Where(index.ContainsKey).Select(id => index[id]).Distinct().ToArray()).ToArray()).ToArray();
        var kinds = groups.Select(g => g.Aggregate(0, (mask, q) => mask | Bit(q.Kind))).ToArray();
        var primaries = groups.Select(g => g.Select(q => index[q.ObjectiveIds.First(index.ContainsKey)]).ToArray()).ToArray();
        var lengthSuffix = new int[groups.Length + 1];
        var kindSuffix = new int[groups.Length + 1];
        for (var g = groups.Length - 1; g >= 0; g--)
        {
            lengthSuffix[g] = lengthSuffix[g + 1] + groups[g].Length;
            kindSuffix[g] = kindSuffix[g + 1] | kinds[g];
        }
        var required = blueprint.RequiredKinds.Aggregate(0, (mask, kind) => mask | Bit(kind));
        // Each remaining slot can cover at most this many blueprint objectives, so a branch whose uncovered objectives
        // exceed that capacity can never succeed. Pruning it never changes which selection the search returns.
        var coverPerSlot = Math.Max(1, groups.SelectMany(g => g).Select(q => q.ObjectiveIds.Count(blueprint.ObjectiveIds.Contains)).DefaultIfEmpty(1).Max());

        var weighted = blueprint.ObjectiveWeights is not null;
        int[] low = [], high = [], primaryCount = new int[objectives];
        int[][] standaloneSuffix = [];
        int[] caseGroups = [];
        var deficit = 0;
        if (weighted)
        {
            (low, high) = Quotas(blueprint, index);
            standaloneSuffix = new int[groups.Length + 1][];
            standaloneSuffix[groups.Length] = new int[objectives];
            for (var g = groups.Length - 1; g >= 0; g--)
            {
                standaloneSuffix[g] = (int[])standaloneSuffix[g + 1].Clone();
                if (groups[g].Length == 1) standaloneSuffix[g][primaries[g][0]]++;
            }
            caseGroups = Enumerable.Range(0, groups.Length).Where(g => groups[g].Length > 1).ToArray();
            if (low.Sum() > blueprint.Count)
                throw new InvalidOperationException($"Blueprint '{blueprint.Id}' cannot give every weighted objective a question. Raise its count or combine objectives.");
            foreach (var (id, i) in index)
            {
                var eligible = standaloneSuffix[0][i] + caseGroups.Sum(g => primaries[g].Count(p => p == i));
                if (eligible < low[i])
                    throw new InvalidOperationException($"Blueprint '{blueprint.Id}' needs at least {low[i]} questions whose first objective is '{id}'; {eligible} are eligible.");
            }
            deficit = low.Sum();
        }

        var chosen = new List<Question>();
        var families = new HashSet<string>();
        var coverage = new int[objectives];
        var kindCount = new int[8];
        var steps = 0;
        int Uncovered() => blueprint.ObjectiveIds.Count(id => coverage[index[id]] == 0);
        int Missing()
        {
            var present = 0;
            for (var k = 0; k < kindCount.Length; k++) if (kindCount[k] > 0) present |= 1 << k;
            return required & ~present;
        }
        void Apply(int g, int sign)
        {
            for (var j = 0; j < groups[g].Length; j++)
            {
                var q = groups[g][j];
                if (sign > 0) { chosen.Add(q); families.Add(q.FamilyId); } else families.Remove(q.FamilyId);
                kindCount[(int)q.Kind] += sign;
                foreach (var o in covers[g][j]) coverage[o] += sign;
                if (!weighted) continue;
                var p = primaries[g][j];
                if (sign > 0 && primaryCount[p] < low[p]) deficit--;
                primaryCount[p] += sign;
                if (sign < 0 && primaryCount[p] < low[p]) deficit++;
            }
            if (sign < 0) chosen.RemoveRange(chosen.Count - groups[g].Length, groups[g].Length);
        }
        bool FitsQuotas(int g, int remainingAfter)
        {
            if (primaries[g].Length == 1)
            {
                var p = primaries[g][0];
                return primaryCount[p] < high[p] && deficit - (primaryCount[p] < low[p] ? 1 : 0) <= remainingAfter;
            }
            var added = new Dictionary<int, int>();
            foreach (var p in primaries[g]) added[p] = added.GetValueOrDefault(p) + 1;
            var relief = 0;
            foreach (var (p, n) in added)
            {
                if (primaryCount[p] + n > high[p]) return false;
                relief += Math.Min(n, Math.Max(0, low[p] - primaryCount[p]));
            }
            return deficit - relief <= remainingAfter;
        }
        // Necessary condition: every objective still short of its minimum has enough primary questions left to reach it.
        bool Reachable(int start, int remaining)
        {
            for (var i = 0; i < objectives; i++)
            {
                var need = low[i] - primaryCount[i];
                if (need <= 0) continue;
                var supply = standaloneSuffix[start][i];
                foreach (var g in caseGroups)
                    if (g >= start && groups[g].Length <= remaining) supply += primaries[g].Count(p => p == i);
                if (supply < need) return false;
            }
            return true;
        }
        bool Search(int start)
        {
            if (++steps > 100_000) return false;
            var remaining = blueprint.Count - chosen.Count;
            var uncovered = Uncovered();
            if (uncovered > remaining * coverPerSlot) return false;
            var missing = Missing();
            if (chosen.Count == blueprint.Count) return uncovered == 0 && missing == 0 && deficit == 0;
            if (chosen.Count > blueprint.Count || lengthSuffix[start] + chosen.Count < blueprint.Count) return false;
            // Each question has one kind, so missing kinds need as many questions, and each must still be available.
            if (BitOperations.PopCount((uint)missing) > remaining || (missing & ~kindSuffix[start]) != 0) return false;
            if (weighted && (deficit > remaining || !Reachable(start, remaining))) return false;
            for (var i = start; i < groups.Length; i++)
            {
                var group = groups[i];
                var remainingAfter = remaining - group.Length;
                if (remainingAfter < 0 || group.Any(q => families.Contains(q.FamilyId))) continue;
                if (BitOperations.PopCount((uint)(missing & ~kinds[i])) > remainingAfter) continue;
                if (weighted && !FitsQuotas(i, remainingAfter)) continue;
                Apply(i, 1);
                if (Search(i + 1)) return true;
                Apply(i, -1);
            }
            return false;
        }
        if (!Search(0)) throw new InvalidOperationException("Cannot compose this blueprint within the selection budget. Add eligible items or simplify its count, objective, format or scenario constraints.");
        return chosen.ToArray();
    }

    // Largest-remainder shares of Count; each objective may land one question either side, and never below one.
    private static (int[] Low, int[] High) Quotas(Blueprint blueprint, Dictionary<string, int> index)
    {
        var weights = blueprint.ObjectiveWeights!;
        var share = new decimal[index.Count];
        foreach (var (id, i) in index)
            share[i] = weights.TryGetValue(id, out var w) ? w : throw new InvalidOperationException($"Blueprint '{blueprint.Id}' has no weight for '{id}'.");
        var total = share.Sum();
        var target = share.Select(w => blueprint.Count * w / total).ToArray();
        var quota = target.Select(t => (int)decimal.Floor(t)).ToArray();
        foreach (var i in Enumerable.Range(0, quota.Length).OrderByDescending(i => target[i] - quota[i]).ThenBy(i => i).Take(blueprint.Count - quota.Sum()))
            quota[i]++;
        return (quota.Select(q => Math.Max(1, q - 1)).ToArray(), quota.Select(q => q + 1).ToArray());
    }

    private static int Bit(QuestionKind kind) => 1 << (int)kind;
}
