using TwoButton.Core.Engine;
using TwoButton.Core.Jobs;
using TwoButton.Core.Jobs.Dragoon;
using Xunit;

namespace TwoButton.Core.Tests;

/// <summary>
/// Hard-coded ids are the most fragile thing in the plugin. These tests pin down the
/// behaviour that keeps a wrong one from ever reaching a raid.
/// </summary>
public sealed class ActionTableVerifierTests
{
    private sealed class StubLookup : IGameDataLookup
    {
        private readonly Dictionary<uint, string> _actions = [];
        private readonly Dictionary<uint, string> _statuses = [];

        public StubLookup Action(uint id, string name)
        {
            _actions[id] = name;
            return this;
        }

        public StubLookup Status(uint id, string name)
        {
            _statuses[id] = name;
            return this;
        }

        /// <summary>Registers every id/name pair the job currently believes in.</summary>
        public StubLookup FromJob(IJobRotation job)
        {
            foreach (var action in job.AllActions)
                _actions[action.Id] = action.Name;

            foreach (var status in job.AllStatuses)
                _statuses[status.Id] = status.Name;

            return this;
        }

        public string? GetActionName(uint actionId) =>
            _actions.TryGetValue(actionId, out var name) ? name : null;

        public uint? FindActionIdByName(string name)
        {
            foreach (var pair in _actions)
            {
                if (string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }

            return null;
        }

        public string? GetStatusName(uint statusId) =>
            _statuses.TryGetValue(statusId, out var name) ? name : null;

        public uint? FindStatusIdByName(string name)
        {
            foreach (var pair in _statuses)
            {
                if (string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase))
                    return pair.Key;
            }

            return null;
        }
    }

    [Fact]
    public void AMatchingTableVerifiesCleanly()
    {
        var job = JobRotationBase.Create<DragoonRotation>();
        var report = ActionTableVerifier.Verify(job, new StubLookup().FromJob(job));

        Assert.True(report.IsSafeToRun);
        Assert.Equal(0, report.RepairedCount);
        Assert.Equal(0, report.UnresolvedCount);
    }

    [Fact]
    public void AWrongIdIsRepairedFromTheName()
    {
        var job = JobRotationBase.Create<DragoonRotation>();
        var original = DragoonActions.TrueThrust.Id;

        try
        {
            // The sheet says True Thrust really lives at 9999, not where we guessed.
            var lookup = new StubLookup().FromJob(job);
            lookup.Action(original, "Something Else");
            lookup.Action(9999, "True Thrust");

            var report = ActionTableVerifier.Verify(job, lookup);

            Assert.True(report.IsSafeToRun);
            Assert.Equal(9999u, DragoonActions.TrueThrust.Id);
            Assert.True(DragoonActions.TrueThrust.WasRepaired);
            Assert.Contains(report.Entries, e => e.Outcome == VerificationOutcome.Repaired);
        }
        finally
        {
            DragoonActions.TrueThrust.Bind(original);
        }
    }

    [Fact]
    public void AnUnresolvableActionDisablesTheJobRatherThanGuessing()
    {
        var job = JobRotationBase.Create<DragoonRotation>();

        var lookup = new StubLookup().FromJob(job);
        lookup.Action(DragoonActions.Drakesbane.Id, "Not Drakesbane");

        var report = ActionTableVerifier.Verify(job, lookup);

        Assert.False(report.IsSafeToRun);
        Assert.Equal(1, report.UnresolvedCount);
        Assert.Contains("UNRESOLVED", report.Summarise());
    }

    [Fact]
    public void EveryRegisteredJobHasAUniqueIdAndBothButtons()
    {
        var jobs = JobRegistry.CreateAll();

        Assert.NotEmpty(jobs);
        Assert.Equal(jobs.Count, jobs.Select(j => j.JobId).Distinct().Count());

        foreach (var job in jobs)
        {
            Assert.NotNull(job.SingleTargetButton);
            Assert.NotNull(job.AoeButton);
            Assert.NotEmpty(job.SingleTarget.Rules);
            Assert.NotEmpty(job.Aoe.Rules);
        }
    }

    [Fact]
    public void EveryActionARuleCanSuggestIsDeclaredForVerification()
    {
        // A rule that suggests an action missing from AllActions would slip past the id
        // check entirely, so the two lists must not drift apart.
        foreach (var job in JobRegistry.CreateAll())
        {
            var declared = job.AllActions.Select(a => a.Name).ToHashSet();

            Assert.Contains(job.SingleTargetButton.Name, declared);
            Assert.Contains(job.AoeButton.Name, declared);

            if (job.PositionalRescue is not null)
                Assert.Contains(job.PositionalRescue.Name, declared);

            if (job.Opener is null)
                continue;

            foreach (var step in job.Opener.Steps)
                Assert.Contains(step.Name, declared);
        }
    }
}
