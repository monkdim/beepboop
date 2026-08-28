using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Paladin.PaladinActions;

namespace OneTwoPunch.Core.Jobs.Paladin;

/// <summary>
/// Paladin, Dawntrail. A three step physical combo that hands out two buffs - Atonement
/// stacks and Divine Might - and a sixty second magic burst that spends them, opened by
/// Fight or Flight and Imperator and closed by the Confiteor chain.
/// <para>
/// Nothing here touches mitigation. Sheltron, Sentinel, Guardian, Hallowed Ground,
/// Intervention, Cover, Passage of Arms and Divine Veil are all declared so the verifier
/// checks their ids, and none of them is in a rule: when to press them is a judgement about
/// the fight, not about the rotation, and it belongs to the player.
/// </para>
/// <para>
/// No scripted opener. The chart has not been checked, and a wrong script overrides the
/// priority list, which is worse than not having one - the list below opens correctly on
/// its own, because Fight or Flight and Imperator are both simply "as soon as they are up".
/// </para>
/// </summary>
public sealed class PaladinRotation : JobRotationBase
{
    public override uint JobId => 19;

    public override string Name => "Paladin";

    public override ActionRef SingleTargetButton => A.FastBlade;

    public override ActionRef AoeButton => A.TotalEclipse;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? BurstAction => A.FightOrFlight;

    public override StatusRef? BurstStatus => A.FightOrFlightBuff;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        p.OGcd(A.FightOrFlight).When(c => !c.Downtime).Because("burst window");

        // Requiescat and its upgrade are the same press. It is held for Fight or Flight so
        // the Confiteor chain lands inside the twenty seconds that buff it - except where
        // Fight or Flight does not exist yet, which is only levels 68 and 69.
        p.OGcd(c => RequiescatAction(c))
            .When(c => !c.Downtime && (c.Buff(A.FightOrFlightBuff) || !c.Has(A.FightOrFlight)))
            .Because("magic burst");

        p.OGcd(A.BladeOfHonor)
            .When(c => c.Buff(A.BladeOfHonorReady))
            .Because("Blade of Honor Ready");

        p.OGcd(A.CircleOfScorn).When(c => !c.Downtime);
        p.OGcd(c => ExpiacionAction(c)).When(c => !c.Downtime);

        // A dash, so it is only ever offered inside the burst it belongs to. Suggesting a
        // gap closer as filler moves the player somewhere they did not ask to be.
        p.OGcd(A.Intervene)
            .When(c => !c.Downtime && c.Buff(A.FightOrFlightBuff))
            .Because("spend a charge in burst");

        // ---- GCDs --------------------------------------------------------
        // Confiteor opens the chain and the three blades finish it.
        p.Gcd(A.Confiteor).When(c => c.Buff(A.ConfiteorReady)).Because("Confiteor Ready");

        // Which blade is live is asked two ways, and this is deliberate.
        //
        // The job gauge carries a Confiteor combo step of its own, and a gauge field for a
        // chain is the tell that the ordinary combo does not track it - Viper's coils are
        // exactly that shape, and asking the combo about them left the whole branch
        // unreachable for weeks. But Dalamud does not expose Paladin's field: it hands over
        // the Oath gauge and nothing else.
        //
        // So the Requiescat stacks stand in for it. Confiteor and the three blades spend one
        // each, which makes the count a position in the chain that always moves forwards -
        // three is Blade of Faith, two Blade of Truth, one Blade of Valor - and a count can
        // never leave the button stuck the way an ordering guess can. The combo checks sit
        // above in case the chain is the ordinary combo after all, and cost nothing if not.
        p.Gcd(A.BladeOfValor).When(c => c.ComboIs(A.BladeOfTruth));
        p.Gcd(A.BladeOfTruth).When(c => c.ComboIs(A.BladeOfFaith));
        p.Gcd(A.BladeOfFaith).When(c => c.ComboIs(A.Confiteor));

        p.Gcd(A.BladeOfValor).When(c => c.BuffStacks(A.RequiescatBuff) == 1);
        p.Gcd(A.BladeOfTruth).When(c => c.BuffStacks(A.RequiescatBuff) == 2);
        p.Gcd(A.BladeOfFaith).When(c => c.BuffStacks(A.RequiescatBuff) == 3);

        p.Gcd(A.GoringBlade)
            .When(c => c.Buff(A.GoringBladeReady))
            .Because("Goring Blade Ready");

        // Under Requiescat every Holy Spirit is an instant and hits for a great deal more,
        // so the stacks are spent before anything physical.
        p.Gcd(A.HolySpirit)
            .When(c => c.Buff(A.RequiescatBuff))
            .Because("Requiescat");

        // The Atonement chain, deepest step first. Each is named outright by the buff the
        // one before it left.
        p.Gcd(A.Sepulchre).When(c => c.Buff(A.SepulchreReady));
        p.Gcd(A.Supplication).When(c => c.Buff(A.SupplicationReady));
        p.Gcd(A.Atonement).When(c => c.Buff(A.AtonementReady));

        // Divine Might makes Holy Spirit instant and free, which is also what makes it the
        // movement answer for a job whose only other ranged global is a tomahawk.
        p.Gcd(A.HolySpirit)
            .When(c => c.Buff(A.DivineMight))
            .Because("Divine Might");

        // The physical combo. Royal Authority is what refills both buffs above.
        p.Gcd(c => FinisherAction(c)).When(c => c.ComboIs(A.RiotBlade));
        p.Gcd(A.RiotBlade).When(c => c.ComboIs(A.FastBlade));
        p.Gcd(A.FastBlade);

        p.Gcd(A.ShieldLob).When(c => !c.InRange).Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.FightOrFlight).When(c => !c.Downtime).Because("burst window");

        p.OGcd(c => RequiescatAction(c))
            .When(c => !c.Downtime && (c.Buff(A.FightOrFlightBuff) || !c.Has(A.FightOrFlight)))
            .Because("magic burst");

        p.OGcd(A.BladeOfHonor).When(c => c.Buff(A.BladeOfHonorReady));
        p.OGcd(A.CircleOfScorn).When(c => !c.Downtime);
        p.OGcd(c => ExpiacionAction(c)).When(c => !c.Downtime);

        // The blades are all circles around the target, so the burst is the same one.
        p.Gcd(A.Confiteor).When(c => c.Buff(A.ConfiteorReady));

        p.Gcd(A.BladeOfValor).When(c => c.ComboIs(A.BladeOfTruth));
        p.Gcd(A.BladeOfTruth).When(c => c.ComboIs(A.BladeOfFaith));
        p.Gcd(A.BladeOfFaith).When(c => c.ComboIs(A.Confiteor));

        p.Gcd(A.BladeOfValor).When(c => c.BuffStacks(A.RequiescatBuff) == 1);
        p.Gcd(A.BladeOfTruth).When(c => c.BuffStacks(A.RequiescatBuff) == 2);
        p.Gcd(A.BladeOfFaith).When(c => c.BuffStacks(A.RequiescatBuff) == 3);

        p.Gcd(A.HolyCircle).When(c => c.Buff(A.RequiescatBuff)).Because("Requiescat");
        p.Gcd(A.HolyCircle).When(c => c.Buff(A.DivineMight)).Because("Divine Might");

        p.Gcd(A.Prominence).When(c => c.ComboIs(A.TotalEclipse));
        p.Gcd(A.TotalEclipse);
    }

    /// <summary>Royal Authority once it exists, and Rage of Halone until then.</summary>
    private static ActionRef FinisherAction(RotationContext c) =>
        c.Has(A.RoyalAuthority) ? A.RoyalAuthority : A.RageOfHalone;

    /// <summary>Imperator is Requiescat's upgrade; both are the same press.</summary>
    private static ActionRef RequiescatAction(RotationContext c) =>
        c.Has(A.Imperator) ? A.Imperator : A.Requiescat;

    /// <summary>Expiacion is Spirits Within's upgrade.</summary>
    private static ActionRef ExpiacionAction(RotationContext c) =>
        c.Has(A.Expiacion) ? A.Expiacion : A.SpiritsWithin;

    /// <summary>What the recorded log prints for a Paladin line.</summary>
    public override string DescribeGauge(CombatSnapshot snapshot) =>
        $"oath {snapshot.Gauges.Paladin.Oath}";
}
