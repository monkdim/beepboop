using TwoButton.Core.Jobs.Dragoon;
using TwoButton.Core.Jobs.Machinist;
using TwoButton.Core.Jobs.Monk;
using TwoButton.Core.Jobs.Ninja;
using TwoButton.Core.Jobs.Reaper;
using TwoButton.Core.Jobs.Samurai;

namespace TwoButton.Core.Jobs;

/// <summary>
/// Every rotation the plugin knows about. Adding a job means adding one line here and one
/// pair of files under <c>Jobs/</c> - see <c>docs/ADDING_A_JOB.md</c>.
/// </summary>
public static class JobRegistry
{
    private static readonly Func<IJobRotation>[] Factories =
    [
        JobRotationBase.Create<DragoonRotation>,
        JobRotationBase.Create<MachinistRotation>,
        JobRotationBase.Create<SamuraiRotation>,
        JobRotationBase.Create<ReaperRotation>,
        JobRotationBase.Create<MonkRotation>,
        JobRotationBase.Create<NinjaRotation>,
    ];

    /// <summary>Builds a fresh instance of every supported rotation.</summary>
    public static IReadOnlyList<IJobRotation> CreateAll()
    {
        var rotations = new List<IJobRotation>(Factories.Length);
        foreach (var factory in Factories)
            rotations.Add(factory());

        return rotations;
    }

    /// <summary>Builds the rotation for a ClassJob id, or null if the job is not supported yet.</summary>
    public static IJobRotation? Create(uint jobId)
    {
        foreach (var factory in Factories)
        {
            var rotation = factory();
            if (rotation.JobId == jobId)
                return rotation;
        }

        return null;
    }

    private static readonly HashSet<uint> SupportedIds = BuildSupportedIds();

    /// <summary>Cheap enough to call every frame, unlike <see cref="Create"/>.</summary>
    public static bool IsSupported(uint jobId) => SupportedIds.Contains(jobId);

    private static HashSet<uint> BuildSupportedIds()
    {
        var ids = new HashSet<uint>();
        foreach (var factory in Factories)
            ids.Add(factory().JobId);

        return ids;
    }
}
