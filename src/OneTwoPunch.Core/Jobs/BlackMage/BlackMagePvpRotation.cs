using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.BlackMage;

public sealed class BlackMagePvpRotation : JobRotationBase
{
    private static readonly ActionRef Fire = new(0x73D1, "Fire", ActionKind.Gcd, 1);
    private static readonly ActionRef Fire3 = new(30896, "Fire III", ActionKind.Gcd, 1);
    private static readonly ActionRef Fire4 = new(29650, "Fire IV", ActionKind.Gcd, 1);
    private static readonly ActionRef HighFire = new(0xA201, "High Fire II", ActionKind.Gcd, 1);
    private static readonly ActionRef Flare = new(29651, "Flare", ActionKind.Gcd, 1);
    private static readonly ActionRef Blizzard = new(0x73D5, "Blizzard", ActionKind.Gcd, 1);
    private static readonly ActionRef Blizzard3 = new(30897, "Blizzard III", ActionKind.Gcd, 1);
    private static readonly ActionRef Blizzard4 = new(29654, "Blizzard IV", ActionKind.Gcd, 1);
    private static readonly ActionRef HighBlizzard = new(0xA202, "High Blizzard II", ActionKind.Gcd, 1);
    private static readonly ActionRef Freeze = new(29655, "Freeze", ActionKind.Gcd, 1);
    private static readonly ActionRef Burst = new(29657, "Burst", ActionKind.Gcd, 1);
    private static readonly ActionRef Xenoglossy = new(0x73DA, "Xenoglossy", ActionKind.Gcd, 1);
    private static readonly ActionRef Paradox = new(0x73DF, "Paradox", ActionKind.Gcd, 1);
    private static readonly ActionRef PhantomDart = new(0xA91B, "Phantom Dart", ActionKind.OGcd, 1);
    private static readonly ActionRef Lethargy = new(0xA226, "Lethargy", ActionKind.OGcd, 1);
    private static readonly ActionRef WreathOfFire = new(0xA204, "Wreath of Fire", ActionKind.OGcd, 1);
    private static readonly StatusRef AstralFire = new(0xC8C, "Astral Fire");
    private static readonly StatusRef AstralFire2 = new(0xC8D, "Astral Fire II");
    private static readonly StatusRef UmbralIce = new(0xC8E, "Umbral Ice");
    private static readonly StatusRef UmbralIce2 = new(0xC8F, "Umbral Ice II");
    private static readonly StatusRef AstralFire3 = new(0xD35, "Astral Fire III");
    private static readonly StatusRef UmbralIce3 = new(0xD36, "Umbral Ice III");

    public override uint JobId => 25;
    public override string Name => "Black Mage PvP";
    public override ActionRef SingleTargetButton => Fire;
    public override ActionRef AoeButton => Blizzard;
    public override WeaveStyle MinimumWeaveStyle => WeaveStyle.Double;
    public override IReadOnlyList<ActionRef> AllActions { get; } =
        [Fire, Fire3, Fire4, HighFire, Flare, Blizzard, Blizzard3, Blizzard4, HighBlizzard, Freeze,
            Burst, Xenoglossy, Paradox, PhantomDart, Lethargy, WreathOfFire];
    public override IReadOnlyList<StatusRef> AllStatuses { get; } =
        [AstralFire, AstralFire2, AstralFire3, UmbralIce, UmbralIce2, UmbralIce3];

    protected override void Build()
    {
        BuildPlan(SingleTarget, Fire, false);
        BuildPlan(Aoe, Blizzard, true);
    }

    private void BuildPlan(RotationPlan plan, ActionRef starter, bool aoe)
    {
        plan.OGcd(PhantomDart).When(c => c.HasTarget && c.NextGcdIsAny(Xenoglossy, HighFire, HighBlizzard));
        plan.OGcd(Lethargy).When(c => c.HasTarget && c.NextGcdIsAny(Xenoglossy, HighFire, HighBlizzard));
        plan.OGcd(WreathOfFire).When(c => c.HasTarget && InFire(c));

        plan.Gcd(Paradox);
        if (aoe)
            plan.Gcd(Burst).When(c => c.HasTarget && c.Snapshot.TargetDistance <= 6f);
        plan.Gcd(Xenoglossy);
        plan.Gcd(c => ElementalSpell(c, starter));
    }

    private ActionRef ElementalSpell(RotationContext context, ActionRef starter)
    {
        if (InFire(context))
        {
            var fire = Current(context, Fire);
            return fire.Id == Fire3.Id ? Current(context, Blizzard) : fire;
        }

        if (context.Buff(UmbralIce) || context.Buff(UmbralIce2) || context.Buff(UmbralIce3))
        {
            var ice = Current(context, Blizzard);
            return ice.Id == Blizzard3.Id ? Current(context, Fire) : ice;
        }

        return Current(context, starter);
    }

    private static bool InFire(RotationContext context) =>
        context.Buff(AstralFire) || context.Buff(AstralFire2) || context.Buff(AstralFire3);
}
