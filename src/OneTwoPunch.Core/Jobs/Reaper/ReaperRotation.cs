using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Reaper.ReaperActions;

namespace OneTwoPunch.Core.Jobs.Reaper;

/// <summary>
/// Reaper, Dawntrail. Two resources feeding two different states: Soul spends into Soul
/// Reaver (Gibbet / Gallows), Shroud spends into Enshroud (Void / Cross Reaping into
/// Communio). The engine's state checks come before the filler combo, so whichever state
/// the player is in takes over the button entirely.
/// </summary>
public sealed class ReaperRotation : JobRotationBase
{
    public override uint JobId => 39;

    public override string Name => "Reaper";

    public override ActionRef SingleTargetButton => A.Slice;

    public override ActionRef AoeButton => A.SpinningScythe;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? PositionalRescue => A.TrueNorth;

    public override StatusRef? PositionalRescueStatus => A.TrueNorthBuff;

    public override StatusRef? BurstStatus => A.ArcaneCircleBuff;

    public override ActionRef? BurstAction => A.ArcaneCircle;

    /// <summary>
    /// The Balance's "Opener - 2nd GCD AC" for Reaper level 100, Dawntrail patch 7.0.
    /// Arcane Circle and the potion are both late-weaved after Soul Slice, which is what
    /// puts the buff over the Executioner pair rather than over Shadow of Death.
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance 2nd-GCD Arcane Circle", 100,
        A.Harpe,
        A.ShadowofDeath,
        A.SoulSlice, A.ArcaneCircle, A.Gluttony,
        A.ExecutionersGibbet,
        A.ExecutionersGallows,
        A.SoulSlice,
        A.PlentifulHarvest, A.Enshroud,
        A.VoidReaping, A.Sacrificium,
        A.CrossReaping, A.LemuresSlice,
        A.VoidReaping,
        A.CrossReaping, A.LemuresSlice,
        A.Communio,
        A.Perfectio, A.UnveiledGibbet,
        A.Gibbet,
        A.ShadowofDeath,
        A.Slice)
    {
        // Drunk alongside Arcane Circle, which the chart weaves after Soul Slice.
        PotionBeforeStep = 2,
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
        p.OGcd(A.ArcaneCircle)
            .When(c => !c.Downtime)
            .Because("raid buff");

        // Two Soul Reavers for one 50 Soul, so it beats spending Soul any other way.
        p.OGcd(A.Gluttony)
            .When(c => c.Rpr.Soul >= 50 && !c.Buff(A.SoulReaver) && !c.Buff(A.Executioner))
            .Because("two reavers for one spend");

        // Ideal Host is a free Enshroud - Arcane Circle leaves it behind, and it does not
        // care what the Shroud gauge says. Asking for 50 Shroud missed it every single time:
        // a recorded pull has Ideal Host counting down from 29s to 3s untouched while the
        // list built Soul instead, which is a whole Enshroud of damage dropped once every
        // two minutes. Reported as "not using enshroud twice during the boost, using it once
        // then holding and letting the second fall off".
        p.OGcd(A.Enshroud)
            .When(c => !c.Rpr.Enshrouded
                       && !c.Buff(A.SoulReaver)
                       && (c.Buff(A.IdealHost) || (c.Rpr.Shroud >= 50 && !PoolingForBurst(c))))
            .Because(c => c.Buff(A.IdealHost) ? "free Enshroud, do not waste it" : "spend Shroud");

        p.OGcd(A.LemuresSlice)
            .When(c => c.Rpr.VoidShroud >= 2)
            .Because("void shroud is full");

        p.OGcd(A.Sacrificium).When(c => c.Buff(A.Oblatio));

        // Soul caps at 100. Unveiled Gibbet / Gallows are the same spend as Blood Stalk but
        // pick the side we are already buffed for.
        p.OGcd(c => NextReaper(c))
            .When(c => c.Rpr.Soul >= 50
                       && !c.Buff(A.SoulReaver)
                       && !c.Rpr.Enshrouded
                       && !c.ReadyIn(A.Gluttony, c.GcdTotal * 2f))
            .Because("spend Soul before it caps");

        // ---- GCDs --------------------------------------------------------
        // Enshroud state first: it locks the GCD into its own line.
        p.Gcd(A.Perfectio).When(c => c.Buff(A.PerfectioParata));

        // Communio is a cast and closes the Enshroud window, so it waits for the last
        // lemure and for the player to be standing still.
        p.Gcd(A.Communio)
            .When(c => c.Rpr.Enshrouded && c.Rpr.LemureShroud == 1 && !c.Moving)
            .Because("closes Enshroud");

        p.Gcd(A.CrossReaping)
            .When(c => c.Rpr.Enshrouded && c.Buff(A.EnhancedCrossReaping));

        p.Gcd(A.VoidReaping)
            .When(c => c.Rpr.Enshrouded);

        // Soul Reaver state.
        p.Gcd(A.ExecutionersGallows)
            .When(c => c.Buff(A.Executioner) && c.Buff(A.EnhancedGallows))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.ExecutionersGibbet)
            .When(c => c.Buff(A.Executioner))
            .Needs(PositionalHint.Flank);

        p.Gcd(A.Gallows)
            .When(c => c.Buff(A.SoulReaver) && c.Buff(A.EnhancedGallows))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.Gibbet)
            .When(c => c.Buff(A.SoulReaver))
            .Needs(PositionalHint.Flank);

        p.Gcd(A.PlentifulHarvest)
            .When(c => c.Buff(A.ImmortalSacrifice) && !c.Rpr.Enshrouded)
            .Because("Immortal Sacrifice");

        // The debuff multiplies everything else, so it is refreshed before it drops rather
        // than after.
        p.Gcd(A.ShadowofDeath)
            .When(c => c.DotExpiring(A.DeathsDesign, 6f) && !c.Downtime)
            .Because("keep Death's Design up");

        // Soul Slice holds two charges; used at 50 or less so its 50 Soul never overcaps.
        p.Gcd(A.SoulSlice)
            .When(c => c.Rpr.Soul <= 50)
            .Because("build Soul");

        p.Gcd(A.InfernalSlice).When(c => c.ComboIs(A.WaxingSlice));
        p.Gcd(A.WaxingSlice).When(c => c.ComboIs(A.Slice));
        p.Gcd(A.Slice);

        p.Gcd(A.Harpe)
            .When(c => !c.InRange)
            .Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.ArcaneCircle).When(c => !c.Downtime).Because("raid buff");

        p.OGcd(A.Gluttony)
            .When(c => c.Rpr.Soul >= 50 && !c.Buff(A.SoulReaver) && !c.Buff(A.Executioner));

        // See the single-target list: Ideal Host is a free Enshroud regardless of gauge.
        p.OGcd(A.Enshroud)
            .When(c => (c.Rpr.Shroud >= 50 || c.Buff(A.IdealHost))
                       && !c.Rpr.Enshrouded
                       && !c.Buff(A.SoulReaver));

        p.OGcd(A.LemuresScythe).When(c => c.Rpr.VoidShroud >= 2);

        p.OGcd(A.Sacrificium).When(c => c.Buff(A.Oblatio));

        p.OGcd(A.GrimSwathe)
            .When(c => c.Rpr.Soul >= 50 && !c.Buff(A.SoulReaver) && !c.Rpr.Enshrouded)
            .Because("spend Soul before it caps");

        p.Gcd(A.Perfectio).When(c => c.Buff(A.PerfectioParata));

        p.Gcd(A.Communio)
            .When(c => c.Rpr.Enshrouded && c.Rpr.LemureShroud == 1 && !c.Moving);

        p.Gcd(A.GrimReaping).When(c => c.Rpr.Enshrouded);

        p.Gcd(A.ExecutionersGuillotine).When(c => c.Buff(A.Executioner));
        p.Gcd(A.Guillotine).When(c => c.Buff(A.SoulReaver));

        p.Gcd(A.PlentifulHarvest)
            .When(c => c.Buff(A.ImmortalSacrifice) && !c.Rpr.Enshrouded);

        p.Gcd(A.WhorlofDeath)
            .When(c => c.DotExpiring(A.DeathsDesign, 6f) && !c.Downtime)
            .Because("keep Death's Design up");

        p.Gcd(A.SoulScythe).When(c => c.Rpr.Soul <= 50).Because("build Soul");

        p.Gcd(A.NightmareScythe).When(c => c.ComboIs(A.SpinningScythe));
        p.Gcd(A.SpinningScythe);
    }

    /// <summary>
    /// How long before Arcane Circle that Shroud stops being spent and starts being saved.
    /// <para>
    /// A judgement call, not a number from anywhere: long enough to bank the fifty Shroud a
    /// second Enshroud needs, short enough that Shroud is not sat on for most of a minute.
    /// </para>
    /// </summary>
    private const float ShroudPoolWindow = 25f;

    /// <summary>
    /// Whether Shroud is being saved for the burst rather than spent now.
    /// <para>
    /// The two-minute window wants two Enshrouds: the free one Arcane Circle leaves behind,
    /// and a paid one out of banked Shroud. Spending at fifty the moment it is reached means
    /// the gauge is never near a hundred when the burst arrives, and a recorded pull shows
    /// exactly that - Enshroud at 00:50 and 01:43, both well outside a buff window, and then
    /// only the free one inside each burst.
    /// </para>
    /// <para>
    /// Held only while there is room to hold: at a hundred Shroud every further Reaver spend
    /// is thrown away, which costs more than a badly timed Enshroud.
    /// </para>
    /// </summary>
    private static bool PoolingForBurst(RotationContext c) =>
        FreeEnshroudIsComing(c)
        || (!c.Buff(A.ArcaneCircleBuff)
            && c.Rpr.Shroud < 100
            && c.ReadyIn(A.ArcaneCircle, ShroudPoolWindow));

    /// <summary>
    /// Whether the free Enshroud is close enough that paying for one now would waste it.
    /// <para>
    /// Immortal Sacrifice means Plentiful Harvest is waiting, and Plentiful Harvest is what
    /// leaves Ideal Host behind. Enshroud has a fifteen second cooldown, so paying for one in
    /// the seconds before that lands does not buy an extra Enshroud - it blocks the free one
    /// for most of the buff window and usually loses it outright.
    /// </para>
    /// <para>
    /// This is the case the hundred-Shroud release above would otherwise walk straight into:
    /// Plentiful Harvest's own fifty Shroud is what pushes the gauge to a hundred, and the
    /// release fires a paid Enshroud on exactly the GCD the free one was meant for.
    /// </para>
    /// </summary>
    private static bool FreeEnshroudIsComing(RotationContext c) =>
        c.Buff(A.ImmortalSacrifice) && !c.Buff(A.IdealHost);

    /// <summary>Soul and Shroud are the whole rotation, so the recorder gets to see them.</summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Reaper;
        var shroud = g.Enshrouded
            ? $"enshrouded {g.EnshroudTimeRemaining:0.0}s, lemure {g.LemureShroud}, void {g.VoidShroud}"
            : $"shroud {g.Shroud}";

        return $"soul {g.Soul} | {shroud}";
    }

    /// <summary>
    /// Which Soul spend to use. Unveiled Gibbet and Gallows cost the same as Blood Stalk but
    /// set up the side the player is already buffed for, so the follow-up keeps its bonus.
    /// </summary>
    private static ActionRef NextReaper(RotationContext c)
    {
        if (c.Buff(A.EnhancedGallows) && c.Has(A.UnveiledGallows))
            return A.UnveiledGallows;

        if (c.Buff(A.EnhancedGibbet) && c.Has(A.UnveiledGibbet))
            return A.UnveiledGibbet;

        return A.BloodStalk;
    }
}
