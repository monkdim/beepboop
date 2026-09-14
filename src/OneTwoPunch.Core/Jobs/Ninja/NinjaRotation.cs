using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Ninja.NinjaActions;

namespace OneTwoPunch.Core.Jobs.Ninja;

/// <summary>
/// Ninja, Dawntrail.
/// <para>
/// <b>Mudras get a third key.</b> A ninjutsu is not one action - it is two or three mudra
/// presses and then the cast - and those presses do not roll the global, so they cannot
/// share the two main buttons without silencing them for whole weave windows. The extra
/// button walks the whole sequence, two-mudra and three-mudra alike, and fires the result.
/// See <see cref="BuildMudraButton"/> for how it knows where it is without counting.
/// </para>
/// <para>
/// The two main buttons drive everything else, and still fire a charged ninjutsu the moment
/// the game will accept one - so charging mudras by hand keeps working.
/// </para>
/// <para>
/// Ninja is still the worst fit in the game for a two-key layout, and the mudra key is a
/// real third key rather than a convenience. If that is the part that hurts, this job may
/// simply not be the one to bring.
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
    /// The third button: it walks a mudra sequence and then fires the ninjutsu, for both the
    /// two-mudra and the three-mudra spells.
    /// <para>
    /// <b>No step counter.</b> The game is asked where the sequence is, every frame, by
    /// reading what <c>Ninjutsu</c> currently resolves to. That id <em>names the spell the
    /// charged mudras would cast</em>: Fuma Shuriken after one mudra, Raiton or Katon after
    /// the matching two, Suiton after three. So the button never has to remember what it
    /// pressed, and a player who charges mudras by hand cannot desync it.
    /// </para>
    /// <para>
    /// That read is what makes three-mudra work here. Suiton is Ten - Chi - Jin, whose first
    /// two mudras <em>are</em> Raiton: after them the game will happily fire Raiton, and the
    /// old button did, because it could not tell "Raiton, finished" from "Suiton, one mudra
    /// short". Now it can - the charged form says Raiton outright - so the only question left
    /// is whether to fire it or grow it with Jin.
    /// </para>
    /// <para>
    /// <b>What decides.</b> Once two mudras are in, nothing is guessed: the game has named
    /// the spell, and the only spell that can still grow is Raiton. Everything else is fired
    /// as it stands. The single prediction is the <em>first</em> mudra - Ten for the
    /// single-target line, Chi for the area one - because one mudra in, the game reports Fuma
    /// Shuriken whichever mudra it was. Suiton and Raiton share that prefix, so the Suiton
    /// decision costs nothing and is deferred to where it is free.
    /// </para>
    /// <para>
    /// <b>Huton is deliberately absent.</b> It is Jin - Chi - Ten, and Jin+Chi is not a valid
    /// pair, so the game reports Rabbit Medium halfway through - indistinguishable from a
    /// botched sequence. Driving it would mean guessing which, and the guess costs a wasted
    /// global when it is wrong. Suiton still arms Kunai's Bane at any number of targets, so
    /// nothing is lost but the last 180 potency of a once-a-minute spell. Put Huton on its own
    /// key if you want it.
    /// </para>
    /// </summary>
    private void BuildMudraButton()
    {
        var p = AddExtraButton(
            A.Ten1,
            "Mudra",
            "Walks a mudra sequence and fires the ninjutsu. Raiton on a single target, Katon "
            + "on a group, Suiton when Kunai's Bane or Meisui needs it, and the Kassatsu "
            + "upgrades automatically. Huton and Doton still need their own keys.").Plan;

        // ---- Three mudras in: the sequence is finished either way ---------
        p.Gcd(A.Ninjutsu)
            .When(c => IsThreeMudra(Charged(c)))
            .Because("fire the ninjutsu");

        // ---- Two mudras in: the game has named the spell ------------------
        // Raiton is the one spell that can still grow. Jin turns it into Suiton, which is
        // what arms Kunai's Bane and what Meisui needs. Above the plain Raiton rule so it
        // gets first refusal, and below nothing - if Jin is not available, whether for level
        // or for mudra charges, the rule under it fires Raiton rather than stranding the
        // button on a charged sequence with nothing to press.
        p.OGcd(A.Jin2)
            .When(c => Charged(c) == A.Raiton.Id && NeedsShadowWalker(c))
            .Because("grow it into Suiton");

        // Everything else that can be charged: fire it as it stands.
        p.Gcd(A.Ninjutsu)
            .When(c => IsTwoMudra(Charged(c)))
            .Because("fire the ninjutsu");

        // ---- One mudra in: the second -------------------------------------
        // The game only accepts these ids mid-sequence, so they are self-gating as well as
        // guarded here.
        p.OGcd(A.Ten2)
            .When(c => Charged(c) == A.FumaShuriken.Id && Area(c))
            .Because(c => c.Buff(A.KassatsuBuff) ? "Goka Mekkyaku" : "Katon");

        p.OGcd(A.Jin2)
            .When(c => Charged(c) == A.FumaShuriken.Id && c.Buff(A.KassatsuBuff))
            .Because("Hyosho Ranryu");

        p.OGcd(A.Chi2)
            .When(c => Charged(c) == A.FumaShuriken.Id)
            .Because(c => NeedsShadowWalker(c) ? "Suiton" : "Raiton");

        // ---- Nothing charged: start ---------------------------------------
        // The first-mudra ids are only accepted when no sequence is running, so these mean
        // "start a new one" whether or not the guard above them says so.
        p.OGcd(A.Chi1)
            .When(Area)
            .Because(c => c.Buff(A.KassatsuBuff) ? "Goka Mekkyaku" : "Katon");

        p.OGcd(A.Ten1)
            .Because(c => c.Buff(A.KassatsuBuff) ? "Hyosho Ranryu"
                : NeedsShadowWalker(c) ? "Suiton"
                : "Raiton");
    }

    /// <summary>The spell the charged mudras would cast, straight from the game.</summary>
    private static uint Charged(RotationContext c) => c.CurrentFormOf(A.Ninjutsu);

    /// <summary>A finished three-mudra sequence. Nothing can be added to these.</summary>
    private static bool IsThreeMudra(uint form) =>
        form == A.Suiton.Id || form == A.Huton.Id;

    /// <summary>
    /// Two mudras in, and done. Raiton is deliberately absent: it is the one spell that can
    /// still grow, so it is decided by the rule above this one and only falls through to here
    /// when Jin cannot be pressed.
    /// <para>
    /// Hyoton and Doton are here because a player can charge them by hand, and Rabbit Medium
    /// because a botched sequence has to be cleared rather than leaving the button with no
    /// answer and two mudras spent.
    /// </para>
    /// </summary>
    private static bool IsTwoMudra(uint form) =>
        form == A.Raiton.Id || form == A.Katon.Id
        || form == A.HyoshoRanryu.Id || form == A.GokaMekkyaku.Id
        || form == A.Hyoton.Id || form == A.Doton.Id
        || form == A.RabbitMedium.Id;

    /// <summary>
    /// Whether this sequence is heading for the area ninjutsu - Katon, or Goka Mekkyaku under
    /// Kassatsu.
    /// <para>
    /// The band is wider once a sequence is running, and that is the whole point of it being a
    /// method rather than a comparison. One mudra in, the game reports Fuma Shuriken whichever
    /// mudra it was, so a target that changed its <em>first</em> mudra mid-sequence would
    /// press Ten on top of Chi and botch into Rabbit Medium. Loosening to two means that needs
    /// the pack to fall from three enemies to one between two presses half a second apart,
    /// rather than from three to two.
    /// </para>
    /// </summary>
    private static bool Area(RotationContext c) =>
        Charged(c) == A.Ninjutsu.Id ? c.Enemies >= 3 : c.Enemies >= 2;

    /// <summary>
    /// Whether a Suiton is owed. Kunai's Bane and Meisui both need Shadow Walker and both
    /// consume it, and Suiton is the only thing that grants it - which is why every published
    /// opener starts with one pre-pull and spends a second on Meisui a few globals later.
    /// <para>
    /// The lead is four globals or so: enough to walk three mudras and cast, without holding
    /// Raiton hostage for the whole minute Kunai's Bane spends on cooldown.
    /// </para>
    /// </summary>
    private static bool NeedsShadowWalker(RotationContext c)
    {
        if (c.Buff(A.ShadowWalker))
            return false;

        var trick = c.Has(A.KunaisBane) ? A.KunaisBane : A.TrickAttack;
        return c.ReadyIn(trick, ShadowWalkerLead) || c.ReadyIn(A.Meisui, ShadowWalkerLead);
    }

    private const float ShadowWalkerLead = 10f;

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
