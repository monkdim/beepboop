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
        p.OGcd(A.Bloodbath).When(c => c.Hurt).Because("you are hurt and Second Wind is down");

        // ---- Off-globals -------------------------------------------------
        p.OGcd(A.SerpentsIre).When(c => !c.Downtime).Because("burst window");

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

        // Second step. Which one depends on the self-buff that is closer to dropping -
        // losing either costs damage on everything after it.
        p.Gcd(A.SwiftskinsSting)
            .When(c => ComboStarted(c) && NeedsSwiftscaled(c))
            .Because("refresh Swiftscaled");

        p.Gcd(A.HuntersSting)
            .When(c => ComboStarted(c))
            .Because("refresh Hunter's Instinct");

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
            .When(c => !c.Downtime && (c.Vpr.SerpentOffering >= 50 || c.Buff(A.ReawakenReady)))
            .Because("spend Serpent Offering");

        p.Gcd(A.Vicewinder).When(c => !c.Downtime);

        // Uncoiled Fury is ranged and instant, so it doubles as the movement option.
        p.Gcd(A.UncoiledFury)
            .When(c => c.Vpr.RattlingCoils > 0
                       && (c.Moving || !c.InRange || c.Vpr.RattlingCoils >= CoilReserve + 1))
            .Because(c => c.Moving ? "instant, you are moving" : "spend down to the reserve coil");

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
        p.OGcd(A.Bloodbath).When(c => c.Hurt).Because("you are hurt and Second Wind is down");

        p.OGcd(A.SerpentsIre).When(c => !c.Downtime).Because("burst window");

        p.OGcd(c => SerpentsTailAction(c)).When(c => c.Vpr.SerpentCombo != SerpentCombo.None);

        p.OGcd(A.TwinfangThresh).When(c => c.Buff(A.FellhuntersVenom));
        p.OGcd(A.TwinbloodThresh).When(c => c.Buff(A.FellskinsVenom));

        p.OGcd(A.UncoiledTwinfang).When(c => c.Buff(A.PoisedForTwinfang));
        p.OGcd(A.UncoiledTwinblood).When(c => c.Buff(A.PoisedForTwinblood));

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
            .When(c => !c.Downtime && (c.Vpr.SerpentOffering >= 50 || c.Buff(A.ReawakenReady)))
            .Because("spend Serpent Offering");

        p.Gcd(A.Vicepit).When(c => !c.Downtime);

        // "Uncoiled Fury and the entire Reawaken combo are also aoe by default", so the AoE
        // list uses it exactly as the single-target one does - including as the answer to
        // moving or being out of reach, which is the only ranged global either list has.
        p.Gcd(A.UncoiledFury)
            .When(c => c.Vpr.RattlingCoils > 0
                       && (c.Moving || !c.InRange || c.Vpr.RattlingCoils >= CoilReserve + 1))
            .Because(c => c.Moving ? "instant, you are moving" : "spend down to the reserve coil");

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
