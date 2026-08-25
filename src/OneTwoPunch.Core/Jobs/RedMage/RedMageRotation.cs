using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.RedMage.RedMageActions;

namespace OneTwoPunch.Core.Jobs.RedMage;

/// <summary>
/// Red Mage, Dawntrail. Build two mana colours to sixty or so while keeping them within a
/// few points of each other, then spend the lot on the melee combo.
/// <para>
/// Dualcast makes every second spell instant, which is what lets a Red Mage move without
/// losing anything - so while moving the rules prefer whatever is already instant, and only
/// fall back to a hard cast when there is nothing else.
/// </para>
/// </summary>
public sealed class RedMageRotation : JobRotationBase
{
    public override uint JobId => 35;

    public override string Name => "Red Mage";

    public override ActionRef SingleTargetButton => A.Jolt;

    public override ActionRef AoeButton => A.Scatter;

    public override float AoeRadius => 5f;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override StatusRef? BurstStatus => A.EmboldenBuff;

    public override ActionRef? BurstAction => A.Embolden;

    /// <summary>
    /// The Balance's "Standard Opener" for Red Mage level 100, Dawntrail patch 7.0,
    /// including the first loop out of the burst.
    /// </summary>
    private static readonly Opener Sequence = new(
        "The Balance standard", 100,
        A.VeraeroIII,
        A.VerthunderIII, A.Swiftcast,
        A.VerthunderIII, A.Fleche, A.Acceleration,
        A.VerthunderIII, A.Embolden, A.Manafication,
        A.EnchantedRiposte, A.ContreSixte,
        A.EnchantedZwerchhau, A.Engagement,
        A.EnchantedRedoublement, A.CorpsACorps,
        A.Verholy, A.ViceOfThorns,
        A.Scorch, A.Engagement, A.CorpsACorps,
        A.Resolution, A.Prefulgence,
        A.GrandImpact, A.Acceleration,
        A.Verfire,
        A.GrandImpact,
        A.VerthunderIII, A.Fleche,
        A.VeraeroIII,
        A.Verfire,
        A.VerthunderIII,
        A.Verstone,
        A.VeraeroIII, A.Swiftcast,
        A.VeraeroIII, A.ContreSixte)
    {
        // Drunk right after Swiftcast, in the first global's weave window.
        PotionBeforeStep = 3,
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
        p.OGcd(A.Embolden).When(c => !c.Downtime).Because("raid buff");

        p.OGcd(A.Manafication)
            .When(c => !c.Downtime && c.Rdm.ManaStacks == 0 && c.Rdm.LowerMana <= 50)
            .Because("refill both colours");

        p.OGcd(A.Fleche).When(c => !c.Downtime);
        p.OGcd(A.ContreSixte).When(c => !c.Downtime);
        p.OGcd(A.ViceOfThorns).When(c => c.Buff(A.ThornedFlourish));
        p.OGcd(A.Prefulgence).When(c => c.Buff(A.PrefulgenceReady));

        p.OGcd(A.Acceleration)
            .When(c => !c.Buff(A.AccelerationBuff) && !c.Buff(A.Dualcast))
            .Because("guarantees the next proc");

        p.OGcd(A.Swiftcast)
            .When(c => c.Moving && !c.Buff(A.Dualcast) && !c.Buff(A.SwiftcastBuff))
            .Because("you are moving");

        // ---- GCDs --------------------------------------------------------
        // The melee combo, resolved backwards so the deepest live step wins. Each step is
        // instant and closes the gap, so once it starts it runs regardless of movement.
        p.Gcd(A.Resolution).When(c => c.Ready(A.Resolution));
        p.Gcd(A.Scorch).When(c => c.Ready(A.Scorch));

        // Whichever finisher keeps the two colours closer together.
        p.Gcd(A.Verholy)
            .When(c => c.Rdm.ManaStacks >= 3 && c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because("balance the gauge");

        p.Gcd(A.Verflare).When(c => c.Rdm.ManaStacks >= 3);

        p.Gcd(A.EnchantedRedoublement).When(c => c.ComboIs(A.EnchantedZwerchhau));
        p.Gcd(A.EnchantedZwerchhau).When(c => c.ComboIs(A.EnchantedRiposte));

        // Enough of both colours to see the combo through without dropping out of it.
        p.Gcd(A.EnchantedRiposte)
            .When(c => c.Rdm.LowerMana >= 50 && !c.Downtime && c.InRange)
            .Because("spend the gauge");

        // Free instants.
        p.Gcd(A.GrandImpact).When(c => c.Buff(A.GrandImpactReady)).Because("free and instant");

        // Procs are instant, so they are what you want while moving.
        p.Gcd(A.Verstone)
            .When(c => c.Buff(A.VerstoneReady) && c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because(c => c.Moving ? "instant, you are moving" : "proc");

        p.Gcd(A.Verfire)
            .When(c => c.Buff(A.VerfireReady))
            .Because(c => c.Moving ? "instant, you are moving" : "proc");

        p.Gcd(A.Verstone).When(c => c.Buff(A.VerstoneReady)).Because("proc");

        // Hard casts, taking whichever colour is behind.
        p.Gcd(c => c.Has(A.VeraeroIII) ? A.VeraeroIII : A.Veraero)
            .When(c => c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because("build the lower colour");

        p.Gcd(c => c.Has(A.VerthunderIII) ? A.VerthunderIII : A.Verthunder)
            .Because("build the lower colour");

        p.Gcd(c => c.Has(A.JoltIII) ? A.JoltIII : c.Has(A.JoltII) ? A.JoltII : A.Jolt);
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(A.Embolden).When(c => !c.Downtime).Because("raid buff");
        p.OGcd(A.Manafication)
            .When(c => !c.Downtime && c.Rdm.ManaStacks == 0 && c.Rdm.LowerMana <= 50);
        p.OGcd(A.Fleche).When(c => !c.Downtime);
        p.OGcd(A.ContreSixte).When(c => !c.Downtime);
        p.OGcd(A.ViceOfThorns).When(c => c.Buff(A.ThornedFlourish));
        p.OGcd(A.Prefulgence).When(c => c.Buff(A.PrefulgenceReady));
        p.OGcd(A.Acceleration).When(c => !c.Buff(A.AccelerationBuff) && !c.Buff(A.Dualcast));

        p.Gcd(A.Resolution).When(c => c.Ready(A.Resolution));
        p.Gcd(A.Scorch).When(c => c.Ready(A.Scorch));

        p.Gcd(A.Verholy)
            .When(c => c.Rdm.ManaStacks >= 3 && c.Rdm.WhiteMana <= c.Rdm.BlackMana);

        p.Gcd(A.Verflare).When(c => c.Rdm.ManaStacks >= 3);

        p.Gcd(A.EnchantedMoulinetTrois).When(c => c.Ready(A.EnchantedMoulinetTrois));
        p.Gcd(A.EnchantedMoulinetDeux).When(c => c.Ready(A.EnchantedMoulinetDeux));

        p.Gcd(A.EnchantedMoulinet)
            .When(c => c.Rdm.LowerMana >= 50 && !c.Downtime && c.InRange)
            .Because("spend the gauge");

        p.Gcd(A.GrandImpact).When(c => c.Buff(A.GrandImpactReady)).Because("free and instant");

        p.Gcd(A.VeraeroII)
            .When(c => c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because("build the lower colour");

        p.Gcd(A.VerthunderII).Because("build the lower colour");

        p.Gcd(c => c.Has(A.Impact) ? A.Impact : A.Scatter);
    }
}
