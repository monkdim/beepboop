using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Dragoon.DragoonActions;

namespace OneTwoPunch.Core.Jobs.Dragoon;

/// <summary>
/// Dragoon, Dawntrail. Two five-GCD chains that alternate, wrapped in a two-minute burst.
/// <para>
/// Read the lists top to bottom: the first line whose action is usable and whose condition
/// holds is what the button becomes. Off-global lines are only ever considered inside a
/// weave window that the engine has already proved is safe.
/// </para>
/// </summary>
public sealed class DragoonRotation : JobRotationBase
{
    public override uint JobId => 22;

    public override string Name => "Dragoon";

    public override ActionRef SingleTargetButton => A.TrueThrust;

    public override ActionRef AoeButton => A.DoomSpike;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? PositionalRescue => A.TrueNorth;

    public override StatusRef? PositionalRescueStatus => A.TrueNorthBuff;

    /// <summary>Lance Charge is the clean marker for Dragoon's burst window.</summary>
    public override StatusRef? BurstStatus => A.LanceChargeBuff;

    public override ActionRef? BurstAction => A.LanceCharge;

    /// <summary>
    /// The Balance's "Standard Opener - No PT" for Dragoon level 100, Dawntrail patch 7.1.
    /// Both Life Surges are spent inside the window, on Drakesbane and on Heavens' Thrust.
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance standard, no PT", 100,
        A.TrueThrust,
        A.SpiralBlow, A.LanceCharge,
        A.ChaoticSpring, A.BattleLitany, A.Geirskogul,
        A.WheelingThrust, A.HighJump, A.LifeSurge,
        A.Drakesbane, A.DragonfireDive, A.Nastrond,
        A.RaidenThrust, A.Stardiver,
        A.LanceBarrage, A.Starcross, A.LifeSurge,
        A.HeavensThrust, A.RiseOfTheDragon, A.MirageDive,
        A.FangAndClaw,
        A.Drakesbane,
        A.RaidenThrust, A.WyrmwindThrust)
    {
        // Drunk right after Lance Charge, in Spiral Blow's weave window.
        PotionBeforeStep = 3,
    };

    public override Opener? Opener => Sequence;

    /// <summary>Firstminds' Focus is what Wyrmwind Thrust waits on; Life of the Dragon gates Nastrond.</summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Dragoon;

        return $"focus {g.FirstmindsFocus}"
               + (g.LotdActive ? $" | life of the dragon {g.LotdTimeRemaining:0.0}s" : string.Empty);
    }

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // Burst first: everything else lines up behind the two-minute window.
        p.OGcd(A.LanceCharge)
            .When(c => !c.Downtime)
            .Because("burst window");

        p.OGcd(A.BattleLitany)
            .When(c => !c.Downtime)
            .Because("raid buff");

        // Life Surge guarantees a crit, so it is only worth spending on the two biggest
        // hits in the chain, and only under Lance Charge.
        // Life Surge is consumed by the next weaponskill, so the only thing that matters is
        // that the next global is one of the big ones - any weave slot in this window works.
        // It used to also demand GcdImminent, which narrowed it to about half a second, and
        // Lance Charge, which meant the second charge was never spent outside burst. Between
        // them it effectively never fired. Burst is still preferred; the charge is spent
        // outside it only to avoid throwing one away by overcapping.
        p.OGcd(A.LifeSurge)
            .When(c => !c.Buff(A.LifeSurgeBuff)
                       && c.NextGcdIsAny(A.Drakesbane, A.HeavensThrust, A.FullThrust)
                       && (c.Buff(A.LanceChargeBuff) || c.Charges(A.LifeSurge) >= 2))
            .Because("guaranteed crit on the big hit");

        p.OGcd(A.Geirskogul).When(c => !c.Downtime);

        p.OGcd(A.Nastrond)
            .When(c => c.Buff(A.NastrondReady))
            .Because("Life of the Dragon");

        p.OGcd(c => c.Has(A.HighJump) ? A.HighJump : A.Jump)
            .When(c => !c.Downtime);

        p.OGcd(A.MirageDive).When(c => c.Buff(A.DiveReady));

        p.OGcd(A.DragonfireDive).When(c => !c.Downtime);

        p.OGcd(A.RiseOfTheDragon).When(c => c.Buff(A.DragonsFlight));

        p.OGcd(A.Stardiver).When(c => c.Drg.LotdActive);

        p.OGcd(A.Starcross).When(c => c.Buff(A.StarcrossReady));

        // Two stacks of Firstminds' Focus. Held at one stack it is not yet spendable.
        p.OGcd(A.WyrmwindThrust)
            .When(c => c.Drg.FirstmindsFocus >= 2)
            .Because("gauge is full");

        // ---- GCDs --------------------------------------------------------
        // The chain, resolved backwards: finishers before their prerequisites, so the
        // deepest live combo step always wins.
        p.Gcd(A.Drakesbane)
            .When(NextIsDrakesbane)
            .Because("combo finisher");

        p.Gcd(A.WheelingThrust)
            .When(c => c.ComboIs(A.ChaoticSpring) || c.ComboIs(A.ChaosThrust))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.FangAndClaw)
            .When(c => c.ComboIs(A.HeavensThrust) || c.ComboIs(A.FullThrust))
            .Needs(PositionalHint.Flank);

        p.Gcd(c => c.Has(A.ChaoticSpring) ? A.ChaoticSpring : A.ChaosThrust)
            .When(c => c.ComboIs(A.SpiralBlow))
            .Needs(PositionalHint.Rear);

        p.Gcd(c => c.Has(A.HeavensThrust) ? A.HeavensThrust : A.FullThrust)
            .When(c => c.ComboIs(A.LanceBarrage));

        // The fork. Spiral Blow refreshes Power Surge and leads to the damage-over-time
        // chain; Lance Barrage leads to the raw damage chain. Take the buff chain whenever
        // Power Surge or the dot is running low, otherwise hit harder.
        p.Gcd(A.SpiralBlow)
            .When(c => ComboStarted(c)
                       && (c.BuffTime(A.PowerSurge) < 12f || DotExpiring(c)))
            .Because("refresh Power Surge / dot");

        p.Gcd(A.LanceBarrage).When(ComboStarted);

        p.Gcd(A.RaidenThrust)
            .When(c => c.Buff(A.DraconianFire))
            .Because("Draconian Fire proc");

        p.Gcd(A.TrueThrust);

        // Out of melee range and unable to close - keep some damage going rather than
        // suggesting something that will just error out.
        p.Gcd(A.PiercingTalon)
            .When(c => !c.InRange)
            .Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.LanceCharge).When(c => !c.Downtime).Because("burst window");
        p.OGcd(A.BattleLitany).When(c => !c.Downtime).Because("raid buff");
        p.OGcd(A.Geirskogul).When(c => !c.Downtime);
        p.OGcd(A.Nastrond).When(c => c.Buff(A.NastrondReady));
        p.OGcd(A.DragonfireDive).When(c => !c.Downtime);
        p.OGcd(A.RiseOfTheDragon).When(c => c.Buff(A.DragonsFlight));
        p.OGcd(A.Stardiver).When(c => c.Drg.LotdActive);
        p.OGcd(A.Starcross).When(c => c.Buff(A.StarcrossReady));
        p.OGcd(A.WyrmwindThrust).When(c => c.Drg.FirstmindsFocus >= 2);
        p.OGcd(c => c.Has(A.HighJump) ? A.HighJump : A.Jump).When(c => !c.Downtime);
        p.OGcd(A.MirageDive).When(c => c.Buff(A.DiveReady));

        // Life Surge does nothing useful for the AoE chain, so it is simply absent - one
        // fewer thing for the button to ever become.

        p.Gcd(A.CoerthanTorment).When(c => c.ComboIs(A.SonicThrust));

        p.Gcd(A.SonicThrust)
            .When(c => c.ComboIs(A.DoomSpike) || c.ComboIs(A.DraconianFury));

        p.Gcd(A.DraconianFury).When(c => c.Buff(A.DraconianFire));

        p.Gcd(A.DoomSpike);
    }

    private static bool ComboStarted(RotationContext c) =>
        c.ComboIs(A.TrueThrust) || c.ComboIs(A.RaidenThrust);

    private static bool NextIsDrakesbane(RotationContext c) =>
        c.ComboIs(A.WheelingThrust) || c.ComboIs(A.FangAndClaw);

    private static bool DotExpiring(RotationContext c) =>
        c.Has(A.ChaoticSpring)
            ? c.DotExpiring(A.ChaoticSpringBuff)
            : c.DotExpiring(A.ChaosThrustBuff);
}
