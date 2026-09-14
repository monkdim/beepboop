using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Beastmaster;
using OneTwoPunch.Core.Jobs.Ninja;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// A rule that never fires leaves no trace in a log. The gauge line says what the player
/// has; this says what the engine believes it may suggest, and the gap between the two is
/// where the silence hides. Two recorded Ninja pulls went past the mudras in every weave
/// window for eighty seconds and neither could say why.
/// </summary>
public sealed class ReadinessReportTests
{
    [Fact]
    public void NinjaReportsTheMudrasAndWhatWaitsBehindThem()
    {
        var job = JobRotationBase.Create<NinjaRotation>();

        var line = job.DescribeReadiness(
            new SnapshotBuilder().Job(30).Build(), new FakeActionState())!;

        Assert.NotNull(line);
        foreach (var name in new[] { "Ten", "Chi", "Jin", "Ninjutsu", "Ten Chi Jin", "Kunais Bane" })
            Assert.Contains(name, line);
    }

    [Fact]
    public void BeastmasterReportsTrickAndTheRing()
    {
        var job = JobRotationBase.Create<BeastmasterRotation>();

        var line = job.DescribeReadiness(
            new SnapshotBuilder().Job(43).Build(), new FakeActionState())!;

        foreach (var name in new[] { "Trick", "Gale Axe", "Spinning Axe", "Mistral Axe", "Avalanche Axe" })
            Assert.Contains(name, line);
    }

    /// <summary>
    /// The facts that decide whether a rule may offer an action, distinguishable at a glance
    /// - and, when it is refused, the game's own reason. That last part is the point: three
    /// rounds went by on "refused" with no way to see that the reason was the target.
    /// </summary>
    [Fact]
    public void TheProbeTellsRefusedApartFromUncharged()
    {
        var job = JobRotationBase.Create<NinjaRotation>();
        var snapshot = new SnapshotBuilder().Job(30).Build();

        var healthy = job.DescribeReadiness(snapshot, new FakeActionState())!;
        Assert.Contains("Ten=y", healthy);
        Assert.DoesNotContain("#", healthy);

        var refused = job.DescribeReadiness(
            snapshot, new FakeActionState().Unusable(NinjaActions.Ten1.Id))!;
        Assert.Contains("Ten=n", refused);
        Assert.Contains("#566", refused);

        var uncharged = job.DescribeReadiness(
            snapshot, new FakeActionState().WithCharges(NinjaActions.Ten1.Id, 0, 2))!;
        Assert.Contains("Ten=y0/2", uncharged);
    }

    /// <summary>A job that has not needed one says nothing rather than padding the line.</summary>
    [Fact]
    public void AJobWithoutOneIsSilent()
    {
        var job = JobRotationBase.Create<OneTwoPunch.Core.Jobs.Dragoon.DragoonRotation>();

        Assert.Null(job.DescribeReadiness(
            new SnapshotBuilder().Job(22).Build(), new FakeActionState()));
    }
}
