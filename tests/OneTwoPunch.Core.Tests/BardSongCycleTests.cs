using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Bard;
using OneTwoPunch.Core.Model;
using Xunit;
using B = OneTwoPunch.Core.Jobs.Bard.BardActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// The song that is playing decides when the next one starts, not the three independent
/// two-minute cooldowns. The Balance: "~43 seconds in Wanderer's Minuet, ~43 seconds in
/// Mage's Ballad (leaving as the timer hits 3-2 in both cases), then ~34 seconds in Army's
/// Paeon (leaving at 12-10 on the timer)."
/// </summary>
public sealed class BardSongCycleTests
{
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

    private static CombatSnapshot Playing(BardSong song, float remaining) =>
        new SnapshotBuilder()
            .Job(23).Level(100).Gcd(1.6f).Enemies(1)
            .Gauge(s =>
            {
                s.Gauges.Bard.Song = song;
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
        var snapshot = Playing(BardSong.MagesBallad, 44f);

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, Idle());

        Assert.NotEqual(B.ArmysPaeon.Id, suggestion.Action.Id);
        Assert.NotEqual(B.WanderersMinuet.Id, suggestion.Action.Id);
    }

    /// <summary>Repertoire only ticks under a song, so silence is always filled.</summary>
    [Fact]
    public void WithNoSongPlayingTheStrongestIsTakenAtOnce()
    {
        var session = Session();
        var snapshot = Playing(BardSong.None, 0f);

        var suggestion = session.Resolve(RotationMode.SingleTarget, snapshot, Idle());

        Assert.Equal(B.WanderersMinuet.Id, suggestion.Action.Id);
    }

    /// <summary>The last proc chance is at three seconds, so that is the hand-off.</summary>
    [Theory]
    [InlineData(3f)]
    [InlineData(2f)]
    public void MinuetAndBalladRunToThreeSeconds(float remaining)
    {
        var session = Session();
        var actions = Idle().OnCooldown(B.WanderersMinuet.Id, 60f);

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Playing(BardSong.MagesBallad, remaining), actions);

        Assert.Equal(B.ArmysPaeon.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// Army's Paeon is the weakest of the three and is the one cut short - left at 12-10 on
    /// the timer rather than run to three like the other two.
    /// </summary>
    [Fact]
    public void PaeonIsLeftEarlyWithTimeStillOnIt()
    {
        var session = Session();
        var actions = Idle().OnCooldown(B.MagesBallad.Id, 60f);

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Playing(BardSong.ArmysPaeon, 11f), actions);

        Assert.Equal(B.WanderersMinuet.Id, suggestion.Action.Id);
    }

    /// <summary>
    /// The song playing is never the answer, even at its own hand-off. Its two minute
    /// cooldown normally hides it, but resting on that would make the cycle only as right
    /// as the cooldown bookkeeping - and restarting the song you are in throws away every
    /// Repertoire stack banked under it. This is what CI caught: Ballad at two seconds
    /// came back as Ballad again rather than handing over to Paeon.
    /// </summary>
    [Fact]
    public void TheSongPlayingIsNeverOfferedAgain()
    {
        var session = Session();
        var actions = Idle().OnCooldown(B.WanderersMinuet.Id, 60f);

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Playing(BardSong.MagesBallad, 2f), actions);

        Assert.NotEqual(B.MagesBallad.Id, suggestion.Action.Id);
    }

    /// <summary>But not so early that a third of the song is thrown away.</summary>
    [Fact]
    public void PaeonIsNotLeftTheMomentItStarts()
    {
        var session = Session();

        var suggestion = session.Resolve(
            RotationMode.SingleTarget, Playing(BardSong.ArmysPaeon, 30f), Idle());

        Assert.NotEqual(B.WanderersMinuet.Id, suggestion.Action.Id);
        Assert.NotEqual(B.MagesBallad.Id, suggestion.Action.Id);
    }
}
