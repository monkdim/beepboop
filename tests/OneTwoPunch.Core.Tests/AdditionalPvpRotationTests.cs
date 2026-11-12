using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

public sealed class AdditionalPvpRotationTests
{
    [Fact]
    public void BlackMageUsesParadoxThenHighFireBeforeSwitchingToBlizzard()
    {
        var session = Session(25);
        var actions = new FakeActionState().OnCooldown(0x73DA, 8);
        Assert.Equal("Paradox", Suggest(0xC8C));

        actions.Unusable(0x73DF).Resolving(0x73D1, 0xA201);
        Assert.Equal("High Fire II", Suggest(0xD35));

        actions.Resolving(0x73D1, 30896);
        Assert.Equal("Blizzard", Suggest(0xC8C));

        string Suggest(uint status) => session.Resolve(RotationMode.Aoe,
            new SnapshotBuilder().Job(25).Gcd(0f).Buff(status)
                .Gauge(snapshot => snapshot.TargetDistance = 15f).Build(), actions).Action.Name;
    }

    [Fact]
    public void GunbreakerWeavesContinuationAndKeepsTheNativeChainGoing()
    {
        var session = Session(37);
        var actions = new FakeActionState();
        Assert.Equal("Gnashing Fang", Suggest(0f));
        session.NotifyActionUsed(0x71AE);

        actions.Resolving(0x71AE, 0x71AF);
        Assert.Equal("Jugular Rip", Suggest(1.6f));
        session.NotifyActionUsed(0x71B4);
        Assert.Equal("Savage Claw", Suggest(0f));

        string Suggest(float gcd) => session.Resolve(RotationMode.SingleTarget,
            new SnapshotBuilder().Job(37).Gcd(gcd, 2.2f).Buff(0x7D2).Build(), actions).Action.Name;
    }

    private static RotationSession Session(uint jobId) =>
        new(JobRegistry.CreatePvp(jobId)!, new RotationSettings { SuggestionHoldSeconds = 0 });
}
