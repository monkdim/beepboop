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
        // already waiting to be spent, and wasteful to start one whose Blitz will land with
        // no damage window on it.
        p.OGcd(A.PerfectBalance)
            .When(c => !c.Downtime
                       && !c.Buff(A.PerfectBalanceBuff)
                       && c.Mnk.BeastChakraCount == 0
                       && (BlitzWouldLandInRiddleOfFire(c) || PerfectBalanceIsAboutToOvercap(c)))
            .Because(c => PerfectBalanceIsAboutToOvercap(c) && !BlitzWouldLandInRiddleOfFire(c)
                ? "bank Beast Chakra before a charge is lost"
                : "bank Beast Chakra for the damage window");

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
    /// What to press while Perfect Balance suspends forms, and which Blitz that builds.
    /// <para>
    /// This used to bank Opo-opo three times, every window, for ever. Opo-opo does hit
    /// hardest, so three of it is the strongest Blitz available in isolation - but three
    /// matching chakra is an Elixir Burst, which lights the Lunar Nadi, and lighting a Nadi
    /// that is already lit does nothing at all.
    /// </para>
    /// <para>
    /// Phantom Rush needs both Nadi, and the only thing that lights Solar is a Blitz of three
    /// *different* chakra. So a list that always builds matching chakra gets exactly one
    /// Phantom Rush - the one its scripted opener happens to set up - and never another. A
    /// recorded pull shows it precisely: Rising Phoenix and Elixir Burst in the opener, Phantom
    /// Rush at 00:49, and then Elixir Burst again at 01:30 and 02:05 with Lunar already lit
    /// both times. Two windows spent lighting a lamp that was on.
    /// </para>
    /// <para>
    /// So the windows alternate. With Lunar lit and Solar dark, build one of each chakra and
    /// take Rising Phoenix; otherwise build Opo-opo, which is both the hardest hitting and
    /// what lights Lunar for the pair. NadiFlags was already in the gauge and read by nothing.
    /// </para>
    /// </summary>
    private static ActionRef PerfectBalanceAction(RotationContext c)
    {
        if (WantsSolarNadi(c))
        {
            // One of each, in whatever order the window has not covered yet. Asked of the
            // gauge rather than counted, so a global pressed by hand mid-window does not put
            // the rest of the window out of step.
            if (!c.Mnk.HasOpoChakra)
                return OpoChakra(c);

            if (!c.Mnk.HasRaptorChakra)
                return RaptorChakra(c);

            if (!c.Mnk.HasCoeurlChakra)
                return CoeurlChakra(c);
        }

        return OpoChakra(c);
    }

    /// <summary>
    /// Whether this Perfect Balance window should be spent on three different chakra.
    /// <para>
    /// Only when Lunar is already lit and Solar is not: that is the one arrangement where a
    /// matching Blitz is worth nothing and a mixed one completes the pair for Phantom Rush.
    /// With neither lit, or both, Opo-opo is the harder hitting build.
    /// </para>
    /// <para>
    /// Below the level that has Rising Phoenix there are no Nadi to pair, and the flags read
    /// zero for ever - so this is false and the window builds Opo-opo, which is the rotation
    /// those levels actually have.
    /// </para>
    /// </summary>
    private static bool WantsSolarNadi(RotationContext c) =>
        c.Has(A.RisingPhoenix) && c.Mnk.HasLunarNadi && !c.Mnk.HasSolarNadi;

    /// <summary>The Opo-opo chakra: spend the fury if it is there, otherwise build it.</summary>
    private static ActionRef OpoChakra(RotationContext c) =>
        c.Mnk.OpoOpoFury > 0
            ? c.Has(A.LeapingOpo) ? A.LeapingOpo : A.Bootshine
            : A.DragonKick;

    /// <summary>The Raptor chakra, the same way round.</summary>
    private static ActionRef RaptorChakra(RotationContext c) =>
        c.Mnk.RaptorFury > 0
            ? c.Has(A.RisingRaptor) ? A.RisingRaptor : A.TrueStrike
            : A.TwinSnakes;

    /// <summary>And the Coeurl chakra.</summary>
    private static ActionRef CoeurlChakra(RotationContext c) =>
        c.Mnk.CoeurlFury > 0
            ? c.Has(A.PouncingCoeurl) ? A.PouncingCoeurl : A.SnapPunch
            : A.Demolish;

    /// <summary>
    /// How long a Perfect Balance window takes to pay out: three globals to bank the chakra
    /// and a fourth to spend the Blitz. Read off a recorded pull - Perfect Balance at 01:22.9,
    /// Blitz at 01:29.6 - rather than assumed from the global count, because the window is
    /// opened as a weave and the Blitz is a global, so it is never a whole number of them.
    /// </summary>
    private const float BlitzLead = 7f;

    /// <summary>
    /// How much of Riddle of Fire's cooldown may still be turning when the window opens. The
    /// list weaves Riddle of Fire the instant it is up and the Blitz is four globals out, so
    /// a couple of seconds is nothing - but it has to come up clearly *before* the Blitz, not
    /// alongside it, or the two race and the Blitz loses about half the time.
    /// </summary>
    private const float RiddleLead = 4f;

    /// <summary>
    /// Whether the Blitz this window builds will land inside Riddle of Fire.
    /// <para>
    /// Perfect Balance used to be pressed the moment it came off cooldown, with no idea a
    /// damage window was coming. A recorded pull shows what that costs: Phantom Rush - the
    /// hardest hitting button the job has - at 00:51.0 with no Riddle of Fire and no
    /// Brotherhood on it at all, and Elixir Burst at 01:29.6 three seconds after Riddle of
    /// Fire fell off. Both windows opened with the damage window twenty-odd seconds away or
    /// four seconds from ending.
    /// </para>
    /// <para>
    /// Brotherhood needs no rule of its own. It is two minutes to Riddle of Fire's one, so
    /// aligning to Riddle of Fire puts every second Blitz inside Brotherhood for free.
    /// </para>
    /// <para>
    /// Below the level that has Riddle of Fire there is no window to align to and the answer
    /// is always yes - a rung that cannot be climbed must not hang the phase.
    /// </para>
    /// </summary>
    private static bool BlitzWouldLandInRiddleOfFire(RotationContext c)
    {
        if (!c.Has(A.RiddleOfFire))
            return true;

        // Running: enough left for the Blitz to land before it drops.
        if (c.Buff(A.RiddleOfFireBuff))
            return c.BuffTime(A.RiddleOfFireBuff) >= BlitzLead;

        // Or about to be pressed, with the Blitz still four globals behind it.
        return c.ReadyIn(A.RiddleOfFire, RiddleLead);
    }

    /// <summary>
    /// Whether holding Perfect Balance any longer would throw a charge away.
    /// <para>
    /// The escape hatch on the rule above, and the reason it cannot deadlock. Waiting for a
    /// damage window is only ever worth it while the waiting is free: a charge sitting at the
    /// cap is a Blitz that will never happen, which is worse than a Blitz with no buff on it.
    /// It is also what keeps this honest if Riddle of Fire's timer ever reads wrong - the
    /// window still opens, just without the alignment.
    /// </para>
    /// </summary>
    private static bool PerfectBalanceIsAboutToOvercap(RotationContext c) =>
        c.Charges(A.PerfectBalance) >= c.MaxCharges(A.PerfectBalance);

    /// <summary>
    /// Everything the list actually reads, printed beside every cast.
    /// <para>
    /// Monk had no gauge line at all, and it cost two whole recorded pulls. Both showed
    /// Perfect Balance banking three Opo-opo when the list should have been asking for one
    /// of each, and with nothing but form buffs in the log there was no way to tell a Nadi
    /// that was read wrong from beast chakra that were - so the fix went to the wrong one
    /// first. The Red Mage combo bug was found the moment its gauge reached the log; this
    /// is the same line for the same reason.
    /// </para>
    /// </summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Monk;

        var nadi = (g.HasLunarNadi, g.HasSolarNadi) switch
        {
            (true, true) => "lunar+solar",
            (true, false) => "lunar",
            (false, true) => "solar",
            _ => "none",
        };

        var beast = g.BeastChakraCount == 0
            ? "none"
            : string.Join('+', OpenChakraNames(g));

        var blitz = g.BlitzTimeRemaining > 0f ? $" {g.BlitzTimeRemaining:0.0}s" : string.Empty;

        return $"chakra {g.Chakra} | beast {beast}{blitz} | nadi {nadi}"
               + $" | fury opo {g.OpoOpoFury} raptor {g.RaptorFury} coeurl {g.CoeurlFury}";
    }

    /// <summary>
    /// The beast chakra that are open, by name. Which ones, not how many: it is the missing
    /// one that decides what Perfect Balance asks for next.
    /// </summary>
    private static IEnumerable<string> OpenChakraNames(MonkGauge g)
    {
        if (g.HasOpoChakra)
            yield return "opo";

        if (g.HasRaptorChakra)
            yield return "raptor";

        if (g.HasCoeurlChakra)
            yield return "coeurl";
    }
}
