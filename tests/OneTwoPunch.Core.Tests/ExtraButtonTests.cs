using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Ninja;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Ninja.NinjaActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Two buttons cover almost every job, but not all. An extra button drives its own
/// sequence, so the global-cooldown rules that govern the main two must not apply to it.
/// </summary>
public sealed class ExtraButtonTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<NinjaRotation>(),
            new RotationSettings { UseOpener = false, SuggestionHoldSeconds = 0f });

    [Fact]
    public void NinjaDeclaresAMudraButton()
    {
        var job = JobRotationBase.Create<NinjaRotation>();

        Assert.Single(job.ExtraButtons);
        Assert.Equal("Mudra", job.ExtraButtons[0].Name);
        Assert.NotEmpty(job.ExtraButtons[0].Purpose);
    }

    /// <summary>
    /// The button reads where the sequence is by asking what Ninjutsu currently resolves to.
    /// The game names the spell the charged mudras would cast, so a test states that spell
    /// rather than a press count - which is the same thing the button sees in a fight.
    /// </summary>
    private static FakeActionState Charged(ActionRef ninjutsu) =>
        new FakeActionState().Resolving(A.Ninjutsu.Id, ninjutsu.Id);

    private static SnapshotBuilder Nin() => new SnapshotBuilder().Job(30).Gcd(0.1f);

    [Fact]
    public void TheMudraButtonFiresTheNinjutsuOnceItIsCharged()
    {
        var session = Session();

        // Suiton is finished - three mudras in, nothing left to add.
        var suggestion = session.Resolve(
            RotationMode.Extra1, Nin().Build(), Charged(A.Suiton));

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheMudraButtonStartsASequenceWhenNoNinjutsuIsCharged()
    {
        var session = Session();
        var suggestion = session.Resolve(
            RotationMode.Extra1, Nin().Build(), new FakeActionState());

        Assert.Equal(A.Ten1.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheMudraButtonTakesTheSecondMudraOnceTheFirstIsSpent()
    {
        var session = Session();

        // One mudra in: the game reports Fuma Shuriken, whichever mudra it was.
        var suggestion = session.Resolve(
            RotationMode.Extra1, Nin().Build(), Charged(A.FumaShuriken));

        Assert.Equal(A.Chi2.Id, suggestion.Action.Id);
    }

    [Fact]
    public void TheMudraButtonTakesTheKassatsuBranch()
    {
        var session = Session();
        var snapshot = Nin().Buff(A.KassatsuBuff, 10f).Build();

        var suggestion = session.Resolve(
            RotationMode.Extra1, snapshot, Charged(A.FumaShuriken));

        // Ten then Jin is Hyosho Ranryu under Kassatsu.
        Assert.Equal(A.Jin2.Id, suggestion.Action.Id);
    }

    // ---- Three mudras ----------------------------------------------------

    /// <summary>
    /// The case the button could not express before. Suiton's first two mudras are Raiton's,
    /// so after them the game will happily fire Raiton - and it used to. Reading the charged
    /// form tells "Raiton, finished" from "Suiton, one mudra short", so Jin goes on instead.
    /// </summary>
    [Fact]
    public void RaitonGrowsIntoSuitonWhenShadowWalkerIsOwed()
    {
        var session = Session();

        // Kunai's Bane is ready and there is no Shadow Walker to spend on it.
        var suggestion = session.Resolve(
            RotationMode.Extra1, Nin().Build(), Charged(A.Raiton));

        Assert.Equal(A.Jin2.Id, suggestion.Action.Id);
    }

    [Fact]
    public void RaitonIsFiredWhenShadowWalkerIsAlreadyUp()
    {
        var session = Session();
        var snapshot = Nin().Buff(A.ShadowWalker, 15f).Build();

        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, Charged(A.Raiton));

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    [Fact]
    public void RaitonIsFiredWhenNothingIsWaitingOnShadowWalker()
    {
        var session = Session();

        // Both consumers a long way off: no reason to spend a mudra charge on Suiton.
        var actions = Charged(A.Raiton)
            .OnCooldown(A.KunaisBane.Id, 45f)
            .OnCooldown(A.Meisui.Id, 90f);

        var suggestion = session.Resolve(RotationMode.Extra1, Nin().Build(), actions);

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Every gate needs an escape. Jin is level 45, and the mudras run on charges - so when
    /// the third press cannot be made, the button must fire the Raiton it already has rather
    /// than stranding a charged sequence with nothing to press.
    /// </summary>
    [Fact]
    public void WithNoJinAvailableTheChargedRaitonIsFiredRatherThanStranded()
    {
        var session = Session();
        var actions = Charged(A.Raiton).Unusable(A.Jin2.Id);

        var suggestion = session.Resolve(RotationMode.Extra1, Nin().Build(), actions);

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    [Fact]
    public void BelowJinTheButtonNeverTriesToBuildSuiton()
    {
        var session = Session();
        var snapshot = Nin().Level(40).Build();

        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, Charged(A.Raiton));

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    // ---- The area line ---------------------------------------------------

    [Fact]
    public void OnAGroupTheSequenceStartsOnChiForKaton()
    {
        var session = Session();
        var snapshot = Nin().Enemies(3).Build();

        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, new FakeActionState());

        Assert.Equal(A.Chi1.Id, suggestion.Action.Id);
    }

    [Fact]
    public void OnAGroupTheSecondMudraIsTen()
    {
        var session = Session();
        var snapshot = Nin().Enemies(3).Build();

        var suggestion = session.Resolve(
            RotationMode.Extra1, snapshot, Charged(A.FumaShuriken));

        Assert.Equal(A.Ten2.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Chi then Ten is Goka Mekkyaku under Kassatsu, not Hyosho Ranryu - the area upgrade
    /// wins on a group. Pinned because the Kassatsu rule sits between the two area rules and
    /// the order is the whole specification.
    /// </summary>
    [Fact]
    public void OnAGroupKassatsuTakesTheGokaBranchRatherThanHyosho()
    {
        var session = Session();
        var snapshot = Nin().Enemies(3).Buff(A.KassatsuBuff, 10f).Build();

        var suggestion = session.Resolve(
            RotationMode.Extra1, snapshot, Charged(A.FumaShuriken));

        Assert.Equal(A.Ten2.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// One mudra in, the game says Fuma Shuriken whichever mudra it was - so a target that
    /// changed its first mudra mid-sequence would press Ten on top of Chi and botch into
    /// Rabbit Medium. The area band is wider once a sequence is running, so losing one enemy
    /// of three between two presses finishes the Katon it started.
    /// </summary>
    [Fact]
    public void ASequenceThatStartedOnAGroupFinishesOnTwoEnemies()
    {
        var session = Session();
        var snapshot = Nin().Enemies(2).Build();

        // Would not have started an area sequence at two, but will finish one.
        Assert.Equal(
            A.Ten1.Id,
            session.Resolve(RotationMode.Extra1, snapshot, new FakeActionState()).Action.Id);

        Assert.Equal(
            A.Ten2.Id,
            session.Resolve(RotationMode.Extra1, snapshot, Charged(A.FumaShuriken)).Action.Id);
    }

    // ---- Sequences that cannot grow --------------------------------------

    /// <summary>
    /// Hyosho Ranryu, Katon and Goka Mekkyaku are finished at two mudras - only Raiton can
    /// still grow. A rule that tried to extend them would botch the sequence.
    /// </summary>
    [Theory]
    [InlineData(16492u)] // Hyosho Ranryu
    [InlineData(2266u)]  // Katon
    [InlineData(16491u)] // Goka Mekkyaku
    [InlineData(2268u)]  // Hyoton
    [InlineData(2270u)]  // Doton
    public void ATwoMudraSpellThatCannotGrowIsFiredAsItStands(uint chargedId)
    {
        var session = Session();
        var actions = new FakeActionState().Resolving(A.Ninjutsu.Id, chargedId);

        var suggestion = session.Resolve(RotationMode.Extra1, Nin().Build(), actions);

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// A botched sequence has to be cleared. Leaving Rabbit Medium charged with no rule for
    /// it would strand the button - and the player - with two mudras spent and no way out.
    /// </summary>
    [Fact]
    public void ABotchedSequenceIsCleared()
    {
        var session = Session();

        var suggestion = session.Resolve(
            RotationMode.Extra1, Nin().Build(), Charged(A.RabbitMedium));

        Assert.Equal(A.Ninjutsu.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Flat resolution: the extra key answers whatever comes next in the sequence without
    /// waiting on a weave window. The mudras turned out to be globals, which is why the main
    /// buttons now drive them and this key is redundant - but it still answers.
    /// </summary>
    [Fact]
    public void TheMudraButtonIgnoresTheWeaveWindow()
    {
        var session = Session();
        var actions = new FakeActionState().Unusable(A.Ninjutsu.Id);

        // No room at all to weave.
        var snapshot = new SnapshotBuilder().Gcd(0.2f).AnimationLock(0.4f).Build();
        var suggestion = session.Resolve(RotationMode.Extra1, snapshot, actions);

        Assert.Equal(A.Ten1.Id, suggestion.Action.Id);
    }

    // ---- The recorder's line ---------------------------------------------

    /// <summary>
    /// Ninja had no gauge line at all, so nothing about Ninki, Kazematoi or Shadow Walker
    /// reached a log - and this change cannot be checked against a real pull without one.
    /// Monk and Viper each cost several pulls to exactly this gap.
    /// </summary>
    [Fact]
    public void TheGaugeLineCarriesBothGaugesAndTheMudraState()
    {
        var job = JobRotationBase.Create<NinjaRotation>();

        var line = job.DescribeGauge(Nin()
            .Gauge(s =>
            {
                s.Gauges.Ninja.Ninki = 70;
                s.Gauges.Ninja.Kazematoi = 3;
            })
            .Buff(A.Mudra, 5f)
            .Buff(A.ShadowWalker, 15f)
            .Build())!;

        Assert.Contains("ninki 70", line);
        Assert.Contains("kazematoi 3", line);
        Assert.Contains("MUDRA", line);
        Assert.Contains("shadow-walker", line);
    }

    [Fact]
    public void TheGaugeLineIsQuietWhenNothingIsUp()
    {
        var job = JobRotationBase.Create<NinjaRotation>();

        var line = job.DescribeGauge(Nin().Build())!;

        Assert.Contains("ninki 0", line);
        Assert.DoesNotContain("MUDRA", line);
        Assert.DoesNotContain("kassatsu", line);
    }

    [Fact]
    public void JobsWithoutExtraButtonsFallBackHarmlessly()
    {
        var job = JobRotationBase.Create<OneTwoPunch.Core.Jobs.Dragoon.DragoonRotation>();
        var session = new RotationSession(job, new RotationSettings { UseOpener = false });

        var suggestion = session.Resolve(
            RotationMode.Extra1, new SnapshotBuilder().Gcd(0.1f).NoCombo().Build(), new FakeActionState());

        // Falls through to the single-target button rather than throwing or going silent.
        Assert.NotNull(suggestion);
        Assert.True(suggestion.Action.Id > 0);
    }
}
