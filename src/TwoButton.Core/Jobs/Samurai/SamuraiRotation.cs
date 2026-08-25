using TwoButton.Core.Engine;
using TwoButton.Core.Model;
using A = TwoButton.Core.Jobs.Samurai.SamuraiActions;

namespace TwoButton.Core.Jobs.Samurai;

/// <summary>
/// Samurai, Dawntrail. Three combo branches feed three different Sen; three Sen spend into
/// an Iaijutsu. The branch choice is the whole job, and it is driven by which of the two
/// self-buffs is running out and which Sen is still missing.
/// <para>
/// No scripted opener. Samurai's opening varies by GCD tier and a wrong script overrides
/// the priority list, which is worse than not having one - the list below opens correctly
/// on its own.
/// </para>
/// </summary>
public sealed class SamuraiRotation : JobRotationBase
{
    public override uint JobId => 34;

    public override string Name => "Samurai";

    public override ActionRef SingleTargetButton => A.Hakaze;

    public override ActionRef AoeButton => A.Fuga;

    public override float AoeRadius => 8f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? PositionalRescue => A.TrueNorth;

    public override StatusRef? PositionalRescueStatus => A.TrueNorthBuff;

    public override ActionRef? BurstAction => A.Ikishoten;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // Ikishoten grants 50 Kenki, so throwing it at a full gauge wastes most of it.
        p.OGcd(A.Ikishoten)
            .When(c => !c.Downtime && c.Sam.Kenki <= 50)
            .Because("burst window");

        p.OGcd(A.Zanshin)
            .When(c => c.Buff(A.ZanshinReady) && c.Sam.Kenki >= 50)
            .Because("Zanshin Ready");

        // Held until Ikishoten has been spent, so the two do not compete for the same Kenki.
        p.OGcd(A.HissatsuSenei)
            .When(c => !c.Downtime && c.Sam.Kenki >= 25 && !c.Ready(A.Ikishoten))
            .Because("biggest Kenki spend");

        p.OGcd(A.Shoha)
            .When(c => c.Sam.Meditation >= 3)
            .Because("Meditation is full");

        // Meikyo's real job is getting the two self-buffs back up: it hands out combo
        // finishers for free, which is how a dropped Fugetsu gets fixed without losing GCDs.
        p.OGcd(A.MeikyoShisui)
            .When(c => !c.Downtime && !c.Buff(A.MeikyoShisuiBuff) && !HasBothBuffs(c))
            .Because("restore Fugetsu and Fuka");

        // Kenki caps at 100 and Senei wants 25 held back for it.
        p.OGcd(A.HissatsuShinten)
            .When(c => c.Sam.Kenki >= 50 || (c.Sam.Kenki >= 25 && !c.ReadyIn(A.HissatsuSenei, 20f)))
            .Because("spend Kenki before it caps");

        // ---- GCDs --------------------------------------------------------
        // Iaijutsu follow-ups are free damage and expire, so they come first. Each is gated
        // by the game itself, so an unavailable one is skipped without needing a status.
        p.Gcd(A.TendoKaeshiSetsugekka).When(c => c.Buff(A.TendoKaeshiSetsugekkaBuff));
        p.Gcd(A.KaeshiSetsugekka).When(c => c.Buff(A.KaeshiSetsugekkaBuff));
        p.Gcd(A.KaeshiNamikiri);

        // Only worth spending once both damage buffs are actually up.
        p.Gcd(A.OgiNamikiri)
            .When(c => c.Buff(A.OgiNamikiriReady) && HasBothBuffs(c) && !c.Moving)
            .Because("Ogi Namikiri Ready");

        // Three Sen. Tendo upgrades it and is worth more, so it is checked first.
        p.Gcd(A.TendoSetsugekka)
            .When(c => c.Sam.SenCount == 3 && c.Buff(A.Tendo));

        p.Gcd(A.MidareSetsugekka)
            .When(c => c.Sam.SenCount == 3);

        // One Sen only goes into the dot, and only when the dot actually needs it.
        p.Gcd(A.Higanbana)
            .When(c => c.Sam.SenCount == 1
                       && c.DotExpiring(A.HiganbanaBuff, 5f)
                       && HasBothBuffs(c)
                       && !c.Downtime)
            .Because("refresh Higanbana");

        // Combo finishers.
        p.Gcd(A.Gekko)
            .When(c => c.ComboIs(A.Jinpu) || (c.Buff(A.MeikyoShisuiBuff) && NeedsFugetsu(c)))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.Kasha)
            .When(c => c.ComboIs(A.Shifu) || (c.Buff(A.MeikyoShisuiBuff) && NeedsFuka(c)))
            .Needs(PositionalHint.Flank);

        p.Gcd(A.Yukikaze)
            .When(c => c.ComboIs(A.Hakaze) && ChooseBranch(c) == Branch.Ice);

        // Second step of the combo. Which branch depends on which buff is dropping and
        // which Sen is still missing - this is the decision the job is actually about.
        p.Gcd(A.Jinpu).When(c => c.ComboIs(A.Hakaze) && ChooseBranch(c) == Branch.Moon);
        p.Gcd(A.Shifu).When(c => c.ComboIs(A.Hakaze) && ChooseBranch(c) == Branch.Flower);

        p.Gcd(A.Hakaze);

        p.Gcd(A.Enpi)
            .When(c => !c.InRange)
            .Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.Ikishoten).When(c => !c.Downtime).Because("burst window");

        p.OGcd(A.Zanshin)
            .When(c => c.Buff(A.ZanshinReady) && c.Sam.Kenki >= 50);

        p.OGcd(A.HissatsuGuren)
            .When(c => !c.Downtime && c.Sam.Kenki >= 25)
            .Because("Kenki spend");

        p.OGcd(A.MeikyoShisui)
            .When(c => !c.Downtime && !c.Buff(A.MeikyoShisuiBuff) && c.ComboBroken);

        p.OGcd(A.HissatsuKyuten)
            .When(c => c.Sam.Kenki >= 50)
            .Because("spend Kenki before it caps");

        p.Gcd(A.TendoKaeshiGoken).When(c => c.Buff(A.TendoKaeshiGokenBuff));
        p.Gcd(A.KaeshiGoken).When(c => c.Buff(A.KaeshiGokenBuff));

        p.Gcd(A.TendoGoken).When(c => c.Sam.SenCount >= 2 && c.Buff(A.Tendo));
        p.Gcd(A.TenkaGoken).When(c => c.Sam.SenCount >= 2);

        // Mangetsu and Oka refresh the same two buffs as the single-target branches.
        p.Gcd(A.Mangetsu)
            .When(c => (c.ComboIs(A.Fuko) || c.ComboIs(A.Fuga))
                       && (NeedsFugetsu(c) || !c.Sam.HasGetsu));

        p.Gcd(A.Oka).When(c => c.ComboIs(A.Fuko) || c.ComboIs(A.Fuga));

        p.Gcd(c => c.Has(A.Fuko) ? A.Fuko : A.Fuga);
    }

    private enum Branch
    {
        /// <summary>Jinpu into Gekko. Refreshes Fugetsu, banks Getsu.</summary>
        Moon,

        /// <summary>Shifu into Kasha. Refreshes Fuka, banks Ka.</summary>
        Flower,

        /// <summary>Yukikaze. Banks Setsu, and is one GCD shorter.</summary>
        Ice,
    }

    /// <summary>
    /// Which combo branch to take out of Hakaze. Keeping the two self-buffs up beats banking
    /// a Sen, because losing Fugetsu costs damage on everything that follows.
    /// </summary>
    private static Branch ChooseBranch(RotationContext c)
    {
        if (NeedsFugetsu(c))
            return Branch.Moon;

        if (NeedsFuka(c))
            return Branch.Flower;

        if (!c.Sam.HasSetsu)
            return Branch.Ice;

        if (!c.Sam.HasKa)
            return Branch.Flower;

        if (!c.Sam.HasGetsu)
            return Branch.Moon;

        return Branch.Ice;
    }

    /// <summary>
    /// Both self-buffs up. Most of Samurai's big spends are only worth it under both, so
    /// this gates them rather than each rule re-deriving it.
    /// </summary>
    private static bool HasBothBuffs(RotationContext c) =>
        (!c.Has(A.Jinpu) || c.Buff(A.Fugetsu)) && (!c.Has(A.Shifu) || c.Buff(A.Fuka));

    /// <summary>True when Fugetsu will not survive another two globals.</summary>
    private static bool NeedsFugetsu(RotationContext c) =>
        c.Has(A.Jinpu) && c.BuffTime(A.Fugetsu) < c.GcdTotal * 2f;

    private static bool NeedsFuka(RotationContext c) =>
        c.Has(A.Shifu) && c.BuffTime(A.Fuka) < c.GcdTotal * 2f;
}
