using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The line at the foot of a recording that says what the opener did.
/// <para>
/// It is read by a person trying to work out why a pull went the way it did, so a line that
/// describes a different pull is worse than no line at all - it reads as a finding. Two
/// recorded synced dungeons, at level 70 and level 90, both ended with "held there:
/// Brotherhood is still on cooldown" for a Monk opener that is written for level 100 and
/// could not have been consulted at either level. The reason belonged to an earlier pull
/// that day and had been carried across, because the report outlives the recording it was
/// made in and was only ever overwritten when there was something new to say.
/// </para>
/// </summary>
public sealed class OpenerReportTests
{
    private static RotationSession Session(bool useOpener = true) =>
        new(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings { UseOpener = useOpener, SuggestionHoldSeconds = 0f });

    private static CombatSnapshot Pull(byte level = 100, bool inCombat = true, float duration = 0.5f) =>
        new SnapshotBuilder()
            .Job(20).Level(level).Gcd(0f).NoCombo().Enemies(1)
            .Gauge(s =>
            {
                s.InCombat = inCombat;
                s.CombatDuration = inCombat ? duration : 0f;
            })
            .Build();

    /// <summary>
    /// A synced pull says so, and says both numbers. "The level is below the one it is
    /// written for" is equally true of a level 99 pull and a level 60 one, and reading a
    /// synced log you want to know which without going and looking the opener up.
    /// </summary>
    [Fact]
    public void ASyncedPullNamesTheLevelItWasSyncedTo()
    {
        var session = Session();

        session.Resolve(RotationMode.SingleTarget, Pull(level: 90), new FakeActionState());

        var report = session.OpenerReport ?? string.Empty;

        Assert.Contains("level 100", report);
        Assert.Contains("you are 90", report);
    }

    /// <summary>
    /// And it is the level that gets named, not whatever the next guard would have said.
    /// Brotherhood being on cooldown is true here and is not the reason.
    /// </summary>
    [Fact]
    public void TheLevelIsNamedRatherThanACooldownBehindIt()
    {
        var session = Session();

        session.Resolve(
            RotationMode.SingleTarget,
            Pull(level: 90),
            new FakeActionState().OnCooldown(A.Brotherhood.Id, 80f));

        Assert.DoesNotContain("Brotherhood", session.OpenerReport ?? string.Empty);
    }

    /// <summary>An opener turned off in the settings says that, rather than nothing.</summary>
    [Fact]
    public void AnOpenerSwitchedOffSaysSo()
    {
        var session = Session(useOpener: false);

        session.Resolve(RotationMode.SingleTarget, Pull(), new FakeActionState());

        Assert.Contains("switched off", session.OpenerReport ?? string.Empty);
    }

    /// <summary>
    /// The defect. A report belongs to the recording it is printed in, and a session outlives
    /// any number of recordings - a whole evening of duties.
    /// </summary>
    [Fact]
    public void TheCarriedReportIsDroppedWhenANewRecordingStarts()
    {
        var session = Session();
        var actions = new FakeActionState();

        // A pull that has something to say, then the fight ending, which is what puts the
        // report somewhere it can outlive the pull.
        session.Resolve(RotationMode.SingleTarget, Pull(duration: 60f), actions);
        session.Resolve(RotationMode.SingleTarget, Pull(inCombat: false), actions);

        Assert.NotNull(session.OpenerReportForLog);

        session.ForgetOpenerReport();

        Assert.Null(session.OpenerReportForLog);
    }
}
