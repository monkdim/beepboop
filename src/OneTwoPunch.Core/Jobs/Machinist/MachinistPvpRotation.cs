using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.Machinist;

public sealed class MachinistPvpRotation : JobRotationBase
{
    private static readonly ActionRef BlastCharge = new(0x72DA, "Blast Charge", ActionKind.Gcd, 1);
    public const uint BlazingShotId = 0xA1FC;
    private static readonly ActionRef BlazingShot = new(BlazingShotId, "Blazing Shot", ActionKind.Gcd, 1);
    private static readonly ActionRef Drill = new(0x72DD, "Drill", ActionKind.Gcd, 1);
    private static readonly ActionRef Bioblaster = new(0x72DE, "Bioblaster", ActionKind.Gcd, 1);
    private static readonly ActionRef AirAnchor = new(0x72DF, "Air Anchor", ActionKind.Gcd, 1);
    private static readonly ActionRef ChainSaw = new(0x72E0, "Chain Saw", ActionKind.Gcd, 1);
    private static readonly ActionRef Wildfire = new(0x72E1, "Wildfire", ActionKind.OGcd, 1);
    public const uint BishopAutoturretId = 0x72E4;
    private static readonly ActionRef BishopAutoturret = new(BishopAutoturretId, "Bishop Autoturret", ActionKind.OGcd, 1);
    private static readonly ActionRef Analysis = new(0x72E6, "Analysis", ActionKind.OGcd, 1);
    private static readonly ActionRef FullMetalField = new(0xA1FD, "Full Metal Field", ActionKind.Gcd, 1);
    private static readonly ActionRef Bravery = new(0xA8F2, "Bravery", ActionKind.OGcd, 1);
    private static readonly StatusRef Analysed = new(0xC56, "Analysis");
    private static readonly StatusRef WildfireDebuff = new(0x52B, "Wildfire");

    public override uint JobId => 31;
    public override string Name => "Machinist PvP";
    public override ActionRef SingleTargetButton => BlastCharge;
    public override ActionRef AoeButton => BlastCharge;
    public override WeaveStyle MinimumWeaveStyle => WeaveStyle.Double;
    public override IReadOnlyList<ActionRef> AllActions { get; } =
        [BlastCharge, BlazingShot, Drill, Bioblaster, AirAnchor, ChainSaw, Wildfire, BishopAutoturret, Analysis, FullMetalField, Bravery];
    public override IReadOnlyList<StatusRef> AllStatuses { get; } = [Analysed, WildfireDebuff];

    protected override void Build()
    {
        BuildPlan(SingleTarget);
        BuildPlan(Aoe);
    }

    private void BuildPlan(RotationPlan plan)
    {
        plan.OGcd(Wildfire).When(c => c.HasTarget && c.NextGcdIsAny(BlazingShot, FullMetalField)
            && !c.Debuff(WildfireDebuff) && c.CurrentFormOf(Wildfire) == Wildfire.Id);
        plan.OGcd(Bravery).When(c => c.HasTarget && c.NextGcdIsAny(BlazingShot, FullMetalField));
        plan.OGcd(BishopAutoturret).When(c => c.HasTarget && c.InRangeRightNow && c.NextGcdIs(BlazingShot));
        plan.OGcd(Analysis).When(c => c.HasTarget && !c.Buff(Analysed)
            && c.NextGcdIsAny(Drill, Bioblaster, AirAnchor, ChainSaw));

        // Finish the current Overheated window before starting another one.
        plan.Gcd(BlazingShot).When(c => c.CurrentFormOf(BlastCharge) == BlazingShot.Id);
        plan.Gcd(FullMetalField);
        plan.Gcd(c => Current(c, Drill));
        plan.Gcd(BlastCharge);
    }
}
