using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Dragoon.DragoonActions;

namespace OneTwoPunch.Core.Tests;

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


    /// <summary>
    /// The two buttons are resolved one after the other, every frame, for as long as both
    /// are on a hotbar. They used to share one held suggestion, so each handed its answer to
    /// the other and the pair swapped every hold window. Reported as "the button just starts
    /// changing rapidly without being pressed", and as never seeing the AoE rotation at all.
    /// </summary>
    [Fact]
    public void OneButtonsSuggestionNeverLeaksIntoTheOther()
    {
        var session = Session(0.15f);
        var actions = new FakeActionState();

        // Five targets, so the AoE button has an AoE answer of its own rather than falling
        // back to the single-target list.
        CombatSnapshot Frame(double now) =>
            new SnapshotBuilder().At(now).Gcd(0f).NoCombo().Enemies(5).Build();

        var single = session.Resolve(RotationMode.SingleTarget, Frame(10.0), actions);
        var aoe = session.Resolve(RotationMode.Aoe, Frame(10.0), actions);

        Assert.NotEqual(single.Action.Id, aoe.Action.Id);

        // Walk several frames across the hold window, alternating the way the game does.
        for (var i = 1; i <= 20; i++)
        {
            var now = 10.0 + (i * 0.05);

            var nextSingle = session.Resolve(RotationMode.SingleTarget, Frame(now), actions);
            var nextAoe = session.Resolve(RotationMode.Aoe, Frame(now), actions);

            Assert.True(
                nextSingle.Action.Id == single.Action.Id,
                $"frame {i}: the single-target button became {nextSingle.Action.Name}");

            Assert.True(
                nextAoe.Action.Id == aoe.Action.Id,
                $"frame {i}: the AoE button became {nextAoe.Action.Name}");
        }
    }
}
