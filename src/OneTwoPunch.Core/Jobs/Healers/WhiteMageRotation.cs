using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.Healers;

public sealed class WhiteMageRotation : JobRotationBase
{
    private static readonly ActionRef[] Actions =
    [
        new(119, "Stone", ActionKind.Gcd, 1),
        new(127, "Stone II", ActionKind.Gcd, 18),
        new(3568, "Stone III", ActionKind.Gcd, 54),
        new(7431, "Stone IV", ActionKind.Gcd, 64),
        new(16533, "Glare", ActionKind.Gcd, 72),
        new(25859, "Glare III", ActionKind.Gcd, 82),
        new(139, "Holy", ActionKind.Gcd, 45),
        new(25860, "Holy III", ActionKind.Gcd, 82),
        new(121, "Aero", ActionKind.Gcd, 4),
        new(132, "Aero II", ActionKind.Gcd, 46),
        new(16532, "Dia", ActionKind.Gcd, 72),
        new(136, "Presence of Mind", ActionKind.OGcd, 30),
        new(3571, "Assize", ActionKind.OGcd, 56),
        new(16535, "Afflatus Misery", ActionKind.Gcd, 74),
        new(37009, "Glare IV", ActionKind.Gcd, 92),
        new(7562, "Lucid Dreaming", ActionKind.OGcd, 14)
    ];
    private static readonly StatusRef[] Statuses = [new(143, "Aero"), new(144, "Aero II"), new(1871, "Dia"), new(3879, "Sacred Sight")];
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

    public override uint JobId => 24;
    public override string Name => "White Mage";
    public override float AoeRadius => 8;
    public override bool AoeCenteredOnPlayer => true;
    public override ActionRef SingleTargetButton => A(119);
    public override ActionRef AoeButton => A(139);
    public override IReadOnlyList<ActionRef> AllActions => Actions;
    public override IReadOnlyList<StatusRef> AllStatuses => Statuses;

    public override ActionRef? BurstAction => A(136);

    protected override void Build()
    {
        BuildRotation(SingleTarget, false);
        BuildRotation(Aoe, true);
    }

    private void BuildRotation(RotationPlan p, bool aoe)
    {
        ActionRef[] singleFiller = [A(119), A(127), A(3568), A(7431), A(16533), A(25859)];
        ActionRef[] areaFiller = [A(139), A(25860)];
        p.OGcd(A(7562)).When(c => c.InCombat && c.Mp < 7000);
        p.OGcd(A(136)).When(c => c.HasTarget);
        p.OGcd(A(3571)).When(c => c.HasTarget && c.Snapshot.TargetDistance <= 15);
        p.Gcd(A(16535)).When(c => c.HasTarget && c.Snapshot.Gauges.WhiteMageBloodLily >= 3);
        p.Gcd(A(37009)).When(c => c.HasTarget && c.Buff(S(3879)));
        if (!aoe)
        {
            p.Gcd(A(121)).When(c => c.HasTarget && c.Level >= 4 && c.Level < 46 && c.DotExpiring(S(143)));
            p.Gcd(A(132)).When(c => c.HasTarget && c.Level >= 46 && c.Level < 72 && c.DotExpiring(S(144)));
            p.Gcd(A(16532)).When(c => c.HasTarget && c.Level >= 72 && c.Level < 255 && c.DotExpiring(S(1871)));
        }
        if (aoe)
            p.Gcd(c => Highest(c, areaFiller)).When(c => c.Snapshot.EnemiesInAoeRange > 0);
        p.Gcd(c => Highest(c, singleFiller));
    }
}
