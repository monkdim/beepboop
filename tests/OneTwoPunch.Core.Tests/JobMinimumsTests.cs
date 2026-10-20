using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Jobs.Dragoon;
using OneTwoPunch.Core.Jobs.Viper;
using OneTwoPunch.Core.Model;
using Xunit;
using V = OneTwoPunch.Core.Jobs.Viper.ViperActions;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// A job's weave recommendation never overrides the player; its area threshold still
/// determines when an optional fallback to single target is worthwhile.
/// </summary>
public sealed class JobMinimumsTests
{
    // ---- Input ceiling ---------------------------------------------------

    /// <summary>
    /// Viper recommends two weaves but the player's single-weave limit wins.
    /// </summary>
    [Fact]
    public void ViperRespectsTheSingleWeaveLimit()
    {
        var session = new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { WeaveStyle = WeaveStyle.Single, UseOpener = false, SuggestionHoldSeconds = 0f });

        Assert.Equal(1, session.WeaveBudget);

        // Twinfang Bite has gone; on a single-weave budget Twinblood Bite would not be offered.
        var snapshot = new SnapshotBuilder()
            .Job(41).Level(100).Gcd(1.6f).Enemies(1)
            .Buff(V.SwiftskinsVenom.Id, 30f)
            .Build();

        var actions = new FakeActionState().OnCooldown(V.SerpentsIre.Id, 60f);

        session.Resolve(RotationMode.SingleTarget, snapshot, actions);
        session.NotifyActionUsed(V.TwinfangBite.Id);
        var second = session.Resolve(RotationMode.SingleTarget, snapshot, actions);

        Assert.Equal(ActionKind.Gcd, second.Kind);
    }

    /// <summary>"Globals only" is the player's to choose, and the job does not get to override it.</summary>
    [Fact]
    public void GlobalsOnlyIsRespectedEvenByAJobThatAsksForTwo()
    {
        var session = new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { WeaveStyle = WeaveStyle.None, UseOpener = false, SuggestionHoldSeconds = 0f });

        Assert.Equal(0, session.WeaveBudget);
    }

    /// <summary>A job with no minimum takes the setting as it is.</summary>
    [Fact]
    public void AJobWithNoMinimumKeepsThePlayersSetting()
    {
        var session = new RotationSession(JobRotationBase.Create<DragoonRotation>(),
            new RotationSettings { WeaveStyle = WeaveStyle.Single });

        Assert.Equal(1, session.WeaveBudget);
    }

    // ---- Area threshold ---------------------------------------------------

    /// <summary>
    /// "It is only a gain to use the AoE forms when fighting three or more targets. For one
    /// or two enemies, continue to use the single target versions."
    /// </summary>
    [Fact]
    public void ViperFallsBackToSingleTargetOnTwoEnemies()
    {
        var session = new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { AoeFallsBackToSingleTarget = true, UseOpener = false, SuggestionHoldSeconds = 0f });

        var snapshot = new SnapshotBuilder().Job(41).Level(100).Gcd(0f).NoCombo().Enemies(2).Build();
        var actions = new FakeActionState().OnCooldown(V.Vicewinder.Id, 30f).OnCooldown(V.Vicepit.Id, 30f);

        var suggestion = session.Resolve(RotationMode.Aoe, snapshot, actions);

        Assert.Equal(V.SteelFangs.Id, suggestion.Action.Id);
        Assert.Contains("2 enemies", suggestion.Note ?? string.Empty);
    }

    /// <summary>And stays on the area list at three.</summary>
    [Fact]
    public void ViperStaysOnTheAreaListAtThreeEnemies()
    {
        var session = new RotationSession(JobRotationBase.Create<ViperRotation>(),
            new RotationSettings { AoeFallsBackToSingleTarget = true, UseOpener = false, SuggestionHoldSeconds = 0f });

        var snapshot = new SnapshotBuilder().Job(41).Level(100).Gcd(0f).NoCombo().Enemies(3).Build();
        var actions = new FakeActionState().OnCooldown(V.Vicewinder.Id, 30f).OnCooldown(V.Vicepit.Id, 30f);

        var suggestion = session.Resolve(RotationMode.Aoe, snapshot, actions);

        Assert.Equal(V.SteelMaw.Id, suggestion.Action.Id);
    }

    /// <summary>The default is unchanged: two enemies is an area pull for everyone else.</summary>
    [Fact]
    public void TheDefaultThresholdIsStillTwo()
    {
        Assert.Equal(2, JobRotationBase.Create<DragoonRotation>().AoeMinimumEnemies);
    }
}
