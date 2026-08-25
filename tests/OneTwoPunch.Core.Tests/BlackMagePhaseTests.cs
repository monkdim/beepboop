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

    /// <summary>
    /// Running the bar dry in Astral Fire has to leave Astral Fire. The escape rule asked
    /// for "not in fire", so in fire with no mana nothing could match at all - Fire IV and
    /// Despair both want mana - and the list fell through to its fallback and cast Fire I
    /// for ever. Reported as "no mp to cast the next one and so nothing is working now".
    /// </summary>
    [Fact]
    public void OutOfManaInFireGoesToIce()
    {
        var job = JobRegistry.Create(25)!;

        var snapshot = new SnapshotBuilder()
            .Job(25)
            .Mp(0)
            .Gcd(0f)
            .NoCombo()
            .Enemies(1)
            .Gauge(s => s.Gauges.BlackMage.AstralFire = 3)
            .Build();

        // Everything that costs mana is unusable, which is what an empty bar means.
        var actions = new FakeActionState()
            .Unusable(BlackMageActions.Fire4.Id)
            .Unusable(BlackMageActions.Despair.Id)
            .Unusable(BlackMageActions.Fire1.Id);

        var session = new RotationSession(job, new RotationSettings { UseOpener = false });
        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(BlackMageActions.Blizzard3.Id, suggestion.Action.Id);
    }
}
