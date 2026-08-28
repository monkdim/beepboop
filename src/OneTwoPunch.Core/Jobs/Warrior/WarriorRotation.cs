using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Warrior.WarriorActions;

namespace OneTwoPunch.Core.Jobs.Warrior;

/// <summary>
/// Warrior, Dawntrail. One buff to keep up, one gauge to spend, and a sixty second window
/// that hands out three free Fell Cleaves.
/// <para>
/// Surging Tempest is the whole job underneath everything else: it is a ten percent damage
/// buff on every hit, it only comes from Storm's Eye and Mythril Tempest, and dropping it
/// costs more than any single global could earn. So the refresh outranks the spenders.
/// </para>
/// <para>
/// Mitigation is not here. Vengeance, Damnation, Thrill of Battle, Bloodwhetting, Raw
/// Intuition, Equilibrium, Shake It Off, Holmgang and Nascent Flash are declared so their
/// ids are verified and left to the player, who can see the fight.
/// </para>
/// <para>
/// No scripted opener - see PaladinRotation for why. Inner Release is "as soon as Surging
/// Tempest is up", which the list below already does.
/// </para>
/// </summary>
public sealed class WarriorRotation : JobRotationBase
{
    public override uint JobId => 21;

    public override string Name => "Warrior";

    public override ActionRef SingleTargetButton => A.HeavySwing;

    public override ActionRef AoeButton => A.Overpower;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? BurstAction => A.InnerRelease;

    public override StatusRef? BurstStatus => A.InnerReleaseBuff;

    /// <summary>
    /// How little Surging Tempest may have left before Storm's Eye outranks everything.
    /// Storm's Eye is the third step of a combo, so the refresh has to be started early
    /// enough that two more globals still fit inside what is left.
    /// </summary>
    private const float TempestRefreshWindow = 15f;

    /// <summary>Beast Gauge at or above which another combo would waste some of it.</summary>
    private const byte BeastNearCap = 90;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // Inner Release is three free Fell Cleaves and a Primal Rend, all of which want
        // Surging Tempest underneath them - so it waits for the buff rather than the other
        // way round. Berserk is the same press below level 70.
        p.OGcd(c => ReleaseAction(c))
            .When(c => !c.Downtime && (c.Buff(A.SurgingTempest) || !c.Has(A.StormEye)))
            .Because("burst window");

        // Infuriate is fifty gauge, so it is only pressed where fifty gauge fits.
        p.OGcd(A.Infuriate)
            .When(c => !c.Downtime && c.War.BeastGauge <= 50 && !c.Buff(A.NascentChaos))
            .Because("Beast Gauge has room");

        p.OGcd(A.PrimalWrath).When(c => c.Buff(A.Wrathful)).Because("Wrathful");

        p.OGcd(A.Upheaval).When(c => !c.Downtime);

        // A dash, so it is only offered where it is part of the burst rather than as
        // filler that moves the player somewhere they did not ask to be.
        p.OGcd(A.Onslaught)
            .When(c => !c.Downtime && c.Buff(A.InnerReleaseBuff))
            .Because("spend a charge in burst");

        // ---- GCDs --------------------------------------------------------
        p.Gcd(A.PrimalRuination)
            .When(c => c.Buff(A.PrimalRuinationReady))
            .Because("Primal Ruination Ready");

        p.Gcd(A.PrimalRend)
            .When(c => c.Buff(A.PrimalRendBuff))
            .Because("Primal Rend Ready");

        // Surging Tempest first, always. Storm's Eye is two globals deep, so the window
        // has to open early enough for the combo to reach it.
        p.Gcd(A.StormEye)
            .When(c => c.ComboIs(A.Maim) && TempestNeedsRefreshing(c))
            .Because("refresh Surging Tempest");

        p.Gcd(A.Maim)
            .When(c => c.ComboIs(A.HeavySwing) && TempestNeedsRefreshing(c))
            .Because("on the way to Storm's Eye");

        p.Gcd(A.HeavySwing)
            .When(c => TempestNeedsRefreshing(c) && !c.ComboIs(A.HeavySwing) && !c.ComboIs(A.Maim))
            .Because("on the way to Storm's Eye");

        // Inner Chaos is Fell Cleave with the Nascent Chaos spent on it, so it is strictly
        // the better press whenever the buff is up.
        p.Gcd(A.InnerChaos).When(c => c.Buff(A.NascentChaos)).Because("Nascent Chaos");

        // Free under Inner Release; otherwise a fifty gauge spend, held back from a bar
        // that is about to overflow rather than thrown at every fifty.
        p.Gcd(A.FellCleave)
            .When(c => c.Buff(A.InnerReleaseBuff))
            .Because("free under Inner Release");

        p.Gcd(A.FellCleave)
            .When(c => c.War.BeastGauge >= BeastNearCap)
            .Because("Beast Gauge is about to overflow");

        p.Gcd(A.FellCleave)
            .When(c => c.War.BeastGauge >= 50 && c.Buff(A.SurgingTempest) && !ReleaseIsClose(c))
            .Because("spend the gauge");

        // The ordinary combo, ending on Storm's Path for the gauge and the heal.
        p.Gcd(A.StormPath).When(c => c.ComboIs(A.Maim));
        p.Gcd(A.Maim).When(c => c.ComboIs(A.HeavySwing));
        p.Gcd(A.HeavySwing);

        p.Gcd(A.Tomahawk).When(c => !c.InRange).Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(c => ReleaseAction(c))
            .When(c => !c.Downtime && (c.Buff(A.SurgingTempest) || !c.Has(A.MythrilTempest)))
            .Because("burst window");

        p.OGcd(A.Infuriate)
            .When(c => !c.Downtime && c.War.BeastGauge <= 50 && !c.Buff(A.NascentChaos))
            .Because("Beast Gauge has room");

        p.OGcd(A.PrimalWrath).When(c => c.Buff(A.Wrathful));
        p.OGcd(A.Orogeny).When(c => !c.Downtime);

        p.Gcd(A.PrimalRuination).When(c => c.Buff(A.PrimalRuinationReady));
        p.Gcd(A.PrimalRend).When(c => c.Buff(A.PrimalRendBuff));

        // Mythril Tempest is the AoE half of Surging Tempest, and it is only one global
        // deep rather than two.
        p.Gcd(A.MythrilTempest)
            .When(c => c.ComboIs(A.Overpower) && TempestNeedsRefreshing(c))
            .Because("refresh Surging Tempest");

        p.Gcd(A.ChaoticCyclone).When(c => c.Buff(A.NascentChaos)).Because("Nascent Chaos");

        p.Gcd(A.Decimate)
            .When(c => c.Buff(A.InnerReleaseBuff))
            .Because("free under Inner Release");

        p.Gcd(A.Decimate)
            .When(c => c.War.BeastGauge >= 50)
            .Because("spend the gauge");

        p.Gcd(A.MythrilTempest).When(c => c.ComboIs(A.Overpower));
        p.Gcd(A.Overpower);
    }

    /// <summary>
    /// True when Surging Tempest is missing or close enough to gone that the three globals
    /// of the combo are worth spending on getting it back.
    /// </summary>
    private static bool TempestNeedsRefreshing(RotationContext c) =>
        (c.Has(A.StormEye) || c.Has(A.MythrilTempest))
        && c.BuffTime(A.SurgingTempest) < TempestRefreshWindow;

    /// <summary>
    /// True when Inner Release is close enough that the gauge is better held for it: the
    /// three free Fell Cleaves do not spend gauge, so fifty banked now is a Fell Cleave
    /// more inside the window rather than one wasted before it.
    /// </summary>
    private static bool ReleaseIsClose(RotationContext c) =>
        c.Has(A.InnerRelease) && c.Cd(A.InnerRelease) < 10f;

    /// <summary>Inner Release is Berserk's upgrade; both are the same press.</summary>
    private static ActionRef ReleaseAction(RotationContext c) =>
        c.Has(A.InnerRelease) ? A.InnerRelease : A.Berserk;

    /// <summary>What the recorded log prints for a Warrior line.</summary>
    public override string DescribeGauge(CombatSnapshot snapshot) =>
        $"beast {snapshot.Gauges.Warrior.BeastGauge}";
}
