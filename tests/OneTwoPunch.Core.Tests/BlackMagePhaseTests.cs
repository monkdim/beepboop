using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.BlackMage;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Which element a phaseless Black Mage opens into is decided by mana, not by habit. The
/// list used to answer "into Umbral Ice" unconditionally, so a full-mana pull opened into
/// ice - a wasted opener every single time, and the reported symptom.
/// </summary>
public sealed class BlackMagePhaseTests
{
    private static ActionRef OpensWith(uint mp)
    {
        var job = JobRegistry.Create(25)!;

        // Neither Astral Fire nor Umbral Ice: a pull, or after a death or long downtime.
        var snapshot = new SnapshotBuilder()
            .Job(25)
            .Mp(mp)
            .Gcd(0f)
            .NoCombo()
            .Enemies(1)
            .Build();

        var session = new RotationSession(job, new RotationSettings { UseOpener = false });
        return session.Resolve(RotationMode.SingleTarget, snapshot, new FakeActionState()).Action;
    }

    [Fact]
    public void AFullManaPullOpensInFire()
    {
        Assert.Equal(BlackMageActions.Fire3.Id, OpensWith(10000).Id);
    }

    [Fact]
    public void AnEmptyManaPullOpensInIceToRefill()
    {
        Assert.Equal(BlackMageActions.Blizzard3.Id, OpensWith(1000).Id);
    }
}
