using TwoButton.Core.Engine;
using TwoButton.Core.Jobs;
using TwoButton.Core.Jobs.Dragoon;
using TwoButton.Core.Model;
using Xunit;
using A = TwoButton.Core.Jobs.Dragoon.DragoonActions;

namespace TwoButton.Core.Tests;

/// <summary>
/// An icon that changes while somebody is reaching for the key is the single most hostile
/// thing this plugin could do to the people it is for.
/// </summary>
public sealed class StabiliserTests
{
    private static RotationSession Session(float hold)
    {
        var settings = new RotationSettings
        {
            UseOpener = false,
            SuggestionHoldSeconds = hold,
        };

        return new RotationSession(JobRotationBase.Create<DragoonRotation>(), settings);
    }

    [Fact]
    public void ASuggestionIsHeldBrieflyEvenAfterAHigherPriorityOneAppears()
    {
        var session = Session(0.15f);

        // Lance Charge is still cooling down, so the button is Battle Litany.
        var first = session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.0).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState().OnCooldown(A.LanceCharge.Id, 1f));

        Assert.Equal(A.BattleLitany.Id, first.Action.Id);

        // 50ms later Lance Charge comes up and outranks it - but the icon must not swap
        // under somebody's hand, and Battle Litany is still a perfectly good press.
        var held = session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.05).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState());

        Assert.Equal(A.BattleLitany.Id, held.Action.Id);
    }

    [Fact]
    public void TheHoldExpiresAndTheNewSuggestionComesThrough()
    {
        var session = Session(0.15f);

        session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.0).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState());

        var later = session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.4).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState().OnCooldown(A.LanceCharge.Id, 60f));

        Assert.Equal(A.BattleLitany.Id, later.Action.Id);
    }

    [Fact]
    public void AHeldSuggestionIsDroppedTheMomentItStopsBeingUsable()
    {
        var session = Session(0.15f);

        session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.0).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState());

        // Same frame budget as the hold test, but Lance Charge itself is now unusable.
        var fresh = session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.05).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState().OnCooldown(A.LanceCharge.Id, 60f));

        Assert.NotEqual(A.LanceCharge.Id, fresh.Action.Id);
    }

    [Fact]
    public void AHeldOffGlobalIsDroppedWhenTheWeaveWindowCloses()
    {
        var session = Session(0.15f);
        var actions = new FakeActionState();

        var first = session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.0).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            actions);

        Assert.Equal(ActionKind.OGcd, first.Kind);

        // The GCD is about to come up; holding the off-global now would clip it.
        var afterWindow = session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.05).Gcd(0.2f).Combo(A.TrueThrust).Build(),
            actions);

        Assert.Equal(ActionKind.Gcd, afterWindow.Kind);
    }

    [Fact]
    public void HoldingCanBeTurnedOffEntirely()
    {
        var session = Session(0f);

        session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.0).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState());

        var immediate = session.Resolve(
            RotationMode.SingleTarget,
            new SnapshotBuilder().At(10.01).Gcd(2.2f).Combo(A.TrueThrust).Build(),
            new FakeActionState().OnCooldown(A.LanceCharge.Id, 60f));

        Assert.Equal(A.BattleLitany.Id, immediate.Action.Id);
    }
}
