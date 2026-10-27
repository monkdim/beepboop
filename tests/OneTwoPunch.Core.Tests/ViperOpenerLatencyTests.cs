using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;
using V = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

public sealed class ViperOpenerLatencyTests
{
    private static CombatSnapshot At(double now, bool casting = false, float gcd = 2.4f) =>
        new SnapshotBuilder().Job(41).Gcd(gcd).At(now).Casting(casting)
            .Gauge(s => s.CombatDuration = 0.5f).Build();

    // Use the real Viper opener and action notifications, not a custom test rotation.
    private static RotationSession AfterHuntersCoil(FakeActionState actions, WeaveStyle style = WeaveStyle.Double)
    {
        var job = JobRegistry.Create(41)!;
        var session = new RotationSession(job, new RotationSettings { WeaveStyle = style, SuggestionHoldSeconds = 0 });
        foreach (var step in job.Opener!.Steps.Take(6))
        {
            Assert.Equal(step.Id, session.Resolve(RotationMode.SingleTarget, At(0), actions).Action.Id);
            session.NotifyActionUsed(step.Id);
        }
        Assert.Equal(6,session.OpenerStep);
        return session;
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.05)]
    [InlineData(0.15)]
    [InlineData(0.5)]
    [InlineData(0.749)]
    public void DelayedBitePrerequisitesDoNotDiscardEitherAttack(double delay)
    {
        var actions = new FakeActionState();
        var session = AfterHuntersCoil(actions);
        actions.Refused(V.TwinfangBite.Id,572).Refused(V.TwinbloodBite.Id,572);
        session.Resolve(RotationMode.SingleTarget, At(10), actions);
        session.Resolve(RotationMode.SingleTarget, At(10 + delay), actions);
        Assert.Equal(6,session.OpenerStep);
        Assert.True(session.OpenerActive);
        Assert.Null(session.OpenerOutcome);

        actions.Usable(V.TwinfangBite.Id);
        Assert.Equal(V.TwinfangBite.Id,session.Resolve(RotationMode.SingleTarget,At(10 + delay),actions).Action.Id);
        session.NotifyActionUsed(V.TwinfangBite.Id);
        session.Resolve(RotationMode.SingleTarget,At(10 + delay),actions);
        session.Resolve(RotationMode.SingleTarget,At(10 + delay + .5),actions);
        Assert.Equal(7,session.OpenerStep);

        actions.Usable(V.TwinbloodBite.Id);
        Assert.Equal(V.TwinbloodBite.Id,session.Resolve(RotationMode.SingleTarget,At(10 + delay + .5),actions).Action.Id);
        session.NotifyActionUsed(V.TwinbloodBite.Id);
        Assert.Equal(8,session.OpenerStep);
        Assert.Equal(V.SwiftskinsCoil.Id,session.Resolve(RotationMode.SingleTarget,At(10 + delay + .5),actions).Action.Id);
    }

    [Fact]
    public void PersistentMissingPrerequisitesEventuallyLetTheOpenerContinue()
    {
        var actions = new FakeActionState();
        var session = AfterHuntersCoil(actions);
        actions.Refused(V.TwinfangBite.Id,572).Refused(V.TwinbloodBite.Id,572);
        session.Resolve(RotationMode.SingleTarget,At(10),actions);
        session.Resolve(RotationMode.SingleTarget,At(10.749),actions);
        Assert.Equal(6,session.OpenerStep);
        session.Resolve(RotationMode.SingleTarget,At(10.75),actions);
        Assert.Equal(7,session.OpenerStep);
        session.Resolve(RotationMode.SingleTarget,At(11.499),actions);
        Assert.Equal(7,session.OpenerStep);
        var next = session.Resolve(RotationMode.SingleTarget,At(11.5),actions);
        Assert.Equal(8,session.OpenerStep);
        Assert.Equal(V.SwiftskinsCoil.Id,next.Action.Id);
        Assert.True(session.OpenerActive);
        Assert.Null(session.OpenerOutcome);
    }

    [Fact]
    public void ARefusalDuringCastingDoesNotSkipAnAttackEvenAfterSettling()
    {
        var actions = new FakeActionState();
        var session = AfterHuntersCoil(actions);
        actions.Refused(V.TwinfangBite.Id,572);
        session.Resolve(RotationMode.SingleTarget,At(10,casting:true),actions);
        session.Resolve(RotationMode.SingleTarget,At(11,casting:true),actions);
        Assert.Equal(6,session.OpenerStep);
        actions.Usable(V.TwinfangBite.Id);
        Assert.Equal(V.TwinfangBite.Id,session.Resolve(RotationMode.SingleTarget,At(11),actions).Action.Id);
    }

    /// <summary>
    /// Waiting out a late prerequisite on the single-weave setting still gets both halves of
    /// the pair, because Viper's own minimum raises the budget to two. This asserted the
    /// opposite while the minimum was advisory, and that is the shape of the duty that came
    /// back doing the three-step combo and nothing else: Twinfang Bite landed, Twinblood
    /// Bite was dropped for want of a second weave, and the opener moved on without it.
    /// </summary>
    [Fact]
    public void WaitingStillGetsBothBitesOnTheSingleWeaveSetting()
    {
        var actions = new FakeActionState();
        var session = AfterHuntersCoil(actions,WeaveStyle.Single);
        Assert.Equal(2,session.WeaveBudget);

        actions.Refused(V.TwinfangBite.Id,572);
        session.Resolve(RotationMode.SingleTarget,At(10),actions);
        actions.Usable(V.TwinfangBite.Id);
        Assert.Equal(V.TwinfangBite.Id,session.Resolve(RotationMode.SingleTarget,At(10.15),actions).Action.Id);
        session.NotifyActionUsed(V.TwinfangBite.Id);
        Assert.Equal(7,session.OpenerStep);

        var second = session.Resolve(RotationMode.SingleTarget,At(10.2),actions);
        Assert.Equal(ActionKind.OGcd,second.Kind);
        Assert.Equal(V.TwinbloodBite.Id,second.Action.Id);
        session.NotifyActionUsed(V.TwinbloodBite.Id);
        Assert.Equal(8,session.OpenerStep);
    }

    [Fact]
    public void RecoveredPrerequisitesStillWaitForASafeWeaveWindow()
    {
        var actions = new FakeActionState();
        var session = AfterHuntersCoil(actions);
        actions.Refused(V.TwinfangBite.Id,572);
        session.Resolve(RotationMode.SingleTarget,At(10),actions);
        actions.Usable(V.TwinfangBite.Id);
        Assert.Equal(ActionKind.Gcd,session.Resolve(RotationMode.SingleTarget,At(10.15,gcd:.2f),actions).Kind);
        Assert.Equal(6,session.OpenerStep);
    }
}
