using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.Healers;

public sealed class ScholarRotation : JobRotationBase
{
    private static readonly ActionRef[] Actions =
    [
        new(17869, "Ruin", ActionKind.Gcd, 1),
        new(3584, "Broil", ActionKind.Gcd, 54),
        new(7435, "Broil II", ActionKind.Gcd, 64),
        new(16541, "Broil III", ActionKind.Gcd, 72),
        new(25865, "Broil IV", ActionKind.Gcd, 82),
        new(16539, "Art of War", ActionKind.Gcd, 46),
        new(25866, "Art of War II", ActionKind.Gcd, 82),
        new(17864, "Bio", ActionKind.Gcd, 2),
        new(17865, "Bio II", ActionKind.Gcd, 26),
        new(16540, "Biolysis", ActionKind.Gcd, 72),
        new(7436, "Chain Stratagem", ActionKind.OGcd, 66),
        new(37012, "Baneful Impaction", ActionKind.OGcd, 92),
        new(17870, "Ruin II", ActionKind.Gcd, 38),
        new(7562, "Lucid Dreaming", ActionKind.OGcd, 14)
    ];
    private static readonly StatusRef[] Statuses = [new(179, "Bio"), new(189, "Bio II"), new(1895, "Biolysis"), new(3882, "Impact Imminent")];
    private static readonly Dictionary<uint, ActionRef> ById = Actions.ToDictionary(a => a.Id);
    private static readonly Dictionary<uint, StatusRef> StatusById = Statuses.ToDictionary(s => s.Id);
    private static ActionRef A(uint id) => ById[id];
    private static StatusRef S(uint id) => StatusById[id];
    private static ActionRef Highest(RotationContext c, ActionRef[] actions)
    {
        for (var i = actions.Length - 1; i > 0; i--)
            if (c.Has(actions[i]))
                return actions[i];
        return actions[0];
    }

    public override uint JobId => 28;
    public override string Name => "Scholar";
    public override float AoeRadius => 5;
    public override bool AoeCenteredOnPlayer => true;
    public override ActionRef SingleTargetButton => A(17869);
    public override ActionRef AoeButton => A(16539);
    public override IReadOnlyList<ActionRef> AllActions => Actions;
    public override IReadOnlyList<StatusRef> AllStatuses => Statuses;

    public override ActionRef? BurstAction => A(7436);

    protected override void Build()
    {
        BuildRotation(SingleTarget, false);
        BuildRotation(Aoe, true);
    }

    private void BuildRotation(RotationPlan p, bool aoe)
    {
        ActionRef[] singleFiller = [A(17869), A(3584), A(7435), A(16541), A(25865)];
        ActionRef[] areaFiller = [A(16539), A(25866)];
        p.OGcd(A(7562)).When(c => c.InCombat && c.Mp < 7000);
        p.OGcd(A(7436)).When(c => c.HasTarget);
        p.OGcd(A(37012)).When(c => c.HasTarget && c.Buff(S(3882)));
        if (!aoe)
        {
            p.Gcd(A(17864)).When(c => c.HasTarget && c.Level >= 2 && c.Level < 26 && c.DotExpiring(S(179)));
            p.Gcd(A(17865)).When(c => c.HasTarget && c.Level >= 26 && c.Level < 72 && c.DotExpiring(S(189)));
            p.Gcd(A(16540)).When(c => c.HasTarget && c.Level >= 72 && c.Level < 255 && c.DotExpiring(S(1895)));
        }
        if (aoe)
            p.Gcd(c => Highest(c, areaFiller)).When(c => c.Snapshot.EnemiesInAoeRange > 0);
        p.Gcd(A(17870)).When(c => c.Moving);
        p.Gcd(c => Highest(c, singleFiller));
    }
}
