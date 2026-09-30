using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Pictomancer.PictomancerActions;

namespace OneTwoPunch.Core.Jobs.Pictomancer;

/// <summary>
/// Pictomancer, Dawntrail. Three motifs are painted in advance and then spent as muses,
/// while a three-colour spell cycle runs underneath and flips between the additive and
/// subtractive palettes.
/// <para>
/// The motifs are the part worth getting right: they are long casts that root you, and the
/// muses that spend them are instant. So motifs are painted while standing still and held
/// back while moving, and the instants - Holy in White, Comet in Black, the hammer line -
/// come out instead. That is the same movement handling as Black Mage, applied to a job
/// that gets to choose when to be rooted.
/// </para>
/// </summary>
public sealed class PictomancerRotation : JobRotationBase
{
    public override uint JobId => 42;

    public override string Name => "Pictomancer";

    public override ActionRef SingleTargetButton => A.FireInRed;

    public override ActionRef AoeButton => A.FireIIInRed;

    public override float AoeRadius => 5f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override StatusRef? BurstStatus => A.StarryMuseBuff;

    public override ActionRef? BurstAction => A.StarryMuse;

    /// <summary>
    /// The Balance's "2nd GCD Starry Opener" for Pictomancer level 100, Dawntrail patch
    /// 7.2. Starry Muse lands between Wing Motif and Hammer Stamp, which is what the name
    /// refers to: the buff covers from the second global on.
    /// <para>
    /// The chart assumes the pre-pull motifs are already drawn - Pom Muse and Striking Muse
    /// cannot go off otherwise. Those are held between pulls rather than drawn during the
    /// opener, so they are not steps here; the priority list draws them when they are down.
    /// </para>
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance 2nd-GCD Starry", 100,
        A.RainbowDrip,
        A.PomMuse, A.StrikingMuse,
        A.WingMotif, A.StarryMuse,
        A.HammerStamp, A.SubtractivePalette,
        A.BlizzardInCyan,
        A.StoneInYellow,
        A.ThunderInMagenta,
        A.CometInBlack, A.WingedMuse, A.MogOfTheAges,
        A.StarPrism,
        A.HammerBrush,
        A.PolishingHammer,
        A.RainbowDrip,
        A.FireInRed, A.Swiftcast,
        A.AeroInGreen)
    {
        // Drunk on the pull itself, ahead of the two muses.
        PotionBeforeStep = 1,
    };

    public override Opener? Opener => Sequence;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    /// <summary>
    /// Whether this is a moment to be painting rather than attacking.
    /// <para>
    /// A motif is three seconds of standing still that deals no damage, so the two things
    /// that rule it out are movement and the burst window. Starry Muse is twenty seconds of
    /// increased damage and Hyperphantasia is five paint spells that each hit harder for
    /// being inside it - neither is spent by a motif, so a motif painted there is a global
    /// of the buff thrown away. The canvases are drawn in the ninety seconds between.
    /// </para>
    /// </summary>
    private static bool CanPaint(RotationContext c) =>
        !c.Moving && !c.Buff(A.StarryMuseBuff) && !c.Buff(A.Hyperphantasia);

    /// <summary>How far ahead of a muse its canvas is worth painting.</summary>
    private const float PaintLead = 10f;

    /// <summary>The landscape canvas gets longer, because Scenic Muse is the raid buff.</summary>
    private const float ScenicPaintLead = 15f;

    /// <summary>
    /// Whether the muse that spends this canvas can actually take it.
    /// <para>
    /// A drawn canvas does nothing on its own - it is a muse waiting to happen - so a motif
    /// painted with no charge to spend it is a rooted three second global that deals no damage
    /// and buys nothing until the charge comes back. A recorded pull painted six of its eleven
    /// motifs that way, each with between seventy-seven and a hundred and eighteen seconds to
    /// the next charge, and one stretch ran fifteen seconds in which every global was a motif:
    /// paint, spend, paint, spend, paint, with no damage in between. Scored against the game's
    /// own potencies that pull ran fourteen percent behind the one before it, which did none
    /// of this because it could not paint at all.
    /// </para>
    /// <para>
    /// The canvases still get banked ahead of the muse - that is what the lead is for, and the
    /// landscape one has always had it. This only stops the button painting into a wall.
    /// </para>
    /// </summary>
    private static bool MuseCanTakeIt(RotationContext c, ActionRef muse, float lead = PaintLead) =>
        c.Charges(muse) > 0 || c.ReadyIn(muse, lead);

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // The raid buff, and the two muses that only exist because a motif was painted
        // earlier - which is the whole reason motifs get painted during downtime.
        // Every one of these is an icon the game always replaces, so they are named by the
        // icon and resolved to whatever it currently is. See JobRotationBase.Current.
        p.OGcd(c => Current(c, A.ScenicMuse))
            .When(c => !c.Downtime && c.Pct.LandscapeMotifDrawn)
            .Because("raid buff");

        p.OGcd(c => Current(c, A.SteelMuse))
            .When(c => !c.Downtime && c.Pct.WeaponMotifDrawn)
            .Because("spend the weapon motif");

        p.OGcd(c => Current(c, A.LivingMuse))
            .When(c => !c.Downtime && c.Pct.CreatureMotifDrawn)
            .Because("spend the creature motif");

        // Both portraits are the same button - Mog of the Ages becomes Retribution of the
        // Madeen once that half is the one standing - so one rule covers them.
        p.OGcd(c => Current(c, A.MogOfTheAges))
            .When(c => c.Pct.MooglePortraitReady || c.Pct.MadeenPortraitReady)
            .Because("spend the portrait");

        // Fifty is the price, but Starry Muse hands out a free one and it expires with the
        // burst - so the gauge is not the only way in, and a Subtractive Spectrum left
        // unspent is a whole subtractive cycle missed inside the damage window.
        p.OGcd(A.SubtractivePalette)
            .When(c => !c.Buff(A.SubtractivePaletteBuff)
                && (c.Buff(A.SubtractiveSpectrum) || c.Pct.PaletteGauge >= 50))
            .Because(c => c.Buff(A.SubtractiveSpectrum) ? "free, and it expires" : "palette is close to capping");

        // ---- GCDs --------------------------------------------------------
        // Free instants and burst follow-ups, all of which expire.
        p.Gcd(A.StarPrism).When(c => c.Buff(A.Starstruck));
        p.Gcd(A.RainbowDrip).When(c => c.Buff(A.RainbowBright)).Because("free and instant");

        // The hammer line is three instant hits and is the movement answer.
        p.Gcd(A.PolishingHammer).When(c => c.Buff(A.HammerTime) && c.Ready(A.PolishingHammer));
        p.Gcd(A.HammerBrush).When(c => c.Buff(A.HammerTime) && c.Ready(A.HammerBrush));
        p.Gcd(A.HammerStamp)
            .When(c => c.Buff(A.HammerTime))
            .Because(c => c.Moving ? "instant, you are moving" : "hammer");

        // White and black paint are instant, so they cover movement too.
        p.Gcd(A.CometInBlack)
            .When(c => c.Buff(A.MonochromeTones) && c.Pct.Paint > 0)
            .Because(c => c.Moving ? "instant, you are moving" : "spend paint");

        p.Gcd(A.HolyInWhite)
            .When(c => c.Pct.Paint > 0 && (c.Moving || c.Pct.Paint >= 5))
            .Because(c => c.Moving ? "instant, you are moving" : "paint is close to capping");

        // Motifs root you for three seconds and deal no damage, so they are painted in the
        // quiet part of the fight: standing still, and outside the burst. See CanPaint.
        p.Gcd(c => Current(c, A.LandscapeMotif))
            .When(c => CanPaint(c) && !c.Pct.LandscapeMotifDrawn
                && MuseCanTakeIt(c, A.ScenicMuse, ScenicPaintLead))
            .Because("paint before the buff window");

        p.Gcd(c => Current(c, A.WeaponMotif))
            .When(c => CanPaint(c) && !c.Pct.WeaponMotifDrawn && MuseCanTakeIt(c, A.SteelMuse))
            .Because("paint while you can stand still");

        p.Gcd(c => Current(c, A.CreatureMotif))
            .When(c => CanPaint(c) && !c.Pct.CreatureMotifDrawn && MuseCanTakeIt(c, A.LivingMuse))
            .Because("paint while you can stand still");

        // The three-colour cycle. Aetherhues decides which colour is next, and the
        // subtractive palette swaps all three for their cool counterparts.
        p.Gcd(A.ThunderInMagenta).When(c => c.Buff(A.AetherhuesII) && c.Buff(A.SubtractivePaletteBuff));
        p.Gcd(A.StoneInYellow).When(c => c.Buff(A.Aetherhues) && c.Buff(A.SubtractivePaletteBuff));
        p.Gcd(A.BlizzardInCyan).When(c => c.Buff(A.SubtractivePaletteBuff));

        p.Gcd(A.WaterInBlue).When(c => c.Buff(A.AetherhuesII));
        p.Gcd(A.AeroInGreen).When(c => c.Buff(A.Aetherhues));
        p.Gcd(A.FireInRed);
    }

    /// <summary>
    /// The three canvases, the two portraits and the two resources - which is to say, the
    /// whole of what the priority list reads.
    /// <para>
    /// A recorded pull went sixty-two casts without a single motif and there was no way to
    /// tell whether the canvases were full, the gauge was lying or the rule was never
    /// reached. Printing the canvases makes the first two answerable from the log alone.
    /// </para>
    /// </summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Pictomancer;

        var canvas = (g.CreatureMotifDrawn ? "creature" : "-")
            + "/" + (g.WeaponMotifDrawn ? "weapon" : "-")
            + "/" + (g.LandscapeMotifDrawn ? "landscape" : "-");

        var portraits = (g.MooglePortraitReady ? " | moogle" : string.Empty)
            + (g.MadeenPortraitReady ? " | madeen" : string.Empty);

        return $"palette {g.PaletteGauge} | paint {g.Paint} | canvas {canvas}{portraits}";
    }

    /// <summary>
    /// Whether the motifs and the muses may be offered at all.
    /// <para>
    /// This is the line that would have named the bug in one read. The motifs are four
    /// second globals against a two and a half second one, and the engine measured their
    /// remaining recast against the shorter of the two - so they reported over a second of
    /// cooldown for the whole of every global and no rule could ever offer them. The gauge
    /// said the canvases were empty and the rules were right; only the clock disagreed.
    /// </para>
    /// </summary>
    public override string? DescribeReadiness(CombatSnapshot snapshot, IActionState actions) =>
        $"{ProbeCurrent(actions, A.CreatureMotif)} {ProbeCurrent(actions, A.WeaponMotif)} "
        + $"{ProbeCurrent(actions, A.LandscapeMotif)} {ProbeCurrent(actions, A.LivingMuse)} "
        + $"{ProbeCurrent(actions, A.SteelMuse)} {ProbeCurrent(actions, A.ScenicMuse)} "
        + $"{ProbeCurrent(actions, A.MogOfTheAges)} {ProbeCurrent(actions, A.HammerStamp)} "
        + $"{Probe(actions, A.RainbowDrip)}";

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(c => Current(c, A.ScenicMuse))
            .When(c => !c.Downtime && c.Pct.LandscapeMotifDrawn).Because("raid buff");

        p.OGcd(c => Current(c, A.SteelMuse)).When(c => !c.Downtime && c.Pct.WeaponMotifDrawn);
        p.OGcd(c => Current(c, A.LivingMuse)).When(c => !c.Downtime && c.Pct.CreatureMotifDrawn);

        p.OGcd(c => Current(c, A.MogOfTheAges))
            .When(c => c.Pct.MooglePortraitReady || c.Pct.MadeenPortraitReady);

        p.OGcd(A.SubtractivePalette)
            .When(c => !c.Buff(A.SubtractivePaletteBuff)
                && (c.Buff(A.SubtractiveSpectrum) || c.Pct.PaletteGauge >= 50));

        p.Gcd(A.StarPrism).When(c => c.Buff(A.Starstruck));
        p.Gcd(A.RainbowDrip).When(c => c.Buff(A.RainbowBright));

        p.Gcd(A.PolishingHammer).When(c => c.Buff(A.HammerTime) && c.Ready(A.PolishingHammer));
        p.Gcd(A.HammerBrush).When(c => c.Buff(A.HammerTime) && c.Ready(A.HammerBrush));
        p.Gcd(A.HammerStamp).When(c => c.Buff(A.HammerTime));

        p.Gcd(A.CometInBlack).When(c => c.Buff(A.MonochromeTones) && c.Pct.Paint > 0);
        p.Gcd(A.HolyInWhite).When(c => c.Pct.Paint > 0 && (c.Moving || c.Pct.Paint >= 5));

        p.Gcd(c => Current(c, A.LandscapeMotif))
            .When(c => CanPaint(c) && !c.Pct.LandscapeMotifDrawn
                && MuseCanTakeIt(c, A.ScenicMuse, ScenicPaintLead));

        p.Gcd(c => Current(c, A.WeaponMotif))
            .When(c => CanPaint(c) && !c.Pct.WeaponMotifDrawn && MuseCanTakeIt(c, A.SteelMuse));

        p.Gcd(c => Current(c, A.CreatureMotif))
            .When(c => CanPaint(c) && !c.Pct.CreatureMotifDrawn && MuseCanTakeIt(c, A.LivingMuse));

        p.Gcd(A.ThunderIIInMagenta).When(c => c.Buff(A.AetherhuesII) && c.Buff(A.SubtractivePaletteBuff));
        p.Gcd(A.StoneIIInYellow).When(c => c.Buff(A.Aetherhues) && c.Buff(A.SubtractivePaletteBuff));
        p.Gcd(A.BlizzardIIInCyan).When(c => c.Buff(A.SubtractivePaletteBuff));

        p.Gcd(A.WaterIIInBlue).When(c => c.Buff(A.AetherhuesII));
        p.Gcd(A.AeroIIInGreen).When(c => c.Buff(A.Aetherhues));
        p.Gcd(A.FireIIInRed);
    }
}
