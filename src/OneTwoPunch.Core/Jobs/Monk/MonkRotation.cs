using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Jobs.Monk;

/// <summary>
/// Monk, Dawntrail. Three forms cycle, and each form has two options: spend the form's fury
/// counter for the big hit, or use the other action to build it back. That single decision,
/// repeated three ways, is most of the job.
/// <para>
/// Perfect Balance suspends forms entirely and banks Beast Chakra; three chakra spend into
/// a Blitz, which the engine puts above everything else because it expires.
/// </para>
/// </summary>
public sealed class MonkRotation : JobRotationBase
{
    public override uint JobId => 20;

    public override string Name => "Monk";

    public override ActionRef SingleTargetButton => A.Bootshine;

    public override ActionRef AoeButton => A.ArmOfTheDestroyer;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? PositionalRescue => A.TrueNorth;

    public override StatusRef? PositionalRescueStatus => A.TrueNorthBuff;

    public override StatusRef? BurstStatus => A.BrotherhoodBuff;

    public override ActionRef? BurstAction => A.Brotherhood;

    /// <summary>
    /// The Balance's "Solar Lunar - DK Opener (5s Buffs)" for Monk level 100, Dawntrail
    /// patch 7.0. Perfect Balance after the first Dragon Kick feeds the solar blitz at
    /// step 5; the second one feeds Elixir Burst at step 13.
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance solar-lunar DK", 100,
        A.DragonKick, A.PerfectBalance,
        A.TwinSnakes,
        A.Demolish, A.Brotherhood, A.RiddleOfFire,
        A.LeapingOpo, A.ForbiddenChakra, A.RiddleOfWind,
        A.RisingPhoenix,
        A.DragonKick,
        A.WindsReply,
        A.FiresReply,
        A.LeapingOpo, A.PerfectBalance,
        A.DragonKick,
        A.LeapingOpo,
        A.DragonKick,
        A.ElixirBurst,
        A.LeapingOpo)
    {
        // The chart drinks in Twin Snakes' weave window, so the prompt belongs on Demolish.
        PotionBeforeStep = 3,
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

        // Second Wind before anything else once you are hurt. It is a two minute cooldown
        // that spends most of a fight doing nothing, and noticing the moment to press it is
        // exactly the attention this plugin exists to not need - so it takes the first weave
        // slot going. That costs a little damage, which is the trade being made on purpose.
        p.OGcd(A.SecondWind).When(c => c.Hurt).Because("you are hurt");

        // ---- Off-globals -------------------------------------------------
        p.OGcd(A.Brotherhood).When(c => !c.Downtime).Because("raid buff");
        p.OGcd(A.RiddleOfFire).When(c => !c.Downtime).Because("damage window");
        p.OGcd(A.RiddleOfWind).When(c => !c.Downtime);

        // Perfect Balance banks chakra for a Blitz; pointless to start one while a Blitz is
        // already waiting to be spent.
        p.OGcd(A.PerfectBalance)
            .When(c => !c.Downtime
                       && !c.Buff(A.PerfectBalanceBuff)
                       && c.Mnk.BeastChakraCount == 0)
            .Because("bank Beast Chakra");

        p.OGcd(A.ForbiddenChakra)
            .When(c => c.Mnk.Chakra >= 5)
            .Because("Chakra is full");

        // ---- GCDs --------------------------------------------------------
        // A Blitz expires, so it outranks the form cycle.
        p.Gcd(c => BlitzAction(c))
            .When(c => c.Mnk.BeastChakraCount >= 3)
            .Because("Beast Chakra is full");

        // Free follow-ups from Riddle of Fire and Riddle of Wind.
        p.Gcd(A.FiresReply).When(c => c.Buff(A.FiresRumination));
        p.Gcd(A.WindsReply).When(c => c.Buff(A.WindsRumination));

        // Under Perfect Balance forms are suspended, so we pick which chakra to bank.
        // Opo-opo is the strongest, so it is taken whenever its fury is already up.
        p.Gcd(c => PerfectBalanceAction(c))
            .When(c => c.Buff(A.PerfectBalanceBuff));

        // Coeurl form: spend the fury, or build it.
        p.Gcd(c => c.Has(A.PouncingCoeurl) ? A.PouncingCoeurl : A.SnapPunch)
            .When(c => c.Buff(A.CoeurlForm) && c.Mnk.CoeurlFury > 0)
            .Needs(PositionalHint.Flank);

        p.Gcd(A.Demolish)
            .When(c => c.Buff(A.CoeurlForm))
            .Needs(PositionalHint.Rear)
            .Because("build Coeurl fury");

        // Raptor form.
        p.Gcd(c => c.Has(A.RisingRaptor) ? A.RisingRaptor : A.TrueStrike)
            .When(c => c.Buff(A.RaptorForm) && c.Mnk.RaptorFury > 0);

        p.Gcd(A.TwinSnakes)
            .When(c => c.Buff(A.RaptorForm))
            .Because("build Raptor fury");

        // Opo-opo form, and the fallback when no form is up at all.
        p.Gcd(c => c.Has(A.LeapingOpo) ? A.LeapingOpo : A.Bootshine)
            .When(c => c.Mnk.OpoOpoFury > 0);

        p.Gcd(A.DragonKick).Because("build Opo-opo fury");

        p.Gcd(A.SixSidedStar)
            .When(c => !c.InRange)
            .Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        // Second Wind before anything else once you are hurt. It is a two minute cooldown
        // that spends most of a fight doing nothing, and noticing the moment to press it is
        // exactly the attention this plugin exists to not need - so it takes the first weave
        // slot going. That costs a little damage, which is the trade being made on purpose.
        p.OGcd(A.SecondWind).When(c => c.Hurt).Because("you are hurt");

        p.OGcd(A.Brotherhood).When(c => !c.Downtime).Because("raid buff");
        p.OGcd(A.RiddleOfFire).When(c => !c.Downtime);
        p.OGcd(A.RiddleOfWind).When(c => !c.Downtime);

        p.OGcd(A.PerfectBalance)
            .When(c => !c.Downtime
                       && !c.Buff(A.PerfectBalanceBuff)
                       && c.Mnk.BeastChakraCount == 0);

        p.OGcd(A.Enlightenment)
            .When(c => c.Mnk.Chakra >= 5)
            .Because("Chakra is full");

        p.Gcd(c => BlitzAction(c))
            .When(c => c.Mnk.BeastChakraCount >= 3)
            .Because("Beast Chakra is full");

        p.Gcd(A.FiresReply).When(c => c.Buff(A.FiresRumination));
        p.Gcd(A.WindsReply).When(c => c.Buff(A.WindsRumination));

        p.Gcd(A.Rockbreaker).When(c => c.Buff(A.CoeurlForm) || c.Buff(A.PerfectBalanceBuff));
        p.Gcd(A.FourPointFury).When(c => c.Buff(A.RaptorForm));

        p.Gcd(c => c.Has(A.ShadowOfTheDestroyer) ? A.ShadowOfTheDestroyer : A.ArmOfTheDestroyer);
    }

    /// <summary>
    /// Which Blitz the banked chakra produced. The game decides this from the chakra opened,
    /// so all four are offered and the first the game will accept wins - Phantom Rush and
    /// Elixir Burst being the ones worth having.
    /// </summary>
    private static ActionRef BlitzAction(RotationContext c)
    {
        if (c.Has(A.PhantomRush) && c.Ready(A.PhantomRush))
            return A.PhantomRush;

        if (c.Has(A.ElixirBurst) && c.Ready(A.ElixirBurst))
            return A.ElixirBurst;

        if (c.Has(A.RisingPhoenix) && c.Ready(A.RisingPhoenix))
            return A.RisingPhoenix;

        if (c.Has(A.ElixirField) && c.Ready(A.ElixirField))
            return A.ElixirField;

        if (c.Has(A.FlintStrike) && c.Ready(A.FlintStrike))
            return A.FlintStrike;

        return A.MasterfulBlitz;
    }

    /// <summary>
    /// What to press while Perfect Balance suspends forms. Opo-opo hits hardest, so its
    /// chakra is banked whenever the fury to spend is already there; otherwise build it.
    /// </summary>
    private static ActionRef PerfectBalanceAction(RotationContext c)
    {
        if (c.Mnk.OpoOpoFury > 0)
            return c.Has(A.LeapingOpo) ? A.LeapingOpo : A.Bootshine;

        return A.DragonKick;
    }
}
