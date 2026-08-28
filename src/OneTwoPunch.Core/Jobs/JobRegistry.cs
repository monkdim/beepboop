using OneTwoPunch.Core.Jobs.Bard;
using OneTwoPunch.Core.Jobs.BlackMage;
using OneTwoPunch.Core.Jobs.Dancer;
using OneTwoPunch.Core.Jobs.DarkKnight;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Jobs.Gunbreaker;
using OneTwoPunch.Core.Jobs.Machinist;
using OneTwoPunch.Core.Jobs.Monk;
using OneTwoPunch.Core.Jobs.Ninja;
using OneTwoPunch.Core.Jobs.Paladin;
using OneTwoPunch.Core.Jobs.Pictomancer;
using OneTwoPunch.Core.Jobs.RedMage;
using OneTwoPunch.Core.Jobs.Reaper;
using OneTwoPunch.Core.Jobs.Samurai;
using OneTwoPunch.Core.Jobs.Summoner;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Jobs.Warrior;

namespace OneTwoPunch.Core.Jobs;

/// <summary>
/// Every rotation the plugin knows about. Adding a job means adding one line here and one
/// pair of files under <c>Jobs/</c> - see <c>docs/ADDING_A_JOB.md</c>.
/// <para>
/// The ClassJob id is written next to each factory rather than read back out of a
/// constructed rotation. Building a rotation means building its whole priority list, and
/// asking every one of them what job it is - which is what this did - meant building all
/// of them to answer a question about one. The duplicated id is checked against the
/// rotation's own <see cref="IJobRotation.JobId"/> by a test, so it cannot drift.
/// </para>
/// </summary>
public static class JobRegistry
{
    public static readonly (uint JobId, Func<IJobRotation> Factory)[] Factories =
    [
        (19, JobRotationBase.Create<PaladinRotation>),
        (20, JobRotationBase.Create<MonkRotation>),
        (21, JobRotationBase.Create<WarriorRotation>),
        (22, JobRotationBase.Create<DragoonRotation>),
        (23, JobRotationBase.Create<BardRotation>),
        (25, JobRotationBase.Create<BlackMageRotation>),
        (27, JobRotationBase.Create<SummonerRotation>),
        (30, JobRotationBase.Create<NinjaRotation>),
        (31, JobRotationBase.Create<MachinistRotation>),
        (32, JobRotationBase.Create<DarkKnightRotation>),
        (34, JobRotationBase.Create<SamuraiRotation>),
        (35, JobRotationBase.Create<RedMageRotation>),
        (37, JobRotationBase.Create<GunbreakerRotation>),
        (38, JobRotationBase.Create<DancerRotation>),
        (39, JobRotationBase.Create<ReaperRotation>),
        (41, JobRotationBase.Create<ViperRotation>),
        (42, JobRotationBase.Create<PictomancerRotation>),
    ];

    /// <summary>Builds a fresh instance of every supported rotation.</summary>
    public static IReadOnlyList<IJobRotation> CreateAll()
    {
        var rotations = new List<IJobRotation>(Factories.Length);
        foreach (var entry in Factories)
            rotations.Add(entry.Factory());

        return rotations;
    }

    /// <summary>
    /// Builds the rotation for a ClassJob id, or null if the job is not supported yet.
    /// Exactly one rotation is ever constructed.
    /// </summary>
    public static IJobRotation? Create(uint jobId)
    {
        foreach (var entry in Factories)
        {
            if (entry.JobId == jobId)
                return entry.Factory();
        }

        return null;
    }

    /// <summary>Costs nothing, and builds nothing.</summary>
    public static bool IsSupported(uint jobId)
    {
        foreach (var entry in Factories)
        {
            if (entry.JobId == jobId)
                return true;
        }

        return false;
    }
}
