using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Ninja;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Ninja.NinjaActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The mudras live on the two main buttons. They do not roll the global, so they are weaves
/// like any other cooldown - the button asks for Ten, then Chi, and the global it was
/// already pointing at becomes the ninjutsu.
/// </summary>
public sealed class NinjaMainButtonMudraTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<NinjaRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f });

    /// <summary>Room to weave.</summary>
    private static SnapshotBuilder Nin() => new SnapshotBuilder().Job(30).Gcd(1.6f);

    /// <summary>No room to weave, so the suggestion is whatever the next global is.</summary>
    private static SnapshotBuilder Global() => new SnapshotBuilder().Job(30).Gcd(0.1f);

    /// <summary>
    /// Every burst cooldown parked, so the mudra rules are actually reachable. Without this
    /// the fixture never gets past Kunai's Bane and the test passes for the wrong reason -
    /// which is how two earlier jobs shipped rules no snapshot could ever reach.
    /// </summary>
    private static FakeActionState Quiet() => new FakeActionState()
        .OnCooldown(A.SecondWind.Id, 100f)
        .OnCooldown(A.Bloodbath.Id, 100f)
        .OnCooldown(A.KunaisBane.Id, 40f)
        .OnCooldown(A.TrickAttack.Id, 40f)
        .OnCooldown(A.Dokumori.Id, 40f)
        .OnCooldown(A.Bunshin.Id, 40f)
        .OnCooldown(A.TenChiJin.Id, 100f)
        .OnCooldown(A.DreamWithinADream.Id, 40f)
        .OnCooldown(A.Kassatsu.Id, 40f)
        .OnCooldown(A.Meisui.Id, 100f);

    private static FakeActionState Charged(FakeActionState a, ActionRef ninjutsu) =>
        a.Resolving(A.Ninjutsu.Id, ninjutsu.Id);

    // ---- The sequence on the single-target button -------------------------

    [Fact]
    public void TheSingleTargetButtonOpensTheSequenceOnTen()
    {
        var suggestion = Session().Resolve(RotationMode.SingleTarget, Nin().Build(), Quiet());

        Assert.Equal(A.Ten1.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheSecondMudraIsChi()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Nin().Build(), Charged(Quiet(), A.FumaShuriken));

        Assert.Equal(A.Chi2.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The point of putting them on this button: the mudra is the weave and the ninjutsu is
    /// the global that was coming anyway. One press of the key gives both in turn.
    /// </summary>
    [Fact]
    public void WithTheSpellChargedTheNextGlobalIsTheNinjutsu()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Nin().Build(), Charged(Quiet(), A.Raiton));

        Assert.Equal(A.Ninjutsu.Id, suggestion.NextGcd?.Id);
    }

    [Fact]
    public void WithNoWeaveRoomTheChargedNinjutsuIsStillTheAnswer()
    {
        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, Global().Build(), Charged(Quiet(), A.Raiton));

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    // ---- Three mudras across the weave/global split -----------------------

    [Fact]
    public void RaitonGrowsIntoSuitonWhenShadowWalkerIsOwed()
    {
        // Kunai's Bane four globals out and no Shadow Walker to spend on it.
        var actions = Charged(Quiet().OnCooldown(A.KunaisBane.Id, 5f), A.Raiton);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, Nin().Build(), actions);

        Assert.Equal(A.Jin2.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The one that the global/weave split makes possible to get wrong. Jin is a weave and
    /// the ninjutsu is a global, so a closed weave window would fire the Raiton and throw
    /// away the Suiton the burst was waiting on. The global has to wait for the weave.
    /// </summary>
    [Fact]
    public void AClosedWeaveWindowDoesNotFireTheRaitonAndLoseTheSuiton()
    {
        var actions = Charged(Quiet().OnCooldown(A.KunaisBane.Id, 5f), A.Raiton);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, Global().Build(), actions);

        Assert.NotEqual(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    [Fact]
    public void OnceSuitonIsChargedItIsCast()
    {
        var actions = Charged(Quiet().OnCooldown(A.KunaisBane.Id, 5f), A.Suiton);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, Global().Build(), actions);

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    [Fact]
    public void WithShadowWalkerAlreadyUpTheRaitonIsCast()
    {
        var actions = Charged(Quiet().OnCooldown(A.KunaisBane.Id, 5f), A.Raiton);
        var snapshot = Global().Buff(A.ShadowWalker, 15f).Build();

        var suggestion = Session().Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    // ---- The area button --------------------------------------------------

    /// <summary>
    /// Chi first, for Katon. The main buttons know which line they are, so unlike the extra
    /// button they never have to guess the opening mudra from the enemy count.
    /// </summary>
    [Fact]
    public void TheAreaButtonOpensTheSequenceOnChi()
    {
        var suggestion = Session().Resolve(
            RotationMode.Aoe, Nin().Enemies(4).Build(), Quiet());

        Assert.Equal(A.Chi1.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheAreaSecondMudraIsTen()
    {
        var suggestion = Session().Resolve(
            RotationMode.Aoe, Nin().Enemies(4).Build(), Charged(Quiet(), A.FumaShuriken));

        Assert.Equal(A.Ten2.Id, suggestion.Action.Id);
    }

    // ---- Ten Chi Jin ------------------------------------------------------

    /// <summary>
    /// A two minute cooldown that no rule suggested at all until now, though it is in every
    /// published opener chart and every even burst window.
    /// </summary>
    [Fact]
    public void TenChiJinIsSpentInsideTheBurstWindow()
    {
        var actions = Quiet().WithCharges(A.TenChiJin.Id, 1, 1);

        var snapshot = Nin().Debuff(A.KunaisBaneBuff.Id, 15f).Build();
        var suggestion = Session().Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(A.TenChiJin.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TenChiJinIsHeldOutsideTheBurstWindow()
    {
        var actions = Quiet().WithCharges(A.TenChiJin.Id, 1, 1);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, Nin().Build(), actions);

        Assert.NotEqual(A.TenChiJin.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Inside Ten Chi Jin the mudras stop being weaves and become globals - one press, one
    /// ninjutsu. The game refuses the ordinary globals for those six seconds, so if these
    /// rules were missing the button would go quiet for three of them. Each press has its
    /// own id accepted at exactly one point, so the game does the gating; the test spells
    /// that out by making the later ids unusable.
    /// </summary>
    [Fact]
    public void TenChiJinWalksFumaThenRaitonThenSuiton()
    {
        var snapshot = Global().Buff(A.TenChiJinBuff, 6f).Build();

        var first = Session().Resolve(
            RotationMode.SingleTarget,
            snapshot,
            Quiet().Unusable(A.TCJRaiton.Id).Unusable(A.TCJSuiton.Id));
        Assert.Equal(A.FumaTen.Id, first.Action.Id);

        var second = Session().Resolve(
            RotationMode.SingleTarget,
            snapshot,
            Quiet().Unusable(A.FumaTen.Id).Unusable(A.TCJSuiton.Id));
        Assert.Equal(A.TCJRaiton.Id, second.Action.Id);

        var third = Session().Resolve(
            RotationMode.SingleTarget,
            snapshot,
            Quiet().Unusable(A.FumaTen.Id).Unusable(A.TCJRaiton.Id));
        Assert.Equal(A.TCJSuiton.Id, third.Action.Id);
    }

    [Fact]
    public void TenChiJinWalksTheAreaLineOnAGroup()
    {
        var snapshot = Global().Enemies(4).Buff(A.TenChiJinBuff, 6f).Build();

        var first = Session().Resolve(
            RotationMode.Aoe,
            snapshot,
            Quiet().Unusable(A.TCJKaton.Id).Unusable(A.TCJDoton.Id));
        Assert.Equal(A.FumaChi.Id, first.Action.Id);

        var second = Session().Resolve(
            RotationMode.Aoe,
            snapshot,
            Quiet().Unusable(A.FumaChi.Id).Unusable(A.TCJDoton.Id));
        Assert.Equal(A.TCJKaton.Id, second.Action.Id);
    }

    // ---- Ninki pooling ----------------------------------------------------

    private static CombatSnapshot WithNinki(SnapshotBuilder b, byte ninki) =>
        b.Gauge(s => s.Gauges.Ninja.Ninki = ninki).Build();

    /// <summary>
    /// Half a bar with the burst close is held, not spent. The guide asks to pool as high as
    /// possible going into Kunai's Bane rather than dumping at the floor.
    /// </summary>
    [Fact]
    public void NinkiIsPooledWhileTheBurstIsClose()
    {
        var actions = Quiet().OnCooldown(A.KunaisBane.Id, 8f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, WithNinki(Nin(), 60), actions);

        Assert.NotEqual(A.ZeshoMeppo.Id, suggestion.Action.Id);
        Assert.NotEqual(A.Bhavacakra.Id, suggestion.Action.Id);
    }

    [Fact]
    public void NinkiIsDumpedInsideTheBurstWindow()
    {
        var snapshot = WithNinki(Nin().Debuff(A.KunaisBaneBuff.Id, 15f), 60);

        var suggestion = Session().Resolve(RotationMode.SingleTarget, snapshot, Quiet());

        Assert.Equal(A.ZeshoMeppo.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The other half of the guide's advice, and the half that wins when they conflict:
    /// never overcap, "even if we must burn gauge right before Trick Attack".
    /// </summary>
    [Fact]
    public void ANearlyFullBarIsSpentEvenWithTheBurstClose()
    {
        var actions = Quiet().OnCooldown(A.KunaisBane.Id, 8f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, WithNinki(Nin(), 90), actions);

        Assert.Equal(A.ZeshoMeppo.Id, suggestion.Action.Id);
    }

    [Fact]
    public void WithTheBurstFarAwayTheGaugeIsSpentRatherThanHeld()
    {
        var actions = Quiet().OnCooldown(A.KunaisBane.Id, 45f);

        var suggestion = Session().Resolve(
            RotationMode.SingleTarget, WithNinki(Nin(), 60), actions);

        Assert.Equal(A.ZeshoMeppo.Id, suggestion.Action.Id);
    }

    /// <summary>Bunshin gets the gauge first - it sits above the spender and costs the same.</summary>
    [Fact]
    public void BunshinStillTakesTheGaugeBeforeASpender()
    {
        var actions = Quiet().WithCharges(A.Bunshin.Id, 1, 1);

        var snapshot = WithNinki(Nin().Debuff(A.KunaisBaneBuff.Id, 15f), 90);
        var suggestion = Session().Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(A.Bunshin.Id, suggestion.Action.Id);
    }

    // ---- The weave budget -------------------------------------------------

    /// <summary>
    /// A two-mudra ninjutsu is two presses in one window on top of whatever cooldown was
    /// already due, so the job raises the floor the way Viper does.
    /// </summary>
    [Fact]
    public void TheJobAsksForRoomToWeave()
    {
        Assert.Equal(WeaveStyle.Double, JobRotationBase.Create<NinjaRotation>().MinimumWeaveStyle);
    }
}
