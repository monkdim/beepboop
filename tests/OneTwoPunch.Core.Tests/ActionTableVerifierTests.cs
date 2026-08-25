using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Jobs.Ninja;
using Xunit;

namespace OneTwoPunch.Core.Tests;

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

        /// <summary>
        /// Makes an id vanish from the game's data entirely, which is what genuinely
        /// unresolvable means: not merely under a different name, but not there. Renaming an
        /// id no longer models this - a real id under a name the table does not use is an
        /// alias, and aliases are expected.
        /// </summary>
        public StubLookup MissingAction(uint id)
        {
            _actions.Remove(id);
            return this;
        }

        public StubLookup MissingStatus(uint id)
        {
            _statuses.Remove(id);
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

        /// <summary>Anything registered as an action is a real one, for the stub's purposes.</summary>
        public bool IsPlayerAction(uint actionId) => _actions.ContainsKey(actionId);

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

        // Not "under another name" - gone. That is the case worth switching a job off for.
        var lookup = new StubLookup().FromJob(job);
        lookup.MissingAction(DragoonActions.Drakesbane.Id);

        var report = ActionTableVerifier.Verify(job, lookup);

        Assert.False(report.IsSafeToRun);
        Assert.Equal(1, report.UnresolvedCount);
        Assert.Contains("UNRESOLVED", report.Summarise());
    }

    /// <summary>
    /// A status that cannot be found simply reads as absent, so the rule depending on it
    /// declines to fire. Losing one condition is a far better outcome than switching the
    /// whole job off, which is what an unresolvable action does.
    /// </summary>
    [Fact]
    public void AnUnresolvableStatusDegradesRatherThanDisablingTheJob()
    {
        var job = JobRotationBase.Create<DragoonRotation>();

        var lookup = new StubLookup().FromJob(job);
        lookup.MissingStatus(DragoonActions.PowerSurge.Id);

        var report = ActionTableVerifier.Verify(job, lookup);

        Assert.True(report.IsSafeToRun);
        Assert.Equal(1, report.UnresolvedCount);
        Assert.Equal(0, report.UnresolvedActionCount);
    }

    [Fact]
    public void NoTableContainsPvpEntries()
    {
        // PvP has its own action set; a PvP status name does not exist in the PvE sheet, so
        // one left in a table would fail to resolve for every player.
        foreach (var job in JobRegistry.CreateAll())
        {
            Assert.DoesNotContain(job.AllActions, a => a.Name.Contains("Pv", StringComparison.Ordinal));
            Assert.DoesNotContain(job.AllStatuses, s => s.Name.Contains("Pv", StringComparison.Ordinal));
        }
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

    /// <summary>
    /// The tables are generated from BossMod, which gives working names to things the game
    /// gives the same name - Fuma Ten, Fuma Chi and Fuma Jin are all "Fuma Shuriken" to the
    /// game. That is not a wrong id and must not switch a whole job off, which is what it
    /// used to do: five of thirteen jobs were disabled over it.
    /// </summary>
    [Fact]
    public void ALabelThatIsNotTheGamesNameKeepsTheIdAndRunsTheJob()
    {
        var job = JobRegistry.Create(30)!; // Ninja, which has the most of them
        var lookup = new StubLookup();

        // The game's view: every id is real, but three of them share one name.
        foreach (var action in job.AllActions)
        {
            var sheetName = action.Name switch
            {
                "Fuma Jin" or "Fuma Chi" or "Fuma Ten" => "Fuma Shuriken",
                "Ten II" => "Ten",
                "Chi II" => "Chi",
                "Jin II" => "Jin",
                var n when n.StartsWith("TCJ ", StringComparison.Ordinal) => n[4..],
                var n => n,
            };

            lookup.Action(action.Id, sheetName);
        }

        foreach (var status in job.AllStatuses)
            lookup.Status(status.Id, status.Name);

        var report = ActionTableVerifier.Verify(job, lookup);

        Assert.True(report.IsSafeToRun, report.Summarise());
        Assert.Equal(0, report.UnresolvedActionCount);
        Assert.True(report.AliasedCount > 0, "expected the aliased entries to be recognised as such");

        // And crucially the ids are untouched - an alias must never rebind.
        Assert.Equal(18875u, NinjaActions.FumaJin.Id);
        Assert.Equal(18873u, NinjaActions.FumaTen.Id);
    }

    /// <summary>
    /// The counterpart to the test above, and the distinction the whole change rests on: a
    /// real id under a name the table does not use is an alias and must keep running, where
    /// an id that is not in the game's data at all still switches the job off.
    /// </summary>
    [Fact]
    public void ARealIdUnderAnotherNameAliasesInsteadOfDisablingTheJob()
    {
        var job = JobRotationBase.Create<DragoonRotation>();
        var seeded = DragoonActions.Drakesbane.Id;

        var lookup = new StubLookup().FromJob(job);
        lookup.Action(seeded, "Something The Game Calls It Instead");

        var report = ActionTableVerifier.Verify(job, lookup);

        Assert.True(report.IsSafeToRun, report.Summarise());
        Assert.Equal(0, report.UnresolvedCount);
        Assert.Contains(report.Entries, e => e.Outcome == VerificationOutcome.Aliased);

        // The id is kept exactly as seeded - aliasing must never rebind.
        Assert.Equal(seeded, DragoonActions.Drakesbane.Id);
    }
}
