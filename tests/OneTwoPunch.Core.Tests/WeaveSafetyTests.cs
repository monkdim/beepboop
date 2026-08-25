using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Dragoon.DragoonActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The single promise the engine has to keep: pressing the button must never cost a global
/// cooldown. Everything else is a preference.
/// </summary>
public sealed class WeaveSafetyTests
{
    // The opener is exercised in OpenerTests; these tests are about the weave window alone.
    private static RotationSession Session(RotationSettings? settings = null)
    {
        settings ??= new RotationSettings();
        settings.UseOpener = false;
        settings.SuggestionHoldSeconds = 0f;
        return new RotationSession(JobRotationBase.Create<DragoonRotation>(), settings);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.3f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    public void NoOffGlobalIsSuggestedWhenItWouldClipTheGcd(float gcdRemaining)
    {
        var session = Session();
        var snapshot = new SnapshotBuilder().Gcd(gcdRemaining).Combo(A.TrueThrust).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(ActionKind.Gcd, suggestion.Kind);
    }

    [Fact]
    public void AnOffGlobalIsSuggestedWhenTheGapIsWideEnough()
    {
        var session = Session();
        var snapshot = new SnapshotBuilder().Gcd(2.0f).Combo(A.TrueThrust).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(ActionKind.OGcd, suggestion.Kind);
    }

    [Fact]
    public void RunningAnimationLockIsSubtractedFromTheGap()
    {
        var session = Session();

        // 1.0s of GCD left, but 0.6s of that is still animation lock: no room.
        var snapshot = new SnapshotBuilder().Gcd(1.0f).AnimationLock(0.6f).Combo(A.TrueThrust).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(ActionKind.Gcd, suggestion.Kind);
    }

    [Fact]
    public void SingleWeaveStyleStopsAfterOneOffGlobal()
    {
        var settings = new RotationSettings { WeaveStyle = WeaveStyle.Single };
        var session = Session(settings);
        var actions = new FakeActionState();
        var snapshot = new SnapshotBuilder().Gcd(2.2f).Combo(A.TrueThrust).Build();

        var first = session.Resolve(RotationMode.SingleTarget, snapshot, actions);
        Assert.Equal(ActionKind.OGcd, first.Kind);

        session.NotifyActionUsed(first.Action.Id);

        var second = session.Resolve(RotationMode.SingleTarget, snapshot, actions);
        Assert.Equal(ActionKind.Gcd, second.Kind);
    }

    [Fact]
    public void DoubleWeaveStyleAllowsASecondOffGlobal()
    {
        var settings = new RotationSettings { WeaveStyle = WeaveStyle.Double };
        var session = Session(settings);
        var actions = new FakeActionState();
        var snapshot = new SnapshotBuilder().Gcd(2.2f).Combo(A.TrueThrust).Build();

        var first = session.Resolve(RotationMode.SingleTarget, snapshot, actions);
        session.NotifyActionUsed(first.Action.Id);
        actions.OnCooldown(first.Action.Id, 60f);

        var second = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(ActionKind.OGcd, second.Kind);
        Assert.NotEqual(first.Action.Id, second.Action.Id);
    }

    [Fact]
    public void WeaveStyleNoneNeverSuggestsAnOffGlobal()
    {
        var settings = new RotationSettings { WeaveStyle = WeaveStyle.None };
        var session = Session(settings);
        var snapshot = new SnapshotBuilder().Gcd(2.4f).Combo(A.TrueThrust).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

        Assert.Equal(ActionKind.Gcd, suggestion.Kind);
    }

    [Fact]
    public void UsingAGcdOpensAFreshWeaveWindow()
    {
        var settings = new RotationSettings { WeaveStyle = WeaveStyle.Single };
        var session = Session(settings);
        var actions = new FakeActionState();
        var snapshot = new SnapshotBuilder().Gcd(2.2f).Combo(A.TrueThrust).Build();

        var first = session.Resolve(RotationMode.SingleTarget, snapshot, actions);
        session.NotifyActionUsed(first.Action.Id);
        Assert.Equal(ActionKind.Gcd, session.Resolve(RotationMode.SingleTarget, snapshot, actions).Kind);

        session.NotifyActionUsed(A.LanceBarrage.Id);

        Assert.Equal(ActionKind.OGcd, session.Resolve(RotationMode.SingleTarget, snapshot, actions).Kind);
    }

    [Fact]
    public void ASuggestionIsNeverAnActionTheGameWouldRefuse()
    {
        var session = Session();
        var rotation = JobRotationBase.Create<DragoonRotation>();

        // Everything the job owns is on cooldown and unusable except the combo starter.
        var actions = new FakeActionState();
        foreach (var action in rotation.AllActions)
        {
            if (action.Id != A.TrueThrust.Id)
                actions.Unusable(action.Id);
        }

        var snapshot = new SnapshotBuilder().Gcd(2.4f).NoCombo().Build();
        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(A.TrueThrust.Id, suggestion.Action.Id);
    }
}
