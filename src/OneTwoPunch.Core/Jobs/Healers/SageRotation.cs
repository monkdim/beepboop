using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.Healers;

public sealed class SageRotation : JobRotationBase
{
    private static readonly ActionRef[] Actions =
    [
        new(24283, "Dosis", ActionKind.Gcd, 1),
        new(24306, "Dosis II", ActionKind.Gcd, 72),
        new(24312, "Dosis III", ActionKind.Gcd, 82),
        new(24297, "Dyskrasia", ActionKind.Gcd, 46),
        new(24315, "Dyskrasia II", ActionKind.Gcd, 82),
        new(24293, "Eukrasian Dosis", ActionKind.Gcd, 30),
        new(24308, "Eukrasian Dosis II", ActionKind.Gcd, 72),
        new(24314, "Eukrasian Dosis III", ActionKind.Gcd, 82),
        new(24290, "Eukrasia", ActionKind.Gcd, 30),
        new(24289, "Phlegma", ActionKind.Gcd, 26),
        new(24307, "Phlegma II", ActionKind.Gcd, 72),
        new(24313, "Phlegma III", ActionKind.Gcd, 82),
        new(37032, "Eukrasian Dyskrasia", ActionKind.Gcd, 82),
        new(37033, "Psyche", ActionKind.OGcd, 92),
        new(7562, "Lucid Dreaming", ActionKind.OGcd, 14)
    ];
    private static readonly StatusRef[] Statuses = [new(2614, "Eukrasian Dosis"), new(2615, "Eukrasian Dosis II"), new(2616, "Eukrasian Dosis III"), new(2606, "Eukrasia"), new(3897, "Eukrasian Dyskrasia")];
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

    public override uint JobId => 40;
    public override string Name => "Sage";
    public override float AoeRadius => 5;
    public override bool AoeCenteredOnPlayer => true;
    public override ActionRef SingleTargetButton => A(24283);
    public override ActionRef AoeButton => A(24297);
    public override IReadOnlyList<ActionRef> AllActions => Actions;
    public override IReadOnlyList<StatusRef> AllStatuses => Statuses;

    public override ActionRef? BurstAction => A(37033);

    protected override void Build()
    {
        BuildRotation(SingleTarget, false);
        BuildRotation(Aoe, true);
    }

    private void BuildRotation(RotationPlan p, bool aoe)
    {
        ActionRef[] singleFiller = [A(24283), A(24306), A(24312)];
        ActionRef[] areaFiller = [A(24297), A(24315)];
        ActionRef[] singleDot = [A(24293), A(24308), A(24314)];
        ActionRef[] phlegma = [A(24289), A(24307), A(24313)];
        p.OGcd(A(7562)).When(c => c.InCombat && c.Mp < 7000);
        p.OGcd(A(37033)).When(c => c.HasTarget);
        bool WantsDot(RotationContext c) => c.HasTarget && (aoe && c.Has(A(37032)) && c.Snapshot.TargetDistance <= 5
            ? c.DotExpiring(S(3897)) && c.DotExpiring(S(2616))
            : c.DotExpiring(c.Level >= 82 ? S(2616) : c.Level >= 72 ? S(2615) : S(2614))
                && c.DotExpiring(S(3897)));
        p.Gcd(A(24290)).When(c => WantsDot(c) && !c.Buff(S(2606)));
        p.Gcd(c => aoe && c.Has(A(37032)) && c.Snapshot.TargetDistance <= 5
            ? A(37032) : Highest(c, singleDot))
            .When(c => c.HasTarget && c.Buff(S(2606)));
        p.Gcd(c => Highest(c, phlegma)).When(c => c.HasTarget && !c.Buff(S(2606)));
        if (aoe)
            p.Gcd(c => Highest(c, areaFiller)).When(c => c.Snapshot.EnemiesInAoeRange > 0);
        p.Gcd(c => Highest(c, singleFiller)).When(c => !c.Buff(S(2606)));
    }
}
