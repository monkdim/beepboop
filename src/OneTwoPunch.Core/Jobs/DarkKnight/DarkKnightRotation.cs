using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.DarkKnight.DarkKnightActions;

namespace OneTwoPunch.Core.Jobs.DarkKnight;

/// <summary>
/// Dark Knight, Dawntrail. A three step combo that pays for itself in mana, a Blood gauge
/// that spends into Bloodspiller, and Darkside - a ten percent damage buff that only Edge
/// and Flood extend, and that the job is expected to hold for the entire fight.
/// <para>
/// Darkside is the reason the mana rules look aggressive. Letting it fall off is a bigger
/// loss than any global here can make back, so Edge outranks everything the moment the
/// timer gets short, and otherwise it is spent whenever there is mana for it - which is
/// what keeps the timer topped up in the first place.
/// </para>
/// <para>
/// Mitigation is not here: Shadow Wall, Shadowed Vigil, Dark Mind, The Blackest Night,
/// Oblation, Dark Missionary and Living Dead are declared for verification and left to the
/// player.
/// </para>
/// <para>
/// No scripted opener - see PaladinRotation for why.
/// </para>
/// </summary>
public sealed class DarkKnightRotation : JobRotationBase
{
    public override uint JobId => 32;

    public override string Name => "Dark Knight";

    public override ActionRef SingleTargetButton => A.HardSlash;

    public override ActionRef AoeButton => A.Unleash;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? BurstAction => A.LivingShadow;

    /// <summary>What Edge and Flood cost, and so the floor for spending one at all.</summary>
    private const uint EdgeMp = 3000;

    /// <summary>
    /// Mana kept back from an ordinary Edge so there is always enough to rescue Darkside
    /// if the timer runs down while the combo is somewhere unhelpful.
    /// </summary>
    private const uint EdgeReserveMp = 6000;

    /// <summary>Seconds of Darkside left at which the rescue outranks everything else.</summary>
    private const float DarksideRescueWindow = 10f;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // Darkside about to drop outranks everything. Ten seconds is three globals of
        // warning, which is enough to find a weave slot without clipping for it.
        p.OGcd(c => EdgeAction(c))
            .When(c => DarksideIsRunningOut(c) && (c.Drk.HasDarkArts || c.Mp >= EdgeMp))
            .Because("Darkside is about to drop");

        p.OGcd(A.LivingShadow).When(c => !c.Downtime).Because("burst window");

        p.OGcd(A.Delirium).When(c => !c.Downtime).Because("free Bloodspillers");

        p.OGcd(A.CarveAndSpit).When(c => !c.Downtime);

        p.OGcd(A.SaltedEarth).When(c => !c.Downtime);

        p.OGcd(A.SaltAndDarkness).When(c => c.Buff(A.SaltedEarthBuff));

        p.OGcd(A.Shadowbringer).When(c => !c.Downtime);

        // A free Edge is never worth holding.
        p.OGcd(c => EdgeAction(c)).When(c => c.Drk.HasDarkArts).Because("Dark Arts is free");

        // And otherwise mana above the reserve is Darkside time waiting to be bought.
        p.OGcd(c => EdgeAction(c))
            .When(c => !c.Downtime && c.Mp >= EdgeReserveMp)
            .Because("spend the mana on Darkside");

        // ---- GCDs --------------------------------------------------------
        p.Gcd(A.Disesteem).When(c => c.Buff(A.Scorn)).Because("Scorn");

        // The Delirium chain. Which step is live is a gauge field, not the combo - Viper's
        // coils are the same shape and asking the combo about them meant the whole branch
        // was unreachable.
        p.Gcd(c => DeliriumAction(c))
            .When(c => c.Buff(A.EnhancedDelirium) && c.Has(A.ScarletDelirium))
            .Because("Delirium");

        // Below 96 Delirium has no chain of its own; it just makes Bloodspiller free.
        p.Gcd(A.Bloodspiller)
            .When(c => c.Buff(A.DeliriumBuff) && !c.Has(A.ScarletDelirium))
            .Because("free under Delirium");

        p.Gcd(A.Bloodspiller)
            .When(c => c.Drk.Blood >= 50)
            .Because("spend the Blood");

        p.Gcd(A.Souleater).When(c => c.ComboIs(A.SyphonStrike));
        p.Gcd(A.SyphonStrike).When(c => c.ComboIs(A.HardSlash));
        p.Gcd(A.HardSlash);

        p.Gcd(A.Unmend).When(c => !c.InRange).Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(c => FloodAction(c))
            .When(c => DarksideIsRunningOut(c) && (c.Drk.HasDarkArts || c.Mp >= EdgeMp))
            .Because("Darkside is about to drop");

        p.OGcd(A.LivingShadow).When(c => !c.Downtime).Because("burst window");
        p.OGcd(A.Delirium).When(c => !c.Downtime);
        p.OGcd(A.SaltedEarth).When(c => !c.Downtime);
        p.OGcd(A.SaltAndDarkness).When(c => c.Buff(A.SaltedEarthBuff));
        p.OGcd(A.AbyssalDrain).When(c => !c.Downtime);
        p.OGcd(A.Shadowbringer).When(c => !c.Downtime);

        p.OGcd(c => FloodAction(c)).When(c => c.Drk.HasDarkArts).Because("Dark Arts is free");

        p.OGcd(c => FloodAction(c))
            .When(c => !c.Downtime && c.Mp >= EdgeReserveMp)
            .Because("spend the mana on Darkside");

        p.Gcd(A.Disesteem).When(c => c.Buff(A.Scorn)).Because("Scorn");

        p.Gcd(A.Impalement)
            .When(c => c.Buff(A.EnhancedDelirium))
            .Because("Delirium");

        p.Gcd(A.Quietus)
            .When(c => c.Buff(A.DeliriumBuff) && !c.Has(A.Impalement))
            .Because("free under Delirium");

        p.Gcd(A.Quietus).When(c => c.Drk.Blood >= 50).Because("spend the Blood");

        p.Gcd(A.StalwartSoul).When(c => c.ComboIs(A.Unleash));
        p.Gcd(A.Unleash);
    }

    /// <summary>
    /// True when Darkside is short enough to be worth rescuing, or gone. Gone counts: the
    /// buff being absent entirely is the state this rule exists to get out of.
    /// </summary>
    private static bool DarksideIsRunningOut(RotationContext c) =>
        c.Drk.DarksideTimeRemaining < DarksideRescueWindow;

    /// <summary>Edge of Shadow is Edge of Darkness's upgrade; both are the same press.</summary>
    private static ActionRef EdgeAction(RotationContext c) =>
        c.Has(A.EdgeOfShadow) ? A.EdgeOfShadow : A.EdgeOfDarkness;

    /// <summary>Flood is the AoE half of the same press.</summary>
    private static ActionRef FloodAction(RotationContext c) =>
        c.Has(A.FloodOfShadow) ? A.FloodOfShadow : A.FloodOfDarkness;

    /// <summary>
    /// The Delirium chain, read off the gauge step rather than tracked here: 0 is Scarlet
    /// Delirium, 1 Comeuppance, 2 Torcleaver.
    /// </summary>
    private static ActionRef DeliriumAction(RotationContext c) => c.Drk.DeliriumStep switch
    {
        0 => A.ScarletDelirium,
        1 => A.Comeuppance,
        _ => A.Torcleaver,
    };

    /// <summary>
    /// What the recorded log prints for a Dark Knight line. Darkside is there because it is
    /// the thing most worth being able to see afterwards: a pull where it lapsed looks
    /// exactly like one where it did not until the number is on the page.
    /// </summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.DarkKnight;
        var arts = g.HasDarkArts ? ", dark arts" : string.Empty;
        var shadow = g.ShadowTimeRemaining > 0f ? $", shadow {g.ShadowTimeRemaining:0.0}s" : string.Empty;

        return $"blood {g.Blood} | darkside {g.DarksideTimeRemaining:0.0}s{arts}{shadow}";
    }
}
