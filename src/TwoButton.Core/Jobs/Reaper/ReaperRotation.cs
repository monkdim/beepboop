using TwoButton.Core.Engine;
using TwoButton.Core.Model;
using A = TwoButton.Core.Jobs.Reaper.ReaperActions;

namespace TwoButton.Core.Jobs.Reaper;

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

        p.OGcd(A.Enshroud)
            .When(c => c.Rpr.Shroud >= 50 && !c.Rpr.Enshrouded && !c.Buff(A.SoulReaver))
            .Because("spend Shroud");

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

        p.OGcd(A.Enshroud)
            .When(c => c.Rpr.Shroud >= 50 && !c.Rpr.Enshrouded && !c.Buff(A.SoulReaver));

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
