using TwoButton.Core.Engine;
using TwoButton.Core.Model;
using A = TwoButton.Core.Jobs.Viper.ViperActions;

namespace TwoButton.Core.Jobs.Viper;

/// <summary>
/// Viper, Dawntrail. Every combo step hands out a venom buff that names the next one, so
/// the rotation is mostly "read the buff you were just given". Two self-buffs have to stay
/// up underneath that, and Reawaken replaces the whole bar while it runs.
/// <para>
/// The follow-up actions - Serpent's Tail, Twinfang, Twinblood - are offered as their base
/// ids and left for the game to adjust into Death Rattle, Twinfang Bite, Legacy and the
/// rest. That is what the game does for the real hotbar too, so there is no second copy of
/// those rules here to drift.
/// </para>
/// </summary>
public sealed class ViperRotation : JobRotationBase
{
    public override uint JobId => 41;

    public override string Name => "Viper";

    public override ActionRef SingleTargetButton => A.SteelFangs;

    public override ActionRef AoeButton => A.SteelMaw;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? PositionalRescue => A.TrueNorth;

    public override StatusRef? PositionalRescueStatus => A.TrueNorthBuff;

    public override ActionRef? BurstAction => A.SerpentsIre;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        p.OGcd(A.SerpentsIre).When(c => !c.Downtime).Because("burst window");

        // The game turns these into whichever follow-up is live, and only accepts them when
        // one is - so Ready() alone is the correct guard.
        p.OGcd(A.SerpentsTail);
        p.OGcd(A.Twinfang);
        p.OGcd(A.Twinblood);

        // ---- GCDs --------------------------------------------------------
        // Reawaken replaces the bar and its tribute drains, so it outranks everything.
        p.Gcd(A.Ouroboros)
            .When(c => c.Vpr.AnguineTribute == 1)
            .Because("Reawaken finisher");

        p.Gcd(c => GenerationAction(c))
            .When(c => c.Vpr.AnguineTribute > 0);

        p.Gcd(A.Reawaken)
            .When(c => !c.Downtime
                       && (c.Vpr.SerpentOffering >= 50 || c.Buff(A.ReawakenReady))
                       && c.ComboBroken)
            .Because("spend Serpent Offering");

        // Vicewinder's two coils, each with their own positional.
        p.Gcd(A.SwiftskinsCoil)
            .When(c => c.ComboIs(A.Vicewinder) && NeedsSwiftscaled(c))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.HuntersCoil)
            .When(c => c.ComboIs(A.Vicewinder))
            .Needs(PositionalHint.Flank);

        p.Gcd(A.SwiftskinsCoil)
            .When(c => c.ComboIs(A.HuntersCoil))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.HuntersCoil)
            .When(c => c.ComboIs(A.SwiftskinsCoil))
            .Needs(PositionalHint.Flank);

        p.Gcd(A.Vicewinder)
            .When(c => c.ComboBroken && !c.Downtime);

        // Combo finishers. Each is named outright by the venom buff the previous step gave.
        p.Gcd(A.FlankstingStrike).When(c => c.Buff(A.FlankstungVenom)).Needs(PositionalHint.Flank);
        p.Gcd(A.FlanksbaneFang).When(c => c.Buff(A.FlanksbaneVenom)).Needs(PositionalHint.Flank);
        p.Gcd(A.HindstingStrike).When(c => c.Buff(A.HindstungVenom)).Needs(PositionalHint.Rear);
        p.Gcd(A.HindsbaneFang).When(c => c.Buff(A.HindsbaneVenom)).Needs(PositionalHint.Rear);

        // Second step. Which one depends on the self-buff that is closer to dropping -
        // losing either costs damage on everything after it.
        p.Gcd(A.SwiftskinsSting)
            .When(c => ComboStarted(c) && NeedsSwiftscaled(c))
            .Because("refresh Swiftscaled");

        p.Gcd(A.HuntersSting)
            .When(c => ComboStarted(c))
            .Because("refresh Hunter's Instinct");

        // Uncoiled Fury is ranged and instant, so it doubles as the movement option.
        p.Gcd(A.UncoiledFury)
            .When(c => c.Vpr.RattlingCoils > 0 && (c.Moving || !c.InRange || c.Vpr.RattlingCoils >= 3))
            .Because(c => c.Moving ? "instant, you are moving" : "coils are close to capping");

        p.Gcd(A.ReavingFangs).When(c => c.Buff(A.HonedReavers));
        p.Gcd(A.SteelFangs);

        p.Gcd(A.WrithingSnap)
            .When(c => !c.InRange)
            .Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.SerpentsIre).When(c => !c.Downtime).Because("burst window");
        p.OGcd(A.SerpentsTail);
        p.OGcd(A.Twinfang);
        p.OGcd(A.Twinblood);

        p.Gcd(A.Ouroboros).When(c => c.Vpr.AnguineTribute == 1);
        p.Gcd(c => GenerationAction(c)).When(c => c.Vpr.AnguineTribute > 0);

        p.Gcd(A.Reawaken)
            .When(c => !c.Downtime
                       && (c.Vpr.SerpentOffering >= 50 || c.Buff(A.ReawakenReady))
                       && c.ComboBroken);

        p.Gcd(A.SwiftskinsDen).When(c => c.ComboIs(A.Vicepit) && NeedsSwiftscaled(c));
        p.Gcd(A.HuntersDen).When(c => c.ComboIs(A.Vicepit));
        p.Gcd(A.SwiftskinsDen).When(c => c.ComboIs(A.HuntersDen));
        p.Gcd(A.HuntersDen).When(c => c.ComboIs(A.SwiftskinsDen));

        p.Gcd(A.Vicepit).When(c => c.ComboBroken && !c.Downtime);

        p.Gcd(A.JaggedMaw).When(c => c.Buff(A.GrimhuntersVenom));
        p.Gcd(A.BloodiedMaw).When(c => c.Buff(A.GrimskinsVenom));

        p.Gcd(A.SwiftskinsBite)
            .When(c => (c.ComboIs(A.SteelMaw) || c.ComboIs(A.ReavingMaw)) && NeedsSwiftscaled(c));

        p.Gcd(A.HuntersBite).When(c => c.ComboIs(A.SteelMaw) || c.ComboIs(A.ReavingMaw));

        p.Gcd(A.UncoiledFury)
            .When(c => c.Vpr.RattlingCoils >= 3)
            .Because("coils are close to capping");

        p.Gcd(A.ReavingMaw).When(c => c.Buff(A.HonedReavers));
        p.Gcd(A.SteelMaw);
    }

    private static bool ComboStarted(RotationContext c) =>
        c.ComboIs(A.SteelFangs) || c.ComboIs(A.ReavingFangs);

    /// <summary>
    /// True when Swiftscaled is the buff closer to dropping. Both self-buffs multiply
    /// everything that follows, so whichever is shorter wins the branch.
    /// </summary>
    private static bool NeedsSwiftscaled(RotationContext c) =>
        c.BuffTime(A.Swiftscaled) <= c.BuffTime(A.HuntersInstinct);

    /// <summary>
    /// The Reawaken chain. The game only accepts the current step, so all four are offered
    /// and Ready() picks the live one rather than us tracking the position ourselves.
    /// </summary>
    private static ActionRef GenerationAction(RotationContext c)
    {
        if (c.Ready(A.FirstGeneration))
            return A.FirstGeneration;

        if (c.Ready(A.SecondGeneration))
            return A.SecondGeneration;

        if (c.Ready(A.ThirdGeneration))
            return A.ThirdGeneration;

        return A.FourthGeneration;
    }
}
