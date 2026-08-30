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

        // ---- The filler, which is a pair of globals rather than one ------
        // This was the whole of what was wrong with the list, and it was wrong twice.
        //
        // Cast times, from BossMod's own annotations: Verthunder III and Veraero III are
        // five seconds. Jolt III, Verfire and Verstone are two. Grand Impact is instant.
        // Dualcast makes the next spell instant and is earned by finishing a spell that had
        // a cast time - so the filler is two globals, not one: spend two seconds earning
        // Dualcast, then spend Dualcast on a five second spell that now costs nothing.
        //
        // The list had no pairing at all. "Build the lower colour" on Verthunder III carried
        // no condition whatsoever, so it matched every single time the list reached it: the
        // five second casts went out hard, one after another, which is what a Red Mage looks
        // like when they do not know about Dualcast. And because it always matched, the Jolt
        // underneath it was unreachable - not rarely chosen, but dead. Reported from a pull
        // as "Jolt isn't being cast at all, it's hard casting my 5 second casts".

        // Dualcast in hand: spend it on the spell that costs the most to hard cast, taking
        // whichever colour is behind. This is above the procs on purpose - a proc is a two
        // second cast and belongs in the half of the pair that earns the next Dualcast, so
        // spending Dualcast on one would waste four fifths of it.
        p.Gcd(c => AeroSpell(c))
            .When(c => NextCastIsFree(c) && c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because("free under Dualcast, and white is behind");

        p.Gcd(c => ThunderSpell(c))
            .When(c => NextCastIsFree(c))
            .Because("free under Dualcast");

        // No Dualcast: a two second spell, which earns the one the global above spends.
        //
        // Grand Impact is already instant, so a Dualcast spent on it is a Dualcast thrown
        // away - hence it waits here rather than sitting above the pair.
        p.Gcd(A.GrandImpact)
            .When(c => c.Buff(A.GrandImpactReady) && !NextCastIsFree(c))
            .Because("free and instant");

        // Procs first among the two second casts: they expire, and the pair comes round
        // again within two globals either way.
        p.Gcd(A.Verstone)
            .When(c => c.Buff(A.VerstoneReady) && c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because(c => c.Moving ? "instant, you are moving" : "proc");

        p.Gcd(A.Verfire)
            .When(c => c.Buff(A.VerfireReady))
            .Because(c => c.Moving ? "instant, you are moving" : "proc");

        p.Gcd(A.Verstone).When(c => c.Buff(A.VerstoneReady)).Because("proc");

        // And with no proc either, Jolt - two seconds for the Dualcast, rather than five for
        // a spell the Dualcast was about to make free. Jolt is level 2, so this rung always
        // exists and the list can never run out of an answer here.
        p.Gcd(c => JoltSpell(c)).Because("two seconds, and it earns the Dualcast");

        // The five second casts taken hard, which is what is left when Dualcast cannot be
        // earned in time - a proc-less opener step, or coming back from downtime with the
        // pair out of phase. Below Jolt because it is the worse half of every trade above.
        p.Gcd(c => AeroSpell(c))
            .When(c => c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because("build the lower colour");

        p.Gcd(c => ThunderSpell(c)).Because("build the lower colour");
    }

    /// <summary>
    /// Whether the next spell will go off instantly, and so whether a five second cast is
    /// actually a five second cast.
    /// <para>
    /// Only the statuses are asked, not what granted them - Acceleration grants Dualcast on
    /// the levels that have it, and asking about Dualcast picks that up without this needing
    /// to know which patch changed what.
    /// </para>
    /// </summary>
    private static bool NextCastIsFree(RotationContext c) =>
        c.Buff(A.Dualcast) || c.Buff(A.SwiftcastBuff);

    /// <summary>The white five second cast, at whichever rank the player has.</summary>
    private static ActionRef AeroSpell(RotationContext c) =>
        c.Has(A.VeraeroIII) ? A.VeraeroIII : A.Veraero;

    /// <summary>The black five second cast, at whichever rank the player has.</summary>
    private static ActionRef ThunderSpell(RotationContext c) =>
        c.Has(A.VerthunderIII) ? A.VerthunderIII : A.Verthunder;

    /// <summary>The two second cast that earns the Dualcast, at whichever rank.</summary>
    private static ActionRef JoltSpell(RotationContext c) =>
        c.Has(A.JoltIII) ? A.JoltIII : c.Has(A.JoltII) ? A.JoltII : A.Jolt;

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

        // The cast times run the other way round here, so this ordering is right as it
        // stands and is not the single-target bug wearing AoE clothes.
        //
        // Veraero II and Verthunder II are two second casts; Scatter and Impact are five.
        // So the two spells that build mana are also the cheap ones, they alternate into
        // each other's Dualcast by themselves, and the five second rung below is correctly
        // never reached above level 18 - which is where Verthunder II arrives and Scatter
        // stops being the only thing there is.
        p.Gcd(A.VeraeroII)
            .When(c => c.Rdm.WhiteMana <= c.Rdm.BlackMana)
            .Because("build the lower colour");

        p.Gcd(A.VerthunderII).Because("build the lower colour");

        p.Gcd(c => c.Has(A.Impact) ? A.Impact : A.Scatter);
    }
}
