namespace OneTwoPunch.Core.Jobs.Healers;

public readonly record struct CardRecipient(ulong Id, byte Role, bool IsSelf, float Health, bool Eligible = true);

public static class AstrologianCardTarget
{
    public static bool IsCard(uint actionId) => actionId is >= 37023 and <= 37028;

    public static ulong Choose(uint actionId, ReadOnlySpan<CardRecipient> candidates)
    {
        ulong best = 0;
        var bestRank = int.MaxValue;
        var bestHealth = float.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            if (candidate.Id == 0 || !candidate.Eligible)
                continue;
            var rank = Priority(actionId, candidate.Role, candidate.IsSelf);
            if (rank > bestRank || rank == bestRank && candidate.Health >= bestHealth)
                continue;
            best = candidate.Id;
            bestRank = rank;
            bestHealth = candidate.Health;
        }
        return best;
    }

    // ClassJob.Role: tank 1, melee 2, ranged 3, healer 4.
    public static int Priority(uint actionId, byte role, bool self) => actionId switch
    {
        37023 => role switch { 2 => 0, 1 => 1, 3 => 2, _ => self ? 4 : 3 }, // Balance
        37026 => role switch { 3 => 0, 4 => self ? 2 : 1, 2 => 3, _ => 4 }, // Spear
        37024 or 37025 or 37027 => role == 1 ? 0 : 1, // Arrow, Spire, Bole
        _ => 0, // Ewer: lowest HP among usable targets
    };
}
