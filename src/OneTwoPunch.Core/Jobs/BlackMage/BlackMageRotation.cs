using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.BlackMage.BlackMageActions;

namespace OneTwoPunch.Core.Jobs.BlackMage;

/// <summary>
/// Black Mage, Dawntrail. Two elemental phases that feed each other: burn mana in Astral
/// Fire, restore it in Umbral Ice, and never let the element timer run out.
/// <para>
/// This is the job the movement handling exists for. Nearly every Black Mage cast roots you
/// in place, and the ones that do not - Xenoglossy, Paradox, the thunder line, anything
/// under Triplecast - are precisely what you want the instant you have to move. Those rules
/// sit above the hard casts, so the button becomes an instant the moment you start walking
/// and goes back on its own when you stop.
/// </para>
/// </summary>
public sealed class BlackMageRotation : JobRotationBase
{
    public override uint JobId => 25;

    public override string Name => "Black Mage";

    public override ActionRef SingleTargetButton => A.Fire1;

    public override ActionRef AoeButton => A.Fire2;

    public override float AoeRadius => 5f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override StatusRef? BurstStatus => A.LeyLinesBuff;

    public override ActionRef? BurstAction => A.LeyLines;

    /// <summary>
    /// Mana at or above which a phaseless Black Mage opens in fire rather than ice. Matches
    /// RotationSolver Reborn.
    /// </summary>
    private const uint NeutralFireMp = 7200;

    /// <summary>
    /// The Balance's Standard "5+7" opener, Black Mage level 100, Dawntrail patch 7.2,
    /// transcribed step for step from their chart rather than reasoned out.
    /// <para>
    /// Off-globals sit immediately after the global they are woven into, which is how the
    /// chart draws them: Swiftcast and Amplifier after High Thunder, the potion and Ley
    /// Lines after the first Fire IV, Manafont after Xenoglossy, Transpose and Triplecast
    /// after Despair, and the second Transpose between the two Paradoxes.
    /// </para>
    /// <para>
    /// The name is the shape of it - five Fire IVs before Xenoglossy, seven after - and the
    /// tail is the transition back to ice, ending on the Firestarter proc.
    /// </para>
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance standard 5+7", 100,
        A.Fire3,                                    //  1
        A.HighThunder,                              //  2
        A.Swiftcast, A.Amplifier,                   //     woven
        A.Fire4,                                    //  3
        A.LeyLines,                                 //     woven, with the potion
        A.Fire4,                                    //  4
        A.Fire4,                                    //  5
        A.Fire4,                                    //  6
        A.Fire4,                                    //  7
        A.Xenoglossy,                               //  8
        A.Manafont,                                 //     woven
        A.Fire4,                                    //  9
        A.FlareStar,                                // 10
        A.Fire4,                                    // 11
        A.Fire4,                                    // 12
        A.HighThunder,                              // 13
        A.Fire4,                                    // 14
        A.Fire4,                                    // 15
        A.Fire4,                                    // 16
        A.Fire4,                                    // 17
        A.FlareStar,                                // 18
        A.Despair,                                  // 19
        A.Transpose, A.Triplecast,                  //     woven
        A.Blizzard3,                                // 20
        A.Blizzard4,                                // 21
        A.Paradox,                                  // 22
        A.Transpose,                                //     woven
        A.Paradox,                                  // 23
        A.Fire3)                                    // 24, the Firestarter proc
    {
        // The chart puts Gemdraught of Intelligence in the same weave window as Ley Lines,
        // which is the step after the first Fire IV.
        PotionBeforeStep = 5,
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
        p.OGcd(A.LeyLines).When(c => !c.Downtime && !c.Moving).Because("damage window");
        p.OGcd(A.Amplifier).When(c => c.Blm.PolyglotStacks < 2).Because("do not overcap Polyglot");

        p.OGcd(A.Manafont)
            .When(c => c.Blm.InAstralFire && !c.Downtime)
            .Because("refill mana without leaving Astral Fire");

        // Triplecast and Swiftcast are deliberately not suggested at all.
        //
        // They were, briefly, and the mechanism worked - but a button that sometimes becomes
        // Triplecast competes with the key the player has bound to Triplecast, and the loser
        // of that race is a wasted charge. The player asked for them back on their own keys
        // for the faster reaction, which is the right call: reacting to movement is a
        // half-second decision and a suggestion cannot beat a thumb that already knows.
        //
        // The rotation still notices them - see StuckMoving - so pressing one by hand keeps
        // the list casting Fire IV rather than dropping to the instants.

        // ---- GCDs --------------------------------------------------------
        // Everything instant, first, while moving.
        p.Gcd(A.Xenoglossy)
            .When(c => StuckMoving(c) && c.Blm.PolyglotStacks > 0)
            .Because("instant, you are moving");

        p.Gcd(A.Paradox)
            .When(c => StuckMoving(c) && c.Blm.ParadoxActive)
            .Because("instant, you are moving");

        p.Gcd(c => ThunderAction(c))
            .When(c => StuckMoving(c) && c.Buff(A.Thunderhead))
            .Because("instant, you are moving");

        // The dot, refreshed on its proc so it never costs a cast.
        p.Gcd(c => ThunderAction(c))
            .When(c => c.Buff(A.Thunderhead)
                       && !c.Downtime
                       && (c.DotExpiring(A.HighThunderBuff, 3f) && c.DotExpiring(A.ThunderIII, 3f)))
            .Because("refresh the dot");

        // Polyglot caps, and each stack lost is a free instant thrown away.
        p.Gcd(A.Xenoglossy)
            .When(c => c.Blm.PolyglotStacks >= 2 || (c.Blm.PolyglotStacks > 0 && c.Buff(A.LeyLinesBuff)))
            .Because("Polyglot is close to capping");

        // ---- Astral Fire -------------------------------------------------
        p.Gcd(A.FlareStar)
            .When(c => c.Blm.AstralSoulStacks >= 6)
            .Because("Astral Soul is full");

        p.Gcd(A.Despair)
            .When(c => c.Blm.InAstralFire && c.Blm.AstralSoulStacks < 6 && !c.Ready(A.Fire4))
            .Because("last cast before Umbral Ice");

        p.Gcd(A.Fire4).When(c => c.Blm.InAstralFire);

        p.Gcd(A.Fire3)
            .When(c => c.Buff(A.Firestarter))
            .Because("free and instant");

        // ---- Umbral Ice --------------------------------------------------
        p.Gcd(A.Paradox).When(c => c.Blm.InUmbralIce && c.Blm.ParadoxActive);
        p.Gcd(A.Blizzard4).When(c => c.Blm.InUmbralIce && c.Blm.UmbralHearts < 3);

        // Back into fire once ice has done its job.
        p.Gcd(A.Fire3).When(c => c.Blm.InUmbralIce).Because("back to Astral Fire");

        // From neither phase - the pull, or after a death or a long downtime - which element
        // you open into is decided by mana, not by habit. With a full bar you open in fire;
        // this used to go into ice unconditionally, which is a wasted opener every time.
        // Threshold matches RotationSolver Reborn's.
        p.Gcd(A.Fire3)
            .When(c => !c.Blm.InAstralFire && !c.Blm.InUmbralIce && c.Mp >= NeutralFireMp)
            .Because("full mana, open in Astral Fire");

        // Anything that is not already ice goes to ice, and that has to include Astral Fire
        // itself. This asked for !InAstralFire, so a Black Mage who ran the bar dry in fire
        // had no rule left that could match: Fire IV and Despair both want mana, and the one
        // rule that could have escaped refused to fire precisely because it was in fire. The
        // list fell through to its fallback and cast Fire I for ever.
        p.Gcd(A.Blizzard3).When(c => !c.Blm.InUmbralIce).Because("into Umbral Ice");

        p.Gcd(A.Fire1);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.LeyLines).When(c => !c.Downtime && !c.Moving);
        p.OGcd(A.Amplifier).When(c => c.Blm.PolyglotStacks < 2);
        p.OGcd(A.Manafont).When(c => c.Blm.InAstralFire && !c.Downtime);
        p.Gcd(A.Foul)
            .When(c => StuckMoving(c) && c.Blm.PolyglotStacks > 0)
            .Because("instant, you are moving");

        p.Gcd(A.Foul)
            .When(c => c.Blm.PolyglotStacks >= 2)
            .Because("Polyglot is close to capping");

        p.Gcd(A.FlareStar).When(c => c.Blm.AstralSoulStacks >= 6).Because("Astral Soul is full");

        p.Gcd(A.Flare)
            .When(c => c.Blm.InAstralFire && !c.Ready(A.HighFire2))
            .Because("last cast before Umbral Ice");

        p.Gcd(c => c.Has(A.HighFire2) ? A.HighFire2 : A.Fire2).When(c => c.Blm.InAstralFire);

        p.Gcd(A.Freeze).When(c => c.Blm.InUmbralIce && c.Blm.UmbralHearts < 3);

        p.Gcd(c => c.Has(A.HighFire2) ? A.HighFire2 : A.Fire2)
            .When(c => c.Blm.InUmbralIce)
            .Because("back to Astral Fire");

        p.Gcd(c => c.Has(A.HighFire2) ? A.HighFire2 : A.Fire2)
            .When(c => !c.Blm.InAstralFire && !c.Blm.InUmbralIce && c.Mp >= NeutralFireMp)
            .Because("full mana, open in Astral Fire");

        p.Gcd(c => c.Has(A.HighBlizzard2) ? A.HighBlizzard2 : A.Blizzard2)
            .When(c => !c.Blm.InUmbralIce)
            .Because("into Umbral Ice");

        p.Gcd(c => c.Has(A.HighFire2) ? A.HighFire2 : A.Fire2);
    }

    /// <summary>The thunder the player actually has, which upgrades twice.</summary>
    /// <summary>Element, timer and the two resources every rule here turns on.</summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.BlackMage;

        var element = g.AstralFire > 0 ? $"fire {g.AstralFire}"
            : g.UmbralIce > 0 ? $"ice {g.UmbralIce}"
            : "neutral";

        return $"{element} {g.ElementTimeRemaining:0.0}s | hearts {g.UmbralHearts} "
               + $"| polyglot {g.PolyglotStacks} | soul {g.AstralSoulStacks}"
               + (g.ParadoxActive ? " | paradox" : string.Empty);
    }

    /// <summary>
    /// Moving, with nothing that would make the next cast instant.
    /// <para>
    /// Once Triplecast or Swiftcast is up the movement rules have to stand down. Otherwise
    /// a free instant gets spent on Xenoglossy while Fire IV sits there - which is the
    /// opposite of the point: the instants exist so the rotation continues while you move,
    /// not so the rotation is abandoned for the cheap spells.
    /// </para>
    /// </summary>
    private static bool StuckMoving(RotationContext c) =>
        c.Moving && !c.Buff(A.TriplecastBuff) && !c.Buff(A.SwiftcastBuff);

    private static ActionRef ThunderAction(RotationContext c) =>
        c.Has(A.HighThunder) ? A.HighThunder : A.Thunder3;
}
