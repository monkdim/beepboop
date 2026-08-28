using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.BlackMage;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Drives a session through the Black Mage opener step by step, the way a pull does: ask
/// what to press, press it, tell the session it went off, ask again.
/// <para>
/// Two recorded level 100 pulls both stop being driven after step seven - the Fire IV that
/// follows Ley Lines - and everything after it comes from the priority list. That costs
/// more than it sounds: the chart wants Xenoglossy and then Manafont there, and in both
/// logs Manafont instead landed some twenty-five seconds late.
/// </para>
/// <para>
/// Reading the engine did not explain it, so this walks it instead. The trace is carried
/// into the assertion message so a failure names the step it died on and what it was asked
/// for at the time.
/// </para>
/// </summary>
public sealed class BlackMageOpenerWalkTests
{
    private static SnapshotBuilder AtPull(float gcdRemaining) =>
        new SnapshotBuilder()
            .Job(25)
            .Level(100)
            .Gcd(gcdRemaining)
            .NoCombo()
            .Enemies(1)
            // Under the opener grace, so the run counts as a real pull rather than a
            // session loaded mid-fight.
            .Gauge(s => s.CombatDuration = 1f);

    /// <summary>
    /// The whole sequence, with room for every weave. Nothing here is about weave budgets:
    /// the window is wide open on every step so a step that is skipped is skipped for some
    /// other reason.
    /// </summary>
    [Fact]
    public void TheOpenerDrivesEveryStepOfItsOwnSequence()
    {
        var job = JobRegistry.Create(25)!;
        var opener = job.Opener!;
        var session = new RotationSession(job, new RotationSettings
        {
            SuggestionHoldSeconds = 0f,
            WeaveStyle = WeaveStyle.Double,
        });

        var actions = new FakeActionState();
        var trace = new List<string>();

        for (var i = 0; i < opener.Steps.Count; i++)
        {
            var wanted = opener.Steps[i];
            var snapshot = AtPull(wanted.Kind == ActionKind.OGcd ? 2.0f : 0f).Build();

            var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);
            trace.Add($"  step {i + 1,2}: wanted {wanted.Name,-16} got {suggestion.Action.Name,-16} ({suggestion.Note})");

            Assert.True(
                suggestion.Action.Id == wanted.Id,
                $"the opener stopped driving.\n{string.Join("\n", trace)}");

            session.NotifyActionUsed(suggestion.Action.Id);
        }
    }

    /// <summary>
    /// A weave that cannot fit right now makes the opener wait, not give up. On the
    /// single-weave setting the chart's Swiftcast-then-Amplifier pair cannot both fit, and
    /// the global is up: the opener has nothing to say for that frame and the priority list
    /// answers instead.
    /// </summary>
    [Fact]
    public void AWeaveThatCannotFitLeavesTheOpenerWaitingRatherThanEndingIt()
    {
        var (session, opener, actions) = WalkedToTheSwiftcastStep();

        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull(0f).Build(), actions);

        Assert.True(session.OpenerActive, $"the opener gave up: {session.OpenerOutcome}");
        Assert.Null(session.OpenerOutcome);
        Assert.NotEqual(opener.Steps[2].Id, suggestion.Action.Id);
    }

    /// <summary>
    /// And pressing the sequence's next global while that weave is still pending carries the
    /// opener on, stepping over the weave rather than throwing the rest away.
    /// </summary>
    [Fact]
    public void PressingTheNextGlobalStepsOverAPendingWeave()
    {
        var (session, opener, actions) = WalkedToTheSwiftcastStep();

        // Steps 3 and 4 are the Swiftcast and Amplifier weaves; step 5 is the next global.
        var nextGlobal = opener.Steps[4];
        session.NotifyActionUsed(nextGlobal.Id);

        // In a weave window, because the step the opener lands on next is itself a weave -
        // asking on an open global would only show it waiting again.
        var suggestion = session.Resolve(RotationMode.SingleTarget, AtPull(2.0f).Build(), actions);

        Assert.True(session.OpenerActive, $"the opener gave up: {session.OpenerOutcome}");
        Assert.Equal(opener.Steps[5].Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Going genuinely off script does end it - but it now says so out loud. Both recorded
    /// pulls that stopped being driven were silent about it, which is why neither could be
    /// read for a cause.
    /// </summary>
    [Fact]
    public void GoingOffScriptEndsTheOpenerAndSaysWhy()
    {
        var (session, _, _) = WalkedToTheSwiftcastStep();

        session.NotifyActionUsed(BlackMageActions.Blizzard3.Id);

        Assert.False(session.OpenerActive);
        Assert.NotNull(session.OpenerOutcome);
        Assert.Contains("Blizzard III", session.OpenerOutcome);
    }

    /// <summary>Walks the first two globals, leaving the opener on its Swiftcast step.</summary>
    private static (RotationSession Session, Opener Opener, FakeActionState Actions) WalkedToTheSwiftcastStep()
    {
        var job = JobRegistry.Create(25)!;
        var opener = job.Opener!;
        var session = new RotationSession(job, new RotationSettings
        {
            SuggestionHoldSeconds = 0f,
            WeaveStyle = WeaveStyle.Single,
        });

        var actions = new FakeActionState();

        for (var i = 0; i < 2; i++)
        {
            session.Resolve(RotationMode.SingleTarget, AtPull(0f).Build(), actions);
            session.NotifyActionUsed(opener.Steps[i].Id);
        }

        return (session, opener, actions);
    }
}
