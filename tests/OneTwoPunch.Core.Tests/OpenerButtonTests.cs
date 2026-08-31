using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Model;
using Xunit;
using A = OneTwoPunch.Core.Jobs.Monk.MonkActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Which button a scripted opener is allowed to drive.
/// <para>
/// It used to drive both, because nothing here looked at which one was asking. A recorded
/// level 100 dungeon has the player pressing the *area* button into a six target pull and
/// being walked through Twin Snakes, Demolish, Leaping Opo, Dragon Kick, Leaping Opo - five
/// single target globals on six enemies - with Rockbreaker sitting in the other list the
/// whole time.
/// </para>
/// </summary>
public sealed class OpenerButtonTests
{
    private static RotationSession Session(bool aoeFallsBack = false) =>
        new(JobRotationBase.Create<MonkRotation>(),
            new RotationSettings
            {
                SuggestionHoldSeconds = 0f,
                AoeFallsBackToSingleTarget = aoeFallsBack,
            });

    /// <summary>A pull just starting, with a pack in front of you.</summary>
    private static CombatSnapshot Pull(int enemies) =>
        new SnapshotBuilder()
            .Job(20).Level(100).Gcd(0f).NoCombo().Enemies(enemies)
            .Gauge(s => s.CombatDuration = 0.5f)
            .Build();

    /// <summary>The defect, stated directly.</summary>
    [Fact]
    public void TheAreaButtonDoesNotWalkTheSingleTargetChart()
    {
        var session = Session();

        var suggestion = session.Resolve(RotationMode.Aoe, Pull(enemies: 6), new FakeActionState());

        Assert.NotEqual(A.DragonKick.Id, suggestion.Action.Id);
        Assert.Equal(A.ShadowOfTheDestroyer.Id, suggestion.Action.Id);
    }

    /// <summary>And the single-target button is unaffected in the same moment.</summary>
    [Fact]
    public void TheSingleTargetButtonStillWalksIt()
    {
        var session = Session();

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Pull(enemies: 6), new FakeActionState());

        Assert.Equal(A.DragonKick.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The area button on one enemy is the single-target button, when the fallback is on -
    /// so it walks the chart there. Read off the mode the context was built with rather than
    /// the one the caller asked for, which is what makes that hold true.
    /// </summary>
    [Fact]
    public void TheAreaButtonWalksItOnceItHasFallenBackToSingleTarget()
    {
        var session = Session(aoeFallsBack: true);

        var suggestion = session.Resolve(RotationMode.Aoe, Pull(enemies: 1), new FakeActionState());

        Assert.Equal(A.DragonKick.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Standing down on the area button is not going off script. The opener holds its step
    /// rather than giving up, so it is still there for the pull after the pack.
    /// </summary>
    [Fact]
    public void PressingTheAreaButtonHoldsTheOpenerRatherThanEndingIt()
    {
        var session = Session();
        var actions = new FakeActionState();

        // The game asks about every slot every frame, so both buttons are answered.
        var area = session.Resolve(RotationMode.Aoe, Pull(enemies: 6), actions);
        session.Resolve(RotationMode.SingleTarget, Pull(enemies: 6), actions);

        session.NotifyActionUsed(area.Action.Id);

        Assert.True(session.OpenerActive, $"the opener gave up: {session.OpenerOutcome}");

        // Still on its first step, not advanced by a global that was never in the chart.
        Assert.Equal(
            A.DragonKick.Id,
            session.Resolve(RotationMode.SingleTarget, Pull(enemies: 6), actions).Action.Id);
    }
}
