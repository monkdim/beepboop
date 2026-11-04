using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Bard;
using OneTwoPunch.Core.Model;
using Xunit;
using B = OneTwoPunch.Core.Jobs.Bard.BardActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// An opener weave whose own recast is still turning is stepped over, however little is
/// left on it. A recorded Bard pull read "step 16 of 21, gave up: step 16 (Empyreal Arrow)
/// was not usable (#582)" - six steps abandoned over about 1.6 seconds of cooldown, and the
/// priority list fired that same Empyreal Arrow two seconds later anyway.
/// </summary>
public sealed class OpenerRecastStepOverTests
{
    private const int RecastNotReady = 582;

    private static CombatSnapshot At(double now) =>
        new SnapshotBuilder()
            .Job(23).Level(100).Gcd(0f, 2.3f).Enemies(1).At(now)
            .Gauge(s => s.CombatDuration = 0.5f)
            .Build();

    /// <summary>
    /// Walks the real Bard opener to its second Empyreal Arrow, refuses it with the recast
    /// code, and asserts the chart carries on rather than being thrown away.
    /// </summary>
    [Fact]
    public void AShortRecastOnAWeaveDoesNotAbandonTheRestOfTheChart()
    {
        var job = JobRotationBase.Create<BardRotation>();
        var session = new RotationSession(job,
            new RotationSettings { WeaveStyle = WeaveStyle.Double, SuggestionHoldSeconds = 0f });
        var actions = new FakeActionState();

        var steps = job.Opener!.Steps;
        var secondEmpyreal = -1;
        var seen = 0;
        for (var i = 0; i < steps.Count; i++)
        {
            if (steps[i].Id != B.EmpyrealArrow.Id)
                continue;
            if (++seen == 2)
            {
                secondEmpyreal = i;
                break;
            }
        }

        Assert.True(secondEmpyreal > 0, "the Bard opener should ask for Empyreal Arrow twice");

        // Drive the chart up to that step.
        for (var i = 0; i < secondEmpyreal; i++)
        {
            session.Resolve(RotationMode.SingleTarget, At(i), actions);
            session.NotifyActionUsed(steps[i].Id);
        }

        Assert.Equal(secondEmpyreal, session.OpenerStep);

        // Its recast has a moment left - shorter than the global, which is what used to
        // fall past the step-over and abandon the opener.
        actions.Refused(B.EmpyrealArrow.Id, RecastNotReady).OnCooldown(B.EmpyrealArrow.Id, 1.6f);

        session.Resolve(RotationMode.SingleTarget, At(secondEmpyreal), actions);

        Assert.True(session.OpenerActive, "the opener should still be driving");
        Assert.True(session.OpenerStep > secondEmpyreal, "it should have stepped over the weave");
    }
}
