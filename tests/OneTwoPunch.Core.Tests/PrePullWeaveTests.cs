using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// What the button may offer before the pull while a scripted opener is still standing by.
/// <para>
/// The opener declines for a frame or two at a standing start: the game refuses everything
/// during the animation lock of the global before, so "is the next step usable" comes back
/// no. The priority list answered into that gap, and on Monk it answered with a raid buff.
/// </para>
/// <para>
/// A recorded pull has Dragon Kick as opener step one at 00:03.2 and Brotherhood out of the
/// priority list at 00:03.8, still out of combat. The chart puts Brotherhood at step five
/// beside Riddle of Fire. Spending it 1.8s early threw away that much of a twenty second
/// raid buff and left it 5.6s out of phase with Riddle of Fire for the rest of the fight,
/// since both are used on cooldown from wherever they first went off.
/// </para>
/// </summary>
public sealed class PrePullWeaveTests
{
    private static RotationSession Session() =>
        new(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings { SuggestionHoldSeconds = 0f });

    /// <summary>Out of combat, with a wide open weave window and no form up.</summary>
    private static CombatSnapshot BeforeThePull(float gcdRemaining) =>
        new SnapshotBuilder()
            .Job(20).Level(100).Gcd(gcdRemaining).NoCombo().Enemies(1)
            .OutOfCombat()
            .Build();

    /// <summary>
    /// Perfect Balance is opener step two. On a short cooldown it is neither usable nor far
    /// enough out to be stepped over, which is exactly the frame the opener stands down on -
    /// and out of combat it declines rather than aborting.
    /// </summary>
    private static FakeActionState TheOpenerStandingDown() =>
        new FakeActionState().OnCooldown(A.PerfectBalance.Id, 1f);

    /// <summary>The defect, stated directly.</summary>
    [Fact]
    public void TheListDoesNotBurnARaidBuffWhileTheOpenerIsStandingBy()
    {
        var session = Session();
        var actions = TheOpenerStandingDown();

        session.Resolve(RotationMode.SingleTarget, BeforeThePull(0f), actions);
        session.NotifyActionUsed(A.DragonKick.Id);

        // Wide weave window, so nothing here is about the slot being too tight.
        var suggestion = session.Resolve(RotationMode.SingleTarget, BeforeThePull(1.3f), actions);

        Assert.NotEqual(A.Brotherhood.Id, suggestion.Action.Id);
        Assert.NotEqual(A.RiddleOfFire.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// And what it offers instead is the global. That one is not thrown away by being early:
    /// it is what starts the fight, and it is what the opener is about to ask for anyway.
    /// </summary>
    [Fact]
    public void TheGlobalIsStillOfferedBeforeThePull()
    {
        var session = Session();
        var actions = TheOpenerStandingDown();

        session.Resolve(RotationMode.SingleTarget, BeforeThePull(0f), actions);
        session.NotifyActionUsed(A.DragonKick.Id);

        var suggestion = session.Resolve(RotationMode.SingleTarget, BeforeThePull(1.3f), actions);

        Assert.Equal(A.DragonKick.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Once the fight is running the guard is gone entirely - weaving on cooldown is the
    /// whole job of the off-global half of the list.
    /// </summary>
    [Fact]
    public void OnceTheFightIsRunningTheWeaveComesBack()
    {
        var session = Session();
        var actions = new FakeActionState();

        var inCombat = new SnapshotBuilder()
            .Job(20).Level(100).Gcd(1.3f).NoCombo().Enemies(1)
            .Gauge(s => s.CombatDuration = 30f)
            .Build();

        // Well past the opener's grace, so the script is not driving.
        var suggestion = session.Resolve(RotationMode.SingleTarget, inCombat, actions);

        Assert.Equal(A.Brotherhood.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Pressing what the button was showing before the pull holds the opener where it is.
    /// It used to send it back to step one, so the chart's own first global was pressed
    /// twice and whatever the list had offered in the gap was already spent.
    /// </summary>
    [Fact]
    public void PressingOurOwnSuggestionBeforeThePullDoesNotRewindTheOpener()
    {
        var session = Session();
        var actions = TheOpenerStandingDown();

        session.Resolve(RotationMode.SingleTarget, BeforeThePull(0f), actions);
        session.NotifyActionUsed(A.DragonKick.Id);

        // Whatever it is offering in the gap - the player can only press what they are shown.
        var shown = session.Resolve(RotationMode.SingleTarget, BeforeThePull(1.3f), actions);
        session.NotifyActionUsed(shown.Action.Id);

        Assert.StartsWith(
            "step 2 of ",
            session.OpenerReport ?? string.Empty,
            StringComparison.Ordinal);
    }
}
