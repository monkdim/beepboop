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
    /// Mana at which Umbral Ice has done everything it is for and the phase can end. Short of
    /// a full bar on purpose: waiting for the exact cap costs a global for the last few
    /// hundred mana that Paradox is about to hand back anyway.
    /// </summary>
    private const uint IceExitMp = 9600;

    /// <summary>
    /// How close to expiry the dot has to be before an instant refresh is worth taking during
    /// movement. Wider than the standing refresh window - moving is the one time spending a
    /// global on the dot early is defensible - but not so wide that a fresh dot gets clipped.
    /// </summary>
    private const float MovementRefreshWindow = 10f;

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

        // Held until Despair has actually been cast. Manafont refills the bar, so spending it
        // while there is still enough mana for a Despair buys nothing and costs the Despair
        // that mana would have paid for - a recorded fight has this six times in one pull,
        // reported as "Manafont was used before Despair". Despair being uncastable is the
        // signal that the bar is spent, and it accounts for the mana cost by itself.
        p.OGcd(A.Manafont)
            .When(c => c.Blm.InAstralFire && !c.Downtime && !c.Ready(A.Despair))
            .Because("the bar is spent, refill it without leaving Astral Fire");

        // ---- Leaving a phase ---------------------------------------------
        // Transpose was never suggested at all, and both weakened casts in the log come from
        // that one omission. Blizzard III cast in Astral Fire and Fire III cast in Umbral Ice
        // are both damage-penalised; Transpose crosses the same gap for free, off the global.
        // A recorded fight has nine of each.
        p.OGcd(A.Transpose)
            .When(c => c.Blm.InAstralFire
                       && c.Blm.AstralSoulStacks < 6
                       && !c.Blm.ParadoxActive
                       && !c.Buff(A.Firestarter)
                       && !c.Ready(A.Fire4)
                       && !c.Ready(A.Despair))
            .Because("fire is spent, cross to ice without a weakened Blizzard III");

        p.OGcd(A.Transpose)
            .When(c => IceHasDoneItsJob(c) && !c.Blm.ParadoxActive)
            .Because("ice has done its job, cross without a weakened Fire III");

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

        // Only when the dot is actually near the end. This used to fire on any movement with
        // a proc up, which refreshes a dot that has twenty-five seconds left - an average of
        // 12.6 seconds of High Thunder clipped per minute in a recorded fight. Moving with a
        // healthy dot now falls through to the hard casts, which is what slidecasting is for.
        p.Gcd(c => ThunderAction(c))
            .When(c => StuckMoving(c) && c.Buff(A.Thunderhead) && ThunderIsRunningOut(c, MovementRefreshWindow))
            .Because("instant, you are moving");

        // The dot, refreshed on its proc so it never costs a cast.
        p.Gcd(c => ThunderAction(c))
            .When(c => c.Buff(A.Thunderhead) && !c.Downtime && ThunderIsRunningOut(c, 3f))
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

        // Above Fire IV on purpose. A marker made in Astral Fire - by Manafont, or by crossing
        // into fire - had no rule that could spend it, so the next one overwrote it: nine
        // times in one recorded fight. It is instant, it refreshes the timer, and it leaves
        // Firestarter behind for a free Fire III.
        p.Gcd(A.Paradox)
            .When(c => c.Blm.InAstralFire && c.Blm.ParadoxActive)
            .Because("spend Paradox before it is overwritten");

        // The same rung on the other side, and it was missing for the same reason.
        //
        // Transpose crosses into Astral Fire *one*, exactly as it crosses into Umbral Ice
        // one, and Fire IV at Astral Fire I is a fraction of what it is at three. A recorded
        // level 90 pull has twenty-one globals at "fire 1" - both ice exits Transposed, and
        // then the whole phase ran at the bottom rung.
        //
        // Fire III is what climbs, it is full damage in fire, and it is free and instant when
        // the Paradox above has just left a Firestarter behind - which is the whole shape of
        // the crossing: Transpose, Paradox, Fire III, and you arrive at three with a full bar.
        p.Gcd(A.Fire3)
            .When(c => c.Blm.InAstralFire && c.Blm.AstralFire < 3)
            .Because(c => c.Buff(A.Firestarter)
                ? "up to Astral Fire III, free and instant"
                : "up to Astral Fire III");

        p.Gcd(A.Fire4).When(c => c.Blm.InAstralFire);

        p.Gcd(A.Fire3)
            .When(c => c.Buff(A.Firestarter))
            .Because("free and instant");

        // ---- Umbral Ice --------------------------------------------------
        p.Gcd(A.Paradox).When(c => c.Blm.InUmbralIce && c.Blm.ParadoxActive);

        // The rung that was missing, and it cost most of a bar every cycle.
        //
        // Transpose crosses into Umbral Ice *one*, not three, and mana only comes back at
        // any rate worth having at three. Nothing here climbed: hearts arrive from Blizzard
        // IV at any ice level, so the list filled them, saw its work done and went straight
        // back to fire. A recorded pull shows every ice phase after the opener stuck at
        // "ice 1" with mana peaking at 3500, against "ice 3" and a full 10000 in the opener
        // - which is a third of a fire phase, every cycle.
        //
        // Blizzard III is what climbs, and cast here it is in ice rather than in fire, which
        // is the whole reason for Transposing in the first place.
        p.Gcd(A.Blizzard3)
            .When(c => c.Blm.InUmbralIce && c.Blm.UmbralIce < 3)
            .Because("up to Umbral Ice III, where the mana is");

        p.Gcd(A.Blizzard4).When(c => c.Blm.InUmbralIce && c.Blm.UmbralHearts < 3);

        // Back into fire, but only once ice has actually done its job. Leaving on a third of
        // a bar is what the missing rung above caused, and this is the guard that says so out
        // loud rather than relying on the rules above to run out.
        p.Gcd(A.Fire3).When(c => IceHasDoneItsJob(c)).Because("back to Astral Fire");

        // Waiting on the last mana tick with the ice rungs and hearts already full. Without
        // this the list has nothing left that matches in ice and falls all the way through to
        // Fire I, which in Umbral Ice is about the worst global available.
        p.Gcd(A.Blizzard4).When(c => c.Blm.InUmbralIce).Because("waiting for the bar to fill");

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

    /// <summary>
    /// Whether the thunder dot is within <paramref name="within"/> seconds of dropping.
    /// <para>
    /// Both forms are asked about because which one is on the target depends on level, and a
    /// dot that is not there at all reads as zero - which is correctly "running out".
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether Umbral Ice has done everything it is for: at the third rung, hearts filled,
    /// and the bar actually refilled.
    /// <para>
    /// All three, because any one of them alone is a way to leave early. Hearts fill at any
    /// ice level, so hearts alone let the list cross back on a third of a bar; the rung
    /// alone says nothing about mana; and mana alone would leave the hearts behind that the
    /// next fire phase spends.
    /// </para>
    /// </summary>
    private static bool IceHasDoneItsJob(RotationContext c) =>
        c.Blm.InUmbralIce
        && c.Blm.UmbralIce >= 3
        && c.Blm.UmbralHearts >= 3
        && c.Mp >= IceExitMp;

    private static bool ThunderIsRunningOut(RotationContext c, float within) =>
        c.DotExpiring(A.HighThunderBuff, within) && c.DotExpiring(A.ThunderIII, within);
}
