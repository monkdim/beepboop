using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Bard;
using OneTwoPunch.Core.Model;
using Xunit;
using B = OneTwoPunch.Core.Jobs.Bard.BardActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The song that is playing decides when the next one starts, not the three independent
/// two-minute cooldowns. The Balance: "leaving as the timer hits 3-2".
/// </summary>
public sealed class BardSongCycleTests
{
    /// <summary>Any song id will do for these; which number is which is not what is on test.</summary>
    private const byte SomeSong = 1;

    private static RotationSession Session() =>
        new(JobRotationBase.Create<BardRotation>(),
            new RotationSettings { WeaveStyle = WeaveStyle.Double, UseOpener = false, SuggestionHoldSeconds = 0f });

    /// <summary>
    /// Radiant Finale, Battle Voice and Raging Strikes all sit above the songs in the
    /// off-global list, so they are parked to leave the song rules as the thing under test.
    /// </summary>
    private static FakeActionState Idle() =>
        new FakeActionState()
            .OnCooldown(B.BattleVoice.Id, 60f)
            .OnCooldown(B.RagingStrikes.Id, 60f)
            .OnCooldown(B.RadiantFinale.Id, 60f);

    private static CombatSnapshot Playing(byte songId, float remaining) =>
        new SnapshotBuilder()
            .Job(23).Level(100).Gcd(1.6f).Enemies(1)
            .Gauge(s =>
            {
                s.Gauges.Bard.SongId = songId;
                s.Gauges.Bard.SongTimeRemaining = remaining;
            })
            .Build();

    /// <summary>
    /// The recorded pull: Mage's Ballad at 00:20.3 and Army's Paeon at 00:20.9, six tenths
    /// of a second apart, because both cooldowns were up and neither rule looked at the song
    /// that was already playing.
    /// </summary>
    [Fact]
    public void ASongThatJustStartedIsNotImmediatelyReplaced()
    {
        var session = Session();

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Playing(SomeSong, 44f), Idle());

        Assert.NotEqual(B.ArmysPaeon.Id, suggestion.Action.Id);
        Assert.NotEqual(B.WanderersMinuet.Id, suggestion.Action.Id);
        Assert.NotEqual(B.MagesBallad.Id, suggestion.Action.Id);
    }

    /// <summary>Repertoire only ticks under a song, so silence is always filled.</summary>
    [Fact]
    public void WithNoSongPlayingTheStrongestIsTakenAtOnce()
    {
        var session = Session();
        var snapshot = new SnapshotBuilder().Job(23).Level(100).Gcd(1.6f).Enemies(1).Build();

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, Idle());

        Assert.Equal(B.WanderersMinuet.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// At the hand-off the next song is taken. A song runs at most forty-five seconds
    /// against a two minute recast, so the one playing is still turning and the ordering
    /// picks the strongest of what is actually back.
    /// </summary>
    [Theory]
    [InlineData(3f)]
    [InlineData(2f)]
    public void TheNextSongIsTakenAtTheHandOff(float remaining)
    {
        var session = Session();
        var actions = Idle()
            .OnCooldown(B.WanderersMinuet.Id, 60f)
            .OnCooldown(B.MagesBallad.Id, 77f);

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Playing(SomeSong, remaining), actions);

        Assert.Equal(B.ArmysPaeon.Id, suggestion.Action.Id);
    }

    /// <summary>Half a song still to run is not a hand-off.</summary>
    [Fact]
    public void ASongWithTimeLeftIsNotHandedOver()
    {
        var session = Session();

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Playing(SomeSong, 20f), Idle());

        Assert.NotEqual(B.WanderersMinuet.Id, suggestion.Action.Id);
        Assert.NotEqual(B.MagesBallad.Id, suggestion.Action.Id);
        Assert.NotEqual(B.ArmysPaeon.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The recorded log carries the raw song id, which is what will settle the one thing the
    /// rules still cannot tell: which number is Army's Paeon, so it can be left early.
    /// </summary>
    [Fact]
    public void TheGaugeLineNamesTheSongIdAndItsTimer()
    {
        var job = JobRotationBase.Create<BardRotation>();

        var line = job.DescribeGauge(Playing(3, 41.5f)) ?? string.Empty;

        Assert.Contains("song 3", line);
        Assert.Contains("41.5s", line);
        Assert.Contains("repertoire", line);
        Assert.Contains("coda", line);
    }

    /// <summary>With nothing playing the line says so rather than printing a zero.</summary>
    [Fact]
    public void TheGaugeLineSaysWhenThereIsNoSong()
    {
        var job = JobRotationBase.Create<BardRotation>();
        var snapshot = new SnapshotBuilder().Job(23).Level(100).Build();

        Assert.Contains("no song", job.DescribeGauge(snapshot) ?? string.Empty);
    }
}
