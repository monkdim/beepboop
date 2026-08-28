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
    /// Mana at or below which the bar counts as spent for Manafont's purposes.
    /// <para>
    /// Above level 72 "Despair will not cast" says this by itself, and says it exactly - but
    /// below 72 there is no Despair to ask, so that test is trivially true and Manafont was
    /// being thrown on a full bar the moment the pull started. A recorded level 50 pull has
    /// it at 8400 mana and a level 68 pull at 8000, both labelled "the bar is spent".
    /// </para>
    /// <para>
    /// The number is one Fire I in Astral Fire, so the floor means "there is not another
    /// cast left in this bar". At 100 it changes nothing: Despair costs 800, so the bar is
    /// already under this by the time Despair refuses.
    /// </para>
    /// </summary>
    private const uint ManafontMp = 2000;

    /// <summary>
    /// Mana at which the AoE ice phase has done its job and Transpose can take the loop back
    /// into Astral Fire.
    /// <para>
    /// Nothing like the single-target bar. Two Flares is two Flares whether you brought ten
    /// thousand mana or three: the first consumes two thirds of what you have (the Umbral
    /// Hearts pay the other third) and the second consumes the rest, and neither cast is
    /// worth more for being fed more. So the ice phase is over the moment it has bought
    /// three hearts and enough mana that the second Flare is a real cast, which is why the
    /// chart's ice phase is two globals rather than a full refill.
    /// </para>
    /// <para>
    /// Deliberately not a stack count. Umbral Ice entered by Transpose is one stack, and a
    /// rung that cannot be climbed is how the ice phase got stuck at level 18.
    /// </para>
    /// </summary>
    private const uint AoeIceExitMp = 6000;

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
            .When(c => c.Blm.InAstralFire && !c.Downtime && c.Mp <= ManafontMp && !c.Ready(A.Despair))
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
                       && FireIsSpent(c))
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
        // The proc is required to keep it *instant*, which is what the movement rule above
        // is for - but it is not required to keep the dot up. Thunderhead arrives with a
        // trait partway up the job, and gating this on it as well meant a synced-down Black
        // Mage never cast Thunder at all: the whole dot, missing, for a whole dungeon.
        p.Gcd(c => ThunderAction(c))
            .When(c => ThunderProcHeldOrNotNeeded(c) && !c.Downtime && ThunderIsRunningOut(c, 3f))
            .Because("refresh the dot");

        // Only at the actual ceiling, and never for Ley Lines.
        //
        // This asked for two stacks when three is the ceiling from level 98, and it also
        // dumped a stack any time Ley Lines was up - on the premise that Ley Lines is a
        // damage window. It is not: it is fifteen percent haste and no damage at all, which
        // makes it the *worst* moment to spend an instant, because a cast there is already
        // as cheap as it ever gets.
        //
        // Between the two, a recorded seven-minute pull never once banked a stack: polyglot
        // read one for 127 of 201 samples, zero for 65, two for nine, and three never. Ninety
        // seconds of instants held for a mechanic, spent for nothing. Eleven of the sixteen
        // went out at "fire 3" - see the rule further down for why that is the expensive
        // place to spend one.
        p.Gcd(A.Xenoglossy)
            .When(c => c.Blm.PolyglotStacks >= PolyglotCap(c))
            .Because("Polyglot is about to overcap");

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
        // The chart puts this at its eighth global, after three Fire IVs - not the instant
        // the phase begins. Spending it on arrival costs the climb: Transpose lands on Astral
        // Fire one, and the global that belongs there is the free Fire III the Firestarter
        // pays for, which reaches three in a single cast.
        //
        // Three Astral Soul is exactly three Fire IVs, which is where the chart draws it. The
        // escape is for the bar running dry first: a marker that cannot be spent is one that
        // gets overwritten, which is the thing this rule was written for.
        p.Gcd(A.Paradox)
            .When(c => c.Blm.InAstralFire
                       && c.Blm.ParadoxActive
                       && (c.Blm.AstralSoulStacks >= 3 || !c.Ready(A.Fire4)))
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

        // Below level 60 there is no Fire IV, and with no Despair either the whole Astral
        // Fire section above had nothing that could match: the list fell straight past it to
        // "into Umbral Ice" and crossed back the global after arriving. A recorded level 50
        // pull spends its entire fire phase one global long and never casts Fire at all.
        //
        // Fire I is what the phase is at that level - it is the spell the bar exists to
        // spend - and once Fire IV is learned this rung is dead, which is what the level
        // test says rather than leaving it to the ordering above.
        p.Gcd(A.Fire1)
            .When(c => c.Blm.InAstralFire && !c.Has(A.Fire4))
            .Because("spend the bar in Astral Fire");

        p.Gcd(A.Fire3)
            .When(c => c.Buff(A.Firestarter))
            .Because("free and instant");

        // ---- Umbral Ice --------------------------------------------------
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
        // The climb has to stand down once the bar is full, or it outranks the rule that
        // leaves. Below level 20 the third rung does not exist to be reached - Astral Fire
        // and Umbral Ice both cap at one stack until Aspect Mastery raises it - so "climb to
        // three" is a condition that can never come true, and it sat above the exit. A
        // recorded Sastasha run is forty-four straight globals at "ice 1" with a full bar,
        // reported as being stuck in ice and never going back to fire.
        p.Gcd(c => IceSpell(c))
            .When(c => c.Blm.InUmbralIce && c.Blm.UmbralIce < 3 && !IceHasDoneItsJob(c))
            .Because("up to Umbral Ice III, where the mana is");

        p.Gcd(A.Blizzard4).When(c => c.Blm.InUmbralIce && c.Blm.UmbralHearts < 3);

        // Last of the three, which is where the chart draws it - Blizzard III, Blizzard IV,
        // Paradox - and not first. Cast here it is the bridge out: it restores the last of
        // the bar and leaves the Firestarter that makes the climb back to Astral Fire III a
        // single free instant. Cast on arrival instead, as this used to be, the bridge is
        // spent before there is anything to cross to.
        p.Gcd(A.Paradox).When(c => c.Blm.InUmbralIce && c.Blm.ParadoxActive);

        // Back into fire, but only once ice has actually done its job. Leaving on a third of
        // a bar is what the missing rung above caused, and this is the guard that says so out
        // loud rather than relying on the rules above to run out.
        p.Gcd(c => FireSpell(c)).When(c => IceHasDoneItsJob(c)).Because("back to Astral Fire");

        // Waiting on the last mana tick with the ice rungs and hearts already full. Without
        // this the list has nothing left that matches in ice and falls all the way through to
        // Fire I, which in Umbral Ice is about the worst global available.
        //
        // Blizzard IV is level 58, though, so naming it here left the rung dead below that
        // and the fall-through happened anyway. Blizzard I is free in ice and refreshes the
        // timer, which is the whole of what this rung is for - it does not climb, whatever
        // the rung above it hoped.
        // The one global in the rotation that exists only to pass time, and so the one place
        // a Polyglot costs almost nothing to spend.
        //
        // Xenoglossy is unaspected: 890 whether it is cast at Astral Fire III or standing in
        // a puddle. Fire IV is fire-aspected and worth far more than its own number once
        // Astral Fire is up, so a Xenoglossy in the fire phase is half a global thrown away -
        // and because the fire phase is bounded by mana rather than by time, it does not even
        // replace a Fire IV, it just makes the phase a global longer for the same six. The
        // recorded pull has both phases side by side: the one carrying three Xenoglossies
        // took twelve globals to deliver six Fire IVs, the one carrying none took ten.
        //
        // Here it displaces a Blizzard IV cast purely to watch the bar tick up, and it holds
        // a stack back so there is still an instant in the bank when a mechanic asks for one.
        p.Gcd(A.Xenoglossy)
            .When(c => c.Blm.InUmbralIce && c.Blm.PolyglotStacks >= 2)
            .Because("spend it where the phase is only marking time");

        p.Gcd(c => c.Has(A.Blizzard4) ? A.Blizzard4 : A.Blizzard1)
            .When(c => c.Blm.InUmbralIce)
            .Because("waiting for the bar to fill");

        // From neither phase - the pull, or after a death or a long downtime - which element
        // you open into is decided by mana, not by habit. With a full bar you open in fire;
        // this used to go into ice unconditionally, which is a wasted opener every time.
        // Threshold matches RotationSolver Reborn's.
        p.Gcd(c => FireSpell(c))
            .When(c => !c.Blm.InAstralFire && !c.Blm.InUmbralIce && c.Mp >= NeutralFireMp)
            .Because("full mana, open in Astral Fire");

        // Anything that is not already ice goes to ice, and that has to include Astral Fire
        // itself. This asked for !InAstralFire, so a Black Mage who ran the bar dry in fire
        // had no rule left that could match: Fire IV and Despair both want mana, and the one
        // rule that could have escaped refused to fire precisely because it was in fire. The
        // list fell through to its fallback and cast Fire I for ever.
        p.Gcd(c => IceSpell(c)).When(c => !c.Blm.InUmbralIce).Because("into Umbral Ice");

        p.Gcd(A.Fire1);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.LeyLines).When(c => !c.Downtime && !c.Moving);
        p.OGcd(A.Amplifier).When(c => c.Blm.PolyglotStacks < 2);
        p.OGcd(A.Manafont).When(c => c.Blm.InAstralFire && !c.Downtime && c.Mp <= ManafontMp);

        // Transpose is the AoE loop's phase change, in both directions. The chart says so
        // outright - "Transpose leveraged to skip both High Fire II and High Blizzard II" -
        // and it is the one thing the AoE list had no idea about: it was changing phase with
        // the two spells the chart exists to avoid.
        //
        // Both rules wait on the filler. The chart draws Foul or High Thunder II as the
        // global before each Transpose, and the look-ahead already knows which global is
        // next, so "hold the weave while a filler wants the window" is the whole of it. With
        // no filler to cast it goes immediately, which is the chart's own advice about
        // clipping the Transpose when the fillers have run dry.
        p.OGcd(A.Transpose)
            .When(c => AoeUsesTranspose(c)
                       && c.Blm.InUmbralIce
                       && c.Blm.UmbralHearts >= 3
                       && c.Mp >= AoeIceExitMp
                       && !AoeFillerWantsThisGlobal(c))
            .Because("into Astral Fire for the Flares");

        p.OGcd(A.Transpose)
            .When(c => AoeUsesTranspose(c)
                       && c.Blm.InAstralFire
                       && AoeFireIsSpent(c)
                       && !AoeFillerWantsThisGlobal(c))
            .Because("back to Umbral Ice");

        p.Gcd(A.Foul)
            .When(c => StuckMoving(c) && c.Blm.PolyglotStacks > 0)
            .Because("instant, you are moving");

        // The AoE dot, which the list had no rule for at all - at any level. Trash packs are
        // most of what the AoE button is ever pressed at, and Thunder II on a pack is worth
        // more than the global it costs.
        p.Gcd(c => AoeThunderAction(c))
            .When(c => StuckMoving(c) && c.Buff(A.Thunderhead)
                       && AoeThunderIsRunningOut(c, MovementRefreshWindow))
            .Because("instant, you are moving");

        p.Gcd(c => AoeThunderAction(c))
            .When(c => ThunderProcHeldOrNotNeeded(c) && !c.Downtime && AoeThunderIsRunningOut(c, 3f))
            .Because("refresh the dot");

        p.Gcd(A.Foul)
            .When(c => c.Blm.PolyglotStacks >= PolyglotCap(c))
            .Because("Polyglot is about to overcap");

        // ---- Astral Fire: two Flares and a Flare Star, and nothing else -----
        // The fire phase used to be High Fire II filler with Flare as the last cast before
        // ice, which is the single-target shape wearing AoE spells. The chart's fire phase
        // is Flare, Flare, Flare Star: the first Flare spends the Umbral Hearts and two
        // thirds of the bar, the second takes the rest, and three Astral Soul each puts
        // Flare Star up. Readiness is the mana check - Flare needs eight hundred - so the
        // phase ends by itself when the bar does.
        p.Gcd(A.FlareStar).When(c => c.Blm.AstralSoulStacks >= 6).Because("Astral Soul is full");

        p.Gcd(A.Flare)
            .When(c => AoeUsesTranspose(c) && c.Blm.InAstralFire)
            .Because("the AoE fire phase is Flare");

        // Below the chart's level the old shape is still the right one: no Astral Soul means
        // no Flare Star to build towards, and no Umbral Heart discount until 68 means one
        // Flare empties the bar however many hearts you brought. So there Flare is what it
        // always was - the last cast of the phase, after the filler has spent the bar down.
        p.Gcd(A.Flare)
            .When(c => c.Blm.InAstralFire
                       && !c.Ready(A.HighFire2)
                       && (c.Has(A.HighFire2) || !c.Ready(A.Fire2)))
            .Because("last cast before Umbral Ice");

        p.Gcd(c => AoeFireSpell(c)).When(c => c.Blm.InAstralFire);

        // ---- Umbral Ice: buy three hearts and leave ------------------------
        // At three targets and up that is Freeze; at two the chart says Blizzard IV, which
        // buys the same three hearts.
        p.Gcd(c => AoeHeartSpell(c)).When(c => c.Blm.InUmbralIce && c.Blm.UmbralHearts < 3);

        // The filler, in whichever phase it lands. Foul is unaspected, so the global it is
        // cast on costs it nothing - what matters is that it is the global before Transpose.
        p.Gcd(A.Foul)
            .When(c => c.Blm.PolyglotStacks >= 2)
            .Because("the filler before Transpose");

        // Below Flare's level there is no Transpose loop to run, so the old phase change by
        // spell is still what happens.
        p.Gcd(c => AoeFireSpell(c))
            .When(c => !AoeUsesTranspose(c) && IceHasDoneItsJob(c))
            .Because("back to Astral Fire");

        p.Gcd(c => AoeIceSpell(c))
            .When(c => c.Blm.InUmbralIce)
            .Because("waiting for the bar to fill");

        // The chart's AoE loop opens in ice, because Flare wants the three Umbral Hearts
        // Freeze buys - without them one Flare eats the whole bar and there is no second.
        // Only where there is no Transpose loop to run does a full bar mean open in fire.
        p.Gcd(c => AoeFireSpell(c))
            .When(c => !AoeUsesTranspose(c)
                       && !c.Blm.InAstralFire && !c.Blm.InUmbralIce && c.Mp >= NeutralFireMp)
            .Because("full mana, open in Astral Fire");

        p.Gcd(c => AoeIceSpell(c))
            .When(c => !c.Blm.InUmbralIce)
            .Because("into Umbral Ice");

        p.Gcd(c => AoeFireSpell(c));
    }

    /// <summary>The thunder the player actually has, which upgrades twice.</summary>
    /// <summary>Element, timer and the two resources every rule here turns on.</summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.BlackMage;

        var element = g.AstralFire > 0 ? $"fire {g.AstralFire}"
            : g.UmbralIce > 0 ? $"ice {g.UmbralIce}"
            : "neutral";

        // The seconds used to be printed against the element, which is not what they are -
        // the gauge has no element countdown, only the Polyglot one, and reading "fire 3
        // 28.2s" as an element about to last another 28 seconds is wrong twice over. Worse,
        // Enochian is level 70, so every synced-down log read "0.0s" and looked like a phase
        // expiring on every line.
        return $"{element} | hearts {g.UmbralHearts} "
               + $"| polyglot {g.PolyglotStacks} (+1 in {g.EnochianTimeRemaining:0.0}s) "
               + $"| soul {g.AstralSoulStacks}"
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

    /// <summary>
    /// The highest form of each spell the player actually has.
    /// <para>
    /// Naming the level 100 spell in a rule is how most of the synced-down damage was lost:
    /// a rule that names Blizzard III is simply dead below 35, and the list falls past it to
    /// whatever is underneath - which for a Black Mage in Umbral Ice was Fire I, a spell that
    /// removes the phase it is standing in. Every rung that is a *rung* rather than a
    /// specific spell asks through one of these instead.
    /// </para>
    /// </summary>
    private static ActionRef FireSpell(RotationContext c) =>
        c.Has(A.Fire3) ? A.Fire3 : A.Fire1;

    private static ActionRef IceSpell(RotationContext c) =>
        c.Has(A.Blizzard3) ? A.Blizzard3 : A.Blizzard1;

    private static ActionRef AoeFireSpell(RotationContext c) =>
        c.Has(A.HighFire2) ? A.HighFire2
        : c.Has(A.Fire2) ? A.Fire2
        : A.Fire1;

    /// <summary>
    /// Whether the AoE loop changes phase with Transpose.
    /// <para>
    /// Keyed on Flare Star, because that is what makes the chart's loop a loop: Astral Soul
    /// arrives with it at level 100, and without it there is nothing for two Flares to build
    /// towards. Below that the fire and ice spells still do the switching themselves, which
    /// is the rotation those levels actually have.
    /// </para>
    /// </summary>
    private static bool AoeUsesTranspose(RotationContext c) =>
        c.Has(A.FlareStar) && c.Has(A.Transpose);

    /// <summary>
    /// True when the fire phase has nothing left to do: no mana for another Flare, and not
    /// enough Astral Soul for a Flare Star.
    /// </summary>
    private static bool AoeFireIsSpent(RotationContext c) =>
        !c.Ready(A.Flare) && c.Blm.AstralSoulStacks < 6;

    /// <summary>
    /// True when the global about to be cast is one of the chart's fillers, so a Transpose
    /// would take the phase away from underneath it.
    /// </summary>
    private static bool AoeFillerWantsThisGlobal(RotationContext c) =>
        c.NextGcdIsAny(A.Foul, A.HighThunder2, A.Thunder4, A.Thunder2);

    /// <summary>
    /// What buys three Umbral Hearts. Freeze on a real pack; on two targets the chart says
    /// Blizzard IV instead, which buys the same three.
    /// </summary>
    private static ActionRef AoeHeartSpell(RotationContext c)
    {
        if (c.Enemies >= 3 && c.Has(A.Freeze))
            return A.Freeze;

        if (c.Has(A.Blizzard4))
            return A.Blizzard4;

        return c.Has(A.Freeze) ? A.Freeze : AoeIceSpell(c);
    }

    private static ActionRef AoeIceSpell(RotationContext c) =>
        c.Has(A.HighBlizzard2) ? A.HighBlizzard2
        : c.Has(A.Blizzard2) ? A.Blizzard2
        : A.Blizzard1;

    private static ActionRef ThunderAction(RotationContext c) =>
        c.Has(A.HighThunder) ? A.HighThunder
        : c.Has(A.Thunder3) ? A.Thunder3
        : A.Thunder1;

    private static ActionRef AoeThunderAction(RotationContext c) =>
        c.Has(A.HighThunder2) ? A.HighThunder2
        : c.Has(A.Thunder4) ? A.Thunder4
        : A.Thunder2;

    /// <summary>
    /// Whether the thunder line can be cast at all: on its proc where the job has one, and
    /// as a plain hard cast before that.
    /// <para>
    /// Thunder I is a cast like any other and predates Thunderhead by a long way. Where the
    /// game does require the proc it refuses the cast itself, and readiness already asks the
    /// game, so this never suggests something that would produce an error noise.
    /// </para>
    /// </summary>
    /// <summary>
    /// The most Polyglot the player can hold. Enhanced Polyglot raises it twice: one stack
    /// from Foul at 70, two from 80, three from 98.
    /// <para>
    /// Asking for two when the ceiling is three is ninety seconds of banked instants spent
    /// for nothing, and asking for three where the ceiling is two would lose a stack every
    /// thirty seconds - so the rule that spends in Umbral Ice does not depend on this being
    /// right. It drains the bank on its own every cycle; this is only the backstop.
    /// </para>
    /// </summary>
    private static byte PolyglotCap(RotationContext c) =>
        c.Level >= 98 ? (byte)3
        : c.Level >= 80 ? (byte)2
        : (byte)1;

    private static bool ThunderProcHeldOrNotNeeded(RotationContext c) =>
        !c.Has(A.Thunder3) || c.Buff(A.Thunderhead);

    /// <summary>
    /// Whether the thunder dot is within <paramref name="within"/> seconds of dropping.
    /// <para>
    /// Both forms are asked about because which one is on the target depends on level, and a
    /// dot that is not there at all reads as zero - which is correctly "running out".
    /// </para>
    /// </summary>
    /// <summary>
    /// Whether Umbral Ice has done everything it is for: the bar refilled, and the hearts
    /// filled where there is a spell that fills them.
    /// <para>
    /// This used to ask for the third ice rung as well, which is the thing ice is *for* at
    /// level 100 - but it is not what ice is for. Refilling the bar is, and the rung is only
    /// the fastest way to do it. Below level 20 the rung cannot be reached at all: both
    /// elements cap at a single stack until Aspect Mastery raises the ceiling, so the test
    /// was false for ever and the phase had no way out. Forty-four globals of a recorded
    /// Sastasha run are "ice 1" with a full bar and nowhere to go.
    /// </para>
    /// <para>
    /// Nothing changes at full level. Ice is entered on a spent bar, so the climb above -
    /// which stands down exactly when this becomes true - always reaches the third rung long
    /// before the mana arrives.
    /// </para>
    /// </summary>
    private static bool IceHasDoneItsJob(RotationContext c) =>
        c.Blm.InUmbralIce
        && (!c.Has(A.Blizzard4) || c.Blm.UmbralHearts >= 3)
        && c.Mp >= IceExitMp;

    /// <summary>
    /// Whether Astral Fire has nothing left worth casting, which is the signal to cross back.
    /// <para>
    /// Fire IV and Despair are the two that normally answer this, and asking about them is
    /// asking about mana - both refuse when the bar cannot pay. Below their levels neither
    /// exists, so both tests came back true the instant the phase began and the fire phase
    /// lasted one global. Under level 60 the phase is Fire I, and it ends when the bar can
    /// no longer pay for one.
    /// </para>
    /// </summary>
    private static bool FireIsSpent(RotationContext c) =>
        !c.Ready(A.Fire4)
        && !c.Ready(A.Despair)
        && (c.Has(A.Fire4) || !c.Ready(A.Fire1));

    private static bool ThunderIsRunningOut(RotationContext c, float within) =>
        c.DotExpiring(A.HighThunderBuff, within)
        && c.DotExpiring(A.ThunderIII, within)
        && c.DotExpiring(A.Thunder, within);

    private static bool AoeThunderIsRunningOut(RotationContext c, float within) =>
        c.DotExpiring(A.HighThunderII, within)
        && c.DotExpiring(A.ThunderIV, within)
        && c.DotExpiring(A.ThunderII, within);
}
