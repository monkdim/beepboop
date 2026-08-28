using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
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
    /// The same walk, on the single-weave setting the plugin ships with. The chart draws two
    /// double weaves - Swiftcast with Amplifier, and Transpose with Triplecast - and on this
    /// setting the second of each pair cannot fit. Stepping over it is correct; giving up on
    /// the remaining seventeen steps is not.
    /// </summary>
    [Fact]
    public void ASingleWeaveBudgetStepsOverTheSecondWeaveRatherThanEndingTheOpener()
    {
        var job = JobRegistry.Create(25)!;
        var opener = job.Opener!;
        var session = new RotationSession(job, new RotationSettings
        {
            SuggestionHoldSeconds = 0f,
            WeaveStyle = WeaveStyle.Single,
        });

        var actions = new FakeActionState();
        var trace = new List<string>();
        var globalsDriven = 0;

        // Walk far enough to be past both double weaves and well into the second half.
        for (var frame = 0; frame < 60 && globalsDriven < 20; frame++)
        {
            var snapshot = AtPull(frame % 2 == 0 ? 0f : 2.0f).Build();
            var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

            trace.Add($"  frame {frame,2}: {suggestion.Action.Name,-16} ({suggestion.Note})");
            session.NotifyActionUsed(suggestion.Action.Id);

            if (suggestion.Kind == ActionKind.Gcd)
                globalsDriven++;
        }

        Assert.True(
            session.OpenerActive,
            $"the opener gave up before its twentieth global.\n{string.Join("\n", trace)}");
    }
}
