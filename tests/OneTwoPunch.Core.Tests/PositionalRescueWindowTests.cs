using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// When a positional is rescued, which is a question about the player rather than the game.
/// <para>
/// The rule used to be <c>AssumedAnimationLock * 2</c> - 1.3 seconds - which answers "will an
/// off-global fit in this weave window". Nobody chose it as a repositioning deadline; it fell
/// out of weave arithmetic. A recorded Monk pull has True North going out four times in two
/// and a half minutes, every one at <c>gcd 1.3s</c>, on a two charge cooldown: full uptime,
/// and so nothing banked for the moments the player genuinely cannot turn. Reported as "the
/// facing was right but it fires a little prematurely because I could easily make it in time".
/// </para>
/// </summary>
public sealed class PositionalRescueWindowTests
{
    private static RotationSession Session(float window) =>
        new(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings
            {
                UseOpener = false,
                SuggestionHoldSeconds = 0f,
                PositionalRescueWindow = window,
            });

    /// <summary>
    /// A weave window with a positional the player is not standing in. Coeurl form with the
    /// fury up asks for a flank, and the player is behind the boss.
    /// </summary>
    private static CombatSnapshot OutOfPosition(float gcdRemaining) =>
        new SnapshotBuilder()
            .Job(20).Level(100).Gcd(gcdRemaining).NoCombo().Enemies(1)
            .Position(RelativePosition.Rear)
            .Buff(A.CoeurlForm.Id, 25f)
            .Gauge(s => s.Gauges.Monk.CoeurlFury = 1)
            .Build();

    /// <summary>The off-globals that would otherwise take the weave slot under test.</summary>
    private static FakeActionState Weaves() =>
        new FakeActionState()
            .OnCooldown(A.Brotherhood.Id, 120f)
            .OnCooldown(A.RiddleOfFire.Id, 60f)
            .OnCooldown(A.RiddleOfWind.Id, 60f)
            .OnCooldown(A.PerfectBalance.Id, 60f)
            .OnCooldown(A.ForbiddenChakra.Id, 60f)
            .OnCooldown(A.SecondWind.Id, 120f);

    private static uint Suggest(float window, float gcdRemaining) =>
        Session(window).Resolve(RotationMode.SingleTarget, OutOfPosition(gcdRemaining), Weaves())
            .Action.Id;

    /// <summary>
    /// The reported behaviour. At the old threshold there is over a second left - plenty of
    /// time to walk round - and the rescue went out anyway.
    /// </summary>
    [Fact]
    public void AtTheTightDefaultThereIsStillTimeToWalkAndNoRescueIsOffered()
    {
        Assert.NotEqual(A.TrueNorth.Id, Suggest(window: 0.8f, gcdRemaining: 1.3f));
    }

    /// <summary>But once the global really is imminent, the rescue is what is wanted.</summary>
    [Fact]
    public void OnceTheGlobalIsImminentTheRescueIsOffered()
    {
        Assert.Equal(A.TrueNorth.Id, Suggest(window: 0.8f, gcdRemaining: 0.78f));
    }

    /// <summary>
    /// And someone who cannot reposition at all winds the setting up and gets the old
    /// behaviour back. That is the point of it being a setting rather than a better number.
    /// </summary>
    [Fact]
    public void AWiderWindowRescuesEarlyAgain()
    {
        Assert.Equal(A.TrueNorth.Id, Suggest(window: 2.0f, gcdRemaining: 1.9f));
    }

    /// <summary>
    /// Wound below the point a weave fits at all, the setting cannot promise a rescue any
    /// earlier than the engine would offer one - the weave check is the real floor and the
    /// clamp only keeps the two from disagreeing.
    /// </summary>
    [Fact]
    public void AWindowWoundBelowTheWeaveFloorChangesNothing()
    {
        Assert.NotEqual(A.TrueNorth.Id, Suggest(window: 0.1f, gcdRemaining: 0.9f));
        Assert.Equal(A.TrueNorth.Id, Suggest(window: 0.9f, gcdRemaining: 0.9f));
    }
}
