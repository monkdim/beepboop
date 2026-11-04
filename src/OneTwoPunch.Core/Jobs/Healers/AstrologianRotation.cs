using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.Healers;

public sealed class AstrologianRotation : JobRotationBase
{
    private static readonly ActionRef[] Actions =
    [
        new(3596, "Malefic", ActionKind.Gcd, 1),
        new(3598, "Malefic II", ActionKind.Gcd, 54),
        new(7442, "Malefic III", ActionKind.Gcd, 64),
        new(16555, "Malefic IV", ActionKind.Gcd, 72),
        new(25871, "Fall Malefic", ActionKind.Gcd, 82),
        new(3615, "Gravity", ActionKind.Gcd, 45),
        new(25872, "Gravity II", ActionKind.Gcd, 82),
        new(3599, "Combust", ActionKind.Gcd, 4),
        new(3608, "Combust II", ActionKind.Gcd, 46),
        new(16554, "Combust III", ActionKind.Gcd, 72),
        new(37017, "Astral Draw", ActionKind.OGcd, 30),
        new(37018, "Umbral Draw", ActionKind.OGcd, 30),
        new(37019, "Play I", ActionKind.OGcd, 30),
        new(37020, "Play II", ActionKind.OGcd, 30),
        new(37021, "Play III", ActionKind.OGcd, 30),
        new(37023, "The Balance", ActionKind.OGcd, 30),
        new(37024, "The Arrow", ActionKind.OGcd, 30),
        new(37025, "The Spire", ActionKind.OGcd, 30),
        new(37026, "The Spear", ActionKind.OGcd, 30),
        new(37027, "The Bole", ActionKind.OGcd, 30),
        new(37028, "The Ewer", ActionKind.OGcd, 30),
        new(16552, "Divination", ActionKind.OGcd, 50),
        new(37022, "Minor Arcana", ActionKind.OGcd, 70),
        new(7444, "Lord of Crowns", ActionKind.OGcd, 70),
        new(7445, "Lady of Crowns", ActionKind.OGcd, 70),
        new(37029, "Oracle", ActionKind.OGcd, 92),
        new(7562, "Lucid Dreaming", ActionKind.OGcd, 14)
    ];
    private static readonly StatusRef[] Statuses = [new(838, "Combust"), new(843, "Combust II"), new(1881, "Combust III"), new(3893, "Divining")];
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

    public override uint JobId => 33;
    public override string Name => "Astrologian";
    public override float AoeRadius => 5;
    public override bool AoeCenteredOnPlayer => false;
    public override ActionRef SingleTargetButton => A(3596);
    public override ActionRef AoeButton => A(3615);
    public override IReadOnlyList<ActionRef> AllActions => Actions;
    public override IReadOnlyList<StatusRef> AllStatuses => Statuses;

    public override ActionRef? BurstAction => A(16552);

    protected override void Build()
    {
        BuildRotation(SingleTarget, false);
        BuildRotation(Aoe, true);
    }

    private void BuildRotation(RotationPlan p, bool aoe)
    {
        ActionRef[] singleFiller = [A(3596), A(3598), A(7442), A(16555), A(25871)];
        ActionRef[] areaFiller = [A(3615), A(25872)];
        p.OGcd(A(7562)).When(c => c.InCombat && c.Mp < 7000);
        p.OGcd(A(16552)).When(c => c.HasTarget);
        p.OGcd(A(37029)).When(c => c.HasTarget && c.Buff(S(3893)));
        foreach (var play in new uint[] { 37019, 37020, 37021 })
        {
            p.OGcd(c => Card(c, play)).When(c => c.InCombat);
        }
        p.OGcd(A(7444)).When(c => c.HasTarget && c.Snapshot.TargetDistance <= 20 && Current(c, A(37022)).Id == 7444);
        p.OGcd(c => Current(c, A(37017)))
            .When(c => c.InCombat && Card(c, 37019) is null && Card(c, 37020) is null && Card(c, 37021) is null);
        if (!aoe)
        {
            p.Gcd(A(3599)).When(c => c.HasTarget && c.Level >= 4 && c.Level < 46 && c.DotExpiring(S(838)));
            p.Gcd(A(3608)).When(c => c.HasTarget && c.Level >= 46 && c.Level < 72 && c.DotExpiring(S(843)));
            p.Gcd(A(16554)).When(c => c.HasTarget && c.Level >= 72 && c.Level < 255 && c.DotExpiring(S(1881)));
        }
        if (aoe)
            p.Gcd(c => Highest(c, areaFiller)).When(c => c.HasTarget);
        p.Gcd(c => Highest(c, singleFiller));
    }

    private ActionRef? Card(RotationContext context, uint slot)
    {
        var form = Current(context, A(slot));
        return AstrologianCardTarget.IsCard(form.Id) ? form : null;
    }
}
