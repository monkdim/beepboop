using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Healers;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

public sealed class HealerSummonerRotationTests
{
    private static RotationSession Session(uint job, WeaveStyle style = WeaveStyle.None) =>
        new(JobRegistry.Create(job)!, new RotationSettings { WeaveStyle = style, UseOpener = false, SuggestionHoldSeconds = 0 });

    [Theory]
    [InlineData(24, 1871, 25859, 25860)]
    [InlineData(28, 1895, 25865, 25866)]
    [InlineData(33, 1881, 25871, 25872)]
    [InlineData(40, 2616, 24312, 24315)]
    public void HealersUseDifferentSingleTargetAndAreaFillers(uint job, uint dot, uint single, uint area)
    {
        var actions = new FakeActionState().OnCooldown(24313, 30);
        var snapshot = new SnapshotBuilder().Job(job).Enemies(4).Debuff(dot, 15).Build();
        var session = Session(job);
        Assert.Equal(single, session.Resolve(RotationMode.SingleTarget, snapshot, actions).Action.Id);
        Assert.Equal(area, session.Resolve(RotationMode.Aoe, snapshot, actions).Action.Id);
    }

    [Theory]
    [InlineData(24, 16532)]
    [InlineData(28, 16540)]
    [InlineData(33, 16554)]
    public void SingleTargetRefreshesTheLearnedDamageOverTimeSpell(uint job, uint expected) =>
        Assert.Equal(expected, Session(job).Resolve(RotationMode.SingleTarget,
            new SnapshotBuilder().Job(job).Build(), new FakeActionState()).Action.Id);

    [Fact]
    public void WhiteMageUsesMiseryOnlyWithAFullBloodLily()
    {
        var session = Session(24);
        var state = new SnapshotBuilder().Job(24).Debuff(1871, 20).Build();
        var actions = new FakeActionState();
        state.Gauges.WhiteMageBloodLily = 2;
        Assert.Equal(25859u, session.Resolve(RotationMode.SingleTarget, state, actions).Action.Id);
        state.Gauges.WhiteMageBloodLily = 3;
        Assert.Equal(16535u, session.Resolve(RotationMode.SingleTarget, state, actions).Action.Id);
    }

    [Theory]
    [InlineData(RotationMode.SingleTarget, 24314)]
    [InlineData(RotationMode.Aoe, 37032)]
    public void SageUsesEukrasiaThenTheRightDot(RotationMode mode, uint expected)
    {
        var session = Session(40);
        var snapshot = new SnapshotBuilder().Job(40).Enemies(3).Gauge(s => s.TargetDistance = 4);
        var actions = new FakeActionState();
        Assert.Equal(24290u, session.Resolve(mode, snapshot.Build(), actions).Action.Id);
        session.NotifyActionUsed(24290);
        Assert.Equal(expected, session.Resolve(mode, snapshot.Buff(2606).Build(), actions).Action.Id);
    }

    [Theory]
    [InlineData(RotationMode.SingleTarget)]
    [InlineData(RotationMode.Aoe)]
    public void AstrologianPlaysHeldCardsBeforeDrawingAgain(RotationMode mode)
    {
        var session = Session(33, WeaveStyle.Double);
        var state = new SnapshotBuilder().Job(33).Gcd(1.9f).Build();
        var actions = new FakeActionState().OnCooldown(16552, 90)
            .Resolving(37017, 37018).Resolving(37019, 37026).Resolving(37020, 37027);
        Assert.Equal(37026u, session.Resolve(mode, state, actions).Action.Id);
        session.NotifyActionUsed(37026);
        actions.Resolving(37019, 37019);
        Assert.Equal(37027u, session.Resolve(mode, state, actions).Action.Id);
        session.NotifyActionUsed(37027);
        actions.Resolving(37020, 37020);
        Assert.Equal(ActionKind.Gcd, session.Resolve(mode, state, actions).Kind);
        state.GcdRemaining = 2.4f;
        Assert.Equal(37018u, session.Resolve(mode, state, actions).Action.Id);
    }

    [Theory]
    [InlineData(37023u, 2, 1, 3)]
    [InlineData(37026u, 3, 4, 2)]
    public void DamageCardsPreferTheCorrectJobAndHaveAFallback(uint card, byte preferred, byte backup, byte other)
    {
        Assert.True(AstrologianCardTarget.Priority(card, preferred, false)
            < AstrologianCardTarget.Priority(card, backup, false));
        Assert.True(AstrologianCardTarget.Priority(card, backup, false)
            < AstrologianCardTarget.Priority(card, other, false));
    }

    [Theory]
    [InlineData(37024u)]
    [InlineData(37025u)]
    [InlineData(37027u)]
    public void ProtectiveCardsPreferATank(uint card) =>
        Assert.True(AstrologianCardTarget.Priority(card, 1, false)
            < AstrologianCardTarget.Priority(card, 2, false));

    [Fact]
    public void BalanceUsesATankBackupWhenTheMeleeCannotReceiveTheCard()
    {
        CardRecipient[] party = [new(10, 2, false, 1, false), new(20, 1, false, .8f),
            new(30, 3, false, .6f), new(40, 4, true, 1)];
        Assert.Equal(20ul, AstrologianCardTarget.Choose(37023, party));
        Assert.Equal(40ul, AstrologianCardTarget.Choose(37023, party.AsSpan(3)));
        Assert.Equal(0ul, AstrologianCardTarget.Choose(37023, party.AsSpan(0, 1)));
    }

    [Fact]
    public void EwerPrefersTheLowestHealthEligiblePlayer()
    {
        CardRecipient[] party = [new(10, 1, false, .2f, false), new(20, 2, false, .45f),
            new(30, 4, true, .8f)];
        Assert.Equal(20ul, AstrologianCardTarget.Choose(37028, party));
    }

    [Theory]
    [InlineData(RotationMode.SingleTarget, 25824u, 36994u)]
    [InlineData(RotationMode.Aoe, 25833u, 36995u)]
    public void SummonerUsesTheCurrentElementAndDemiForms(RotationMode mode, uint elemental, uint demi)
    {
        var session = Session(27);
        var actions = new FakeActionState().Resolving(25883, 25824).Resolving(25884, 25833)
            .Resolving(163, 36994).Resolving(16511, 36995);
        var snapshot = new SnapshotBuilder().Job(27).Gauge(s =>
        {
            s.Gauges.Summoner.HasPet = true;
            s.Gauges.Summoner.Attunement = 3;
        }).Build();
        Assert.Equal(elemental, session.Resolve(mode, snapshot, actions).Action.Id);
        snapshot.Gauges.Summoner.Attunement = 0;
        snapshot.Gauges.Summoner.SummonTimeRemaining = 12;
        Assert.Equal(demi, session.Resolve(mode, snapshot, actions).Action.Id);
    }

    [Theory]
    [InlineData(RotationMode.SingleTarget)]
    [InlineData(RotationMode.Aoe)]
    public void IfritFollowUpsAreGlobalsOnBothButtons(RotationMode mode)
    {
        var session = Session(27);
        var actions = new FakeActionState();
        var snapshot = new SnapshotBuilder().Job(27).Buff(2724)
            .Gauge(s => s.Gauges.Summoner.HasPet = true).Build();
        var cyclone = session.Resolve(mode, snapshot, actions);
        Assert.Equal(25835u, cyclone.Action.Id);
        Assert.Equal(ActionKind.Gcd, cyclone.Kind);
        snapshot.SelfStatuses = [new StatusEntry(4403, 10, 0)];
        var strike = session.Resolve(mode, snapshot, actions);
        Assert.Equal(25885u, strike.Action.Id);
        Assert.Equal(ActionKind.Gcd, strike.Kind);
    }

    [Fact]
    public void LevelSyncedSummonerCanRestartItsLoop()
    {
        var session = Session(27);
        var snapshot = new SnapshotBuilder().Job(27).Level(20).Build();
        var actions = new FakeActionState();
        Assert.Equal(25798u, session.Resolve(RotationMode.SingleTarget, snapshot, actions).Action.Id);
        snapshot.Gauges.Summoner.HasPet = true;
        Assert.Equal(25800u, session.Resolve(RotationMode.SingleTarget, snapshot, actions).Action.Id);
        actions.OnCooldown(25800, 30);
        snapshot.Gauges.Summoner.IfritReady = true;
        Assert.Equal(25802u, session.Resolve(RotationMode.SingleTarget, snapshot, actions).Action.Id);
    }

    [Theory]
    [InlineData(RotationMode.SingleTarget, 36994u, 25824u, 25825u, 25823u, 3579u)]
    [InlineData(RotationMode.Aoe, 36995u, 25833u, 25834u, 25832u, 25826u)]
    public void SummonerWalksADemiAndAllThreePrimalsBeforeFiller(
        RotationMode mode, uint demi, uint earth, uint wind, uint fire, uint filler)
    {
        var session = Session(27);
        var actions = new FakeActionState().Resolving(25800, 36992);
        var state = new SnapshotBuilder().Job(27).Build();
        ref var gauge = ref state.Gauges.Summoner;
        gauge.HasPet = true;
        var host = mode == RotationMode.Aoe ? 16511u : 163u;
        var elemental = mode == RotationMode.Aoe ? 25884u : 25883u;

        void Press(uint expected)
        {
            Assert.Equal(expected, session.Resolve(mode, state, actions).Action.Id);
            session.NotifyActionUsed(expected);
        }

        Press(36992);
        actions.OnCooldown(36992, 45).Resolving(host, demi);
        gauge.SummonTimeRemaining = 12;
        Press(demi);
        gauge.SummonTimeRemaining = 0;
        gauge.TitanReady = gauge.GarudaReady = gauge.IfritReady = true;
        actions.Resolving(25803, 25839).Resolving(25804, 25840).Resolving(25802, 25838);
        Press(25839);
        gauge.TitanReady = false;
        gauge.Attunement = 1;
        PressElement(earth);
        Press(25840);
        gauge.GarudaReady = false;
        gauge.Attunement = 1;
        PressElement(wind);
        Press(25838);
        gauge.IfritReady = false;
        gauge.Attunement = 1;
        PressElement(fire);
        actions.Resolving(host, filler);
        Press(filler);
        actions.OnCooldown(36992, 0);
        Press(36992);

        void PressElement(uint form)
        {
            actions.Resolving(elemental, form);
            Press(form);
            state.Gauges.Summoner.Attunement = 0;
        }
    }
}
