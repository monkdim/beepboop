using OneTwoPunch.Core.Jobs;
using Xunit;

namespace OneTwoPunch.Core.Tests;

public sealed class JobRegistryTests
{
    /// <summary>
    /// The registry writes each ClassJob id next to its factory so that looking a job up
    /// does not have to build every rotation to ask. That is a duplicate of the id the
    /// rotation itself declares, and a duplicate that drifts would silently hand the player
    /// another job's rotation - so it is checked here rather than trusted.
    /// </summary>
    [Fact]
    public void DeclaredJobIdMatchesTheRotationsOwn()
    {
        foreach (var (jobId, factory) in JobRegistry.Factories)
        {
            var rotation = factory();
            Assert.True(
                jobId == rotation.JobId,
                $"JobRegistry lists {rotation.Name} under job id {jobId}, "
                + $"but the rotation declares {rotation.JobId}.");
        }
    }

    [Fact]
    public void EveryJobIdIsListedOnlyOnce()
    {
        var seen = new HashSet<uint>();
        foreach (var (jobId, _) in JobRegistry.Factories)
            Assert.True(seen.Add(jobId), $"job id {jobId} is listed twice");
    }

    [Fact]
    public void CreateReturnsTheRightRotationForEveryListedJob()
    {
        foreach (var (jobId, _) in JobRegistry.Factories)
        {
            var rotation = JobRegistry.Create(jobId);
            Assert.NotNull(rotation);
            Assert.Equal(jobId, rotation!.JobId);
            Assert.True(JobRegistry.IsSupported(jobId));
        }
    }

    [Fact]
    public void UnsupportedJobIsNotClaimed()
    {
        // 1 is Gladiator: a real ClassJob, and not a DPS rotation this plugin supports.
        Assert.False(JobRegistry.IsSupported(1));
        Assert.Null(JobRegistry.Create(1));
        Assert.False(JobRegistry.IsSupported(0));
    }
}
