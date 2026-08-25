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

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // The raid buff, and the two muses that only exist because a motif was painted
        // earlier - which is the whole reason motifs get painted during downtime.
        p.OGcd(A.ScenicMuse)
            .When(c => !c.Downtime && c.Pct.LandscapeMotifDrawn)
            .Because("raid buff");

        p.OGcd(A.StrikingMuse)
            .When(c => !c.Downtime && c.Pct.WeaponMotifDrawn)
            .Because("spend the weapon motif");

        p.OGcd(A.LivingMuse)
            .When(c => !c.Downtime && c.Pct.CreatureMotifDrawn)
            .Because("spend the creature motif");

        // The two portraits, once their halves are complete.
        p.OGcd(A.MogOfTheAges).When(c => c.Pct.MooglePortraitReady);
        p.OGcd(A.RetributionOfTheMadeen).When(c => c.Pct.MadeenPortraitReady);

        p.OGcd(A.SubtractivePalette)
            .When(c => c.Pct.PaletteGauge >= 50 && !c.Buff(A.SubtractivePaletteBuff))
            .Because("palette is close to capping");

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

        // Motifs root you, so they are painted while standing still - ideally in downtime -
        // and never while moving.
        p.Gcd(A.LandscapeMotif)
            .When(c => !c.Moving && !c.Pct.LandscapeMotifDrawn && c.ReadyIn(A.ScenicMuse, 15f))
            .Because("paint before the buff window");

        p.Gcd(A.WeaponMotif)
            .When(c => !c.Moving && !c.Pct.WeaponMotifDrawn)
            .Because("paint while you can stand still");

        p.Gcd(A.CreatureMotif)
            .When(c => !c.Moving && !c.Pct.CreatureMotifDrawn)
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

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.ScenicMuse).When(c => !c.Downtime && c.Pct.LandscapeMotifDrawn).Because("raid buff");
        p.OGcd(A.StrikingMuse).When(c => !c.Downtime && c.Pct.WeaponMotifDrawn);
        p.OGcd(A.LivingMuse).When(c => !c.Downtime && c.Pct.CreatureMotifDrawn);
        p.OGcd(A.MogOfTheAges).When(c => c.Pct.MooglePortraitReady);
        p.OGcd(A.RetributionOfTheMadeen).When(c => c.Pct.MadeenPortraitReady);

        p.OGcd(A.SubtractivePalette)
            .When(c => c.Pct.PaletteGauge >= 50 && !c.Buff(A.SubtractivePaletteBuff));

        p.Gcd(A.StarPrism).When(c => c.Buff(A.Starstruck));
        p.Gcd(A.RainbowDrip).When(c => c.Buff(A.RainbowBright));

        p.Gcd(A.PolishingHammer).When(c => c.Buff(A.HammerTime) && c.Ready(A.PolishingHammer));
        p.Gcd(A.HammerBrush).When(c => c.Buff(A.HammerTime) && c.Ready(A.HammerBrush));
        p.Gcd(A.HammerStamp).When(c => c.Buff(A.HammerTime));

        p.Gcd(A.CometInBlack).When(c => c.Buff(A.MonochromeTones) && c.Pct.Paint > 0);
        p.Gcd(A.HolyInWhite).When(c => c.Pct.Paint > 0 && (c.Moving || c.Pct.Paint >= 5));

        p.Gcd(A.LandscapeMotif)
            .When(c => !c.Moving && !c.Pct.LandscapeMotifDrawn && c.ReadyIn(A.ScenicMuse, 15f));

        p.Gcd(A.WeaponMotif).When(c => !c.Moving && !c.Pct.WeaponMotifDrawn);
        p.Gcd(A.CreatureMotif).When(c => !c.Moving && !c.Pct.CreatureMotifDrawn);

        p.Gcd(A.ThunderIIInMagenta).When(c => c.Buff(A.AetherhuesII) && c.Buff(A.SubtractivePaletteBuff));
        p.Gcd(A.StoneIIInYellow).When(c => c.Buff(A.Aetherhues) && c.Buff(A.SubtractivePaletteBuff));
        p.Gcd(A.BlizzardIIInCyan).When(c => c.Buff(A.SubtractivePaletteBuff));

        p.Gcd(A.WaterIIInBlue).When(c => c.Buff(A.AetherhuesII));
        p.Gcd(A.AeroIIInGreen).When(c => c.Buff(A.Aetherhues));
        p.Gcd(A.FireIIInRed);
    }
}
