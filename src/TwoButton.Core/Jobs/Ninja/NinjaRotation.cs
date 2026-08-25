using TwoButton.Core.Engine;
using TwoButton.Core.Model;
using A = TwoButton.Core.Jobs.Ninja.NinjaActions;

namespace TwoButton.Core.Jobs.Ninja;

/// <summary>
/// Ninja, Dawntrail.
/// <para>
/// <b>Mudras stay on their own keys.</b> A ninjutsu is not one action - it is two or three
/// mudra presses and then the cast, and collapsing that into a button that changes under
/// the player between presses is exactly the kind of thing this plugin refuses to do
/// blind. So the buttons drive everything else, and the moment the game says a charged
/// ninjutsu is usable the button becomes it - meaning you charge mudras yourself and the
/// button fires the result at the right time.
/// </para>
/// <para>
/// Ninja is the worst fit in the game for a two-key layout. If mudras are the part that
/// hurts, this job may simply not be the one to bring.
/// </para>
/// </summary>
public sealed class NinjaRotation : JobRotationBase
{
    public override uint JobId => 30;

    public override string Name => "Ninja";

    public override ActionRef SingleTargetButton => A.SpinningEdge;

    public override ActionRef AoeButton => A.DeathBlossom;

    public override IReadOnlyList<ActionRef> AllActions => A.All;

    public override IReadOnlyList<StatusRef> AllStatuses => A.AllStatuses;

    public override ActionRef? PositionalRescue => A.TrueNorth;

    public override StatusRef? PositionalRescueStatus => A.TrueNorthBuff;

    public override ActionRef? BurstAction => A.KunaisBane;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
        BuildMudraButton();
    }

    /// <summary>
    /// The third button: it walks a mudra sequence and then fires the ninjutsu.
    /// <para>
    /// No mudra state is tracked here, because the game already distinguishes it. Ten, Chi
    /// and Jin each have two action ids - one the game accepts only at the start of a
    /// sequence and one only once a sequence is running - and Ninjutsu itself is only
    /// accepted once enough mudras are charged. So asking the game what it will accept, via
    /// the same Ready() check every other rule uses, resolves the whole sequence with no
    /// counter of ours to drift out of sync when somebody presses a mudra by hand.
    /// </para>
    /// <para>
    /// This covers the two-mudra ninjutsu, which is nearly all of them in a fight: Raiton,
    /// Katon, and both Kassatsu upgrades. The three-mudra ones (Suiton, Huton, Doton) can't
    /// be driven this way, because after two mudras the game would happily fire the
    /// two-mudra spell instead - telling those apart needs a real step counter, which is not
    /// worth guessing at without being able to test it. Keep Suiton on its own keys.
    /// </para>
    /// </summary>
    private void BuildMudraButton()
    {
        var p = AddExtraButton(
            A.Ten1,
            "Mudra",
            "Walks a mudra sequence and fires the ninjutsu. Raiton on a single target, "
            + "Katon on a group, and the Kassatsu upgrades automatically. Suiton, Huton and "
            + "Doton still need their own keys.").Plan;

        // Enough mudras are charged: fire it.
        p.Gcd(A.Ninjutsu).Because("fire the ninjutsu");

        // The game only accepts the first-mudra ids when no sequence is running, so this
        // rule is self-gating and always means "start a new sequence".
        p.OGcd(A.Chi1)
            .When(c => c.Enemies >= 3)
            .Because("Katon");

        p.OGcd(A.Ten1)
            .Because(c => c.Buff(A.KassatsuBuff) ? "Hyosho Ranryu" : "Raiton");

        // Second mudra. Only accepted mid-sequence, so again the game does the gating.
        p.OGcd(A.Ten2)
            .When(c => c.Enemies >= 3)
            .Because("Katon");

        p.OGcd(A.Jin2)
            .When(c => c.Buff(A.KassatsuBuff))
            .Because("Hyosho Ranryu");

        p.OGcd(A.Chi2).Because("Raiton");
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------
        // Kunai's Bane is the raid debuff everything else lines up behind.
        p.OGcd(c => c.Has(A.KunaisBane) ? A.KunaisBane : A.TrickAttack)
            .When(c => !c.Downtime)
            .Because("burst window");

        p.OGcd(A.Dokumori).When(c => !c.Downtime).Because("raid debuff");

        p.OGcd(A.Bunshin)
            .When(c => !c.Downtime && c.Nin.Ninki >= 50)
            .Because("spend Ninki on Bunshin first");

        p.OGcd(A.DreamWithinADream).When(c => !c.Downtime);

        p.OGcd(A.TenriJindo).When(c => c.Buff(A.TenriJindoReady));

        p.OGcd(c => c.Has(A.ZeshoMeppo) ? A.ZeshoMeppo : A.Bhavacakra)
            .When(c => c.Nin.Ninki >= 50 && !c.Ready(A.Bunshin))
            .Because("spend Ninki before it caps");

        p.OGcd(A.Kassatsu).When(c => !c.Downtime);

        p.OGcd(A.Meisui)
            .When(c => c.Buff(A.ShadowWalker) && c.Nin.Ninki <= 50);

        // ---- GCDs --------------------------------------------------------
        // A charged ninjutsu is offered the instant the game will accept it. Ready() asks
        // the game directly, so this needs no mudra tracking of our own.
        p.Gcd(A.Ninjutsu).Because("ninjutsu is charged");

        p.Gcd(A.PhantomKamaitachi).When(c => c.Buff(A.PhantomKamaitachiReady));

        // Raiju Ready stacks expire, so they come before the combo.
        p.Gcd(A.ForkedRaiju).When(c => c.Buff(A.RaijuReady) && !c.InRange);
        p.Gcd(A.FleetingRaiju).When(c => c.Buff(A.RaijuReady));

        // Armor Crush banks Kazematoi, which Aeolian Edge then spends. Keeping it topped up
        // is worth more than the slightly bigger finisher.
        p.Gcd(A.ArmorCrush)
            .When(c => c.ComboIs(A.GustSlash) && c.Nin.Kazematoi <= 3)
            .Needs(PositionalHint.Flank)
            .Because("bank Kazematoi");

        p.Gcd(A.AeolianEdge)
            .When(c => c.ComboIs(A.GustSlash))
            .Needs(PositionalHint.Rear);

        p.Gcd(A.GustSlash).When(c => c.ComboIs(A.SpinningEdge));
        p.Gcd(A.SpinningEdge);

        p.Gcd(A.ThrowingDagger)
            .When(c => !c.InRange)
            .Because("out of range");
    }

    private void BuildAoe()
    {
        var p = Aoe;

        p.OGcd(c => c.Has(A.KunaisBane) ? A.KunaisBane : A.TrickAttack)
            .When(c => !c.Downtime)
            .Because("burst window");

        p.OGcd(A.Dokumori).When(c => !c.Downtime);
        p.OGcd(A.Bunshin).When(c => !c.Downtime && c.Nin.Ninki >= 50);
        p.OGcd(A.TenriJindo).When(c => c.Buff(A.TenriJindoReady));

        p.OGcd(c => c.Has(A.DeathfrogMedium) ? A.DeathfrogMedium : A.HellfrogMedium)
            .When(c => c.Nin.Ninki >= 50 && !c.Ready(A.Bunshin))
            .Because("spend Ninki before it caps");

        p.OGcd(A.Kassatsu).When(c => !c.Downtime);

        p.Gcd(A.Ninjutsu).Because("ninjutsu is charged");
        p.Gcd(A.PhantomKamaitachi).When(c => c.Buff(A.PhantomKamaitachiReady));

        p.Gcd(A.HakkeMujinsatsu).When(c => c.ComboIs(A.DeathBlossom));
        p.Gcd(A.DeathBlossom);
    }
}
