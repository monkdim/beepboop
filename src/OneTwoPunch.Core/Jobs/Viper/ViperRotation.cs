using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Jobs.Viper;

/// <summary>
/// Viper, Dawntrail. Every combo step hands out a venom buff that names the next one, so
/// the rotation is mostly "read the buff you were just given". Two self-buffs have to stay
/// up underneath that, and Reawaken replaces the whole bar while it runs.
/// <para>
/// The follow-ups - Death Rattle, the Legacies, the Twinfang and Twinblood pairs - are
/// named outright rather than offered as Serpent's Tail, Twinfang and Twinblood for the
/// game to adjust. Those three ids are hotbar placeholders: the game draws them and swaps
/// them for whatever is live, but it will not accept one as an action, so every rule
/// written against them asked for something unusable and never fired. Two recorded pulls
/// have Poised for Twinfang sitting on the player for a full minute, and not one Death
/// Rattle or Legacy in a hundred and forty seconds of Reawaken chains.
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

    /// <summary>
    /// "It is only a gain to use the AoE forms when fighting three or more targets. For one
    /// or two enemies, continue to use the single target versions." - the Basic guide.
    /// </summary>
    public override int AoeMinimumEnemies => 3;

    /// <summary>
    /// Every coil and every Uncoiled Fury hands out two off-globals that both have to go
    /// out before the next global. On one weave per window the job drops half of them - a
    /// recorded 31 minute raid on two weaves still lost 17 - so the rotation asks for two.
    /// </summary>
    public override WeaveStyle MinimumWeaveStyle => WeaveStyle.Double;

    /// <summary>
    /// The Balance's "Standard Opener" for Viper level 100, Dawntrail patch 7.5, up to the
    /// end of the burst.
    /// <para>
    /// The chart runs four globals further, but both of those are written as a choice -
    /// "Steel Fangs or Reaving Fangs", "Hindsting Strike or Hindsbane Fang" - because which
    /// one is correct depends on the buff you are holding. A scripted list cannot express
    /// that, and guessing wrong would abandon the opener anyway, so the tail is left to the
    /// priority list, which reads the buff and picks properly.
    /// </para>
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance standard", 100,
        A.Slither,
        A.SteelFangs, A.SerpentsIre,
        A.SwiftskinsSting,
        A.Vicewinder,
        A.HuntersCoil, A.TwinfangBite, A.TwinbloodBite,
        A.SwiftskinsCoil, A.TwinbloodBite, A.TwinfangBite,
        A.Reawaken,
        A.FirstGeneration, A.FirstLegacy,
        A.SecondGeneration, A.SecondLegacy,
        A.ThirdGeneration, A.ThirdLegacy,
        A.FourthGeneration, A.FourthLegacy,
        A.Ouroboros,
        A.UncoiledFury, A.UncoiledTwinfang, A.UncoiledTwinblood,
        A.UncoiledFury, A.UncoiledTwinfang, A.UncoiledTwinblood)
    {
        // The chart drinks in Vicewinder's weave window, just before Hunter's Coil.
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

        // Second Wind before anything else once you are hurt. It is a two minute cooldown
        // that spends most of a fight doing nothing, and noticing the moment to press it is
        // exactly the attention this plugin exists to not need - so it takes the first weave
        // slot going. That costs a little damage, which is the trade being made on purpose.
        p.OGcd(A.SecondWind).When(c => c.Hurt).Because("you are hurt");

        // And Bloodbath behind it. Second Wind is two minutes and a dungeon is much
        // longer than that, so the button used to have nothing left to offer once it
        // had gone - two recorded Monk runs have the player reaching past us for this
        // seventeen times between them. Second in order, so it is only ever the answer
        // when Second Wind is unavailable.
        // ---- Off-globals -------------------------------------------------
        // The follow-ups come before everything else that could take a weave slot, Serpent's
        // Ire and Bloodbath included. A follow-up not woven this window is usually gone: the
        // next coil or Uncoiled Fury overwrites the venom that named it. A recorded 31 minute
        // raid lost 17 of them, and of the ones the plugin could have prevented, four went
        // to Serpent's Ire taking the slot and two to the heals. Ire a global later costs
        // nothing the guide does not already allow for; a bite is 120 potency gone.
        //
        // Second Wind stays above them on purpose - see the note beside it. Bloodbath is the
        // weaker heal and can wait one global, so it drops below.

        // Serpent's Tail. The gauge says which follow-up is live, so it is asked rather
        // than guessed at: Death Rattle after a venom finisher, a Legacy after each
        // Generation.
        p.OGcd(c => SerpentsTailAction(c)).When(c => c.Vpr.SerpentCombo != SerpentCombo.None);

        // The Twin pair, each named by the venom the coil before it left. They chain:
        // Hunter's Coil leaves Hunter's Venom, Twinfang Bite spends it and leaves
        // Swiftskin's Venom for Twinblood Bite - so both weaves follow one coil.
        p.OGcd(A.TwinfangBite).When(c => c.Buff(A.HuntersVenom));
        p.OGcd(A.TwinbloodBite).When(c => c.Buff(A.SwiftskinsVenom));

        // Uncoiled Fury's own pair, the same chain one step apart.
        p.OGcd(A.UncoiledTwinfang).When(c => c.Buff(A.PoisedForTwinfang));
        p.OGcd(A.UncoiledTwinblood).When(c => c.Buff(A.PoisedForTwinblood));

        p.OGcd(A.SerpentsIre).When(c => !c.Downtime).Because("burst window");

        p.OGcd(A.Bloodbath).When(c => c.Hurt).Because("you are hurt and Second Wind is down");

        // ---- GCDs --------------------------------------------------------
        // Reawaken replaces the bar and its tribute drains, so it outranks everything.
        p.Gcd(A.Ouroboros)
            .When(c => c.Vpr.AnguineTribute == 1)
            .Because("Reawaken finisher");

        p.Gcd(c => GenerationAction(c))
            .When(c => c.Vpr.AnguineTribute > 0);

        // Vicewinder's two coils, each with their own positional. Which chain step is live
        // is a gauge field, not the combo - see ViperGauge.DreadCombo.
        p.Gcd(A.SwiftskinsCoil)
            .When(c => c.Vpr.DreadCombo == DreadCombo.Vicewinder && NeedsSwiftscaled(c))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.HuntersCoil)
            .When(c => c.Vpr.DreadCombo == DreadCombo.Vicewinder)
            .Needs(PositionalHint.Flank);

        p.Gcd(A.SwiftskinsCoil)
            .When(c => c.Vpr.DreadCombo == DreadCombo.HuntersCoil)
            .Needs(PositionalHint.Rear);

        p.Gcd(A.HuntersCoil)
            .When(c => c.Vpr.DreadCombo == DreadCombo.SwiftskinsCoil)
            .Needs(PositionalHint.Flank);

        // Combo finishers. Each is named outright by the venom buff the previous step gave.
        p.Gcd(A.FlankstingStrike).When(c => c.Buff(A.FlankstungVenom)).Needs(PositionalHint.Flank);
        p.Gcd(A.FlanksbaneFang).When(c => c.Buff(A.FlanksbaneVenom)).Needs(PositionalHint.Flank);
        p.Gcd(A.HindstingStrike).When(c => c.Buff(A.HindstungVenom)).Needs(PositionalHint.Rear);
        p.Gcd(A.HindsbaneFang).When(c => c.Buff(A.HindsbaneVenom)).Needs(PositionalHint.Rear);

        // Second step. The venom finisher buff you are holding decides it, because it names
        // the finisher, and each sting only leads to two of the four: Hunter's Sting to the
        // flank pair, Swiftskin's Sting to the rear pair. Take the other sting and "it is no
        // longer possible to press the buffed combo finisher" - the guide's words - which
        // here means no finisher rule matches at all and the list restarts the combo.
        //
        // This used to be decided by whichever self-buff was closer to dropping. In full
        // uptime the two happen to agree, and a recorded 4:29 pull is 17 for 17. They part
        // company after forty to sixty seconds of downtime, when the 40s self-buffs have gone
        // and the 60s venom has not. Only with no venom held - the first combo, coming back
        // from a death - does the self-buff decide, and then it is the one closer to dropping.
        p.Gcd(A.SwiftskinsSting)
            .When(c => ComboStarted(c) && WantsSwiftskinsSting(c))
            .Because(c => HoldsARearVenom(c) ? "the venom you hold wants a rear finisher" : "refresh Swiftscaled");

        p.Gcd(A.HuntersSting)
            .When(c => ComboStarted(c))
            .Because(c => HoldsAFlankVenom(c) ? "the venom you hold wants a flank finisher" : "refresh Hunter's Instinct");

        // Reawaken and Vicewinder are not gated on the combo being broken any more, they are
        // *placed* where the combo starter goes - below every finisher and both stings, above
        // Steel Fangs and Reaving Fangs. The gate could not come true: a finisher leaves the
        // combo live rather than clearing it, so in a working loop the combo is never broken
        // and neither rule ever fired.
        //
        // That one condition took most of the job with it. No Vicewinder means no Hunter's
        // Coil or Swiftskin's Coil, which are what Twinfang and Twinblood follow; no Reawaken
        // means no Generation chain, no Ouroboros and no Serpent's Tail. A recorded pull is
        // seventy seconds of Steel Fangs, a sting and a finisher on repeat, with Ready to
        // Reawaken counting down from twenty-nine to nothing untouched.
        //
        // Placing them here cannot interrupt a combo, because anything mid-combo has already
        // matched above.
        p.Gcd(A.Reawaken)
            .When(c => !c.Downtime && ReawakenIsAffordable(c))
            .Because(c => ReawakenReason(c));

        // Not inside the last ten seconds before Serpent's Ire. "Around 10s left on Ire's
        // cooldown, you should start to use only dual wield combos" - a twinblade combo is
        // three globals and its coils each want two weaves, so Ire landing inside one has
        // nowhere to go. The guide accepts holding a charge for this.
        p.Gcd(A.Vicewinder)
            .When(c => !c.Downtime && !BurstIsImminent(c));

        // Uncoiled Fury is ranged and instant, so it is the answer to being out of reach.
        // Moving on its own is not a reason: every other Viper global is instant too, and a
        // recorded raid spent the reserve coil on "you are moving" 32 times in 31 minutes,
        // then had nothing for two real disconnects and threw Writhing Snap. Out of reach,
        // or moving and out of reach this instant - the start of a real disengage - is.
        p.Gcd(A.UncoiledFury)
            .When(c => c.Vpr.RattlingCoils > 0 && WantsUncoiledFury(c))
            .Because(c => UncoiledFuryReason(c));

        p.Gcd(A.ReavingFangs).When(c => c.Buff(A.HonedReavers));
        p.Gcd(A.SteelFangs);

        p.Gcd(A.WrithingSnap)
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

        // And Bloodbath behind it. Second Wind is two minutes and a dungeon is much
        // longer than that, so the button used to have nothing left to offer once it
        // had gone - two recorded Monk runs have the player reaching past us for this
        // seventeen times between them. Second in order, so it is only ever the answer
        // when Second Wind is unavailable.
        // Follow-ups before Ire and Bloodbath, for the reason on the single-target list.
        p.OGcd(c => SerpentsTailAction(c)).When(c => c.Vpr.SerpentCombo != SerpentCombo.None);

        p.OGcd(A.TwinfangThresh).When(c => c.Buff(A.FellhuntersVenom));
        p.OGcd(A.TwinbloodThresh).When(c => c.Buff(A.FellskinsVenom));

        p.OGcd(A.UncoiledTwinfang).When(c => c.Buff(A.PoisedForTwinfang));
        p.OGcd(A.UncoiledTwinblood).When(c => c.Buff(A.PoisedForTwinblood));

        p.OGcd(A.SerpentsIre).When(c => !c.Downtime).Because("burst window");

        p.OGcd(A.Bloodbath).When(c => c.Hurt).Because("you are hurt and Second Wind is down");

        p.Gcd(A.Ouroboros).When(c => c.Vpr.AnguineTribute == 1);
        p.Gcd(c => GenerationAction(c)).When(c => c.Vpr.AnguineTribute > 0);

        p.Gcd(A.SwiftskinsDen)
            .When(c => c.Vpr.DreadCombo == DreadCombo.Vicepit && NeedsSwiftscaled(c));

        p.Gcd(A.HuntersDen).When(c => c.Vpr.DreadCombo == DreadCombo.Vicepit);
        p.Gcd(A.SwiftskinsDen).When(c => c.Vpr.DreadCombo == DreadCombo.HuntersDen);
        p.Gcd(A.HuntersDen).When(c => c.Vpr.DreadCombo == DreadCombo.SwiftskinsDen);

        p.Gcd(A.JaggedMaw).When(c => c.Buff(A.GrimhuntersVenom));
        p.Gcd(A.BloodiedMaw).When(c => c.Buff(A.GrimskinsVenom));

        p.Gcd(A.SwiftskinsBite)
            .When(c => (c.ComboIs(A.SteelMaw) || c.ComboIs(A.ReavingMaw)) && NeedsSwiftscaled(c));

        p.Gcd(A.HuntersBite).When(c => c.ComboIs(A.SteelMaw) || c.ComboIs(A.ReavingMaw));

        // Placed where the combo starter goes, for the reason spelled out on the
        // single-target list: gating these on a broken combo meant they never fired at all.
        p.Gcd(A.Reawaken)
            .When(c => !c.Downtime && ReawakenIsAffordable(c))
            .Because(c => ReawakenReason(c));

        p.Gcd(A.Vicepit)
            .When(c => !c.Downtime && !BurstIsImminent(c));

        // "Uncoiled Fury and the entire Reawaken combo are also aoe by default", so the AoE
        // list uses it exactly as the single-target one does.
        p.Gcd(A.UncoiledFury)
            .When(c => c.Vpr.RattlingCoils > 0 && WantsUncoiledFury(c))
            .Because(c => UncoiledFuryReason(c));

        p.Gcd(A.ReavingMaw).When(c => c.Buff(A.HonedReavers));
        p.Gcd(A.SteelMaw);
    }

    /// <summary>
    /// Whichever follow-up Serpent's Tail is offering. The gauge names it outright, so
    /// there is nothing here to keep in step with the rest of the rotation.
    /// </summary>
    /// <summary>
    /// Everything the list actually reads, printed beside every cast.
    /// <para>
    /// Viper had no gauge line, and it is the job that needs one most: five separate things
    /// drive its rules and not one of them reached the log. A recorded four and a half minute
    /// pull is structurally perfect - seven Reawaken chains, every follow-up matched, Swiftscaled
    /// at ninety-nine percent - and it still cannot answer the only question asked of it,
    /// which was whether the resources were being held or spent, because Serpent Offering
    /// is not in it.
    /// </para>
    /// <para>
    /// Both chain trackers are named too. Neither is the ordinary combo state - Vicewinder
    /// leaves Steel Fangs' combo running underneath it - and both have already been the cause
    /// of a rule that fired for nobody.
    /// </para>
    /// </summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Viper;

        var tribute = g.AnguineTribute > 0 ? $" | tribute {g.AnguineTribute}" : string.Empty;
        var dread = g.DreadCombo != DreadCombo.None ? $" | dread {g.DreadCombo}" : string.Empty;
        var tail = g.SerpentCombo != SerpentCombo.None ? $" | tail {g.SerpentCombo}" : string.Empty;

        return $"coils {g.RattlingCoils} | offering {g.SerpentOffering}{tribute}{dread}{tail}";
    }

    // ---- Burst ----------------------------------------------------------

    /// <summary>
    /// How far ahead of Serpent's Ire the list starts setting up for it. "Around 10s left
    /// on Ire's cooldown, you should start to use only dual wield combos."
    /// </summary>
    private const float BurstLeadSeconds = 10f;

    /// <summary>
    /// Serpent Offering earned per second in full uptime at the reference global below. The
    /// Intermediate guide: "Viper generates enough Offerings to use one Reawaken per minute"
    /// - fifty in sixty seconds. Downtime only makes this an over-estimate, which errs
    /// towards spending, never towards a Reawaken that cannot be afforded.
    /// </summary>
    private const float ReferenceOfferingPerSecond = 50f / 60f;

    /// <summary>
    /// The global that figure is quoted at: the guide gives Viper's dual wield recast as
    /// "2.5s (2.12s with 15% haste buff) with no skill speed", and quotes the one-Reawaken-
    /// per-minute rate for full uptime, which is Swiftscaled up.
    /// </summary>
    private const float ReferenceGcd = 2.12f;

    /// <summary>
    /// Offering per second at the global the player actually has.
    /// <para>
    /// Offering is earned per action, not per second, so the rate above only holds at the
    /// global it was quoted at. A player melded into skill speed earns it faster and one
    /// without Swiftscaled up earns it slower, and the constant alone was blind to both. A
    /// recorded pull put this beyond argument: a 2.04s global, four percent faster than the
    /// reference, which moves the boundary at fifty Offerings from sixty seconds of Serpent's
    /// Ire cooldown to 57.7 - so the hold sat on a full gauge 2.3s longer than it needed to.
    /// Small, but it is worst for exactly the gear that reports it.
    /// </para>
    /// <para>
    /// Read off the single-target button's own recast, so it is one global tier rather than
    /// whichever of Viper's four was used last, and it already carries haste. Clamped to a
    /// plausible range: a rate derived from a nonsense reading would be worse than the
    /// constant it replaced.
    /// </para>
    /// </summary>
    private static float OfferingPerSecond(RotationContext c)
    {
        var gcd = Math.Clamp(c.GcdTotal, 1.5f, 3.5f);
        return ReferenceOfferingPerSecond * (ReferenceGcd / gcd);
    }

    private const int ReawakenCost = 50;

    private const int OfferingCap = 100;

    /// <summary>What one dual wield finisher adds - the largest single step the gauge takes.</summary>
    private const int OfferingPerFinisher = 10;

    /// <summary>
    /// Serpent's Ire is up, or about to be. True while it is ready and unpressed, so that
    /// the globals the burst is written around are not started in the frame before it.
    /// Below the level that has it there is no burst to set up for.
    /// </summary>
    private static bool BurstIsImminent(RotationContext c) =>
        c.Has(A.SerpentsIre) && c.ReadyIn(A.SerpentsIre, BurstLeadSeconds);

    /// <summary>
    /// Whether the fifty Offerings can be spent now without the two-minute window going
    /// short - the Intermediate guide's rule, made arithmetic.
    /// <para>
    /// "While it is possible to purely follow the priority system mentioned in the basic
    /// guide, which would involve sending Reawakens essentially as soon as they are
    /// available, this comes at a significant loss of potency inside party buffs. [...] The
    /// simplest way to manage these Reawakens and still put maximum potency into party
    /// buffs is to save at least 50 Offerings for when [Serpent's Ire comes up]." That was
    /// exactly the old rule, and a recorded 31 minute raid shows the cost: fifteen burst
    /// windows, seven of them a double Reawaken, eight a single with the second one landing
    /// somewhere outside the buffs.
    /// </para>
    /// <para>
    /// So a paid Reawaken goes only when what is left will have grown back to fifty by the
    /// time Ire returns. At fifty Offerings that means Ire is a minute or more away, which is
    /// the guide's one-between-windows. Three things override it: the free Reawaken from
    /// Ire is always taken; a gauge that would cap on the next finisher is spent, because
    /// holding would waste generation; and below the level that has Serpent's Ire there is
    /// no window to save for - a rung that cannot be climbed must not hang the phase.
    /// </para>
    /// </summary>
    private static bool ReawakenIsAffordable(RotationContext c)
    {
        if (c.Buff(A.ReawakenReady))
            return true;

        var offering = c.Vpr.SerpentOffering;
        if (offering < ReawakenCost)
            return false;

        if (!c.Has(A.SerpentsIre))
            return true;

        if (offering + OfferingPerFinisher > OfferingCap)
            return true;

        var regrown = offering - ReawakenCost + OfferingPerSecond(c) * c.Cd(A.SerpentsIre);
        return regrown >= ReawakenCost;
    }

    private static string ReawakenReason(RotationContext c)
    {
        if (c.Buff(A.ReawakenReady))
            return "free from Serpent's Ire";

        if (c.Vpr.SerpentOffering + OfferingPerFinisher > OfferingCap)
            return "the gauge would cap";

        return !c.Has(A.SerpentsIre)
            ? "spend Serpent Offering"
            : "spend Serpent Offering, it will be back for the burst";
    }

    /// <summary>
    /// When a coil is spent. Out of reach, or moving and out of reach this instant, is a
    /// disengage and what the reserve is for. Everything above the reserve is spent as it
    /// comes. And in the last seconds before Serpent's Ire the reserve itself goes, because
    /// Ire grants another and the guide says to "spend them before using Serpent's Ire".
    /// </summary>
    private static bool WantsUncoiledFury(RotationContext c) =>
        !c.InRange
        || (c.Moving && !c.InRangeRightNow)
        || c.Vpr.RattlingCoils >= CoilReserve + 1
        || BurstIsImminent(c);

    private static string UncoiledFuryReason(RotationContext c)
    {
        if (!c.InRange || (c.Moving && !c.InRangeRightNow))
            return "instant, you are out of reach";

        return c.Vpr.RattlingCoils >= CoilReserve + 1
            ? "spend down to the reserve coil"
            : "spend the reserve, Serpent's Ire grants another";
    }

    // ---- Combo ----------------------------------------------------------

    private static bool HoldsAFlankVenom(RotationContext c) =>
        c.Buff(A.FlankstungVenom) || c.Buff(A.FlanksbaneVenom);

    private static bool HoldsARearVenom(RotationContext c) =>
        c.Buff(A.HindstungVenom) || c.Buff(A.HindsbaneVenom);

    /// <summary>The venom decides; only with none held does the self-buff.</summary>
    private static bool WantsSwiftskinsSting(RotationContext c) =>
        HoldsARearVenom(c) || (!HoldsAFlankVenom(c) && NeedsSwiftscaled(c));

    /// <summary>
    /// How many Rattling Coils to keep in the bank. Everything above this is spent.
    /// <para>
    /// The list used to spend only at three, which is the cap - so a coil earned while full
    /// was simply lost, and the Balance's basic priority list says the opposite in as many
    /// words: "Spend Rattling Coils as you get them. Save one at all times to cover potential
    /// disengages, but spend them before using Serpent's Ire as it will grant another. Avoid
    /// overcapping Coils."
    /// </para>
    /// <para>
    /// One is the reserve because that is what a disengage costs, and Uncoiled Fury is the
    /// only ranged global either list has - which is why the moving and out-of-range clauses
    /// spend the reserve itself. Sitting at one also means Serpent's Ire's own coil lands in
    /// an empty slot rather than on a full gauge.
    /// </para>
    /// </summary>
    private const int CoilReserve = 1;

    private static ActionRef SerpentsTailAction(RotationContext c) => c.Vpr.SerpentCombo switch
    {
        SerpentCombo.DeathRattle => A.DeathRattle,
        SerpentCombo.LastLash => A.LastLash,
        SerpentCombo.FirstLegacy => A.FirstLegacy,
        SerpentCombo.SecondLegacy => A.SecondLegacy,
        SerpentCombo.ThirdLegacy => A.ThirdLegacy,
        _ => A.FourthLegacy,
    };

    private static bool ComboStarted(RotationContext c) =>
        c.ComboIs(A.SteelFangs) || c.ComboIs(A.ReavingFangs);

    /// <summary>
    /// True when Swiftscaled is the buff closer to dropping. Both self-buffs multiply
    /// everything that follows, so whichever is shorter wins the branch.
    /// </summary>
    private static bool NeedsSwiftscaled(RotationContext c) =>
        c.BuffTime(A.Swiftscaled) <= c.BuffTime(A.HuntersInstinct);

    /// <summary>
    /// The Reawaken chain, read off the tribute the gauge is counting down. Offering all
    /// four and letting Ready() pick was meant to avoid tracking the position ourselves,
    /// but the game accepts any of them - it adjusts the press to the step you are actually
    /// on - so the first one always won and the button read "First Generation" for the whole
    /// chain. It cast correctly and looked broken, which is how it was reported.
    /// <para>
    /// Ouroboros takes the last tribute where the player has it, so the generations left are
    /// one fewer than the count: four of them at level 96 and up, and below that the four
    /// are the whole chain. Readiness still has the last word, so an unexpected count cannot
    /// leave the chain stuck on an action the game will not take.
    /// </para>
    /// </summary>
    private static ActionRef GenerationAction(RotationContext c)
    {
        var generationsLeft = c.Vpr.AnguineTribute - (c.Has(A.Ouroboros) ? 1 : 0);

        var wanted = generationsLeft switch
        {
            >= 4 => A.FirstGeneration,
            3 => A.SecondGeneration,
            2 => A.ThirdGeneration,
            _ => A.FourthGeneration,
        };

        if (c.Ready(wanted))
            return wanted;

        if (c.Ready(A.FirstGeneration))
            return A.FirstGeneration;

        if (c.Ready(A.SecondGeneration))
            return A.SecondGeneration;

        if (c.Ready(A.ThirdGeneration))
            return A.ThirdGeneration;

        return A.FourthGeneration;
    }
}
