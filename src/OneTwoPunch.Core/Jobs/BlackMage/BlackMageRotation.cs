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

        // Triplecast is the answer to movement, so it is held for it rather than spent on
        // damage - a Black Mage who has to move with no instants loses far more.
        p.OGcd(A.Triplecast)
            .When(c => c.Moving && !c.Buff(A.TriplecastBuff))
            .Because("you are moving");

        p.OGcd(A.Swiftcast)
            .When(c => c.Moving && !c.Buff(A.SwiftcastBuff))
            .Because("you are moving");

        // ---- GCDs --------------------------------------------------------
        // Everything instant, first, while moving.
        p.Gcd(A.Xenoglossy)
            .When(c => c.Moving && c.Blm.PolyglotStacks > 0)
            .Because("instant, you are moving");

        p.Gcd(A.Paradox)
            .When(c => c.Moving && c.Blm.ParadoxActive)
            .Because("instant, you are moving");

        p.Gcd(c => ThunderAction(c))
            .When(c => c.Moving && c.Buff(A.Thunderhead))
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

        p.Gcd(A.Blizzard3).When(c => !c.Blm.InAstralFire).Because("into Umbral Ice");

        p.Gcd(A.Fire1);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.LeyLines).When(c => !c.Downtime && !c.Moving);
        p.OGcd(A.Amplifier).When(c => c.Blm.PolyglotStacks < 2);
        p.OGcd(A.Manafont).When(c => c.Blm.InAstralFire && !c.Downtime);
        p.OGcd(A.Triplecast).When(c => c.Moving && !c.Buff(A.TriplecastBuff)).Because("you are moving");

        p.Gcd(A.Foul)
            .When(c => c.Moving && c.Blm.PolyglotStacks > 0)
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
            .When(c => !c.Blm.InAstralFire)
            .Because("into Umbral Ice");

        p.Gcd(c => c.Has(A.HighFire2) ? A.HighFire2 : A.Fire2);
    }

    /// <summary>The thunder the player actually has, which upgrades twice.</summary>
    private static ActionRef ThunderAction(RotationContext c) =>
        c.Has(A.HighThunder) ? A.HighThunder : A.Thunder3;
}
