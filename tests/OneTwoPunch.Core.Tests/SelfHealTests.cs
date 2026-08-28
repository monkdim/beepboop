using OneTwoPunch.Core.Engine;
using OneTwoPunch.Core.Jobs;
using OneTwoPunch.Core.Model;
using Xunit;

namespace OneTwoPunch.Core.Tests;

/// <summary>
/// Second Wind on the melee jobs. It is two minutes of cooldown doing nothing for most of a
/// fight, and noticing the moment to press it is the sort of attention this plugin exists to
/// not need - so once you are hurt it takes the first weave slot going, ahead of the burst.
/// </summary>
public sealed class SelfHealTests
{
    /// <summary>Second Wind, the melee and ranged role action.</summary>
    private const uint SecondWind = 7541u;

    /// <summary>Every job that has it in a rule: the six melee.</summary>
    public static TheoryData<uint, string> MeleeJobs() => new()
    {
        { 20, "Monk" },
        { 22, "Dragoon" },
        { 30, "Ninja" },
        { 34, "Samurai" },
        { 39, "Reaper" },
        { 41, "Viper" },
    };

    private static Suggestion Resolve(uint jobId, SnapshotBuilder builder, RotationSettings settings) =>
        new RotationSession(JobRegistry.Create(jobId)!, settings)
            .Resolve(RotationMode.SingleTarget, builder.Build(), new FakeActionState());

    private static RotationSettings Settings() => new()
    {
        UseOpener = false,
        SuggestionHoldSeconds = 0f,
    };

    /// <summary>Mid-global, so an off-global can be suggested at all.</summary>
    private static SnapshotBuilder Weaving(uint jobId) =>
        new SnapshotBuilder().Job(jobId).Level(100).Gcd(2f).Enemies(1).NoCombo();

    [Theory]
    [MemberData(nameof(MeleeJobs))]
    public void AHurtMeleeIsToldToHealItself(uint jobId, string name)
    {
        var suggestion = Resolve(jobId, Weaving(jobId).Hp(0.5f), Settings());

        Assert.True(
            suggestion.Action.Id == SecondWind,
            $"{name} suggested {suggestion.Action.Name} at half health instead of Second Wind");
    }

    /// <summary>And a healthy one is not: the rotation is what the button is for.</summary>
    [Theory]
    [MemberData(nameof(MeleeJobs))]
    public void AHealthyMeleeIsNot(uint jobId, string name)
    {
        var suggestion = Resolve(jobId, Weaving(jobId).Hp(1f), Settings());

        Assert.True(
            suggestion.Action.Id != SecondWind,
            $"{name} suggested Second Wind at full health");
    }

    /// <summary>Just above the mark is still healthy.</summary>
    [Fact]
    public void TheThresholdIsWhereItSays()
    {
        var settings = Settings();

        Assert.NotEqual(SecondWind, Resolve(41, Weaving(41).Hp(0.76f), settings).Action.Id);
        Assert.Equal(SecondWind, Resolve(41, Weaving(41).Hp(0.75f), settings).Action.Id);
    }

    /// <summary>And it can be moved, or turned off entirely.</summary>
    [Fact]
    public void TheThresholdMoves()
    {
        var settings = Settings();
        settings.SelfHealBelowHp = 0.4f;

        Assert.NotEqual(SecondWind, Resolve(41, Weaving(41).Hp(0.5f), settings).Action.Id);
        Assert.Equal(SecondWind, Resolve(41, Weaving(41).Hp(0.3f), settings).Action.Id);
    }

    [Fact]
    public void ItCanBeTurnedOff()
    {
        var settings = Settings();
        settings.SuggestSelfHeal = false;

        Assert.NotEqual(SecondWind, Resolve(41, Weaving(41).Hp(0.1f), settings).Action.Id);
    }
}
