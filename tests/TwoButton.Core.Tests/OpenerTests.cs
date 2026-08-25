using TwoButton.Core.Engine;
using TwoButton.Core.Jobs;
using TwoButton.Core.Jobs.Dragoon;
using TwoButton.Core.Model;
using Xunit;
using A = TwoButton.Core.Jobs.Dragoon.DragoonActions;

namespace TwoButton.Core.Tests;

/// <summary>
/// The opener is the one place the engine overrides the priority list, so it has to give
/// that control back the instant the player does something else.
/// </summary>
public sealed class OpenerTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<DragoonRotation>(), new RotationSettings());

    private static SnapshotBuilder AtPull() =>
        new SnapshotBuilder().Gcd(0.1f).NoCombo().Gauge(s => s.CombatDuration = 0.5f);

    [Fact]
    public void TheOpenerStartsAtTheTopOfTheList()
    {
        var session = Session();

        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull().Build(), new FakeActionState());

        Assert.Equal(A.TrueThrust.Id, suggestion.Action.Id);
        Assert.Equal(0, session.OpenerStep);
    }

    [Fact]
    public void TheOpenerAdvancesWhenTheStepIsActuallyUsed()
    {
        var session = Session();
        var actions = new FakeActionState();

        session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);
        session.NotifyActionUsed(A.TrueThrust.Id);

        var next = session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);

        Assert.Equal(A.SpiralBlow.Id, next.Action.Id);
        Assert.Equal(1, session.OpenerStep);
    }

    [Fact]
    public void TheOpenerIsAbandonedWhenThePlayerDoesSomethingElse()
    {
        var session = Session();
        var actions = new FakeActionState();

        session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);
        session.NotifyActionUsed(A.PiercingTalon.Id);

        Assert.False(session.OpenerActive);

        // And the priority list takes over cleanly rather than jamming.
        var next = session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);
        Assert.Equal(A.TrueThrust.Id, next.Action.Id);
    }

    [Fact]
    public void TheOpenerDoesNotStartMidFight()
    {
        var session = Session();
        var snapshot = new SnapshotBuilder().Gcd(0.1f).NoCombo().Build(); // 60s into combat

        session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.False(session.OpenerActive);
    }

    [Fact]
    public void TheOpenerWaitsRatherThanClippingForAnOffGlobalStep()
    {
        var session = Session();
        var actions = new FakeActionState();

        // Walk to the Lance Charge step, which is an off-global.
        session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);
        session.NotifyActionUsed(A.TrueThrust.Id);
        session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);
        session.NotifyActionUsed(A.SpiralBlow.Id);

        // No room to weave: the button must not become the off-global, and the opener must
        // survive rather than being torn down.
        var tight = new SnapshotBuilder()
            .Gcd(0.2f)
            .Combo(A.SpiralBlow)
            .Gauge(s => s.CombatDuration = 3f)
            .Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, tight, actions);

        Assert.NotEqual(A.LanceCharge.Id, suggestion.Action.Id);
        Assert.True(session.OpenerActive);
    }

    [Fact]
    public void TheOpenerIsSkippedWhenTurnedOff()
    {
        var settings = new RotationSettings { UseOpener = false };
        var session = new RotationSession(JobRotationBase.Create<DragoonRotation>(), settings);

        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull().Build(), new FakeActionState());

        Assert.False(session.OpenerActive);
        Assert.Equal(A.TrueThrust.Id, suggestion.Action.Id);
    }

    [Fact]
    public void LeavingCombatRearmsTheOpenerForTheNextPull()
    {
        var session = Session();
        var actions = new FakeActionState();

        session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);
        session.NotifyActionUsed(A.TrueThrust.Id);
        Assert.Equal(1, session.OpenerStep);

        session.Resolve(RotationMode.SingleTarget, new SnapshotBuilder().OutOfCombat().Build(), actions);
        var fresh = session.Resolve(RotationMode.SingleTarget, AtPull().Build(), actions);

        Assert.Equal(0, session.OpenerStep);
        Assert.Equal(A.TrueThrust.Id, fresh.Action.Id);
    }
}
