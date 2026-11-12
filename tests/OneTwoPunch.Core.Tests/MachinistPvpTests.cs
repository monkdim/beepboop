using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Machinist;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

public sealed class MachinistPvpTests
{
    [Fact]
    public void FullMetalFieldLeadsIntoWildfireAndBraveryBeforeBlazingShot()
    {
        var job = JobRotationBase.Create<MachinistPvpRotation>();
        var session = new RotationSession(job, new RotationSettings { SuggestionHoldSeconds = 0 });
        var actions = new FakeActionState();

        Press("Full Metal Field", 0);
        actions.Resolving(job.SingleTargetButton.Id, MachinistPvpRotation.BlazingShotId);
        Press("Wildfire", 1.8f);
        actions.OnCooldown(0x72E1, 24);
        Press("Bravery", 1.1f);
        actions.OnCooldown(0xA8F2, 30);
        Press("Blazing Shot", 0);
        Press("Bishop Autoturret", 0.9f);

        void Press(string name, float gcdRemaining)
        {
            var snapshot = new SnapshotBuilder().Job(31).Gcd(gcdRemaining, 2.4f).Build();
            var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);
            Assert.Equal(name, suggestion.Action.Name);
            session.NotifyActionUsed(suggestion.Action.Id);
        }
    }

    [Fact]
    public void AnalysisIsWovenBeforeThePrimedToolAndBlastChargeFillsTheGap()
    {
        var session = new RotationSession(JobRotationBase.Create<MachinistPvpRotation>(),
            new RotationSettings { SuggestionHoldSeconds = 0 });
        var actions = new FakeActionState().OnCooldown(0xA1FD, 20).Resolving(0x72DD, 0x72DF);

        Assert.Equal("Analysis", Suggest(1.6f));
        Assert.Equal("Air Anchor", Suggest(0));

        actions.OnCooldown(0x72DF, 8);
        Assert.Equal("Blast Charge", Suggest(0));

        string Suggest(float gcdRemaining) =>
            session.Resolve(RotationMode.SingleTarget,
                new SnapshotBuilder().Job(31).Gcd(gcdRemaining, 2.4f).Build(), actions).Action.Name;
    }
}
