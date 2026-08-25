using TwoButton.Core.Engine;
using TwoButton.Core.Jobs;
using TwoButton.Core.Model;
using Xunit;

namespace TwoButton.Core.Tests;

/// <summary>
/// Invariants every job must hold, whatever its priority list says.
/// <para>
/// The lists themselves are judgement calls and will be argued over. These are not: the
/// button must always resolve to something, that something must always be an action the
/// verifier has checked, and no state of the world may throw. A rotation that violates one
/// of these is broken regardless of whether its priorities are any good.
/// </para>
/// </summary>
public sealed class AllJobsSmokeTests
{
    public static TheoryData<uint, string> AllJobs()
    {
        var data = new TheoryData<uint, string>();
        foreach (var job in JobRegistry.CreateAll())
            data.Add(job.JobId, job.Name);

        return data;
    }

    private static RotationSession Session(uint jobId, out IJobRotation job)
    {
        job = JobRegistry.Create(jobId) ?? throw new InvalidOperationException($"no job {jobId}");
        return new RotationSession(job, new RotationSettings { SuggestionHoldSeconds = 0f });
    }

    [Theory]
    [MemberData(nameof(AllJobs))]
    public void EveryJobSuggestsADeclaredActionInEveryMode(uint jobId, string name)
    {
        var session = Session(jobId, out var job);
        var declared = job.AllActions.Select(a => a.Id).ToHashSet();

        var modes = new List<RotationMode> { RotationMode.SingleTarget, RotationMode.Aoe };
        for (var i = 0; i < job.ExtraButtons.Count; i++)
            modes.Add(i == 0 ? RotationMode.Extra1 : RotationMode.Extra2);

        foreach (var mode in modes)
        {
            foreach (var snapshot in Situations())
            {
                var suggestion = session.Resolve(mode, snapshot, new FakeActionState());

                Assert.NotNull(suggestion);

                // Anything the button can become must be in the table the verifier checks,
                // or a wrong id could reach a hotbar without ever being validated.
                Assert.True(
                    declared.Contains(suggestion.Action.Id),
                    $"{name} ({mode}) suggested {suggestion.Action} which is not in AllActions");
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllJobs))]
    public void EveryJobSurvivesHavingNothingAvailable(uint jobId, string name)
    {
        var session = Session(jobId, out var job);

        // The worst case the engine can be handed: every action refused by the game.
        var actions = new FakeActionState();
        foreach (var action in job.AllActions)
            actions.Unusable(action.Id);

        foreach (var snapshot in Situations())
        {
            var single = session.Resolve(RotationMode.SingleTarget, snapshot, actions);
            var aoe = session.Resolve(RotationMode.Aoe, snapshot, actions);

            // Falls back to the player's own hotbar action rather than returning nothing,
            // so the button keeps working even when the engine has no opinion.
            Assert.Equal(job.SingleTargetButton.Id, single.Action.Id);
            Assert.Equal(job.AoeButton.Id, aoe.Action.Id);
        }
    }

    [Theory]
    [MemberData(nameof(AllJobs))]
    public void NoJobSuggestsAnOffGlobalWhenItWouldClipTheGcd(uint jobId, string name)
    {
        var session = Session(jobId, out var job);

        // The one promise the engine makes, checked on every job rather than just Dragoon.
        foreach (var gcdRemaining in new[] { 0f, 0.2f, 0.5f })
        {
            var snapshot = new SnapshotBuilder().Gcd(gcdRemaining).Build();
            var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState());

            Assert.True(
                suggestion.Kind == ActionKind.Gcd,
                $"{name} offered {suggestion.Action} as a weave with {gcdRemaining:0.0}s of GCD left");
        }
    }

    [Theory]
    [MemberData(nameof(AllJobs))]
    public void EveryJobDeclaresItsButtonsAndBurst(uint jobId, string name)
    {
        var job = JobRegistry.Create(jobId)!;
        var declared = job.AllActions.Select(a => a.Name).ToHashSet();

        Assert.Contains(job.SingleTargetButton.Name, declared);
        Assert.Contains(job.AoeButton.Name, declared);
        Assert.NotEmpty(job.SingleTarget.Rules);
        Assert.NotEmpty(job.Aoe.Rules);

        // The potion prompt needs somewhere to hang off outside the opener.
        Assert.True(
            job.BurstAction is not null || job.BurstStatus is not null || job.Opener is not null,
            $"{name} has no burst marker and no opener, so the potion prompt can never fire");

        foreach (var extra in job.ExtraButtons)
        {
            Assert.Contains(extra.Host.Name, declared);
            Assert.NotEmpty(extra.Plan.Rules);
            Assert.NotEmpty(extra.Purpose);
        }
    }

    /// <summary>A spread of situations a rotation has to cope with.</summary>
    private static IEnumerable<CombatSnapshot> Situations()
    {
        yield return new SnapshotBuilder().Gcd(0.1f).NoCombo().Build();
        yield return new SnapshotBuilder().Gcd(2.2f).NoCombo().Build();
        yield return new SnapshotBuilder().Gcd(0.1f).NoCombo().Moving().Build();
        yield return new SnapshotBuilder().Gcd(2.2f).NoCombo().Downtime().Build();
        yield return new SnapshotBuilder().Gcd(0.1f).NoCombo().Enemies(5).Build();
        yield return new SnapshotBuilder().Gcd(0.1f).NoCombo().Level(50).Build();
        yield return new SnapshotBuilder().Gcd(0.1f).NoCombo().OutOfCombat().Build();
    }
}
