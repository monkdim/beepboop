using TwoButton.Core.Engine;
using TwoButton.Core.Model;
using A = TwoButton.Core.Jobs.Machinist.MachinistActions;

namespace TwoButton.Core.Jobs.Machinist;

/// <summary>
/// Machinist, Dawntrail. Fully mobile, and the densest weaving in the game - which makes it
/// the job where the weave budget matters most. On the single-weave setting the engine
/// drops the lowest-value off-globals first (Checkmate before Double Check, both before
/// Wildfire) rather than clipping the global cooldown.
/// </summary>
public sealed class MachinistRotation : JobRotationBase
{
    public override uint JobId => 31;

    public override string Name => "Machinist";

    public override ActionRef SingleTargetButton => A.SplitShot;

    public override ActionRef AoeButton => A.SpreadShot;

    public override float AoeRadius => 12f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    // Machinist's burst is marked by Wildfire, which sits on the target rather than on the
    // player, so there is no self-buff to key off - the burst ability serves instead.
    public override ActionRef? BurstAction => A.Wildfire;

    private static readonly Opener Sequence = new(
        "Dawntrail standard", 100,
        A.Reassemble, A.AirAnchor, A.Drill, A.ChainSaw, A.Excavator,
        A.BarrelStabilizer, A.Wildfire, A.Hypercharge, A.FullMetalField)
    {
        // Just before Wildfire, so the potion covers the burst it opens.
        PotionBeforeStep = 6,
    };

    public override Opener? Opener => Sequence;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // Wildfire wants the overheat window that follows it, so it goes first and
        // Hypercharge follows immediately behind.
        p.OGcd(A.Wildfire)
            .When(c => !c.Downtime && c.Mch.Heat >= 50)
            .Because("burst window");

        p.OGcd(A.BarrelStabilizer)
            .When(c => !c.Downtime && c.Mch.Heat <= 50)
            .Because("top up heat");

        p.OGcd(A.Hypercharge)
            .When(c => !c.Downtime
                       && !c.Buff(A.Overheated)
                       && (c.Mch.Heat >= 50 || c.Buff(A.Hypercharged))
                       // Never start an overheat window on top of a tool that is about to
                       // come up - the tool GCDs are worth more than the blasts.
                       && !ToolReadySoon(c))
            .Because("spend heat");

        p.OGcd(A.AutomatonQueen)
            .When(c => !c.Downtime && !c.Mch.RobotActive && c.Mch.Battery >= 50)
            .Because("battery is spendable");

        // Reassemble is a guaranteed crit direct hit, so it is only ever spent in front of
        // a tool - never on a filler combo GCD.
        p.OGcd(A.Reassemble)
            .When(c => !c.Buff(A.Reassembled)
                       && c.GcdImminent
                       && c.NextGcdIsAny(A.Excavator, A.ChainSaw, A.AirAnchor, A.Drill))
            .Because("guaranteed crit on a tool");

        p.OGcd(c => c.Has(A.DoubleCheck) ? A.DoubleCheck : A.GaussRound)
            .When(c => !c.Downtime);

        p.OGcd(c => c.Has(A.Checkmate) ? A.Checkmate : A.Ricochet)
            .When(c => !c.Downtime);

        // ---- GCDs --------------------------------------------------------
        // Overheat locks the GCD into blasts, so it outranks everything else.
        p.Gcd(c => c.Has(A.BlazingShot) ? A.BlazingShot : A.HeatBlast)
            .When(c => c.Buff(A.Overheated))
            .Because("overheated");

        p.Gcd(A.FullMetalField)
            .When(c => c.Buff(A.FullMetalMachinist))
            .Because("free Full Metal Field");

        p.Gcd(A.Excavator).When(c => c.Buff(A.ExcavatorReady));

        p.Gcd(A.ChainSaw).When(c => !c.Downtime);

        p.Gcd(A.AirAnchor).When(c => !c.Downtime);

        p.Gcd(A.Drill).When(c => !c.Downtime);

        p.Gcd(c => c.Has(A.HeatedCleanShot) ? A.HeatedCleanShot : A.CleanShot)
            .When(c => c.ComboIs(A.SlugShot) || c.ComboIs(A.HeatedSlugShot))
            .Because("combo finisher, builds battery");

        p.Gcd(c => c.Has(A.HeatedSlugShot) ? A.HeatedSlugShot : A.SlugShot)
            .When(c => c.ComboIs(A.SplitShot) || c.ComboIs(A.HeatedSplitShot));

        p.Gcd(c => c.Has(A.HeatedSplitShot) ? A.HeatedSplitShot : A.SplitShot);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.BarrelStabilizer).When(c => !c.Downtime && c.Mch.Heat <= 50);

        p.OGcd(A.Hypercharge)
            .When(c => !c.Downtime
                       && !c.Buff(A.Overheated)
                       && (c.Mch.Heat >= 50 || c.Buff(A.Hypercharged)))
            .Because("spend heat");

        p.OGcd(A.AutomatonQueen)
            .When(c => !c.Downtime && !c.Mch.RobotActive && c.Mch.Battery >= 50);

        p.OGcd(c => c.Has(A.DoubleCheck) ? A.DoubleCheck : A.GaussRound).When(c => !c.Downtime);
        p.OGcd(c => c.Has(A.Checkmate) ? A.Checkmate : A.Ricochet).When(c => !c.Downtime);

        // Wildfire is single target damage; it is deliberately not in the AoE list.

        p.Gcd(A.AutoCrossbow).When(c => c.Buff(A.Overheated)).Because("overheated");

        p.Gcd(A.FullMetalField).When(c => c.Buff(A.FullMetalMachinist));

        p.Gcd(A.Excavator).When(c => c.Buff(A.ExcavatorReady));

        p.Gcd(A.ChainSaw).When(c => !c.Downtime);

        p.Gcd(A.Bioblaster)
            .When(c => c.Enemies >= 3 && c.DotExpiring(A.BioblasterBuff))
            .Because("dot the pack");

        p.Gcd(A.AirAnchor).When(c => c.Enemies <= 3 && !c.Downtime);

        p.Gcd(A.Drill).When(c => c.Enemies <= 3 && !c.Downtime);

        p.Gcd(c => c.Has(A.Scattergun) ? A.Scattergun : A.SpreadShot);
    }

    /// <summary>
    /// True when a tool GCD is up or lands within roughly one global. Used to keep an
    /// overheat window from eating a tool.
    /// </summary>
    private static bool ToolReadySoon(RotationContext c) =>
        c.Buff(A.ExcavatorReady)
        || c.ReadyIn(A.ChainSaw, c.GcdTotal)
        || c.ReadyIn(A.AirAnchor, c.GcdTotal)
        || c.ReadyIn(A.Drill, c.GcdTotal);
}
