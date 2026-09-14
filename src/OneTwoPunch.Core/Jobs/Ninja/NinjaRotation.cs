using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Model;
using A = OneTwoPunch.Core.Jobs.Ninja.NinjaActions;

namespace OneTwoPunch.Core.Jobs.Ninja;

/// <summary>
/// Ninja, Dawntrail.
/// <para>
/// <b>The two buttons drive the mudras.</b> A ninjutsu is not one action - it is two or
/// three mudra presses and then the cast - but those presses do not roll the global, which
/// makes them weaves like any other. So they sit in the off-global section of both main
/// priority lists: the button asks for Ten, then Chi, and the global it was already
/// pointing at becomes the ninjutsu. Nothing extra to bind.
/// </para>
/// <para>
/// The mudra key still exists as a third button, walking exactly the same rules. It is
/// optional for most people - but it is the answer for one setting in particular. Weaving
/// turned off entirely means the main buttons never offer an off-global, which for this job
/// would mean no ninjutsu at all: a large part of the damage, gone. A raised minimum cannot
/// help there, because turning weaving on is not the engine's decision to make. So somebody
/// who has turned it off keeps the ninjutsu by binding this key, where the sequence resolves
/// flat and never waits for a window.
/// </para>
/// <para>
/// See <see cref="AddMudraContinueRules"/> for how the sequence knows where it is without
/// keeping a step counter, and <see cref="AddTenChiJinRules"/> for the six seconds where
/// the mudras become globals instead.
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

    /// <summary>
    /// Ninja needs the room. A two-mudra ninjutsu is two presses in one window on top of
    /// whatever cooldown was already due, and a mudra's animation lock is around 0.5s
    /// against the 0.65s the engine budgets for an ordinary off-global - so they are
    /// cheaper than the thing the budget was sized for.
    /// <para>
    /// This raises the floor, not the player's setting: someone who has turned weaving down
    /// still gets one press per window, which is slower but never wrong. A sequence left
    /// half-charged survives six seconds, so it simply finishes in the next window.
    /// </para>
    /// </summary>
    public override WeaveStyle MinimumWeaveStyle => WeaveStyle.Double;

    protected override void Build()
    {
        BuildSingleTarget();
        BuildAoe();
        BuildMudraButton();
    }

    private void BuildSingleTarget()
    {
        var p = SingleTarget;

        // ---- Off-globals -------------------------------------------------

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

        // A half-charged sequence expires in six seconds. Everything below this is on a
        // sixty second clock or longer, so finishing what is started comes first.
        AddMudraContinueRules(p);

        // Kunai's Bane is the raid debuff everything else lines up behind.
        p.OGcd(c => c.Has(A.KunaisBane) ? A.KunaisBane : A.TrickAttack)
            .When(c => !c.Downtime)
            .Because("burst window");

        p.OGcd(A.Dokumori).When(c => !c.Downtime).Because("raid debuff");

        p.OGcd(A.Bunshin)
            .When(c => !c.Downtime && c.Nin.Ninki >= 50)
            .Because("spend Ninki on Bunshin first");

        // Three free ninjutsu, and the guide puts all three inside the window.
        p.OGcd(A.TenChiJin)
            .When(c => !c.Downtime && InBurst(c))
            .Because("three free ninjutsu inside the window");

        p.OGcd(A.DreamWithinADream).When(c => !c.Downtime);

        p.OGcd(A.TenriJindo).When(c => c.Buff(A.TenriJindoReady));

        p.OGcd(A.Kassatsu).When(c => !c.Downtime).Because("free upgraded ninjutsu");

        p.OGcd(A.Meisui)
            .When(c => c.Buff(A.ShadowWalker) && c.Nin.Ninki <= 50);

        // A bar about to overflow outranks even a mudra. The guide is explicit that this is
        // the bigger loss: overcapped Ninki is "potential oGCD loss, which is a far larger
        // loss than the gain of getting more Bhavacakras under Trick".
        p.OGcd(SingleTargetSpender)
            .When(AboutToOvercapNinki)
            .Because("Ninki is about to cap");

        // Otherwise the mudras go first. A Raiton is 740 potency of global against a
        // spender's 550 to 700 of weave, and the guide's own burst list reads the same way:
        // the ninjutsu are named individually and the spenders are "as many as we have
        // available" - the thing that fills what is left of the window.
        AddMudraStartRules(p, aoe: false);

        p.OGcd(SingleTargetSpender).When(WantsToSpendNinki).Because("spend Ninki");

        // ---- Globals -------------------------------------------------------

        AddTenChiJinRules(p, aoe: false);
        AddNinjutsuFireRule(p);

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

        p.OGcd(A.SecondWind).When(c => c.Hurt).Because("you are hurt");
        p.OGcd(A.Bloodbath).When(c => c.Hurt).Because("you are hurt and Second Wind is down");

        AddMudraContinueRules(p);

        p.OGcd(c => c.Has(A.KunaisBane) ? A.KunaisBane : A.TrickAttack)
            .When(c => !c.Downtime)
            .Because("burst window");

        p.OGcd(A.Dokumori).When(c => !c.Downtime);
        p.OGcd(A.Bunshin).When(c => !c.Downtime && c.Nin.Ninki >= 50);

        p.OGcd(A.TenChiJin)
            .When(c => !c.Downtime && InBurst(c))
            .Because("three free ninjutsu inside the window");

        p.OGcd(A.TenriJindo).When(c => c.Buff(A.TenriJindoReady));

        p.OGcd(A.Kassatsu).When(c => !c.Downtime).Because("free Goka Mekkyaku");

        p.OGcd(A.Meisui)
            .When(c => c.Buff(A.ShadowWalker) && c.Nin.Ninki <= 50);

        p.OGcd(AreaSpender).When(AboutToOvercapNinki).Because("Ninki is about to cap");

        AddMudraStartRules(p, aoe: true);

        p.OGcd(AreaSpender).When(WantsToSpendNinki).Because("spend Ninki");

        AddTenChiJinRules(p, aoe: true);
        AddNinjutsuFireRule(p);

        p.Gcd(A.PhantomKamaitachi).When(c => c.Buff(A.PhantomKamaitachiReady));

        p.Gcd(A.HakkeMujinsatsu).When(c => c.ComboIs(A.DeathBlossom));
        p.Gcd(A.DeathBlossom);
    }

    /// <summary>
    /// The optional third button. It walks the same rules as the two main ones, flat - first
    /// match wins, with no global-cooldown or weave gating - for anyone who would rather
    /// drive the sequence on its own key than have it share the weave budget.
    /// </summary>
    private void BuildMudraButton()
    {
        var p = AddExtraButton(
            A.Ten1,
            "Mudra",
            "Optional for most people - the two main buttons already walk the mudras. Bind "
            + "it if you have weaving turned off, or if you would rather drive the sequence "
            + "yourself: it never waits for a weave window.").Plan;

        AddNinjutsuFireRule(p);
        AddMudraContinueRules(p);
        AddMudraStartRules(p, aoe: null);
    }

    // ---- The mudra sequence ----------------------------------------------

    /// <summary>
    /// Fires the charged ninjutsu.
    /// <para>
    /// <b>No step counter.</b> The game is asked where the sequence is, every frame, by
    /// reading what <c>Ninjutsu</c> currently resolves to. That id <em>names the spell the
    /// charged mudras would cast</em>: Fuma Shuriken after one mudra, Raiton or Katon after
    /// the matching two, Suiton after three. So nothing here has to remember what it
    /// pressed, and a player who charges mudras by hand cannot desync it.
    /// </para>
    /// <para>
    /// That read is what makes three-mudra work. Suiton is Ten - Chi - Jin, whose first two
    /// mudras <em>are</em> Raiton: after them the game will happily fire Raiton, and without
    /// this read there is no telling "Raiton, finished" from "Suiton, one mudra short".
    /// </para>
    /// </summary>
    private static void AddNinjutsuFireRule(RotationPlan p)
    {
        p.Gcd(A.Ninjutsu)
            .When(c => ChargedIsCastable(c) && !WantsToGrow(c))
            .Because(c => NinjutsuName(Charged(c)));
    }

    /// <summary>
    /// Presses the next mudra of a sequence that is already running.
    /// <para>
    /// These sit high in both main lists because a half-charged sequence expires in six
    /// seconds while everything else on the button is on a sixty second clock. The game only
    /// accepts the mid-sequence mudra ids once a sequence is running, so they are
    /// self-gating as well as guarded here.
    /// </para>
    /// </summary>
    private static void AddMudraContinueRules(RotationPlan p)
    {
        // Raiton is the one spell that can still grow: Jin turns it into Suiton, which is
        // what arms Kunai's Bane and what Meisui needs. WantsToGrow tests Jin's readiness
        // itself, so when it cannot be pressed - level 45, or mudra charges spent - the fire
        // rule takes the Raiton instead of stranding a charged sequence.
        p.OGcd(A.Jin2).When(WantsToGrow).Because("grow it into Suiton");

        p.OGcd(A.Ten2)
            .When(c => Charged(c) == A.FumaShuriken.Id && Area(c))
            .Because(c => c.Buff(A.KassatsuBuff) ? "Goka Mekkyaku" : "Katon");

        p.OGcd(A.Jin2)
            .When(c => Charged(c) == A.FumaShuriken.Id && c.Buff(A.KassatsuBuff))
            .Because("Hyosho Ranryu");

        p.OGcd(A.Chi2)
            .When(c => Charged(c) == A.FumaShuriken.Id)
            .Because(c => NeedsShadowWalker(c) ? "Suiton" : "Raiton");
    }

    /// <summary>
    /// Opens a new sequence. Only the opening mudra costs a charge - the mid-sequence ids
    /// recast in half a second - so <c>Ready</c> on these is the whole question of whether a
    /// ninjutsu is affordable. Two charges at twenty seconds is the three natural ninjutsu a
    /// minute the guide expects, so spending as they come is the right rate.
    /// </summary>
    /// <param name="aoe">
    /// <c>true</c> for the area button, <c>false</c> for single target, <c>null</c> to read
    /// the enemy count. The main buttons know their own line, so they do not guess; only the
    /// extra button, which has no mode of its own, falls back to counting.
    /// </param>
    private static void AddMudraStartRules(RotationPlan p, bool? aoe)
    {
        if (aoe is not false)
        {
            p.OGcd(A.Chi1)
                .When(c => aoe == true || Area(c))
                .Because(c => c.Buff(A.KassatsuBuff) ? "Goka Mekkyaku" : "Katon");
        }

        if (aoe is not true)
        {
            p.OGcd(A.Ten1)
                .Because(c => c.Buff(A.KassatsuBuff) ? "Hyosho Ranryu"
                    : NeedsShadowWalker(c) ? "Suiton"
                    : "Raiton");
        }
    }

    /// <summary>
    /// The six seconds inside Ten Chi Jin, where the mudras stop being weaves and become
    /// globals: one press, one ninjutsu, no charge spent. The game refuses the ordinary
    /// globals for the duration, so these have to be the first thing the list offers or the
    /// button goes quiet for three globals.
    /// <para>
    /// Each press has its own action id and the game accepts each at exactly one point in
    /// the sequence, so - as with the ordinary mudras - the priority order is the whole
    /// state machine and <c>Ready</c> does the gating. Single target walks Fuma Shuriken
    /// into Raiton into Suiton, which is the order every published opener chart shows. The
    /// area line is Fuma Shuriken into Katon into Doton.
    /// </para>
    /// </summary>
    private static void AddTenChiJinRules(RotationPlan p, bool aoe)
    {
        if (aoe)
        {
            p.Gcd(A.TCJDoton).When(InTenChiJin).Because("Ten Chi Jin: Doton");
            p.Gcd(A.TCJKaton).When(InTenChiJin).Because("Ten Chi Jin: Katon");
            p.Gcd(A.FumaChi).When(InTenChiJin).Because("Ten Chi Jin: Fuma Shuriken");
        }
        else
        {
            p.Gcd(A.TCJSuiton).When(InTenChiJin).Because("Ten Chi Jin: Suiton");
            p.Gcd(A.TCJRaiton).When(InTenChiJin).Because("Ten Chi Jin: Raiton");
            p.Gcd(A.FumaTen).When(InTenChiJin).Because("Ten Chi Jin: Fuma Shuriken");
        }
    }

    private static bool InTenChiJin(RotationContext c) => c.Buff(A.TenChiJinBuff);

    /// <summary>The spell the charged mudras would cast, straight from the game.</summary>
    private static uint Charged(RotationContext c) => c.CurrentFormOf(A.Ninjutsu);

    /// <summary>A finished three-mudra sequence. Nothing can be added to these.</summary>
    private static bool IsThreeMudra(uint form) =>
        form == A.Suiton.Id || form == A.Huton.Id;

    /// <summary>
    /// Two mudras in. Rabbit Medium is here because a botched sequence has to be cleared
    /// rather than leaving the button with no answer and two mudras spent, and Hyoton and
    /// Doton because a player can charge them by hand.
    /// </summary>
    private static bool IsTwoMudra(uint form) =>
        form == A.Raiton.Id || form == A.Katon.Id
        || form == A.HyoshoRanryu.Id || form == A.GokaMekkyaku.Id
        || form == A.Hyoton.Id || form == A.Doton.Id
        || form == A.RabbitMedium.Id;

    private static bool ChargedIsCastable(RotationContext c) =>
        IsThreeMudra(Charged(c)) || IsTwoMudra(Charged(c));

    /// <summary>
    /// Whether the charged Raiton should become Suiton instead of being cast.
    /// <para>
    /// Tested by the fire rule as well as the Jin rule, and that is the point: on the main
    /// buttons the two live in different halves of the list - Jin is a weave, the ninjutsu
    /// is a global - so a closed weave window would otherwise fire the Raiton and lose the
    /// Suiton the burst was waiting on. Asking the same question in both places means the
    /// global waits for the weave rather than racing it.
    /// </para>
    /// </summary>
    private static bool WantsToGrow(RotationContext c) =>
        Charged(c) == A.Raiton.Id && NeedsShadowWalker(c) && c.Ready(A.Jin2);

    /// <summary>
    /// Whether this sequence is heading for the area ninjutsu. Only the extra button asks -
    /// the main buttons know their own line - and the band is wider once a sequence is
    /// running: one mudra in, the game reports Fuma Shuriken whichever mudra it was, so a
    /// target that changed its <em>first</em> mudra mid-sequence would press Ten on top of
    /// Chi and botch into Rabbit Medium.
    /// </summary>
    private static bool Area(RotationContext c) =>
        Charged(c) == A.Ninjutsu.Id ? c.Enemies >= 3 : c.Enemies >= 2;

    /// <summary>
    /// Whether a Suiton is owed. Kunai's Bane and Meisui both need Shadow Walker and both
    /// consume it, and Suiton is the only thing that grants it - which is why every published
    /// opener starts with one pre-pull and spends a second on Meisui a few globals later.
    /// </summary>
    private static bool NeedsShadowWalker(RotationContext c)
    {
        if (c.Buff(A.ShadowWalker))
            return false;

        var trick = c.Has(A.KunaisBane) ? A.KunaisBane : A.TrickAttack;
        return c.ReadyIn(trick, ShadowWalkerLead) || c.ReadyIn(A.Meisui, ShadowWalkerLead);
    }

    /// <summary>
    /// True while the job's own damage-up debuff is on the target. A debuff, not a buff:
    /// Kunai's Bane marks the enemy, and reading it off the player would be permanently
    /// false and silently disable everything that hangs on the burst window.
    /// </summary>
    private static bool InBurst(RotationContext c) =>
        c.Debuff(c.Has(A.KunaisBane) ? A.KunaisBaneBuff : A.TrickAttackBuff);

    /// <summary>
    /// Ninki is pooled into the burst rather than spent at the floor.
    /// <para>
    /// The guide asks for both halves of this and they pull against each other: pool as high
    /// as possible going into Kunai's Bane, and never overcap, "even if we must burn gauge
    /// right before Trick Attack - overcapped Ninki is potential oGCD loss, which is a far
    /// larger loss than the gain of getting more Bhavacakras under Trick". So: dump inside
    /// the window, dump when the bar is nearly full whatever the window is doing, and
    /// otherwise only hold while there is a burst close enough to be worth holding for.
    /// </para>
    /// </summary>
    private static bool WantsToSpendNinki(RotationContext c)
    {
        if (!CanSpendNinki(c))
            return false;

        if (InBurst(c) || c.Nin.Ninki >= NinkiCeiling)
            return true;

        var trick = c.Has(A.KunaisBane) ? A.KunaisBane : A.TrickAttack;
        return !c.ReadyIn(trick, NinkiPoolLead);
    }

    /// <summary>
    /// The half of the rule that outranks a mudra: the bar is nearly full and the next
    /// global would waste part of the gain.
    /// </summary>
    private static bool AboutToOvercapNinki(RotationContext c) =>
        CanSpendNinki(c) && c.Nin.Ninki >= NinkiCeiling;

    /// <summary>
    /// Bunshin gets the gauge first. It costs the same fifty and sits above both spender
    /// rules, so this only ever spends what Bunshin is not waiting on.
    /// </summary>
    private static bool CanSpendNinki(RotationContext c) =>
        c.Nin.Ninki >= 50 && !c.Ready(A.Bunshin);

    private static ActionRef SingleTargetSpender(RotationContext c) =>
        c.Has(A.ZeshoMeppo) ? A.ZeshoMeppo : A.Bhavacakra;

    private static ActionRef AreaSpender(RotationContext c) =>
        c.Has(A.DeathfrogMedium) ? A.DeathfrogMedium : A.HellfrogMedium;

    /// <summary>A readable name for a charged ninjutsu, for the log and the button's note.</summary>
    private static string NinjutsuName(uint form) =>
        form == A.Raiton.Id ? "Raiton"
        : form == A.Katon.Id ? "Katon"
        : form == A.Suiton.Id ? "Suiton"
        : form == A.Huton.Id ? "Huton"
        : form == A.HyoshoRanryu.Id ? "Hyosho Ranryu"
        : form == A.GokaMekkyaku.Id ? "Goka Mekkyaku"
        : form == A.RabbitMedium.Id ? "clear the botched sequence"
        : "fire the ninjutsu";

    /// <summary>
    /// How far ahead of Kunai's Bane or Meisui the list starts building a Suiton. Four
    /// globals: enough to walk three mudras and cast, without holding Raiton hostage for the
    /// whole minute Kunai's Bane spends on cooldown.
    /// </summary>
    private const float ShadowWalkerLead = 10f;

    /// <summary>
    /// Ninki at which the bar is spent regardless of the burst. Each global is worth five,
    /// and fifteen with Bunshin out, so this is roughly one global of headroom.
    /// </summary>
    private const int NinkiCeiling = 85;

    /// <summary>
    /// How close the burst has to be to be worth pooling for. Fifty Ninki takes about ten
    /// globals to earn back, so holding longer than this risks the overcap the guide calls
    /// the larger loss.
    /// </summary>
    private const float NinkiPoolLead = 20f;

    /// <summary>
    /// The recorder's line for Ninja. Both gauges drive rules - Ninki gates every spender and
    /// Kazematoi decides Armor Crush against Aeolian Edge - and neither reached a log before,
    /// which is the failure that cost several pulls each on Monk and Viper.
    /// <para>
    /// Shadow Walker is here because it is what the Suiton decision turns on, and the Mudra
    /// status because it says a sequence is running. Which spell is charged is not: that
    /// comes from asking the game what Ninjutsu resolves to, and this method is handed a
    /// snapshot rather than the action state. The suggestion's own reason line carries it
    /// instead - "Raiton", "Suiton", "grow it into Suiton" - so a log still shows the
    /// sequence being walked one press at a time.
    /// </para>
    /// </summary>
    public override string DescribeGauge(CombatSnapshot snapshot)
    {
        var g = snapshot.Gauges.Ninja;

        var mudra = Holding(snapshot, A.Mudra) ? " | MUDRA" : string.Empty;
        var tcj = Holding(snapshot, A.TenChiJinBuff) ? " | TCJ" : string.Empty;
        var kassatsu = Holding(snapshot, A.KassatsuBuff) ? " | kassatsu" : string.Empty;
        var shadow = Holding(snapshot, A.ShadowWalker) ? " | shadow-walker" : string.Empty;
        var raiju = Holding(snapshot, A.RaijuReady) ? " | raiju" : string.Empty;
        var phantom = Holding(snapshot, A.PhantomKamaitachiReady) ? " | phantom" : string.Empty;
        var bunshin = Holding(snapshot, A.BunshinBuff) ? " | bunshin" : string.Empty;
        var tenri = Holding(snapshot, A.TenriJindoReady) ? " | tenri" : string.Empty;
        var meisui = Holding(snapshot, A.MeisuiBuff) ? " | meisui" : string.Empty;

        return $"ninki {g.Ninki} | kazematoi {g.Kazematoi}"
            + $"{mudra}{tcj}{kassatsu}{shadow}{raiju}{phantom}{bunshin}{tenri}{meisui}";
    }

    private static bool Holding(CombatSnapshot snapshot, StatusRef status)
    {
        for (var i = 0; i < snapshot.SelfStatuses.Count; i++)
        {
            if (snapshot.SelfStatuses[i].Id == status.Id)
                return true;
        }

        return false;
    }
}
