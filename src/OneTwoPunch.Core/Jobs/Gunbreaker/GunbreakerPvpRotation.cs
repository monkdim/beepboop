using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;

namespace OneTwoPunch.Core.Jobs.Gunbreaker;

public sealed class GunbreakerPvpRotation : JobRotationBase
{
    private static readonly ActionRef KeenEdge = new(0x71AA, "Keen Edge", ActionKind.Gcd, 1);
    private static readonly ActionRef BrutalShell = new(0x71AB, "Brutal Shell", ActionKind.Gcd, 1);
    private static readonly ActionRef SolidBarrel = new(0x71AC, "Solid Barrel", ActionKind.Gcd, 1);
    private static readonly ActionRef BurstStrike = new(0x71AD, "Burst Strike", ActionKind.Gcd, 1);
    private static readonly ActionRef GnashingFang = new(0x71AE, "Gnashing Fang", ActionKind.Gcd, 1);
    private static readonly ActionRef SavageClaw = new(0x71AF, "Savage Claw", ActionKind.Gcd, 1);
    private static readonly ActionRef WickedTalon = new(0x71B0, "Wicked Talon", ActionKind.Gcd, 1);
    private static readonly ActionRef FatedCircle = new(0xA227, "Fated Circle", ActionKind.Gcd, 1);
    private static readonly ActionRef Hypervelocity = new(0x71B3, "Hypervelocity", ActionKind.OGcd, 1);
    private static readonly ActionRef JugularRip = new(0x71B4, "Jugular Rip", ActionKind.OGcd, 1);
    private static readonly ActionRef AbdomenTear = new(0x71B5, "Abdomen Tear", ActionKind.OGcd, 1);
    private static readonly ActionRef EyeGouge = new(0x71B6, "Eye Gouge", ActionKind.OGcd, 1);
    private static readonly ActionRef FatedBrand = new(0xA1E2, "Fated Brand", ActionKind.OGcd, 1);
    private static readonly ActionRef RoughDivide = new(0x71C3, "Rough Divide", ActionKind.OGcd, 1);
    private static readonly ActionRef BlastingZone = new(0x71C8, "Blasting Zone", ActionKind.OGcd, 1);
    private static readonly ActionRef Rampage = new(0xA8EB, "Rampage", ActionKind.OGcd, 1);
    private static readonly StatusRef ReadyToRip = new(0x7D2, "Ready to Rip");
    private static readonly StatusRef ReadyToTear = new(0x7D3, "Ready to Tear");
    private static readonly StatusRef ReadyToGouge = new(0x7D4, "Ready to Gouge");
    private static readonly StatusRef ReadyToBlast = new(0xBE1, "Ready to Blast");
    private static readonly StatusRef ReadyToRaze = new(0x10C5, "Ready to Raze");
    private static readonly StatusRef NoMercy = new(0xBE2, "No Mercy");

    public override uint JobId => 37;
    public override string Name => "Gunbreaker PvP";
    public override ActionRef SingleTargetButton => GnashingFang;
    public override ActionRef AoeButton => FatedCircle;
    public override ActionRef GcdProbe => KeenEdge;
    public override WeaveStyle MinimumWeaveStyle => WeaveStyle.Double;
    public override IReadOnlyList<ActionRef> AllActions { get; } =
        [KeenEdge, BrutalShell, SolidBarrel, BurstStrike, GnashingFang, SavageClaw, WickedTalon,
            FatedCircle, Hypervelocity, JugularRip, AbdomenTear, EyeGouge, FatedBrand, RoughDivide,
            BlastingZone, Rampage];
    public override IReadOnlyList<StatusRef> AllStatuses { get; } =
        [ReadyToRip, ReadyToTear, ReadyToGouge, ReadyToBlast, ReadyToRaze, NoMercy];

    protected override void Build()
    {
        BuildPlan(SingleTarget, false);
        BuildPlan(Aoe, true);
    }

    private void BuildPlan(RotationPlan plan, bool aoe)
    {
        plan.OGcd(JugularRip).When(c => c.Buff(ReadyToRip));
        plan.OGcd(AbdomenTear).When(c => c.Buff(ReadyToTear));
        plan.OGcd(EyeGouge).When(c => c.Buff(ReadyToGouge));
        plan.OGcd(Hypervelocity).When(c => c.Buff(ReadyToBlast));
        plan.OGcd(FatedBrand).When(c => c.Buff(ReadyToRaze));
        plan.OGcd(RoughDivide).When(c => c.HasTarget && !c.Buff(NoMercy));
        plan.OGcd(Rampage).When(c => c.HasTarget && c.Snapshot.TargetDistance <= 10f);
        plan.OGcd(BlastingZone);

        plan.Gcd(WickedTalon).When(c => c.CurrentFormOf(GnashingFang) == WickedTalon.Id);
        plan.Gcd(SavageClaw).When(c => c.CurrentFormOf(GnashingFang) == SavageClaw.Id);
        if (aoe)
            plan.Gcd(FatedCircle).When(c => c.HasTarget && c.Snapshot.TargetDistance <= 5f);
        plan.Gcd(GnashingFang).When(c => c.CurrentFormOf(GnashingFang) == GnashingFang.Id);
        plan.Gcd(BurstStrike).When(c => c.ComboIs(SolidBarrel));
        if (!aoe)
            plan.Gcd(FatedCircle).When(c => c.HasTarget && c.Snapshot.TargetDistance <= 5f);
        plan.Gcd(SolidBarrel).When(c => c.ComboIs(BrutalShell));
        plan.Gcd(BrutalShell).When(c => c.ComboIs(KeenEdge));
        plan.Gcd(c => Current(c, KeenEdge));
    }
}
